namespace LotteryBitcoinMiner.Logging
{
    public interface IStatsDisplay : IDisposable
    {
        void UpdateHashrate(double hashrate);
        void AddHashes(long hashes);
        void ShareAccepted();
        void ShareRejected();
        void UpdateJob(string jobId);
        void UpdatePoolStatus(string status);
        void UpdateActiveThreads(int threads);
        void UpdateBestWorkerDifficulty(int workerId, double difficulty);
        void DisplayBanner();
        void DisplayDetailedStats();
    }
}
