using Newtonsoft.Json;

namespace BitcoinMinerConsole.Configuration
{
    public class MinerConfig
    {
        [JsonProperty("pool")]
        public PoolSettings Pool { get; set; } = new PoolSettings();

        [JsonProperty("mining")]
        public MiningSettings Mining { get; set; } = new MiningSettings();

        [JsonProperty("logging")]
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        [JsonProperty("display")]
        public DisplaySettings Display { get; set; } = new DisplaySettings();

        [JsonProperty("statistics")]
        public StatisticsSettings Statistics { get; set; } = new StatisticsSettings();
    }

    public class PoolSettings
    {
        [JsonProperty("url")]
        public string Url { get; set; } = "solo.ckpool.org";

        [JsonProperty("port")]
        public int Port { get; set; } = 3333;

        [JsonProperty("wallet")]
        public string Wallet { get; set; } = "";

        [JsonProperty("worker_name")]
        public string WorkerName { get; set; } = "miner01";

        [JsonProperty("password")]
        public string Password { get; set; } = "x";
    }

    public class MiningSettings
    {
        [JsonProperty("threads")]
        public int Threads { get; set; } = 0; // 0 = auto-detect

        [JsonProperty("intensity")]
        public string Intensity { get; set; } = "high";

        [JsonProperty("target_temp")]
        public int TargetTemp { get; set; } = 80;

        [JsonProperty("max_nonce")]
        public uint MaxNonce { get; set; } = uint.MaxValue;

        [JsonProperty("random_seed")]
        public int? RandomSeed { get; set; } = null; // null = use timestamp

        [JsonProperty("avoid_recent_duplicates")]
        public bool AvoidRecentDuplicates { get; set; } = true;

        [JsonProperty("duplicate_cache_size")]
        public int DuplicateCacheSize { get; set; } = 1000000;

        [JsonProperty("nonce_batch_size")]
        public int NonceBatchSize { get; set; } = 1000;
    }

    public class LoggingSettings
    {
        [JsonProperty("level")]
        public string Level { get; set; } = "info";

        [JsonProperty("show_hashrate")]
        public bool ShowHashrate { get; set; } = true;

        [JsonProperty("update_interval")]
        public int UpdateInterval { get; set; } = 5;

        [JsonProperty("log_to_file")]
        public bool LogToFile { get; set; } = false;

        [JsonProperty("log_file")]
        public string LogFile { get; set; } = "mining.log";
    }

    public class DisplaySettings
    {
        [JsonProperty("show_banner")]
        public bool ShowBanner { get; set; } = true;

        [JsonProperty("colored_output")]
        public bool ColoredOutput { get; set; } = true;

        [JsonProperty("stats_refresh_rate")]
        public int StatsRefreshRate { get; set; } = 2;
    }

    public class StatisticsSettings
    {
        [JsonProperty("best_worker_difficulty")]
        public double BestWorkerDifficulty { get; set; } = 0.0;

        [JsonProperty("best_difficulty_date")]
        public DateTime? BestDifficultyDate { get; set; } = null;
    }
}
