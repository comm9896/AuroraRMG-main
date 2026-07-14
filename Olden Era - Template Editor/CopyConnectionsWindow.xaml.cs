using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Two-step wizard: pick source zone pair → pick target zone pair → copy connections.
    /// </summary>
    public partial class CopyConnectionsWindow : Window
    {
        private readonly List<Zone> _zones;
        private readonly List<Connection> _connections;
        private readonly Action<Connection> _onRoadRecalc;
        private List<Connection> _sourceConns = [];
        private bool _hasRoad;

        /// <summary>True if user completed the copy (connections were added).</summary>
        public bool Applied { get; private set; }

        public CopyConnectionsWindow(List<Zone> zones, List<Connection> connections,
                                     Action<Connection> onRoadRecalc)
        {
            InitializeComponent();
            _zones = zones;
            _connections = connections;
            _onRoadRecalc = onRoadRecalc;

            var names = zones.Select(z => z.Name).Where(n => !string.IsNullOrEmpty(n))
                .Where(n => connections.Any(c => c.From == n || c.To == n))
                .OrderBy(n => n).ToList();
            CmbSource1.ItemsSource = names;
            CmbSource2.ItemsSource = names;
            CmbTarget1.ItemsSource = names;
            CmbTarget2.ItemsSource = names;
        }

        // ── Step 1: Source selection ──────────────────────────────────────

        private void SourceChanged(object sender, SelectionChangedEventArgs e)
        {
            // Filter CmbSource2: only zones connected to selected CmbSource1
            if (CmbSource1.SelectedItem is string a)
            {
                var connected = _connections.Where(c => c.From == a || c.To == a)
                    .Select(c => c.From == a ? c.To : c.From)
                    .Distinct().OrderBy(n => n).ToList();
                string? prev = CmbSource2.SelectedItem as string;
                CmbSource2.ItemsSource = connected;
                if (connected.Contains(prev)) CmbSource2.SelectedItem = prev;
            }

            if (CmbSource1.SelectedItem is not string a2 || CmbSource2.SelectedItem is not string b)
            {
                BtnNext.IsEnabled = false;
                TxtSourceInfo.Text = L("S.CC.ConnRequired");
                return;
            }
            if (a2 == b)
            {
                BtnNext.IsEnabled = false;
                TxtSourceInfo.Text = L("S.CC.SameZone");
                return;
            }

            _sourceConns = _connections.Where(c =>
                (c.From == a2 && c.To == b) || (c.From == b && c.To == a2)).ToList();

            if (_sourceConns.Count == 0)
            {
                BtnNext.IsEnabled = false;
                TxtSourceInfo.Text = string.Format(L("S.CC.NoConns"), a2, b);
                return;
            }

            _hasRoad = _sourceConns.Any(c => c.Road == true);
            var types = _sourceConns.Select(c => c.ConnectionType ?? "default").Distinct();
            BtnNext.IsEnabled = true;
            TxtSourceInfo.Text = $"Найдено связей: {_sourceConns.Count}  |  Типы: {string.Join(", ", types)}" +
                                 (_hasRoad ? "  |  🛤 Road" : "");
        }

        // ── Navigation ───────────────────────────────────────────────────

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Visible;
            BtnBack.Visibility = Visibility.Visible;
            BtnNext.Visibility = Visibility.Collapsed;
            BtnCopy.Visibility = Visibility.Visible;
            TxtStep.Text = "Шаг 2: Выберите зоны-цели";

            // Exclude source zones from targets
            string src1 = CmbSource1.SelectedItem as string ?? "";
            string src2 = CmbSource2.SelectedItem as string ?? "";
            var targetNames = _zones.Select(z => z.Name)
                .Where(n => !string.IsNullOrEmpty(n) && n != src1 && n != src2)
                .OrderBy(n => n).ToList();
            CmbTarget1.ItemsSource = targetNames;
            CmbTarget2.ItemsSource = targetNames;

            TxtTargetInfo.Text = $"Источники: {src1} ↔ {src2}  ({_sourceConns.Count} связей)" +
                                 (_hasRoad ? "  |  🛤 Road будет пересчитан" : "");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Step1Panel.Visibility = Visibility.Visible;
            Step2Panel.Visibility = Visibility.Collapsed;
            BtnBack.Visibility = Visibility.Collapsed;
            BtnNext.Visibility = Visibility.Visible;
            BtnCopy.Visibility = Visibility.Collapsed;
            TxtStep.Text = "Шаг 1: Выберите зоны-источники";
        }

        // ── Step 2: Target selection ──────────────────────────────────────

        private void TargetChanged(object sender, SelectionChangedEventArgs e)
        {
            // Filter CmbTarget2: only zones connected to selected CmbTarget1, excluding source zones
            if (CmbTarget1.SelectedItem is string a)
            {
                string? s1 = CmbSource1.SelectedItem as string;
                string? s2 = CmbSource2.SelectedItem as string;
                var connected = _connections.Where(c => c.From == a || c.To == a)
                    .Select(c => c.From == a ? c.To : c.From)
                    .Where(n => n != s1 && n != s2 && n != a)
                    .Distinct().OrderBy(n => n).ToList();
                string? prev = CmbTarget2.SelectedItem as string;
                CmbTarget2.ItemsSource = connected;
                if (connected.Contains(prev)) CmbTarget2.SelectedItem = prev;
            }

            if (CmbTarget1.SelectedItem is not string tgt1 || CmbTarget2.SelectedItem is not string tgt2)
            {
                BtnCopy.IsEnabled = false;
                TxtPreview.Text = L("S.CC.PickZones");
                return;
            }
            if (tgt1 == tgt2)
            {
                BtnCopy.IsEnabled = false;
                TxtPreview.Text = "Выберите 2 РАЗНЫЕ зоны";
                return;
            }

            // Check if connection already exists
            bool exists = _connections.Any(c =>
                (c.From == tgt1 && c.To == tgt2) || (c.From == tgt2 && c.To == tgt1));

            string src1 = CmbSource1.SelectedItem as string ?? "";
            string src2 = CmbSource2.SelectedItem as string ?? "";

            var preview = new List<string>();
            foreach (var sc in _sourceConns)
            {
                string newFrom = sc.From == src1 ? tgt1 : sc.From == src2 ? tgt2 : sc.From;
                string newTo = sc.To == src1 ? tgt1 : sc.To == src2 ? tgt2 : sc.To;
                string newName = $"{newFrom.ToUpperInvariant()}-{newTo.ToUpperInvariant()}";
                string type = sc.ConnectionType ?? "default";
                string road = sc.Road == true ? " 🛤" : "";
                string guard = sc.GuardValue.HasValue ? $" Guard={sc.GuardValue}" : "";
                preview.Add($"  {newName}  |  {type}{guard}{road}");
            }

            BtnCopy.IsEnabled = !exists;
            TxtPreview.Text = $"Будет создано связей: {_sourceConns.Count}\n" +
                              $"Цель: {tgt1} ↔ {tgt2}\n" +
                              (exists ? L("S.CC.AlreadyExists") + "\n" : "") +
                              string.Join("\n", preview);
        }

        // ── Copy ─────────────────────────────────────────────────────────

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTarget1.SelectedItem is not string tgt1 || CmbTarget2.SelectedItem is not string tgt2)
                return;
            if (tgt1 == tgt2) return;

            string src1 = CmbSource1.SelectedItem as string ?? "";
            string src2 = CmbSource2.SelectedItem as string ?? "";

            // Pre-check: which connections will be created and which already exist
            var toCreate = new List<(Connection source, string newFrom, string newTo)>();
            foreach (var sc in _sourceConns)
            {
                string newFrom = sc.From == src1 ? tgt1 : sc.From == src2 ? tgt2 : sc.From;
                string newTo = sc.To == src1 ? tgt1 : sc.To == src2 ? tgt2 : sc.To;

                // Check against ORIGINAL connections (not ones we're about to add)
                bool dup = _connections.Any(c =>
                    (c.From == newFrom && c.To == newTo) || (c.From == newTo && c.To == newFrom));
                if (!dup)
                    toCreate.Add((sc, newFrom, newTo));
            }

            int copied = 0;
            foreach (var (sc, newFrom, newTo) in toCreate)
            {
                var conn = new Connection
                {
                    Name = $"{newFrom.ToUpperInvariant()}-{newTo.ToUpperInvariant()}",
                    From = newFrom,
                    To = newTo,
                    ConnectionType = sc.ConnectionType,
                    GuardZone = sc.GuardZone,
                    GuardEscape = sc.GuardEscape,
                    SimTurnSquad = sc.SimTurnSquad,
                    GuardValue = sc.GuardValue,
                    GuardWeeklyIncrement = sc.GuardWeeklyIncrement,
                    GuardMatchGroup = sc.GuardMatchGroup,
                    Road = sc.Road,
                    GatePlacement = sc.GatePlacement,
                    GatePlacementArgs = sc.GatePlacementArgs != null ? [.. sc.GatePlacementArgs] : null,
                    GuardRandomization = sc.GuardRandomization,
                    Length = sc.Length,
                    PortalPlacementRulesFrom = sc.PortalPlacementRulesFrom != null
                        ? [.. sc.PortalPlacementRulesFrom] : null,
                    PortalPlacementRulesTo = sc.PortalPlacementRulesTo != null
                        ? [.. sc.PortalPlacementRulesTo] : null,
                };

                _connections.Add(conn);
                copied++;

                // Recalculate roads if needed
                if (conn.Road == true)
                    _onRoadRecalc(conn);
            }

            Applied = true;
            MessageBox.Show(this, $"Скопировано связей: {copied}" +
                            (_hasRoad ? "\n🛤 Дороги пересчитаны" : ""),
                            "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Localisation helper ──
        private static string L(string key, params object[] args)
            => string.Format(Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T(key), args);
    }
}
