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

            var mainWindow = new MainWindow();
            await PerformInitializationAsync(mainWindow);
            MainWindow = mainWindow;
            mainWindow.Show();

            splashScreen.Close();
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
