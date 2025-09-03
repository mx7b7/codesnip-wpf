using MahApps.Metro.Controls;
using System.Diagnostics;
using System.Windows.Navigation;

namespace CodeSnip.Views.AboutView
{

    public partial class AboutWindow : MetroWindow
    {
        protected AboutWindow()
        {
            InitializeComponent();
            DataContext = new AboutWindowModel();
        }

        public AboutWindow(MetroWindow parent) : this()
        {
            Owner = parent;
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
