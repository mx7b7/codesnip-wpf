using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CodeSnip.Views.LanguageCategoryView
{
    public partial class Language : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string? code;

        [ObservableProperty]
        private string? name;

        public ObservableCollection<Category> Categories { get; set; } = new();

        [ObservableProperty]
        private bool isExpanded;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isVisible = true;
    }
}
