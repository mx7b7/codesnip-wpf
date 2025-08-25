using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Theming;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
namespace CodeSnip.Views.SettingsView
{


    internal partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private bool isDarkTheme = true;

        [ObservableProperty]
        private AccentInfo? selectedAccent;

        public ObservableCollection<AccentInfo> AccentColors { get; } = [];

        [ObservableProperty]
        private bool _loadOnStartup;

        [ObservableProperty]
        private bool _enableFiltering;


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


        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadAccents();
            InitializeFromCurrentTheme();
            LoadOnStartup = settingsService.LoadOnStartup;
            EnableFiltering = settingsService.EnableFiltering;
            TabToSpaces = _settingsService.TabToSpaces;
            EmailLinks = _settingsService.EnableEmailLinks;
            HyperLinks = _settingsService.EnableHyperinks;
            HighlightLine = _settingsService.HighlightLine;
            IntendationSize = _settingsService.IntendationSize;
            EnableBraceStyleFolding =_settingsService.EnableBraceStyleFolding;
            EnablePythonFolding = _settingsService.EnablePythonFolding;
            EnableXmlFolding = _settingsService.EnableXmlFolding;
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



    } // SettingsViewModel


    public class AccentInfo
    {
        public string Name { get; set; } = "";
        public Brush ColorBrush { get; set; } = Brushes.Transparent;
    }



} // namespace
