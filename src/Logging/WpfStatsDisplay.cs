using BitcoinMinerConsole.Configuration;
using BitcoinMinerConsole.UI;

namespace BitcoinMinerConsole.Logging
{
    public class WpfStatsDisplay : IStatsDisplay
    {
        private readonly MinerConfig _config;
        private readonly MainWindow _mainWindow;
        private readonly object _lockObject = new object();

        // Statistics
        private DateTime _startTime = DateTime.Now;
        private long _totalHashes = 0;
        private int _sharesAccepted = 0;
        private int _sharesRejected = 0;
        private double _currentHashrate = 0;
        private double _averageHashrate = 0;
        private string _currentJob = "";
        private double _bestWorkerDifficulty = 0;
        private string _poolStatus = "Disconnected";
        private int _activeThreads = 0;
        private readonly List<double> _hashrateHistory = new List<double>();

        public WpfStatsDisplay(MinerConfig config, MainWindow mainWindow)
        {
            _config = config;
            _mainWindow = mainWindow;
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

        public void UpdateBestWorkerDifficulty(int workerId, double difficulty)
        {
            lock (_lockObject)
            {
                if (difficulty > _bestWorkerDifficulty)
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        _bestWorkerDifficulty = difficulty;
                        _mainWindow.LogMiningEvent($"Worker {workerId} achieved difficulty: {_bestWorkerDifficulty:N2}");
                    });
                }
            }
        }

        public void UpdateDisplay()
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
            
            // Update UI through dispatcher
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.UpdateStats(
                    FormatHashrate(_currentHashrate),
                    FormatHashrate(_averageHashrate),
                    _totalHashes.ToString("N0"),
                    _sharesAccepted.ToString(),
                    _sharesRejected.ToString(),
                    uptimeStr
                );
            });
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

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.LogMiningEvent("=== BITCOIN MINER WPF v1.0 ===");
                _mainWindow.LogMiningEvent("solo.ckpool.org Mining Interface");
                _mainWindow.LogMiningEvent("================================");
            });
        }

        public void DisplayDetailedStats()
        {
            lock (_lockObject)
            {
                var uptime = DateTime.Now - _startTime;
                var totalShares = _sharesAccepted + _sharesRejected;
                var acceptanceRate = totalShares > 0 ? (_sharesAccepted * 100.0 / totalShares) : 0;
                
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.LogMiningEvent("=== DETAILED STATISTICS ===");
                    _mainWindow.LogMiningEvent($"Start Time: {_startTime:yyyy-MM-dd HH:mm:ss}");
                    _mainWindow.LogMiningEvent($"Uptime: {uptime.TotalHours:F1} hours");
                    _mainWindow.LogMiningEvent($"Total Hashes: {_totalHashes:N0}");
                    _mainWindow.LogMiningEvent($"Average Hashrate: {FormatHashrate(_averageHashrate)}");
                    _mainWindow.LogMiningEvent($"Peak Hashrate: {FormatHashrate(_hashrateHistory.Count > 0 ? _hashrateHistory.Max() : 0)}");
                    _mainWindow.LogMiningEvent($"Shares Accepted: {_sharesAccepted}");
                    _mainWindow.LogMiningEvent($"Shares Rejected: {_sharesRejected}");
                    _mainWindow.LogMiningEvent($"Acceptance Rate: {acceptanceRate:F1}%");
                    
                    if (uptime.TotalHours > 0)
                    {
                        var sharesPerHour = totalShares / uptime.TotalHours;
                        _mainWindow.LogMiningEvent($"Shares/Hour: {sharesPerHour:F1}");
                    }
                    
                    _mainWindow.LogMiningEvent("============================");
                });
            }
        }

        public void Dispose()
        {
            // No timer to dispose in WPF version
        }
    }
}
