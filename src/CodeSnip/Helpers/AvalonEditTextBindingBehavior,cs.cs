using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace CodeSnip.Helpers
{
    public class AvalonEditTextBindingBehavior : Behavior<TextEditor>
    {
        public static readonly DependencyProperty BoundTextProperty =
            DependencyProperty.Register(
                nameof(BoundText),
                typeof(string),
                typeof(AvalonEditTextBindingBehavior),
                new FrameworkPropertyMetadata(
                    default(string),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundTextChanged));

        public string? BoundText
        {
            get => (string?)GetValue(BoundTextProperty);
            set => SetValue(BoundTextProperty, value);
        }

        private bool _isUpdating;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.TextChanged += AssociatedObject_TextChanged;
                AssociatedObject.Text = BoundText ?? string.Empty;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            }
            base.OnDetaching();
        }

        private void AssociatedObject_TextChanged(object? sender, EventArgs e)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;
            BoundText = AssociatedObject?.Text;
            _isUpdating = false;
        }

        private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (AvalonEditTextBindingBehavior)d;

            if (behavior._isUpdating)
                return;

            behavior._isUpdating = true;

            if (behavior.AssociatedObject != null)
            {
                behavior.AssociatedObject.Text = e.NewValue as string ?? string.Empty;
            }

            behavior._isUpdating = false;
        }
    }
}
