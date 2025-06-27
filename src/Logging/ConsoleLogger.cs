using BitcoinMinerConsole.Configuration;

namespace BitcoinMinerConsole.Logging
{
    public class ConsoleLogger
    {
        private readonly MinerConfig _config;
        private readonly object _lockObject = new object();

        public ConsoleLogger(MinerConfig config)
        {
            _config = config;
        }

        public void LogInfo(string message)
        {
            Log("INFO", message, ConsoleColor.White);
        }

        public void LogSuccess(string message)
        {
            Log("SUCCESS", message, ConsoleColor.Green);
        }

        public void LogWarning(string message)
        {
            Log("WARNING", message, ConsoleColor.Yellow);
        }

        public void LogError(string message)
        {
            Log("ERROR", message, ConsoleColor.Red);
        }

        public void LogDebug(string message)
        {
            if (_config.Logging.Level.ToLower() == "debug")
            {
                Log("DEBUG", message, ConsoleColor.Gray);
            }
        }

        public void LogMining(string message)
        {
            Log("MINING", message, ConsoleColor.Cyan);
        }

        public void LogNetwork(string message)
        {
            Log("NETWORK", message, ConsoleColor.Magenta);
        }

        public void LogShare(string message, bool accepted)
        {
            var color = accepted ? ConsoleColor.Green : ConsoleColor.Red;
            var level = accepted ? "SHARE+" : "SHARE-";
            Log(level, message, color);
        }

        private void Log(string level, string message, ConsoleColor color)
        {
            lock (_lockObject)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                
                if (_config.Display.ColoredOutput)
                {
                    Console.Write($"[{timestamp}] ");
                    
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.Write($"[{level}] ");
                    Console.ForegroundColor = originalColor;
                    
                    Console.WriteLine(message);
                }
                else
                {
                    Console.WriteLine($"[{timestamp}] [{level}] {message}");
                }

                // Log to file if enabled
                if (_config.Logging.LogToFile)
                {
                    LogToFile(timestamp, level, message);
                }
            }
        }

        private void LogToFile(string timestamp, string level, string message)
        {
            try
            {
                var logEntry = $"[{timestamp}] [{level}] {message}";
                File.AppendAllText(_config.Logging.LogFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore file logging errors to prevent infinite loops
            }
        }

        public void ClearLine()
        {
            if (_config.Display.ColoredOutput)
            {
                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            }
        }

        public void WriteInPlace(string message, ConsoleColor color = ConsoleColor.White)
        {
            lock (_lockObject)
            {
                if (_config.Display.ColoredOutput)
                {
                    ClearLine();
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.Write(message);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.Write($"\r{message}");
                }
            }
        }
    }
}
