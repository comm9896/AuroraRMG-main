using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public static class ComboBoxBehavior
    {
        public static readonly DependencyProperty SyncTextWithSelectedItemProperty =
            DependencyProperty.RegisterAttached(
                "SyncTextWithSelectedItem",
                typeof(bool),
                typeof(ComboBoxBehavior),
                new PropertyMetadata(false, OnSyncChanged));

        public static bool GetSyncTextWithSelectedItem(DependencyObject obj) =>
            (bool)obj.GetValue(SyncTextWithSelectedItemProperty);

        public static void SetSyncTextWithSelectedItem(DependencyObject obj, bool value) =>
            obj.SetValue(SyncTextWithSelectedItemProperty, value);

        private static void OnSyncChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox cb) return;

            if ((bool)e.NewValue)
            {
                cb.SelectionChanged += OnSelectionChanged;
                cb.Loaded += OnLoaded;
            }
            else
            {
                cb.SelectionChanged -= OnSelectionChanged;
                cb.Loaded -= OnLoaded;
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
                cb.Text = cb.SelectedItem?.ToString() ?? "";
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb)
                cb.Text = cb.SelectedItem?.ToString() ?? "";
        }
    }
}
