using System.Diagnostics;
using System.Windows.Navigation;

namespace CodeSnip.Views.CompilerSettingsView
{
    public partial class CompilerSettingsView
    {
        public CompilerSettingsView()
        {
            InitializeComponent();
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
            e.Handled = true;
        }
    }

}
