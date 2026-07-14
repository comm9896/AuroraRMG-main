using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor
{
    public partial class HotkeySettingsWindow : Window
    {
        private readonly Dictionary<string, string> _hotkeys;
        private string? _recordingAction;
        private readonly List<HotkeyItem> _items;

        public HotkeySettingsWindow(Dictionary<string, string> currentHotkeys)
        {
            InitializeComponent();
            _hotkeys = new Dictionary<string, string>(currentHotkeys);
            _items = new List<HotkeyItem>();

            // All bindable actions with labels and defaults
            var defaults = new (string Action, string Label, string DefaultKey)[]
            {
                ("LoadTemplate",   "📂 Загрузить шаблон",      ""),
                ("AddZone",        "➕ Добавить зону",          ""),
                ("CopyZone",       "📋 Копировать зону",       "Ctrl+C"),
                ("PasteZone",      "📋 Вставить зону",         "Ctrl+V"),
                ("ConnectMode",    "🔗 Режим связи",           "Ctrl+L"),
                ("Delete",         "🗑 Удалить",               "Delete"),
                ("Validate",       "✅ Валидность",            ""),
                ("Save",           "💾 Сохранить",             "Ctrl+S"),
                ("Relayout",       "🔄 Раскладка",             "Ctrl+R"),
                ("ExportPng",      "📸 Экспорт PNG",           "Ctrl+E"),
                ("Mirror",         L("S.EC.Mirror"),               "Ctrl+M"),
                ("CopyConnections",L("S.EC.CopyConnections"),     ""),
                ("ConnManager",    "📋 Менеджер связей",       "Ctrl+Shift+M"),
            };

            foreach (var (action, label, def) in defaults)
            {
                string key = _hotkeys.TryGetValue(action, out var v) ? v : def;
                _items.Add(new HotkeyItem { Action = action, Label = label, Key = key });
            }

            HotkeyList.ItemsSource = _items;
        }

        private void HotkeyField_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement fe) return;
            if (fe.DataContext is not HotkeyItem item) return;

            _recordingAction = item.Action;
            item.Key = "<<<нажмите клавишу>>>";
            HotkeyList.Items.Refresh();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_recordingAction is null) { base.OnKeyDown(e); return; }

            e.Handled = true;

            // Escape = cancel recording
            if (e.Key == Key.Escape)
            {
                var item = _items.FirstOrDefault(i => i.Action == _recordingAction);
                if (item != null)
                {
                    // Restore previous value
                    if (_hotkeys.TryGetValue(_recordingAction, out var prev))
                        item.Key = prev;
                    else
                        item.Key = "";
                }
                _recordingAction = null;
                HotkeyList.Items.Refresh();
                return;
            }

            // Build key string
            var mod = Keyboard.Modifiers;
            string keyStr = "";
            if (mod.HasFlag(ModifierKeys.Control)) keyStr += "Ctrl+";
            if (mod.HasFlag(ModifierKeys.Alt)) keyStr += "Alt+";
            if (mod.HasFlag(ModifierKeys.Shift)) keyStr += "Shift+";

            // Ignore bare modifiers
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            keyStr += e.Key.ToString();

            // Apply
            var target = _items.FirstOrDefault(i => i.Action == _recordingAction);
            if (target != null)
            {
                // Check for conflict
                var conflict = _items.FirstOrDefault(i => i.Action != _recordingAction && i.Key == keyStr);
                if (conflict != null)
                {
                    MessageBox.Show(this, string.Format(L("S.HK.Conflict"), keyStr, conflict.Label),
                        L("S.HK.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    target.Key = _hotkeys.TryGetValue(_recordingAction, out var prev2) ? prev2 : "";
                }
                else
                {
                    target.Key = keyStr;
                    _hotkeys[_recordingAction] = keyStr;
                }
            }

            _recordingAction = null;
            HotkeyList.Items.Refresh();
        }

        private void ClearHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string action) return;
            var item = _items.FirstOrDefault(i => i.Action == action);
            if (item != null) item.Key = "";
            _hotkeys.Remove(action);
            HotkeyList.Items.Refresh();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _hotkeys.Clear();
            foreach (var item in _items) item.Key = "";
            HotkeyList.Items.Refresh();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Save to AppSettings
            var settings = AppSettings.Current;
            settings.Hotkeys = new Dictionary<string, string>();
            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Key))
                    settings.Hotkeys[item.Action] = item.Key;
            }
            settings.Save();
            DialogResult = true;
            Close();
        }

        public Dictionary<string, string> GetHotkeys()
        {
            var result = new Dictionary<string, string>();
            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Key) && item.Key != "<<<нажмите клавишу>>>")
                    result[item.Action] = item.Key;
            }
            return result;
        }
    }

    public class HotkeyItem : INotifyPropertyChanged
    {
        public string Action { get; set; } = "";
        public string Label { get; set; } = "";
        private string _key = "";
        public string Key { get => _key; set { _key = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

namespace Olden_Era___Template_Editor
{
    partial class HotkeySettingsWindow
    {
        // ── Localisation helper ──
        private static string L(string key, params object[] args)
            => string.Format(Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T(key), args);
    }
}
