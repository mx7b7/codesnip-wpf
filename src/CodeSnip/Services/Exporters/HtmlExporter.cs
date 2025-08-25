using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Windows;

namespace CodeSnip.Services.Exporters
{
    public static class HtmlExporter
    {
        
        public static void ExportToHtml(string title = "Snipet", string code = "")
        {
            string html = CreateHtmlPage(title, code);

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "HTML Files (*.html;*.htm)|*.html;*.htm",
                FileName = $"{title}.html",
                DefaultExt = ".html"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                File.WriteAllText(saveFileDialog.FileName, html);
                MessageBox.Show("Snippet exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static string CreateHtmlPage(string title, string code)
        {
            string escapedCode = WebUtility.HtmlEncode(code);
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>{title}</title>
    <style>
        body {{ font-family: Consolas, monospace; background: #f4f4f4; padding: 20px; }}
        pre {{ background: #272822; color: #f8f8f2; padding: 15px; border-radius: 5px; overflow-x: auto; }}
    </style>
    <link rel=""stylesheet"" href=""styles/monokai.min.css"">
	<script src=""highlight.min.js""></script>
	<script>hljs.highlightAll();</script>

</head>
<body>
    <h1>{title}</h1>
    <pre><code class=""language-d"">{escapedCode}</code></pre>
</body>
</html>";
        }
    }
}
