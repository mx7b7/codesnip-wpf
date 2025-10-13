using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows;

namespace CodeSnip.Services
{
    /// <summary>
    /// Defines a contract for a service that shows various dialogs to the user.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Displays a message dialog.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message to display.</param>
        /// <returns>A task that completes when the dialog is closed.</returns>
        Task ShowMessageAsync(string title, string message);

        /// <summary>
        /// Displays a confirmation dialog with affirmative and negative buttons.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message to display.</param>
        /// <param name="affirmativeText">The text for the affirmative button (default is "Yes").</param>
        /// <param name="negativeText">The text for the negative button (default is "No").</param>
        /// <returns>A task that resolves to <c>true</c> if the user clicked the affirmative button, otherwise <c>false</c>.</returns>
        Task<bool> ShowConfirmAsync(string title, string message, string affirmativeText = "Yes", string negativeText = "No");

        /// <summary>
        /// Displays a dialog that prompts the user for input.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message to display above the input field.</param>
        /// <returns>A task that resolves to the string entered by the user, or null if the dialog was cancelled.</returns>
        Task<string?> ShowInputAsync(string title, string message);
    }

    /// <summary>
    /// A singleton implementation of <see cref="IDialogService"/> that uses MahApps.Metro dialogs.
    /// It provides an easy way to show message, confirmation, and input dialogs from anywhere in the application.
    /// </summary>
    /// <example><code>await DialogService.Instance.ShowMessageAsync("Title", "Message");</code></example>
    public class DialogService : IDialogService
    {
        private static DialogService? _instance;

        /// <summary>
        /// Gets the singleton instance of the DialogService.
        /// </summary>
        public static DialogService Instance => _instance ??= new DialogService(() => Application.Current.MainWindow as MetroWindow);

        private readonly Func<MetroWindow?> _getMainWindow;
        private DialogService(Func<MetroWindow?> getMainWindow)
        {
            _getMainWindow = getMainWindow;
        }

        /// <inheritdoc/>
        public Task ShowMessageAsync(string title, string message)
        {
            var window = _getMainWindow();
            if (window == null)
                throw new InvalidOperationException("MainWindow is not available.");

            return window.ShowMessageAsync(title, message, MessageDialogStyle.Affirmative);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
