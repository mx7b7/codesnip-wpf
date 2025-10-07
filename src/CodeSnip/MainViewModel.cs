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
using Notifications.Wpf.Core;
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

        private readonly NotificationManager notificationManager = new();

        private readonly Geometry? _panelOpenIcon;
        private readonly Geometry? _panelCloseIcon;

        [ObservableProperty]
        private string? databaseStatusTooltip;

        [ObservableProperty]
        private bool isDatabaseAlertActive;

        [ObservableProperty]
        private string? databaseStatusPopupMessage;

        public ObservableCollection<Language> Languages { get; } = [];

        [ObservableProperty]
        private Snippet? _selectedSnippet;

        [ObservableProperty]
        private Category? _selectedCategory;

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
        private string windowTitle = "CodeSnip";

        [ObservableProperty]
        private bool _isLoadSnippetEnabled = true;

        private bool _isInternalTextUpdate = true;

        [ObservableProperty]
        private string editorFontFamily = "Consolas";

        [ObservableProperty]
        private double editorFontSize = 13;

        [ObservableProperty]
        private bool isPaneOpen = true;

        [ObservableProperty]
        private double _splitViewOpenPaneLength = 350;

        [ObservableProperty]
        private double _windowX = 0;

        [ObservableProperty]
        private double _windowY = 0;

        [ObservableProperty]
        private double _windowWidth = 800;

        [ObservableProperty]
        private double _windowHeight = 600;

        [ObservableProperty]
        private bool _isSearchExpanded = false;

        [ObservableProperty]
        private bool _isSnippetsExpanded = true;

        [ObservableProperty]
        private bool _isSnippetMetadataExpanded = false;

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

        [ObservableProperty]
        public WindowState _windowState = WindowState.Normal;

        [ObservableProperty]
        private bool _disableIntendation = false;

        [ObservableProperty]
        private bool _isNotificationEnabled = true;

        [ObservableProperty]
        private int _languageFontSize = 14;

        [ObservableProperty]
        private string _languageFontWeight = "SemiBold";

        [ObservableProperty]
        private int _categoryFontSize = 13;

        [ObservableProperty]
        private string _categoryFontWeight = "Medium";

        [ObservableProperty]
        private int _snippetFontSize = 13;

        [ObservableProperty]
        private string _snippetFontWeight = "Light";

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

            _panelOpenIcon = Application.Current.Resources["PanelLeftOpen"] as Geometry;
            _panelCloseIcon = Application.Current.Resources["PanelLeftClose"] as Geometry;
            if (_panelOpenIcon == null || _panelCloseIcon == null)
            {
                throw new InvalidOperationException("Icons not found in resources.");
            }

            LoadSettingsIntoViewModel();

        }

        private void LoadSettingsIntoViewModel()
        {
            SplitViewOpenPaneLength = settingsService.PanelLength;
            WindowX = settingsService.WindowX;
            WindowY = settingsService.WindowY;
            WindowWidth = settingsService.WindowWidth;
            WindowHeight = settingsService.WindowHeight;
            IsSearchExpanded = settingsService.IsSearchExpanded;
            IsSnippetMetadataExpanded = settingsService.IsSnippetMetadataExpanded;
            WindowState = settingsService.WindowState;
            opt.EnableEmailHyperlinks = settingsService.EnableEmailLinks;
            opt.EnableHyperlinks = settingsService.EnableHyperinks;
            opt.ConvertTabsToSpaces = settingsService.TabToSpaces;
            opt.HighlightCurrentLine = settingsService.HighlightLine;
            opt.IndentationSize = settingsService.IntendationSize;

            IsFilteringEnabled = settingsService.EnableFiltering;
            IsNotificationEnabled = settingsService.IsNotificationEnabled;
            EnableBraceStyleFolding = settingsService.EnableBraceStyleFolding;
            EnablePythonFolding = settingsService.EnablePythonFolding;
            EnableXmlFolding = settingsService.EnableXmlFolding;
            EditorFontFamily = settingsService.EditorFontFamily;
            EditorFontSize = settingsService.EditorFontSize;
            ShowEmptyLanguages = settingsService.ShowEmptyLanguages;
            ShowEmptyCategories = settingsService.ShowEmptyCategories;

            LanguageFontSize = settingsService.LanguageFontSize;
            LanguageFontWeight = settingsService.LanguageFontWeight;
            CategoryFontSize = settingsService.CategoryFontSize;
            CategoryFontWeight = settingsService.CategoryFontWeight;
            SnippetFontSize = settingsService.SnippetFontSize;
            SnippetFontWeight = settingsService.SnippetFontWeight;
        }

        public async Task UpdateDatabaseHealthStatusAsync()
        {
            var (needVacuum, fragmentationPercent) = await _databaseService.IsVacuumNeeded();


            IsDatabaseAlertActive = needVacuum;

            if (needVacuum)
            {
                DatabaseStatusTooltip = $"Database is fragmented: {fragmentationPercent:P1} - click for details.";
                DatabaseStatusPopupMessage = $"Database fragmentation is at {fragmentationPercent:P1}.\n\n" +
                    $"It is recommended to perform a VACUUM operation. You can find this option in:\nSettings -> Database tab.";
            }
            else
            {
                DatabaseStatusTooltip = string.Empty;
                DatabaseStatusPopupMessage = string.Empty;
            }
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() => _databaseService.InitializeDatabaseIfNeeded());

            IsLoadSnippetEnabled = !settingsService.LoadOnStartup;

            if (settingsService.LoadOnStartup)
            {
                var languages = await Task.Run(() => _databaseService.GetSnippets());

                var languageList = languages.ToList();

                if (languageList.Count == 0 && _databaseService.GetSnippets().Any()) // Check if loading failed
                {
                    await DialogService.Instance.ShowMessageAsync("Database Load Error",
                        "Could not load snippets. The database file might be corrupted or the schema might have changed.");
                }
                else
                {
                    PopulateLanguagesCollection(languageList);
                    if (settingsService.LastSnippet != null)
                    {
                        RestoreSelectedSnippetState(settingsService.LastSnippet);
                    }
                }
            }
            await UpdateDatabaseHealthStatusAsync();
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

        partial void OnIsSearchExpandedChanged(bool value)
        {
            // If expanding the search panel, also ensure snippets panel is expanded
            if (value)
            {
                IsSnippetsExpanded = true;
            }
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

            var words = textToSearch.Split([' ', ',', ';', ':', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);

            return words.Any(word => word.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
        }

        private bool FilterMatch(Snippet snippet)
        {
            if (string.IsNullOrWhiteSpace(FilterText)) return true;

            var filterWords = FilterText.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (filterWords.Length == 0) return true;

            foreach (var word in filterWords)
            {
                bool wordMatch = FilterMode switch
                {
                    SnippetFilterMode.Name => MatchOnWordStart(snippet.Title, word),
                    SnippetFilterMode.Tag => MatchOnWordStart(snippet.Tag, word),
                    _ => false
                };

                if (!wordMatch)
                    return false;
            }
            return true;
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
            var vm = new SettingsViewModel(settingsService, _databaseService, UpdateDatabaseHealthStatusAsync);
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
                settingsService.EditorFontFamily = vm.EditorFontFamily;
                settingsService.EditorFontSize = vm.EditorFontSize;
                settingsService.LanguageFontSize = vm.LanguageFontSize;
                settingsService.LanguageFontWeight = vm.LanguageFontWeight;
                settingsService.CategoryFontSize = vm.CategoryFontSize;
                settingsService.CategoryFontWeight = vm.CategoryFontWeight;
                settingsService.SnippetFontSize = vm.SnippetFontSize;
                settingsService.SnippetFontWeight = vm.SnippetFontWeight;
                settingsService.ShowEmptyLanguages = vm.ShowEmptyLanguages;
                settingsService.ShowEmptyCategories = vm.ShowEmptyCategories;
                settingsService.IsNotificationEnabled = vm.IsNotificationEnabled;

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
                EditorFontFamily = vm.EditorFontFamily;
                EditorFontSize = vm.EditorFontSize;
                LanguageFontSize = vm.LanguageFontSize;
                LanguageFontWeight = vm.LanguageFontWeight;
                CategoryFontSize = vm.CategoryFontSize;
                CategoryFontWeight = vm.CategoryFontWeight;
                SnippetFontSize = vm.SnippetFontSize;
                SnippetFontWeight = vm.SnippetFontWeight;
                ShowEmptyLanguages = vm.ShowEmptyLanguages;
                ShowEmptyCategories = vm.ShowEmptyCategories;
                IsNotificationEnabled = vm.IsNotificationEnabled;

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

                //settingsService.SaveSettings(); // Moved to OnWindowClosing
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
                if (IsEditorModified)
                {
                    PerformSave();
                }
            }
            var vm = new LanguageCategoryViewModel(_databaseService);
            _flyoutService.ShowFlyout("flyEditLangCat", vm, "Edit Languages Categories", () =>
            {
                LoadSnippets();
                if (tmpSnippet != null)
                {
                    // This block handles the edge case where the currently active snippet
                    // might have been deleted (e.g., via "Force Delete Category") inside the Language/Category editor.

                    // Flatten the entire collection of snippets from the newly loaded data and check if our snippet's ID is still present.
                    bool snippetStillExists = Languages
                        .SelectMany(l => l.Categories)
                        .SelectMany(c => c.Snippets)
                        .Any(s => s.Id == tmpSnippet.Id);

                    if (snippetStillExists)
                    {
                        // The snippet was not deleted. Restore the selection in the TreeView
                        ExpandAndSelectSnippet(
                            tmpSnippet.Category?.Language?.Id ?? 0,
                            tmpSnippet.CategoryId,
                            tmpSnippet.Id);
                    }
                    else
                    {
                        // The snippet was deleted. To prevent data inconsistency and potential crashes
                        // from actions on a "phantom" snippet, we must reset the editor state completely.
                        SelectedSnippet = null;
                        EditingSnippet = null;
                        EditorText = string.Empty;
                        IsEditorModified = false;
                        UpdateWindowTitle();
                        StatusMessage = "The previously selected snippet was deleted.";
                    }
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

            var vm = new SnippetViewModel(false, new Snippet(), [.. Languages], _databaseService, SelectedCategory);

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

            if (IsEditorModified)
            {
                PerformSave();
                if (!IsNotificationEnabled) return;
                // Show notification
                var content = new NotificationContent
                {
                    Title = "Message",
                    Message = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}",
                    Type = NotificationType.Information
                };
                await notificationManager.ShowAsync(content, areaName: "NotificationWindowArea", expirationTime: TimeSpan.FromSeconds(2));
            }
        }

        private void PerformSave()
        {
            try
            {
                // This method assumes EditingSnippet is not null.
                _databaseService.UpdateSnippetCode(EditingSnippet!.Id, EditorText);

                EditingSnippet.Code = EditorText; // need this because otherwise the old text is displayed ...
                UpdateSnippetInMemory(EditingSnippet);
                StatusMessage = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}";
                IsEditorModified = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving snippet '{EditingSnippet?.Title}': {ex.Message}";
                _ = DialogService.Instance.ShowMessageAsync("Save Error", $"Failed to save snippet '{EditingSnippet?.Title}'.\n\nDetails: {ex.Message}");
            }
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

            try
            {
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
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting snippet '{SelectedSnippet?.Title}': {ex.Message}";
                await DialogService.Instance.ShowMessageAsync("Delete Error", $"Failed to delete snippet '{SelectedSnippet?.Title}'.\n\nDetails: {ex.Message}");
            }
        }

        [RelayCommand]
        private void IncreaseFontSize()
        {
            if (EditorFontSize < 72)
                EditorFontSize += 1;
        }

        [RelayCommand]
        private void DecreaseFontSize()
        {
            if (EditorFontSize > 6)
                EditorFontSize -= 1;
        }

        [RelayCommand]
        private void ResetFontSize()
        {
            EditorFontSize = settingsService.EditorFontSize;
        }

        [RelayCommand]
        private void OpenSearchReplace()
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
            settingsService.PanelLength = (int)SplitViewOpenPaneLength;
            settingsService.LastSnippet = SaveSelectedSnippetState();
            settingsService.IsSearchExpanded = IsSearchExpanded;
            settingsService.IsSnippetMetadataExpanded = IsSnippetMetadataExpanded;
            settingsService.WindowState = WindowState;
            if (WindowState == WindowState.Normal)
            {
                settingsService.WindowX = (int)WindowX;
                settingsService.WindowY = (int)WindowY;
                settingsService.WindowWidth = (int)WindowWidth;
                settingsService.WindowHeight = (int)WindowHeight;
            }
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
                return (_panelOpenIcon != null && _panelCloseIcon != null)
                    ? (IsPaneOpen ? _panelCloseIcon! : _panelOpenIcon!)
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

        public async Task ChangeSelectedSnippetAsync(Snippet? newSnippet)
        {
            if (newSnippet == null) return;

            if (IsEditorModified && EditingSnippet != null)
            {
                var result = await DialogService.Instance.ShowConfirmAsync($"Unsaved Changes for {EditingSnippet?.Title}",
                    $"You have unsaved changes. Do you want to save them?");

                if (result == true)
                {
                    try
                    {
                        _databaseService.UpdateSnippetCode(EditingSnippet!.Id, EditorText);
                        EditingSnippet.Code = EditorText;
                        UpdateSnippetInMemory(EditingSnippet);
                        IsEditorModified = false;
                        StatusMessage = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Error saving snippet '{EditingSnippet?.Title}': {ex.Message}";
                        _ = DialogService.Instance.ShowMessageAsync("Save Error", $"Failed to save snippet '{EditingSnippet?.Title}'.\n\nDetails: {ex.Message}");
                        // If save failed, keep IsEditorModified as true and don't proceed with changing the selected snippet
                        return;
                    }
                }
                else if (result == false)
                {
                    IsEditorModified = false;
                }
                // for cancel which is currently not in the ShowConfirmAsync dialog
                else
                {
                    SelectedSnippet = EditingSnippet;
                    return;
                }
            }
            // Lazy load the snippet code if it hasn't been loaded yet.
            if (!newSnippet.IsCodeLoaded)
            {
                try
                {
                    newSnippet.Code = _databaseService.GetSnippetCode(newSnippet.Id);
                    newSnippet.IsCodeLoaded = true;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading snippet '{newSnippet.Title}'";
                    _ = DialogService.Instance.ShowMessageAsync("Load Error", $"Failed to load content for snippet '{newSnippet.Title}'.\n\nDetails: {ex.Message}");
                    // Revert the TreeView selection and stop the switch.
                    SelectedSnippet = EditingSnippet;
                    return;
                }
            }

            SelectedSnippet = newSnippet;
            EditingSnippet = newSnippet;
            SelectedCategory = newSnippet.Category;

            _isInternalTextUpdate = true;
            EditorText = newSnippet.Code ?? string.Empty;
            _isInternalTextUpdate = false;

            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            var title = "CodeSnip";
            if (SelectedSnippet != null)
                title += " - " + SelectedSnippet.Title;
            if (IsEditorModified)
                title += " *";
            WindowTitle = title;
        }


    }


}


