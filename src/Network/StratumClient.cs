using System.Net.Sockets;
using System.Text;
using LotteryBitcoinMiner.Configuration;
using LotteryBitcoinMiner.Core;

namespace LotteryBitcoinMiner.Network
{
    public class StratumClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly MinerConfig _config;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private int _messageId = 1;
        private bool _isConnected = false;
        private bool _isSubscribed = false;
        private bool _isAuthorized = false;
        private string _extraNonce1 = "";
        private int _extraNonce2Size = 4;
        private uint _extraNonce2Counter = 0;
        private string _receivedData = "";
        private readonly Dictionary<int, string> _pendingRequests = new Dictionary<int, string>();

        // Events
        public event Action<WorkItem>? WorkReceived;
        public event Action<double>? PoolShareDifficultyChanged;
        public event Action<bool, string>? ShareResult;
        public event Action<string>? StatusChanged;
        public event Action<string>? ErrorOccurred;

        public bool IsConnected => _isConnected;
        public bool IsSubscribed => _isSubscribed;
        public bool IsAuthorized => _isAuthorized;

        public StratumClient(MinerConfig config)
        {
            _config = config;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                StatusChanged?.Invoke("Connecting to pool...");
                
                _tcpClient = new TcpClient(AddressFamily.InterNetwork);
                
                // Use the proven async connection pattern from working code
                _tcpClient.BeginConnect(_config.Pool.Url, _config.Pool.Port, ConnectCallback, _tcpClient);
                
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Connection failed: {ex.Message}");
                await DisconnectAsync();
                return false;
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            if (_tcpClient?.Connected != true)
                return;
            try
            {
                _isConnected = true;
                _stream = _tcpClient.GetStream();
                SendSubscribe();
                SendAuthorize();
                byte[] buffer = new byte[_tcpClient.ReceiveBufferSize];

                // Now we are connected start async read operation.
                _stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);
            }
            catch(Exception ex)
            {
                ErrorOccurred?.Invoke($"Subscribe failed: {ex.Message}");
            }
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            _isSubscribed = false;
            _isAuthorized = false;
            
            _cancellationTokenSource.Cancel();
            
            _stream?.Close();
            _tcpClient?.Close();
            _pendingRequests.Clear();
            
            StatusChanged?.Invoke("Disconnected from pool");
            
            return Task.CompletedTask;
        }

        private void SendSubscribe()
        {
            try
            {
                StatusChanged?.Invoke("Subscribing to mining...");
                
                var subscribeMessage = StratumMessage.CreateRequest(
                    _messageId,
                    StratumMethods.Subscribe
                );
                
                _pendingRequests[_messageId] = StratumMethods.Subscribe;
                _messageId++;
                
                SendMessage(subscribeMessage);
                StatusChanged?.Invoke("Sent mining.subscribe");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Subscribe failed: {ex.Message}");
            }
        }

        private void SendAuthorize()
        {
            try
            {
                StatusChanged?.Invoke("Authorizing worker...");
                
                // Solo.ckpool uses wallet address as username
                var username = _config.Pool.Wallet;
                if (!string.IsNullOrEmpty(_config.Pool.WorkerName))
                {
                    username = $"{_config.Pool.Wallet}.{_config.Pool.WorkerName}";
                }
                
                var authorizeMessage = StratumMessage.CreateRequest(
                    _messageId,
                    StratumMethods.Authorize,
                    username,
                    _config.Pool.Password
                );
                
                _pendingRequests[_messageId] = StratumMethods.Authorize;
                _messageId++;
                
                SendMessage(authorizeMessage);
                StatusChanged?.Invoke("Sent mining.authorize");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Authorization failed: {ex.Message}");
            }
        }

        public Task<bool> SubmitShareAsync(WorkItem work, uint nonce, double difficulty)
        {
            if (!_isAuthorized || work == null)
                return Task.FromResult(false);

            try
            {
                var submitMessage = StratumMessage.CreateRequest(
                    _messageId,
                    StratumMethods.Submit,
                    _config.Pool.Wallet,
                    work.JobId,
                    work.ExtraNonce2,
                    work.Time,
                    nonce.ToString("x8")
                );
                
                _pendingRequests[_messageId] = StratumMethods.Submit;
                _messageId++;
                
                SendMessage(submitMessage);
                StatusChanged?.Invoke($"Submit (Difficulty {difficulty:F2}) from: {work.PoolShareDifficulty}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Share submission failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        private void SendMessage(StratumMessage message)
        {
            if (_stream == null || !_isConnected)
            {
                StatusChanged?.Invoke("Cannot send message: stream is null or not connected");
                return;
            }

            try
            {
                var json = message.ToJson() + "\n";
                var bytes = Encoding.ASCII.GetBytes(json);
                
                _stream.Write(bytes, 0, bytes.Length);
                StatusChanged?.Invoke($"Sending: {json.Trim()}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to send message: {ex.Message}");
                // Try to reconnect like the working code does
                Task.Run(async () => {
                    await DisconnectAsync();
                    await Task.Delay(1000);
                    await ConnectAsync();
                });
            }
        }

        private void ReadCallback(IAsyncResult result)
        {
            if (!_isConnected || _stream == null)
                return;

            int bytesRead;
            byte[] buffer = (byte[])result.AsyncState!;
            
            try
            {
                bytesRead = _stream.EndRead(result);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Socket error: {ex.Message}");
                return;
            }

            if (bytesRead == 0)
            {
                StatusChanged?.Invoke($"{DateTime.Now} Disconnected. Reconnecting...");
                _tcpClient?.Close();
                _tcpClient = null;
                _pendingRequests.Clear();
                Task.Run(async () => await ConnectAsync());
                return;
            }

            // Get the data using the same pattern as working code
            string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            _receivedData += data;

            // Process complete JSON messages (ending with '}')
            int foundClose = _receivedData.IndexOf('}');
            
            while (foundClose > 0)
            {
                string currentMessage = _receivedData.Substring(0, foundClose + 1);
                
                try
                {
                    var message = StratumMessage.FromJson(currentMessage);
                    if (message != null)
                    {
                        StatusChanged?.Invoke($"Received: {currentMessage}");
                        ProcessMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke($"Failed to process message: {ex.Message}");
                }

                // Remove processed message and look for next one
                _receivedData = _receivedData.Remove(0, foundClose + 2);
                foundClose = _receivedData.IndexOf('}');
            }

            // Continue reading
            try
            {
                _stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to continue reading: {ex.Message}");
            }
        }

        private void ProcessMessage(StratumMessage message)
        {
            try
            {
                if (message.IsResponse)
                {
                    HandleResponse(message);
                }
                else if (message.IsNotification)
                {
                    HandleNotification(message);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Message processing error: {ex.Message}");
            }
        }

        private void HandleResponse(StratumMessage message)
        {
            if (message.Error != null)
            {
                ErrorOccurred?.Invoke($"Server error: {message.Error}");
                return;
            }

            // Find the command that this is the response to
            var messageId = Convert.ToInt32(message.Id);
            if (_pendingRequests.TryGetValue(messageId, out string? method))
            {
                _pendingRequests.Remove(messageId);
                
                switch (method)
                {
                    case StratumMethods.Subscribe:
                        HandleSubscribeResponse(message);
                        _isSubscribed = true;
                        StatusChanged?.Invoke("Subscribed to mining");
                        break;
                        
                    case StratumMethods.Authorize:
                        var result = message.Result?.ToString();
                        if (result?.ToLower() == "true")
                        {
                            _isAuthorized = true;
                            StatusChanged?.Invoke("Worker authorized");
                        }
                        else
                        {
                            ErrorOccurred?.Invoke("Worker authorization failed");
                        }
                        break;
                        
                    case StratumMethods.Submit:
                        var submitResult = message.Result?.ToString();
                        bool accepted = submitResult?.ToLower() == "true";
                        // This is the ORIGIN of share statistics - only pool responses count
                        // ShareResult event is the single source of truth for share counting
                        ShareResult?.Invoke(accepted, accepted ? "Share accepted" : "Share rejected");
                        break;
                }
            }
            else
            {
                StatusChanged?.Invoke("Unexpected response");
            }
        }

        private void HandleNotification(StratumMessage message)
        {
                switch (message.Method)
                {
                    case StratumMethods.Notify:
                        HandleMiningNotify(message);
                        break;
                        
                    case StratumMethods.SetDifficulty:
                        HandleSetPoolDifficulty(message);
                        break;
                        
                    case StratumMethods.Reconnect:
                        Task.Run(async () => await HandleReconnect());
                        break;
                        
                    case StratumMethods.ShowMessage:
                        HandleShowMessage(message);
                        break;
                }
        }

        private void HandleMiningNotify(StratumMessage message)
        {
            try
            {
                if (message.Params == null || message.Params.Count < 8)
                    return;

                var jobId = message.GetParamAsString(0);
                var prevHash = message.GetParamAsString(1);
                var coinbase1 = message.GetParamAsString(2);
                var coinbase2 = message.GetParamAsString(3);
                var merkleTree = message.GetParamAsStringArray(4);
                var version = message.GetParamAsString(5);
                var bits = message.GetParamAsString(6);
                var time = message.GetParamAsString(7);
                var cleanJobs = message.GetParamAsBool(8);

                // Solo.ckpool specific: Use proper extranonce2 handling
                // For solo mining, we need to generate our own extranonce2
                var extranonce2 = GenerateExtraNonce2();
                var coinbaseTransaction = coinbase1 + extranonce2 + coinbase2;

                var work = new WorkItem(jobId, prevHash, coinbaseTransaction, merkleTree, version, bits, time)
                {
                    ExtraNonce2 = extranonce2 // Store for later submission
                };
                
                WorkReceived?.Invoke(work);
                StatusChanged?.Invoke($"New work received (Job: {jobId}, Clean: {cleanJobs})");
                
                if (cleanJobs)
                {
                    StatusChanged?.Invoke("Clean jobs requested - discarding old work");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to process mining notification: {ex.Message}");
            }
        }

        private void HandleSetPoolDifficulty(StratumMessage message)
        {
            try
            {
                var difficulty = message.GetParam<double>(0);
                if (difficulty > 0)
                {
                    PoolShareDifficultyChanged?.Invoke(difficulty);
                    StatusChanged?.Invoke($"Difficulty changed to {difficulty:F2}");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to process difficulty change: {ex.Message}");
            }
        }

        private async Task HandleReconnect()
        {
            StatusChanged?.Invoke("Pool requested reconnection");
            await DisconnectAsync();
            await Task.Delay(5000); // Wait 5 seconds before reconnecting
            await ConnectAsync();
        }

        private void HandleShowMessage(StratumMessage message)
        {
            var msg = message.GetParamAsString(0);
            if (!string.IsNullOrEmpty(msg))
            {
                StatusChanged?.Invoke($"Pool message: {msg}");
            }
        }

        private string GenerateExtraNonce2()
        {
            var extraNonce2 = _extraNonce2Counter++;
            return extraNonce2.ToString($"x{_extraNonce2Size * 2}").PadLeft(_extraNonce2Size * 2, '0');
        }

        private void HandleSubscribeResponse(StratumMessage message)
        {
            try
            {
                if (message.Result is Newtonsoft.Json.Linq.JArray resultArray && resultArray.Count >= 2)
                {
                    // Extract subscription details and extranonce1
                    if (resultArray[1] is Newtonsoft.Json.Linq.JArray subscriptionDetails && subscriptionDetails.Count >= 2)
                    {
                        _extraNonce1 = subscriptionDetails[0]?.ToString() ?? "";
                        _extraNonce2Size = subscriptionDetails[1]?.ToObject<int>() ?? 4;
                        
                        StatusChanged?.Invoke($"Subscription details: ExtraNonce1={_extraNonce1}, ExtraNonce2Size={_extraNonce2Size}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to parse subscription response: {ex.Message}");
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait(5000);
            _cancellationTokenSource.Dispose();
        }
    }
}
