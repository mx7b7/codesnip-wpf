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
    /// <summary>
    /// Represents a single named color from a highlighting definition, allowing its properties to be edited.
    /// </summary>
    public partial class HighlightingColorInfo : ObservableObject
    {
        /// <summary>Gets or sets the name of the color definition (e.g., "Comment", "String").</summary>
        public string Name { get; set; } = string.Empty;

        [ObservableProperty]
        private Color? foreground;

        [ObservableProperty]
        private Color? background;

        [ObservableProperty]
        private FontWeight fontWeight = FontWeights.Normal;

        [ObservableProperty]
        private FontStyle fontStyle = FontStyles.Normal;

        [ObservableProperty]
        private bool underline;

        [ObservableProperty]
        private bool strikethrough;

        [ObservableProperty]
        private int? fontSize;

        /// <summary>
        /// Gets the collection of available font weights for the UI.
        /// </summary>
        public ObservableCollection<FontWeight> AvailableFontWeights { get; } = new ObservableCollection<FontWeight>()
    {
        FontWeights.Normal,
        FontWeights.Bold
    };
        /// <summary>
        /// Gets the collection of available font styles for the UI.
        /// </summary>
        public ObservableCollection<FontStyle> AvailableFontStyles { get; } = new ObservableCollection<FontStyle>()
    {
        FontStyles.Normal,
        FontStyles.Italic,
        FontStyles.Oblique
    };
    }

    /// <summary>
    /// ViewModel for the Highlighting Editor. It allows users to customize the colors, font weights,
    /// and styles of a syntax highlighting definition, preview the changes live, and save them as a
    /// custom '.xshd' file.
    /// </summary>
    public partial class HighlightingEditorViewModel : ObservableObject
    {
        private readonly IHighlightingDefinition _originalDefinition;
        private readonly ICSharpCode.AvalonEdit.TextEditor _editor;
        private readonly string _themeName;
        private readonly string _languageCode;
        private readonly string _customXshdPath;

        /// <summary>Gets or sets the informational message displayed at the top of the editor view.</summary>
        [ObservableProperty]
        private string? message;

        /// <summary>Gets or sets a value indicating whether a custom user-defined highlighting file exists.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetDefinitionCommand))]
        private bool _customDefinitionExists;

        private bool CanResetDefinition() => CustomDefinitionExists;

        /// <summary>
        /// Gets the collection of highlighting colors extracted from the current definition.
        /// </summary>
        public ObservableCollection<HighlightingColorInfo> HighlightingColors { get; } = new();

        /// <summary>
        /// Gets or sets the currently selected color item from the list.
        /// </summary>
        [ObservableProperty]
        private HighlightingColorInfo? selectedColor;


        /// <summary>
        /// Initializes a new instance of the <see cref="HighlightingEditorViewModel"/> class.
        /// </summary>
        /// <param name="definition">The active highlighting definition to be edited.</param>
        /// <param name="editor">The TextEditor instance where live previews will be applied.</param>
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

        /// <summary>
        /// Deletes the custom highlighting definition file and reverts the editor to the
        /// default embedded definition.
        /// </summary>
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

        /// <summary>
        /// Applies the current color and style modifications to the editor for a live preview, without saving to disk.
        /// </summary>
        [RelayCommand]
        private void ApplyLivePreview()
        {
            using var ms = new MemoryStream();
            if (TryGetOriginalXshd(out string? xshdXml) && xshdXml != null)
            {
                try
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

                        if (color.Underline)
                            colorElem.SetAttributeValue("underline", "true");

                        if (color.Strikethrough)
                            colorElem.SetAttributeValue("strikethrough", "true");

                        if (color.FontSize.HasValue)
                            colorElem.SetAttributeValue("fontSize", color.FontSize.Value.ToString());

                        doc.Root?.AddFirst(colorElem);
                    }

                    doc.Save(ms);
                    ms.Position = 0;

                    using var finalReader = new XmlTextReader(ms);
                    var updated = HighlightingLoader.Load(finalReader, HighlightingManager.Instance);
                    _editor.SyntaxHighlighting = updated;
                }
                catch (XmlException ex)
                {
                    _ = DialogService.Instance.ShowMessageAsync("Parsing Error", $"The original highlighting definition is corrupt and cannot be parsed.\n\nDetails: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _ = DialogService.Instance.ShowMessageAsync("Error", $"Unexpected error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Saves the current color and style modifications to a custom '.xshd' file in the application's
        /// 'Highlighting' directory. This overrides the default embedded definition for future sessions.
        /// </summary>
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
                if (!TryGetOriginalXshd(out string? inputXshdXml) || inputXshdXml == null) return;
                HighlightingSerializer.SaveColorOverrides(inputXshdXml, _customXshdPath, HighlightingColors.ToList());
                HighlightingService.InvalidateCache(_languageCode, _themeName);
                UpdateCustomDefinitionExists();

                await DialogService.Instance.ShowMessageAsync("Success", $"Custom syntax definition saved to:\n{_customXshdPath}");
            }
            catch (XmlException ex)
            {
                await DialogService.Instance.ShowMessageAsync("Parsing Error", $"Failed to save because the original highlighting definition is corrupt.\n\nDetails: {ex.Message}");
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", $"Failed to save definition: {ex.Message}");
            }
        }

        private bool TryGetOriginalXshd(out string? xshdXml)
        {
            xshdXml = HighlightingService.GetXshdXml(_languageCode, _themeName);
            if (xshdXml == null)
            {
                // This is an async method in a sync context, but it's acceptable for showing a dialog.
                _ = DialogService.Instance.ShowMessageAsync("Error", "The original highlighting definition source (XSHD) could not be found.");
                return false;
            }
            return true;
        }

    }

    /// <summary>
    /// A helper class to parse color information from an <see cref="IHighlightingDefinition"/>.
    /// </summary>
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
                    FontStyle = named.FontStyle ?? FontStyles.Normal,
                    FontSize = named.FontSize,
                    Underline = named.Underline ?? false,
                    Strikethrough = named.Strikethrough ?? false
                };

                result.Add(colorInfo);
            }
            return result;
        }

    }

    /// <summary>
    /// A helper class to serialize highlighting color changes back into an XSHD file format.
    /// </summary>
    public static class HighlightingSerializer
    {
        /// <summary>
        /// Generates a new XSHD file by taking an original XSHD structure and replacing its color definitions with a new set of overrides.
        /// </summary>
        /// <param name="xshdXml">The original XSHD content as an XML string.</param>
        /// <param name="outputPath">The file path where the new XSHD file will be saved.</param>
        /// <param name="overrides">The list of <see cref="HighlightingColorInfo"/> to write into the new file.</param>

        public static void SaveColorOverrides(string xshdXml, string outputPath, List<HighlightingColorInfo> overrides)
        {
            var doc = XDocument.Parse(xshdXml);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var overridesDict = overrides.ToDictionary(o => o.Name);

            var colorElements = doc.Root?.Elements(ns + "Color").ToList();

            if (colorElements != null)
            {
                foreach (var colorElem in colorElements)
                {
                    var name = colorElem.Attribute("name")?.Value;
                    if (name != null && overridesDict.TryGetValue(name, out var colorOverride))
                    {
                        // Preserve and re-apply 'exampleText' to ensure it's last.
                        var exampleTextAttr = colorElem.Attribute("exampleText");
                        string? exampleTextValue = exampleTextAttr?.Value;
                        exampleTextAttr?.Remove();

                        // Update other attributes
                        if (colorOverride.Foreground.HasValue)
                            colorElem.SetAttributeValue("foreground", colorOverride.Foreground.Value.ToString());
                        else
                            colorElem.Attribute("foreground")?.Remove();

                        if (colorOverride.Background.HasValue)
                            colorElem.SetAttributeValue("background", colorOverride.Background.Value.ToString());
                        else
                            colorElem.Attribute("background")?.Remove();

                        if (colorOverride.FontWeight != FontWeights.Normal)
                            colorElem.SetAttributeValue("fontWeight", colorOverride.FontWeight.ToString().ToLowerInvariant());
                        else
                            colorElem.Attribute("fontWeight")?.Remove();

                        if (colorOverride.FontStyle != FontStyles.Normal)
                            colorElem.SetAttributeValue("fontStyle", colorOverride.FontStyle.ToString().ToLowerInvariant());
                        else
                            colorElem.Attribute("fontStyle")?.Remove();

                        if (colorOverride.Underline)
                            colorElem.SetAttributeValue("underline", "true");
                        else
                            colorElem.Attribute("underline")?.Remove();

                        if (colorOverride.Strikethrough)
                            colorElem.SetAttributeValue("strikethrough", "true");
                        else
                            colorElem.Attribute("strikethrough")?.Remove();

                        if (colorOverride.FontSize.HasValue)
                            colorElem.SetAttributeValue("fontSize", colorOverride.FontSize.Value.ToString());
                        else
                            colorElem.Attribute("fontSize")?.Remove();
                        // Re-add 'exampleText' at the end.
                        if (exampleTextValue != null)
                        {
                            colorElem.SetAttributeValue("exampleText", exampleTextValue);
                        }
                    }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            doc.Save(outputPath);
        }

    }
}
