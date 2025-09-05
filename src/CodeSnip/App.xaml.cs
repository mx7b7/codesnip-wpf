using System.Windows;

namespace CodeSnip
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var splashScreen = new Views.SplashScreenView.SplashScreen();
            splashScreen.Show();

            try
            {
                var mainWindow = new MainWindow();
                await PerformInitializationAsync(mainWindow);
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A critical error occurred during application startup and the application will now close.\n\nError: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
            finally
            {
                splashScreen.Close();
            }
        }
        private async Task PerformInitializationAsync(MainWindow mainWindow)
        {

            if (mainWindow.DataContext is MainViewModel mainViewModel)
            {
                await mainViewModel.InitializeAsync();
            }

        }
    }

}
