using System.Windows;

namespace CodeSnip
{
    public class MainWindowSettings
    {
        public bool LoadOnStartup { get; set; } = true;
        public bool EnableFiltering { get; set; } = true;
        public string LastSnippet { get; set; } = "23:72:4";
        public int X { get; set; } = 50;
        public int Y { get; set; } = 50;
        public int Height { get; set; } = 760;
        public int Width { get; set; } = 1200;
        public int PanelLength { get; set; } = 350;
        public bool ShowEmptyLanguages { get; set; } = false;
        public bool ShowEmptyCategories { get; set; } = false;
        public bool IsSearchExpanded { get; set; } = false;
        public bool IsSnippetMetadataExpanded { get; set; } = false;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public bool IsNotificationEnabled { get; set; } = true;
    }

    public class EditorSettings
    {
        public bool TabToSpaces { get; set; } = true;
        public bool EnableEmailLinks { get; set; } = false;
        public bool EnableHyperinks { get; set; } = false;
        public bool HighlightLine { get; set; } = false;
        public int IntendationSize { get; set; } = 4;
        public bool EnableBraceStyleFolding { get; set; } = false;
        public bool EnablePythonFolding { get; set; } = false;
        public bool EnableXmlFolding { get; set; } = false;
        public string EditorFontFamily { get; set; } = "Consolas";
        public int EditorFontSize { get; set; } = 14;

    }
    public class ThemeSettings
    {
        public string BaseColor { get; set; } = "Dark";
        public string Accent { get; set; } = "Sienna";
    }

    public class TreeViewFontSettings
    {
        public int LanguageFontSize { get; set; } = 15;
        public string LanguageFontWeight { get; set; } = "SemiBold";
        public int CategoryFontSize { get; set; } = 14;
        public string CategoryFontWeight { get; set; } = "Medium";
        public int SnippetFontSize { get; set; } = 14;
        public string SnippetFontWeight { get; set; } = "Normal";
    }

    public class AppSettings
    {
        public MainWindowSettings MainWindow { get; set; } = new MainWindowSettings();
        public ThemeSettings Theme { get; set; } = new ThemeSettings();
        public EditorSettings Editor { get; set; } = new EditorSettings();
        public TreeViewFontSettings TreeViewFont { get; set; } = new TreeViewFontSettings();
    }
}
