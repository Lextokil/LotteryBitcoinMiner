using BitcoinMinerConsole.Configuration;
using BitcoinMinerConsole.Logging;
using System.Diagnostics;

namespace BitcoinMinerConsole.Core
{
    public class MiningEngine : IDisposable
    {
        private readonly MinerConfig _config;
        private readonly ConsoleLogger _logger;
        private readonly StatsDisplay _statsDisplay;
        private readonly List<MinerWorker> _workers;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private WorkItem? _currentWork;
        private readonly object _workLock = new object();
        private bool _isRunning = false;

        // Events
        public event Action<uint, string>? ShareFound;
        public event Action<double>? HashrateUpdated;

        public bool IsRunning => _isRunning;
        public int ActiveWorkers => _workers.Count(w => w.IsRunning);

        public MiningEngine(MinerConfig config, ConsoleLogger logger, StatsDisplay statsDisplay)
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

            _logger.LogInfo("Starting mining engine...");
            
            int threadCount = _config.Mining.Threads;
            if (threadCount <= 0)
            {
                threadCount = Environment.ProcessorCount;
                _logger.LogInfo($"Auto-detected {threadCount} CPU cores");
            }

            _logger.LogInfo($"Starting {threadCount} mining threads");

            // Create and start worker threads
            for (int i = 0; i < threadCount; i++)
            {
                var worker = new MinerWorker(i, _config, _logger, _cancellationTokenSource.Token);
                worker.ShareFound += OnShareFound;
                worker.HashrateUpdated += OnWorkerHashrateUpdated;
                worker.BestDifficultyFound += OnBestDifficultyFound;
                _workers.Add(worker);
            }

            // Start all workers
            foreach (var worker in _workers)
            {
                worker.Start();
            }

            _isRunning = true;
            _statsDisplay.UpdateActiveThreads(ActiveWorkers);
            _logger.LogSuccess($"Mining engine started with {threadCount} threads");

            // Start hashrate monitoring
            _ = Task.Run(MonitorHashrateAsync, _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _logger.LogInfo("Stopping mining engine...");
            _isRunning = false;

            _cancellationTokenSource.Cancel();

            // Stop all workers
            foreach (var worker in _workers)
            {
                worker.Stop();
            }

            _workers.Clear();
            _statsDisplay.UpdateActiveThreads(0);
            _logger.LogInfo("Mining engine stopped");
        }

        public void SetWork(WorkItem work)
        {
            lock (_workLock)
            {
                _currentWork = work;
                _logger.LogMining($"New work assigned: {work}");
                _statsDisplay.UpdateJob(work.JobId);
                _statsDisplay.UpdateDifficulty(work.Difficulty);

                // Distribute work to all workers
                uint nonceRange = uint.MaxValue / (uint)Math.Max(_workers.Count, 1);
                for (int i = 0; i < _workers.Count; i++)
                {
                    uint startNonce = (uint)i * nonceRange;
                    uint endNonce = (i == _workers.Count - 1) ? uint.MaxValue : (uint)(i + 1) * nonceRange - 1;
                    
                    var workerWork = new WorkItem
                    {
                        JobId = work.JobId,
                        PreviousBlockHash = work.PreviousBlockHash,
                        CoinbaseTransaction = work.CoinbaseTransaction,
                        MerkleTree = work.MerkleTree,
                        Version = work.Version,
                        Bits = work.Bits,
                        Time = work.Time,
                        StartNonce = startNonce,
                        EndNonce = endNonce,
                        Target = work.Target,
                        Difficulty = work.Difficulty
                    };
                    
                    workerWork.ComputeMerkleRoot();
                    workerWork.ComputeTarget();
                    
                    _workers[i].SetWork(workerWork);
                }
            }
        }

        private void OnShareFound(int workerId, uint nonce, string extraNonce2)
        {
            lock (_workLock)
            {
                if (_currentWork != null)
                {
                    _logger.LogShare($"Share found by worker {workerId}: nonce={nonce:x8}", true);
                    ShareFound?.Invoke(nonce, extraNonce2);
                    _statsDisplay.ShareAccepted();
                }
            }
        }

        private void OnWorkerHashrateUpdated(int workerId, double hashrate)
        {
            // Individual worker hashrate updates are handled in monitoring
        }

        private void OnBestDifficultyFound(int workerId, double difficulty)
        {
            _statsDisplay.UpdateBestWorkerDifficulty(difficulty);
            _logger.LogInfo($"Worker {workerId} achieved difficulty: {difficulty:N2}");
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

        public void OnShareResult(bool accepted, string message)
        {
            if (accepted)
            {
                _statsDisplay.ShareAccepted();
                _logger.LogShare(message, true);
            }
            else
            {
                _statsDisplay.ShareRejected();
                _logger.LogShare(message, false);
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
        }
    }

    public class MinerWorker
    {
        private readonly int _workerId;
        private readonly MinerConfig _config;
        private readonly ConsoleLogger _logger;
        private readonly CancellationToken _cancellationToken;
        private Task? _miningTask;
        private WorkItem? _currentWork;
        private readonly object _workLock = new object();
        private bool _isRunning = false;
        private long _totalHashes = 0;
        private double _currentHashrate = 0;
        private DateTime _lastHashrateUpdate = DateTime.Now;

        // Events
        public event Action<int, uint, string>? ShareFound;
        public event Action<int, double>? HashrateUpdated;
        public event Action<int, double>? BestDifficultyFound;

        public bool IsRunning => _isRunning;
        public long TotalHashes => _totalHashes;
        public double CurrentHashrate => _currentHashrate;

        public MinerWorker(int workerId, MinerConfig config, ConsoleLogger logger, CancellationToken cancellationToken)
        {
            _workerId = workerId;
            _config = config;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _miningTask = Task.Run(MineAsync, _cancellationToken);
        }

        public void Stop()
        {
            _isRunning = false;
            _miningTask?.Wait(5000);
        }

        public void SetWork(WorkItem work)
        {
            lock (_workLock)
            {
                _currentWork = work;
            }
        }

        private async Task MineAsync()
        {
            var hashCount = 0;
            var stopwatch = Stopwatch.StartNew();

            while (_isRunning && !_cancellationToken.IsCancellationRequested)
            {
                WorkItem? work;
                lock (_workLock)
                {
                    work = _currentWork;
                }

                if (work == null)
                {
                    await Task.Delay(100, _cancellationToken);
                    continue;
                }

                try
                {
                    // Mine a batch of nonces
                    const int batchSize = 1000;
                    for (uint nonce = work.StartNonce; nonce <= work.EndNonce && _isRunning; nonce++)
                    {
                        // Prepare block header with current nonce
                        work.PrepareBlockHeader(nonce);
                        
                        // Hash the block header
                        var hash = SHA256Hasher.HashBlockHeader(work.BlockHeader);
                        
                        hashCount++;
                        Interlocked.Increment(ref _totalHashes);

                        // Calculate hash difficulty for tracking best worker performance
                        var hashDifficulty = work.CalculateHashDifficulty(hash);
                        
                        // Check if hash meets target
                        if (work.IsValidHash(hash))
                        {
                            var hashHex = SHA256Hasher.HashToHexString(hash);
                            _logger.LogSuccess($"Worker {_workerId} found valid hash: {hashHex} (Difficulty: {hashDifficulty:N2})");
                            ShareFound?.Invoke(_workerId, nonce, "00000000");
                        }
                        
                        // Update best worker difficulty if this hash is better
                        if (hashDifficulty > 1.0) // Only track meaningful difficulties
                        {
                            BestDifficultyFound?.Invoke(_workerId, hashDifficulty);
                        }

                        // Update hashrate periodically
                        if (hashCount >= batchSize)
                        {
                            UpdateHashrate(hashCount, stopwatch.Elapsed);
                            hashCount = 0;
                            stopwatch.Restart();

                            // Small delay to prevent CPU overload
                            if (_config.Mining.Intensity.ToLower() != "high")
                            {
                                await Task.Delay(1, _cancellationToken);
                            }
                        }

                        // Check for cancellation periodically
                        if (nonce % 10000 == 0)
                        {
                            _cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Worker {_workerId} mining error: {ex.Message}");
                    await Task.Delay(1000, _cancellationToken);
                }
            }
        }

        private void UpdateHashrate(int hashes, TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds > 0)
            {
                _currentHashrate = hashes / elapsed.TotalSeconds;
                HashrateUpdated?.Invoke(_workerId, _currentHashrate);
                _lastHashrateUpdate = DateTime.Now;
            }
        }
    }
}
