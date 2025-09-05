using CodeSnip.Services;
using CodeSnip.Views.CodeRunnerView;
using CodeSnip.Views.CompilerSettingsView;
using CodeSnip.Views.LanguageCategoryView;
using CodeSnip.Views.SettingsView;
using CodeSnip.Views.SnippetView;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Theming;
using ICSharpCode.AvalonEdit;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;


namespace CodeSnip
{

    public partial class MainViewModel : ObservableObject
    {
        private SearchReplacePanel? _searchReplacePanel;
        TextEditorOptions opt = new();
        private readonly DatabaseService _databaseService = new();
        private readonly SettingsService settingsService = new();
        private readonly IFlyoutService _flyoutService;

        private readonly Geometry? _menuOpenIcon;
        private readonly Geometry? _menuCloseIcon;

        public ObservableCollection<Language> Languages { get; } = [];

        [ObservableProperty]
        private Snippet? _selectedSnippet;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private Snippet? editingSnippet;

        [ObservableProperty]
        private string editorText = string.Empty;

        [ObservableProperty]
        private bool isEditorModified;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private string windowTitle = "codesnip";

        [ObservableProperty]
        private bool _isLoadSnippetEnabled = true;

        private bool _isInternalTextUpdate = true;

        [ObservableProperty]
        private double fontSize = 14;

        [ObservableProperty]
        private bool isPaneOpen = true;

        [ObservableProperty]
        private double _splitViewOpenPaneLength = 350;

        [ObservableProperty]
        private double _windowX = 100;

        [ObservableProperty]
        private double _windowY = 100;

        [ObservableProperty]
        private double _windowWidth = 800;

        [ObservableProperty]
        private double _windowHeight = 600;

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private bool _isFilteringEnabled = true;

        [ObservableProperty]
        private bool _enableBraceStyleFolding = false;

        [ObservableProperty]
        private bool _enablePythonFolding = false;

        [ObservableProperty]
        private bool _enableXmlFolding = false;

        [ObservableProperty]
        private bool _showEmptyLanguages = false;

        [ObservableProperty]
        private bool _showEmptyCategories = false;

        public enum SnippetFilterMode
        {
            Name,
            Tag
        }

        [ObservableProperty]
        private SnippetFilterMode _filterMode = SnippetFilterMode.Name;

        public MainViewModel(IFlyoutService flyoutService)
        {
            _flyoutService = flyoutService;
            try
            {
                _menuOpenIcon = Application.Current.Resources["MenuOpen"] as Geometry;
                _menuCloseIcon = Application.Current.Resources["MenuClose"] as Geometry;
                if (_menuOpenIcon == null || _menuCloseIcon == null)
                {
                    throw new InvalidOperationException("Icons not found in resources.");
                }

                SplitViewOpenPaneLength = settingsService.PanelLength;
                WindowX = settingsService.WindowX;
                WindowY = settingsService.WindowY;
                WindowWidth = settingsService.WindowWidth;
                WindowHeight = settingsService.WindowHeight;
                opt.EnableEmailHyperlinks = settingsService.EnableEmailLinks;
                opt.EnableHyperlinks = settingsService.EnableHyperinks;
                opt.ConvertTabsToSpaces = settingsService.TabToSpaces;
                opt.HighlightCurrentLine = settingsService.HighlightLine;
                opt.IndentationSize = settingsService.IntendationSize;

                IsFilteringEnabled = settingsService.EnableFiltering;
                EnableBraceStyleFolding = settingsService.EnableBraceStyleFolding;
                EnablePythonFolding = settingsService.EnablePythonFolding;
                EnableXmlFolding = settingsService.EnableXmlFolding;
                ShowEmptyLanguages = settingsService.ShowEmptyLanguages;
                ShowEmptyCategories = settingsService.ShowEmptyCategories;

            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error while starting the application:\n{ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n{ex.Message}");
            }

        }
        public async Task InitializeAsync()
        {
            await Task.Run(() => _databaseService.InitializeDatabaseIfNeeded());

            IsLoadSnippetEnabled = !settingsService.LoadOnStartup;

            if (settingsService.LoadOnStartup)
            {
                var languages = await Task.Run(() => _databaseService.GetSnippets());

                PopulateLanguagesCollection(languages);

                if (settingsService.LastSnippet != null && settingsService.LoadOnStartup)
                {
                    RestoreSelectedSnippetState(settingsService.LastSnippet);
                }
            }
        }

        private void PopulateLanguagesCollection(IEnumerable<Language> languages)
        {
            Languages.Clear();
            foreach (var lang in languages)
            {
                bool languageHasAnySnippets = false;
                foreach (var cat in lang.Categories)
                {
                    bool categoryHasSnippets = cat.Snippets.Any();
                    if (categoryHasSnippets)
                    {
                        languageHasAnySnippets = true;
                    }

                    // A category is visible if the setting is on, OR if it has snippets.
                    cat.IsVisible = ShowEmptyCategories || categoryHasSnippets;
                }
                // A language is visible if the setting is on, OR if it has any snippets.
                lang.IsVisible = ShowEmptyLanguages || languageHasAnySnippets;
                Languages.Add(lang);
            }
        }

        // Call from the MainWindow constructor
        public void InitializeEditor(TextEditor textEditor)
        {
            textEditor.Options = opt;
            _searchReplacePanel = SearchReplacePanel.Install(textEditor);
        }

        partial void OnFilterTextChanged(string? oldValue, string newValue)
        {
            if (!IsFilteringEnabled) return;

            ApplySnippetFilter();

            // If the filter has just been cleared (transition from filled to empty string)
            if (!string.IsNullOrWhiteSpace(oldValue) && string.IsNullOrWhiteSpace(newValue))
            {
                // 1. First collapse all nodes
                foreach (var lang in Languages)
                {
                    lang.IsExpanded = false;
                    foreach (var cat in lang.Categories)
                    {
                        cat.IsExpanded = false;
                    }
                }

                // 2. Then expand only the path to the currently selected snippet
                if (SelectedSnippet != null)
                {
                    if (SelectedSnippet.Category?.Language != null)
                    {
                        SelectedSnippet.Category.Language.IsExpanded = true;
                    }
                    if (SelectedSnippet.Category != null)
                    {
                        SelectedSnippet.Category.IsExpanded = true;
                    }
                    SelectedSnippet.IsSelected = true;
                }
            }
        }

        partial void OnFilterModeChanged(SnippetFilterMode value)
        {
            if (!IsFilteringEnabled) return;

            ApplySnippetFilter();
        }

        partial void OnIsFilteringEnabledChanged(bool value)
        {
            if (!IsFilteringEnabled) return;

            ApplySnippetFilter();
        }

        private void ApplySnippetFilter()
        {
            bool isFilterActive = IsFilteringEnabled && !string.IsNullOrWhiteSpace(FilterText);

            foreach (var lang in Languages)
            {
                bool langVisible = false;
                foreach (var cat in lang.Categories)
                {
                    bool catVisible = false;
                    foreach (var snip in cat.Snippets)
                    {
                        // The snippet is visible if the filter is not active, or if it matches the filter
                        bool snipVisible = !isFilterActive || FilterMatch(snip);
                        snip.IsVisible = snipVisible;
                        if (snipVisible)
                        {
                            catVisible = true; // If at least one snippet is visible, the category is also visible
                        }
                    }
                    cat.IsVisible = catVisible;
                    if (catVisible)
                    {
                        langVisible = true; // If at least one category is visible, the language is also visible
                    }
                }
                lang.IsVisible = langVisible;

                // Automatically expand nodes if the filter is active and they are visible
                if (isFilterActive && langVisible)
                {
                    lang.IsExpanded = true;
                    foreach (var cat in lang.Categories)
                    {
                        if (cat.IsVisible)
                        {
                            cat.IsExpanded = true;
                        }
                    }
                }
            }
        }

        private bool MatchOnWordStart(string? textToSearch, string filter)
        {
            if (string.IsNullOrEmpty(textToSearch))
                return false;

            var words = textToSearch.Split([' ', ',', ';', ':', '-', '(', ')', '[', ']', '.'], StringSplitOptions.RemoveEmptyEntries);

            return words.Any(word => word.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
        }

        private bool FilterMatch(Snippet snippet)
        {
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            return FilterMode switch
            {
                SnippetFilterMode.Name => MatchOnWordStart(snippet.Title, FilterText),
                SnippetFilterMode.Tag => MatchOnWordStart(snippet.Tag, FilterText),
                _ => false
            };
        }


        #region FLYOUTS

        [RelayCommand]
        private void OpenCodeRunnerView()
        {
            if (_flyoutService.IsFlyoutOpen("flyCodeRunner")) return;

            string langCode = EditingSnippet?.Category?.Language?.Code ?? "d";

            if (EditingSnippet != null && EditorText != string.Empty)
            {
                var vm = new CodeRunnerViewModel(langCode, EditorText, () => EditorText);
                _flyoutService.ShowFlyout("flyCodeRunner", vm, "Run code");
            }
            else
            {
                StatusMessage = "Snippet is null";
            }
        }

        [RelayCommand]
        private void OpenCompilerSettings()
        {
            if (_flyoutService.IsFlyoutOpen("flyCompilerSettings")) return;

            var vm = new CompilerSettingsViewModel();
            _flyoutService.ShowFlyout("flyCompilerSettings", vm, "Compiler settings");
        }

        [RelayCommand]
        private void OpenHighlightingEditor()
        {
            if (EditingSnippet != null && EditorText != string.Empty)
            {
                _flyoutService.ShowHighlightingEditor();
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            if (_flyoutService.IsFlyoutOpen("flySettings")) return;

            // Store current values before opening the flyout to check for changes later
            bool oldShowEmptyLanguages = ShowEmptyLanguages;
            bool oldShowEmptyCategories = ShowEmptyCategories;
            var vm = new SettingsViewModel(settingsService, _databaseService);
            _flyoutService.ShowFlyout("flySettings", vm, "Settings", () =>
            {
                settingsService.HighlightLine = vm.HighlightLine;
                settingsService.EnableEmailLinks = vm.EmailLinks;
                settingsService.EnableHyperinks = vm.HyperLinks;
                settingsService.TabToSpaces = vm.TabToSpaces;
                settingsService.IntendationSize = vm.IntendationSize;
                settingsService.EnableFiltering = vm.EnableFiltering;
                settingsService.EnablePythonFolding = vm.EnablePythonFolding;
                settingsService.EnableBraceStyleFolding = vm.EnableBraceStyleFolding;
                settingsService.EnableXmlFolding = vm.EnableXmlFolding;
                settingsService.ShowEmptyLanguages = vm.ShowEmptyLanguages;
                settingsService.ShowEmptyCategories = vm.ShowEmptyCategories;

                // Instant application
                opt.HighlightCurrentLine = vm.HighlightLine;
                opt.EnableEmailHyperlinks = vm.EmailLinks;
                opt.ConvertTabsToSpaces = vm.TabToSpaces;
                opt.IndentationSize = vm.IntendationSize;
                opt.EnableHyperlinks = vm.HyperLinks;
                IsFilteringEnabled = vm.EnableFiltering;
                EnableBraceStyleFolding = vm.EnableBraceStyleFolding;
                EnablePythonFolding = vm.EnablePythonFolding;
                EnableXmlFolding = vm.EnableXmlFolding;
                ShowEmptyLanguages = vm.ShowEmptyLanguages;
                ShowEmptyCategories = vm.ShowEmptyCategories;

                if (oldShowEmptyLanguages != ShowEmptyLanguages || oldShowEmptyCategories != ShowEmptyCategories)
                {
                    Snippet? tmpSnippet = null;
                    if (EditingSnippet != null)
                    {
                        tmpSnippet = EditingSnippet;
                    }
                    LoadSnippets(); // Reload to apply visibility changes
                    if (tmpSnippet != null)
                    {
                        ExpandAndSelectSnippet(
                            tmpSnippet.Category?.Language?.Id ?? 0,
                            tmpSnippet.CategoryId,
                            tmpSnippet.Id);
                    }
                }

                settingsService.SaveSettings();
            });
        }

        [RelayCommand]
        private void OpenLanguageCategory()
        {
            if (_flyoutService.IsFlyoutOpen("flyEditLangCat")) return;


            Snippet? tmpSnippet = null;
            if (EditingSnippet != null)
            {
                tmpSnippet = EditingSnippet;
                PerformSave();
            }
            var vm = new LanguageCategoryViewModel(_databaseService);
            _flyoutService.ShowFlyout("flyEditLangCat", vm, "Edit Languages Categories", () =>
            {
                LoadSnippets();
                if (tmpSnippet != null)
                {
                    ExpandAndSelectSnippet(
                        tmpSnippet.Category?.Language?.Id ?? 0,
                        tmpSnippet.CategoryId,
                        tmpSnippet.Id);
                }

            });
        }


        #endregion


        #region TOOLBAR ACTIONS

        [RelayCommand]
        private void TogglePanel()
        {
            IsPaneOpen = !IsPaneOpen;
            OnPropertyChanged(nameof(OpenCloseIcon));
        }

        [RelayCommand]
        private void LoadSnippetsDatabase()
        {
            LoadSnippets();
            IsLoadSnippetEnabled = false;
        }

        [RelayCommand]
        private async Task AddSnippet()
        {
            if (_flyoutService.IsFlyoutOpen("flySnippet")) return;

            if (!Languages.Any())
            {
                await DialogService.Instance.ShowMessageAsync("Cannot Add Snippet",
                    "There are no languages defined. Add a language and category first via 'Edit Languages/Categories'.");
                return;
            }

            var vm = new SnippetViewModel(false, new Snippet(), [.. Languages], _databaseService);

            _flyoutService.ShowFlyout("flySnippet", vm, " Add new snippet");
        }

        [RelayCommand]
        private async Task EditSnippet()
        {
            if (_flyoutService.IsFlyoutOpen("flySnippet")) return;

            if (SelectedSnippet is null)
            {
                await DialogService.Instance.ShowMessageAsync("No Snippet Selected",
                    "Select a snippet from the list to edit.");
                return;
            }

            var vm = new SnippetViewModel(true, EditingSnippet, [.. Languages], _databaseService);
            if (IsEditorModified && EditingSnippet != null)
            {
                PerformSave();
            }
            _flyoutService.ShowFlyout("flySnippet", vm, $"Edit {SelectedSnippet.Title}", () =>
            {
                // onClosed  ...
            });

        }

        [RelayCommand]
        private async Task SaveCode()
        {
            if (EditingSnippet is null)
            {
                await DialogService.Instance.ShowMessageAsync("Cannot Save",
                    "There is no active snippet to save. Please select a snippet first.");
                return;
            }

            PerformSave();
        }

        private void PerformSave()
        {
            // This method assumes EditingSnippet is not null.
            _databaseService.UpdateSnippetCode(EditingSnippet!.Id, EditorText);

            EditingSnippet.Code = EditorText; // need this because otherwise the old text is displayed ...
            UpdateSnippetInMemory(EditingSnippet);
            StatusMessage = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}";
            IsEditorModified = false;
            UpdateWindowTitle();

        }

        [RelayCommand]
        private async Task DeleteSnippet()
        {
            if (SelectedSnippet is null)
            {
                await DialogService.Instance.ShowMessageAsync("No Snippet Selected",
                    "Select a snippet from the list before attempting to delete it.");
                return;
            }

            bool confirm = await DialogService.Instance.ShowConfirmAsync("Delete Confirmation",
                $"Are you sure you want to delete the snippet '{SelectedSnippet.Title}'?");

            if (!confirm)
                return;

            string snippetTitle = SelectedSnippet.Title;
            _databaseService.DeleteSnippet(SelectedSnippet.Id);

            var category = Languages
                .SelectMany(l => l.Categories)
                .FirstOrDefault(c => c.Id == SelectedSnippet.CategoryId);

            category?.Snippets.Remove(SelectedSnippet);

            // Reset the VM state and clear the editor
            SelectedSnippet = null;

            _isInternalTextUpdate = true;
            EditorText = string.Empty;
            _isInternalTextUpdate = false;

            IsEditorModified = false;
            EditingSnippet = null;

            StatusMessage = $"Snippet '{snippetTitle}' deleted successfully.";
        }

        [RelayCommand]
        private void IncreaseFontSize()
        {
            if (FontSize < 72)
                FontSize += 1;
        }

        [RelayCommand]
        private void DecreaseFontSize()
        {
            if (FontSize > 6)
                FontSize -= 1;
        }

        [RelayCommand]
        private void ResetFontSize()
        {
            FontSize = 14;
        }

        [RelayCommand]
        private void SearchReplace()
        {
            if ((_searchReplacePanel != null && _searchReplacePanel.IsClosed))
            {
                _searchReplacePanel.Open();
            }
        }

        #endregion

        partial void OnEditorTextChanged(string value)
        {

            if (_isInternalTextUpdate) return;

            IsEditorModified = true;
            UpdateWindowTitle();
        }

        public void OnWindowClosing(CancelEventArgs e)
        {
            if (IsEditorModified && EditingSnippet != null)
            {
                PerformSave();
            }
            SaveSettings();
            // e.Cancel = true;
        }

        public void LoadSnippets()
        {
            var languages = _databaseService.GetSnippets();
            PopulateLanguagesCollection(languages);
        }

        private void SaveSettings()
        {
            settingsService.WindowX = (int)WindowX;
            settingsService.WindowY = (int)WindowY;
            settingsService.WindowWidth = (int)WindowWidth;
            settingsService.WindowHeight = (int)WindowHeight;
            settingsService.PanelLength = (int)SplitViewOpenPaneLength;
            settingsService.LastSnippet = SaveSelectedSnippetState();
            var theme = ThemeManager.Current.DetectTheme(Application.Current);
            if (theme != null)
            {
                settingsService.BaseColor = theme.BaseColorScheme;
                settingsService.AccentColor = theme.ColorScheme;
            }
            settingsService.SaveSettings();
        }

        public Geometry? OpenCloseIcon
        {
            get
            {
                return (_menuOpenIcon != null && _menuCloseIcon != null)
                    ? (IsPaneOpen ? _menuCloseIcon! : _menuOpenIcon!)
                    : null;
            }
        }

        public void ExpandAndSelectSnippet(int languageId, int categoryId, int snippetId)
        {
            var lang = Languages.FirstOrDefault(l => l.Id == languageId);
            if (lang == null) return;

            lang.IsExpanded = true;

            var cat = lang.Categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            cat.IsExpanded = true;

            var snip = cat.Snippets.FirstOrDefault(s => s.Id == snippetId);

            if (snip == null) return;

            snip.IsSelected = true;
            SelectedSnippet = snip;
        }

        public string SaveSelectedSnippetState()
        {
            if (SelectedSnippet == null)
                return string.Empty;

            // Find the parents of the snippet
            var lang = Languages.FirstOrDefault(l => l.Categories.Any(c => c.Snippets.Contains(SelectedSnippet)));
            if (lang == null) return string.Empty;

            var cat = lang.Categories.FirstOrDefault(c => c.Snippets.Contains(SelectedSnippet));
            if (cat == null) return string.Empty;

            // Format: languageId:categoryId:snippetId
            return $"{lang.Id}:{cat.Id}:{SelectedSnippet.Id}";
        }

        public void RestoreSelectedSnippetState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;

            var parts = state.Split(':');
            if (parts.Length != 3) return;

            if (!int.TryParse(parts[0], out int languageId)) return;
            if (!int.TryParse(parts[1], out int categoryId)) return;
            if (!int.TryParse(parts[2], out int snippetId)) return;

            ExpandAndSelectSnippet(languageId, categoryId, snippetId);
        }

        private void UpdateSnippetInMemory(Snippet snippet)
        {
            if (snippet == null) return;

            var language = Languages.FirstOrDefault(l => l.Id == snippet.Category?.Language?.Id);
            if (language == null) return;

            var category = language.Categories.FirstOrDefault(c => c.Id == snippet.CategoryId);
            if (category == null) return;

            var snippetInCollection = category.Snippets.FirstOrDefault(s => s.Id == snippet.Id);
            if (snippetInCollection != null)
            {
                snippetInCollection.Code = snippet.Code;
            }
        }

        public async Task TrySaveEditingSnippetAsync(Snippet? newSnippet)
        {
            if (newSnippet == null) return;

            if (IsEditorModified && EditingSnippet != null)
            {
                var result = await DialogService.Instance.ShowConfirmAsync($"Unsaved Changes for {EditingSnippet?.Title}",
                    $"You have unsaved changes. Do you want to save them?");

                if (result == true)
                {
                    _databaseService.UpdateSnippetCode(EditingSnippet!.Id, EditorText);

                    EditingSnippet.Code = EditorText;
                    UpdateSnippetInMemory(EditingSnippet);
                    IsEditorModified = false;
                    StatusMessage = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}";
                }
                else if (result == false)
                {
                    IsEditorModified = false;
                }
                else
                {
                    SelectedSnippet = EditingSnippet;
                    return;
                }
            }

            SelectedSnippet = newSnippet;
            EditingSnippet = newSnippet;

            _isInternalTextUpdate = true;
            EditorText = newSnippet.Code ?? string.Empty;
            _isInternalTextUpdate = false;

            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            var title = "codesnip";
            if (SelectedSnippet != null)
                title += " - " + SelectedSnippet.Title;
            if (IsEditorModified)
                title += " *";
            WindowTitle = title;
        }


    }


}


