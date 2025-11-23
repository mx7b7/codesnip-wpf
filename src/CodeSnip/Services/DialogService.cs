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

        /// <summary>
        /// Displays an asynchronous message dialog with Yes, No, and Cancel options, allowing the user to select a response.
        /// </summary>
        /// <remarks>The dialog is modal and blocks interaction with other windows until the user makes a
        /// selection. The method returns immediately and completes when the user responds. Button text can be
        /// customized to suit the application's context.</remarks>
        /// <param name="title">The title text displayed in the dialog window. Cannot be null or empty.</param>
        /// <param name="message">The message content shown in the dialog. Cannot be null or empty.</param>
        /// <param name="affirmativeText">The text label for the affirmative (Yes) button. Defaults to "Yes" if not specified.</param>
        /// <param name="negativeText">The text label for the negative (No) button. Defaults to "No" if not specified.</param>
        /// <param name="cancelText">The text label for the cancel button. Defaults to "Cancel" if not specified.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is a <see cref="MessageDialogResult"/>
        /// value indicating which button the user selected.</returns>
        Task<MessageDialogResult> ShowYesNoCancelAsync(string title, string message,
                                  string affirmativeText = "Yes", string negativeText = "No", string cancelText = "Cancel");
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
            var window = _getMainWindow() ?? throw new InvalidOperationException("MainWindow is not available.");
            return window.ShowMessageAsync(title, message, MessageDialogStyle.Affirmative);
        }

        /// <inheritdoc/>
        public async Task<bool> ShowConfirmAsync(string title, string message, string affirmativeText = "Yes", string negativeText = "No")
        {
            var window = _getMainWindow() ?? throw new InvalidOperationException("MainWindow is not available.");
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
            var window = _getMainWindow() ?? throw new InvalidOperationException("MainWindow is not available.");
            var result = await window.ShowInputAsync(title, message);
            return result;
        }

        /// <inheritdoc/>
        public async Task<MessageDialogResult> ShowYesNoCancelAsync(string title, string message,
                                  string affirmativeText = "Yes", string negativeText = "No", string cancelText = "Cancel")
        {
            var window = _getMainWindow() ?? throw new InvalidOperationException("MainWindow is not available.");
            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = affirmativeText,
                NegativeButtonText = negativeText,
                FirstAuxiliaryButtonText = cancelText

            };
            var result = await window.ShowMessageAsync(title, message, MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, settings);
            return result;
        }
    }
}
