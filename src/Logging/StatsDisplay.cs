using BitcoinMinerConsole.Configuration;

namespace BitcoinMinerConsole.Logging
{
    public class StatsDisplay
    {
        private readonly MinerConfig _config;
        private readonly ConsoleLogger _logger;
        private readonly Timer _updateTimer;
        private readonly object _lockObject = new object();

        // Statistics
        private DateTime _startTime = DateTime.Now;
        private long _totalHashes = 0;
        private int _sharesAccepted = 0;
        private int _sharesRejected = 0;
        private double _currentHashrate = 0;
        private double _averageHashrate = 0;
        private string _currentJob = "";
        private double _currentDifficulty = 0;
        private double _bestWorkerDifficulty = 0;
        private string _poolStatus = "Disconnected";
        private int _activeThreads = 0;
        private readonly List<double> _hashrateHistory = new List<double>();

        public StatsDisplay(MinerConfig config, ConsoleLogger logger)
        {
            _config = config;
            _logger = logger;
            
            // Update stats display every few seconds
            _updateTimer = new Timer(UpdateDisplay, null, 
                TimeSpan.FromSeconds(_config.Display.StatsRefreshRate), 
                TimeSpan.FromSeconds(_config.Display.StatsRefreshRate));
        }

        public void UpdateHashrate(double hashrate)
        {
            lock (_lockObject)
            {
                _currentHashrate = hashrate;
                _hashrateHistory.Add(hashrate);
                
                // Keep only last 60 entries (for average calculation)
                if (_hashrateHistory.Count > 60)
                {
                    _hashrateHistory.RemoveAt(0);
                }
                
                _averageHashrate = _hashrateHistory.Average();
            }
        }

        public void AddHashes(long hashes)
        {
            Interlocked.Add(ref _totalHashes, hashes);
        }

        public void ShareAccepted()
        {
            Interlocked.Increment(ref _sharesAccepted);
        }

        public void ShareRejected()
        {
            Interlocked.Increment(ref _sharesRejected);
        }

        public void UpdateJob(string jobId)
        {
            lock (_lockObject)
            {
                _currentJob = jobId;
            }
        }

        public void UpdateDifficulty(double difficulty)
        {
            lock (_lockObject)
            {
                _currentDifficulty = difficulty;
            }
        }

        public void UpdatePoolStatus(string status)
        {
            lock (_lockObject)
            {
                _poolStatus = status;
            }
        }

        public void UpdateActiveThreads(int threads)
        {
            lock (_lockObject)
            {
                _activeThreads = threads;
            }
        }

        public void UpdateBestWorkerDifficulty(double difficulty)
        {
            lock (_lockObject)
            {
                if (difficulty > _bestWorkerDifficulty)
                {
                    _bestWorkerDifficulty = difficulty;
                }
            }
        }

        private void UpdateDisplay(object? state)
        {
            if (!_config.Logging.ShowHashrate)
                return;

            lock (_lockObject)
            {
                try
                {
                    DisplayStats();
                }
                catch
                {
                    // Ignore display errors
                }
            }
        }

        private void DisplayStats()
        {
            var uptime = DateTime.Now - _startTime;
            var uptimeStr = $"{uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";
            
            // Clear previous stats (move cursor up and clear lines)
            // if (_config.Display.ColoredOutput)
            // {
            //     Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - 10));
            //     for (int i = 0; i < 10; i++)
            //     {
            //         Console.WriteLine(new string(' ', Console.WindowWidth - 1));
            //     }
            //     Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - 10));
            // }

            // Display header
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("                    BITCOIN MINER CONSOLE                     ");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            
            // Pool information
            Console.WriteLine($"Pool: {_config.Pool.Url}:{_config.Pool.Port}");
            Console.WriteLine($"Status: {GetColoredStatus(_poolStatus)}");
            Console.WriteLine($"Worker: {_config.Pool.WorkerName}");
            
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            
            // Mining statistics
            Console.WriteLine($"Current Job: {_currentJob}");
            Console.WriteLine($"Network Difficulty: {_currentDifficulty:N0}");
            Console.WriteLine($"Best Worker Difficulty: {_bestWorkerDifficulty:N2}");
            Console.WriteLine($"Threads: {_activeThreads}/{Environment.ProcessorCount}");
            
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            
            // Performance metrics
            Console.WriteLine($"Hashrate: {FormatHashrate(_currentHashrate)} (avg: {FormatHashrate(_averageHashrate)})");
            Console.WriteLine($"Total Hashes: {_totalHashes:N0}");
            Console.WriteLine($"Shares: {_sharesAccepted} accepted, {_sharesRejected} rejected");
            Console.WriteLine($"Uptime: {uptimeStr}");
            
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("Commands: [q]uit, [s]tats, [c]onfig, [h]elp");
            Console.WriteLine();
        }

        private string GetColoredStatus(string status)
        {
            if (!_config.Display.ColoredOutput)
                return status;

            return status.ToLower() switch
            {
                "connected" => $"\u001b[32m{status}\u001b[0m", // Green
                "mining" => $"\u001b[32m{status}\u001b[0m", // Green
                "authorized" => $"\u001b[32m{status}\u001b[0m", // Green
                "disconnected" => $"\u001b[31m{status}\u001b[0m", // Red
                "error" => $"\u001b[31m{status}\u001b[0m", // Red
                _ => $"\u001b[33m{status}\u001b[0m" // Yellow
            };
        }

        private static string FormatHashrate(double hashrate)
        {
            if (hashrate >= 1_000_000_000_000) // TH/s
                return $"{hashrate / 1_000_000_000_000:F2} TH/s";
            if (hashrate >= 1_000_000_000) // GH/s
                return $"{hashrate / 1_000_000_000:F2} GH/s";
            if (hashrate >= 1_000_000) // MH/s
                return $"{hashrate / 1_000_000:F2} MH/s";
            if (hashrate >= 1_000) // KH/s
                return $"{hashrate / 1_000:F2} KH/s";
            return $"{hashrate:F2} H/s";
        }

        public void DisplayBanner()
        {
            if (!_config.Display.ShowBanner)
                return;

            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("██████╗ ██╗████████╗ ██████╗ ██████╗ ██╗███╗   ██╗");
            Console.WriteLine("██╔══██╗██║╚══██╔══╝██╔════╝██╔═══██╗██║████╗  ██║");
            Console.WriteLine("██████╔╝██║   ██║   ██║     ██║   ██║██║██╔██╗ ██║");
            Console.WriteLine("██╔══██╗██║   ██║   ██║     ██║   ██║██║██║╚██╗██║");
            Console.WriteLine("██████╔╝██║   ██║   ╚██████╗╚██████╔╝██║██║ ╚████║");
            Console.WriteLine("╚═════╝ ╚═╝   ╚═╝    ╚═════╝ ╚═════╝ ╚═╝╚═╝  ╚═══╝");
            Console.WriteLine();
            Console.WriteLine("           MINER CONSOLE v1.0 - solo.ckpool.org");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }

        public void DisplayDetailedStats()
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════ DETAILED STATISTICS ═══════════════");
            
            var uptime = DateTime.Now - _startTime;
            var totalShares = _sharesAccepted + _sharesRejected;
            var acceptanceRate = totalShares > 0 ? (_sharesAccepted * 100.0 / totalShares) : 0;
            
            Console.WriteLine($"Start Time: {_startTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Uptime: {uptime.TotalHours:F1} hours");
            Console.WriteLine($"Total Hashes: {_totalHashes:N0}");
            Console.WriteLine($"Average Hashrate: {FormatHashrate(_averageHashrate)}");
            Console.WriteLine($"Peak Hashrate: {FormatHashrate(_hashrateHistory.Count > 0 ? _hashrateHistory.Max() : 0)}");
            Console.WriteLine($"Shares Accepted: {_sharesAccepted}");
            Console.WriteLine($"Shares Rejected: {_sharesRejected}");
            Console.WriteLine($"Acceptance Rate: {acceptanceRate:F1}%");
            
            if (uptime.TotalHours > 0)
            {
                var sharesPerHour = totalShares / uptime.TotalHours;
                Console.WriteLine($"Shares/Hour: {sharesPerHour:F1}");
            }
            
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine();
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
        }
    }
}
