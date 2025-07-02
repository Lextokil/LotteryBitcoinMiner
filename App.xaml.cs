using System.Windows;

namespace LotteryBitcoinMiner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set up global exception handling
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            MessageBox.Show($"A fatal error occurred:\n\n{exception?.Message ?? "Unknown error"}", 
                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
