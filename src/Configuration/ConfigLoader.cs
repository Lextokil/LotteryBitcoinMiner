using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace LotteryBitcoinMiner.Configuration
{
    public static class ConfigLoader
    {
        private const string DefaultConfigPath = "config/config.json";

        public static MinerConfig LoadConfig(string? configPath = null)
        {
            string path = configPath ?? DefaultConfigPath;
            
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"Configuration file not found at: {path}");
                    Console.WriteLine("Creating default configuration...");
                    CreateDefaultConfig(path);
                }

                string jsonContent = File.ReadAllText(path, Encoding.UTF8);
                
                // Try to deserialize with statistics first (for migration)
                var configWithStats = JsonConvert.DeserializeObject<MinerConfigWithStats>(jsonContent);
                if (configWithStats?.Statistics != null)
                {
                    // Migrate statistics to app settings
                    AppSettings.Instance.LoadFromStatisticsSettings(configWithStats.Statistics);
                    
                    // Remove statistics from config file
                    var configWithoutStats = new MinerConfig
                    {
                        Pool = configWithStats.Pool,
                        Mining = configWithStats.Mining,
                        Logging = configWithStats.Logging,
                        Display = configWithStats.Display
                    };
                    
                    SaveConfig(configWithoutStats, path);
                    return configWithoutStats;
                }
                
                var config = JsonConvert.DeserializeObject<MinerConfig>(jsonContent);
                
                if (config == null)
                {
                    throw new InvalidOperationException("Failed to deserialize configuration");
                }

                ValidateConfig(config);
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                Console.WriteLine("Using default configuration...");
                return new MinerConfig();
            }
        }

        public static void SaveConfig(MinerConfig config, string? configPath = null)
        {
            string path = configPath ?? DefaultConfigPath;
            
            try
            {
                string directory = Path.GetDirectoryName(path) ?? "";
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(path, jsonContent, Encoding.UTF8);
                
                Console.WriteLine($"Configuration saved to: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }

        private static void CreateDefaultConfig(string path)
        {
            var defaultConfig = new MinerConfig();
            SaveConfig(defaultConfig, path);
        }

        private static void ValidateConfig(MinerConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Pool.Url))
            {
                throw new InvalidOperationException("Pool URL cannot be empty");
            }

            if (config.Pool.Port <= 0 || config.Pool.Port > 65535)
            {
                throw new InvalidOperationException("Pool port must be between 1 and 65535");
            }

            if (string.IsNullOrWhiteSpace(config.Pool.Wallet))
            {
                Console.WriteLine("WARNING: Bitcoin wallet address is not configured!");
                Console.WriteLine("Please update the 'wallet' field in config.json with your Bitcoin address.");
            }

            if (config.Mining.Threads < 0)
            {
                config.Mining.Threads = 0; // Auto-detect
            }

            if (config.Logging.UpdateInterval < 1)
            {
                config.Logging.UpdateInterval = 5;
            }

            if (config.Display.StatsRefreshRate < 1)
            {
                config.Display.StatsRefreshRate = 2;
            }
        }
        
    }
}
