using CodeSnip.EditorHelpers;
using CodeSnip.Services;
using CodeSnip.Services.Exporters;
using CodeSnip.Views.HighlightingEditorView;
using CodeSnip.Views.LanguageCategoryView;
using CodeSnip.Views.SnippetView;
using ControlzEx.Theming;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using MahApps.Metro.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeSnip
{
    public partial class MainWindow : MetroWindow, IFlyoutService
    {
        private readonly MainViewModel mainViewModel;
        private FoldingManager? foldingManager;
        private object? foldingStrategy;
        private readonly BraceFoldingStrategy braceFoldingStrategy = new();
        private readonly XmlFoldingStrategy xmlFoldingStrategy = new();
        private readonly PythonFoldingStrategy pythonFoldingStrategy = new();
        private readonly DefaultIndentationStrategy defaultIndentationStrategy = new();
        private readonly CSharpIndentationStrategy csharpIndentationStrategy = new();

        private static readonly Brush s_darkThemeHighlighterBrush = new SolidColorBrush(Color.FromArgb(60, 220, 220, 220));
        private static readonly Brush s_lightThemeHighlighterBrush = new SolidColorBrush(Color.FromArgb(60, 100, 100, 100));


        public ICommand ToggleSingleLineCommentCommand { get; }
        public ICommand ToggleMultiLineCommentCommand { get; }
        public ICommand ToggleCommentSelectionCommand { get; }
        public ICommand FormatAllCommand { get; }


        public ICommand OpenAboutCommand { get; }

        // Languages with C-style braces {} that use CSharpIndentationStrategy and BraceFoldingStrategy
        private static readonly HashSet<string> braceStyleLanguages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Original
            "as", "cpp", "cs", "d", "fx", "java", "js", "json", "nut", "php", "rs", "swift",
            "kt", "kts", "groovy", "dart", "v", "sv", "zig", "mm", "h", "c", "go",
            "css", "hcl"
        };

        public MainWindow()
        {
            s_darkThemeHighlighterBrush.Freeze();
            s_lightThemeHighlighterBrush.Freeze();

            InitializeComponent();
            mainViewModel = new MainViewModel(this);
            DataContext = mainViewModel;
            mainViewModel.InitializeEditor(textEditor);

            // This wrapping is only to support keyboard shortcuts from the menu items that are bound to these commands.
            ToggleSingleLineCommentCommand = new RelayCommand(_ => ToggleSingleLineComment_Click(this, new RoutedEventArgs()));
            ToggleMultiLineCommentCommand = new RelayCommand(_ => ToggleMultiLineLineComment_Click(this, new RoutedEventArgs()));
            ToggleCommentSelectionCommand = new RelayCommand(_ => ToggleCommentSelection_Click(this, new RoutedEventArgs()));
            OpenAboutCommand = new RelayCommand(_ => About_Click(this, new RoutedEventArgs()));
            FormatAllCommand = new RelayCommand(_ => FormatAll_Click(this, new RoutedEventArgs()));

            mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;

            mainViewModel.ReplaceLineHighlightRendererRequested += () =>
            {
                ReplaceCurrentLineRenderer(textEditor);
            };
            ReplaceCurrentLineRenderer(textEditor);
        }

        private static void ReplaceCurrentLineRenderer(TextEditor editor)
        {
            var textView = editor.TextArea.TextView;

            // Detect current theme (light or dark)
            var currentTheme = ThemeManager.Current.DetectTheme(Application.Current);
            bool isDarkTheme = currentTheme?.BaseColorScheme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ?? true;

            // Choose appropriate brush based on theme
            Brush borderBrush = isDarkTheme ? s_darkThemeHighlighterBrush : s_lightThemeHighlighterBrush;

            // 3. Remove existing CurrentLineHighlighter instances to avoid duplicates.
            var renderersToRemove = textView.BackgroundRenderers
                .Where(r => r is CurrentLineHighlighter || r.GetType().Name == "CurrentLineHighlightRenderer")
                .ToList();

            foreach (var renderer in renderersToRemove)
            {
                textView.BackgroundRenderers.Remove(renderer);
            }

            // Add the new CurrentLineHighlighter with the selected brush.
            textView.BackgroundRenderers.Add(new CurrentLineHighlighter(editor, borderBrush));
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When the indentation toggle changes, re-apply the strategy for the current snippet.
            if (e.PropertyName == nameof(MainViewModel.DisableIntendation) && mainViewModel.SelectedSnippet != null)
            {
                if (mainViewModel.DisableIntendation)
                {
                    // If indentation is disabled, it's disabled for ALL languages.
                    textEditor.TextArea.IndentationStrategy = null;
                }
                else
                {
                    // If re-enabled, restore the correct strategy based on the current language.
                    string langCode = mainViewModel.SelectedSnippet.Category?.Language?.Code?.ToLower() ?? string.Empty;
                    textEditor.TextArea.IndentationStrategy = braceStyleLanguages.Contains(langCode)
                        ? csharpIndentationStrategy
                        : defaultIndentationStrategy;
                }

            }
        }

        #region IFlyoutService Implementation

        public bool IsFlyoutOpen(string tag)
        {
            return flyControl.Items.OfType<Flyout>().Any(f => f.Tag is string flyoutTag && flyoutTag == tag);
        }

        public void ShowFlyout(string tag, object viewModel, string header, Action? onClosed = null)
        {
            var accentColor = TryFindResource("MahApps.Brushes.AccentBase") as SolidColorBrush;
            var flyout = new Flyout
            {
                Tag = tag,
                Header = header,
                Content = viewModel,
                IsOpen = true
            };
            switch (tag)
            {
                case "flyHighlightingEditor":
                case "flyCodeRunner":
                    flyout.Position = Position.Right;
                    flyout.Width = 600;
                    flyout.IsPinned = true;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    flyout.AnimateOpacity = true;
                    HeaderedControlHelper.SetHeaderMargin(flyout, new Thickness(5, 5, 5, 5));
                    flyout.BorderBrush = accentColor ?? Brushes.Gray;
                    flyout.BorderThickness = new Thickness(.5, 0, 0, 0);
                    break;
                case "flySnippet":
                case "flyEditLangCat":
                    flyout.Position = Position.Left;
                    flyout.Width = 400;
                    flyout.IsPinned = false;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    flyout.AnimateOpacity = true;
                    HeaderedControlHelper.SetHeaderMargin(flyout, new Thickness(5, 5, 5, 5));
                    flyout.BorderBrush = accentColor ?? Brushes.Gray;
                    flyout.BorderThickness = new Thickness(0, 0, .5, 0);
                    break;
                case "flySettings":
                    flyout.Position = Position.Right;
                    flyout.IsPinned = false;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    flyout.BorderBrush = accentColor ?? Brushes.Gray;
                    flyout.BorderThickness = new Thickness(.5, 0, 0, 0);
                    break;
                case "flyCompilerSettings":
                    flyout.Position = Position.Right;
                    flyout.IsPinned = false;
                    flyout.MinWidth = 250;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    flyout.BorderBrush = accentColor ?? Brushes.Gray;
                    flyout.BorderThickness = new Thickness(.5, 0, 0, 0);
                    break;
            }
            void ClosingFinishedHandler(object sender, RoutedEventArgs args)
            {
                flyout.ClosingFinished -= ClosingFinishedHandler;

                onClosed?.Invoke();

                flyControl.Items.Remove(flyout);
            }

            flyout.ClosingFinished += ClosingFinishedHandler;
            flyControl.Items.Add(flyout);
        }

        public void ShowHighlightingEditor()
        {
            if (IsFlyoutOpen("flyHighlightingEditor")) return;

            string? langCode = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (langCode == null) return;

            var vm = new HighlightingEditorViewModel(textEditor.SyntaxHighlighting, textEditor, langCode);

            ShowFlyout("flyHighlightingEditor", vm, "Syntax Highlighting Editor");
        }

        #endregion


        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            if (DataContext is MainViewModel vm)
            {
                vm.OnWindowClosing(e);
            }

        }

        private void TextEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var menu = textEditor.ContextMenu;
            if (menu == null) return;

            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                switch (item.Header)
                {
                    case "Undo":
                        item.IsEnabled = textEditor.Document.UndoStack.CanUndo;
                        break;
                    case "Redo":
                        item.IsEnabled = textEditor.Document.UndoStack.CanRedo;
                        break;
                    case "Cut":
                    case "Copy":
                        item.IsEnabled = !(textEditor.SelectedText == string.Empty);
                        break;
                    case "Paste":
                        item.IsEnabled = Clipboard.ContainsText();
                        break;
                    case "Copy As":
                        item.IsEnabled = !string.IsNullOrEmpty(textEditor.SelectedText);
                        break;
                }
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.Document.UndoStack.CanUndo)
                textEditor.Document.UndoStack.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.Document.UndoStack.CanRedo)
                textEditor.Document.UndoStack.Redo();
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            textEditor.Cut();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            textEditor.Copy();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            textEditor.Paste();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            textEditor.SelectAll();

        }

        private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            switch (e.NewValue)
            {
                case Snippet snippet:
                    // method is called from ViewModel with new selected snippet, if old snippet has been modified asks to save
                    await mainViewModel.ChangeSelectedSnippetAsync(snippet);

                    var langCode = snippet.Category?.Language?.Code ?? string.Empty;
                    HighlightingService.ApplyHighlighting(textEditor, langCode);

                    if (IsFoldingEnabledForCurrentSnippet(langCode))
                    {
                        SetupFolding(snippet);
                    }
                    else
                    {
                        SetupIndentationStrategy(langCode);
                    }

                    textEditor.Document.UndoStack.ClearAll();
                    break;

                case Category category:
                    mainViewModel.SelectedCategory = category;
                    break;

                case Language lang:
                    mainViewModel.SelectedCategory = lang.Categories.FirstOrDefault();
                    break;

                default:
                    break;
            }
        }

        private async void FormatDfmt_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "d")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithDfmtAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }
            }
        }

        private async void FormatClang_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {

                var supported = new[]
                {
            "c", "cpp", "h", "cs", "d", "js", "java", "mjs", "ts",
            "json", "m", "mm", "proto", "protodevel", "td", "txtpb",
            "textpb", "textproto", "asciipb", "sv", "svh", "v", "vh"
            };

                if (supported.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    string originalCode = textEditor.Text;
                    string filename = $"example.{code}";
                    var (isSuccess, formattedClang, errorClang) = await FormattingService.TryFormatCodeWithClangAsync(originalCode, assumeFilename: filename);
                    if (isSuccess)
                    {
                        textEditor.Document.Text = formattedClang;
                    }
                    else
                    {
                        MessageBox.Show(errorClang);
                    }
                }
            }
        }

        private async void FormatCSharpier_Click(object sender, RoutedEventArgs e)
        {

            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                var originalCode = textEditor.Text;

                (bool isSuccess, string? formatted, string? error) = code switch
                {
                    "cs" => await FormattingService.TryFormatCodeWithCSharpierAsync(originalCode),
                    "xml" => await FormattingService.TryFormatXmlWithCSharpierAsync(originalCode),
                    _ => (false, null, $"Formatting for '{code}' is not supported with CSharpier")
                };

                if (isSuccess)
                {
                    textEditor.Document.Text = formatted!;
                }
                else
                {
                    MessageBox.Show($"Formatting failed:\n {error}");
                }
            }
        }

        private async void FormatBlack_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "py")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPythonModuleAsync(originalCode, "black");
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }

            }
        }

        private async void FormatAutopep8_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "py")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPythonModuleAsync(originalCode, "autopep8");
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted!;
                }
                else
                {
                    MessageBox.Show(error);
                }

            }
        }

        private async void FormatRuff_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "py")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithRuffAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }

            }
        }

        private async void FormatRustfmt_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "rs")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithRustFmtAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }
            }
        }

        private async void FormatGofmt_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "go")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithGofmtAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }
            }
        }

        private async void FormatStylua_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "lua")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithStyluaAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }
            }
        }

        private async void FormatPasfmt_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "pas")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPasFmtAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show(error);
                }
            }
        }

        private async void FormatPrettier_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                var supported = new[]
                        {
                        "js","jsx","ts","tsx","css","scss","html","json","md","mdx","vue","yaml","yml"
                     };
                if (supported.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    string filename = $"example.{code}";
                    string originalCode = textEditor.Text;
                    var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPrettierAsync(originalCode, assumeFilename: filename);
                    if (isSuccess)
                    {
                        textEditor.Document.Text = formatted;
                    }
                    else
                    {
                        MessageBox.Show(error);
                    }
                }
            }
        }

        private async void FormatFantomas_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null and "fs")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithFantomasAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else if (error != null && error.Contains("Could not execute", StringComparison.OrdinalIgnoreCase))
                {
                    error += "\n\nMake sure Fantomas is installed. You can install it via the .NET CLI:\n\n" +
                             "Globally:\n" +
                             "  dotnet tool install -g fantomas-tool\n\n" +
                             "Local:\n" +
                             "Open command prompt in Tools directory" +
                             "  dotnet new tool-manifest\n" +
                             "  dotnet tool install fantomas";
                    MessageBox.Show(error);
                }
                else
                {
                    MessageBox.Show(error);
                }
            }
        }

        private async void FormatAll_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                switch (code.ToLowerInvariant())
                {
                    case "cs":
                        FormatCSharpier_Click(sender, e);
                        break;

                    case "d":
                        FormatDfmt_Click(sender, e);
                        break;

                    case "fs":
                        FormatFantomas_Click(sender, e);
                        break;

                    case "go":
                        FormatGofmt_Click(sender, e);
                        break;

                    case "lua":
                        FormatStylua_Click(sender, e);
                        break;

                    case "pas":
                        FormatPasfmt_Click(sender, e);
                        break;

                    case "py":
                        FormatBlack_Click(sender, e);
                        break;

                    case "rs":
                        FormatRustfmt_Click(sender, e);
                        break;

                    case "xml":
                        FormatCSharpier_Click(sender, e);
                        break;

                    case "html":
                    case "css":
                    case "md":
                        FormatPrettier_Click(sender, e);
                        break;

                    default:
                        // DEFAULT: Use clang-format for other supported languages
                        FormatClang_Click(sender, e);
                        break;
                }
            }
        }

        private void ToggleSingleLineComment_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                try
                {
                    CommentHelper.ToggleCommentByExtension(textEditor, code, useMultiLine: false);
                }
                catch (Exception ex) { Debug.WriteLine(ex.Message); }
            }
        }

        private void ToggleMultiLineLineComment_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                try
                {
                    CommentHelper.ToggleCommentByExtension(textEditor, code, useMultiLine: true);
                }
                catch { }
            }
        }

        private void ToggleCommentSelection_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                try
                {
                    CommentHelper.ToggleInlineCommentByExtension(textEditor, code);
                }
                catch { /* skip error */ }
            }
        }

        private void CopyAsMarkdown_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textEditor.SelectedText))
                return;

            string langCode = mainViewModel.SelectedSnippet?.Category?.Language?.Code ?? "";
            langCode = MapLangCodeToMarkdown(langCode);
            string markdownCode = $"```{langCode}\n{textEditor.SelectedText}\n```";


            try
            {
                Clipboard.SetText(markdownCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void CopyAsHtml_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textEditor.SelectedText))
                return;

            string encodedCode = WebUtility.HtmlEncode(textEditor.SelectedText);
            string htmlCode = $"<pre><code>{encodedCode}</code></pre>";

            try
            {
                Clipboard.SetText(htmlCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void CopyAsHtmlColored_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textEditor.SelectedText))
                return;

            var selection = textEditor.TextArea.Selection;
            if (selection.IsEmpty)
                return;

            var htmlOptions = new ICSharpCode.AvalonEdit.Highlighting.HtmlOptions();
            string fragment = selection.CreateHtmlFragment(htmlOptions);

            var theme = ThemeManager.Current.DetectTheme(Application.Current);
            string backgroundColor = "white";
            string foregroundColor = "black";

            if (theme != null && theme.BaseColorScheme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                backgroundColor = "#252525";
                foregroundColor = "white";
            }

            string wrappedHtml = $"<div style=\"background-color: {backgroundColor}; color: {foregroundColor};\">{fragment}</div>";

            try
            {
                Clipboard.SetText(wrappedHtml);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void CopyAsBBCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textEditor.SelectedText))
                return;
            string langCode = mainViewModel.SelectedSnippet?.Category?.Language?.Code ?? "";
            // BBCode often uses the languageCode (exstension) directly. A mapping function could be added if needed. ?
            //langCode = MapLangCodeToMarkdown(langCode);
            string bbCode = $"[code={langCode}]{textEditor.SelectedText}[/code]";
            try
            {
                Clipboard.SetText(bbCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void CopyAsJsonString_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textEditor.SelectedText))
                return;

            // Serialize the string to get a valid JSON string literal (with quotes and escaped characters)
            string jsonString = JsonSerializer.Serialize(textEditor.SelectedText);

            try
            {
                Clipboard.SetText(jsonString);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void CopyAsBase64String_Click(object sender, RoutedEventArgs e)
        {
            string selectedText = textEditor.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
                return;

            try
            {
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(selectedText);
                string base64String = Convert.ToBase64String(textBytes);
                Clipboard.SetText(base64String);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void CopyAsImage_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.TextArea.Selection is not ICSharpCode.AvalonEdit.Editing.RectangleSelection selection || selection.IsEmpty)
            {
                MessageBox.Show("This action requires a rectangular selection (Alt + Mouse Drag or Alt + Shift + Arrow Keys).", "No Rectangular Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var bitmap = RenderSelectionToBitmap(selection);
                Clipboard.SetImage(bitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy selection as image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Clipboard.SetText(ex.Message);
            }
        }

        private void ExportToPng_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.TextArea.Selection is not ICSharpCode.AvalonEdit.Editing.RectangleSelection selection || selection.IsEmpty)
            {
                MessageBox.Show("This action requires a rectangular selection (Alt + Mouse Drag or Alt + Shift + Arrow Keys).", "No Rectangular Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (mainViewModel.SelectedSnippet == null)
            {
                MessageBox.Show("Please select a snippet first to determine the filename.", "No Snippet Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var bitmap = RenderSelectionToBitmap(selection);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                string exportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
                Directory.CreateDirectory(exportsDir);

                string snippetTitle = mainViewModel.SelectedSnippet.Title;
                string invalidChars = new string(Path.GetInvalidFileNameChars());
                foreach (char c in invalidChars)
                {
                    snippetTitle = snippetTitle.Replace(c.ToString(), "_");
                }
                // Replace one or more whitespace characters with a single underscore
                snippetTitle = Regex.Replace(snippetTitle, @"\s+", "_");
                string filePath = Path.Combine(exportsDir, $"{snippetTitle}.png");

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                MessageBox.Show($"Image successfully saved to:\n{filePath}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export selection as image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToHtml_Click(object sender, RoutedEventArgs e)
        {
            if (mainViewModel != null && mainViewModel.SelectedSnippet != null)
            {
                HtmlExporter.ExportToHtml(mainViewModel.SelectedSnippet.Title, mainViewModel.EditorText);
            }
        }

        private void ExportToFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mainViewModel != null && mainViewModel.SelectedSnippet != null)
                {
                    FileExporter.ExportToFile(mainViewModel.EditorText, mainViewModel.SelectedSnippet.Title, mainViewModel.SelectedSnippet.Category!.Language!.Code);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new Views.AboutView.AboutWindow(this);
            aboutWindow.ShowDialog();
        }

        private void ThemeSwitch_Click(object sender, RoutedEventArgs e)
        {
            mainViewModel.SwitchTheme();

            HighlightingService.ApplyHighlighting(textEditor, mainViewModel.SelectedSnippet?.Category?.Language?.Code);
            ReplaceCurrentLineRenderer(textEditor);
        }

        private bool IsFoldingEnabledForCurrentSnippet(string langCode)
        {
            if (braceStyleLanguages.Contains(langCode))
            {
                return mainViewModel.EnableBraceStyleFolding;
            }
            else if (langCode == "xml")
            {
                return mainViewModel.EnableXmlFolding;
            }
            else if (langCode == "py")
            {
                return mainViewModel.EnablePythonFolding;
            }
            else
            {
                return false;
            }
        }

        private void SetupIndentationStrategy(string langCode)
        {
            if (mainViewModel.DisableIntendation)
            {
                textEditor.TextArea.IndentationStrategy = null;
            }
            else
            {
                textEditor.TextArea.IndentationStrategy = braceStyleLanguages.Contains(langCode)
                    ? csharpIndentationStrategy
                    : defaultIndentationStrategy;
            }
            if (foldingManager != null)
            {
                FoldingManager.Uninstall(foldingManager);
                foldingManager = null;
            }
            textEditor.Options.AllowScrollBelowDocument = false;
        }

        private void SetupFolding(Snippet snippet)
        {            
            string langCode = snippet.Category?.Language?.Code?.ToLower() ?? string.Empty;

            if (braceStyleLanguages.Contains(langCode))
            {
                textEditor.TextArea.IndentationStrategy = mainViewModel.DisableIntendation ? null : csharpIndentationStrategy;
                foldingStrategy = braceFoldingStrategy;
            }
            else
            {
                textEditor.TextArea.IndentationStrategy = mainViewModel.DisableIntendation ? null : defaultIndentationStrategy;
                if (langCode == "xml")
                {
                    foldingStrategy = xmlFoldingStrategy;
                }
                else if (langCode == "py")
                {
                    foldingStrategy = pythonFoldingStrategy;
                }
                else
                {
                    foldingStrategy = null;
                }
            }

            // Install or uninstall the folding manager based on whether a strategy was selected.
            if (foldingStrategy != null)
            {
                foldingManager ??= FoldingManager.Install(textEditor.TextArea);
                try
                {
                    switch (foldingStrategy)
                    {
                        case BraceFoldingStrategy braceStrategy:
                            braceStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                            break;
                        case XmlFoldingStrategy xmlStrategy:
                            xmlStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                            break;
                        case PythonFoldingStrategy pythonStrategy:
                            pythonStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating foldings: {ex.Message}");
                }
            }

            textEditor.Options.AllowScrollBelowDocument = foldingStrategy != null;
        }

        private RenderTargetBitmap RenderSelectionToBitmap(ICSharpCode.AvalonEdit.Editing.RectangleSelection selection)
        {
            // 1. Get the selected text
            string selectedText = selection.GetText();

            // 2. Create a new, off-screen TextEditor
            var virtualEditor = new TextEditor
            {
                // 3. Apply the same properties as the main editor
                FontFamily = textEditor.FontFamily,
                FontSize = textEditor.FontSize,
                Background = textEditor.Background,
                Foreground = textEditor.Foreground,
                SyntaxHighlighting = textEditor.SyntaxHighlighting,
                Text = selectedText,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Options = textEditor.Options
            };

            // 4. Force the layout to be calculated
            var viewbox = new Viewbox { Child = virtualEditor };
            viewbox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            viewbox.Arrange(new Rect(viewbox.DesiredSize));

            // 5. Get the actual dimensions of the content
            double contentWidth = virtualEditor.TextArea.TextView.ActualWidth;
            double contentHeight = virtualEditor.TextArea.TextView.ActualHeight;

            if (contentWidth <= 0 || contentHeight <= 0)
            {
                throw new InvalidOperationException("The selected content has no visible area to capture.");
            }

            // Define padding and footer for the watermark ---
            double padding = 4;
            double footerHeight = 20;
            double totalWidth = contentWidth + (2 * padding);
            double totalHeight = contentHeight + (2 * padding) + footerHeight;

            // Create a DrawingVisual to compose the final image
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // Layer 1: Draw the outer background/border
                // For a dark theme, a slightly lighter background works well.
                // For a light theme, a slightly darker one.
                var editorBg = (textEditor.Background as SolidColorBrush)?.Color ?? Colors.White;
                var borderColor = Color.Add(editorBg, Color.FromRgb(10, 10, 10));
                dc.DrawRectangle(new SolidColorBrush(borderColor), null, new Rect(0, 0, totalWidth, totalHeight));

                // Layer 2: Draw the code itself by rendering the virtual editor
                var codeBrush = new VisualBrush(virtualEditor);
                dc.DrawRectangle(codeBrush, null, new Rect(padding, padding, contentWidth, contentHeight));

                double fontSize = totalWidth > 200 ? 12 : 8;
                // Layer 3: Draw the watermark
                var watermarkText = new FormattedText(
                    "Generated by CodeSnip",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    fontSize,
                    new SolidColorBrush(Color.FromArgb(128, 170, 170, 170)), // Semi-transparent gray
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                // Position it in the bottom right corner
                var watermarkPosition = new Point(totalWidth - padding - watermarkText.Width, totalHeight - padding - watermarkText.Height);
                dc.DrawText(watermarkText, watermarkPosition);
            }

            // 6. Render the composed DrawingVisual to a bitmap
            var dpi = VisualTreeHelper.GetDpi(this);
            var rtb = new RenderTargetBitmap(
                (int)Math.Ceiling(totalWidth * dpi.DpiScaleX),
                (int)Math.Ceiling(totalHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);

            rtb.Render(drawingVisual);
            return rtb;
        }

        private static string MapLangCodeToMarkdown(string code)
        {
            return code.ToLower() switch
            {
                "cs" => "csharp",
                "cpp" => "cpp",
                "js" => "javascript",
                "ts" => "typescript",
                "py" => "python",
                "java" => "java",
                "html" => "html",
                "xml" => "xml",
                "json" => "json",
                "rb" => "ruby",
                "php" => "php",
                "go" => "go",
                "rs" => "rust",
                "swift" => "swift",
                "kt" or "kts" => "kotlin",
                "sh" or "bash" => "bash",
                "ps1" => "powershell",
                "sql" => "sql",
                "d" => "d",
                "vb" => "vbnet",
                "lua" => "lua",
                "md" => "markdown",
                "yml" or "yaml" => "yaml",
                "jsonc" => "jsonc",
                "dockerfile" => "dockerfile",
                "makefile" => "makefile",
                "ini" => "ini",
                "toml" => "toml",
                "h" => "c", // Header files as C
                "m" => "objective-c",
                "mm" => "objective-c++",
                "hs" => "haskell",
                "erl" => "erlang",
                "ex" or "exs" => "elixir",
                "r" => "r",
                "jl" => "julia",
                "scala" => "scala",
                "f" or "for" or "f90" => "fortran",
                "ada" or "adb" => "ada",
                "asm" or "s" => "assembly",
                "v" or "vh" or "sv" or "svh" => "systemverilog",
                "vhdl" => "vhdl",
                "ml" => "ocaml",
                "nim" => "nim",
                "zig" => "zig",
                _ => "", // Default to no language if not recognized
            };
        }

        public void DisableHighlightingAndShowError(string errorMessage)
        {
            // Dispatch to the UI thread.
            Dispatcher.Invoke(() =>
            {
                // Disable syntax highlighting to stop the error loop.
                textEditor.SyntaxHighlighting = null;

                MessageBox.Show(this,
                    "A critical error occurred in the syntax highlighting definition (.xshd file).\n" +
                    "Highlighting has been disabled to prevent the application from crashing.\n\n" +
                    "Please check the .xshd file for rules that might match zero-length text (e.g., regex like '^' or '$' inside a <Span> tag).\n\n" +
                    $"Original error: {errorMessage}",
                    "Syntax Highlighting Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (DataContext is MainViewModel vm)
                {
                    vm.StatusMessage = "Syntax highlighting error.";
                }
            });
        }


    }

}
