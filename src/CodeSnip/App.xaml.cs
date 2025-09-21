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

                // Run initialization and a minimum delay in parallel
                Task initializationTask = PerformInitializationAsync(mainWindow);
                Task delayTask = Task.Delay(2000); // Minimum 2 seconds display time
                await Task.WhenAll(initializationTask, delayTask);

                MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A critical error occurred during application startup and the application will now close.\n\nError: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
            finally
            {
                await splashScreen.CloseWithFadeOut();
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
