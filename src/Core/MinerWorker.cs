using System.Diagnostics;
using BitcoinMinerConsole.Configuration;
using BitcoinMinerConsole.Logging;

namespace BitcoinMinerConsole.Core;

public class MinerWorker
{
    private readonly int _workerId;
    private readonly MinerConfig _config;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private Task? _miningTask;
    private WorkItem? _currentWork;
    private readonly object _workLock = new object();
    private bool _isRunning = false;
    private long _totalHashes = 0;
    private double _currentHashrate = 0;

    public WorkItem? CurrentWork => _currentWork;

    // Lottery mining fields
    private readonly Random _random;
    private readonly HashSet<uint> _testedNonces;

    // Events
    public event Action<int, uint, double>? ShareFound;
    public event Action<int, double>? HashrateUpdated;
    public event Action<int, double>? BestDifficultyFound;

    public bool IsRunning => _isRunning;
    public long TotalHashes => _totalHashes;
    public double CurrentHashrate => _currentHashrate;

    public MinerWorker(int workerId, MinerConfig config, ILogger logger, CancellationToken cancellationToken)
    {
        _workerId = workerId;
        _config = config;
        _logger = logger;
        _cancellationToken = cancellationToken;

        // Initialize lottery mining components
        int seed = config.Mining.RandomSeed ?? (int)(DateTime.Now.Ticks + workerId);
        _random = new Random(seed);
        _testedNonces = config.Mining.AvoidRecentDuplicates ? new HashSet<uint>() : new HashSet<uint>();

        _logger.LogMining($"Worker {workerId} initialized with seed: {seed}");
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
                // Choose mining strategy based on configuration
                hashCount = await MineLotteryAsync(work, hashCount, stopwatch);
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

    private async Task<int> MineLotteryAsync(WorkItem work, int hashCount, Stopwatch stopwatch)
    {
        int batchSize = _config.Mining.NonceBatchSize;

        for (int i = 0; i < batchSize && _isRunning; i++)
        {
            // Generate random nonce within this worker's assigned range
            uint nonce = GenerateRandomNonceInRange(work.StartNonce, work.EndNonce);

            // Test the nonce
            hashCount = await TestNonce(work, nonce, hashCount, stopwatch);

            // Check for cancellation periodically
            if (i % 100 == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return hashCount;
    }


    private async Task<int> TestNonce(WorkItem work, uint nonce, int hashCount, Stopwatch stopwatch)
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
            _logger.LogMining($"Worker {_workerId} found valid hash: {hashHex} (Nonce: {nonce:x8}, Difficulty: {hashDifficulty:N2})");
            ShareFound?.Invoke(_workerId, nonce, hashDifficulty);
        }

        // Update best worker difficulty if this hash is better
        if (hashDifficulty > 1.0) // Only track meaningful difficulties
        {
            BestDifficultyFound?.Invoke(_workerId, hashDifficulty);
        }

        // Update hashrate periodically
        if (hashCount >= _config.Mining.NonceBatchSize)
        {
            UpdateHashrate(hashCount, stopwatch.Elapsed);

            hashCount = 0;
            stopwatch.Restart();

            // Small delay to prevent CPU overload

            await Task.Delay(1, _cancellationToken);
        }

        return hashCount;
    }

    private uint GenerateRandomNonceInRange(uint startNonce, uint endNonce)
    {
        lock (_random) // Thread-safe random generation
        {
            // Calculate the range size
            ulong range = (ulong)endNonce - (ulong)startNonce + 1;


            // Large range case: use full precision
            double randomFactor = _random.NextDouble();
            ulong randomOffset = (ulong)(randomFactor * range);
            return (uint)((ulong)startNonce + randomOffset);
        }
    }

    private void UpdateHashrate(int hashes, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds > 0)
        {
            _currentHashrate = hashes / elapsed.TotalSeconds;
            HashrateUpdated?.Invoke(_workerId, _currentHashrate);
        }
    }
}
