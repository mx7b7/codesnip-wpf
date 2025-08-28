using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
        [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
        private Language? selectedLanguage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
        private Language? selectedLanguageForCategory;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
        private Category? selectedCategory;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
        private string newLanguageCode = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
        private string newLanguageName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
        private string newCategoryName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
        private bool isAddingLanguage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
        private bool isAddingCategory;

        // IDataErrorInfo implementation
        public string Error => string.Empty;

        public string this[string _property]
        {
            get
            {
                string error = string.Empty;
                switch (_property)
                {
                    case nameof(NewLanguageCode):
                        if (string.IsNullOrWhiteSpace(NewLanguageCode))
                            error = "Language extension is required.";
                        else if (!IsValidLanguageCode(NewLanguageCode))
                            error = "Language extension can contain only letters and digits.";
                        else
                        {
                            bool isDuplicate = IsAddingLanguage
                                ? Languages.Any(l => l.Code != null && l.Code.Equals(NewLanguageCode, StringComparison.OrdinalIgnoreCase))
                                : SelectedLanguage != null && Languages.Any(l => l.Id != SelectedLanguage.Id && l.Code != null && l.Code.Equals(NewLanguageCode, StringComparison.OrdinalIgnoreCase));

                            if (isDuplicate)
                                error = $"Extension '{NewLanguageCode}' already exists.";
                        }
                        break;

                    case nameof(NewLanguageName):
                        if (string.IsNullOrWhiteSpace(NewLanguageName))
                            error = "Language name is required.";
                        break;

                    case nameof(NewCategoryName):
                        if (string.IsNullOrWhiteSpace(NewCategoryName))
                            error = "Category name is required.";
                        else if (SelectedLanguageForCategory != null)
                        {
                            bool isDuplicate = IsAddingCategory
                                ? SelectedLanguageForCategory.Categories.Any(c => c.Name.Equals(NewCategoryName, StringComparison.OrdinalIgnoreCase))
                                : SelectedCategory != null && SelectedLanguageForCategory.Categories.Any(c => c.Id != SelectedCategory.Id && c.Name.Equals(NewCategoryName, StringComparison.OrdinalIgnoreCase));

                            if (isDuplicate)
                                error = $"Category '{NewCategoryName}' already exists.";
                        }
                        break;
                }
                return error;
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

        private bool CanSaveLanguage()
        {
            // Check for any validation errors on the relevant properties
            if (!string.IsNullOrEmpty(this[nameof(NewLanguageCode)]) || !string.IsNullOrEmpty(this[nameof(NewLanguageName)]))
            {
                return false;
            }
            // If in "Add" mode, saving is allowed.
            if (IsAddingLanguage)
            {
                return true;
            }

            // If in "Edit" mode (an item is selected), saving is allowed only if there are changes.
            if (SelectedLanguage != null)
            {
                return NewLanguageCode != SelectedLanguage.Code || NewLanguageName != SelectedLanguage.Name;
            }

            return false;
        }

        private bool CanSaveCategory()
        {
            // A language must be selected.
            if (SelectedLanguageForCategory == null)
            {
                return false;
            }

            // Check for validation errors.
            if (!string.IsNullOrEmpty(this[nameof(NewCategoryName)]))
            {
                return false;
            }

            // If in "Add" mode, saving is allowed.
            if (IsAddingCategory)
            {
                return true;
            }

            // If in "Edit" mode (an item is selected), saving is allowed only if there are changes.
            if (SelectedCategory != null)
            {
                return NewCategoryName != SelectedCategory.Name;
            }
            return false;
        }


        private void ResortLanguages()
        {
            var currentSelection = SelectedLanguage;
            var sorted = Languages.OrderBy(l => l.Name).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var item = sorted[i];
                var oldIndex = Languages.IndexOf(item);
                if (oldIndex != i)
                {
                    Languages.Move(oldIndex, i);
                }
            }
            SelectedLanguage = currentSelection;
        }

        private void LoadLanguages()
        {
            var langs = _databaseService.GetLanguagesWithCategories().OrderBy(l => l.Name).ToList();
            Languages = new ObservableCollection<Language>(langs);

            if (Languages.Any())
            {
                SelectedLanguage = Languages.First();
                SelectedLanguageForCategory = Languages.First();
            }
        }

        partial void OnSelectedLanguageForCategoryChanged(Language? value)
        {
            if (value != null)
            {
                FilteredCategories = value.Categories;
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
                FilteredCategories = [];
                SelectedCategory = null;
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                NewCategoryName = value.Name;
                IsAddingCategory = false;
            }
        }

        partial void OnSelectedLanguageChanged(Language? value)
        {
            if (value != null)
            {
                NewLanguageCode = value.Code ?? "";
                NewLanguageName = value.Name ?? "";
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

        [RelayCommand(CanExecute = nameof(CanSaveLanguage))]
        private async Task SaveLanguageAsync()
        {
            try
            {
                if (IsAddingLanguage)
                {
                    // Check for syntax highlighting file
                    if (!HighlightingService.SyntaxDefinitionExists(NewLanguageCode))
                    {
                        var confirm = await DialogService.Instance.ShowConfirmAsync(
                            "Missing Syntax Highlighting",
                            $"No syntax highlighting definition (.xshd file) was found for the extension '{NewLanguageCode}'.\n\n" +
                            "The language will be added, but code will appear as plain text.\n\n" +
                            "Do you want to add this language anyway?",
                            "Yes, add it",
                            "No, cancel");

                        if (!confirm)
                        {
                            ToggleAddLanguage(); // Re-use the cancellation logic
                            return; // User cancelled
                        }
                    }

                    // INSERT
                    var newLang = new Language
                    {
                        Code = NewLanguageCode,
                        Name = NewLanguageName
                    };

                    newLang = _databaseService.SaveLanguage(newLang);
                    Languages.Add(newLang);
                    IsAddingLanguage = false;
                    SelectedLanguage = newLang;
                }
                else if (SelectedLanguage != null)
                {
                    // UPDATE
                    SelectedLanguage.Code = NewLanguageCode;
                    SelectedLanguage.Name = NewLanguageName;
                    _databaseService.SaveLanguage(SelectedLanguage);
                }
                ResortLanguages();
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", ex.Message);
            }
        }

        private void ResortCategories()
        {
            if (SelectedLanguageForCategory == null) return;

            var currentSelection = SelectedCategory;
            var sorted = SelectedLanguageForCategory.Categories.OrderBy(c => c.Name).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var item = sorted[i];
                var oldIndex = SelectedLanguageForCategory.Categories.IndexOf(item);
                if (oldIndex != i)
                {
                    SelectedLanguageForCategory.Categories.Move(oldIndex, i);
                }
            }
            SelectedCategory = currentSelection;
        }

        [RelayCommand]
        private async Task ToggleAddCategoryAsync()
        {
            if (IsAddingCategory)
            {
                // Cancel
                IsAddingCategory = false;
                // Reselect the first category for the current language, if any
                SelectedCategory = FilteredCategories.FirstOrDefault();
            }
            else
            {
                // Enter Add Mode
                if (SelectedLanguageForCategory == null)
                {
                    await DialogService.Instance.ShowMessageAsync("No Language Selected", "Please select a language first before adding a category.");
                    return;
                }
                IsAddingCategory = true;
                SelectedCategory = null; // Deselect any category
                NewCategoryName = string.Empty;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveCategory))]
        private async Task SaveCategoryAsync()
        {
            try
            {
                if (IsAddingCategory)
                {
                    // INSERT
                    if (SelectedLanguageForCategory != null)
                    {
                        var newCat = new Category
                        {
                            Name = NewCategoryName,
                            LanguageId = SelectedLanguageForCategory.Id
                        };

                        newCat = _databaseService.SaveCategory(newCat); // Save and get back with ID
                        newCat.Language = SelectedLanguageForCategory; // Set back-reference
                        SelectedLanguageForCategory.Categories.Add(newCat);
                        IsAddingCategory = false;
                        SelectedCategory = newCat; // Select the new category
                    }
                }
                else if (SelectedCategory != null)
                {

                    SelectedCategory.Name = NewCategoryName;
                    _databaseService.SaveCategory(SelectedCategory);
                }
                ResortCategories();
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task DeleteLanguageAsync()
        {
            try
            {
                if (SelectedLanguage == null)
                {
                    await DialogService.Instance.ShowMessageAsync("Action required", "Select a language to delete.");
                    return;
                }
                if (SelectedLanguage.Categories.Any())
                {
                    await DialogService.Instance.ShowMessageAsync("Error", "Cannot delete language that has categories. Delete them first.");
                    return;
                }

                var confirm = await DialogService.Instance.ShowConfirmAsync("Confirm", $"Delete language '{SelectedLanguage.Name}'?");
                if (!confirm)
                    return;

                _databaseService.DeleteLanguage(SelectedLanguage.Id);
                Languages.Remove(SelectedLanguage);
                SelectedLanguage = Languages.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync()
        {
            try
            {
                if (SelectedCategory == null || SelectedLanguageForCategory == null)
                {
                    await DialogService.Instance.ShowMessageAsync("Action required", "Select a category to delete.");
                    return;
                }
                var confirm = await DialogService.Instance.ShowConfirmAsync("Confirm", $"Delete category '{SelectedCategory.Name}'?");
                if (!confirm)
                    return;

                _databaseService.DeleteCategory(SelectedCategory.Id);
                SelectedLanguageForCategory.Categories.Remove(SelectedCategory);
                SelectedCategory = FilteredCategories.FirstOrDefault();

            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("FOREIGN KEY constraint failed"))
                {
                    await DialogService.Instance.ShowMessageAsync("Error", "Cannot delete category that has snippets. Delete them first.");
                }
                else
                {
                    await DialogService.Instance.ShowMessageAsync("Error", ex.Message);
                }
            }
        }

        [RelayCommand]
        private void Close()
        {
            FlyoutService.CloseFlyoutByTag("flyEditLangCat");
        }

    }
}
