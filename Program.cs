
using LotteryBitcoinMiner.UI;

namespace LotteryBitcoinMiner
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            
            var mainWindow = new MainWindow();
            app.MainWindow = mainWindow;
            mainWindow.Show();
            
            app.Run();
        }
    }
}
