using System;
using System.Windows;
using BitcoinMinerConsole.UI;

namespace BitcoinMinerConsole
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
