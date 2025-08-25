using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows;

namespace CodeSnip.Services
{
    public interface IDialogService
    {
        Task ShowMessageAsync(string title, string message);
        Task<bool> ShowConfirmAsync(string title, string message, string affirmativeText = "Yes", string negativeText = "No");
        Task<string?> ShowInputAsync(string title, string message);
    }

    //example call await DialogService.Instance.ShowMessageAsync("Title", "Message");
    public class DialogService : IDialogService
    {
        private static DialogService? _instance;
        public static DialogService Instance => _instance ??= new DialogService(() => Application.Current.MainWindow as MetroWindow);

        private readonly Func<MetroWindow?> _getMainWindow;
        private DialogService(Func<MetroWindow?> getMainWindow)
        {
            _getMainWindow = getMainWindow;
        }

        public Task ShowMessageAsync(string title, string message)
        {
            var window = _getMainWindow();
            if (window == null)
                throw new InvalidOperationException("MainWindow is not available.");

            return window.ShowMessageAsync(title, message, MessageDialogStyle.Affirmative);
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string affirmativeText = "Yes", string negativeText = "No")
        {
            var window = _getMainWindow();
            if (window == null)
                throw new InvalidOperationException("MainWindow is not available.");

            var result = await window.ShowMessageAsync(title, message, MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
            {
                AffirmativeButtonText = affirmativeText,
                NegativeButtonText = negativeText
            });

            return result == MessageDialogResult.Affirmative;
        }

        public async Task<string?> ShowInputAsync(string title, string message)
        {
            var window = _getMainWindow();
            if (window == null)
                throw new InvalidOperationException("MainWindow is not available.");

            var result = await window.ShowInputAsync(title, message);
            return result;
        }
    }
}
