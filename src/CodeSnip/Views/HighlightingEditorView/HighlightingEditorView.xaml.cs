using System.Windows;
using System.Windows.Controls;

namespace CodeSnip.Views.HighlightingEditorView
{
    public partial class HighlightingEditorView : UserControl
    {
        public HighlightingEditorView()
        {
            InitializeComponent();
        }

        private void FlyHighlightingEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HighlightingEditorViewModel vm)
                vm.XshdEditor = XshdEditor;
        }
    }
}
