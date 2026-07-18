using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    public partial class ValueOverridePickerWindow : Window
    {
        /// <summary>Lines in "sid=guardValue" or "sid=,value" format to append to the overrides text box.</summary>
        public List<string> ResultLines { get; private set; } = [];

        public enum PickMode { Guard, Value }

        private readonly PickMode _mode;
        private readonly HashSet<ListBoxItem> _checkedItems = [];
        private List<ObjectItem> _filtered = [];

        private static readonly SolidColorBrush CheckedBrush = new(Color.FromRgb(0x5A, 0x4A, 0x28));

        public ValueOverridePickerWindow(IEnumerable<string> alreadyOverridden, PickMode mode = PickMode.Guard)
        {
            InitializeComponent();
            _mode = mode;
            GuardRow.Visibility = mode == PickMode.Guard ? Visibility.Visible : Visibility.Collapsed;
            ValueRow.Visibility  = mode == PickMode.Value ? Visibility.Visible : Visibility.Collapsed;

            var existing = new HashSet<string>(alreadyOverridden);
            _filtered = KnownValues.ObjectSids
                .Where(s => !existing.Contains(s))
                .OrderBy(s => s)
                .Select(s => new ObjectItem(s))
                .ToList();
            LbSids.ItemsSource = _filtered;
            UpdateAddButton();
        }

        private sealed class ObjectItem
        {
            public string Sid { get; }
            public string Display { get; }
            public ObjectItem(string sid) { Sid = sid; Display = ObjectNameResolver.Instance.Resolve(sid); }
            public override string ToString() => Display;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static TextBlock? FindCheckMark(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBlock tb && tb.Name == "ChkMark") return tb;
                var found = FindCheckMark(child);
                if (found != null) return found;
            }
            return null;
        }

        private void ToggleItem(ListBoxItem lbi)
        {
            if (_checkedItems.Contains(lbi))
            {
                _checkedItems.Remove(lbi);
                lbi.ClearValue(ListBoxItem.BackgroundProperty);
                if (FindCheckMark(lbi) is { } chk) chk.Text = "";
            }
            else
            {
                _checkedItems.Add(lbi);
                lbi.Background = CheckedBrush;
                if (FindCheckMark(lbi) is { } chk) chk.Text = "✓";
            }
            UpdateAddButton();
        }

        private static ListBoxItem? FindListBoxItem(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem lbi) return lbi;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        private void UpdateAddButton()
        {
            int n = _checkedItems.Count;
            BtnAdd.Content   = n > 1 ? LocalizationManager.T("S.P.AddSelectedN", n) : LocalizationManager.T("S.P.AddSelected");
            BtnAdd.IsEnabled = n > 0;
        }

        private void RefreshList(string filter)
        {
            _checkedItems.Clear();
            _filtered = KnownValues.ObjectSids
                .Where(s => string.IsNullOrEmpty(filter)
                         || s.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                         || ObjectNameResolver.Instance.Resolve(s).Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .Select(s => new ObjectItem(s))
                .ToList();
            LbSids.ItemsSource = null;
            LbSids.ItemsSource = _filtered;
            UpdateAddButton();
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshList(TxtSearch.Text);

        private void LbSids_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var lbi = FindListBoxItem(e.OriginalSource);
            if (lbi == null) return;
            lbi.ApplyTemplate(); // ensure visual tree is materialised
            ToggleItem(lbi);
            e.Handled = true;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == PickMode.Guard)
            {
                if (!int.TryParse(TxtGuardValue.Text.Trim(), out int gv)) gv = 5000;
                ResultLines = _checkedItems
                    .Select(lbi => lbi.Content is ObjectItem o ? $"{o.Sid}={gv}" : null)
                    .Where(l => l != null).Select(l => l!).ToList();
            }
            else
            {
                if (!int.TryParse(TxtObjectValue.Text.Trim(), out int vv)) vv = 100;
                ResultLines = _checkedItems
                    .Select(lbi => lbi.Content is ObjectItem o ? $"{o.Sid}=,{vv}" : null)
                    .Where(l => l != null).Select(l => l!).ToList();
            }
            if (ResultLines.Count > 0)
                DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
