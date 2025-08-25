using Microsoft.Win32;
using System.IO;

namespace CodeSnip.Services.Exporters
{
    public static class FileExporter
    {
        public static void ExportToFile(string text, string defaultFileName, string? defaultExtension = null)
        {
            if (text is null)
                throw new ArgumentNullException(nameof(text));

            if (string.IsNullOrWhiteSpace(defaultFileName))
                throw new ArgumentException("Ime fajla ne može biti prazno.", nameof(defaultFileName));

            var dlg = new SaveFileDialog
            {
                FileName = defaultFileName,
                DefaultExt = string.IsNullOrWhiteSpace(defaultExtension) ? "" : "." + defaultExtension,
                Filter = string.IsNullOrWhiteSpace(defaultExtension)
                    ? "All files (*.*)|*.*"
                    : $"{defaultExtension.ToUpper()} files (*.{defaultExtension})|*.{defaultExtension}|All files (*.*)|*.*"
            };

            bool? result = dlg.ShowDialog();

            if (result == true && !string.IsNullOrEmpty(dlg.FileName))
            {
                File.WriteAllText(dlg.FileName, text);
            }
        }
    }
}
