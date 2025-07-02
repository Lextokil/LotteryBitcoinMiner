namespace LotteryBitcoinMiner.Logging
{
    public interface ILogger
    {
        void LogInfo(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogNetwork(string message);
        void LogMining(string message);
        void LogDebug(string message);
        void LogShare(string message, bool accepted);
    }
}
