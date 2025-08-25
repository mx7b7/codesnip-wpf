using CodeSnip.Views.SnippetView;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace CodeSnip.Views.LanguageCategoryView
{
    public partial class Category : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private int languageId;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private Language? language;

        public ObservableCollection<Snippet> Snippets { get; set; } = new();

        [ObservableProperty]
        private bool isExpanded;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isVisible = true;
    }
}
