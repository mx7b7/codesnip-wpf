using CodeSnip.Services;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace CodeSnip.Helpers
{
    public class AvalonEditHighlightingBehavior : Behavior<TextEditor>
    {
        public static readonly DependencyProperty HighlightingNameProperty =
            DependencyProperty.Register(
                nameof(HighlightingName),
                typeof(string),
                typeof(AvalonEditHighlightingBehavior),
                new PropertyMetadata(null, OnHighlightingNameChanged));

        public string HighlightingName
        {
            get => (string)GetValue(HighlightingNameProperty);
            set => SetValue(HighlightingNameProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            ApplyHighlighting();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.SyntaxHighlighting = null; // Clear highlighting on detach
        }

        private static void OnHighlightingNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvalonEditHighlightingBehavior behavior)
            {
                behavior.ApplyHighlighting();
            }
        }

        private void ApplyHighlighting()
        {
            if (AssociatedObject != null && !string.IsNullOrEmpty(HighlightingName))
            {
                HighlightingService.ApplyHighlighting(AssociatedObject, HighlightingName);
            }
            else if (AssociatedObject != null)
            {
                AssociatedObject.SyntaxHighlighting = null;
            }
        }
    }
}
