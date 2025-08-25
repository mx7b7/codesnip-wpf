using MahApps.Metro.Controls;
using System.Windows;

namespace CodeSnip.Services
{
    public static class FlyoutService
    {
        public static bool IsFlayoutExist(string flyoutName)
        {
            var window = Application.Current.MainWindow as MetroWindow;
            var flyout = window?.Flyouts.Items.OfType<Flyout>()
                          .FirstOrDefault(f => f.Name == flyoutName);
            if (flyout != null) return true;

            return false;
        }
        public static void OpenFlyout(string flyoutName)
        {
            var window = Application.Current.MainWindow as MetroWindow;
            var flyout = window?.Flyouts.Items.OfType<Flyout>()
                          .FirstOrDefault(f => f.Name == flyoutName);
            if (flyout != null) flyout.IsOpen = true;
        }
        public static void CloseFlyout(string flyoutName)
        {
            var window = Application.Current.MainWindow as MetroWindow;
            var flyout = window?.Flyouts.Items.OfType<Flyout>()
                          .FirstOrDefault(f => f.Name == flyoutName);
            if (flyout != null) flyout.IsOpen = false;
        }
        public static void CloseFlyoutByTag(string flyoutName)
        {
            var window = Application.Current.MainWindow as MetroWindow;
            var flyout = window?.Flyouts.Items.OfType<Flyout>()
                          .FirstOrDefault(f => (string)f.Tag == flyoutName);
            if (flyout != null) flyout.IsOpen = false;
        }

        public static void ShowFlyout(string flyoutName, bool isOpen)
        {
            var window = Application.Current.MainWindow as MetroWindow;
            var flyout = window?.Flyouts.Items.OfType<Flyout>()
                        .FirstOrDefault(f => f.Name == flyoutName);
            if (flyout != null) flyout.IsOpen = isOpen;
        }
    }
}
