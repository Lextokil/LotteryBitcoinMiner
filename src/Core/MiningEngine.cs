using BitcoinMinerConsole.Configuration;
using BitcoinMinerConsole.Logging;
using System.Diagnostics;

namespace BitcoinMinerConsole.Core
{
    public class MiningEngine : IDisposable
    {
        private readonly MinerConfig _config;
        private readonly ILogger _logger;
        private readonly IStatsDisplay _statsDisplay;
        private readonly List<MinerWorker> _workers;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private WorkItem? _currentWorkItem;
        private readonly object _workLock = new object();
        private bool _isRunning = false;

        public WorkItem? CurrentWorkItem => _currentWorkItem;

        // Events
        public event Action<uint, double>? ShareFound;
        public event Action<double>? HashrateUpdated;
        public event Action<int, double>? BestDifficultyFound;

        public bool IsRunning => _isRunning;
        public int ActiveWorkers => _workers.Count(w => w.IsRunning);

        public MiningEngine(MinerConfig config, ILogger logger, IStatsDisplay statsDisplay)
        {
            _config = config;
            _logger = logger;
            _statsDisplay = statsDisplay;
            _workers = new List<MinerWorker>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _logger.LogMining("Starting mining engine...");
            
            int threadCount = _config.Mining.Threads;
            if (threadCount <= 0)
            {
                threadCount = Environment.ProcessorCount;
                _logger.LogMining($"Auto-detected {threadCount} CPU cores");
            }

            _logger.LogMining($"Starting {threadCount} mining threads");

            // Create and start worker threads
            for (int i = 0; i < threadCount; i++)
            {
                var worker = new MinerWorker(i, _config, _logger, _cancellationTokenSource.Token);
                worker.ShareFound += OnShareFound;
                worker.HashrateUpdated += OnWorkerHashrateUpdated;
                worker.BestDifficultyFound += OnBestDifficultyFound;
                worker.Start();
                _workers.Add(worker);
            }
            
            _isRunning = true;
            _statsDisplay.UpdateActiveThreads(ActiveWorkers);
            _logger.LogMining($"Mining engine started with {threadCount} threads");

            // Start hashrate monitoring
            _ = Task.Run(MonitorHashrateAsync, _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _logger.LogMining("Stopping mining engine...");
            _isRunning = false;

            _cancellationTokenSource.Cancel();

            // Stop all workers
            foreach (var worker in _workers)
            {
                worker.Stop();
            }

            _workers.Clear();
            _statsDisplay.UpdateActiveThreads(0);
            _logger.LogMining("Mining engine stopped");
        }

        public void SetWork(WorkItem work)
        {
            lock (_workLock)
            {
                if (_currentWorkItem is not null)
                {
                    work.PoolShareDifficulty = _currentWorkItem.PoolShareDifficulty;
                    work.PoolShareTarget = _currentWorkItem.PoolShareTarget;
                }
                _currentWorkItem = work;
                _logger.LogMining($"New work assigned: {work}");
                _statsDisplay.UpdateJob(work.JobId);

                // Distribute work to all workers
                uint nonceRange = uint.MaxValue / (uint)Math.Max(_workers.Count, 1);
                for (int i = 0; i < _workers.Count; i++)
                {
                    uint startNonce = (uint)i * nonceRange;
                    uint endNonce = (i == _workers.Count - 1) ? uint.MaxValue : (uint)(i + 1) * nonceRange - 1;
                    
                    var workItem = new WorkItem
                    {
                        JobId = _currentWorkItem.JobId,
                        PreviousBlockHash = _currentWorkItem.PreviousBlockHash,
                        CoinbaseTransaction = _currentWorkItem.CoinbaseTransaction,
                        MerkleTree = _currentWorkItem.MerkleTree,
                        Version = _currentWorkItem.Version,
                        Bits = _currentWorkItem.Bits,
                        Time = _currentWorkItem.Time,
                        StartNonce = startNonce,
                        EndNonce = endNonce,
                        Target = _currentWorkItem.Target,
                        Difficulty = _currentWorkItem.Difficulty,
                        PoolShareDifficulty = _currentWorkItem.PoolShareDifficulty,
                        PoolShareTarget = _currentWorkItem.PoolShareTarget,
                        MerkleRoot = _currentWorkItem.MerkleRoot
                        
                    };
                    
                    _workers[i].SetWork(workItem);
                }
            }
        }

        public void UpdateWorkItemPoolShareDificulty(double newDifficulty)
        {
            lock (_workLock)
            {
                _currentWorkItem ??= new WorkItem();

                _currentWorkItem.UpdatePoolShareTargetAndDificulty(newDifficulty);
                // Update all workers with new difficulty
                foreach (var worker in _workers)
                {
                    if (worker.CurrentWork != null)
                    {
                        worker.CurrentWork.PoolShareDifficulty = _currentWorkItem.PoolShareDifficulty;
                        worker.CurrentWork.PoolShareTarget = _currentWorkItem.PoolShareTarget;
                    }
                }
            }
        }

        private void OnShareFound(int workerId, uint nonce, double difficulty)
        {
            lock (_workLock)
            {
                if (_currentWorkItem != null)
                {
                    _logger.LogShare($"Share found by worker {workerId}: nonce={nonce:x8}", true);
                    ShareFound?.Invoke(nonce, difficulty);
                    // Note: Share statistics will be updated only when pool responds
                }
            }
        }

        private void OnWorkerHashrateUpdated(int workerId, double hashrate)
        {
            // Individual worker hashrate updates are handled in monitoring
        }

        private void OnBestDifficultyFound(int workerId, double difficulty)
        {
            _statsDisplay.UpdateBestWorkerDifficulty(workerId, difficulty);
            // Notify about potential new best difficulty record
            BestDifficultyFound?.Invoke(workerId, difficulty);
        }

        private async Task MonitorHashrateAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            long lastTotalHashes = 0;

            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, _cancellationTokenSource.Token); // Update every 5 seconds

                    // Calculate total hashrate from all workers
                    double totalHashrate = 0;
                    long totalHashes = 0;

                    foreach (var worker in _workers)
                    {
                        totalHashrate += worker.CurrentHashrate;
                        totalHashes += worker.TotalHashes;
                    }

                    // Calculate hashrate based on hash difference
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    if (elapsed > 0)
                    {
                        var hashDiff = totalHashes - lastTotalHashes;
                        var calculatedHashrate = hashDiff / elapsed;
                        
                        // Use the calculated hashrate if it's reasonable
                        if (calculatedHashrate > 0 && Math.Abs(calculatedHashrate - totalHashrate) / totalHashrate < 0.5)
                        {
                            totalHashrate = calculatedHashrate;
                        }
                    }

                    _statsDisplay.UpdateHashrate(totalHashrate);
                    _statsDisplay.AddHashes(totalHashes - lastTotalHashes);
                    HashrateUpdated?.Invoke(totalHashrate);

                    lastTotalHashes = totalHashes;
                    stopwatch.Restart();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Hashrate monitoring error: {ex.Message}");
                }
            }
        }


        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
        }
    }

}
