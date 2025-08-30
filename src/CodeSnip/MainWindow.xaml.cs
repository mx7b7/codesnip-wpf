using CodeSnip.EditorHelpers;
using CodeSnip.Services;
using CodeSnip.Services.Exporters;
using CodeSnip.Views.HighlightingEditorView;
using CodeSnip.Views.SnippetView;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using MahApps.Metro.Controls;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        public ICommand ToggleInlineBlockCommentCommand { get; }

        public MainWindow()
        {
            InitializeComponent();
            mainViewModel = new MainViewModel(this);
            DataContext = mainViewModel;
            mainViewModel.InitializeEditor(textEditor);

            // This wrapping is only to support keyboard shortcuts from the menu items that are bound to these commands.
            ToggleSingleLineCommentCommand = new RelayCommand(_ => ToggleSingleLineComment_Click(this, new RoutedEventArgs()));
            ToggleMultiLineCommentCommand = new RelayCommand(_ => ToggleMultiLineLineComment_Click(this, new RoutedEventArgs()));
            ToggleInlineBlockCommentCommand = new RelayCommand(_ => ToggleInlineBlockComment_Click(this, new RoutedEventArgs()));

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
                    flyout.Width = 500;
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
                await mainViewModel.TrySaveEditingSnippetAsync(snippet);

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
            if (code is not null and "cs")
            {
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithCSharpierAsync(originalCode);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    MessageBox.Show($"Formatting failed: {error}");
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
                            MessageBox.Show(errorDfmt);
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
                            MessageBox.Show($"Formatting (CSharpier) failed: {errorCs}");
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
                            MessageBox.Show(errorBlack);
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
                            MessageBox.Show(errorRust);
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
                                MessageBox.Show(errorClang);
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

        // No menu item for this yet.
        private void ToggleInlineBlockComment_Click(object sender, RoutedEventArgs e)
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


        private void SetupFolding(Snippet snippet)
        {
            textEditor.Document ??= new TextDocument();
            textEditor.Document.Text = snippet.Code ?? string.Empty;

            string langCode = snippet.Category?.Language?.Code?.ToLower() ?? string.Empty;

            // Determine folding strategy and indentation strategy
            switch (langCode)
            {
                case "cs":     // C#
                case "cpp":    // C++
                case "d":      // Dlang
                case "js":     // JavaScript
                case "java":   // Java
                case "rs":     // Rust
                case "mm":     // Objective-C++
                case "go":     // Go
                case "swift":  // Swift
                case "kt":     // Kotlin
                case "php":    // PHP
                case "zig":    // Zig
                    textEditor.TextArea.IndentationStrategy = csharpIndentationStrategy;
                    foldingStrategy = mainViewModel.EnableBraceStyleFolding ? braceFoldingStrategy : null;
                    break;
                case "xml":
                case "html":
                case "xaml":
                    textEditor.TextArea.IndentationStrategy = defaultIndentationStrategy;
                    foldingStrategy = mainViewModel.EnableXmlFolding ? xmlFoldingStrategy : null;
                    break;
                case "py":
                    textEditor.TextArea.IndentationStrategy = defaultIndentationStrategy;
                    foldingStrategy = mainViewModel.EnablePythonFolding ? pythonFoldingStrategy : null;
                    break;
                default:
                    foldingStrategy = null;
                    break;
            }

            textEditor.Options.AllowScrollBelowDocument = foldingStrategy != null;

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
