using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Theming;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace CodeSnip.Views.HighlightingEditorView
{
    public partial class HighlightingColorInfo : ObservableObject
    {
        public string Name { get; set; } = string.Empty;

        [ObservableProperty]
        private Color? foreground;

        [ObservableProperty]
        private Color? background;

        [ObservableProperty]
        private FontWeight fontWeight = FontWeights.Normal;

        [ObservableProperty]
        private FontStyle fontStyle = FontStyles.Normal;

        public ObservableCollection<FontWeight> AvailableFontWeights { get; } = new ObservableCollection<FontWeight>()
    {
        FontWeights.Normal,
        FontWeights.Bold
    };
        public ObservableCollection<FontStyle> AvailableFontStyles { get; } = new ObservableCollection<FontStyle>()
    {
        FontStyles.Normal,
        FontStyles.Italic,
        FontStyles.Oblique
    };
    }

    public partial class HighlightingEditorViewModel : ObservableObject
    {
        private readonly IHighlightingDefinition _originalDefinition;
        private readonly ICSharpCode.AvalonEdit.TextEditor _editor;
        private readonly string _themeName;
        private readonly string _languageCode;
        private readonly string _customXshdPath;

        [ObservableProperty]
        private string? message;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetDefinitionCommand))]
        private bool _customDefinitionExists;

        private bool CanResetDefinition() => CustomDefinitionExists;

        public ObservableCollection<HighlightingColorInfo> HighlightingColors { get; } = new();

        [ObservableProperty]
        private HighlightingColorInfo? selectedColor;


        public HighlightingEditorViewModel(IHighlightingDefinition definition, ICSharpCode.AvalonEdit.TextEditor editor)
        {
            _originalDefinition = definition;
            _editor = editor;

            if (definition == null)
            {
                Message = "No highlighting definition loaded.";
                _themeName = "Dark"; // Default
                _languageCode = "text"; // Default
                _customXshdPath = string.Empty;
                return;
            }

            LoadHighlightingColors();
            Message = $"Customize syntax highlighting for the {definition.Name} language:";

            var theme = ThemeManager.Current.DetectTheme(Application.Current);
            _themeName = theme?.BaseColorScheme ?? "Dark";

            _languageCode = GetLanguageCodeFromDefinition(definition);

            string appBase = AppDomain.CurrentDomain.BaseDirectory;
            _customXshdPath = Path.Combine(appBase, "Highlighting", _themeName, $"{_languageCode}.xshd");

            UpdateCustomDefinitionExists();
        }

        private string GetLanguageCodeFromDefinition(IHighlightingDefinition definition)
        {
            if (definition.Properties.TryGetValue("Extension", out string? ext) && !string.IsNullOrWhiteSpace(ext))
            {
                return ext.TrimStart('.').ToLowerInvariant();
            }
            if (!string.IsNullOrWhiteSpace(definition.Name))
            {
                return definition.Name.ToLowerInvariant();
            }
            return "custom";
        }

        private void UpdateCustomDefinitionExists()
        {
            CustomDefinitionExists = File.Exists(_customXshdPath);
        }

        [RelayCommand(CanExecute = nameof(CanResetDefinition))]
        private async Task ResetDefinition()
        {
            var confirmed = await DialogService.Instance.ShowConfirmAsync(
                "Reset Definition",
                "Are you sure you want to delete your custom highlighting and revert to the default?",
                "Reset", "Cancel");

            if (!confirmed) return;

            try
            {
                File.Delete(_customXshdPath);
                HighlightingService.InvalidateCache(_languageCode, _themeName);

                // Re-apply the original highlighting to the editor, which will load from resources
                HighlightingService.ApplyHighlighting(_editor, _languageCode);

                // Reload the colors in this view from the now-active original definition
                if (_editor.SyntaxHighlighting != null)
                {
                    var colors = HighlightingParser.ExtractColors(_editor.SyntaxHighlighting);
                    HighlightingColors.Clear();
                    foreach (var color in colors)
                        HighlightingColors.Add(color);
                    SelectedColor = null;
                }

                UpdateCustomDefinitionExists();
                await DialogService.Instance.ShowMessageAsync("Success", "Highlighting definition has been reset to default.");
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", $"Failed to reset definition: {ex.Message}");
            }
        }

        private void LoadHighlightingColors()
        {
            HighlightingColors.Clear();

            var colors = HighlightingParser.ExtractColors(_originalDefinition);
            foreach (var color in colors)
                HighlightingColors.Add(color);
        }

        [RelayCommand]
        private void ApplyLivePreview()
        {

            using var ms = new MemoryStream();
            string? xshdXml = HighlightingService.CurrentXshdXml;
            if (xshdXml != null)
            {
                var doc = XDocument.Parse(xshdXml);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                doc.Root?.Elements(ns + "Color").Remove();

                foreach (var color in HighlightingColors)
                {
                    var colorElem = new XElement(ns + "Color",
                        new XAttribute("name", color.Name));

                    if (color.Foreground.HasValue)
                        colorElem.SetAttributeValue("foreground", color.Foreground.Value.ToString());

                    if (color.Background.HasValue)
                        colorElem.SetAttributeValue("background", color.Background.Value.ToString());

                    if (color.FontWeight != FontWeights.Normal)
                        colorElem.SetAttributeValue("fontWeight", color.FontWeight.ToString().ToLowerInvariant());

                    if (color.FontStyle != FontStyles.Normal)
                        colorElem.SetAttributeValue("fontStyle", color.FontStyle.ToString().ToLowerInvariant());

                    doc.Root?.AddFirst(colorElem);
                }

                doc.Save(ms);
                ms.Position = 0;

                using var finalReader = new XmlTextReader(ms);
                var updated = HighlightingLoader.Load(finalReader, HighlightingManager.Instance);
                _editor.SyntaxHighlighting = updated;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrEmpty(_customXshdPath))
            {
                await DialogService.Instance.ShowMessageAsync("Error", "Cannot determine the path to save the definition file.");
                return;
            }

            try
            {
                string? inputXshdXml = HighlightingService.CurrentXshdXml;
                if (inputXshdXml == null)
                {
                    await DialogService.Instance.ShowMessageAsync("Error", "The original highlighting definition source (XSHD) is not available.");
                    return;
                }

                HighlightingSerializer.SaveColorOverrides(inputXshdXml, _customXshdPath, HighlightingColors.ToList());

                HighlightingService.InvalidateCache(_languageCode, _themeName);
                UpdateCustomDefinitionExists();

                await DialogService.Instance.ShowMessageAsync("Success", $"Custom syntax definition saved to:\n{_customXshdPath}");
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", $"Failed to save definition: {ex.Message}");
            }
        }

    }

    public static class HighlightingParser
    {
        public static List<HighlightingColorInfo> ExtractColors(IHighlightingDefinition definition)
        {
            var result = new List<HighlightingColorInfo>();

            foreach (var named in definition.NamedHighlightingColors)
            {
                var fg = named.Foreground?.GetColor(null);
                var bg = named.Background?.GetColor(null);

                var colorInfo = new HighlightingColorInfo
                {
                    Name = named.Name,
                    Foreground = fg,
                    Background = bg,
                    FontWeight = named.FontWeight ?? FontWeights.Normal,
                    FontStyle = named.FontStyle ?? FontStyles.Normal
                };

                result.Add(colorInfo);
            }
            return result;
        }

    }

    /// <summary>
    /// Loads XSHD from the original file and replaces only the <Color> elements according to the given colors, without changing the rules.
    /// </summary>
    public static class HighlightingSerializer
    {
        public static void SaveColorOverrides(string xshdXml, string outputPath, List<HighlightingColorInfo> overrides)
        {
            var doc = XDocument.Parse(xshdXml);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            doc.Root?.Elements(ns + "Color").Remove();

            foreach (var color in overrides)
            {
                var colorElem = new XElement(ns + "Color",
                    new XAttribute("name", color.Name));

                if (color.Foreground.HasValue)
                    colorElem.SetAttributeValue("foreground", color.Foreground.Value.ToString());

                if (color.Background.HasValue)
                    colorElem.SetAttributeValue("background", color.Background.Value.ToString());

                if (color.FontWeight != FontWeights.Normal)
                    colorElem.SetAttributeValue("fontWeight", color.FontWeight.ToString().ToLowerInvariant());

                if (color.FontStyle != FontStyles.Normal)
                    colorElem.SetAttributeValue("fontStyle", color.FontStyle.ToString().ToLowerInvariant());

                doc.Root?.AddFirst(colorElem);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            doc.Save(outputPath);
        }

    }
}

