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

    }
    public class ThemeSettings
    {
        public string BaseColor { get; set; } = "Dark";
        public string Accent { get; set; } = "Sienna";
    }

    public class AppSettings
    {
        public MainWindowSettings MainWindow { get; set; } = new MainWindowSettings();
        public ThemeSettings Theme { get; set; } = new ThemeSettings();
        public EditorSettings Editor { get; set; } = new EditorSettings();
    }
}
