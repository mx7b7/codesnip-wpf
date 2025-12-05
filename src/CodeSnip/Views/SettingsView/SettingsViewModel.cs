using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Theming;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
namespace CodeSnip.Views.SettingsView
{


    internal partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        private readonly DatabaseService _databaseService;

        private readonly Func<Task>? _onDatabaseActionCompleted;

        // Theme
        [ObservableProperty]
        private bool isDarkTheme = true;

        [ObservableProperty]
        private AccentInfo? selectedAccent;

        public ObservableCollection<AccentInfo> AccentColors { get; } = [];

        // Main Window
        [ObservableProperty]
        private bool _loadOnStartup;

        [ObservableProperty]
        private bool _enableFiltering;

        [ObservableProperty]
        private bool _isNotificationEnabled;

        // Editor
        [ObservableProperty]
        public bool _tabToSpaces;

        [ObservableProperty]
        public bool _emailLinks;

        [ObservableProperty]
        public bool _hyperLinks;

        [ObservableProperty]
        public bool _highlightLine;

        [ObservableProperty]
        public int _intendationSize;

        [ObservableProperty]
        public bool _enableBraceStyleFolding;

        [ObservableProperty]
        public bool _enablePythonFolding;

        [ObservableProperty]
        public bool _enableXmlFolding;

        [ObservableProperty]
        private bool _showEmptyLanguages;

        [ObservableProperty]
        private bool _showEmptyCategories;

        [ObservableProperty]
        private bool _showLineNumbers;

        // Database
        [ObservableProperty]
        private string _integrityCheckBadge = "";

        [ObservableProperty]
        private string _vacuumBadge = "";

        [ObservableProperty]
        private string _reindexBadge = "";

        [ObservableProperty]
        private string _backupBadge = "";

        [ObservableProperty]
        private string _editorFontFamily;

        [ObservableProperty]
        private int _editorFontSize;

        [ObservableProperty]
        private int _languageFontSize;

        [ObservableProperty]
        private string _languageFontWeight;

        [ObservableProperty]
        private int _categoryFontSize;

        [ObservableProperty]
        private string _categoryFontWeight;

        [ObservableProperty]
        private int _snippetFontSize;

        [ObservableProperty]
        private string _snippetFontWeight;

        public ObservableCollection<FontFamily> SystemFonts { get; } = new ObservableCollection<FontFamily>(Fonts.SystemFontFamilies);

        public List<string> FontWeights { get; } = ["Black", "Bold", "DemiBold", "ExtraBlack", "ExtraBold", "ExtraLight", "Heavy", "Light", "Medium", "Normal", "Regular", "SemiBold", "Thin"];

        public SettingsViewModel(SettingsService settingsService, DatabaseService databaseService, Func<Task>? onDatabaseActionCompleted = null)
        {
            _settingsService = settingsService;
            _databaseService = databaseService;
            _onDatabaseActionCompleted = onDatabaseActionCompleted;
            LoadAccents();
            InitializeFromCurrentTheme();
            LoadOnStartup = _settingsService.LoadOnStartup;
            EnableFiltering = _settingsService.EnableFiltering;
            IsNotificationEnabled = _settingsService.IsNotificationEnabled;
            TabToSpaces = _settingsService.TabToSpaces;
            EmailLinks = _settingsService.EnableEmailLinks;
            HyperLinks = _settingsService.EnableHyperinks;
            HighlightLine = _settingsService.HighlightLine;
            IntendationSize = _settingsService.IntendationSize;
            ShowLineNumbers = _settingsService.ShowLineNumbers;
            EnableBraceStyleFolding = _settingsService.EnableBraceStyleFolding;
            EnablePythonFolding = _settingsService.EnablePythonFolding;
            EnableXmlFolding = _settingsService.EnableXmlFolding;
            EditorFontFamily = _settingsService.EditorFontFamily;
            EditorFontSize = _settingsService.EditorFontSize;
            LanguageFontSize = _settingsService.LanguageFontSize;
            LanguageFontWeight = _settingsService.LanguageFontWeight;
            CategoryFontSize = _settingsService.CategoryFontSize;
            CategoryFontWeight = _settingsService.CategoryFontWeight;
            SnippetFontSize = _settingsService.SnippetFontSize;
            SnippetFontWeight = _settingsService.SnippetFontWeight;
            ShowEmptyLanguages = _settingsService.ShowEmptyLanguages;
            ShowEmptyCategories = _settingsService.ShowEmptyCategories;
        }

        partial void OnLoadOnStartupChanged(bool value)
        {
            _settingsService.LoadOnStartup = value;
        }
        public void InitializeFromCurrentTheme()
        {
            var theme = ThemeManager.Current.DetectTheme(Application.Current);
            if (theme is null)
                return;

            IsDarkTheme = theme.BaseColorScheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

            SelectedAccent = AccentColors.FirstOrDefault(a => a.Name == theme.ColorScheme);
        }

        private void LoadAccents()
        {
            var accents = ThemeManager.Current.Themes
                .GroupBy(t => t.ColorScheme)
                .Select(g => g.First())
                .OrderBy(t => t.ColorScheme);

            foreach (var theme in accents)
            {
                AccentColors.Add(new AccentInfo
                {
                    Name = theme.ColorScheme,
                    ColorBrush = new SolidColorBrush(GetColorFromBrush(theme.ShowcaseBrush))
                });
            }

            var currentTheme = ThemeManager.Current.DetectTheme();
            if (currentTheme is not null)
            {
                SelectedAccent = AccentColors.FirstOrDefault(a =>
                    string.Equals(a.Name, currentTheme.ColorScheme, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static Color GetColorFromBrush(Brush brush)
        {
            return brush switch
            {
                SolidColorBrush solid => solid.Color,
                GradientBrush gradient => gradient.GradientStops.FirstOrDefault()?.Color ?? Colors.Transparent,
                _ => Colors.Transparent
            };
        }

        [RelayCommand]
        partial void OnIsDarkThemeChanged(bool value)
        {
            string baseColor = value ? "Dark" : "Light";
            ThemeManager.Current.ChangeThemeBaseColor(Application.Current, baseColor);
            _settingsService.BaseColor = baseColor;
            HighlightingService.ApplyHighlighting(((MainWindow)Application.Current.MainWindow).textEditor,
    ((MainViewModel)((MainWindow)Application.Current.MainWindow).DataContext).SelectedSnippet?.Category?.Language?.Code);

        }

        [RelayCommand]
        partial void OnSelectedAccentChanged(AccentInfo? value)
        {
            if (value is not null)
            {
                ThemeManager.Current.ChangeThemeColorScheme(Application.Current, value.Name);
                _settingsService.AccentColor = value.Name;
            }
        }

        partial void OnEnableFilteringChanged(bool value)
        {
            _settingsService.EnableFiltering = value;
        }

        partial void OnEditorFontFamilyChanged(string value)
        {
            _settingsService.EditorFontFamily = value;
        }

        partial void OnEditorFontSizeChanged(int value)
        {
            _settingsService.EditorFontSize = value;
        }
        partial void OnShowLineNumbersChanged(bool value)
        {
            _settingsService.ShowLineNumbers = value;
        }

        partial void OnLanguageFontSizeChanged(int value)
        {
            _settingsService.LanguageFontSize = value;
        }

        partial void OnLanguageFontWeightChanged(string value)
        {
            _settingsService.LanguageFontWeight = value;
        }

        partial void OnCategoryFontSizeChanged(int value)
        {
            _settingsService.CategoryFontSize = value;
        }

        partial void OnCategoryFontWeightChanged(string value)
        {
            _settingsService.CategoryFontWeight = value;
        }

        partial void OnSnippetFontSizeChanged(int value)
        {
            _settingsService.SnippetFontSize = value;
        }

        partial void OnSnippetFontWeightChanged(string value)
        {
            _settingsService.SnippetFontWeight = value;
        }

        [RelayCommand]
        private async Task IntegrityCheck()
        {
            var result = await _databaseService.RunIntegrityCheckAsync();
            if (result)
            {
                IntegrityCheckBadge = "✓";
                await Task.Delay(1500);
                IntegrityCheckBadge = "";
            }
            else
            {
                IntegrityCheckBadge = "✗";
            }
        }

        [RelayCommand]
        private async Task Vacuum()
        {
            var result = await _databaseService.RunVacuumAsync();
            if (result)
            {
                VacuumBadge = "✓";
                await Task.Delay(1500);
                _onDatabaseActionCompleted?.Invoke();
                VacuumBadge = "";
            }
            else
            {
                VacuumBadge = "✗";
            }
        }

        [RelayCommand]
        private async Task Reindex()
        {
            var result = await _databaseService.RunReindexAsync();
            if (result)
            {
                ReindexBadge = "✓";
                await Task.Delay(1500);
                ReindexBadge = "";
            }
            else
            {
                ReindexBadge = "✗";
            }
        }
        [RelayCommand]
        private async Task Backup()
        {
            try
            {
                string appFolder = AppDomain.CurrentDomain.BaseDirectory;
                string dbFilePath = Path.Combine(appFolder, "snippets.sqlite");
                if (!File.Exists(dbFilePath))
                {
                    await DialogService.Instance.ShowMessageAsync("Backup Failed", "Database file not found.");
                    BackupBadge = "✗";
                    return;
                }

                string backupFolder = Path.Combine(appFolder, "Backups");
                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);

                string backupFileName = $"snippets-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.sqlite";
                string backupFilePath = Path.Combine(backupFolder, backupFileName);

                File.Copy(dbFilePath, backupFilePath);

                BackupBadge = "✓";
                await DialogService.Instance.ShowMessageAsync("Backup Success", $"Backup created:\n{backupFileName}");
                await Task.Delay(1000);
                BackupBadge = "";
            }
            catch (Exception ex)
            {
                BackupBadge = "✗";
                await DialogService.Instance.ShowMessageAsync("Backup Failed", ex.Message);
            }
        }

    } // SettingsViewModel


    public class AccentInfo
    {
        public string Name { get; set; } = "";
        public Brush ColorBrush { get; set; } = Brushes.Transparent;
    }



} // namespace
