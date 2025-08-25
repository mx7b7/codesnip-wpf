namespace CodeSnip.Services
{
    public interface IFlyoutService
    {
        void ShowFlyout(string tag, object viewModel, string header, Action? onClosed = null);
        bool IsFlyoutOpen(string tag);
        void ShowHighlightingEditor();
    }
}

