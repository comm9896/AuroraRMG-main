using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    public partial class ConnectionSettingsWindow : Window
    {
        private readonly Connection _connection;
        private readonly RmgTemplate _template;

        public ConnectionSettingsWindow(Connection connection, RmgTemplate template)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации ConnectionSettingsWindow:\n\n{ex}");
                throw;
            }
            _connection = connection;
            _template = template;

            LoadConnectionData();
        }

        private void LoadConnectionData()
        {
            TxtName.Text = _connection.Name ?? "";
            TxtFrom.Text = _connection.From ?? "?";
            TxtTo.Text = _connection.To ?? "?";
            TxtGuardValue.Text = (_connection.GuardValue ?? 0).ToString();
            ChkRoad.IsChecked = _connection.Road == true;

            CmbType.Items.Add("Default");
            foreach (var t in KnownValues.ConnectionTypes)
                if (!CmbType.Items.Contains(t))
                    CmbType.Items.Add(t);

            var currentType = _connection.ConnectionType ?? "Default";
            if (CmbType.Items.Contains(currentType))
                CmbType.SelectedItem = currentType;
            else
                CmbType.SelectedIndex = 0;

            ChkGuardEscape.IsChecked = _connection.GuardEscape == true;
            ChkSimTurnSquad.IsChecked = _connection.SimTurnSquad == true;

            TxtGuardWeeklyInc.Text = (_connection.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture);

            // Gate placement
            foreach (var gp in KnownValues.GatePlacements)
                CmbGatePlacement.Items.Add(gp);
            if (_connection.GatePlacement != null && CmbGatePlacement.Items.Contains(_connection.GatePlacement))
                CmbGatePlacement.SelectedItem = _connection.GatePlacement;
            else
                CmbGatePlacement.Text = _connection.GatePlacement ?? "";

            TxtGuardRandomization.Text = (_connection.GuardRandomization ?? 0).ToString(CultureInfo.InvariantCulture);

            // Guard zone (Expander section)
            var zones = _template.Variants?.FirstOrDefault()?.Zones;
            if (zones != null)
            {
                foreach (var z in zones)
                    if (!string.IsNullOrEmpty(z.Name))
                        CmbGuardZone.Items.Add(z.Name);
            }
            if (_connection.GuardZone != null && CmbGuardZone.Items.Contains(_connection.GuardZone))
                CmbGuardZone.SelectedItem = _connection.GuardZone;
            else
                CmbGuardZone.Text = _connection.GuardZone ?? "";

            TxtGuardMatchGroup.Text = _connection.GuardMatchGroup ?? "";

            // Gate placement args (multi-select, visible only for NearZone)
            var gateZones = _template.Variants?.FirstOrDefault()?.Zones;
            if (gateZones != null)
            {
                var filtered = gateZones
                    .Select(z => z.Name)
                    .Where(n => !string.IsNullOrEmpty(n) && n != _connection.From && n != _connection.To)
                    .ToArray();
                foreach (var zn in filtered)
                    LstGatePlacementArgs.Items.Add(zn);
            }
            if (_connection.GatePlacementArgs is { Count: > 0 })
            {
                foreach (var item in LstGatePlacementArgs.Items)
                {
                    if (_connection.GatePlacementArgs.Contains(item as string))
                        LstGatePlacementArgs.SelectedItems.Add(item);
                }
            }
            GatePlacementArgsPanel.Visibility = _connection.GatePlacement == "NearZone"
                ? Visibility.Visible : Visibility.Collapsed;
            CmbGatePlacement.SelectionChanged += (_, _) =>
            {
                var sel = CmbGatePlacement.SelectedItem as string ?? CmbGatePlacement.Text;
                GatePlacementArgsPanel.Visibility = sel == "NearZone"
                    ? Visibility.Visible : Visibility.Collapsed;
            };

            // Portal panel visibility
            var isPortal = _connection.ConnectionType == "Portal";
            PortalRulesPanel.Visibility = isPortal ? Visibility.Visible : Visibility.Collapsed;
            CmbType.SelectionChanged += (_, _) =>
            {
                var sel = CmbType.SelectedItem as string;
                PortalRulesPanel.Visibility = sel == "Portal" ? Visibility.Visible : Visibility.Collapsed;
            };

            if (isPortal)
                BuildPortalRulesPanel();
        }

        private void BuildPortalRulesPanel()
        {
            PortalRulesPanel.Children.Clear();

            // From rules
            var fromLabel = new TextBlock
            {
                Text = "Правила порталов (из зоны)",
                Foreground = (System.Windows.Media.Brush)FindResource("FieldLabel") ?? System.Windows.Media.Brushes.Gold,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 3),
            };
            PortalRulesPanel.Children.Add(fromLabel);
            BuildRuleList(_connection.PortalPlacementRulesFrom, list => _connection.PortalPlacementRulesFrom = list);

            // To rules
            var toLabel = new TextBlock
            {
                Text = "Правила порталов (в зону)",
                Foreground = (System.Windows.Media.Brush)FindResource("FieldLabel") ?? System.Windows.Media.Brushes.Gold,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 3),
            };
            PortalRulesPanel.Children.Add(toLabel);
            BuildRuleList(_connection.PortalPlacementRulesTo, list => _connection.PortalPlacementRulesTo = list);
        }

        private void BuildRuleList(List<ContentPlacementRule>? rules, Action<List<ContentPlacementRule>> onChanged)
        {
            var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    var idx = i;
                    var rulePanel = new StackPanel
                    {
                        Margin = new Thickness(4, 4, 4, 4),
                        Background = (System.Windows.Media.Brush)FindResource("BrushInput") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 43, 69)),
                    };

                    var typeCombo = new ComboBox
                    {
                        IsEditable = false,
                        Margin = new Thickness(0, 2, 0, 2),
                        MaxDropDownHeight = 200,
                    };
                    typeCombo.Items.Add("Crossroads");
                    typeCombo.SelectedItem = rule.Type;
                    typeCombo.SelectionChanged += (_, _) =>
                    {
                        if (typeCombo.SelectedItem is string s)
                            rule.Type = s;
                    };
                    rulePanel.Children.Add(new TextBlock { Text = "Тип", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
                    rulePanel.Children.Add(typeCombo);

                    var argsBox = new TextBox
                    {
                        Text = rule.Args is { Count: > 0 } ? string.Join(", ", rule.Args) : "",
                        Margin = new Thickness(0, 2, 0, 2),
                        MinHeight = 24,
                    };
                    argsBox.LostFocus += (_, _) =>
                    {
                        rule.Args = string.IsNullOrWhiteSpace(argsBox.Text)
                            ? null
                            : argsBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    };
                    rulePanel.Children.Add(new TextBlock { Text = "Аргументы", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
                    rulePanel.Children.Add(argsBox);

                    var tminBox = new TextBox
                    {
                        Text = (rule.TargetMin ?? 0).ToString(CultureInfo.InvariantCulture),
                        Margin = new Thickness(0, 2, 0, 2),
                    };
                    tminBox.LostFocus += (_, _) =>
                    {
                        if (double.TryParse(tminBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                            rule.TargetMin = d;
                    };
                    rulePanel.Children.Add(new TextBlock { Text = "Мин. цель", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
                    rulePanel.Children.Add(tminBox);

                    var tmaxBox = new TextBox
                    {
                        Text = (rule.TargetMax ?? 0).ToString(CultureInfo.InvariantCulture),
                        Margin = new Thickness(0, 2, 0, 2),
                    };
                    tmaxBox.LostFocus += (_, _) =>
                    {
                        if (double.TryParse(tmaxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                            rule.TargetMax = d;
                    };
                    rulePanel.Children.Add(new TextBlock { Text = "Макс. цель", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
                    rulePanel.Children.Add(tmaxBox);

                    var wBox = new TextBox
                    {
                        Text = (rule.Weight ?? 1).ToString(CultureInfo.InvariantCulture),
                        Margin = new Thickness(0, 2, 0, 2),
                    };
                    wBox.LostFocus += (_, _) =>
                    {
                        if (double.TryParse(wBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                            rule.Weight = d;
                    };
                    rulePanel.Children.Add(new TextBlock { Text = "Вес", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
                    rulePanel.Children.Add(wBox);

                    var removeBtn = new Button
                    {
                        Content = "Удалить",
                        Margin = new Thickness(0, 4, 0, 0),
                        Padding = new Thickness(8, 2, 8, 2),
                        Cursor = Cursors.Hand,
                    };
                    removeBtn.Click += (_, _) =>
                    {
                        var list = _connection.PortalPlacementRulesFrom ?? new List<ContentPlacementRule>();
                        if (list.Contains(rule)) list = _connection.PortalPlacementRulesFrom!;
                        else if (_connection.PortalPlacementRulesTo?.Contains(rule) == true) list = _connection.PortalPlacementRulesTo!;
                        list.RemoveAt(idx);
                        BuildPortalRulesPanel();
                    };
                    rulePanel.Children.Add(removeBtn);

                    outer.Children.Add(rulePanel);
                }
            }

            var addBtn = new Button
            {
                Content = "Добавить правило",
                Margin = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                Cursor = Cursors.Hand,
            };
            addBtn.Click += (_, _) =>
            {
                var list = rules ?? new List<ContentPlacementRule>();
                list.Add(new ContentPlacementRule { Type = "Crossroads", Args = null, TargetMin = 0, TargetMax = 0, Weight = 1 });
                onChanged(list);
                BuildPortalRulesPanel();
            };
            outer.Children.Add(addBtn);
            PortalRulesPanel.Children.Add(outer);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _connection.Name = TxtName.Text.Trim();

            if (CmbType.SelectedItem is string type)
                _connection.ConnectionType = type;

            if (int.TryParse(TxtGuardValue.Text.Trim(), out var guardVal))
                _connection.GuardValue = guardVal;

            _connection.Road = ChkRoad.IsChecked == true;

            _connection.GuardZone = CmbGuardZone.SelectedItem as string ?? CmbGuardZone.Text.Trim();
            if (string.IsNullOrEmpty(_connection.GuardZone)) _connection.GuardZone = null;

            _connection.GuardEscape = ChkGuardEscape.IsChecked == true;
            _connection.SimTurnSquad = ChkSimTurnSquad.IsChecked == true;

            if (double.TryParse(TxtGuardWeeklyInc.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var gwi))
                _connection.GuardWeeklyIncrement = gwi;

            _connection.GuardMatchGroup = TxtGuardMatchGroup.Text.Trim();
            if (string.IsNullOrEmpty(_connection.GuardMatchGroup)) _connection.GuardMatchGroup = null;

            _connection.GatePlacement = CmbGatePlacement.SelectedItem as string ?? CmbGatePlacement.Text.Trim();
            if (string.IsNullOrEmpty(_connection.GatePlacement)) _connection.GatePlacement = null;

            // Gate placement args
            _connection.GatePlacementArgs = LstGatePlacementArgs.SelectedItems.Count > 0
                ? LstGatePlacementArgs.SelectedItems.Cast<string>().ToList()
                : null;

            if (double.TryParse(TxtGuardRandomization.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var gr))
                _connection.GuardRandomization = gr;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
