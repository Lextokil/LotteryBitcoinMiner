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
}
