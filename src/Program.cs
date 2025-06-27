using BitcoinMinerConsole.Configuration;
using BitcoinMinerConsole.Core;
using BitcoinMinerConsole.Logging;
using BitcoinMinerConsole.Network;

namespace BitcoinMinerConsole
{
    public class Program
    {
        private static MinerConfig? _config;
        private static ConsoleLogger? _logger;
        private static StatsDisplay? _statsDisplay;
        private static StratumClient? _stratumClient;
        private static MiningEngine? _miningEngine;
        private static bool _isRunning = false;
        private static readonly CancellationTokenSource _cancellationTokenSource = new();

        public static async Task Main(string[] args)
        {
            try
            {
                // Load configuration
                _config = ConfigLoader.LoadConfig();
                _logger = new ConsoleLogger(_config);
                _statsDisplay = new StatsDisplay(_config, _logger);

                // Display banner
                _statsDisplay.DisplayBanner();

                // Validate configuration
                if (!ValidateConfiguration())
                {
                    _logger.LogError("Configuration validation failed. Please check your config.json file.");
                    await WaitForExit();
                    return;
                }

                // Display configuration
                ConfigLoader.DisplayConfig(_config);
                await Task.Delay(2000);

                // Initialize components
                _stratumClient = new StratumClient(_config);
                _miningEngine = new MiningEngine(_config, _logger, _statsDisplay);

                // Setup event handlers
                SetupEventHandlers();

                // Start the miner
                await StartMinerAsync();

                // Handle user input
                await HandleUserInputAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private static bool ValidateConfiguration()
        {
            if (_config == null)
            {
                Console.WriteLine("Failed to load configuration");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_config.Pool.Url))
            {
                Console.WriteLine("Pool URL is not configured");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_config.Pool.Wallet))
            {
                Console.WriteLine("WARNING: Bitcoin wallet address is not configured!");
                Console.WriteLine("Mining will not be profitable without a valid wallet address.");
                Console.WriteLine("Please update the 'wallet' field in config.json");
                Console.WriteLine();
                Console.WriteLine("Continue anyway? (y/N): ");
                var response = Console.ReadLine();
                return response?.ToLower().StartsWith("y") == true;
            }

            return true;
        }

        private static void SetupEventHandlers()
        {
            if (_stratumClient == null || _miningEngine == null || _logger == null || _statsDisplay == null)
                return;

            // Stratum client events
            _stratumClient.StatusChanged += (status) => {
                _logger.LogNetwork(status);
                _statsDisplay.UpdatePoolStatus(status);
            };

            _stratumClient.ErrorOccurred += (error) => {
                _logger.LogError($"Pool error: {error}");
            };

            _stratumClient.WorkReceived += (work) => {
                _logger.LogMining($"New work received: {work.JobId}");
                _miningEngine.SetWork(work);
            };

            _stratumClient.DifficultyChanged += (difficulty) => {
                _logger.LogInfo($"Difficulty changed to {difficulty:F2}");
                _statsDisplay.UpdateDifficulty(difficulty);
            };

            _stratumClient.ShareResult += (accepted, message) => {
                _miningEngine.OnShareResult(accepted, message);
            };

            // Mining engine events
            _miningEngine.ShareFound += async (nonce, extraNonce2) => {
                if (_stratumClient.CurrentWork != null)
                {
                    // Use the ExtraNonce2 from the work item for solo.ckpool compatibility
                    var workExtraNonce2 = _stratumClient.CurrentWork.ExtraNonce2;
                    await _stratumClient.SubmitShareAsync(_stratumClient.CurrentWork, nonce, workExtraNonce2);
                }
            };

            _miningEngine.HashrateUpdated += (hashrate) => {
                // Hashrate updates are handled by StatsDisplay
            };

            // Console cancel event
            Console.CancelKeyPress += async (sender, e) => {
                e.Cancel = true;
                _logger?.LogInfo("Shutdown requested...");
                _cancellationTokenSource.Cancel();
            };
        }

        private static async Task StartMinerAsync()
        {
            if (_stratumClient == null || _miningEngine == null || _logger == null)
                return;

            _logger.LogInfo("Starting Bitcoin miner...");

            // Connect to pool with retry logic
            _logger.LogInfo($"Connecting to {_config!.Pool.Url}:{_config.Pool.Port}...");
            bool connected = false;
            int connectionAttempts = 0;
            const int maxConnectionAttempts = 3;

            while (!connected && connectionAttempts < maxConnectionAttempts)
            {
                connectionAttempts++;
                _logger.LogInfo($"Connection attempt {connectionAttempts}/{maxConnectionAttempts}");
                
                connected = await _stratumClient.ConnectAsync();
                
                if (!connected && connectionAttempts < maxConnectionAttempts)
                {
                    _logger.LogWarning($"Connection attempt {connectionAttempts} failed, retrying in 5 seconds...");
                    await Task.Delay(5000);
                }
            }

            if (!connected)
            {
                _logger.LogError("Failed to connect to pool after all attempts");
                return;
            }

            // Wait for subscription and authorization with better timeout
            _logger.LogInfo("Waiting for pool subscription and authorization...");
            int attempts = 0;
            const int maxWaitTime = 60; // 60 seconds total

            while ((!_stratumClient.IsSubscribed || !_stratumClient.IsAuthorized) && attempts < maxWaitTime)
            {
                await Task.Delay(1000);
                attempts++;
                
                // Log progress every 10 seconds
                if (attempts % 10 == 0)
                {
                    _logger.LogInfo($"Waiting... Subscribed: {_stratumClient.IsSubscribed}, Authorized: {_stratumClient.IsAuthorized} ({attempts}s)");
                }
            }

            if (!_stratumClient.IsSubscribed)
            {
                _logger.LogError("Failed to subscribe to pool");
                return;
            }

            if (!_stratumClient.IsAuthorized)
            {
                _logger.LogError("Failed to authorize with pool");
                return;
            }

            // Wait a bit more for initial work
            _logger.LogInfo("Waiting for initial work from pool...");
            attempts = 0;
            while (_stratumClient.CurrentWork == null && attempts < 30)
            {
                await Task.Delay(1000);
                attempts++;
            }

            if (_stratumClient.CurrentWork == null)
            {
                _logger.LogWarning("No initial work received, but starting mining engine anyway");
            }

            // Start mining engine
            _miningEngine.Start();
            _statsDisplay?.UpdatePoolStatus("Mining");
            _isRunning = true;

            _logger.LogSuccess("Bitcoin miner started successfully!");
            _logger.LogInfo("Press 'q' to quit, 's' for stats, 'c' for config, 'h' for help, 'r' for restart");
        }

        private static async Task HandleUserInputAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var keyInfo = Console.ReadKey(true);
                    await ProcessUserCommand(keyInfo.KeyChar);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Input handling error: {ex.Message}");
                }
            }
        }

        private static async Task ProcessUserCommand(char command)
        {
            switch (char.ToLower(command))
            {
                case 'q':
                    _logger?.LogInfo("Quit command received");
                    _cancellationTokenSource.Cancel();
                    break;

                case 's':
                    _statsDisplay?.DisplayDetailedStats();
                    break;

                case 'c':
                    if (_config != null)
                    {
                        ConfigLoader.DisplayConfig(_config);
                    }
                    break;

                case 'h':
                    DisplayHelp();
                    break;

                case 'r':
                    _logger?.LogInfo("Restart command received");
                    await RestartMinerAsync();
                    break;

                default:
                    // Ignore unknown commands
                    break;
            }
        }

        private static void DisplayHelp()
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════ HELP ═══════════════════");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  q - Quit the miner");
            Console.WriteLine("  s - Show detailed statistics");
            Console.WriteLine("  c - Show current configuration");
            Console.WriteLine("  r - Restart miner connection");
            Console.WriteLine("  h - Show this help");
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine();
        }

        private static async Task RestartMinerAsync()
        {
            if (_stratumClient == null || _miningEngine == null)
                return;

            _logger?.LogInfo("Restarting miner...");

            // Stop mining
            _miningEngine.Stop();

            // Disconnect from pool
            await _stratumClient.DisconnectAsync();

            // Wait a moment
            await Task.Delay(2000);

            // Reconnect and restart
            await StartMinerAsync();
        }

        private static async Task WaitForExit()
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            await Task.CompletedTask;
        }

        private static async Task CleanupAsync()
        {
            _isRunning = false;

            Console.WriteLine();
            Console.WriteLine("Shutting down...");

            // Stop mining engine
            _miningEngine?.Stop();
            _miningEngine?.Dispose();

            // Disconnect from pool
            if (_stratumClient != null)
            {
                await _stratumClient.DisconnectAsync();
                _stratumClient.Dispose();
            }

            // Cleanup stats display
            _statsDisplay?.Dispose();

            Console.WriteLine("Shutdown complete.");
        }
    }
}
