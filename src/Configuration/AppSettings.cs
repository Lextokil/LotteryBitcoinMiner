using System.Configuration;

namespace BitcoinMinerConsole.Configuration
{
    public class AppSettings : ApplicationSettingsBase
    {
        private static AppSettings? _instance;
        private static readonly object _lock = new object();

        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new AppSettings();
                    }
                }
                return _instance;
            }
        }

        [UserScopedSetting]
        [DefaultSettingValue("0.0")]
        public double BestWorkerDifficulty
        {
            get => (double)this["BestWorkerDifficulty"];
            set => this["BestWorkerDifficulty"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string BestDifficultyDate
        {
            get => (string)this["BestDifficultyDate"];
            set => this["BestDifficultyDate"] = value;
        }

        public DateTime? BestDifficultyDateTime
        {
            get
            {
                if (string.IsNullOrEmpty(BestDifficultyDate))
                    return null;
                
                if (DateTime.TryParse(BestDifficultyDate, out DateTime result))
                    return result;
                
                return null;
            }
            set
            {
                BestDifficultyDate = value?.ToString("O") ?? "";
            }
        }

        public void UpdateBestDifficulty(double difficulty)
        {
            if (difficulty > BestWorkerDifficulty)
            {
                BestWorkerDifficulty = difficulty;
                BestDifficultyDateTime = DateTime.Now;
                Save();
            }
        }

        public StatisticsSettings ToStatisticsSettings()
        {
            return new StatisticsSettings
            {
                BestWorkerDifficulty = BestWorkerDifficulty,
                BestDifficultyDate = BestDifficultyDateTime
            };
        }

        public void LoadFromStatisticsSettings(StatisticsSettings stats)
        {
            BestWorkerDifficulty = stats.BestWorkerDifficulty;
            BestDifficultyDateTime = stats.BestDifficultyDate;
            Save();
        }
    }
}
