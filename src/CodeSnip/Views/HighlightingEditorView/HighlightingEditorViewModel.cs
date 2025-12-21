using CodeSnip.EditorHelpers;
using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Theming;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace CodeSnip.Views.HighlightingEditorView
{
    public partial class HighlightingEditorViewModel : ObservableObject
    {
        private XshdValidationService validationService = new();
        #region Fields
        private readonly IHighlightingDefinition? _originalDefinition;
        private readonly TextEditor _mainEditor = null!;
        private readonly string _themeName = null!;
        private readonly string _languageCode = null!;
        private readonly string _customXshdPath = null!;
        #endregion

        #region Properties
        [ObservableProperty] private string? message;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetDefinitionCommand))]
        private bool customDefinitionExists;

        public ObservableCollection<HighlightingColorInfo> HighlightingColors { get; } = [];

        [ObservableProperty] private HighlightingColorInfo? selectedColor;

        [ObservableProperty] private string? xshdText;

        public IHighlightingDefinition? XmlHighlighting { get; private set; }

        public Brush? XshdEditorForeground { get; private set; }

        [ObservableProperty] private string syncBadge = string.Empty;

        [ObservableProperty] private Brush? syncBadgeBackground = Brushes.Transparent;

        public TextEditor? XshdEditor { get; set; }

        #endregion

        #region Constructor
        public HighlightingEditorViewModel(IHighlightingDefinition? definition, TextEditor editor, string langCode)
        {
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(langCode);

            _originalDefinition = definition;
            _mainEditor = editor;

            if (definition == null)
            {
                return;
            }

            var theme = ThemeManager.Current.DetectTheme(Application.Current);
            _themeName = theme?.BaseColorScheme ?? "Dark";
            _languageCode = langCode;
            _customXshdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Highlighting", _themeName, $"{_languageCode}.xshd");

            LoadHighlightingColors();
            UpdateCustomDefinitionExists();

            if (!TryGetOriginalXshd(out string? xshdXml))
                return;

            XshdText = xshdXml;
            InitializeXshdEditor();
            Message = $"Customize syntax highlighting for the {definition.Name} language:";
        }

        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(CanResetDefinition))]
        private async Task ResetDefinition()
        {
            var confirmed = await DialogService.Instance.ShowConfirmAsync("Reset Definition",
                "Are you sure you want to delete your custom highlighting and revert to the default?", "Reset", "Cancel");
            if (!confirmed) return;

            try
            {
                if (File.Exists(_customXshdPath))
                    File.Delete(_customXshdPath);

                HighlightingService.InvalidateCache(_languageCode, _themeName);
                HighlightingService.ApplyHighlighting(_mainEditor, _languageCode);

                if (TryGetOriginalXshd(out string? xshdXml))
                    XshdText = xshdXml;

                LoadHighlightingColors();
                UpdateCustomDefinitionExists();
                await DialogService.Instance.ShowMessageAsync("Success", "Highlighting definition has been reset to default.");
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", $"Failed to reset definition: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ApplyLivePreview()
        {
            var (isValid, definition) = await ValidateAndLoadDefinitionAsync(XshdText);
            if (isValid && definition != null)
            {
                _mainEditor.SyntaxHighlighting = definition;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrEmpty(_customXshdPath) || string.IsNullOrWhiteSpace(XshdText))
            {
                await DialogService.Instance.ShowMessageAsync("Error", "Cannot save - invalid path or empty content.");
                return;
            }

            var (isValid, _) = await ValidateAndLoadDefinitionAsync(XshdText);
            if (!isValid) return;

            try
            {
                var doc = XDocument.Parse(XshdText);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var extensionProperty = doc.Root?.Descendants(ns + "Property")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == "Extension");

                if (extensionProperty != null)
                    extensionProperty.SetAttributeValue("value", _languageCode);
                else
                    doc.Root?.Add(new XElement(ns + "Property", new XAttribute("name", "Extension"), new XAttribute("value", _languageCode)));

                string textToSave = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + doc.ToString();
                if (textToSave != XshdText) XshdText = textToSave;

                Directory.CreateDirectory(Path.GetDirectoryName(_customXshdPath)!);
                await File.WriteAllTextAsync(_customXshdPath, XshdText, Encoding.UTF8);

                HighlightingService.InvalidateCache(_languageCode, _themeName);
                UpdateCustomDefinitionExists();
                HighlightingService.ApplyHighlighting(_mainEditor, _languageCode);
                await DialogService.Instance.ShowMessageAsync("Success", $"Saved to:\n{_customXshdPath}");
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Error", $"Failed to save: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SyncFromRaw()
        {
            var (isValid, tempDefinition) = await ValidateAndLoadDefinitionAsync(XshdText);
            if (!isValid || tempDefinition == null) return;

            try
            {
                var newColors = HighlightingParser.ExtractColors(tempDefinition);
                ApplyColors(newColors);

                SyncBadge = "✔";
                SyncBadgeBackground = Brushes.LimeGreen;
                await Task.Delay(1200);
                SyncBadge = string.Empty;
            }
            catch (Exception ex)
            {
                SyncBadge = "✘";
                SyncBadgeBackground = Brushes.OrangeRed;
                _ = DialogService.Instance.ShowMessageAsync("Error", $"An unexpected error occurred while synchronizing the highlighting definition.\n\n{ex.Message}");
            }
        }

        [RelayCommand]
        private void FormatXshd()
        {
            if (XshdEditor?.Document is not { } document || XshdText is null)
                return;

            try
            {
                string formatted = FormatXml(XshdText);
                if (formatted == XshdText) return;

                document.BeginUpdate();
                XshdEditor.SelectAll();
                document.Replace(0, document.TextLength, formatted);
                document.EndUpdate();
                XshdText = formatted;
                XshdEditor.SelectionLength = 0;
            }
            catch (XmlException ex)
            {
                _ = DialogService.Instance.ShowMessageAsync("Format Error", $"Invalid XML: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ToggleComment()
        {
            try
            {
                if (XshdEditor == null) return;

                CommentHelper.ToggleCommentByExtension(XshdEditor, "xml", useMultiLine: true);
            }
            catch { }
        }

        #endregion

        #region Private Methods
        private bool CanResetDefinition() => CustomDefinitionExists;

        private void UpdateCustomDefinitionExists() => CustomDefinitionExists = File.Exists(_customXshdPath);

        private void OnHighlightingColorChanged(object? sender, EventArgs e) => UpdateXshdTextFromColors();

        private static string FormatXml(string xml)
        {
            var doc = XDocument.Parse(xml);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + doc.ToString();
        }

        private void LoadHighlightingColors()
        {
            foreach (var color in HighlightingColors)
                color.PropertyChangedWithValue -= OnHighlightingColorChanged;

            HighlightingColors.Clear();
            if (_originalDefinition == null) return;

            var colors = HighlightingParser.ExtractColors(_originalDefinition);
            foreach (var color in colors)
            {
                color.PropertyChangedWithValue += OnHighlightingColorChanged;
                HighlightingColors.Add(color);
            }
            SelectedColor = HighlightingColors.FirstOrDefault();
        }

        private void InitializeXshdEditor()
        {
            string? xmlXshd = HighlightingService.GetXshdXml("xml", _themeName);
            if (xmlXshd != null)
            {
                using var reader = new StringReader(xmlXshd);
                using var xmlReader = XmlReader.Create(reader);
                XmlHighlighting = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            }
            else
            {
                XmlHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".xml");
            }

            XshdEditorForeground = Brushes.Black;
            if (_themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                XshdEditorForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F8F8"));
        }

        private bool TryGetOriginalXshd(out string? xshdXml)
        {
            xshdXml = HighlightingService.GetXshdXml(_languageCode, _themeName);
            return xshdXml != null;
        }

        private void UpdateXshdTextFromColors()
        {
            if (string.IsNullOrWhiteSpace(XshdText)) return;
            try
            {
                XshdText = HighlightingSerializer.GenerateUpdatedXshd(XshdText, [.. HighlightingColors]);
            }
            catch { }
        }

        private void ApplyColors(IEnumerable<HighlightingColorInfo> newColors)
        {
            string? selectedColorName = SelectedColor?.Name;
            foreach (var c in HighlightingColors)
                c.PropertyChangedWithValue -= OnHighlightingColorChanged;

            HighlightingColors.Clear();
            foreach (var c in newColors)
            {
                c.PropertyChangedWithValue += OnHighlightingColorChanged;
                HighlightingColors.Add(c);
            }

            if (selectedColorName != null)
                SelectedColor = HighlightingColors.FirstOrDefault(c => c.Name == selectedColorName);
        }

        private async Task<(bool IsValid, IHighlightingDefinition? Definition)> ValidateAndLoadDefinitionAsync(string? xshdText)
        {
            if (string.IsNullOrWhiteSpace(xshdText))
            {
                await DialogService.Instance.ShowMessageAsync("Error", "XSHD source is empty.");
                return (false, null);
            }

            var validationResult = validationService.Validate(xshdText);
            if (!validationResult.IsValid)
            {
                await DialogService.Instance.ShowMessageAsync("Validation Errors",
                    $"XSHD definition issues:\n\n{string.Join("\n", validationResult.Errors)}");
                return (false, null);
            }

            try
            {
                using var reader = new StringReader(xshdText);
                using var xmlReader = XmlReader.Create(reader);
                var definition = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
                return (true, definition);
            }
            catch { return (false, null); }
        }

        #endregion
    }

    #region Helper classes

    /// <summary>
    /// Represents a single named color from a highlighting definition, allowing its properties to be edited.
    /// </summary>
    public partial class HighlightingColorInfo : ObservableObject
    {
        public event EventHandler? PropertyChangedWithValue;
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
        public ObservableCollection<FontWeight> AvailableFontWeights { get; } =
    [
        FontWeights.Normal,
        FontWeights.Bold
    ];
        /// <summary>
        /// Gets the collection of available font styles for the UI.
        /// </summary>
        public ObservableCollection<FontStyle> AvailableFontStyles { get; } =
    [
        FontStyles.Normal,
        FontStyles.Italic,
        FontStyles.Oblique
    ];

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            PropertyChangedWithValue?.Invoke(this, EventArgs.Empty);
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

            foreach (var named in definition.NamedHighlightingColors.OrderBy(c => c.Name))
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
        public static string GenerateUpdatedXshd(string xshdXml, List<HighlightingColorInfo> overrides)
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
                        // Helper to set or remove attribute
                        void SetOrRemove(string attrName, string? value)
                        {
                            if (!string.IsNullOrEmpty(value))
                                colorElem.SetAttributeValue(attrName, value);
                            else
                                colorElem.Attribute(attrName)?.Remove();
                        }

                        SetOrRemove("foreground", colorOverride.Foreground?.ToString());
                        SetOrRemove("background", colorOverride.Background?.ToString());
                        SetOrRemove("fontWeight", colorOverride.FontWeight == FontWeights.Normal ? null : colorOverride.FontWeight.ToString().ToLowerInvariant());
                        SetOrRemove("fontStyle", colorOverride.FontStyle == FontStyles.Normal ? null : colorOverride.FontStyle.ToString().ToLowerInvariant());
                        SetOrRemove("underline", colorOverride.Underline ? "true" : null);
                        SetOrRemove("strikethrough", colorOverride.Strikethrough ? "true" : null);
                        SetOrRemove("fontSize", colorOverride.FontSize?.ToString());
                    }
                }
            }

            using var sw = new StringWriterUtf8();
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
            {
                doc.WriteTo(xw);
            }
            return sw.ToString();
        }

    }

    public class StringWriterUtf8 : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }


    #endregion

}
