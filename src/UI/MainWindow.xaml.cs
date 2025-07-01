using System.Windows;
using System.Windows.Threading;
using BitcoinMinerConsole.Configuration;
using BitcoinMinerConsole.Core;
using BitcoinMinerConsole.Logging;
using BitcoinMinerConsole.Network;

namespace BitcoinMinerConsole.UI
{
    public partial class MainWindow : Window
    {
        private MinerConfig? _config;
        private WpfStatsDisplay? _statsDisplay;
        private StratumClient? _stratumClient;
        private MiningEngine? _miningEngine;
        private bool _isRunning = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly DispatcherTimer _updateTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            // Setup update timer for UI
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            
            // Load configuration on startup
            LoadConfiguration();
            
            // Setup window closing event
            Closing += MainWindow_Closing;
        }

        private void LoadConfiguration()
        {
            try
            {
                _config = ConfigLoader.LoadConfig();
                if (_config != null)
                {
                    WorkerNameText.Text = _config.Pool.WorkerName ?? "Default";

                    // Update lottery mining display
                    MiningModeText.Foreground = System.Windows.Media.Brushes.Cyan;
                    var seed = _config.Mining.RandomSeed ?? (int)DateTime.Now.Ticks;
                    RandomSeedText.Text = seed.ToString();

                    // Load best difficulty statistics
                    LoadBestDifficultyStats();

                    LogPoolEvent($"Configuration loaded: {_config.Pool.Url}:{_config.Pool.Port}");
                    {
                        LogMiningEvent($"🎲 LOTTERY MINING MODE ENABLED");
                        LogMiningEvent($"Random seed: {RandomSeedText.Text}");
                        LogMiningEvent($"Duplicate avoidance: {(_config.Mining.AvoidRecentDuplicates ? "ON" : "OFF")}");
                        LogMiningEvent($"Cache size: {_config.Mining.DuplicateCacheSize:N0}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogPoolEvent($"Failed to load configuration: {ex.Message}");
                MessageBox.Show($"Failed to load configuration: {ex.Message}", "Configuration Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_config == null)
            {
                MessageBox.Show("Configuration not loaded. Please check your config.json file.", 
                    "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!ValidateConfiguration())
                return;

            try
            {
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                
                LogPoolEvent("Starting Bitcoin miner...");
                
                // Initialize components
                _statsDisplay = new WpfStatsDisplay(_config, this);
                _stratumClient = new StratumClient(_config);
                _miningEngine = new MiningEngine(_config, new WpfLogger(this), _statsDisplay);

                // Setup event handlers
                SetupEventHandlers();

                // Start the miner
                await StartMinerAsync();
            }
            catch (Exception ex)
            {
                LogPoolEvent($"Failed to start miner: {ex.Message}");
                MessageBox.Show($"Failed to start miner: {ex.Message}", "Startup Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await StopMinerAsync();
        }

        private async void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            LogPoolEvent("Restarting miner...");
            await StopMinerAsync();
            await Task.Delay(2000);
            StartButton_Click(sender, e);
        }

        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_config != null)
            {
                var configWindow = new ConfigWindow(_config);
                configWindow.Owner = this;
                if (configWindow.ShowDialog() == true)
                {
                    LoadConfiguration();
                }
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            PoolEventsTextBox.Clear();
            MiningEventsTextBox.Clear();
            LogPoolEvent("Logs cleared");
        }

        private bool ValidateConfiguration()
        {
            if (_config == null)
            {
                MessageBox.Show("Configuration not loaded", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_config.Pool.Url))
            {
                MessageBox.Show("Pool URL is not configured", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_config.Pool.Wallet))
            {
                var result = MessageBox.Show(
                    "WARNING: Bitcoin wallet address is not configured!\n" +
                    "Mining will not be profitable without a valid wallet address.\n" +
                    "Please update the 'wallet' field in config.json\n\n" +
                    "Continue anyway?", 
                    "Wallet Warning", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                
                return result == MessageBoxResult.Yes;
            }

            return true;
        }

        private void SetupEventHandlers()
        {
            if (_stratumClient == null || _miningEngine == null || _statsDisplay == null)
                return;

            // Stratum client events
            _stratumClient.StatusChanged += (status) => {
                Dispatcher.Invoke(() => {
                    LogPoolEvent(status);
                    UpdatePoolStatus(status);
                });
            };

            _stratumClient.ErrorOccurred += (error) => {
                Dispatcher.Invoke(() => LogPoolEvent($"ERROR: {error}"));
            };

            _stratumClient.WorkReceived += (work) => {
                Dispatcher.Invoke(() => {
                    LogMiningEvent($"New work received: {work.JobId}");
                    CurrentJobText.Text = work.JobId;
                    _miningEngine.SetWork(work);
                });
            };

            _stratumClient.PoolShareDifficultyChanged += (difficulty) => {
                Dispatcher.Invoke(() => {
                    LogPoolEvent($"Pool Share Difficulty changed to {difficulty:F2}");
                    _miningEngine.UpdateWorkItemPoolShareDificulty(difficulty);
                    PoolShareDifficultyText.Text = difficulty.ToString("F2");
                });
            };

            _stratumClient.ShareResult += (accepted, message) => {
                Dispatcher.Invoke(() => {
                    // This is the ONLY place where share statistics are updated
                    // Only pool responses trigger share counting - no duplicates
                    if (accepted)
                    {
                        LogMiningEvent($"✓ Share accepted: {message}");
                        _statsDisplay.ShareAccepted();
                    }
                    else
                    {
                        LogMiningEvent($"✗ Share rejected: {message}");
                        _statsDisplay.ShareRejected();
                    }
                });
            };

            // Mining engine events
            _miningEngine.ShareFound += async (nonce, difficulty) => {
                if (_miningEngine.CurrentWorkItem != null)
                {
                    await _stratumClient.SubmitShareAsync(_miningEngine.CurrentWorkItem, nonce, difficulty);
                    Dispatcher.Invoke(() => LogMiningEvent($"Share found! Nonce: {nonce:x8}"));
                }
            };

            _miningEngine.HashrateUpdated += (hashrate) => {
                Dispatcher.Invoke(() => _statsDisplay.UpdateHashrate(hashrate));
            };

            _miningEngine.BestDifficultyFound += (workerId, difficulty) => {
                Dispatcher.Invoke(() => UpdateBestDifficulty(difficulty));
            };
        }

        private async Task StartMinerAsync()
        {
            if (_stratumClient == null || _miningEngine == null)
                return;

            LogPoolEvent($"Connecting to {_config!.Pool.Url}:{_config.Pool.Port}...");
            
            bool connected = false;
            int connectionAttempts = 0;
            const int maxConnectionAttempts = 3;

            while (!connected && connectionAttempts < maxConnectionAttempts)
            {
                connectionAttempts++;
                LogPoolEvent($"Connection attempt {connectionAttempts}/{maxConnectionAttempts}");
                
                connected = await _stratumClient.ConnectAsync();
                
                if (!connected && connectionAttempts < maxConnectionAttempts)
                {
                    LogPoolEvent($"Connection attempt {connectionAttempts} failed, retrying in 5 seconds...");
                    await Task.Delay(5000);
                }
            }

            if (!connected)
            {
                LogPoolEvent("Failed to connect to pool after all attempts");
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                return;
            }

            // Wait for subscription and authorization
            LogPoolEvent("Waiting for pool subscription and authorization...");
            int attempts = 0;
            const int maxWaitTime = 60;

            while ((!_stratumClient.IsSubscribed || !_stratumClient.IsAuthorized) && attempts < maxWaitTime)
            {
                await Task.Delay(1000);
                attempts++;
                
                if (attempts % 10 == 0)
                {
                    LogPoolEvent($"Waiting... Subscribed: {_stratumClient.IsSubscribed}, Authorized: {_stratumClient.IsAuthorized} ({attempts}s)");
                }
            }

            if (!_stratumClient.IsSubscribed)
            {
                LogPoolEvent("Failed to subscribe to pool");
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                return;
            }

            if (!_stratumClient.IsAuthorized)
            {
                LogPoolEvent("Failed to authorize with pool");
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                return;
            }

            // Wait for initial work
            LogPoolEvent("Waiting for initial work from pool...");
            attempts = 0;
            while (_miningEngine.CurrentWorkItem == null && attempts < 30)
            {
                await Task.Delay(1000);
                attempts++;
            }

            if (_miningEngine.CurrentWorkItem == null)
            {
                LogPoolEvent("No initial work received, but starting mining engine anyway");
            }

            // Start mining engine
            _miningEngine.Start();
            _isRunning = true;
            
            LogMiningEvent("Bitcoin miner started successfully!");
            UpdatePoolStatus("Mining");
        }

        private async Task StopMinerAsync()
        {
            _isRunning = false;
            
            LogPoolEvent("Stopping miner...");
            
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

            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            
            UpdatePoolStatus("Disconnected");
            LogPoolEvent("Miner stopped");
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_statsDisplay != null && _isRunning)
            {
                _statsDisplay.UpdateDisplay();
            }
        }

        private void UpdatePoolStatus(string status)
        {
            PoolStatusText.Text = status;
            
            // Update color based on status
            switch (status.ToLower())
            {
                case "connected":
                case "mining":
                case "authorized":
                    PoolStatusText.Foreground = System.Windows.Media.Brushes.Green;
                    break;
                case "disconnected":
                case "error":
                    PoolStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    break;
                default:
                    PoolStatusText.Foreground = System.Windows.Media.Brushes.Yellow;
                    break;
            }
        }

        public void LogPoolEvent(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\r\n";
            
            PoolEventsTextBox.AppendText(logEntry);
            PoolEventsScrollViewer.ScrollToEnd();
        }

        public void LogMiningEvent(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\r\n";
            
            MiningEventsTextBox.AppendText(logEntry);
            MiningEventsScrollViewer.ScrollToEnd();
        }

        public void UpdateStats
        (
            string hashrate,
            string avgHashrate,
            string totalHashes, 
            string sharesAccepted,
            string sharesRejected,
            string uptime
        )
        {
            HashrateText.Text = hashrate;
            AvgHashrateText.Text = avgHashrate;
            TotalHashesText.Text = totalHashes;
            SharesAcceptedText.Text = sharesAccepted;
            SharesRejectedText.Text = sharesRejected;
            UptimeText.Text = uptime;
        }

        private void LoadBestDifficultyStats()
        {
            if (_config?.Statistics != null)
            {
                BestDifficultyText.Text = _config.Statistics.BestWorkerDifficulty.ToString("N2");
                
                if (_config.Statistics.BestDifficultyDate.HasValue)
                {
                    BestDifficultyDateText.Text = _config.Statistics.BestDifficultyDate.Value.ToString("dd/MM/yyyy HH:mm");
                }
                else
                {
                    BestDifficultyDateText.Text = "Never";
                }
                
                if (_config.Statistics.BestWorkerDifficulty > 0)
                {
                    LogMiningEvent($"🏆 Historical best difficulty: {_config.Statistics.BestWorkerDifficulty:N2}");
                }
            }
        }

        public void UpdateBestDifficulty(double difficulty)
        {
            if (_config?.Statistics != null && difficulty > _config.Statistics.BestWorkerDifficulty)
            {
                _config.Statistics.BestWorkerDifficulty = difficulty;
                _config.Statistics.BestDifficultyDate = DateTime.Now;
                
                // Update interface
                BestDifficultyText.Text = difficulty.ToString("N2");
                BestDifficultyDateText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                
                // Save configuration
                ConfigLoader.SaveConfig(_config);
                
                // Log the new record
                LogMiningEvent($"🏆 NOVO RECORDE! Melhor dificuldade: {difficulty:N2}");
            }
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isRunning)
            {
                e.Cancel = true;
                await StopMinerAsync();
                Application.Current.Shutdown();
            }
        }
    }
}
