using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace CodeSnip.EditorHelpers
{
    public static class CommentHelper
    {
        // Languages with single-line comments
        private static readonly Dictionary<string, string> singleLineComments = new()
        {
    { "as", "//" },      // ActionScript3
    { "aspx", "//" },    // ASP/XHTML
    { "atg", "//" },     // Coco
    { "bat", "REM" },    // BAT
    { "boo", "#" },      // Boo
    { "cpp", "//" },     // C++
    { "cs", "//" },      // C#
    { "d", "//" },       // D
    { "fs", "//" },      // F#
    { "fx", "//" },      // HLSL
    { "ini", ";" },      // INI
    { "java", "//" },    // Java
    { "js", "//" },      // JavaScript
    { "nut", "//" },     // Squirrel
    { "pas", "//" },     // Pascal
    { "php", "//" },     // PHP
    { "plsql", "--" },   // PLSQL
    { "ps1", "#" },      // PowerShell
    { "py", "#" },       // Python
    { "rb", "#" },       // Ruby
    { "rs", "//" },      // Rust
    { "sql", "--" },     // SQL
    { "tex", "%" },      // TeX
    { "vb", "'" },       // VB
    { "vtl","##" },      // Velocity Template Language
    // After this line no embedded syntax definitions
    { "vbs", "'" },      // VBScript
    { "vhdl", "--" },    // VHDL
    { "yaml", "#" },     // YAML
    { "yml", "#" },      // YML
    {"zig", "//" },      // Zig
    {"f90", "!" },       // Fortran90
    {"f95", "!" },       // Fortran95
    {"f03", "!" },       // Fortran03
    {"f08", "!" },       // Fortran08
    {"m","!" },          // MATLAB
    {"mm","//" },        // Objective-C++
    {"m4","dnl" },       // M4
    {"sed","#" },        // Sed
    {"awk","#" },        // Awk
    {"raku","#" },       // Raku (Perl 6)
    {"pl","#" },         // Perl
    {"pm","#" },         // Perl Module
    {"t","#" },          // Perl Test Script
    {"swift","//" },     // Swift
    {"kt","//" },        // Kotlin
    {"kts","//" },       // Kotlin Script
    {"groovy","//" },    // Groovy
    {"rsrc","//" },      // Resource Script
    {"rc","//" },        // Resource Script
    {"nim","#" },        // Nim
    {"dart","//" },      // Dart
    {"jl","#" },         // Julia
    {"r","#" },          // R
    {"v","//" },         // Verilog
    {"sv","//" },        // SystemVerilog
    {"groovy","//" },    // Groovy
    {"lua","--" },       // Lua
    {"coffee","#" },     // CoffeeScript
    {"clj",";" },        // Clojure
    {"cljs",";" },       // ClojureScript
    {"scm",";" },        // Scheme
    {"lisp",";" },       // Lisp
    {"rkt",";" },        // Racket
    {"erl","%" },        // Erlang
    {"hs","--" },        // Haskell
    {"elm","--" },       // Elm
    {"ex","#" },         // Elixir
    {"exs","#" },        // Elixir Script
    {"nim","#" },        // Nim
    {"vba","'" },        // VBA
    {"psm1","#" },       // PowerShell Module
    {"psd1","#" },       // PowerShell Data File
    {"makefile","#" },   // Makefile
    {"mk","#" },         // Makefile alternative extension
    {"dockerfile","#" }, // Dockerfile
    {"tf","#" },         // Terraform
    {"hcl","#" },        // HashiCorp Configuration Language
};

        // Languages with multi-line comments
        private static readonly Dictionary<string, (string Start, string End)> multiLineComments = new()
{
    { "cpp", ("/*", "*/") },      // C++
    { "cs", ("/*", "*/") },       // C#
    { "css", ("/*", "*/") },      // CSS
    { "d", ("/*", "*/") },        // D
    { "fs", ("(*", "*)") },       // F#
    { "fx", ("/*", "*/") },       // HLSL
    { "java", ("/*", "*/") },     // Java
    { "js", ("/*", "*/") },       // JavaScript
    { "pas", ("{", "}") },        // Pascal
    { "php", ("/*", "*/") },      // PHP
    { "py", ("\"\"\"", "\"\"\"") }, // Python
    { "plsql", ("/*", "*/") },    // PLSQL
    { "rs", ("/*", "*/") },       // Rust
    { "sql", ("/*", "*/") },      // SQL
    { "html", ("<!--", "-->") },  // HTML
    { "xml", ("<!--", "-->") },   // XML
    { "md", ("<!--", "-->") },    // Markdown
};

        public static void ToggleCommentByExtension(TextEditor textEditor, string fileExtension, bool useMultiLine = false)
        {
            if (string.IsNullOrEmpty(fileExtension) || textEditor.Document == null)
                return;

            string ext = fileExtension.ToLower();

            if (useMultiLine)
            {
                if (multiLineComments.TryGetValue(ext, out var multi))
                {
                    ToggleMultiLineComment(textEditor, multi.Start, multi.End);
                    return;
                }
            }

            if (singleLineComments.TryGetValue(ext, out string? single) && !string.IsNullOrEmpty(single))
            {
                ToggleSingleLineComment(textEditor, single);

                return;
            }
        }

        private static void ToggleSingleLineComment(TextEditor textEditor, string commentToken)
        {
            var document = textEditor.Document;
            int selectionStart = textEditor.SelectionStart;
            int selectionLength = textEditor.SelectionLength;
            int selectionEnd = selectionStart + selectionLength;

            var startLine = document.GetLineByOffset(selectionStart);
            var endLine = document.GetLineByOffset(selectionEnd);

            // If selection ends exactly at the beginning of a new line, and it's not a zero-length selection,
            // do not include that line in the operation.
            if (selectionLength > 0 && endLine.Offset == selectionEnd && endLine.LineNumber > startLine.LineNumber)
            {
                endLine = endLine.PreviousLine;
            }

            var linesToModify = new List<DocumentLine>();
            for (var line = startLine; line != null && line.LineNumber <= endLine.LineNumber; line = line.NextLine)
            {
                linesToModify.Add(line);
            }

            var nonEmptyLines = linesToModify.Where(l => !string.IsNullOrWhiteSpace(document.GetText(l))).ToList();

            if (nonEmptyLines.Count == 0)
                return; // Nothing to do on empty or whitespace-only selection

            // Check if all non-empty lines are commented to decide on the action
            bool allLinesCommented = nonEmptyLines.All(line => document.GetText(line).TrimStart().StartsWith(commentToken));

            string commentTokenWithSpace = commentToken + " ";

            using (document.RunUpdate())
            {
                if (allLinesCommented)
                {
                    // --- UNCOMMENT ---
                    foreach (var line in nonEmptyLines)
                    {
                        string lineText = document.GetText(line);
                        int indentLength = lineText.Length - lineText.TrimStart().Length;
                        int commentStartOffset = line.Offset + indentLength;

                        if (lineText.TrimStart().StartsWith(commentTokenWithSpace))
                        {
                            document.Remove(commentStartOffset, commentTokenWithSpace.Length);
                        }
                        else // This is safe because allLinesCommented is true, so it must start with commentToken
                        {
                            document.Remove(commentStartOffset, commentToken.Length);
                        }
                    }
                }
                else
                {
                    // --- COMMENT ---
                    foreach (var line in linesToModify)
                    {
                        string lineText = document.GetText(line);
                        // Do not comment empty lines to keep them clean
                        if (string.IsNullOrWhiteSpace(lineText))
                            continue;

                        int indentLength = lineText.Length - lineText.TrimStart().Length;
                        document.Insert(line.Offset + indentLength, commentTokenWithSpace);
                    }
                }
            }
        }

        private static void ToggleMultiLineComment(TextEditor textEditor, string startComment, string endComment)
        {
            var document = textEditor.Document;
            int selectionStart = textEditor.SelectionStart;
            int selectionLength = textEditor.SelectionLength;

            int selectionEnd = selectionStart + selectionLength;

            var startLine = document.GetLineByOffset(selectionStart);
            var endLine = document.GetLineByOffset(selectionEnd);

            // If selection ends exactly at the beginning of a new line, and it's not a zero-length selection,
            // do not include that line in the operation.
            if (selectionLength > 0 && endLine.Offset == selectionEnd && endLine.LineNumber > startLine.LineNumber)
            {
                endLine = endLine.PreviousLine;
            }

            int blockStartOffset = startLine.Offset;
            int blockEndOffset = endLine.Offset + endLine.Length; // End of content on the last line

            // Check if the block is already commented
            bool isCommented = false;
            if (document.TextLength >= blockEndOffset && blockEndOffset - blockStartOffset >= startComment.Length + endComment.Length)
            {
                string startText = document.GetText(blockStartOffset, startComment.Length);
                string endText = document.GetText(blockEndOffset - endComment.Length, endComment.Length);
                if (startText == startComment && endText == endComment)
                {
                    isCommented = true;
                }
            }
            using (document.RunUpdate())
            {
                if (isCommented)
                {
                    // --- UNCOMMENT ---
                    // Remove from the end first to preserve offsets
                    document.Remove(blockEndOffset - endComment.Length, endComment.Length);
                    document.Remove(blockStartOffset, startComment.Length);
                }
                else
                {
                    // --- COMMENT ---
                    // Insert at the end first to preserve offsets
                    document.Insert(blockEndOffset, endComment);
                    document.Insert(blockStartOffset, startComment);
                }
            }
        }

        public static void ToggleInlineCommentByExtension(TextEditor textEditor, string fileExtension)
        {
            if (string.IsNullOrEmpty(fileExtension) || textEditor.Document == null)
                return;

            string ext = fileExtension.ToLower();

            if (multiLineComments.TryGetValue(ext, out var multi))
            {
                ToggleInlineBlockComment(textEditor, multi.Start, multi.End);
            }
            // No fallback to single-line, as this is explicitly for block-commenting a selection.
        }

        private static void ToggleInlineBlockComment(TextEditor textEditor, string startComment, string endComment)
        {
            var document = textEditor.Document;
            int selectionStart = textEditor.SelectionStart;
            int selectionLength = textEditor.SelectionLength;

            if (selectionLength == 0) return;

            string selectedText = document.GetText(selectionStart, selectionLength);
            using (document.RunUpdate())
            {
                if (selectedText.StartsWith(startComment) && selectedText.EndsWith(endComment))
                {
                    int contentLength = selectedText.Length - startComment.Length - endComment.Length;
                    string content = selectedText.Substring(startComment.Length, contentLength);
                    document.Replace(selectionStart, selectionLength, content);
                }
                else
                {
                    string newText = startComment + selectedText + endComment;
                    document.Replace(selectionStart, selectionLength, newText);
                }
            }
        }


    }
}
