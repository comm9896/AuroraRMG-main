using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    public partial class ImageImportWindow : Window
    {
        private RmgTemplate? _result;
        private enum Mode { Rmg, H3T, Visual }
        private Mode _mode = Mode.Rmg;

        public RmgTemplate? Result => _result;

        private ImportLayoutAlgorithm _layout = ImportLayoutAlgorithm.Force;

        private static readonly Dictionary<ImportLayoutAlgorithm, string> LayoutKeys = new()
        {
            [ImportLayoutAlgorithm.Force]       = "S.EI.Layout.Force",
            [ImportLayoutAlgorithm.Spectral]    = "S.EI.Layout.Spectral",
            [ImportLayoutAlgorithm.Mds]         = "S.EI.Layout.Mds",
            [ImportLayoutAlgorithm.Hierarchical]= "S.EI.Layout.Hierarchical",
            [ImportLayoutAlgorithm.Centrality] = "S.EI.Layout.Centrality",
            [ImportLayoutAlgorithm.Network]    = "S.EI.Layout.Network",
        };

        public ImageImportWindow() => InitializeComponent();

        private void ImageImportWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Restore the last-used algorithm choice (persisted in AppSettings).
            _layout = ParseLayout(AppSettings.Current.ImportLayout, ImportLayoutAlgorithm.Force);

            // Populate the layout-algorithm selector (default = Force, the historical behaviour).
            CmbLayout.Items.Clear();
            foreach (ImportLayoutAlgorithm algo in Enum.GetValues<ImportLayoutAlgorithm>())
                CmbLayout.Items.Add(L(LayoutKeys[algo]));
            CmbLayout.SelectedItem = L(LayoutKeys[_layout]);
        }

        private static ImportLayoutAlgorithm ParseLayout(string? value, ImportLayoutAlgorithm fallback)
            => Enum.TryParse<ImportLayoutAlgorithm>(value, ignoreCase: true, out var algo)
                && Enum.IsDefined(algo)
                ? algo : fallback;

        private void SaveLayoutChoice()
            => AppSettings.Current.ImportLayout = _layout.ToString();


        private void ModeRmg_Click(object sender, RoutedEventArgs e) => SwitchTo(Mode.Rmg);
        private void ModeH3T_Click(object sender, RoutedEventArgs e) => SwitchTo(Mode.H3T);
        private void ModeVisual_Click(object sender, RoutedEventArgs e) => SwitchTo(Mode.Visual);

        private void SwitchTo(Mode mode)
        {
            _mode = mode;
            bool rmg = mode == Mode.Rmg, h3t = mode == Mode.H3T, vis = mode == Mode.Visual;
            RmgPanel.Visibility = rmg ? Visibility.Visible : Visibility.Collapsed;
            H3TPanel.Visibility = h3t ? Visibility.Visible : Visibility.Collapsed;
            VisualPanel.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            RmgControls.Visibility = rmg ? Visibility.Visible : Visibility.Collapsed;
            H3TControls.Visibility = h3t ? Visibility.Visible : Visibility.Collapsed;
            VisualControls.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            HintText.Text = L("S.EI.Hint");
            ResetState();
        }

        private void ResetState()
        {
            _result = null;
            BtnImport.IsEnabled = false;
            StatusText.Text = L("S.EI.Ready");
            PlaceholderText.Visibility = Visibility.Visible;
            PreviewImage.Source = null;
            ZonesList.Items.Clear();
            RmgFileInfo.Text = L("S.EI.SelectRmg");
            RmgInfoText.Text = "—";
            H3TZonesList.Text = "—";
            H3TConnsList.Text = "—";
        }

        private void BtnSelectRmg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = L("S.EC.LoadTitle"),
                Filter = L("S.EC.LoadFilter"),
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                StatusText.Text = L("S.EI.ReadingRmg");
                BtnImport.IsEnabled = false;
                var json = File.ReadAllText(dlg.FileName);
                var loaded = JsonSerializer.Deserialize<RmgTemplate>(json, TemplateEditorWindow.JsonOptions);
                if (loaded is null) { StatusText.Text = L("S.EI.ParseFail"); return; }

                _result = loaded;
                var fileInfo = new FileInfo(dlg.FileName);
                var zones = loaded.Variants?.FirstOrDefault()?.Zones ?? [];
                var conns = loaded.Variants?.FirstOrDefault()?.Connections ?? [];
                RmgFileInfo.Text = string.Format(L("S.EI.FileInfo"), fileInfo.Name, fileInfo.Length / 1024, loaded.Name);
                RmgInfoText.Text = string.Format(L("S.EI.RmgSummary"), zones.Count, conns.Count) + "\n" +
                    string.Join("\n", zones.Take(40).Select(z => $"• {z.Name}"));
                StatusText.Text = string.Format(L("S.EI.RmgLoaded"), zones.Count, conns.Count);
                BtnImport.IsEnabled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = L("S.EI.Error", ex.Message);
                _result = null;
                RmgFileInfo.Text = L("S.EI.Error", ex.Message);
            }
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|All files|*.*",
                Title = L("S.EI.SelectTitle"),
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(dlg.FileName);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                PreviewImage.Source = bi;
                PlaceholderText.Visibility = Visibility.Collapsed;
                StatusText.Text = L("S.EI.Analyzing");
                BtnImport.IsEnabled = false;
                ZonesList.Items.Clear();

                var template = ImageAnalyzer.Analyze(dlg.FileName,
                    ImageAnalyzer.DetectionMethod.ColourFlood,
                    progress: new Progress<string>(msg => Dispatcher.Invoke(() => StatusText.Text = msg)));

                _result = template;
                var zones = template.Variants is { Count: > 0 }
                    ? template.Variants[0].Zones ?? [] : [];

                foreach (var z in zones)
                    ZonesList.Items.Add($"{z.Name}  |  {z.Layout?.Replace("zone_layout_", "")}  |  GCV={z.GuardedContentValue}");

                StatusText.Text = L("S.EI.Done", zones.Count);
                BtnImport.IsEnabled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = L("S.EI.Error", ex.Message);
                _result = null;
            }
        }

        private void BtnSelectH3T_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "HotA templates|*.h3t|All files|*.*",
                Title = L("S.EI.SelectH3T"),
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                StatusText.Text = L("S.EI.Parsing");
                BtnImport.IsEnabled = false;

                var template = H3TParser.Parse(dlg.FileName);
                _result = template;

                var zones = template.Variants?.FirstOrDefault()?.Zones ?? [];
                var zonesLines = new List<string>();
                foreach (var z in zones)
                {
                    var info = z.Name;
                    if (!string.IsNullOrEmpty(z.Layout))
                        info += $"  |  {z.Layout.Replace("zone_layout_", "")}";
                    if (z.GuardedContentValue.HasValue)
                        info += $"  |  GCV={z.GuardedContentValue}";
                    if (z.MetaData != null)
                    {
                        if (z.MetaData.TryGetValue("cities", out var cities) && !string.IsNullOrEmpty(cities))
                            info += $"\n    Cities: {cities}";
                        if (z.MetaData.TryGetValue("terrain", out var terrain) && !string.IsNullOrEmpty(terrain))
                            info += $"\n    Terrain: {terrain}";
                        if (z.MetaData.TryGetValue("monsters", out var monsters) && !string.IsNullOrEmpty(monsters))
                            info += $"\n    Monsters: {monsters}";
                        if (z.MetaData.TryGetValue("guarded_count", out var gc) && gc != "0")
                            info += $"\n    Guarded: {z.MetaData.GetValueOrDefault("guarded_low", "?")}-{z.MetaData.GetValueOrDefault("guarded_high", "?")} x{gc}";
                        if (z.MetaData.TryGetValue("unguarded_count", out var uc) && uc != "0")
                            info += $"\n    Unguarded: {z.MetaData.GetValueOrDefault("unguarded_low", "?")}-{z.MetaData.GetValueOrDefault("unguarded_high", "?")} x{uc}";
                        if (z.MetaData.TryGetValue("resources_count", out var rc) && rc != "0")
                            info += $"\n    Resources: {z.MetaData.GetValueOrDefault("resources_low", "?")}-{z.MetaData.GetValueOrDefault("resources_high", "?")} x{rc}";
                        if (z.MetaData.TryGetValue("objects_encoded", out var obj) && !string.IsNullOrEmpty(obj))
                            info += $"\n    Objects: {obj.Substring(0, Math.Min(40, obj.Length))}";
                    }
                    zonesLines.Add(info);
                }
                H3TZonesList.Text = string.Join("\n", zonesLines);

                var conns = template.Variants?.FirstOrDefault()?.Connections ?? [];
                var connsLines = new List<string>();
                foreach (var c in conns)
                {
                    var info = $"{c.From} → {c.To}";
                    if (c.GuardValue.HasValue)
                        info += $"  |  Guard={c.GuardValue}";
                    if (c.Road == true)
                        info += "  |  🛤 Road";
                    if (!string.IsNullOrEmpty(c.ConnectionType))
                        info += $"  |  {c.ConnectionType}";
                    connsLines.Add(info);
                }
                H3TConnsList.Text = string.Join("\n", connsLines);

                StatusText.Text = string.Format(L("S.EI.H3TParsed"), zones.Count, conns.Count);
                BtnImport.IsEnabled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = L("S.EI.Error", ex.Message);
                _result = null;
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (_result == null) return;
            // Persist the chosen algorithm so the next import re-opens with it.
            SaveLayoutChoice();
            // Stamp the chosen placement onto the imported template so the editor can
            // reproduce exactly this layout via the existing GeneratorPosition pipeline.
            ApplyLayoutToResult(_result, _layout);
            DialogResult = true;
            Close();
        }

        private void CmbLayout_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbLayout.SelectedItem is not string label) return;
            foreach (var kv in LayoutKeys)
                if (L(kv.Value) == label) { _layout = kv.Key; SaveLayoutChoice(); break; }
        }

        /// <summary>
        /// Computes the chosen algorithm's canvas layout and writes it back as normalized
        /// [0,1]² <see cref="Zone.GeneratorPosition"/> hints on every zone, so the
        /// downstream preview/editor renders the same geometry regardless of topology.
        /// </summary>
        private static void ApplyLayoutToResult(RmgTemplate template, ImportLayoutAlgorithm algorithm)
        {
            var variant = template.Variants?.FirstOrDefault();
            if (variant?.Zones is not { Count: > 0 } zones) return;

            var pixel = TemplatePreviewPngWriter.ComputeLayout(template, MapTopology.Default, algorithm);
            const double w = 700.0, h = 700.0;
            foreach (var z in zones)
                if (pixel.TryGetValue(z.Name, out var p))
                    z.GeneratorPosition = (p.X / w, 1.0 - p.Y / h); // flip Y → unit-square (Y up)
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string L(string key, params object[] args)
            => Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T(key, args);
    }
}
