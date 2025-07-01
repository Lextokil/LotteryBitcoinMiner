using System.IO;
using System.Text.Json;
using System.Windows;
using BitcoinMinerConsole.Configuration;

namespace BitcoinMinerConsole.UI
{
    public partial class ConfigWindow : Window
    {
        private MinerConfig _config;
        private readonly MinerConfig _originalConfig;

        public ConfigWindow(MinerConfig config)
        {
            InitializeComponent();
            
            // Create a deep copy of the config to avoid modifying the original until save
            _originalConfig = config;
            _config = JsonSerializer.Deserialize<MinerConfig>(JsonSerializer.Serialize(config))!;
            
            LoadConfigurationToUI();
        }

        private void LoadConfigurationToUI()
        {
            // Pool Configuration
            PoolUrlTextBox.Text = _config.Pool.Url;
            PoolPortTextBox.Text = _config.Pool.Port.ToString();
            WalletTextBox.Text = _config.Pool.Wallet;
            WorkerNameTextBox.Text = _config.Pool.WorkerName;

            // Mining Configuration
            ThreadsTextBox.Text = _config.Mining.Threads.ToString();

            // Display Configuration
            ShowBannerCheckBox.IsChecked = _config.Display.ShowBanner;
            ColoredOutputCheckBox.IsChecked = _config.Display.ColoredOutput;
            StatsRefreshRateTextBox.Text = _config.Display.StatsRefreshRate.ToString();

            // Logging Configuration
            ShowHashrateCheckBox.IsChecked = _config.Logging.ShowHashrate;
            LogToFileCheckBox.IsChecked = _config.Logging.LogToFile;
            VerboseLoggingCheckBox.IsChecked = false; // VerboseLogging não existe no LoggingSettings
        }

        private bool SaveConfigurationFromUI()
        {
            try
            {
                // Pool Configuration
                _config.Pool.Url = PoolUrlTextBox.Text.Trim();
                
                if (!int.TryParse(PoolPortTextBox.Text, out int port) || port <= 0 || port > 65535)
                {
                    MessageBox.Show("Invalid port number. Please enter a value between 1 and 65535.", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                _config.Pool.Port = port;
                
                _config.Pool.Wallet = WalletTextBox.Text.Trim();
                _config.Pool.WorkerName = WorkerNameTextBox.Text.Trim();

                // Mining Configuration
                if (!int.TryParse(ThreadsTextBox.Text, out int threads) || threads <= 0)
                {
                    MessageBox.Show("Invalid thread count. Please enter a positive number.", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                _config.Mining.Threads = threads;

                // Display Configuration
                _config.Display.ShowBanner = ShowBannerCheckBox.IsChecked ?? false;
                _config.Display.ColoredOutput = ColoredOutputCheckBox.IsChecked ?? false;
                
                if (!int.TryParse(StatsRefreshRateTextBox.Text, out int refreshRate) || refreshRate <= 0)
                {
                    MessageBox.Show("Invalid stats refresh rate. Please enter a positive number.", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                _config.Display.StatsRefreshRate = refreshRate;

                // Logging Configuration
                _config.Logging.ShowHashrate = ShowHashrateCheckBox.IsChecked ?? false;
                _config.Logging.LogToFile = LogToFileCheckBox.IsChecked ?? false;
                // VerboseLogging não existe no LoggingSettings

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", 
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveConfigurationFromUI())
                return;

            try
            {
                // Save to file
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "config.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var jsonString = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(configPath, jsonString);

                // Copy values back to original config
                CopyConfigValues(_config, _originalConfig);

                MessageBox.Show("Configuration saved successfully!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration file: {ex.Message}", 
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to default values?", 
                "Reset Configuration", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _config = CreateDefaultConfig();
                LoadConfigurationToUI();
            }
        }

        private MinerConfig CreateDefaultConfig()
        {
            return new MinerConfig
            {
                Pool = new PoolSettings
                {
                    Url = "solo.ckpool.org",
                    Port = 4334,
                    Wallet = "",
                    WorkerName = "worker1",
                    Password = "x"
                },
                Mining = new MiningSettings
                {
                    Threads = Environment.ProcessorCount
                },
                Display = new DisplaySettings
                {
                    ShowBanner = true,
                    ColoredOutput = true,
                    StatsRefreshRate = 5
                },
                Logging = new LoggingSettings
                {
                    ShowHashrate = true,
                    LogToFile = false,
                    Level = "info"
                }
            };
        }

        private void CopyConfigValues(MinerConfig source, MinerConfig destination)
        {
            // Pool Configuration
            destination.Pool.Url = source.Pool.Url;
            destination.Pool.Port = source.Pool.Port;
            destination.Pool.Wallet = source.Pool.Wallet;
            destination.Pool.WorkerName = source.Pool.WorkerName;
            destination.Pool.Password = source.Pool.Password;

            // Mining Configuration
            destination.Mining.Threads = source.Mining.Threads;

            // Display Configuration
            destination.Display.ShowBanner = source.Display.ShowBanner;
            destination.Display.ColoredOutput = source.Display.ColoredOutput;
            destination.Display.StatsRefreshRate = source.Display.StatsRefreshRate;

            // Logging Configuration
            destination.Logging.ShowHashrate = source.Logging.ShowHashrate;
            destination.Logging.LogToFile = source.Logging.LogToFile;
            destination.Logging.Level = source.Logging.Level;
        }
    }
}
