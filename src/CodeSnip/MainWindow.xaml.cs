using CodeSnip.EditorHelpers;
using CodeSnip.Services;
using CodeSnip.Services.Exporters;
using CodeSnip.Views.HighlightingEditorView;
using CodeSnip.Views.SnippetView;
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

        public ICommand ToggleSingleLineCommentCommand { get; }
        public ICommand ToggleMultiLineCommentCommand { get; }
        public ICommand ToggleCommentSelectionCommand { get; }

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
            InitializeComponent();
            mainViewModel = new MainViewModel(this);
            DataContext = mainViewModel;
            mainViewModel.InitializeEditor(textEditor);

            // This wrapping is only to support keyboard shortcuts from the menu items that are bound to these commands.
            ToggleSingleLineCommentCommand = new RelayCommand(_ => ToggleSingleLineComment_Click(this, new RoutedEventArgs()));
            ToggleMultiLineCommentCommand = new RelayCommand(_ => ToggleMultiLineLineComment_Click(this, new RoutedEventArgs()));
            ToggleCommentSelectionCommand = new RelayCommand(_ => ToggleCommentSelection_Click(this, new RoutedEventArgs()));
            OpenAboutCommand = new RelayCommand(_ => About_Click(this, new RoutedEventArgs()));

            mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
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
            return flyControl.Items.OfType<Flyout>().Any(f => f.Tag is string flyoutTag && flyoutTag == tag && f.IsOpen);
        }

        public void ShowFlyout(string tag, object viewModel, string header, Action? onClosed = null)
        {
            if (IsFlyoutOpen(tag)) return;

            var flyout = new Flyout
            {
                Tag = tag,
                Header = header,
                Content = viewModel,
                IsOpen = true
            };
            switch (tag)
            {
                case "flyCodeRunner":
                    flyout.Position = Position.Right;
                    flyout.Width = 550;
                    flyout.IsPinned = true;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    HeaderedControlHelper.SetHeaderMargin(flyout, new Thickness(5, 5, 5, 5));
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
                    break;
                case "flySettings":
                    flyout.Position = Position.Right;
                    flyout.IsPinned = false;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    break;
                case "flyCompilerSettings":
                    flyout.Position = Position.Right;
                    flyout.IsPinned = false;
                    flyout.MinWidth = 250;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
                    break;
                case "flyHighlightingEditor":
                    flyout.Position = Position.Right;
                    flyout.Theme = FlyoutTheme.Adapt;
                    flyout.CloseButtonIsCancel = true;
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

            var vm = new HighlightingEditorViewModel(textEditor.SyntaxHighlighting, textEditor);

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
            if (e.NewValue is Snippet snippet)
            {
                // method is called from ViewModel with new selected snippet, if old snippet has been modified asks to save
                await mainViewModel.ChangeSelectedSnippetAsync(snippet);

                HighlightingService.ApplyHighlighting(textEditor, snippet.Category?.Language?.Code);

                SetupFolding(snippet);

                textEditor.Document.UndoStack.ClearAll();
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
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithBlackAsync(originalCode);
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

        private async void FormatAll_Click(object sender, RoutedEventArgs e)
        {
            string? code = mainViewModel.SelectedSnippet?.Category?.Language?.Code;
            if (code is not null)
            {
                string originalCode = textEditor.Text;

                switch (code.ToLowerInvariant())
                {
                    case "d":
                        var (successD, formattedDfmt, errorDfmt) = await FormattingService.TryFormatCodeWithDfmtAsync(originalCode);
                        if (successD)
                        {
                            textEditor.Document.Text = formattedDfmt;
                        }
                        else
                        {
                            MessageBox.Show($"Formatting (dfmt) failed:\n {errorDfmt}");
                        }
                        break;

                    case "cs":
                        var (isSuccess, formattedCs, errorCs) = await FormattingService.TryFormatCodeWithCSharpierAsync(originalCode);
                        if (isSuccess)
                        {
                            textEditor.Document.Text = formattedCs;
                        }
                        else
                        {
                            MessageBox.Show($"Formatting (CSharpier) failed:\n{errorCs}");
                        }
                        break;

                    case "py":
                        var (successPy, formattedBlack, errorBlack) = await FormattingService.TryFormatCodeWithBlackAsync(originalCode);
                        if (successPy)
                        {
                            textEditor.Document.Text = formattedBlack;
                        }
                        else
                        {
                            MessageBox.Show($"Formatting (Black) failed:\n{errorBlack}");
                        }
                        break;

                    case "rs":
                        var (successRs, formattedRust, errorRust) = await FormattingService.TryFormatCodeWithRustFmtAsync(originalCode);
                        if (successRs)
                        {
                            textEditor.Document.Text = formattedRust;
                        }
                        else
                        {
                            MessageBox.Show($"Formatting (rustfmt) failed:\n{errorRust}");
                        }
                        break;

                    case "xml":
                        var (successXml, formattedXml, errorXml) = await FormattingService.TryFormatXmlWithCSharpierAsync(originalCode);
                        if (successXml)
                        {
                            textEditor.Document.Text = formattedXml;
                        }
                        else
                        {
                            MessageBox.Show($"Formatting (CSharpier XML) failed:\n{errorXml}");
                        }

                        break;

                    default:
                        // DEFAULT: Use clang-format for other supported languages
                        var supported = new[]
                        {
                        "c", "cpp", "h", "cs", "d", "js", "java", "mjs", "ts",
                        "json", "m", "mm", "proto", "protodevel", "td", "txtpb",
                        "textpb", "textproto", "asciipb", "sv", "svh", "v", "vh"
                     };

                        if (supported.Contains(code, StringComparer.OrdinalIgnoreCase))
                        {
                            string filename = $"example.{code}";
                            var (successClang, formattedClang, errorClang) = await FormattingService.TryFormatCodeWithClangAsync(originalCode, assumeFilename: filename);
                            if (successClang)
                            {
                                textEditor.Document.Text = formattedClang;
                            }
                            else
                            {
                                MessageBox.Show($"Formatting (clang-format) failed:\n{errorClang}");
                            }
                        }
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

            string wrappedHtml = $"<div>{fragment}</div>";

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

        private void SetupFolding(Snippet snippet)
        {
            textEditor.Document ??= new TextDocument();
            textEditor.Document.Text = snippet.Code ?? string.Empty;

            string langCode = snippet.Category?.Language?.Code?.ToLower() ?? string.Empty;

            if (braceStyleLanguages.Contains(langCode))
            {
                textEditor.TextArea.IndentationStrategy = mainViewModel.DisableIntendation ? null : csharpIndentationStrategy;
                foldingStrategy = mainViewModel.EnableBraceStyleFolding ? braceFoldingStrategy : null;
            }
            else
            {
                textEditor.TextArea.IndentationStrategy = mainViewModel.DisableIntendation ? null : defaultIndentationStrategy;
                if (langCode == "xml")
                {
                    foldingStrategy = mainViewModel.EnableXmlFolding ? xmlFoldingStrategy : null;
                }
                else if (langCode == "py")
                {
                    foldingStrategy = mainViewModel.EnablePythonFolding ? pythonFoldingStrategy : null;
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
            else if (foldingManager != null)
            {
                FoldingManager.Uninstall(foldingManager);
                foldingManager = null;
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


    }

}
