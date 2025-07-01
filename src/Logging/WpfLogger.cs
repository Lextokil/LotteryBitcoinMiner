using BitcoinMinerConsole.UI;

namespace BitcoinMinerConsole.Logging
{
    public class WpfLogger : ILogger
    {
        private readonly MainWindow _mainWindow;

        public WpfLogger(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void LogInfo(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogPoolEvent($"INFO: {message}"));
        }

        public void LogSuccess(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogPoolEvent($"SUCCESS: {message}"));
        }

        public void LogWarning(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogPoolEvent($"WARNING: {message}"));
        }

        public void LogError(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogMiningEvent($"ERROR: {message}"));
        }

        public void LogNetwork(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogPoolEvent($"NETWORK: {message}"));
        }

        public void LogMining(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogMiningEvent($"MINING: {message}"));
        }

        public void LogDebug(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogMiningEvent($"DEBUG: {message}"));
        }

        public void LogShare(string message, bool accepted)
        {
            var prefix = accepted ? "SHARE+: " : "SHARE-: ";
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.LogMiningEvent($"{prefix}{message}"));
        }
    }
}
