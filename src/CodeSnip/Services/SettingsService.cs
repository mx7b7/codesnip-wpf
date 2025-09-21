using ControlzEx.Theming;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace CodeSnip.Services
{
    public class SettingsService
    {

        private const string SettingsFile = "appsettings.json";
        private AppSettings _settings;

        public bool LoadOnStartup
        {
            get => _settings.MainWindow.LoadOnStartup;
            set => _settings.MainWindow.LoadOnStartup = value;
        }

        public bool EnableFiltering
        {
            get => _settings.MainWindow.EnableFiltering;
            set => _settings.MainWindow.EnableFiltering = value;
        }

        public string LastSnippet
        {
            get => _settings.MainWindow.LastSnippet;
            set => _settings.MainWindow.LastSnippet = value;
        }

        public int WindowX
        {
            get => _settings.MainWindow.X;
            set => _settings.MainWindow.X = value;
        }
        public int WindowY
        {
            get => _settings.MainWindow.Y;
            set => _settings.MainWindow.Y = value;
        }

        public int WindowWidth
        {
            get => _settings.MainWindow.Width;
            set => _settings.MainWindow.Width = value;
        }

        public int WindowHeight
        {
            get => _settings.MainWindow.Height;
            set => _settings.MainWindow.Height = value;
        }

        public int PanelLength
        {
            get => _settings.MainWindow.PanelLength;
            set => _settings.MainWindow.PanelLength = value;
        }

        public bool IsSearchExpanded
        {
            get => _settings.MainWindow.IsSearchExpanded;
            set => _settings.MainWindow.IsSearchExpanded = value;
        }
        
        public bool IsSnippetMetadataExpanded
        {
            get => _settings.MainWindow.IsSnippetMetadataExpanded;
            set => _settings.MainWindow.IsSnippetMetadataExpanded = value;
        }

        public WindowState WindowState
        {
            get => _settings.MainWindow.WindowState;
            set => _settings.MainWindow.WindowState = value;
        }

        public bool IsNotificationEnabled
        {
            get => _settings.MainWindow.IsNotificationEnabled;
            set => _settings.MainWindow.IsNotificationEnabled = value;
        }

        public bool TabToSpaces
        {
            get => _settings.Editor.TabToSpaces;
            set => _settings.Editor.TabToSpaces = value;
        }

        public bool EnableEmailLinks
        {
            get => _settings.Editor.EnableEmailLinks;
            set => _settings.Editor.EnableEmailLinks = value;
        }
        
        public bool EnableHyperinks
        {
            get => _settings.Editor.EnableHyperinks;
            set => _settings.Editor.EnableHyperinks = value;
        }
        public bool HighlightLine
        {
            get => _settings.Editor.HighlightLine;
            set => _settings.Editor.HighlightLine = value;
        }
        
        public int IntendationSize
        {
            get => _settings.Editor.IntendationSize;
            set => _settings.Editor.IntendationSize = value;
        }

        public bool EnableBraceStyleFolding
        {
            get => _settings.Editor.EnableBraceStyleFolding;
            set => _settings.Editor.EnableBraceStyleFolding = value;
        }

        public bool EnablePythonFolding
        {
            get => _settings.Editor.EnablePythonFolding;
            set => _settings.Editor.EnablePythonFolding = value;
        }
        public bool EnableXmlFolding
        {
            get => _settings.Editor.EnableXmlFolding;
            set => _settings.Editor.EnableXmlFolding = value;
        }
        public string EditorFontFamily
        {
            get => _settings.Editor.EditorFontFamily;
            set => _settings.Editor.EditorFontFamily = value;
        }

        public int EditorFontSize
        {
            get => _settings.Editor.EditorFontSize;
            set => _settings.Editor.EditorFontSize = value;
        }

        public bool ShowEmptyLanguages
        {
            get => _settings.MainWindow.ShowEmptyLanguages;
            set => _settings.MainWindow.ShowEmptyLanguages = value;
        }

        public bool ShowEmptyCategories
        {
            get => _settings.MainWindow.ShowEmptyCategories;
            set => _settings.MainWindow.ShowEmptyCategories = value;
        }

        public string BaseColor
        {
            get => _settings.Theme.BaseColor;
            set => _settings.Theme.BaseColor = value;
        }

        public string AccentColor
        {
            get => _settings.Theme.Accent;
            set => _settings.Theme.Accent = value;
        }

        public int LanguageFontSize
        {
            get => _settings.TreeViewFont.LanguageFontSize;
            set => _settings.TreeViewFont.LanguageFontSize = value;
        }

        public string LanguageFontWeight
        {
            get => _settings.TreeViewFont.LanguageFontWeight;
            set => _settings.TreeViewFont.LanguageFontWeight = value;
        }

        public int CategoryFontSize
        {
            get => _settings.TreeViewFont.CategoryFontSize;
            set => _settings.TreeViewFont.CategoryFontSize = value;
        }

        public string CategoryFontWeight
        {
            get => _settings.TreeViewFont.CategoryFontWeight;
            set => _settings.TreeViewFont.CategoryFontWeight = value;
        }

        public int SnippetFontSize
        {
            get => _settings.TreeViewFont.SnippetFontSize;
            set => _settings.TreeViewFont.SnippetFontSize = value;
        }

        public string SnippetFontWeight
        {
            get => _settings.TreeViewFont.SnippetFontWeight;
            set => _settings.TreeViewFont.SnippetFontWeight = value;
        }

        // Constructor: loads settings or creates defaults
        public SettingsService()
        {
            _settings = new AppSettings();
            LoadSettings();
            ApplyTheme();
        }

        private void LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile);
                    // Attempt to deserialize. If the file is corrupt, Deserialize will return null or throw an exception.
                    // In that case, we use the default settings (?? new AppSettings()).
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    ValidateAndCorrectSettings();
                }
                catch (JsonException ex)
                {
                    MessageBox.Show($"Error deserializing settings: {ex.Message}. Loading default settings.");
                    _settings = new AppSettings();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading settings file: {ex.Message}. Loading default settings.");
                    _settings = new AppSettings();
                }
            }
            else
            {
                // If the file does not exist, _settings will be initialized automatically 
                // with default values in the AppSettings class constructor.
                _settings = new AppSettings();
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}");
            }
        }
        public void ApplyTheme()
        {
            ThemeManager.Current.ChangeThemeBaseColor(Application.Current, BaseColor);
            ThemeManager.Current.ChangeThemeColorScheme(Application.Current, AccentColor);
        }

        private void ValidateAndCorrectSettings()
        {
            var defaultSettings = new AppSettings();

            // 1. Validate WindowState
            if (!Enum.IsDefined(typeof(WindowState), _settings.MainWindow.WindowState))
            {
                _settings.MainWindow.WindowState = defaultSettings.MainWindow.WindowState; // Reset to 'Normal'
            }

            // 2. Validate EditorFontFamily
            var fontExists = Fonts.SystemFontFamilies.Any(f => f.Source.Equals(_settings.Editor.EditorFontFamily, StringComparison.OrdinalIgnoreCase));
            if (!fontExists)
            {
                _settings.Editor.EditorFontFamily = defaultSettings.Editor.EditorFontFamily; // Reset to 'Consolas'
            }

            // 3. Validate FontWeight properties
            _settings.TreeViewFont.LanguageFontWeight = ValidateFontWeight(_settings.TreeViewFont.LanguageFontWeight, defaultSettings.TreeViewFont.LanguageFontWeight);
            _settings.TreeViewFont.CategoryFontWeight = ValidateFontWeight(_settings.TreeViewFont.CategoryFontWeight, defaultSettings.TreeViewFont.CategoryFontWeight);
            _settings.TreeViewFont.SnippetFontWeight = ValidateFontWeight(_settings.TreeViewFont.SnippetFontWeight, defaultSettings.TreeViewFont.SnippetFontWeight);

            // 4. Validate ThemeSettings
            var accentExists = ThemeManager.Current.Themes.Any(t => t.ColorScheme.Equals(_settings.Theme.Accent, StringComparison.OrdinalIgnoreCase));
            if (!accentExists)
            {
                _settings.Theme.Accent = defaultSettings.Theme.Accent; // Reset to 'Sienna'
            }

            if (!_settings.Theme.BaseColor.Equals("Light", StringComparison.OrdinalIgnoreCase) &&
                !_settings.Theme.BaseColor.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                _settings.Theme.BaseColor = defaultSettings.Theme.BaseColor; // Reset to 'Dark'
            }
        }

        private string ValidateFontWeight(string fontWeightToValidate, string defaultFontWeight)
        {
            if (string.IsNullOrWhiteSpace(fontWeightToValidate))
            {
                return defaultFontWeight;
            }
            try
            {
                new FontWeightConverter().ConvertFromString(fontWeightToValidate);
                return fontWeightToValidate; // The value is valid
            }
            catch
            {
                return defaultFontWeight; // The value is invalid, return the default
            }
        }

    }
}
