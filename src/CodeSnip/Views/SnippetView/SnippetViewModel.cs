using CodeSnip.Services;
using CodeSnip.Views.LanguageCategoryView;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace CodeSnip.Views.SnippetView
{
    public partial class SnippetViewModel : ObservableObject, IDataErrorInfo
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<Language> _languages = [];

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        [ObservableProperty]
        private ObservableCollection<Category> _availableCategories = [];

        [ObservableProperty]
        private Snippet? _snippet;

        public bool IsEditMode { get; }

        [ObservableProperty]
        private Language? _selectedLanguage;

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private string? _selectedLanguageName;

        [ObservableProperty]
        private string? _title = string.Empty;

        public string Error => "";

        public string this[string _property]
        {
            get
            {
                if (_property == nameof(Title))
                {
                    if (string.IsNullOrWhiteSpace(Title))
                        return "Title cannot be empty";
                }
                return "";
            }
        }

        public SnippetViewModel(
            bool isEditMode,
            Snippet? snippet,
            List<Language>? languages,
            DatabaseService? databaseService)
        {
            if (databaseService == null)
                throw new ArgumentNullException(nameof(databaseService));

            _databaseService = databaseService;
            IsEditMode = isEditMode;

            Languages = new ObservableCollection<Language>(languages ?? new List<Language>());
            Snippet = snippet;

            InitializeSelections();
        }

        public bool CanSave()
        {
            return string.IsNullOrWhiteSpace(this[nameof(Title)]);
        }

        private void InitializeSelections()
        {
            if (IsEditMode)
            {
                SelectedLanguage = Snippet?.Category?.Language;
                AvailableCategories = new ObservableCollection<Category>(SelectedLanguage?.Categories ?? []);
                SelectedCategory = Snippet?.Category;
                Title = Snippet?.Title ?? string.Empty;
            }
            else
            {
                SelectedLanguage = Languages.FirstOrDefault();
                AvailableCategories = new ObservableCollection<Category>(SelectedLanguage?.Categories ?? []);
                SelectedCategory = AvailableCategories.FirstOrDefault();
            }
        }

        partial void OnSelectedLanguageChanged(Language? value)
        {
            AvailableCategories = new ObservableCollection<Category>(value?.Categories ?? new ObservableCollection<Category>());
            SelectedLanguageName = value?.Name;

            if (!IsEditMode)
                SelectedCategory = AvailableCategories.FirstOrDefault();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null && Snippet != null)
            {
                Snippet.Category = value;
                Snippet.CategoryId = value.Id;
            }
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                if (!CanSave())
                    return;

                if (SelectedLanguage != null && SelectedCategory != null && Snippet != null)
                {
                    Snippet.Title = Title ?? string.Empty;
                    if (!IsEditMode)
                    {
                        Snippet.Code = _defaultCodeTemplates.TryGetValue(SelectedLanguage!.Code!, out var template) ? template : string.Empty;
                    }
                    Snippet saved = _databaseService.SaveSnippet(Snippet);

                    if (saved != null)
                    {
                        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                        {
                            mainVM.LoadSnippets();
                            mainVM.ExpandAndSelectSnippet(SelectedLanguage.Id, saved.CategoryId, saved.Id);
                            mainVM.StatusMessage = $"Snippet '{saved.Title}' saved at {DateTime.Now:HH:mm:ss}";
                        }
                    }
                    FlyoutService.CloseFlyoutByTag("flySnippet");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        [RelayCommand]
        private static void Cancel()
        {
            FlyoutService.CloseFlyoutByTag("flySnippet");
        }


        // Predlošci osnovnih funkcija za različite jezike
        private readonly Dictionary<string, string> _defaultCodeTemplates = new()
        {
            ["cs"] = @"
using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}
".Trim(),

            ["cpp"] = @"
#include <iostream>

int main()
{
    std::cout << ""Hello, World!"" << std::endl;
    return 0;
}
".Trim(),

            ["d"] = @"
import std.stdio;

void main()
{
    writeln(""Hello, World!"");
}
".Trim(),

            ["py"] = @"
def main():
    print(""Hello, World!"")

if __name__ == '__main__':
    main()
".Trim(),

            ["fs"] = @"
// Learn more about F# at http://fsharp.org
printfn ""Hello, World!""
".Trim(),

            ["rs"] = @"
fn main() {
    println!(""Hello, World!"");
}
".Trim(),

            ["js"] = @"
console.log('Hello, World!');
".Trim(),

            ["java"] = @"
public class HelloWorld {
    public static void main(String[] args) {
        System.out.println(""Hello, World!"");
    }
}
".Trim(),

            ["pas"] = @"
program HelloWorld;
begin
  writeln('Hello, World!');
end.
".Trim(),

            ["rb"] = @"
puts 'Hello, World!'
".Trim(),

            ["html"] = @"
<!DOCTYPE html>
<html>
<head>
    <title>Page Title</title>
</head>
<body>

    <h1>This is a Heading</h1>
    <p>This is a paragraph.</p>

</body>
</html>
".Trim()
        };

    }
}
