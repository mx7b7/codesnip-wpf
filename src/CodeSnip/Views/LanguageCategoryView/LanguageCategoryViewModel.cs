using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;


namespace CodeSnip.Views.LanguageCategoryView
{
    public partial class LanguageCategoryViewModel : ObservableObject, IDataErrorInfo
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<Language> languages = [];

        [ObservableProperty]
        private ObservableCollection<Category> filteredCategories = [];

        [ObservableProperty]
        private Language? selectedLanguage;

        [ObservableProperty]
        private Language? selectedLanguageForCategory;

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private string newLanguageCode = string.Empty;

        [ObservableProperty]
        private string newLanguageName = string.Empty;

        [ObservableProperty]
        private string newCategoryName = string.Empty;

        [ObservableProperty]
        private bool isAddingLanguage;

        [ObservableProperty]
        private bool isAddingCategory;

        // IDataErrorInfo implementation
        public string Error => string.Empty;

        public string this[string _property]
        {
            get
            {
                return _property switch
                {
                    nameof(NewLanguageCode) =>
                        string.IsNullOrWhiteSpace(NewLanguageCode) ? "Language extension is required." :
                        !IsValidLanguageCode(NewLanguageCode) ? "Language extension can contain only letters and digits." :
                        string.Empty,
                    nameof(NewLanguageName) => string.IsNullOrWhiteSpace(NewLanguageName) ? "Language name is required." : string.Empty,
                    nameof(NewCategoryName) => string.IsNullOrWhiteSpace(NewCategoryName) ? "Category name is required." : string.Empty,
                    _ => string.Empty,
                };
            }
        }


        public LanguageCategoryViewModel(DatabaseService dbService)
        {
            _databaseService = dbService;
            LoadLanguages();
        }
        private static bool IsValidLanguageCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            return Regex.IsMatch(code, @"^[a-zA-Z0-9]+$");
        }


        public bool CanSaveLanguage()
        {
            return !string.IsNullOrWhiteSpace(NewLanguageCode)
                && !string.IsNullOrWhiteSpace(NewLanguageName);
        }

        public bool CanSaveCategory()
        {
            return !string.IsNullOrWhiteSpace(NewCategoryName);
        }

        private void LoadLanguages()
        {
            var langs = _databaseService.GetLanguagesWithCategories().ToList();
            Languages = new ObservableCollection<Language>(langs);

            if (langs.Any())
            {
                SelectedLanguage = langs.First();
                SelectedLanguageForCategory = langs.First();
            }
        }

        partial void OnSelectedLanguageForCategoryChanged(Language? value)
        {
            if (value != null)
            {
                FilteredCategories = new ObservableCollection<Category>(value.Categories);
                if (FilteredCategories.Any())
                {
                    SelectedCategory = FilteredCategories.First();
                }
                else
                {
                    SelectedCategory = null;
                    NewCategoryName = string.Empty;
                }
            }
            else
            {
                FilteredCategories.Clear();
                SelectedCategory = null;
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                NewCategoryName = value.Name;
            }
        }

        partial void OnSelectedLanguageChanged(Language? value)
        {
            if (value != null)
            {
                NewLanguageCode = value.Code ?? "";
                NewLanguageName = value.Name ?? "";
                Debug.WriteLine($"Selected NewLanguageCode {NewLanguageCode}");
                Debug.WriteLine($"Selected language {SelectedLanguage?.Code}");
            }
        }

        [RelayCommand]
        private void ToggleAddLanguage()
        {
            if (IsAddingLanguage)
            {
                // Cancel
                IsAddingLanguage = false;
                SelectedLanguage = Languages.FirstOrDefault();
            }
            else
            {
                // Enter Add Mode
                IsAddingLanguage = true;
                SelectedLanguage = null;
                NewLanguageCode = string.Empty;
                NewLanguageName = string.Empty;
            }
        }

        [RelayCommand]
        private void SaveLanguage()
        {
            try
            {
                if (!CanSaveLanguage())
                    return;

                if (IsAddingLanguage)
                {
                    // INSERT
                    var newLang = new Language
                    {
                        Code = NewLanguageCode,
                        Name = NewLanguageName
                    };

                    _databaseService.SaveLanguage(newLang);
                    IsAddingLanguage = false;
                }
                else if (SelectedLanguage != null)
                {
                    // UPDATE
                    SelectedLanguage.Code = NewLanguageCode;
                    SelectedLanguage.Name = NewLanguageName;
                    _databaseService.SaveLanguage(SelectedLanguage);
                }
                else
                {
                    MessageBox.Show("Click 'Add' to insert a new language.");
                }
                LoadLanguages();
                NewLanguageCode = string.Empty;
                NewLanguageName = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleAddCategory()
        {
            if (IsAddingCategory)
            {
                // Cancel
                IsAddingCategory = false;
                SelectedCategory = FilteredCategories.FirstOrDefault();
            }
            else
            {
                IsAddingCategory = true;
                SelectedCategory = null;
                NewCategoryName = string.Empty;
            }
        }

        [RelayCommand]
        private void SaveCategory()
        {
            try
            {
                if (!CanSaveCategory())
                    return;

                if (SelectedLanguageForCategory == null)
                {
                    MessageBox.Show("Select a language.");
                    return;
                }

                if (IsAddingCategory)
                {
                    var newCat = new Category
                    {
                        Name = NewCategoryName,
                        LanguageId = SelectedLanguageForCategory.Id
                    };

                    _databaseService.SaveCategory(newCat);
                    IsAddingCategory = false;
                }
                else if (SelectedCategory != null)
                {
                    SelectedCategory.Name = NewCategoryName;
                    _databaseService.SaveCategory(SelectedCategory);
                }
                else
                {
                    MessageBox.Show("Click 'Add' to insert a new category.");
                }

                LoadLanguages();
                SelectedLanguageForCategory = Languages.FirstOrDefault(l => l.Id == SelectedLanguageForCategory?.Id);
                NewCategoryName = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteLanguage()
        {
            try
            {
                if (SelectedLanguage == null)
                {
                    MessageBox.Show("Select a language to delete.");
                    return;
                }
                if (SelectedLanguage.Categories.Any())
                {
                    MessageBox.Show("Cannot delete language that has categories. Delete them first.");
                    return;
                }

                var confirm = MessageBox.Show($"Delete language '{SelectedLanguage.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;

                _databaseService.DeleteLanguage(SelectedLanguage.Id);
                LoadLanguages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteCategory()
        {
            try
            {
                if (SelectedCategory == null)
                {
                    MessageBox.Show("Select a category to delete.");
                    return;
                }
                var confirm = MessageBox.Show($"Delete category '{SelectedCategory.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;

                _databaseService.DeleteCategory(SelectedCategory.Id);
                LoadLanguages();
                SelectedLanguageForCategory = Languages.FirstOrDefault(l => l.Id == SelectedLanguageForCategory?.Id);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        [RelayCommand]
        private void Close()
        {
            FlyoutService.CloseFlyoutByTag("flyEditLangCat");
        }

    }
}
