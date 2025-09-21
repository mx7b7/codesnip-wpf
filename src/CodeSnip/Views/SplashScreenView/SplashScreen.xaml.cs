using MahApps.Metro.Controls;
using System.Windows.Media.Animation;

namespace CodeSnip.Views.SplashScreenView
{
    public partial class SplashScreen : MetroWindow
    {
        public SplashScreen()
        {
            InitializeComponent();
        }
        private void SplashScreen_ContentRendered(object? sender, EventArgs e)
        {
            var fadeInAnimation = (Storyboard)FindResource("FadeIn");
            fadeInAnimation.Begin(this);
        }

        public async Task CloseWithFadeOut()
        {
            var fadeOutAnimation = (Storyboard)FindResource("FadeOut");
            var tcs = new TaskCompletionSource<bool>();
            fadeOutAnimation.Completed += (s, e) => tcs.SetResult(true);
            fadeOutAnimation.Begin(this);
            await tcs.Task;
            Close();
        }
    }
}
