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

        // AvalonEdit throws exceptions from while loop ( HighlightingEngine.HighlightLineInternal() )
        private static int _errorCount = 0;
        /*
         cpp.xshd example:
        <Span color="String" multiline="true">
        <Begin>"</Begin>
        <End>"</End>
        <RuleSet>
        <Span begin="\\" end="." />
        <Span begin="^" end="$" />
        </RuleSet>
        </Span>

         std::string my_string = R"(
         Hello

         World
         )";
         */
        private void Application_DispatcherUnhandledException(
            object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (_errorCount >= 1)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            _errorCount++;

            if (e.Exception is InvalidOperationException ex && ex.Message.Contains("matched 0 characters"))
            {
                if (Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.DisableHighlightingAndShowError(ex.Message);
                }
            }
            else if (e.Exception != null)
            {
                string errorMessage = e.Exception.Message;
                MessageBox.Show($"A text matching error occurred:\n\n{errorMessage}\n\nApplication will now close.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

    }

}
