using ControlzEx.Theming;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace CodeSnip.Services
{
    public static class HighlightingService
    {
        // cache: key = "<themeFolder>/<langCode>", value = IHighlightingDefinition
        private static readonly ConcurrentDictionary<string, IHighlightingDefinition> _highlightCache = new();
        public static string? CurrentXshdXml { get; private set; }

        private static void ApplyFoldingMarkerColors(TextEditor editor, string themeBase)
        {
            SolidColorBrush back, hover, fore;

            if (themeBase.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                back = new SolidColorBrush(Color.FromRgb(37, 37, 37));
                hover = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                fore = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            }
            else
            {
                back = new SolidColorBrush(Color.FromRgb(255, 255, 255));      // White
                hover = new SolidColorBrush(Color.FromRgb(224, 224, 224));     // Light gray
                fore = new SolidColorBrush(Color.FromRgb(96, 96, 96));         // Medium gray
            }

            FoldingMargin.SetFoldingMarkerBackgroundBrush(editor, back);
            FoldingMargin.SetSelectedFoldingMarkerBackgroundBrush(editor, hover);
            FoldingMargin.SetFoldingMarkerBrush(editor, fore);
            FoldingMargin.SetSelectedFoldingMarkerBrush(editor, fore);
        }

        public static void ApplyHighlighting(TextEditor editor, string? langCode)
        {
            if (editor is null) return;

            string themeBase = ThemeManager.Current.DetectTheme(Application.Current)?.BaseColorScheme ?? "Dark";

            if (themeBase.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                ApplyHighlightingWithTheme(editor, langCode, "Dark");
            }
            else
            {
                ApplyHighlightingWithTheme(editor, langCode, "Light");
            }
            ApplyFoldingMarkerColors(editor, themeBase);
        }

        private static void ApplyHighlightingWithTheme(TextEditor editor, string? langCode, string themeFolder)
        {
            if (string.IsNullOrWhiteSpace(langCode))
            {
                editor.SyntaxHighlighting = null;
                return;
            }

            try
            {
                editor.ClearValue(TextEditor.ForegroundProperty);
                if (themeFolder.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                    editor.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F8F2"));

                string key = $"{themeFolder}/{langCode.ToLower()}";

                // Is the definition in the cache?
                if (_highlightCache.TryGetValue(key, out var cachedHighlighting))
                {
                    editor.SyntaxHighlighting = cachedHighlighting;
                    return;
                }

                // Not in cache → load and store in cache
                string relativePath = Path.Combine("Highlighting", themeFolder, $"{langCode.ToLower()}.xshd");
                IHighlightingDefinition? loadedHighlighting = LoadHighlightingFromPath(relativePath);

                if (loadedHighlighting != null)
                {
                    _highlightCache[key] = loadedHighlighting;
                    editor.SyntaxHighlighting = loadedHighlighting;
                }
                else
                {
                    editor.SyntaxHighlighting = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HighlightingService] Error loading highlighting for '{langCode}' in {themeFolder}: {ex.Message}");
                editor.SyntaxHighlighting = null;
            }
        }

        /// <summary>
        /// Loads the XSHD definition first from disk, if not found, from resources.
        /// Returns null if not found.
        /// </summary>
        private static IHighlightingDefinition? LoadHighlightingFromPath(string relativeXshdPath)
        {
            try
            {
                string appBase = AppDomain.CurrentDomain.BaseDirectory;
                string diskFilePath = Path.Combine(appBase, relativeXshdPath);

                string? xshdXml = null;

                if (File.Exists(diskFilePath))
                {
                    xshdXml = File.ReadAllText(diskFilePath);
                }
                else
                {
                    Uri resourceUri = new Uri($"/CodeSnip;component/Resources/{relativeXshdPath.Replace('\\', '/')}", UriKind.Relative);
                    var resourceInfo = Application.GetResourceStream(resourceUri);

                    if (resourceInfo is not null)
                    {
                        using Stream resourceStream = resourceInfo.Stream;
                        using StreamReader reader = new(resourceStream);
                        xshdXml = reader.ReadToEnd();
                    }
                }

                if (xshdXml == null)
                    return null;

                CurrentXshdXml = xshdXml;

                using StringReader stringReader = new(xshdXml);
                using XmlReader xmlReader = XmlReader.Create(stringReader);

                return HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HighlightingService] Failed to load highlighting file '{relativeXshdPath}': {ex.Message}");
                return null;
            }
        }

        public static void InvalidateCache(string langCode, string themeFolder)
        {
            if (string.IsNullOrWhiteSpace(langCode) || string.IsNullOrWhiteSpace(themeFolder))
                return;

            string key = $"{themeFolder}/{langCode.ToLower()}";
            _highlightCache.TryRemove(key, out _);
        }

    }
}
