using CodeSnip.Views.LanguageCategoryView;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeSnip.Views.SnippetView
{
    public partial class Snippet : ObservableObject
    {
        [ObservableProperty]
        private int id = 0;

        [ObservableProperty]
        private int categoryId;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string code = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string tag = string.Empty;

        [ObservableProperty]
        private Category? category;

        [ObservableProperty]
        private bool isExpanded;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isVisible = true;

    }
}