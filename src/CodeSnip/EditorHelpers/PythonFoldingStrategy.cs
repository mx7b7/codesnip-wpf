using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace CodeSnip.EditorHelpers
{
    internal class PythonFoldingStrategy
    {
        public int SpacesInTab { get; private set; } = 4;

        // Python keywords that can open a block
        private readonly string[] blockKeywords = new[]
        {
            "def ", "class ", "if ", "elif ", "else:",
            "for ", "while ", "try:", "except", "finally:", "with "
        };

        public PythonFoldingStrategy()
        {

        }
        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            int firstErrorOffset;
            IEnumerable<NewFolding> newFoldings = CreateNewFoldings(document, out firstErrorOffset);
            manager.UpdateFoldings(newFoldings, firstErrorOffset);
        }

        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            return CreateNewFoldingsByLine(document);
        }

        private IEnumerable<NewFolding> CreateNewFoldingsByLine(ITextSource document)
        {
            var newFoldings = new List<NewFolding>();
            if (document == null || document.TextLength == 0)
                return newFoldings;

            if (document is not TextDocument textDocument)
                return newFoldings;

            var startOffsets = new Stack<int>();
            var startIndents = new Stack<int>();
            var startLineNumbers = new Stack<int>();

            foreach (DocumentLine line in textDocument.Lines)
            {
                var lineText = document.GetText(line.Offset, line.Length);

                // Skip empty lines and comments, but DO NOT break the fold because of them
                if (string.IsNullOrWhiteSpace(lineText) || lineText.TrimStart().StartsWith("#"))
                    continue;

                // Calculate indentation (tab = SpacesInTab spaces)
                int indent = 0;
                foreach (char ch in lineText)
                {
                    if (ch == ' ') indent++;
                    else if (ch == '\t') indent += SpacesInTab;
                    else break;
                }

                if (startOffsets.Count == 0)
                {
                    // First block in the file
                    startOffsets.Push(line.Offset);
                    startIndents.Push(indent);
                    startLineNumbers.Push(line.LineNumber);
                    continue;
                }

                int prevIndent = startIndents.Peek();

                if (indent > prevIndent)
                {
                    // New nested block
                    startOffsets.Push(line.Offset);
                    startIndents.Push(indent);
                    startLineNumbers.Push(line.LineNumber);
                }
                else if (indent < prevIndent)
                {
                    // Close all blocks until we reach the same indent
                    while (startIndents.Count > 0 && indent < startIndents.Peek())
                    {
                        int startOffset = startOffsets.Pop();
                        startIndents.Pop();
                        int startLine = startLineNumbers.Pop();

                        int endOffset = line.PreviousLine != null ? line.PreviousLine.EndOffset : line.EndOffset;

                        // Add fold only if block has at least 2 lines
                        if (line.LineNumber - startLine >= 1)
                        {
                            newFoldings.Add(new NewFolding(startOffset, endOffset));
                        }
                    }
                }
                // If indent == prevIndent → continue within the same block
            }

            // Close all remaining blocks at the end of the file
            while (startOffsets.Count > 0)
            {
                int startOffset = startOffsets.Pop();
                startIndents.Pop();
                int startLine = startLineNumbers.Pop();

                int endOffset = document.TextLength;

                if (textDocument.LineCount - startLine >= 1)
                {
                    newFoldings.Add(new NewFolding(startOffset, endOffset));
                }
            }

            newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return newFoldings;
        }

    }
}

