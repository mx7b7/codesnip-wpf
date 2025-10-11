using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace CodeSnip.EditorHelpers
{
    /// <summary>
    /// Custom background renderer for AvalonEdit that draws a customizable border
    /// around the current caret line instead of the default filled highlight.
    /// </summary>
    /// <remarks>
    /// The renderer respects the editor's <see cref="TextEditor.Options.HighlightCurrentLine"/> option
    /// and only draws the border when it's enabled.
    /// 
    /// Border color and thickness can be configured via the constructor.
    /// This design allows easy adaptation for different themes or dynamic color changes.
    /// </remarks>
    public class CurrentLineHighlighter : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly Brush _highlightBrush;
        private readonly Pen _borderPen;

        public CurrentLineHighlighter(TextEditor editor, Brush? highlightBrush = null, double borderThickness = 1.0)
        {
            _editor = editor;

            _highlightBrush = highlightBrush ?? new SolidColorBrush(Color.FromArgb(120, 100, 100, 130));// visible in light and dark themes
            _highlightBrush.Freeze();

            _borderPen = new Pen(_highlightBrush, borderThickness);
            _borderPen.Freeze();
        }
        public KnownLayer Layer => KnownLayer.Background;


        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor.Document == null)
                return;

            if (!_editor.Options.HighlightCurrentLine)
                return;

            textView.EnsureVisualLines();

            var currentLine = _editor.Document.GetLineByNumber(_editor.TextArea.Caret.Line);

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, currentLine))
            {
                var rectFull = new Rect(
                    new Point(0, rect.Top),
                    new Size(textView.ActualWidth, rect.Height));

                drawingContext.DrawRectangle(null, _borderPen, rectFull);
            }
        }
    }
}
