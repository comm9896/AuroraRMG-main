using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using static Olden_Era___Template_Editor.ComboBoxBehavior;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using IOPath = System.IO.Path;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Visual zone-graph editor (the "HotA-style" canvas). Renders an <see cref="RmgTemplate"/>'s
    /// zones as draggable nodes and connections as edges, reusing the preview layout so it matches
    /// the generated map. Supports inspect/edit of zone &amp; connection properties, add/remove of
    /// zones and connections, validation, and round-trip save back to <c>.rmg.json</c>.
    /// </summary>
    public partial class TemplateEditorWindow : Window
    {
        // Colours mirror TemplatePreviewPngWriter so the editor speaks the same visual language.
        private static readonly Color SpawnFill    = Color.FromRgb( 42,  90,  50);
        private static readonly Color SpawnBorder  = Color.FromRgb(100, 200, 120);
        private static readonly Color HubFill      = Color.FromRgb( 55,  80,  95);
        private static readonly Color HubBorder    = Color.FromRgb(130, 180, 200);
        private static readonly Color SidesFill    = Color.FromRgb(101,  67,  33);
        private static readonly Color SidesBorder  = Color.FromRgb(205, 127,  50);
        private static readonly Color SideZoneFill   = Color.FromRgb(130,  80,  30);
        private static readonly Color SideZoneBorder = Color.FromRgb(220, 160,  60);
        private static readonly Color TreasureFill    = Color.FromRgb( 72,  76,  80);
        private static readonly Color TreasureBorder  = Color.FromRgb(192, 192, 192);
        private static readonly Color TreasuresFill   = Color.FromRgb( 90,  90, 110);
        private static readonly Color TreasuresBorder = Color.FromRgb(180, 180, 210);
        private static readonly Color SuperTreasureFill   = Color.FromRgb(140, 110,  20);
        private static readonly Color SuperTreasureBorder = Color.FromRgb(255, 220,  80);
        private static readonly Color CenterFill    = Color.FromRgb(120,  90,  20);
        private static readonly Color CenterBorder  = Color.FromRgb(255, 210,  50);
        private static readonly Color CenterZoneFill   = Color.FromRgb(150, 120,  30);
        private static readonly Color CenterZoneBorder = Color.FromRgb(255, 230, 100);
        private static readonly Color StartZoneFill   = Color.FromRgb( 40,  70, 100);
        private static readonly Color StartZoneBorder = Color.FromRgb(100, 160, 220);
        private static readonly Color BackFill    = Color.FromRgb( 60,  50,  90);
        private static readonly Color BackBorder  = Color.FromRgb(140, 120, 200);
        private static readonly Color LeafFill    = Color.FromRgb( 50, 100,  50);
        private static readonly Color LeafBorder  = Color.FromRgb(120, 200, 120);
        private static readonly Color WinCondFill   = Color.FromRgb(160, 100,  20);
        private static readonly Color WinCondBorder = Color.FromRgb(220, 180,  60);
        private static readonly Color AiSpawnFill   = Color.FromRgb( 30,  70,  80);
        private static readonly Color AiSpawnBorder = Color.FromRgb( 80, 180, 200);
        private static readonly Color SecondSpawnFill   = Color.FromRgb( 60,  90,  40);
        private static readonly Color SecondSpawnBorder = Color.FromRgb(140, 210, 100);
        private static readonly Color SideSpawnZoneFill   = Color.FromRgb( 80,  60,  90);
        private static readonly Color SideSpawnZoneBorder = Color.FromRgb(170, 130, 210);
        private static readonly Color SpawnsFill   = Color.FromRgb( 70,  70,  40);
        private static readonly Color SpawnsBorder = Color.FromRgb(190, 190, 100);
        private static readonly Color DirectLine       = Color.FromRgb(180, 145,  60);  // gold solid
        private static readonly Color DefaultLine      = Color.FromRgb(160, 160, 160);  // grey solid
        private static readonly Color PortalLine       = Color.FromArgb(200, 90, 170, 210); // blue dashed
        private static readonly Color ProximityLine    = Color.FromRgb(80, 200, 120);   // green dotted
        private static readonly Color GladiatorLine    = Color.FromRgb(220,  60,  60);   // red dash-dot
        private static readonly Color RoadLine         = Color.FromRgb(150, 120,  90);   // dirt road
        private static readonly Color SelectColor = Color.FromRgb(179, 169, 255); // accent violet
        private static readonly Color GridLine     = Color.FromRgb( 30,  36,  51);

        // Literal UTF-8 (no \uXXXX), UTF-8 no-BOM. See Services.JsonExport.
        private static readonly JsonSerializerOptions JsonOptions = Olden_Era___Template_Editor.Services.JsonExport.Options;

        private RmgTemplate _template;
        public RmgTemplate CurrentTemplate => _template;
        private MapTopology _topology;
        private string? _currentPath;
        private bool _dirty;

        private readonly Dictionary<string, Point>   _positions  = new(StringComparer.Ordinal);
        private readonly Dictionary<string, System.Windows.Shapes.Shape> _nodeShapes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FrameworkElement> _nodeLabels = new(StringComparer.Ordinal);
        private readonly List<(Line Line, Connection Conn)> _edges = [];
        private readonly Dictionary<Connection, double> _edgeFanOffsets = [];
        private double _radius = 24;

        // Interaction state
        private bool   _isPanning;
        private Point  _panStartScreen;
        private double _panStartTx, _panStartTy;
        private Zone?  _dragZone;
        private Vector _dragGrab;
        private bool   _movedWhileDragging;
        private bool   _connectMode;
        private Zone?  _connectFrom;
        private bool   _gridSnap = true; // Grid snap enabled by default
        private const double GridSize = 50.0; // Grid cell size in pixels
        private object? _selected; // Zone or Connection
        private Window? _connectionManagerWindow; // Connection manager window reference
        private Zone? _copiedZone; // Zone data for copy/paste

        public TemplateEditorWindow(RmgTemplate? template = null, MapTopology topology = MapTopology.Default)
        {
            InitializeComponent();
            _template = template ?? NewEmptyTemplate();
            _topology = topology;
            Loaded += (_, _) =>
            {
                ComputePositions();
                RebuildGraph();
                FitToView();
                UpdateTitle();
                UpdateStatus(L("S.EC.Status0", Zones.Count, Connections.Count));
            };
        }

        // ── Model accessors ────────────────────────────────────────────────────────

        private Variant Variant
        {
            get
            {
                _template.Variants ??= [];
                if (_template.Variants.Count == 0) _template.Variants.Add(new Variant());
                return _template.Variants[0];
            }
        }

        private List<Zone> Zones => Variant.Zones ??= [];
        private List<Connection> Connections => Variant.Connections ??= [];

        private static RmgTemplate NewEmptyTemplate() => new()
        {
            Name = L("S.EC.NewTemplate"),
            Variants =
            [
                new Variant
                {
                    Orientation = new OldenEraTemplateEditor.Models.Orientation { Mode = "MinimalBoundingSquare" },
                    Border = new OldenEraTemplateEditor.Models.Border
                    {
                        CornerRadius = 0.0, ObstaclesWidth = 3,
                    },
                    Zones = [], Connections = [],
                }
            ],
        };

        // ── Layout ──────────────────────────────────────────────────────────────────

        private void ComputePositions()
        {
            _positions.Clear();
            try
            {
                var layout = TemplatePreviewPngWriter.ComputeLayout(_template, _topology);
                foreach (var kv in layout) _positions[kv.Key] = kv.Value;
                _radius = Math.Max(16, TemplatePreviewPngWriter.GetLastZoneRadius());
            }
            catch { /* fall through to placement */ }
            PlaceMissingZones();
            SnapAllPositionsToGrid();
        }

        /// <summary>Snaps all zone positions to the nearest <see cref="GridSize"/> cell so
        /// the initial layout matches the grid that dragging uses.</summary>
        private void SnapAllPositionsToGrid()
        {
            if (!_gridSnap) return;
            var keys = _positions.Keys.ToList();
            foreach (var key in keys)
            {
                var p = _positions[key];
                _positions[key] = new Point(
                    Math.Round(p.X / GridSize) * GridSize,
                    Math.Round(p.Y / GridSize) * GridSize);
            }
        }

        /// <summary>Places any zone lacking a computed position on a tidy grid near the centre.</summary>
        private void PlaceMissingZones()
        {
            int slot = 0;
            foreach (var z in Zones)
            {
                if (_positions.ContainsKey(z.Name)) continue;
                int col = slot % 5, row = slot / 5;
                _positions[z.Name] = new Point(140 + col * 110, 140 + row * 110);
                slot++;
            }
        }

        internal void RebuildGraph()
        {
            SyncConnectionPlacementArgs();

            // Strip managed roads (MO → Connection for Connection-placed MOs) — they are
            // regenerated by RebuildConnectionRoads and must not influence road flags via snapshot.
            foreach (var zone in Zones)
            {
                if (zone.MainObjects == null || zone.Roads == null) continue;
                var managedConns = new HashSet<string>();
                for (int i = 0; i < zone.MainObjects.Count; i++)
                {
                    var mo = zone.MainObjects[i];
                    if (mo.Placement == "Connection" && mo.PlacementArgs is { Count: > 0 })
                        managedConns.Add(mo.PlacementArgs[0]);
                }
                if (managedConns.Count > 0)
                {
                    // Also collect stale connection names from existing MO → Connection roads
                    // (they won't be in placementArgs after SyncConnectionPlacementArgs reassigned them)
                    foreach (var r in zone.Roads)
                    {
                        if (r.From?.Type == "MainObject" && r.To?.Type == "Connection" && r.To.Args is { Count: > 0 })
                            managedConns.Add(r.To.Args[0]);
                        if (r.From?.Type == "Connection" && r.From.Args is { Count: > 0 } && r.To?.Type == "MainObject")
                            managedConns.Add(r.From.Args[0]);
                    }
                }
                if (managedConns.Count == 0) continue;
                zone.Roads.RemoveAll(r =>
                {
                    bool fromM = r.From?.Type == "MainObject" && r.To?.Type == "Connection" && r.To.Args is { Count: > 0 };
                    bool fromC = r.From?.Type == "Connection" && r.From.Args is { Count: > 0 } && r.To?.Type == "MainObject";
                    if (fromM) return managedConns.Contains(r.To.Args[0]);
                    if (fromC) return managedConns.Contains(r.From.Args[0]);
                    return false;
                });
            }

            // Snapshot pre-auto-generation roads so SyncConnectionRoadFlags only sees original roads
            var preRoads = Zones.ToDictionary(z => z.Name, z => z.Roads?.ToList());
            RebuildConnectionRoads();
            SyncConnectionRoadFlags(preRoads);
            // Part D (disabled): automatic road generation on template load
            // foreach (var conn in Connections)
            //     if (conn.Road == true)
            //         AutoGenerateRoadsForConnection(conn);
            // Part D2 (disabled): stale cleanup after Part D
            // foreach (var zone in Zones)
            // {
            //     if (zone.Roads == null || zone.MainObjects == null) continue;
            //     var managedTargets = zone.MainObjects
            //         .Where(m => m.Placement == "Connection" && m.PlacementArgs is { Count: > 0 })
            //         .Select(m => m.PlacementArgs[0])
            //         .ToHashSet(StringComparer.OrdinalIgnoreCase);
            //     if (managedTargets.Count == 0) continue;
            //     zone.Roads.RemoveAll(r =>
            //     {
            //         bool fromM0 = r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == "0"
            //                    && r.To?.Type == "Connection" && r.To.Args is { Count: > 0 };
            //         bool toM0 = r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == "0"
            //                  && r.From?.Type == "Connection" && r.From.Args is { Count: > 0 };
            //         if (fromM0) return !managedTargets.Contains(r.To.Args[0]);
            //         if (toM0) return !managedTargets.Contains(r.From.Args[0]);
            //         return false;
            //     });
            // }
            GraphCanvas.Children.Clear();
            _nodeShapes.Clear();
            _nodeLabels.Clear();
            _edges.Clear();
            _edgeFanOffsets.Clear();

            DrawGrid();

            // Group connections by zone pair (order-independent) so we can fan them out.
            var groups = new Dictionary<(string A, string B), List<Connection>>();
            foreach (var c in Connections)
            {
                string a = string.CompareOrdinal(c.From, c.To) < 0 ? c.From : c.To;
                string b = string.CompareOrdinal(c.From, c.To) < 0 ? c.To : c.From;
                var key = (a, b);
                if (!groups.ContainsKey(key)) groups[key] = new List<Connection>();
                groups[key].Add(c);
            }

            // Draw edges first, with per-group fan-out from zone boundary.
            const double fanSpread = 18.0; // pixels between parallel lines
            foreach (var kv in groups)
            {
                var conns = kv.Value.OrderBy(c => c.Name ?? "").ToList();
                if (conns.Count == 0) continue;

                var first = conns[0];
                if (!_positions.TryGetValue(first.From, out var fromPos) ||
                    !_positions.TryGetValue(first.To,   out var toPos)) continue;

                double dx = toPos.X - fromPos.X, dy = toPos.Y - fromPos.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1) continue;

                double ux = dx / len, uy = dy / len;
                double px = -uy, py = ux;

                var zFrom = Zones.FirstOrDefault(z => z.Name == first.From);
                var zTo   = Zones.FirstOrDefault(z => z.Name == first.To);
                double rFrom = zFrom is not null ? NodeRadius(zFrom) : 20;
                double rTo   = zTo   is not null ? NodeRadius(zTo)   : 20;
                double maxPerp = Math.Min(rFrom, rTo) * 0.85;

                int n = conns.Count;
                for (int i = 0; i < n; i++)
                {
                    double offset = Math.Clamp(
                        (i - (n - 1) / 2.0) * fanSpread,
                        -maxPerp, maxPerp);

                    _edgeFanOffsets[conns[i]] = offset;

                    double x1 = fromPos.X + px * offset;
                    double y1 = fromPos.Y + py * offset;
                    double x2 = toPos.X   + px * offset;
                    double y2 = toPos.Y   + py * offset;

                    var line = new Line
                    {
                        X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                        Tag = conns[i],
                        Cursor = Cursors.Hand,
                    };
                    StyleEdge(line, conns[i]);
                    GraphCanvas.Children.Add(line);
                    _edges.Add((line, conns[i]));
                }
            }

            foreach (var z in Zones)
                if (_positions.TryGetValue(z.Name, out var p))
                    DrawNode(z, p);

            UpdateSelectionVisuals();
        }

        /// <summary>Draws a subtle 50px reference grid that pans/zooms with the graph.</summary>
        private void DrawGrid()
        {
            var cell = new GeometryDrawing
            {
                Geometry = new RectangleGeometry(new Rect(0, 0, 50, 50)),
                Pen = new Pen(new SolidColorBrush(GridLine), 1),
            };
            var brush = new DrawingBrush
            {
                Drawing = cell, TileMode = TileMode.Tile, Stretch = Stretch.None,
                Viewport = new Rect(0, 0, 50, 50), ViewportUnits = BrushMappingMode.Absolute,
            };
            brush.Freeze();
            GraphCanvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = GraphCanvas.Width, Height = GraphCanvas.Height, Fill = brush, IsHitTestVisible = false,
            });
        }

        /// <summary>Applies colour/dash/thickness to a connection line based on its type.</summary>
        private static void StyleEdge(Line line, Connection c)
        {
            bool road = c.Road == true;
            if (road)
            {
                line.Stroke = new SolidColorBrush(RoadLine);
                line.StrokeThickness = 3.0;
                line.StrokeDashArray = new DoubleCollection { 6, 4 };
                return;
            }

            string t = c.ConnectionType ?? "Default";
            Color color;
            double thickness;
            DoubleCollection? dash;

            if (string.Equals(t, "portal", StringComparison.OrdinalIgnoreCase))
            {
                color = PortalLine;
                thickness = 2.0;
                dash = new DoubleCollection { 8, 4 };
            }
            else if (string.Equals(t, "proximity", StringComparison.OrdinalIgnoreCase))
            {
                color = ProximityLine;
                thickness = 2.5;
                dash = new DoubleCollection { 2, 3 };
            }
            else if (string.Equals(t, "gladiatorarena", StringComparison.OrdinalIgnoreCase))
            {
                color = GladiatorLine;
                thickness = 2.5;
                dash = new DoubleCollection { 6, 2, 2, 2 };
            }
            else if (string.Equals(t, "direct", StringComparison.OrdinalIgnoreCase))
            {
                color = DirectLine;
                thickness = 3.0;
                dash = null;
            }
            else // Default
            {
                color = DefaultLine;
                thickness = 2.5;
                dash = null;
            }

            line.Stroke = new SolidColorBrush(color);
            line.StrokeThickness = thickness;
            line.StrokeDashArray = dash;
        }

        private void DrawNode(Zone z, Point p)
        {
            var (fill, border) = ClassifyZone(z);
            double r = NodeRadius(z);

            var avgVal = ((z.GuardedContentValue ?? 0) + (z.GuardedContentValuePerArea ?? 0)
                        + (z.UnguardedContentValue ?? 0) + (z.UnguardedContentValuePerArea ?? 0)
                        + (z.ResourcesValue ?? 0) + (z.ResourcesValuePerArea ?? 0)) / 6.0;
            int castles = z.MainObjects?.Count(o => o.Type == "City" || o.Type == "AbandonedOutpost") ?? 0;

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(border),
                StrokeThickness = 2.5,
                Tag = z,
                Cursor = Cursors.SizeAll,
                ToolTip = ZoneTooltip(z),
            };
            Canvas.SetLeft(rect, p.X - r);
            Canvas.SetTop(rect, p.Y - r);
            GraphCanvas.Children.Add(rect);
            _nodeShapes[z.Name] = rect;

            var innerPanel = new StackPanel { IsHitTestVisible = false };

            innerPanel.Children.Add(new TextBlock
            {
                Text = z.Name,
                Foreground = Brushes.White,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
            });

            var yellow = new SolidColorBrush(Color.FromRgb(255, 230, 80));

            if (avgVal > 0)
            {
                innerPanel.Children.Add(new TextBlock
                {
                    Text = $"⌀{avgVal:0}",
                    Foreground = yellow,
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                });
            }

            innerPanel.Children.Add(new TextBlock
            {
                Text = $"🏰{castles}",
                Foreground = Brushes.White,
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
            });

            innerPanel.Measure(new Size(r * 2, r * 2));
            var iw = innerPanel.DesiredSize.Width;
            var ih = innerPanel.DesiredSize.Height;
            if (iw > r * 2) iw = r * 2;
            if (ih > r * 2) ih = r * 2;
            Canvas.SetLeft(innerPanel, p.X - iw / 2);
            Canvas.SetTop(innerPanel, p.Y - ih / 2);
            GraphCanvas.Children.Add(innerPanel);
            _nodeLabels[z.Name] = innerPanel;
        }

        /// <summary>Centres a node label inside the node circle.</summary>
        private static void PlaceInnerLabel(FrameworkElement label, Point p)
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, p.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, p.Y - label.DesiredSize.Height / 2);
        }

        private double NodeRadius(Zone z)
        {
            double scale = Math.Sqrt(Math.Max(0.4, z.Size ?? 1.0));
            return Math.Clamp(_radius * scale, 14, 48);
        }

        private static (Color Fill, Color Border) ClassifyZone(Zone z)
        {
            string n = z.Name ?? "";
            if (n.StartsWith("Spawn-", StringComparison.OrdinalIgnoreCase)) return (SpawnFill, SpawnBorder);
            if (n.Contains("Hub", StringComparison.OrdinalIgnoreCase))      return (HubFill, HubBorder);
            return z.Layout switch
            {
                "zone_layout_player_spawn"      => (SpawnFill, SpawnBorder),
                "zone_layout_ai_spawn"          => (AiSpawnFill, AiSpawnBorder),
                "zone_layout_spawn"             => (SpawnFill, SpawnBorder),
                "zone_layout_spawns"            => (SpawnsFill, SpawnsBorder),
                "zone_layout_second_spawn"      => (SecondSpawnFill, SecondSpawnBorder),
                "zone_layout_side_spawn_zone"   => (SideSpawnZoneFill, SideSpawnZoneBorder),
                "zone_layout_sides"             => (SidesFill, SidesBorder),
                "zone_layout_side_zone"         => (SideZoneFill, SideZoneBorder),
                "zone_layout_treasure"          => (TreasureFill, TreasureBorder),
                "zone_layout_treasure_zone"     => (TreasureFill, TreasureBorder),
                "zone_layout_treasures"         => (TreasuresFill, TreasuresBorder),
                "zone_layout_supertreasure_zone"=> (SuperTreasureFill, SuperTreasureBorder),
                "zone_layout_center"            => (CenterFill, CenterBorder),
                "zone_layout_center_zone"       => (CenterZoneFill, CenterZoneBorder),
                "zone_layout_start_zone"        => (StartZoneFill, StartZoneBorder),
                "zone_layout_back"              => (BackFill, BackBorder),
                "zone_layout_leaf"              => (LeafFill, LeafBorder),
                "zone_layout_wincondition_zone" => (WinCondFill, WinCondBorder),
                _                               => (SidesFill, SidesBorder),
            };
        }

        private static string ZoneTooltip(Zone z)
        {
            var parts = new List<string> { z.Name };
            if (!string.IsNullOrEmpty(z.Layout)) parts.Add($"layout: {z.Layout}");
            if (z.Size is { } s) parts.Add($"size: {s:0.##}");
            return string.Join("\n", parts);
        }

        // ── Selection & inspector (Phase B) ──────────────────────────────────────────

        private void Select(object? item)
        {
            _selected = item;
            UpdateSelectionVisuals();
            BuildInspector();
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var (line, conn) in _edges)
            {
                if (ReferenceEquals(_selected, conn))
                {
                    line.Stroke = new SolidColorBrush(SelectColor);
                    line.StrokeThickness = 4.5;
                    line.StrokeDashArray = null;
                }
                else StyleEdge(line, conn);
            }
            foreach (var (name, shape) in _nodeShapes)
            {
                bool sel = _selected is Zone z && string.Equals(z.Name, name, StringComparison.Ordinal);
                bool isConnectFrom = _connectFrom is Zone cf && string.Equals(cf.Name, name, StringComparison.Ordinal);
                if (sel || isConnectFrom)
                {
                    shape.Stroke = new SolidColorBrush(SelectColor);
                    shape.StrokeThickness = 4.0;
                }
                else if (Zones.FirstOrDefault(zz => zz.Name == name) is { } zone)
                {
                    var (_, brd) = ClassifyZone(zone);
                    shape.Stroke = new SolidColorBrush(brd);
                    shape.StrokeThickness = 2.5;
                }
            }
        }

        private Panel AddExpanderSection(string header, Panel parent)
        {
            var contentPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            parent.Children.Add(contentPanel);
            return contentPanel;
        }

        /// <summary>
        /// If the zone has an auto-generated name (Zone-N) or was previously auto-renamed to a player name,
        /// rename it to match the currently selected spawn player.
        /// </summary>
        private void AutoRenameZoneForSpawn(Zone z, MainObject mo)
        {
            if (string.IsNullOrEmpty(mo.Spawn)) return;
            string current = z.Name ?? "";
            // Check if name is auto-generated (Zone-N) or matches any known player name
            bool isAuto = System.Text.RegularExpressions.Regex.IsMatch(current, @"^Zone-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!isAuto)
            {
                // Check if it was previously auto-renamed to a player name
                isAuto = KnownValues.SpawnPlayers.Any(p => string.Equals(p, current, StringComparison.OrdinalIgnoreCase));
            }
            if (isAuto && !string.Equals(current, mo.Spawn, StringComparison.OrdinalIgnoreCase))
            {
                string newName = UniqueZoneName(mo.Spawn);
                RenameZone(z, newName);
            }
        }

        /// <summary>
        /// Generates a unique zone name based on a preferred base name.
        /// </summary>
        private string UniqueZoneName(string preferred)
        {
            if (!Zones.Any(z => string.Equals(z.Name, preferred, StringComparison.OrdinalIgnoreCase)))
                return preferred;
            for (int i = 2; ; i++)
            {
                string n = $"{preferred}-{i}";
                if (!Zones.Any(z => string.Equals(z.Name, n, StringComparison.OrdinalIgnoreCase))) return n;
            }
        }

        /// <summary>Returns the set of player names currently used as Owner or Spawn across all zones.</summary>
        private HashSet<string> GetUsedPlayers(string? excludeZoneName = null)
        {
            var used = new HashSet<string>();
            foreach (var z in Zones)
            {
                if (excludeZoneName != null && string.Equals(z.Name, excludeZoneName, StringComparison.Ordinal))
                    continue;
                if (z.MainObjects != null)
                    foreach (var mo in z.MainObjects)
                    {
                        if (!string.IsNullOrEmpty(mo.Owner))
                            used.Add(mo.Owner);
                        if (!string.IsNullOrEmpty(mo.Spawn))
                            used.Add(mo.Spawn);
                    }
            }
            return used;
        }

        /// <summary>
        /// Handles MainObject type changes - sets default values for the selected type.
        /// </summary>
        private static void OnMainObjectTypeChanged(MainObject mo)
        {
            switch (mo.Type)
            {
                case "City":
                    mo.Faction ??= new TypedSelector { Type = "Random", Args = [] };
                    mo.BuildingsConstructionSid ??= "default_buildings_construction";
                    mo.Owner = null;
                    mo.Spawn = null;
                    break;
                case "AbandonedOutpost":
                    mo.Faction = null;
                    mo.Owner = null;
                    mo.Spawn = null;
                    mo.BuildingsConstructionSid ??= "rich_buildings_construction";
                    mo.GuardChance ??= 1.0;
                    mo.GuardValue ??= 30000;
                    mo.GuardWeeklyIncrement ??= 0.20;
                    mo.Placement ??= "Uniform";
                    break;
                case "Spawn":
                    mo.Spawn ??= "Player1";
                    mo.Faction ??= new TypedSelector { Type = "Random", Args = [] };
                    mo.BuildingsConstructionSid ??= "default_buildings_construction";
                    mo.Owner = null;
                    break;
                case "GladiatorArena":
                    mo.Faction = null;
                    mo.Owner = null;
                    mo.Spawn = null;
                    break;
            }
        }

        internal void BuildInspector()
        {
            InspectorFieldsMain.Children.Clear();
            InspectorFieldsGuard.Children.Clear();
            InspectorFieldsPools.Children.Clear();
            InspectorFieldsContent.Children.Clear();
            InspectorFieldsBiome.Children.Clear();
            InspectorFieldsObjects.Children.Clear();

            if (_selected is Zone z)
            {
                TxtInspectorHint.Text = L("S.EC.Zone");
                var mainPanel = InspectorFieldsMain;
                var mainSection = AddExpanderSection(L("S.EC.Name"), mainPanel);
                AddTextField(L("S.EC.Name"), z.Name, v => { RenameZone(z, v); }, mainSection);
                AddTextField(L("S.EC.Size"), (z.Size ?? 1.0).ToString(CultureInfo.InvariantCulture),
                    v =>
                    {
                        if (string.IsNullOrWhiteSpace(v)) { z.Size = 1.0; MarkDirty(); RefreshNode(z); }
                        else if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.Size = d; MarkDirty(); RefreshNode(z); }
                    }, mainSection);
                AddComboField(L("S.EC.Layout"), KnownValues.ZoneLayouts, z.Layout,
                    v => { z.Layout = v; MarkDirty(); RefreshNode(z); }, mainSection);

                // Main Object section - hidden by default, shown via "Add" button
                var moSection = new StackPanel { Visibility = Visibility.Collapsed };
                mainPanel.Children.Add(moSection);

                // Add Main Object button (hidden when section 4 is visible)
                var addMainObjBtn = new System.Windows.Controls.Button
                {
                    Content = L("S.EC.MoAddObject"),
                    Margin = new Thickness(0, 8, 0, 8),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                addMainObjBtn.Click += (_, _) =>
                {
                    z.MainObjects ??= [];
                    var newMo = new MainObject
                    {
                        Type = "City",
                        GuardChance = 1.0,
                        GuardValue = 5000,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "default_buildings_construction",
                        Faction = new TypedSelector { Type = "Random", Args = [] },
                        Placement = "Uniform"
                    };
                    z.MainObjects.Add(newMo);
                    MarkDirty();
                    RefreshNode(z);
                    BuildInspector();
                };
                mainPanel.Children.Add(addMainObjBtn);

                // Build main object editor if exists
                var mo = z.MainObjects?.FirstOrDefault(o => o.Type is "City" or "AbandonedOutpost" or "Spawn" or "GladiatorArena");
                bool hasMainObject = mo != null;
                if (hasMainObject && mo != null)
                {
                    moSection.Visibility = Visibility.Visible;
                    RebuildMainObjectEditor(z, mo, moSection);
                }
                addMainObjBtn.Visibility = hasMainObject ? Visibility.Collapsed : Visibility.Visible;

                var guardPanel = InspectorFieldsGuard;
                var g1 = AddExpanderSection(L("S.EC.Diplomacy"), guardPanel);
                AddTextField(L("S.EC.Diplomacy"), (z.DiplomacyModifier ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.DiplomacyModifier = d; MarkDirty(); } }, g1);
                var g2 = AddExpanderSection(L("S.EC.GuardMult"), guardPanel);
                AddTextField(L("S.EC.GuardMult"), (z.GuardMultiplier ?? 1.0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardMultiplier = d; MarkDirty(); } }, g2);
                var g3 = AddExpanderSection(L("S.EC.GuardCutoff"), guardPanel);
                AddTextField(L("S.EC.GuardCutoff"), (z.GuardCutoffValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.GuardCutoffValue = i; MarkDirty(); } }, g3);
                var g4 = AddExpanderSection(L("S.EC.GuardRandom"), guardPanel);
                AddTextField(L("S.EC.GuardRandom"), (z.GuardRandomization ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardRandomization = d; MarkDirty(); } }, g4);
                var g5 = AddExpanderSection(L("S.EC.GuardWeeklyInc"), guardPanel);
                AddTextField(L("S.EC.GuardWeeklyInc"), (z.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardWeeklyIncrement = d; MarkDirty(); } }, g5);
                var g6 = AddExpanderSection(L("S.EC.GuardReactDist"), guardPanel);
                AddIntListField(L("S.EC.GuardReactDist"), z.GuardReactionDistribution,
                    v => { z.GuardReactionDistribution = v; MarkDirty(); }, g6);
                var g7 = AddExpanderSection(L("S.EC.EncounterHoles"), guardPanel);
                AddTextField(L("S.EC.AffectedEnc"), (z.EncounterHolesSettings?.AffectedEncounters ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.EncounterHolesSettings ??= new(); z.EncounterHolesSettings.AffectedEncounters = d; MarkDirty(); } }, g7,
                    "Принимает аргументы от 0 до 1.\n\nКомментарий от Mont: Это пустые клетки в точке интереса (там, где стоит охраняемый или неохраняемый объект, здание/артефакт и т.д.). Используется эта функция на шаблонах типа анархии (без правил), где можно \"воровать\" всякие вкусности, не пробивая охрану.");
                AddTextField(L("S.EC.TwoHoleEnc"), (z.EncounterHolesSettings?.TwoHoleEncounters ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.EncounterHolesSettings ??= new(); z.EncounterHolesSettings.TwoHoleEncounters = d; MarkDirty(); } }, g7,
                    "Принимает аргументы от 0 до 1.\n\nКомментарий от Mont: Это пустые клетки в точке интереса (там, где стоит охраняемый или неохраняемый объект, здание/артефакт и т.д.). Используется эта функция на шаблонах типа анархии (без правил), где можно \"воровать\" всякие вкусности, не пробивая охрану.");

                var poolsPanel = InspectorFieldsPools;

                var allSelectedPools = new List<string>();
                if (z.GuardedContentPool != null) allSelectedPools.AddRange(z.GuardedContentPool);
                if (z.UnguardedContentPool != null) allSelectedPools.AddRange(z.UnguardedContentPool);
                if (z.ResourcesContentPool != null) allSelectedPools.AddRange(z.ResourcesContentPool);

                var viewPoolsBtn = new System.Windows.Controls.Button
                {
                    Content = "📋 Просмотр содержимого пулов",
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 11
                };
                viewPoolsBtn.Click += (_, _) =>
                {
                    try
                    {
                        var allPoolNames = GamePoolDataLoader.GetAllPools().Select(p => p.Name).ToList();
                        var viewer = new ContentPoolViewerWindow(allPoolSids: allPoolNames, allSelectedPids: allSelectedPools);
                        viewer.Owner = this;
                        viewer.Show();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(this, $"Ошибка загрузки пулов:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                poolsPanel.Children.Add(viewPoolsBtn);

                var createPoolBtn = new System.Windows.Controls.Button
                {
                    Content = "➕ Создать новый пул",
                    Margin = new Thickness(0, 0, 0, 12),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 11
                };
                createPoolBtn.Click += (_, _) =>
                {
                    var creator = new ContentPoolCreatorWindow();
                    creator.Owner = this;
                    creator.PoolCreated += (poolName, poolLists) =>
                    {
                        try
                        {
                            // Add custom_ prefix for filtering
                            var fullName = poolName.StartsWith("custom_") ? poolName : "custom_" + poolName;

                            var newPool = new GamePool
                            {
                                Name = fullName,
                                Groups = new List<PoolGroup>
                                {
                                    new PoolGroup
                                    {
                                        Weight = 1,
                                        IncludeLists = poolLists
                                    }
                                }
                            };

                            GamePoolDataLoader.AddPool(newPool);

                            System.Windows.MessageBox.Show(this, $"Пул '{fullName}' создан и добавлен в список!", "Пул создан", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(this, $"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    };
                    creator.Show();
                };
                poolsPanel.Children.Add(createPoolBtn);

                var p1 = AddExpanderSection(L("S.EC.GuardedPool"), poolsPanel);
                AddStringListPicker(L("S.EC.GuardedPool"), KnownValues.GuardedContentPoolSids, z.GuardedContentPool, v => { z.GuardedContentPool = v; MarkDirty(); }, p1);
                var p2 = AddExpanderSection(L("S.EC.UnguardedPool"), poolsPanel);
                AddStringListPicker(L("S.EC.UnguardedPool"), KnownValues.UnguardedContentPoolSids, z.UnguardedContentPool, v => { z.UnguardedContentPool = v; MarkDirty(); }, p2);
                var p3 = AddExpanderSection(L("S.EC.ResourcesPool"), poolsPanel);
                AddStringListPicker(L("S.EC.ResourcesPool"), KnownValues.ResourcesContentPoolSids, z.ResourcesContentPool, v => { z.ResourcesContentPool = v; MarkDirty(); }, p3);
                var p4 = AddExpanderSection(L("S.EC.MandatoryContent"), poolsPanel);
                AddStringListPicker(L("S.EC.MandatoryContent"), KnownValues.MandatoryContentNames, z.MandatoryContent, v => { z.MandatoryContent = v; MarkDirty(); }, p4);
                var p5 = AddExpanderSection(L("S.EC.ContentCountLimits"), poolsPanel);
                AddStringListPicker(L("S.EC.ContentCountLimits"), KnownValues.ContentCountLimitNames, z.ContentCountLimits, v => { z.ContentCountLimits = v; MarkDirty(); }, p5);

                var contentPanel = InspectorFieldsContent;
                var c1 = AddExpanderSection(L("S.EC.GuardedVal"), contentPanel);
                AddTextField(L("S.EC.GuardedVal"), (z.GuardedContentValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.GuardedContentValue = i; MarkDirty(); RefreshNode(z); } }, c1);
                var c2 = AddExpanderSection(L("S.EC.GuardedValPerArea"), contentPanel);
                AddTextField(L("S.EC.GuardedValPerArea"), (z.GuardedContentValuePerArea ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.GuardedContentValuePerArea = i; MarkDirty(); RefreshNode(z); } }, c2);
                var c3 = AddExpanderSection(L("S.EC.UnguardedVal"), contentPanel);
                AddTextField(L("S.EC.UnguardedVal"), (z.UnguardedContentValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.UnguardedContentValue = i; MarkDirty(); RefreshNode(z); } }, c3);
                var c4 = AddExpanderSection(L("S.EC.UnguardedValPerArea"), contentPanel);
                AddTextField(L("S.EC.UnguardedValPerArea"), (z.UnguardedContentValuePerArea ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.UnguardedContentValuePerArea = i; MarkDirty(); RefreshNode(z); } }, c4);
                var c5 = AddExpanderSection(L("S.EC.ResourcesVal"), contentPanel);
                AddTextField(L("S.EC.ResourcesVal"), (z.ResourcesValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.ResourcesValue = i; MarkDirty(); RefreshNode(z); } }, c5);
                var c6 = AddExpanderSection(L("S.EC.ResourcesValPerArea"), contentPanel);
                AddTextField(L("S.EC.ResourcesValPerArea"), (z.ResourcesValuePerArea ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.ResourcesValuePerArea = i; MarkDirty(); RefreshNode(z); } }, c6);

                var biomePanel = InspectorFieldsBiome;
                var b1 = AddExpanderSection(L("S.EC.CrossroadsPos"), biomePanel);
                AddTextField(L("S.EC.CrossroadsPos"), (z.CrossroadsPosition ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.CrossroadsPosition = i; MarkDirty(); } }, b1);
                var b2 = AddExpanderSection(L("S.EC.ZoneBiome"), biomePanel);
                AddBiomeSelector(L("S.EC.ZoneBiome"), z.ZoneBiome, v => { z.ZoneBiome = v; MarkDirty(); }, b2);
                var b3 = AddExpanderSection(L("S.EC.ContentBiome"), biomePanel);
                AddBiomeSelector(L("S.EC.ContentBiome"), z.ContentBiome, v => { z.ContentBiome = v; MarkDirty(); }, b3);
                var b4 = AddExpanderSection(L("S.EC.MetaBiome"), biomePanel);
                AddBiomeSelector(L("S.EC.MetaBiome"), z.MetaObjectsBiome, v => { z.MetaObjectsBiome = v; MarkDirty(); }, b4);
                var b5 = AddExpanderSection(L("S.EC.Roads"), biomePanel);
                AddRoadList(L("S.EC.Roads"), z.Roads, v => { z.Roads = v; MarkDirty(); }, b5);

                int conns = Connections.Count(c => c.From == z.Name || c.To == z.Name);
                AddReadOnly(L("S.EC.ConnCount"), conns.ToString(), biomePanel);

                // Section 5: Additional Main Objects (visible when a main object exists)
                var additionalMoSection = new StackPanel { Visibility = hasMainObject ? Visibility.Visible : Visibility.Collapsed };
                mainPanel.Children.Add(additionalMoSection);

                // New-object placement controls (just below the additional objects list)
                var newMoPlacePanel = new StackPanel { Visibility = hasMainObject ? Visibility.Visible : Visibility.Collapsed };
                var newMoPlaceHeader = new TextBlock
                {
                    Text = L("S.EC.MoAdditionalArgs"),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("BrushText"),
                    FontSize = 12,
                    Margin = new Thickness(0, 8, 0, 4)
                };
                newMoPlacePanel.Children.Add(newMoPlaceHeader);
                var newMoPlaceCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var p in KnownValues.MainObjectPlacements) newMoPlaceCombo.Items.Add(p);
                newMoPlaceCombo.SelectedItem = "Uniform";
                newMoPlacePanel.Children.Add(newMoPlaceCombo);
                // PlacementArgs textbox (Uniform/other)
                var newMoPlaceArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Visible };
                AddSectionLabel("PlacementArgs", newMoPlaceArgsPanel);
                var newMoPlaceArgsBox = new TextBox { Text = "" };
                newMoPlaceArgsPanel.Children.Add(newMoPlaceArgsBox);
                // Connection args ComboBox (initially hidden)
                var newMoConnArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
                AddSectionLabel("Connection", newMoConnArgsPanel);
                var newMoConnArgsCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                BuildConnectionArgsCombo(newMoConnArgsCombo, z, -1);
                newMoConnArgsPanel.Children.Add(newMoConnArgsCombo);
                newMoPlacePanel.Children.Add(newMoConnArgsPanel);
                // NearZone zone selector (initially hidden)
                var newMoNearZonePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
                AddSectionLabel("NearZone", newMoNearZonePanel);
                var newMoNearZoneCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                newMoNearZoneCombo.Items.Add("");
                foreach (var zn in Zones)
                    newMoNearZoneCombo.Items.Add(zn.Name);
                newMoNearZoneCombo.SelectedItem = "";
                newMoNearZonePanel.Children.Add(newMoNearZoneCombo);
                newMoPlacePanel.Children.Add(newMoNearZonePanel);
                newMoPlaceCombo.SelectionChanged += (_, _) =>
                {
                    var sel = newMoPlaceCombo.SelectedItem as string;
                    newMoPlaceArgsPanel.Visibility = sel == "Center" || sel == "NearZone" || sel == "Connection" ? Visibility.Collapsed : Visibility.Visible;
                    newMoConnArgsPanel.Visibility = sel == "Connection" ? Visibility.Visible : Visibility.Collapsed;
                    newMoNearZonePanel.Visibility = sel == "NearZone" ? Visibility.Visible : Visibility.Collapsed;
                };
                newMoPlacePanel.Children.Add(newMoPlaceArgsPanel);
                var newMoAutoRoadCheck = new CheckBox
                {
                    Content = "Авто-дорога",
                    IsChecked = true,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                newMoPlacePanel.Children.Add(newMoAutoRoadCheck);
                mainPanel.Children.Add(newMoPlacePanel);

                // Add Additional Object button
                var addAdditionalMoBtn = new System.Windows.Controls.Button
                {
                    Content = L("S.EC.MoAddAdditionalObject"),
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Visibility = hasMainObject ? Visibility.Visible : Visibility.Collapsed
                };
                addAdditionalMoBtn.Click += (_, _) =>
                {
                    z.MainObjects ??= [];
                    var placement = newMoPlaceCombo.SelectedItem as string ?? "Uniform";
                    var newMo = new MainObject
                    {
                        Type = "City",
                        GuardChance = 1.0,
                        GuardValue = 5000,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "default_buildings_construction",
                        Faction = new TypedSelector { Type = "Random", Args = [] },
                        Placement = placement,
                    };
                    if (placement == "Connection")
                    {
                        var connSel = newMoConnArgsCombo.SelectedItem as string;
                        if (!string.IsNullOrEmpty(connSel))
                            newMo.PlacementArgs = new List<string> { connSel };
                        else
                        {
                            var auto = GetAutoPlacementArgs(z);
                            if (auto != null) newMo.PlacementArgs = new List<string> { auto };
                        }
                    }
                    else if (placement == "NearZone")
                    {
                        var zoneSel = newMoNearZoneCombo.SelectedItem as string;
                        if (!string.IsNullOrEmpty(zoneSel))
                            newMo.PlacementArgs = new List<string> { zoneSel };
                    }
                    else if (placement != "Center")
                    {
                        var argsText = newMoPlaceArgsBox.Text.Trim();
                        if (argsText.Length > 0)
                            newMo.PlacementArgs = new List<string>(argsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    }
                    z.MainObjects.Add(newMo);
                    // Auto-create roads
                    int newIdx = z.MainObjects.Count - 1;
                    if (newIdx > 0 && newMoAutoRoadCheck.IsChecked == true)
                    {
                        z.Roads ??= [];
                        if (placement == "Connection" && newMo.PlacementArgs is { Count: > 0 })
                        {
                            string target = newMo.PlacementArgs[0];
                            // Part E: if target is MO index, generate MO→MO roads
                            if (int.TryParse(target, out int tIdx) && tIdx >= 0 && tIdx < z.MainObjects.Count && tIdx != newIdx)
                            {
                                AddUniqueRoad(z, "MainObject", ["0"], "MainObject", [target]);
                                AddUniqueRoad(z, "MainObject", [newIdx.ToString()], "MainObject", [target]);
                            }
                            else
                            {
                                AddUniqueRoad(z, "MainObject", ["0"], "Connection", [target]);
                                AddUniqueRoad(z, "MainObject", [newIdx.ToString()], "Connection", [target]);
                            }
                        }
                        else
                        {
                            AddUniqueRoad(z, "MainObject", ["0"], "MainObject", [newIdx.ToString()]);
                        }
                    }
                    MarkDirty();
                    RefreshNode(z);
                    BuildInspector();
                };
                mainPanel.Children.Add(addAdditionalMoBtn);

                // Section 6: Content Objects (hidden by default, shown when section 5 exists)
                var contentObjectsSection = new StackPanel { Visibility = Visibility.Collapsed };
                mainPanel.Children.Add(contentObjectsSection);

                // Build sections if data exists
                bool hasAdditionalObjects = z.MainObjects != null && z.MainObjects.Count > 1;
                if (hasAdditionalObjects)
                {
                    additionalMoSection.Visibility = Visibility.Visible;
                    RebuildAdditionalMainObjectsList(z, additionalMoSection);
                }

                // Content objects panel
                var contentObjectsPanel = InspectorFieldsObjects;
                RebuildContentObjectsList(z, contentObjectsPanel);
            }
            else if (_selected is Connection c)
            {
                TxtInspectorHint.Text = L("S.EC.Conn");
                var mainPanel = InspectorFieldsMain;

                // Connection name
                AddTextField(L("S.EC.ConnName"), c.Name ?? "", v =>
                {
                    if (!string.Equals(c.Name, v, StringComparison.Ordinal) &&
                        Connections.Any(x => !ReferenceEquals(x, c) && string.Equals(x.Name, v, StringComparison.Ordinal)))
                    {
                        MessageBox.Show(this, L("S.EC.NameTaken", v), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    c.Name = v; MarkDirty();
                }, mainPanel);

                // From zone (read-only display)
                AddReadOnly(L("S.EC.From"), c.From ?? "?", mainPanel);

                // To zone (read-only display)
                AddReadOnly(L("S.EC.To"), c.To ?? "?", mainPanel);

                // Connection type
                string[] connectionTypes = KnownValues.ConnectionTypes;

                // Length panel (declared before combo handler that references it)
                var lengthPanel = new StackPanel { Visibility = c.ConnectionType == "Proximity" ? Visibility.Visible : Visibility.Collapsed };
                AddSectionLabel(L("S.EC.Length"), lengthPanel);
                var lengthBox = new TextBox
                {
                    Text = (c.Length ?? 0.94).ToString(CultureInfo.InvariantCulture),
                    Margin = new Thickness(0, 0, 0, 8),
                    ToolTip = "Длина связи (0.0–1.0, по умолч. 0.94)"
                };
                lengthBox.LostFocus += (_, _) =>
                {
                    if (double.TryParse(lengthBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    { c.Length = d; MarkDirty(); RebuildGraph(); }
                };
                lengthPanel.Children.Add(lengthBox);

                AddComboField(L("S.EC.Type"), connectionTypes, c.ConnectionType, v =>
                {
                    c.ConnectionType = v;
                    MarkDirty();
                    lengthPanel.Visibility = v == "Proximity" ? Visibility.Visible : Visibility.Collapsed;
                }, mainPanel);

                // Guard value
                AddTextField(L("S.EC.GuardValue"), (c.GuardValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { c.GuardValue = i; MarkDirty(); } }, mainPanel);

                mainPanel.Children.Add(lengthPanel);

                // Road checkbox
                AddCheckField(L("S.EC.Road"), c.Road == true, v =>
                {
                    c.Road = v;
                    MarkDirty();
                    if (v)
                    {
                        AutoGenerateRoadsForConnection(c);
                    }
                    RebuildGraph();
                }, mainPanel);
            }
            else
            {
                TxtInspectorHint.Text = L("S.Ed.011");
            }
        }

        private ComboBox BuildNearZoneCombo(MainObject mo)
        {
            var combo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
            combo.Items.Add("");
            foreach (var z in Zones)
                combo.Items.Add(z.Name);
            if (mo.PlacementArgs is { Count: > 0 })
                combo.SelectedItem = mo.PlacementArgs[0];
            else
                combo.SelectedItem = "";
            combo.SelectionChanged += (_, _) =>
            {
                var sel = combo.SelectedItem as string;
                mo.PlacementArgs = string.IsNullOrEmpty(sel) ? null : new List<string> { sel };
                MarkDirty();
            };
            return combo;
        }

        /// <summary>
        /// Fills a ComboBox with available PlacementArgs values for Connection-placed MOs:
        /// connection names involving this zone + other MO indices in this zone.
        /// </summary>
        private void BuildConnectionArgsCombo(ComboBox combo, Zone zone, int moIndex)
        {
            combo.Items.Clear();
            // Connection names involving this zone
            foreach (var conn in Connections)
            {
                if (conn.Name != null
                    && (string.Equals(conn.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(conn.To, zone.Name, StringComparison.OrdinalIgnoreCase)))
                    combo.Items.Add(conn.Name);
            }
            // Other MO indices in this zone
            if (zone.MainObjects != null)
            {
                for (int j = 0; j < zone.MainObjects.Count; j++)
                {
                    if (j == moIndex) continue; // can't reference self
                    combo.Items.Add(j.ToString());
                }
            }
        }

        /// <summary>
        /// Rebuilds the main object editor (section 4) for a single main object.
        /// This is the detailed editor shown when "Add main object" is clicked.
        /// </summary>
        private void RebuildMainObjectEditor(Zone z, MainObject mo, Panel panel)
        {
            panel.Children.Clear();

            // Track controls that need to be hidden for GladiatorArena
            var guardFields = new List<FrameworkElement>();
            var factionFields = new List<FrameworkElement>();
            var placementFields = new List<FrameworkElement>();

            // Object type selector
            AddSectionLabel(L("S.EC.MoType"), panel);
            var typeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            foreach (var t in KnownValues.MainObjectTypes) typeCombo.Items.Add(t);
            typeCombo.SelectedItem = mo.Type;

            // Guard chance
            var gcPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoGuardChance"), gcPanel);
            var gcBox = new TextBox { Text = (mo.GuardChance ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 8) };
            gcBox.LostFocus += (_, _) => { if (double.TryParse(gcBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardChance = d; MarkDirty(); };
            gcPanel.Children.Add(gcBox);
            guardFields.Add(gcPanel);

            // Guard value
            var gvPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoGuardValue"), gvPanel);
            var gvBox = new TextBox { Text = (mo.GuardValue ?? 0).ToString(), Margin = new Thickness(0, 0, 0, 8) };
            gvBox.LostFocus += (_, _) => { if (int.TryParse(gvBox.Text, out var v)) mo.GuardValue = v; MarkDirty(); };
            gvPanel.Children.Add(gvBox);
            guardFields.Add(gvPanel);

            // Guard weekly increment
            var gwPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoGuardWeeklyInc"), gwPanel);
            var gwBox = new TextBox { Text = (mo.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 8) };
            gwBox.LostFocus += (_, _) => { if (double.TryParse(gwBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardWeeklyIncrement = d; MarkDirty(); };
            gwPanel.Children.Add(gwBox);
            guardFields.Add(gwPanel);

            // Buildings construction
            var buildPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoBuildings"), buildPanel);
            var buildCombo = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            foreach (var b in KnownValues.BuildingsConstructionSids) buildCombo.Items.Add(b);
            buildCombo.Text = mo.BuildingsConstructionSid ?? "";
            buildCombo.LostFocus += (_, _) => { mo.BuildingsConstructionSid = buildCombo.Text.Trim(); MarkDirty(); };
            buildCombo.SelectionChanged += (_, _) => { if (buildCombo.SelectedItem is string s) { mo.BuildingsConstructionSid = s; MarkDirty(); } };
            buildPanel.Children.Add(buildCombo);
            guardFields.Add(buildPanel);

            // Remove guard if owned (declared before ownerCombo handler that references it)
            var removeGuardCheck = new CheckBox { Content = L("S.EC.MoRemoveGuard"), IsChecked = mo.RemoveGuardIfHasOwner == true, Margin = new Thickness(0, 0, 0, 8) };
            removeGuardCheck.Checked += (_, _) =>
            {
                mo.RemoveGuardIfHasOwner = true;
                mo.GuardChance = null;
                mo.GuardValue = null;
                mo.GuardWeeklyIncrement = null;
                gcBox.Text = "0";
                gvBox.Text = "0";
                gwBox.Text = "0";
                foreach (var field in guardFields) field.Visibility = Visibility.Collapsed;
                MarkDirty();
            };
            removeGuardCheck.Unchecked += (_, _) =>
            {
                mo.RemoveGuardIfHasOwner = false;
                UpdateGuardFieldsVisibility();
                MarkDirty();
            };

            // Faction selector type (disabled for AbandonedOutpost and GladiatorArena)
            bool factionEnabled = mo.Type != "AbandonedOutpost" && mo.Type != "GladiatorArena";
            var facTypePanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoFactionType"), facTypePanel);
            var facTypeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200, IsEnabled = factionEnabled };
            foreach (var ft in KnownValues.SelectorTypes) facTypeCombo.Items.Add(ft);
            if (mo.Faction?.Type is not null && facTypeCombo.Items.Contains(mo.Faction.Type))
                facTypeCombo.SelectedItem = mo.Faction.Type;
            else
                facTypeCombo.SelectedItem = "";
            facTypePanel.Children.Add(facTypeCombo);
            factionFields.Add(facTypePanel);

            // Faction args panel (visible only for FromList) - NOT in factionFields, managed separately
            var facArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
            AddSectionLabel(L("S.EC.MoFactionArgs"), facArgsPanel);
            var factionArgsCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), IsEnabled = factionEnabled };
            foreach (var f in KnownValues.FromListFactionArgs) factionArgsCombo.Items.Add(f);
            if (mo.Faction?.Args is { Count: > 0 })
                factionArgsCombo.SelectedItem = mo.Faction.Args[0];
            factionArgsCombo.SelectionChanged += (_, _) =>
            {
                if (factionArgsCombo.SelectedItem is string selected && selected.Length > 0)
                {
                    if (mo.Faction == null) mo.Faction = new TypedSelector();
                    mo.Faction.Args = [selected];
                    MarkDirty();
                }
            };
            facArgsPanel.Children.Add(factionArgsCombo);

            // Owner (visible only for City)
            var ownerPanel = new StackPanel { Visibility = mo.Type == "City" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel(L("S.EC.MoOwner"), ownerPanel);
            var ownerCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            ownerCombo.Items.Add("");
            foreach (var p in KnownValues.SpawnPlayers) ownerCombo.Items.Add(p);
            ownerCombo.SelectedItem = mo.Owner ?? "";
            ownerCombo.SelectionChanged += (_, _) =>
            {
                var newOwner = ownerCombo.SelectedItem as string;
                bool hasOwner = !string.IsNullOrEmpty(newOwner);

                // Check for duplicate owner across zones
                if (hasOwner)
                {
                    var used = GetUsedPlayers(z.Name);
                    if (used.Contains(newOwner!))
                    {
                        var available = KnownValues.SpawnPlayers.FirstOrDefault(p => !used.Contains(p));
                        if (available != null)
                        {
                            System.Windows.MessageBox.Show(this,
                                $"Игрок {newOwner} уже занят в другой зоне. Назначен {available}.", "Конфликт",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            newOwner = available;
                            ownerCombo.SelectedItem = available;
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(this,
                                "Все игроки уже заняты в других зонах.", "Конфликт",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            ownerCombo.SelectedItem = "";
                            return;
                        }
                    }
                }

                mo.Owner = hasOwner ? newOwner : null;

                if (hasOwner)
                {
                    mo.RemoveGuardIfHasOwner = true;
                    mo.GuardChance = null;
                    mo.GuardValue = null;
                    mo.GuardWeeklyIncrement = null;
                    gcBox.Text = "0";
                    gvBox.Text = "0";
                    gwBox.Text = "0";
                    removeGuardCheck.IsChecked = true;
                    if (factionEnabled)
                    {
                        if (mo.Faction == null) mo.Faction = new TypedSelector();
                        mo.Faction.Type = "Match";
                        mo.Faction.Args = ["0"];
                    }
                }
                else
                {
                    mo.RemoveGuardIfHasOwner = false;
                    removeGuardCheck.IsChecked = false;
                }

                UpdateGuardFieldsVisibility();
                MarkDirty();
            };
            ownerPanel.Children.Add(ownerCombo);
            panel.Children.Add(ownerPanel);
            panel.Children.Add(removeGuardCheck);

            // Spawn field panel (only for Spawn type, placed after removeGuardCheck so the handler can reference it)
            var spawnPanel = new StackPanel { Visibility = mo.Type == "Spawn" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel(L("S.EC.Spawn"), spawnPanel);
            var playerCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            foreach (var p in KnownValues.SpawnPlayers) playerCombo.Items.Add(p);
            playerCombo.SelectedItem = mo.Spawn ?? "";
            playerCombo.SelectionChanged += (_, _) =>
            {
                if (playerCombo.SelectedItem is string s)
                {
                    bool hasSpawn = !string.IsNullOrEmpty(s);
                    mo.Spawn = hasSpawn ? s : null;
                    if (hasSpawn)
                    {
                        mo.RemoveGuardIfHasOwner = true;
                        mo.GuardChance = null;
                        mo.GuardValue = null;
                        mo.GuardWeeklyIncrement = null;
                        gcBox.Text = "0";
                        gvBox.Text = "0";
                        gwBox.Text = "0";
                        removeGuardCheck.IsChecked = true;
                    }
                    else
                    {
                        mo.RemoveGuardIfHasOwner = false;
                        removeGuardCheck.IsChecked = false;
                    }
                    AutoRenameZoneForSpawn(z, mo);
                    UpdateGuardFieldsVisibility();
                    MarkDirty();
                }
            };
            spawnPanel.Children.Add(playerCombo);
            // spawnPanel added to panel.Children at the end of this method (line 1318)

            // Placement
            var placePanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoPlacement"), placePanel);
            var placeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
            foreach (var p in KnownValues.MainObjectPlacements) placeCombo.Items.Add(p);
            placeCombo.SelectedItem = mo.Placement ?? "";
            placeCombo.SelectionChanged += (_, _) => { if (placeCombo.SelectedItem is string s) { mo.Placement = s; MarkDirty(); } };
            placePanel.Children.Add(placeCombo);

            // PlacementArgs (textbox for Uniform/other)
            var placeArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            AddSectionLabel("PlacementArgs", placeArgsPanel);
            var placeArgsBox = new TextBox { Text = mo.PlacementArgs is { Count: > 0 } ? string.Join(", ", mo.PlacementArgs) : "" };
            placeArgsBox.LostFocus += (_, _) =>
            {
                var text = placeArgsBox.Text.Trim();
                mo.PlacementArgs = text.Length > 0 ? new List<string>(text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : null;
                MarkDirty();
            };
            placeArgsPanel.Children.Add(placeArgsBox);

            // Connection args ComboBox (shown instead of textbox when Placement=Connection)
            var connArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Visibility = mo.Placement == "Connection" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel("Connection", connArgsPanel);
            var connArgsCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
            BuildConnectionArgsCombo(connArgsCombo, z, 0); // primary MO is always index 0
            if (mo.PlacementArgs is { Count: > 0 })
                connArgsCombo.SelectedItem = mo.PlacementArgs[0];
            connArgsCombo.SelectionChanged += (_, _) =>
            {
                if (connArgsCombo.SelectedItem is string s)
                {
                    mo.PlacementArgs = new List<string> { s };
                    MarkDirty();
                }
            };
            connArgsPanel.Children.Add(connArgsCombo);

            // NearZone zone selector
            var nearZonePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Visibility = mo.Placement == "NearZone" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel("NearZone", nearZonePanel);
            var nearZoneCombo = BuildNearZoneCombo(mo);
            nearZonePanel.Children.Add(nearZoneCombo);

            // Show/hide args based on placement selection
            placeCombo.SelectionChanged += (_, _) =>
            {
                var sel = placeCombo.SelectedItem as string;
                placeArgsPanel.Visibility = sel == "Center" || sel == "NearZone" || sel == "Connection" ? Visibility.Collapsed : Visibility.Visible;
                connArgsPanel.Visibility = sel == "Connection" ? Visibility.Visible : Visibility.Collapsed;
                nearZonePanel.Visibility = sel == "NearZone" ? Visibility.Visible : Visibility.Collapsed;
                // Auto-assign PlacementArgs when switching to Connection
                if (sel == "Connection" && (mo.PlacementArgs == null || mo.PlacementArgs.Count == 0))
                {
                    var auto = GetAutoPlacementArgs(z);
                    if (auto != null)
                    {
                        mo.PlacementArgs = new List<string> { auto };
                        connArgsCombo.SelectedItem = auto;
                    }
                }
            };

            placementFields.Add(placePanel);
            placementFields.Add(placeArgsPanel);
            placementFields.Add(connArgsPanel);
            placementFields.Add(nearZonePanel);

            // Hold city win condition
            AddCheckField(L("S.EC.MoHoldCity"), mo.HoldCityWinCon == true, v => { mo.HoldCityWinCon = v; MarkDirty(); }, panel);

            // Function to update guard fields visibility based on RemoveGuardIfHasOwner flag and owner/spawn
            void UpdateGuardFieldsVisibility()
            {
                bool hideDueToOwner = mo.Owner != null || mo.Spawn != null;
                bool showGuard = !hideDueToOwner && mo.Type != "GladiatorArena" && mo.Type != "AbandonedOutpost" && mo.RemoveGuardIfHasOwner != true;
                foreach (var field in guardFields) field.Visibility = showGuard ? Visibility.Visible : Visibility.Collapsed;
            }

            // Function to update visibility based on type
            void UpdateFieldVisibility(string type)
            {
                bool isGladiator = type == "GladiatorArena";
                bool isAbandonedOutpost = type == "AbandonedOutpost";
                bool isSpawn = type == "Spawn";
                bool showFaction = !isGladiator && !isAbandonedOutpost && !isSpawn;
                bool showPlacement = !isGladiator;

                UpdateGuardFieldsVisibility();
                foreach (var field in factionFields) field.Visibility = showFaction ? Visibility.Visible : Visibility.Collapsed;
                foreach (var field in placementFields) field.Visibility = showPlacement ? Visibility.Visible : Visibility.Collapsed;

                spawnPanel.Visibility = type == "Spawn" ? Visibility.Visible : Visibility.Collapsed;

                // Hide faction args when type changes (will be shown by facTypeCombo handler if needed)
                facArgsPanel.Visibility = Visibility.Collapsed;

                // Update faction enabled state
                bool factionEnabledNew = !isAbandonedOutpost && !isGladiator;
                facTypeCombo.IsEnabled = factionEnabledNew;
                factionArgsCombo.IsEnabled = factionEnabledNew;

                // Hide owner for non-City types
                ownerPanel.Visibility = type == "City" ? Visibility.Visible : Visibility.Collapsed;
                if (type != "City")
                {
                    mo.Owner = null;
                    ownerCombo.SelectedItem = "";
                }

                // Clear faction when type is Spawn
                if (isSpawn)
                {
                    mo.Faction = null;
                    facTypeCombo.SelectedItem = "";
                    factionArgsCombo.SelectedItem = null;
                }
            }

            // Type combo handler
            typeCombo.SelectionChanged += (_, _) =>
            {
                if (typeCombo.SelectedItem is string s)
                {
                    mo.Type = s;
                    OnMainObjectTypeChanged(mo);
                    if (mo.Type == "Spawn") AutoRenameZoneForSpawn(z, mo);
                    UpdateFieldVisibility(s);
                    MarkDirty();
                    RefreshNode(z);
                }
            };

            // Faction type combo handler
            facTypeCombo.SelectionChanged += (_, _) =>
            {
                if (facTypeCombo.SelectedItem is string s)
                {
                    if (mo.Faction == null) mo.Faction = new TypedSelector();
                    mo.Faction.Type = s;
                    facArgsPanel.Visibility = s == "FromList" ? Visibility.Visible : Visibility.Collapsed;
                    if (s != "FromList")
                    {
                        mo.Faction.Args = [];
                        factionArgsCombo.SelectedItem = null;
                    }
                    MarkDirty();
                }
            };

            // Add all controls to panel in order
            panel.Children.Add(typeCombo);
            panel.Children.Add(spawnPanel);
            foreach (var field in guardFields) panel.Children.Add(field);
            foreach (var field in factionFields) panel.Children.Add(field);
            panel.Children.Add(facArgsPanel);
            foreach (var field in placementFields) panel.Children.Add(field);

            // Set initial visibility
            UpdateFieldVisibility(mo.Type);
            if (mo.Faction?.Type == "FromList")
                facArgsPanel.Visibility = Visibility.Visible;
        }

        private static readonly string[] MainObjectKinds = ["City", "AbandonedOutpost"];

        /// <summary>Switches a zone's primary main object between a castle (<c>City</c>) and an
        /// <c>AbandonedOutpost</c>. An outpost is neutral (no faction/owner) and, when captured, grants the
        /// taker their OWN faction's town instead of a random castle — the shape mirrors the official
        /// templates (e.g. Hallway: guarded 30k, rich buildings, Uniform placement).</summary>
        private static void SetMainObjectKind(MainObject o, string kind)
        {
            if (o.Type == kind) return;
            o.Type = kind;
            if (kind == "AbandonedOutpost")
            {
                o.Faction = null;        // outposts carry no faction — the captor gets their native town
                o.Owner = null;
                o.HoldCityWinCon = null;
                o.BuildingsConstructionSid ??= "rich_buildings_construction";
                o.GuardChance ??= 1.0;
                o.GuardValue ??= 30000;
                o.GuardWeeklyIncrement ??= 0.20;
                o.Placement ??= "Uniform";
            }
            else // City
            {
                o.Faction ??= new TypedSelector { Type = "Random", Args = [] };
                o.BuildingsConstructionSid ??= "default_buildings_construction";
            }
        }

        private void RebuildMainObjectsList(Zone z, Panel panel)
        {
            panel.Children.Clear();

            var addBtn = new System.Windows.Controls.Button
            {
                Content = L("S.EC.MoAddObject"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            addBtn.Click += (_, _) =>
            {
                z.MainObjects ??= [];
                var mo = new MainObject { Type = "City", GuardChance = 1.0, GuardValue = 5000, GuardWeeklyIncrement = 0.10, BuildingsConstructionSid = "default_buildings_construction", Faction = new TypedSelector { Type = "Random", Args = [] }, Placement = "Uniform" };
                z.MainObjects.Add(mo);
                MarkDirty();
                RefreshNode(z);
                BuildInspector();
            };
            panel.Children.Add(addBtn);

            if (z.MainObjects == null || z.MainObjects.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = L("S.EC.MoNoObjects"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
                return;
            }

            for (int i = 0; i < z.MainObjects.Count; i++)
            {
                var mo = z.MainObjects[i];
                var itemPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var typeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 8, 0), MinWidth = 120, MaxDropDownHeight = 200 };
                foreach (var t in KnownValues.MainObjectTypes) typeCombo.Items.Add(t);
                typeCombo.SelectedItem = mo.Type;
                typeCombo.SelectionChanged += (_, _) => { if (typeCombo.SelectedItem is string s) { mo.Type = s; MarkDirty(); RefreshNode(z); } };
                DockPanel.SetDock(typeCombo, Dock.Left);
                headerRow.Children.Add(typeCombo);

                var removeBtn = new System.Windows.Controls.Button { Content = "✕", FontSize = 9, Padding = new Thickness(4, 0, 4, 0), MinWidth = 18, MinHeight = 18, Margin = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = (Brush)FindResource("BrushTextDim"), Cursor = Cursors.Hand };
                var capturedIdx = i;
                removeBtn.Click += (_, _) =>
                {
                    if (capturedIdx < z.MainObjects.Count)
                    {
                        AdjustRoadIndicesAfterRemoval(z, capturedIdx);
                        z.MainObjects.RemoveAt(capturedIdx);
                        MarkDirty();
                        RefreshNode(z);
                        BuildInspector();
                    }
                };
                headerRow.Children.Add(removeBtn);
                itemPanel.Children.Add(headerRow);

                var fieldsGrid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
                fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fieldsGrid.RowDefinitions.Add(new RowDefinition());
                fieldsGrid.RowDefinitions.Add(new RowDefinition());
                fieldsGrid.RowDefinitions.Add(new RowDefinition());
                fieldsGrid.RowDefinitions.Add(new RowDefinition());

                var gcBox = new TextBox { Text = (mo.GuardChance ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 4, 4) };
                gcBox.LostFocus += (_, _) => { if (double.TryParse(gcBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardChance = d; MarkDirty(); };
                var gvBox = new TextBox { Text = (mo.GuardValue ?? 0).ToString(), Margin = new Thickness(4, 0, 0, 4) };
                gvBox.LostFocus += (_, _) => { if (int.TryParse(gvBox.Text, out var v)) mo.GuardValue = v; MarkDirty(); };
                var gwBox = new TextBox { Text = (mo.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 4, 4) };
                gwBox.LostFocus += (_, _) => { if (double.TryParse(gwBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardWeeklyIncrement = d; MarkDirty(); };
                var buildCombo = new ComboBox { IsEditable = false, Margin = new Thickness(4, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var b in KnownValues.BuildingsConstructionSids) buildCombo.Items.Add(b);
                buildCombo.SelectedItem = mo.BuildingsConstructionSid ?? "";
                buildCombo.SelectionChanged += (_, _) => { if (buildCombo.SelectedItem is string s) { mo.BuildingsConstructionSid = s; MarkDirty(); } };
                var facCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 4, 4), MaxDropDownHeight = 200 };
                foreach (var f in KnownValues.SelectorTypes) facCombo.Items.Add(f);
                facCombo.SelectedItem = mo.Faction?.Type ?? "";
                facCombo.SelectionChanged += (_, _) => { if (facCombo.SelectedItem is string s) { if (mo.Faction == null) mo.Faction = new TypedSelector(); mo.Faction.Type = s; MarkDirty(); } };
                var placeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(4, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var p in KnownValues.MainObjectPlacements) placeCombo.Items.Add(p);
                placeCombo.SelectedItem = mo.Placement ?? "";
                placeCombo.SelectionChanged += (_, _) => { if (placeCombo.SelectedItem is string s) { mo.Placement = s; MarkDirty(); } };

                var gcLabel = new TextBlock { Text = L("S.EC.MoGuardChance"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 10, Margin = new Thickness(0, 0, 4, 0) };
                var gvLabel = new TextBlock { Text = L("S.EC.MoGuardValue"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 10, Margin = new Thickness(4, 0, 0, 0) };
                var gwLabel = new TextBlock { Text = L("S.EC.MoGuardWeeklyInc"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 10, Margin = new Thickness(0, 0, 4, 0) };
                var bLabel = new TextBlock { Text = L("S.EC.MoBuildings"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 10, Margin = new Thickness(4, 0, 0, 0) };
                var fLabel = new TextBlock { Text = L("S.EC.MoFaction"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 10, Margin = new Thickness(0, 0, 4, 0) };
                var pLabel = new TextBlock { Text = L("S.EC.MoPlacement"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 10, Margin = new Thickness(4, 0, 0, 0) };

                var row0 = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                row0.Children.Add(gcLabel); row0.Children.Add(gvLabel);
                Grid.SetRow(row0, 0); Grid.SetColumn(row0, 0); Grid.SetColumnSpan(row0, 2);
                fieldsGrid.Children.Add(row0);

                Grid.SetRow(gcBox, 1); Grid.SetColumn(gcBox, 0);
                Grid.SetRow(gvBox, 1); Grid.SetColumn(gvBox, 1);
                fieldsGrid.Children.Add(gcBox); fieldsGrid.Children.Add(gvBox);

                var row2 = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                row2.Children.Add(gwLabel); row2.Children.Add(bLabel);
                Grid.SetRow(row2, 2); Grid.SetColumn(row2, 0); Grid.SetColumnSpan(row2, 2);
                fieldsGrid.Children.Add(row2);

                Grid.SetRow(gwBox, 3); Grid.SetColumn(gwBox, 0);
                Grid.SetRow(buildCombo, 3); Grid.SetColumn(buildCombo, 1);
                fieldsGrid.Children.Add(gwBox); fieldsGrid.Children.Add(buildCombo);

                var row4 = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                row4.Children.Add(fLabel); row4.Children.Add(pLabel);
                Grid.SetRow(row4, 4); Grid.SetColumn(row4, 0); Grid.SetColumnSpan(row4, 2);
                fieldsGrid.Children.Add(row4);

                Grid.SetRow(facCombo, 5); Grid.SetColumn(facCombo, 0);
                Grid.SetRow(placeCombo, 5); Grid.SetColumn(placeCombo, 1);
                fieldsGrid.Children.Add(facCombo); fieldsGrid.Children.Add(placeCombo);

                itemPanel.Children.Add(fieldsGrid);

                var sep = new System.Windows.Shapes.Rectangle { Height = 1, Fill = (Brush)FindResource("BrushBorder"), Margin = new Thickness(0, 4, 0, 0) };
                itemPanel.Children.Add(sep);

                panel.Children.Add(itemPanel);
            }
        }

        /// <summary>
        /// Additional main objects list (for objects beyond the first one).
        /// Shown when there are multiple main objects in a zone.
        /// </summary>
        /// <summary>
        /// List of main object types available for additional objects (section 5).
        /// Excludes "Spawn" which is only for the primary main object.
        /// </summary>
        private static readonly string[] AdditionalMainObjectTypes = ["City", "AbandonedOutpost", "GladiatorArena"];

        private void RebuildAdditionalMainObjectsList(Zone z, Panel panel)
        {
            panel.Children.Clear();

            if (z.MainObjects == null || z.MainObjects.Count <= 1)
            {
                panel.Visibility = Visibility.Collapsed;
                return;
            }

            panel.Visibility = Visibility.Visible;

            // Show all objects except the first one (which is in section 4)
            for (int i = 1; i < z.MainObjects.Count; i++)
            {
                var mo = z.MainObjects[i];
                var itemPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var headerText = new TextBlock
                {
                    Text = $"{mo.Type ?? "?"} #{i}",
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(headerText, Dock.Left);
                headerRow.Children.Add(headerText);

                var removeBtn = new System.Windows.Controls.Button
                {
                    Content = "✕",
                    FontSize = 9,
                    Padding = new Thickness(4, 0, 4, 0),
                    MinWidth = 18,
                    MinHeight = 18,
                    Margin = new Thickness(0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = (Brush)FindResource("BrushTextDim"),
                    Cursor = Cursors.Hand
                };
                var capturedIdx = i;
                removeBtn.Click += (_, _) =>
                {
                    if (capturedIdx < z.MainObjects.Count)
                    {
                        AdjustRoadIndicesAfterRemoval(z, capturedIdx);
                        z.MainObjects.RemoveAt(capturedIdx);
                        MarkDirty();
                        RefreshNode(z);
                        BuildInspector();
                    }
                };
                headerRow.Children.Add(removeBtn);
                itemPanel.Children.Add(headerRow);

                // Track controls for conditional visibility
                var guardFields = new List<FrameworkElement>();
                var factionFields = new List<FrameworkElement>();
                var placementFields = new List<FrameworkElement>();

                // Object type selector (without Spawn)
                var typeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var t in AdditionalMainObjectTypes) typeCombo.Items.Add(t);
                typeCombo.SelectedItem = mo.Type;

                // Guard chance
                var gcPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoGuardChance"), gcPanel);
                var gcBox = new TextBox { Text = (mo.GuardChance ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 4) };
                gcBox.LostFocus += (_, _) => { if (double.TryParse(gcBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardChance = d; MarkDirty(); };
                gcPanel.Children.Add(gcBox);
                guardFields.Add(gcPanel);

                // Guard value
                var gvPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoGuardValue"), gvPanel);
                var gvBox = new TextBox { Text = (mo.GuardValue ?? 0).ToString(), Margin = new Thickness(0, 0, 0, 4) };
                gvBox.LostFocus += (_, _) => { if (int.TryParse(gvBox.Text, out var v)) mo.GuardValue = v; MarkDirty(); };
                gvPanel.Children.Add(gvBox);
                guardFields.Add(gvPanel);

                // Guard weekly increment
                var gwPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoGuardWeeklyInc"), gwPanel);
                var gwBox = new TextBox { Text = (mo.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 4) };
                gwBox.LostFocus += (_, _) => { if (double.TryParse(gwBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardWeeklyIncrement = d; MarkDirty(); };
                gwPanel.Children.Add(gwBox);
                guardFields.Add(gwPanel);

                // Buildings construction
                var buildPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoBuildings"), buildPanel);
                var buildCombo = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var b in KnownValues.BuildingsConstructionSids) buildCombo.Items.Add(b);
                buildCombo.Text = mo.BuildingsConstructionSid ?? "";
                buildCombo.LostFocus += (_, _) => { mo.BuildingsConstructionSid = buildCombo.Text.Trim(); MarkDirty(); };
                buildCombo.SelectionChanged += (_, _) => { if (buildCombo.SelectedItem is string s) { mo.BuildingsConstructionSid = s; MarkDirty(); } };
                buildPanel.Children.Add(buildCombo);
                guardFields.Add(buildPanel);

                // Faction selector type
                bool factionEnabled = mo.Type != "AbandonedOutpost" && mo.Type != "GladiatorArena";
                var facTypePanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoFactionType"), facTypePanel);
                var facTypeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200, IsEnabled = factionEnabled };
                foreach (var f in KnownValues.SelectorTypes) facTypeCombo.Items.Add(f);
                if (mo.Faction?.Type is not null && facTypeCombo.Items.Contains(mo.Faction.Type))
                    facTypeCombo.SelectedItem = mo.Faction.Type;
                else
                    facTypeCombo.SelectedItem = "";
                facTypePanel.Children.Add(facTypeCombo);
                factionFields.Add(facTypePanel);

                // Faction args panel (visible only for FromList) - NOT in factionFields, managed separately
                var facArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
                AddSectionLabel(L("S.EC.MoFactionArgs"), facArgsPanel);
                var factionArgsCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), IsEnabled = factionEnabled };
                foreach (var f in KnownValues.FromListFactionArgs) factionArgsCombo.Items.Add(f);
                if (mo.Faction?.Args is { Count: > 0 })
                    factionArgsCombo.SelectedItem = mo.Faction.Args[0];
                factionArgsCombo.SelectionChanged += (_, _) =>
                {
                    if (factionArgsCombo.SelectedItem is string selected && selected.Length > 0)
                    {
                        if (mo.Faction == null) mo.Faction = new TypedSelector();
                        mo.Faction.Args = [selected];
                        MarkDirty();
                    }
                };
                facArgsPanel.Children.Add(factionArgsCombo);

                // Owner (visible only for City, hidden for Spawn)
                var ownerPanel = new StackPanel { Visibility = mo.Type == "City" ? Visibility.Visible : Visibility.Collapsed };
                AddSectionLabel(L("S.EC.MoOwner"), ownerPanel);
                var ownerCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                ownerCombo.Items.Add("");
                foreach (var p in KnownValues.SpawnPlayers) ownerCombo.Items.Add(p);
                ownerCombo.SelectedItem = mo.Owner ?? "";
                ownerCombo.SelectionChanged += (_, _) =>
                {
                    var newOwner = ownerCombo.SelectedItem as string;
                    bool hasOwner = !string.IsNullOrEmpty(newOwner);

                    // Check for duplicate owner across zones
                    if (hasOwner)
                    {
                        var used = GetUsedPlayers(z.Name);
                        if (used.Contains(newOwner!))
                        {
                            var available = KnownValues.SpawnPlayers.FirstOrDefault(p => !used.Contains(p));
                            if (available != null)
                            {
                                System.Windows.MessageBox.Show(this,
                                    $"Игрок {newOwner} уже занят в другой зоне. Назначен {available}.", "Конфликт",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                newOwner = available;
                                ownerCombo.SelectedItem = available;
                            }
                            else
                            {
                                System.Windows.MessageBox.Show(this,
                                    "Все игроки уже заняты в других зонах.", "Конфликт",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                ownerCombo.SelectedItem = "";
                                return;
                            }
                        }
                    }

                    mo.Owner = hasOwner ? newOwner : null;

                    if (hasOwner)
                    {
                        mo.RemoveGuardIfHasOwner = true;
                        mo.GuardChance = null;
                        mo.GuardValue = null;
                        mo.GuardWeeklyIncrement = null;
                        gcBox.Text = "0";
                        gvBox.Text = "0";
                        gwBox.Text = "0";
                        if (factionEnabled)
                        {
                            if (mo.Faction == null) mo.Faction = new TypedSelector();
                            mo.Faction.Type = "Match";
                            mo.Faction.Args = ["0"];
                        }
                    }
                    else
                    {
                        mo.RemoveGuardIfHasOwner = false;
                    }

                    UpdateFieldVisibility(mo.Type ?? "City");
                    MarkDirty();
                };
                ownerPanel.Children.Add(ownerCombo);

                // Placement
                var placePanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoPlacement"), placePanel);
                var placeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var p in KnownValues.MainObjectPlacements) placeCombo.Items.Add(p);
                placeCombo.SelectedItem = mo.Placement ?? "";
                placeCombo.SelectionChanged += (_, _) => { if (placeCombo.SelectedItem is string s) { mo.Placement = s; MarkDirty(); } };
                placePanel.Children.Add(placeCombo);

                // PlacementArgs (textbox for Uniform/other)
                var placeArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
                AddSectionLabel("PlacementArgs", placeArgsPanel);
                var placeArgsBox = new TextBox { Text = mo.PlacementArgs is { Count: > 0 } ? string.Join(", ", mo.PlacementArgs) : "" };
                placeArgsBox.LostFocus += (_, _) =>
                {
                    var text = placeArgsBox.Text.Trim();
                    mo.PlacementArgs = text.Length > 0 ? new List<string>(text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : null;
                    MarkDirty();
                };
                placeArgsPanel.Children.Add(placeArgsBox);

                // Connection args ComboBox (shown instead of textbox when Placement=Connection)
                var connArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = mo.Placement == "Connection" ? Visibility.Visible : Visibility.Collapsed };
                AddSectionLabel("Connection", connArgsPanel);
                var connArgsCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
            BuildConnectionArgsCombo(connArgsCombo, z, i);
                if (mo.PlacementArgs is { Count: > 0 })
                    connArgsCombo.SelectedItem = mo.PlacementArgs[0];
                connArgsCombo.SelectionChanged += (_, _) =>
                {
                    if (connArgsCombo.SelectedItem is string s)
                    {
                        mo.PlacementArgs = new List<string> { s };
                        MarkDirty();
                    }
                };
                connArgsPanel.Children.Add(connArgsCombo);

                // NearZone zone selector
                var nearZonePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = mo.Placement == "NearZone" ? Visibility.Visible : Visibility.Collapsed };
                AddSectionLabel("NearZone", nearZonePanel);
                var nearZoneCombo = BuildNearZoneCombo(mo);
                nearZonePanel.Children.Add(nearZoneCombo);

                // Show/hide args based on placement selection
                placeCombo.SelectionChanged += (_, _) =>
                {
                    var sel = placeCombo.SelectedItem as string;
                    placeArgsPanel.Visibility = sel == "Center" || sel == "NearZone" || sel == "Connection" ? Visibility.Collapsed : Visibility.Visible;
                    connArgsPanel.Visibility = sel == "Connection" ? Visibility.Visible : Visibility.Collapsed;
                    nearZonePanel.Visibility = sel == "NearZone" ? Visibility.Visible : Visibility.Collapsed;
                    // Auto-assign PlacementArgs when switching to Connection
                    if (sel == "Connection" && (mo.PlacementArgs == null || mo.PlacementArgs.Count == 0))
                    {
                        var auto = GetAutoPlacementArgs(z);
                        if (auto != null)
                        {
                            mo.PlacementArgs = new List<string> { auto };
                            connArgsCombo.SelectedItem = auto;
                        }
                    }
                };

                placementFields.Add(placePanel);
                placementFields.Add(placeArgsPanel);
                placementFields.Add(connArgsPanel);
                placementFields.Add(nearZonePanel);

                // Function to update visibility based on type
                void UpdateFieldVisibility(string type)
                {
                    bool isGladiator = type == "GladiatorArena";
                    bool isAbandonedOutpost = type == "AbandonedOutpost";
                    bool hideGuards = mo.Owner != null || mo.Spawn != null;
                    bool showGuard = !isGladiator && !isAbandonedOutpost && !hideGuards;
                    bool showFaction = !isGladiator && !isAbandonedOutpost;
                    bool showPlacement = !isGladiator;
                    bool showOwner = type == "City";

                    foreach (var field in guardFields) field.Visibility = showGuard ? Visibility.Visible : Visibility.Collapsed;
                    foreach (var field in factionFields) field.Visibility = showFaction ? Visibility.Visible : Visibility.Collapsed;
                    foreach (var field in placementFields) field.Visibility = showPlacement ? Visibility.Visible : Visibility.Collapsed;
                    ownerPanel.Visibility = showOwner ? Visibility.Visible : Visibility.Collapsed;

                    // Hide faction args when type changes (will be shown by facTypeCombo handler if needed)
                    facArgsPanel.Visibility = Visibility.Collapsed;

                    // Update faction enabled state
                    bool factionEnabledNew = !isAbandonedOutpost && !isGladiator;
                    facTypeCombo.IsEnabled = factionEnabledNew;
                    factionArgsCombo.IsEnabled = factionEnabledNew;
                }

                // Type combo handler
                typeCombo.SelectionChanged += (_, _) =>
                {
                    if (typeCombo.SelectedItem is string s)
                    {
                        mo.Type = s;
                        if (s != "City")
                        {
                            mo.Owner = null;
                            ownerCombo.SelectedItem = "";
                        }
                        OnMainObjectTypeChanged(mo);
                        UpdateFieldVisibility(s);
                        MarkDirty();
                        RefreshNode(z);
                    }
                };

                // Faction type combo handler
                facTypeCombo.SelectionChanged += (_, _) =>
                {
                    if (facTypeCombo.SelectedItem is string s)
                    {
                        if (mo.Faction == null) mo.Faction = new TypedSelector();
                        mo.Faction.Type = s;
                        facArgsPanel.Visibility = s == "FromList" ? Visibility.Visible : Visibility.Collapsed;
                        if (s != "FromList")
                        {
                            mo.Faction.Args = [];
                            factionArgsCombo.SelectedItem = null;
                        }
                        MarkDirty();
                    }
                };

                // Add all controls to item panel
                itemPanel.Children.Add(typeCombo);
                foreach (var field in guardFields) itemPanel.Children.Add(field);
                foreach (var field in factionFields) itemPanel.Children.Add(field);
                itemPanel.Children.Add(facArgsPanel);
                itemPanel.Children.Add(ownerPanel);
                foreach (var field in placementFields) itemPanel.Children.Add(field);

                var sep = new System.Windows.Shapes.Rectangle { Height = 1, Fill = (Brush)FindResource("BrushBorder"), Margin = new Thickness(0, 4, 0, 0) };
                itemPanel.Children.Add(sep);

                panel.Children.Add(itemPanel);

                // Set initial visibility
                UpdateFieldVisibility(mo.Type ?? "City");
                if (mo.Faction?.Type == "FromList")
                    facArgsPanel.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Content objects (mandatory content items) that can be added to a zone.
        /// These are the objects from the "Zone Content" tab in the main window.
        /// </summary>
        private void RebuildContentObjectsList(Zone z, Panel panel)
        {
            panel.Children.Clear();

            // List to store content items for this zone
            z.MandatoryContent ??= [];

            // Add new content object section
            var addSection = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            // Object selector
            AddSectionLabel(L("S.EC.SelectContentObject"), addSection);
            var objectCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 300 };
            objectCombo.Items.Add("");
            foreach (var obj in KnownValues.ObjectSids) objectCombo.Items.Add(obj);
            addSection.Children.Add(objectCombo);

            // Count field
            AddSectionLabel(L("S.EC.ContentObjectCount"), addSection);
            var countBox = new TextBox { Text = "1", Margin = new Thickness(0, 0, 0, 8) };
            addSection.Children.Add(countBox);

            // IsGuarded checkbox
            var guardedCheck = new CheckBox { Content = L("S.EC.ContentObjectGuarded"), IsChecked = false, Margin = new Thickness(0, 0, 0, 8) };
            addSection.Children.Add(guardedCheck);

            // Near MainObject checkbox
            var mainObjCheck = new CheckBox { Content = L("S.EC.ContentObjectMainObj"), IsChecked = false, Margin = new Thickness(0, 0, 0, 8) };
            addSection.Children.Add(mainObjCheck);

            // Road distance fields
            var roadDistPanel = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            roadDistPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            roadDistPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var minPanel = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
            AddSectionLabel(L("S.EC.RoadDistanceMin"), minPanel);
            var minBox = new TextBox { Text = "0.15" };
            minPanel.Children.Add(minBox);
            Grid.SetColumn(minPanel, 0);
            roadDistPanel.Children.Add(minPanel);

            var maxPanel = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
            AddSectionLabel(L("S.EC.RoadDistanceMax"), maxPanel);
            var maxBox = new TextBox { Text = "0.30" };
            maxPanel.Children.Add(maxBox);
            Grid.SetColumn(maxPanel, 1);
            roadDistPanel.Children.Add(maxPanel);

            addSection.Children.Add(roadDistPanel);

            // Add button
            var addBtn = new System.Windows.Controls.Button
            {
                Content = L("S.EC.AddContentObject"),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            addBtn.Click += (_, _) =>
            {
                if (objectCombo.SelectedItem is not string selectedObj || string.IsNullOrEmpty(selectedObj))
                    return;

                if (!int.TryParse(countBox.Text, out var count) || count < 1)
                    count = 1;

                // Build the content item name with rules
                string itemName = selectedObj;

                // Add to mandatory content list
                if (!z.MandatoryContent.Contains(itemName))
                {
                    z.MandatoryContent.Add(itemName);
                }

                // Store content item settings in a special format (we'll use a dictionary-like approach)
                // For now, we'll store the settings as JSON in a comment-like format
                // This will be processed during export

                MarkDirty();
                BuildInspector();
            };
            addSection.Children.Add(addBtn);

            panel.Children.Add(addSection);

            // Separator
            panel.Children.Add(new System.Windows.Shapes.Rectangle { Height = 1, Fill = (Brush)FindResource("BrushBorder"), Margin = new Thickness(0, 0, 0, 12) });

            // Existing content objects list
            AddSectionLabel(L("S.EC.MoObjectsList"), panel);

            if (z.MandatoryContent.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = L("S.EC.NoContentObjects"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
            }
            else
            {
                for (int i = 0; i < z.MandatoryContent.Count; i++)
                {
                    var itemName = z.MandatoryContent[i];
                    var itemPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

                    var nameText = new TextBlock { Text = itemName, VerticalAlignment = VerticalAlignment.Center };
                    DockPanel.SetDock(nameText, Dock.Left);
                    itemPanel.Children.Add(nameText);

                    var removeBtn = new System.Windows.Controls.Button
                    {
                        Content = "✕",
                        FontSize = 9,
                        Padding = new Thickness(4, 0, 4, 0),
                        MinWidth = 18,
                        MinHeight = 18,
                        Margin = new Thickness(0),
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = (Brush)FindResource("BrushTextDim"),
                        Cursor = Cursors.Hand
                    };
                    var capturedIdx = i;
                    removeBtn.Click += (_, _) =>
                    {
                        if (capturedIdx < z.MandatoryContent.Count)
                        {
                            z.MandatoryContent.RemoveAt(capturedIdx);
                            MarkDirty();
                            BuildInspector();
                        }
                    };
                    DockPanel.SetDock(removeBtn, Dock.Right);
                    itemPanel.Children.Add(removeBtn);

                    panel.Children.Add(itemPanel);
                }
            }
        }

        private void AddSectionLabel(string text, Panel panel) =>
            panel.Children.Add(new TextBlock
            {
                Text = text, Foreground = (Brush)FindResource("BrushTextDim"),
                FontSize = 12, Margin = new Thickness(0, 8, 0, 2),
            });

        private void AddTextField(string label, string value, Action<string> onCommit, Panel panel, string? tooltip = null)
        {
            AddSectionLabel(label, panel);
            var box = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 4) };
            if (tooltip != null)
                box.ToolTip = tooltip;
            box.LostFocus += (_, _) => onCommit(box.Text.Trim());
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) onCommit(box.Text.Trim()); };
            panel.Children.Add(box);
        }

        private void AddComboField(string label, string[] options, string? value, Action<string> onCommit, Panel panel)
        {
            AddSectionLabel(label, panel);
            var combo = new ComboBox
            {
                IsEditable = true,
                Margin = new Thickness(0, 0, 0, 4),
                MaxDropDownHeight = 300,
            };
            foreach (var o in options) combo.Items.Add(o);
            if (value is not null && combo.Items.Contains(value))
                combo.SelectedItem = value;
            else
                combo.Text = value ?? "";
            combo.LostFocus += (_, _) => onCommit(combo.Text.Trim());
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string s)
                {
                    combo.Text = s;
                    onCommit(s);
                }
            };
            panel.Children.Add(combo);
        }

        private void AddCheckField(string label, bool value, Action<bool> onCommit, Panel panel, string? tooltip = null)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 6, 0, 4), LastChildFill = true };
            var chk = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Top };
            var txt = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 0, 0) };
            DockPanel.SetDock(chk, Dock.Left);
            dock.Children.Add(chk);
            dock.Children.Add(txt);
            if (tooltip != null)
            {
                chk.ToolTip = tooltip;
                txt.ToolTip = tooltip;
            }
            chk.Checked   += (_, _) => onCommit(true);
            chk.Unchecked += (_, _) => onCommit(false);
            panel.Children.Add(dock);
        }

        private void AddReadOnly(string label, string value, Panel panel)
        {
            AddSectionLabel(label, panel);
            panel.Children.Add(new TextBlock
            {
                Text = value, Foreground = (Brush)FindResource("BrushText"),
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        private void AddStringListField(string label, List<string>? value, Action<List<string>> onCommit, Panel panel)
        {
            AddSectionLabel(label, panel);
            var text = value is { Count: > 0 } ? string.Join(", ", value) : "";
            var box = new TextBox { Text = text, Margin = new Thickness(0, 0, 0, 4), MinHeight = 40, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            box.LostFocus += (_, _) => onCommit(ParseStringList(box.Text));
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter && !e.Handled) { onCommit(ParseStringList(box.Text)); } };
            panel.Children.Add(box);
        }

        private void AddStringListPicker(string label, string[] options, List<string>? current, Action<List<string>> onCommit, Panel panel)
        {
            AddSectionLabel(label, panel);
            var innerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Orientation = System.Windows.Controls.Orientation.Vertical };

            var existingPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            if (current is { Count: > 0 })
            {
                foreach (var item in current)
                {
                    var chip = new System.Windows.Controls.Border
                    {
                        Background = (Brush)FindResource("BrushInput"),
                        BorderBrush = (Brush)FindResource("BrushBorder"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 2, 2),
                        Margin = new Thickness(0, 0, 4, 4),
                    };
                    var chipGrid = new Grid();
                    chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var txt = new TextBlock { Text = item, FontSize = 10, Foreground = (Brush)FindResource("BrushText"), VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
                    Grid.SetColumn(txt, 0);
                    var btn = new System.Windows.Controls.Button
                    {
                        Content = "✕",
                        FontSize = 9,
                        Padding = new Thickness(4, 0, 4, 0),
                        Margin = new Thickness(0),
                        MinWidth = 18,
                        MinHeight = 18,
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = (Brush)FindResource("BrushTextDim"),
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    var capturedItem = item;
                    btn.Click += (_, _) =>
                    {
                        var list = current?.ToList() ?? new List<string>();
                        list.Remove(capturedItem);
                        onCommit(list);
                        BuildInspector();
                    };
                    Grid.SetColumn(btn, 1);
                    chipGrid.Children.Add(txt);
                    chipGrid.Children.Add(btn);
                    chip.Child = chipGrid;
                    existingPanel.Children.Add(chip);
                }
            }
            innerPanel.Children.Add(existingPanel);

            var addCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
            addCombo.Items.Add("");
            foreach (var o in options) addCombo.Items.Add(o);
            addCombo.SelectionChanged += (_, _) =>
            {
                if (addCombo.SelectedItem is string s && s.Length > 0)
                {
                    var list = current ?? new List<string>();
                    if (!list.Contains(s))
                    {
                        list.Add(s);
                        onCommit(new List<string>(list));
                        BuildInspector();
                    }
                    // Keep the selected item visible (don't reset)
                }
            };
            innerPanel.Children.Add(addCombo);

            panel.Children.Add(innerPanel);
        }

        private static List<string> ParseStringList(string input)
        {
            return input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .Where(s => s.Length > 0)
                          .ToList();
        }

        private static List<string> ParseFactionArgs(string input)
        {
            return input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .Where(s => s.Length > 0)
                          .ToList();
        }

        private static void UpdateFactionArgsBox(TextBox box, List<string> args)
        {
            box.Text = args is { Count: > 0 } ? string.Join(", ", args) : "";
        }

        private void AddIntListField(string label, List<int>? value, Action<List<int>> onCommit, Panel panel)
        {
            AddSectionLabel(label, panel);
            var text = value is { Count: > 0 } ? string.Join(", ", value) : "";
            var box = new TextBox { Text = text, Margin = new Thickness(0, 0, 0, 4), MinHeight = 40, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            box.LostFocus += (_, _) => onCommit(ParseIntList(box.Text));
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter && !e.Handled) { onCommit(ParseIntList(box.Text)); } };
            panel.Children.Add(box);
        }

        private static List<int> ParseIntList(string input)
        {
            return input.Split(new[] { ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .Where(s => s.Length > 0)
                          .Select(s =>
                          {
                              int.TryParse(s, out var v);
                              return v;
                          })
                          .ToList();
        }

        private void AddBiomeSelector(string label, BiomeSelector? current, Action<BiomeSelector?> onCommit, Panel panel)
        {
            AddSectionLabel(label, panel);

            var ensured = current ?? new BiomeSelector();

            AddSectionLabel(L("S.EC.BiomeType"), panel);
            var typeCombo = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var o in KnownValues.SelectorTypes) typeCombo.Items.Add(o);
            typeCombo.Text = ensured.Type ?? "";
            if (ensured.Type is not null && typeCombo.Items.Contains(ensured.Type))
                typeCombo.SelectedItem = ensured.Type;
            typeCombo.LostFocus += (_, _) => { ensured.Type = typeCombo.Text.Trim(); onCommit(ensured); };
            typeCombo.SelectionChanged += (_, _) =>
            {
                if (typeCombo.SelectedItem is string s)
                {
                    ensured.Type = s;
                    onCommit(ensured);
                    UpdateBiomeArgsVisibility(panel, s);
                }
            };
            panel.Children.Add(typeCombo);

            // Args box (declared early for use in available args combo)
            var argsText = ensured.Args is { Count: > 0 } ? string.Join(", ", ensured.Args) : "";
            var argsBox = new TextBox { Text = argsText, Margin = new Thickness(0, 0, 0, 4), MinHeight = 30, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };

            // Available args dropdown (visible only for FromList)
            var availableArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = ensured.Type == "FromList" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel(L("S.EC.FromListAvailableArgs"), availableArgsPanel);
            var availableArgsCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var arg in KnownValues.FromListBiomeArgs) availableArgsCombo.Items.Add(arg);
            availableArgsCombo.SelectionChanged += (_, _) =>
            {
                if (availableArgsCombo.SelectedItem is string selectedArg)
                {
                    if (ensured.Args == null) ensured.Args = [];
                    if (!ensured.Args.Contains(selectedArg))
                    {
                        ensured.Args.Add(selectedArg);
                        UpdateArgsBox(argsBox, ensured.Args);
                        onCommit(ensured);
                    }
                }
            };
            availableArgsPanel.Children.Add(availableArgsCombo);
            panel.Children.Add(availableArgsPanel);

            var argsSection = new StackPanel { Margin = new Thickness(0) };
            AddSectionLabel(L("S.EC.BiomeArgs"), argsSection);
            argsBox.LostFocus += (_, _) =>
            {
                ensured.Args = ParseStringList(argsBox.Text);
                onCommit(ensured);
            };
            argsSection.Children.Add(argsBox);
            panel.Children.Add(argsSection);

            // Store reference so UpdateBiomeArgsVisibility can toggle both panels
            availableArgsPanel.Tag = argsSection;
        }

        private static void UpdateBiomeArgsVisibility(Panel panel, string selectorType)
        {
            foreach (var child in panel.Children)
            {
                if (child is StackPanel sp && sp.Tag is StackPanel argsSection)
                {
                    bool isFromList = selectorType == "FromList";
                    sp.Visibility = isFromList ? Visibility.Visible : Visibility.Collapsed;
                    argsSection.Visibility = isFromList ? Visibility.Collapsed : Visibility.Visible;
                    break;
                }
            }
        }

        private static void UpdateArgsBox(TextBox argsBox, List<string> args)
        {
            argsBox.Text = string.Join(", ", args);
        }



        private void AddRoadList(string label, List<Road>? value, Action<List<Road>> onCommit, Panel panel)
        {
            AddSectionLabel(label, panel);
            var count = value?.Count ?? 0;
            var box = new TextBox
            {
                Text = count > 0
                    ? string.Join("\n", value!.Select((r, i) => $"[{i}] type={r.Type ?? "?"}, from={r.From?.Type ?? "?"}/{FormatArgs(r.From?.Args)}, to={r.To?.Type ?? "?"}/{FormatArgs(r.To?.Args)}"))
                    : "",
                Margin = new Thickness(0, 0, 0, 4),
                MinHeight = 40,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };
            box.LostFocus += (_, _) => onCommit(ParseRoadList(box.Text));
            panel.Children.Add(box);
        }

        private static string FormatArgs(List<string>? args) => args is { Count: > 0 } ? string.Join(",", args) : "-";

        private static List<Road> ParseRoadList(string input)
        {
            var roads = new List<Road>();
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                var road = new Road();
                var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var key = kv[0].Trim();
                    var val = kv[1].Trim();
                    switch (key)
                    {
                        case "type": road.Type = val; break;
                        case "from":
                            road.From = ParseRoadEndpoint(val);
                            break;
                        case "to":
                            road.To = ParseRoadEndpoint(val);
                            break;
                    }
                }
                roads.Add(road);
            }
            return roads;
        }

        private static RoadEndpoint ParseRoadEndpoint(string input)
        {
            var ep = new RoadEndpoint();
            var parts = input.Split(':', 2);
            if (parts.Length >= 1) ep.Type = parts[0].Trim();
            if (parts.Length >= 2)
                ep.Args = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            return ep;
        }

        /// <summary>
        /// Automatically generates connection names for connections without names.
        /// Uses the pattern "{FromZone}-{ToZone}" for missing names.
        /// </summary>
        private void SyncConnectionRoadFlags(Dictionary<string, List<Road>>? preRoads = null)
        {
            foreach (var zone in Zones)
            {
                var roads = preRoads != null && preRoads.TryGetValue(zone.Name, out var pr) ? pr : zone.Roads;
                if (roads == null) continue;
                foreach (var road in roads)
                {
                    var connName = road.From?.Type == "Connection" ? road.From.Args?.FirstOrDefault()
                                 : road.To?.Type == "Connection" ? road.To.Args?.FirstOrDefault()
                                 : null;
                    if (connName == null) continue;
                    var conn = Connections.FirstOrDefault(c => c.Name == connName);
                    if (conn != null) conn.Road = true;
                }
            }
        }

        private void SyncConnectionPlacementArgs()
        {
            foreach (var zone in Zones)
            {
                if (zone.MainObjects == null || zone.Name == null) continue;
                var validConns = Connections.Where(c => c.Name != null).ToHashSet();

                // Two-pass approach:
                // Pass 1: validate and collect already-occupied connection names
                var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var reassignIndices = new List<int>();

                for (int i = 0; i < zone.MainObjects.Count; i++)
                {
                    var mo = zone.MainObjects[i];
                    if (mo.Placement != "Connection")
                    {
                        // Non-Connection MO at index 0 (Center) still "occupies" its road connections
                        // via Part C — but that's handled elsewhere; nothing to do here.
                        continue;
                    }

                    if (mo.PlacementArgs is { Count: > 0 })
                    {
                        var curr = mo.PlacementArgs[0];
                        bool isConn = validConns.Any(c => string.Equals(c.Name, curr, StringComparison.OrdinalIgnoreCase));
                        bool involvesZone = isConn && validConns.Any(c => string.Equals(c.Name, curr, StringComparison.OrdinalIgnoreCase)
                            && (string.Equals(c.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(c.To, zone.Name, StringComparison.OrdinalIgnoreCase)));
                        bool isMoIndex = !isConn && int.TryParse(curr, out int idx) && idx >= 0 && idx < zone.MainObjects.Count && idx != i;

                        if (isMoIndex) { /* MO index valid, doesn't occupy a connection name */ continue; }

                        if (isConn && involvesZone)
                        {
                            // Upgrade non-road to road variant if available (between same zone pair)
                            var conn = Connections.FirstOrDefault(c => string.Equals(c.Name, curr, StringComparison.OrdinalIgnoreCase));
                            if (conn != null && conn.Road != true)
                            {
                                var roadConn = Connections.FirstOrDefault(c => c.Road == true
                                    && ((string.Equals(c.From, conn.From, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(c.To, conn.To, StringComparison.OrdinalIgnoreCase))
                                     || (string.Equals(c.From, conn.To, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(c.To, conn.From, StringComparison.OrdinalIgnoreCase))));
                                if (roadConn?.Name != null)
                                {
                                    mo.PlacementArgs = new List<string> { roadConn.Name };
                                    occupied.Add(roadConn.Name);
                                    continue;
                                }
                            }

                            // Connection is valid and involves zone
                            if (!occupied.Contains(curr))
                            {
                                // Unique target — keep and mark as occupied
                                occupied.Add(curr);
                                continue;
                            }
                            // Duplicate target — reassign
                        }
                    }

                    reassignIndices.Add(i);
                }

                // Pass 2: reassign duplicates and invalid MOs
                foreach (var i in reassignIndices)
                {
                    var mo = zone.MainObjects[i];
                    var best = GetAutoPlacementArgs(zone, occupied);
                    if (best != null)
                    {
                        mo.PlacementArgs = new List<string> { best };
                        occupied.Add(best);
                    }
                    else
                        mo.PlacementArgs = null;
                }
            }
        }

        /// <summary>
        /// Returns the best auto-assigned PlacementArgs[0] for a Connection-placed MO.
        /// Priority:
        ///   1) Connections to zones that have no castle Connection-placed MOs
        ///   2) Connections with Road==true
        ///   3) Even distribution (least-used connection first)
        /// Returns null if no suitable connection exists.
        /// </summary>
        private string? GetAutoPlacementArgs(Zone zone, HashSet<string>? excludeUsed = null)
        {
            if (zone.Name == null) return null;
            var zoneConns = Connections
                .Where(c => !string.IsNullOrWhiteSpace(c.Name)
                         && (string.Equals(c.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(c.To, zone.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (zoneConns.Count == 0) return null;

            // Exclude already-occupied connections from consideration
            if (excludeUsed != null && excludeUsed.Count > 0)
                zoneConns = zoneConns.Where(c => !excludeUsed.Contains(c.Name)).ToList();
            if (zoneConns.Count == 0) return null;

            // Count how many Connection-placed MOs in this zone already reference each connection name
            var usedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (zone.MainObjects != null)
            {
                foreach (var mo in zone.MainObjects)
                {
                    if (mo.Placement == "Connection" && mo.PlacementArgs is { Count: > 0 })
                        usedCounts[mo.PlacementArgs[0]] = usedCounts.GetValueOrDefault(mo.PlacementArgs[0]) + 1;
                }
            }

            static string OtherZone(Connection c, string zoneName) =>
                string.Equals(c.From, zoneName, StringComparison.OrdinalIgnoreCase) ? c.To : c.From;

            // P1: Road connections to zones without castle Connection-placed MO
            var p1 = zoneConns
                .Where(c => c.Road == true && !HasCastleConnection(OtherZone(c, zone.Name)))
                .OrderBy(c => usedCounts.GetValueOrDefault(c.Name, 0))
                .ThenBy(c => c.Name)
                .ToList();
            if (p1.Count > 0) return p1[0].Name;

            // P2: Road connections (any zone)
            var p2 = zoneConns
                .Where(c => c.Road == true)
                .OrderBy(c => usedCounts.GetValueOrDefault(c.Name, 0))
                .ThenBy(c => c.Name)
                .ToList();
            if (p2.Count > 0) return p2[0].Name;

            // P3: Non-road connections to zones without castle Connection-placed MO
            var p3 = zoneConns
                .Where(c => c.Road != true && !HasCastleConnection(OtherZone(c, zone.Name)))
                .OrderBy(c => usedCounts.GetValueOrDefault(c.Name, 0))
                .ThenBy(c => c.Name)
                .ToList();
            if (p3.Count > 0) return p3[0].Name;

            // P4: any remaining (even distribution)
            var p4 = zoneConns
                .OrderBy(c => usedCounts.GetValueOrDefault(c.Name, 0))
                .ThenBy(c => c.Name)
                .ToList();
            return p4.Count > 0 ? p4[0].Name : null;
        }

        private bool HasCastleConnection(string zoneName)
        {
            // Check if any OTHER zone has a Connection MO targeting this zone
            return Zones.Any(z => !string.Equals(z.Name, zoneName, StringComparison.OrdinalIgnoreCase)
                && z.MainObjects?.Any(mo => mo.Placement == "Connection"
                    && mo.PlacementArgs is { Count: > 0 }
                    && Connections.Any(c => string.Equals(c.Name, mo.PlacementArgs[0], StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(c.From, zoneName, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(c.To, zoneName, StringComparison.OrdinalIgnoreCase)))) == true);
        }

        private void RebuildConnectionRoads()
        {
            foreach (var zone in Zones)
            {
                if (zone.MainObjects == null || zone.MainObjects.Count <= 1) continue;
                zone.Roads ??= [];

                // Part C: Center-placed MO → roads to all road connections involving this zone
                if (zone.MainObjects.Any(mo => mo.Placement == "Center"))
                {
                    foreach (var conn in Connections)
                    {
                        if (conn.Road == true && conn.Name != null
                            && (string.Equals(conn.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(conn.To, zone.Name, StringComparison.OrdinalIgnoreCase)))
                            AddUniqueRoad(zone, "MainObject", ["0"], "Connection", [conn.Name]);
                    }
                }

                for (int i = 0; i < zone.MainObjects.Count; i++)
                {
                    var mo = zone.MainObjects[i];
                    if (mo.Placement != "Connection" || mo.PlacementArgs is not { Count: > 0 }) continue;
                    string connOrIndex = mo.PlacementArgs[0];

                    // Detect old connection from existing roads before removal
                    var oldConn = zone.Roads
                        .Where(r => r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == i.ToString()
                                 && r.To?.Type == "Connection" && r.To.Args is { Count: > 0 })
                        .Select(r => r.To.Args[0])
                        .FirstOrDefault()
                        ?? zone.Roads
                            .Where(r => r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == i.ToString()
                                     && r.From?.Type == "Connection" && r.From.Args is { Count: > 0 })
                            .Select(r => r.From.Args[0])
                            .FirstOrDefault();

                    // Remove stale roads for this MO that don't go to the correct target
                    zone.Roads.RemoveAll(r =>
                    {
                        bool fromThis = r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == i.ToString();
                        bool toThis = r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == i.ToString();
                        bool fromConn = r.From?.Type == "Connection" && r.From.Args?.FirstOrDefault() == connOrIndex;
                        bool toConn = r.To?.Type == "Connection" && r.To.Args?.FirstOrDefault() == connOrIndex;
                        bool fromTargetMo = r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == connOrIndex;
                        bool toTargetMo = r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == connOrIndex;
                        if (fromThis) return !toConn && !toTargetMo;
                        if (toThis) return !fromConn && !fromTargetMo;
                        return false;
                    });

                    // If target changed and no other MO still uses the old one, clean up MO[0] roads
                    if (oldConn != null && oldConn != connOrIndex)
                    {
                        bool oldStillUsed = zone.MainObjects
                            .Skip(1).Any(m => m.Placement == "Connection" && m.PlacementArgs is { Count: > 0 }
                                          && m.PlacementArgs[0] == oldConn);
                        if (!oldStillUsed)
                        {
                            zone.Roads.RemoveAll(r =>
                            {
                                bool fromM0 = r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == "0"
                                           && r.To?.Type == "Connection" && r.To.Args?.FirstOrDefault() == oldConn;
                                bool toM0 = r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == "0"
                                         && r.From?.Type == "Connection" && r.From.Args?.FirstOrDefault() == oldConn;
                                bool fromM0toMo = r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == "0"
                                               && r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == oldConn;
                                bool toM0fromMo = r.To?.Type == "MainObject" && r.To.Args?.FirstOrDefault() == "0"
                                               && r.From?.Type == "MainObject" && r.From.Args?.FirstOrDefault() == oldConn;
                                return fromM0 || toM0 || fromM0toMo || toM0fromMo;
                            });
                        }
                    }

                    // Check if target is a connection name (before int.TryParse to avoid numeric collision)
                    bool isConnName = Connections.Any(c => string.Equals(c.Name, connOrIndex, StringComparison.OrdinalIgnoreCase));
                    // Part E: if target is an MO index (not a connection name), generate MO → MO roads
                    if (!isConnName && int.TryParse(connOrIndex, out int targetIdx) && targetIdx >= 0 && targetIdx < zone.MainObjects.Count && targetIdx != i)
                    {
                        if (i == 0)
                            AddUniqueRoad(zone, "MainObject", ["0"], "MainObject", [connOrIndex]);
                        AddUniqueRoad(zone, "MainObject", [i.ToString()], "MainObject", [connOrIndex]);
                    }
                    else
                    {
                        if (i == 0)
                            AddUniqueRoad(zone, "MainObject", ["0"], "Connection", [connOrIndex]);
                        AddUniqueRoad(zone, "MainObject", [i.ToString()], "Connection", [connOrIndex]);
                    }
                }
            }
        }

        private void AutoGenerateConnectionNames()
        {
            foreach (var conn in Connections)
            {
                if (string.IsNullOrWhiteSpace(conn.Name))
                {
                    // Generate name from From/To zones
                    string autoName = GenerateConnectionName(conn.From, conn.To);
                    conn.Name = autoName;
                }
            }
        }

        /// <summary>
        /// Generates a unique connection name based on From/To zones.
        /// Pattern: "{FromZone}-{ToZone}" with proper capitalization.
        /// </summary>
        private static string GenerateConnectionName(string fromZone, string toZone)
        {
            if (string.IsNullOrWhiteSpace(fromZone) || string.IsNullOrWhiteSpace(toZone))
                return string.Empty;

            // Capitalize first letter of each part, remove any extra whitespace
            fromZone = fromZone.Trim().Trim().ToUpperInvariant().Replace(' ', '_');
            toZone = toZone.Trim().Trim().ToUpperInvariant().Replace(' ', '_');

            return $"{fromZone}-{toZone}";
        }

        /// <summary>
        /// Automatically generates roads for zones connected by a connection with Road=true.
        /// Mirrors the logic from TemplateGenerator:
        /// - Zones with castles: MainObject[0] -> Connection
        /// - Zones without castles: Connection -> Connection (star topology)
        /// </summary>
        private void AutoGenerateRoadsForConnection(Connection conn)
        {
            if (conn.Road != true) return;

            string connName = conn.Name ?? $"{conn.From}-{conn.To}";

            // Find connected zones
            var fromZone = Zones.FirstOrDefault(z => z.Name == conn.From);
            var toZone = Zones.FirstOrDefault(z => z.Name == conn.To);

            if (fromZone != null) AddRoadToZone(fromZone, connName);
            if (toZone != null) AddRoadToZone(toZone, connName);
        }

        /// <summary>
        /// Adds a road from the zone's main object (or connection anchor) to the given connection.
        /// </summary>
        private static void AddUniqueRoad(Zone zone, string fromType, string[] fromArgs, string toType, string[] toArgs)
        {
            zone.Roads ??= [];
            if (zone.Roads.Any(r =>
                r.From?.Type == fromType &&
                r.From?.Args?.FirstOrDefault() == fromArgs?.FirstOrDefault() &&
                r.To?.Type == toType &&
                r.To?.Args?.FirstOrDefault() == toArgs?.FirstOrDefault()))
                return;
            zone.Roads.Add(new Road
            {
                Type = "Stone",
                From = new RoadEndpoint { Type = fromType, Args = [.. fromArgs] },
                To = new RoadEndpoint { Type = toType, Args = [.. toArgs] }
            });
        }

        private void AddRoadToZone(Zone zone, string connectionName)
        {
            zone.Roads ??= [];

            int castleCount = zone.MainObjects?.Count(o => o.Type == "City" || o.Type == "AbandonedOutpost") ?? 0;

            Road newRoad;
            if (castleCount > 0)
            {
                // Zone with castle: MainObject[0] -> Connection
                newRoad = new Road
                {
                    From = new RoadEndpoint { Type = "MainObject", Args = ["0"] },
                    To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
                };
            }
            else
            {
                // Zone without castle: use star topology from first existing connection
                var existingConn = zone.Roads
                    .Select(r => r.From?.Type == "Connection" ? r.From.Args?.FirstOrDefault() : null)
                    .FirstOrDefault(n => n != null)
                    ?? zone.Roads
                        .Select(r => r.To?.Type == "Connection" ? r.To.Args?.FirstOrDefault() : null)
                        .FirstOrDefault(n => n != null);

                if (existingConn != null)
                {
                    newRoad = new Road
                    {
                        From = new RoadEndpoint { Type = "Connection", Args = [existingConn] },
                        To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
                    };
                }
                else
                {
                    // First connection: self-referencing loop
                    newRoad = new Road
                    {
                        From = new RoadEndpoint { Type = "Connection", Args = [connectionName] },
                        To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
                    };
                }
            }

            // Avoid duplicates
            bool exists = zone.Roads.Any(r =>
                r.From?.Type == newRoad.From?.Type &&
                r.From?.Args?.FirstOrDefault() == newRoad.From?.Args?.FirstOrDefault() &&
                r.To?.Type == newRoad.To?.Type &&
                r.To?.Args?.FirstOrDefault() == newRoad.To?.Args?.FirstOrDefault()
            );

            if (!exists)
            {
                zone.Roads.Add(newRoad);
            }
        }

        private void RenameZone(Zone z, string newName)
        {
            newName = newName.Trim();
            if (newName.Length == 0 || newName == z.Name) return;
            if (Zones.Any(zz => zz != z && string.Equals(zz.Name, newName, StringComparison.Ordinal)))
            {
                UpdateStatus(L("S.EC.NameTaken", newName));
                return;
            }
            string old = z.Name;
            // Re-point connections + position map.
            foreach (var c in Connections)
            {
                if (c.From == old) c.From = newName;
                if (c.To == old)   c.To = newName;
            }
            if (_positions.Remove(old, out var pt)) _positions[newName] = pt;
            z.Name = newName;
            MarkDirty();
            RebuildGraph();
            BuildInspector();
        }

        private void RefreshNode(Zone z)
        {
            if (!_positions.TryGetValue(z.Name, out var _)) return;
            RebuildGraph();
        }

        // ── Mouse: pan / zoom / drag / connect ───────────────────────────────────────

        private void CanvasHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = FindTagged(e.OriginalSource as DependencyObject);

            if (hit is Zone z)
            {
                if (_connectMode)
                {
                    HandleConnectClick(z);
                    e.Handled = true;
                    return;
                }
                Select(z);
                _dragZone = z;
                _movedWhileDragging = false;
                var click = e.GetPosition(GraphCanvas);
                _dragGrab = click - _positions[z.Name];
                CanvasHost.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (hit is Connection c)
            {
                Select(c);
                InspectorTabs.SelectedItem = TabMain;
                e.Handled = true;
                return;
            }

            if (e.ClickCount == 2 && !_connectMode)
            {
                AddZoneAt(e.GetPosition(GraphCanvas));
                e.Handled = true;
                return;
            }

            Select(null);
            _isPanning = true;
            _panStartScreen = e.GetPosition(CanvasHost);
            _panStartTx = CanvasTranslate.X;
            _panStartTy = CanvasTranslate.Y;
            CanvasHost.CaptureMouse();
        }

        private void CanvasHost_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragZone is not null && e.LeftButton == MouseButtonState.Pressed)
            {
                var p = e.GetPosition(GraphCanvas) - _dragGrab;
                p = SnapToGrid(p);
                _positions[_dragZone.Name] = p;
                _movedWhileDragging = true;
                RepositionZone(_dragZone, p);
                return;
            }
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var now = e.GetPosition(CanvasHost);
                CanvasTranslate.X = _panStartTx + (now.X - _panStartScreen.X);
                CanvasTranslate.Y = _panStartTy + (now.Y - _panStartScreen.Y);
            }
        }

        private void CanvasHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragZone is not null && _movedWhileDragging)
                UpdateStatus(L("S.EC.ZoneMoved", _dragZone.Name));
            _dragZone = null;
            _isPanning = false;
            if (CanvasHost.IsMouseCaptured) CanvasHost.ReleaseMouseCapture();
        }

        /// <summary>Moves an already-drawn node + its label + incident edges without a full rebuild (smooth drag).</summary>
        private void RepositionZone(Zone z, Point p)
        {
            double r = NodeRadius(z);
            if (_nodeShapes.TryGetValue(z.Name, out var shape))
            {
                Canvas.SetLeft(shape, p.X - r);
                Canvas.SetTop(shape, p.Y - r);
            }
            if (_nodeLabels.TryGetValue(z.Name, out var label))
                PlaceInnerLabel(label, p);

            foreach (var (line, conn) in _edges)
            {
                if (conn.From != z.Name && conn.To != z.Name) continue;

                if (!_positions.TryGetValue(conn.From, out var fp) ||
                    !_positions.TryGetValue(conn.To,   out var tp)) continue;

                double dx = tp.X - fp.X, dy = tp.Y - fp.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1) continue;

                double ux = dx / len, uy = dy / len;
                double px = -uy, py = ux;
                double offset = _edgeFanOffsets.GetValueOrDefault(conn, 0);

                line.X1 = fp.X + px * offset;
                line.Y1 = fp.Y + py * offset;
                line.X2 = tp.X + px * offset;
                line.Y2 = tp.Y + py * offset;
            }
        }

        private void CanvasHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
            double newScale = Math.Clamp(CanvasScale.ScaleX * factor, 0.2, 6.0);
            Point host   = e.GetPosition(CanvasHost);
            Point local  = e.GetPosition(GraphCanvas);
            CanvasScale.ScaleX = CanvasScale.ScaleY = newScale;
            CanvasTranslate.X = host.X - local.X * newScale;
            CanvasTranslate.Y = host.Y - local.Y * newScale;
            TxtZoomLabel.Text = $"{newScale * 100:0}%";
        }

        private static object? FindTagged(DependencyObject? src)
        {
            while (src is not null)
            {
                if (src is FrameworkElement fe)
                {
                    if (fe.Tag is Zone z) return z;
                    if (fe.Tag is Connection c) return c;
                }
                src = VisualTreeHelper.GetParent(src);
            }
            return null;
        }

        // ── Connect mode ─────────────────────────────────────────────────────────────

        private void HandleConnectClick(Zone z)
        {
            if (_connectFrom is null)
            {
                _connectFrom = z;
                UpdateStatus(L("S.EC.ConnectFrom", z.Name));
                UpdateSelectionVisuals();
                return;
            }
            if (ReferenceEquals(_connectFrom, z))
            {
                _connectFrom = null;
                UpdateStatus(L("S.EC.ConnectCancelled"));
                UpdateSelectionVisuals();
                return;
            }
            string connType = "Direct";
            string baseName = $"{connType}-{_connectFrom.Name}-{z.Name}";
            string autoName = baseName;
            for (int suffix = 2; Connections.Any(c => string.Equals(c.Name, autoName, StringComparison.Ordinal)); suffix++)
                autoName = $"{baseName}-{suffix}";
            var conn = new Connection
            {
                Name = autoName, From = _connectFrom.Name, To = z.Name, ConnectionType = connType,
                SimTurnSquad = true,
            };
            if (connType == "Proximity") conn.Length = 0.94;
            Connections.Add(conn);
            UpdateStatus(L("S.EC.ConnAdded", conn.From, conn.To));
            _connectFrom = null;
            _connectMode = false;
            BtnConnectMode.Background = null;
            MarkDirty();
            RebuildGraph();
            Select(conn);
            InspectorTabs.SelectedItem = TabMain;
        }

        // ── Toolbar handlers ──────────────────────────────────────────────────────────

        private void BtnConnectMode_Click(object sender, RoutedEventArgs e)
        {
            _connectMode = !_connectMode;
            _connectFrom = null;
            BtnConnectMode.Background = _connectMode ? new SolidColorBrush(Color.FromRgb(60, 50, 90)) : null;
            UpdateStatus(_connectMode ? L("S.EC.ConnectModeOn") : L("S.EC.ConnectModeOff"));
            UpdateSelectionVisuals();
        }

        private void BtnGridSnap_Click(object sender, RoutedEventArgs e)
        {
            _gridSnap = !_gridSnap;
            BtnGridSnap.Background = _gridSnap
                ? new SolidColorBrush(Color.FromRgb(40, 60, 40))
                : null;
            UpdateStatus(_gridSnap ? L("S.EC.GridSnapOn") : L("S.EC.GridSnapOff"));
        }

        /// <summary>Snaps a point to the nearest grid intersection.</summary>
        private Point SnapToGrid(Point p)
        {
            if (!_gridSnap) return p;
            return new Point(
                Math.Round(p.X / GridSize) * GridSize,
                Math.Round(p.Y / GridSize) * GridSize
            );
        }

        private void BtnAddZone_Click(object sender, RoutedEventArgs e) => AddZoneAt(ViewportCenterInCanvas());

        /// <summary>Adds a new zone at the given canvas position and selects it.</summary>
        private void AddZoneAt(Point pos)
        {
            string name = UniqueZoneName();
            var z = new Zone
            {
                Name = name,
                Size = 1.0,
                Layout = "zone_layout_sides",
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.0,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = 0,
                GuardedContentPool = ["classic_template_pool_random_t2_item"],
                UnguardedContentPool = ["classic_template_pool_random_unguarded_t2_item"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_poor"],
                MandatoryContent = [],
                ContentCountLimits = [],
                GuardedContentValue = 150000,
                GuardedContentValuePerArea = 1000,
                UnguardedContentValue = 35000,
                UnguardedContentValuePerArea = 1000,
                ResourcesValue = 3000,
                ResourcesValuePerArea = 100,
                MainObjects = [],
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
            };
            Zones.Add(z);
            _positions[name] = pos;
            MarkDirty();
            RebuildGraph();
            Select(z);
            UpdateStatus(L("S.EC.ZoneAdded", name));
        }

        private string UniqueZoneName()
        {
            for (int i = 1; ; i++)
            {
                string n = $"Zone-{i}";
                if (!Zones.Any(z => string.Equals(z.Name, n, StringComparison.Ordinal))) return n;
            }
        }

        private Point ViewportCenterInCanvas()
        {
            double scale = CanvasScale.ScaleX <= 0 ? 1 : CanvasScale.ScaleX;
            double cx = (CanvasHost.ActualWidth / 2 - CanvasTranslate.X) / scale;
            double cy = (CanvasHost.ActualHeight / 2 - CanvasTranslate.Y) / scale;
            return new Point(cx, cy);
        }

        // ── Keyboard & zoom buttons ──────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Don't hijack keys while typing in the inspector.
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox)
                return;

            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                CopySelectedZone();
                e.Handled = true;
            }
            else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                PasteCopiedZone();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                BtnDelete_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_connectMode)
                {
                    _connectMode = false; _connectFrom = null; BtnConnectMode.Background = null;
                    UpdateSelectionVisuals();
                    UpdateStatus(L("S.EC.ConnectModeOff"));
                }
                else Select(null);
                e.Handled = true;
            }
        }

        /// <summary>Experimental: export the zone graph (at natural scale, with grid + labels) to a PNG.</summary>
        private void BtnExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = L("S.EC.ExportTitle"), Filter = "PNG (*.png)|*.png",
                DefaultExt = ".png", FileName = "zone-graph.png",
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                // Render the canvas at 1:1 (ignore the current zoom/pan), then restore the view.
                double sx = CanvasScale.ScaleX, sy = CanvasScale.ScaleY, tx = CanvasTranslate.X, ty = CanvasTranslate.Y;
                CanvasScale.ScaleX = CanvasScale.ScaleY = 1; CanvasTranslate.X = 0; CanvasTranslate.Y = 0;
                GraphCanvas.UpdateLayout();

                int w = (int)GraphCanvas.Width, h = (int)GraphCanvas.Height;
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(GraphCanvas);

                CanvasScale.ScaleX = sx; CanvasScale.ScaleY = sy; CanvasTranslate.X = tx; CanvasTranslate.Y = ty;

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = File.Create(dlg.FileName)) encoder.Save(fs);
                UpdateStatus(L("S.EC.Exported", IOPath.GetFileName(dlg.FileName)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.EC.SaveErr", ex.Message), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)  => ZoomBy(1.2);
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.2);

        /// <summary>Zooms by a factor around the viewport centre.</summary>
        private void ZoomBy(double factor)
        {
            double oldScale = CanvasScale.ScaleX <= 0 ? 1 : CanvasScale.ScaleX;
            double newScale = Math.Clamp(oldScale * factor, 0.2, 6.0);
            double cx = CanvasHost.ActualWidth / 2, cy = CanvasHost.ActualHeight / 2;
            double localX = (cx - CanvasTranslate.X) / oldScale;
            double localY = (cy - CanvasTranslate.Y) / oldScale;
            CanvasScale.ScaleX = CanvasScale.ScaleY = newScale;
            CanvasTranslate.X = cx - localX * newScale;
            CanvasTranslate.Y = cy - localY * newScale;
            TxtZoomLabel.Text = $"{newScale * 100:0}%";
        }

        public void RemoveRoadReferences(string connectionName)
        {
            foreach (var zone in Zones)
            {
                if (zone.Roads is null) continue;
                zone.Roads.RemoveAll(r =>
                    (r.From?.Type == "Connection" && r.From.Args?.Contains(connectionName) == true) ||
                    (r.To?.Type == "Connection" && r.To.Args?.Contains(connectionName) == true));
            }
        }

        private static void AdjustRoadIndicesAfterRemoval(Zone zone, int removedIndex)
        {
            if (zone.Roads is null) return;
            var removedStr = removedIndex.ToString();
            zone.Roads.RemoveAll(r =>
                (r.From?.Type == "MainObject" && r.From.Args?.Contains(removedStr) == true) ||
                (r.To?.Type == "MainObject" && r.To.Args?.Contains(removedStr) == true));
            foreach (var road in zone.Roads)
            {
                if (road.From?.Type == "MainObject" && road.From.Args is not null)
                    ShiftArgs(road.From.Args, removedIndex);
                if (road.To?.Type == "MainObject" && road.To.Args is not null)
                    ShiftArgs(road.To.Args, removedIndex);
            }
        }

        private static void ShiftArgs(List<string> args, int removedIndex)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (int.TryParse(args[i], out int idx) && idx > removedIndex)
                    args[i] = (idx - 1).ToString();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected is Zone z)
            {
                var removed = Connections.Where(c => c.From == z.Name || c.To == z.Name).ToList();
                foreach (var conn in removed)
                    RemoveRoadReferences(conn.Name ?? $"{conn.From}-{conn.To}");
                int removedConns = Connections.RemoveAll(c => c.From == z.Name || c.To == z.Name);
                Zones.Remove(z);
                _positions.Remove(z.Name);
                MarkDirty();
                RebuildGraph();
                Select(null);
                UpdateStatus(L("S.EC.ZoneDeleted", z.Name, removedConns));
            }
            else if (_selected is Connection c)
            {
                RemoveRoadReferences(c.Name ?? $"{c.From}-{c.To}");
                Connections.Remove(c);
                MarkDirty();
                RebuildGraph();
                Select(null);
                UpdateStatus(L("S.EC.ConnDeleted", c.From, c.To));
            }
            else UpdateStatus(L("S.EC.NothingToDelete"));
        }

        /// <summary>Validates and fixes non-relevant values on a zone (called after paste).</summary>
        private void ValidateZone(Zone z)
        {
            if (z.MainObjects != null)
            {
                for (int i = 0; i < z.MainObjects.Count; i++)
                {
                    var mo = z.MainObjects[i];
                    // Fix invalid placement types
                    if (!KnownValues.MainObjectPlacements.Contains(mo.Placement ?? ""))
                        mo.Placement = "Uniform";
                    // Clear Spawn for non-Spawn types
                    if (mo.Type != "Spawn")
                        mo.Spawn = null;
                    // Clear Owner for non-City types
                    if (mo.Type != "City")
                        mo.Owner = null;
                    // If owner or spawn is set, ensure RemoveGuardIfHasOwner and zero guards
                    if (mo.Owner != null || mo.Spawn != null)
                    {
                        mo.RemoveGuardIfHasOwner = true;
                        mo.GuardChance = null;
                        mo.GuardValue = null;
                        mo.GuardWeeklyIncrement = null;
                    }
                }
            }
            // Validate roads — remove any that reference non-existent objects
            if (z.Roads != null)
            {
                z.Roads.RemoveAll(r =>
                {
                    bool fromBad = r.From?.Type == "MainObject" && (r.From.Args == null || r.From.Args.Count == 0);
                    bool toBad = r.To?.Type == "MainObject" && (r.To.Args == null || r.To.Args.Count == 0);
                    if (fromBad || toBad)
                        return true;
                    int fromIdx = r.From?.Args is { Count: > 0 } && int.TryParse(r.From.Args[0], out var fi) ? fi : -1;
                    int toIdx = r.To?.Args is { Count: > 0 } && int.TryParse(r.To.Args[0], out var ti) ? ti : -1;
                    bool fromValid = fromIdx < 0 || (fromIdx >= 0 && fromIdx < (z.MainObjects?.Count ?? 0));
                    bool toValid = toIdx < 0 || (toIdx >= 0 && toIdx < (z.MainObjects?.Count ?? 0));
                    return !fromValid || !toValid;
                });
            }
        }

        private void CopySelectedZone()
        {
            if (_selected is not Zone z) { UpdateStatus(L("S.EC.SelectZoneFirst")); return; }
            var json = JsonSerializer.Serialize(z, JsonOptions);
            _copiedZone = JsonSerializer.Deserialize<Zone>(json, JsonOptions);
            UpdateStatus(L("S.EC.ZoneCopied", z.Name));
        }

        private void PasteCopiedZone()
        {
            if (_copiedZone is null) { UpdateStatus(L("S.EC.NothingCopied")); return; }
            var json = JsonSerializer.Serialize(_copiedZone, JsonOptions);
            var copy = JsonSerializer.Deserialize<Zone>(json, JsonOptions)!;
            copy.Name = UniqueZoneName();
            var pos = _positions.Count > 0
                ? new Point(_positions.Values.Min(p => p.X) + 60, _positions.Values.Min(p => p.Y) + 60)
                : ViewportCenterInCanvas();
            ValidateZone(copy);

            // Check for duplicate Owner/Spawn across zones (unified ascending Player1→Player8)
            var usedPlayers = GetUsedPlayers();

            bool hadConflict = false;
            if (copy.MainObjects != null)
            {
                foreach (var mo in copy.MainObjects)
                {
                    if (!string.IsNullOrEmpty(mo.Owner) && usedPlayers.Contains(mo.Owner))
                    {
                        var free = KnownValues.SpawnPlayers.FirstOrDefault(p => !usedPlayers.Contains(p));
                        if (free != null)
                        {
                            usedPlayers.Remove(mo.Owner);
                            mo.Owner = free;
                            usedPlayers.Add(free);
                            hadConflict = true;
                        }
                    }
                    if (!string.IsNullOrEmpty(mo.Spawn) && usedPlayers.Contains(mo.Spawn))
                    {
                        var free = KnownValues.SpawnPlayers.FirstOrDefault(p => !usedPlayers.Contains(p));
                        if (free != null)
                        {
                            usedPlayers.Remove(mo.Spawn);
                            mo.Spawn = free;
                            usedPlayers.Add(free);
                            hadConflict = true;
                        }
                    }
                }
                if (hadConflict)
                    System.Windows.MessageBox.Show(this,
                        "Некоторые игроки уже были заняты в других зонах. Владельцы/спавны скопированной зоны были переназначены на свободных игроков.",
                        "Конфликт игроков", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Zones.Add(copy);
            _positions[copy.Name] = pos;
            MarkDirty();
            RebuildGraph();
            Select(copy);
            UpdateStatus(L("S.EC.ZonePasted", copy.Name));
        }

        private void BtnCopyZone_Click(object sender, RoutedEventArgs e) => CopySelectedZone();

        private void BtnPasteZone_Click(object sender, RoutedEventArgs e) => PasteCopiedZone();

        private void BtnJsonPreview_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            var window = new JsonPreviewWindow(this);
            window.Show();
        }

        private void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                UpdateStatus(L("S.EC.NoIssues"));
                MessageBox.Show(this, L("S.EC.NoIssuesMsg"), L("S.EC.ValidateTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            UpdateStatus(L("S.EC.IssuesFound", issues.Count));
            MessageBox.Show(this, string.Join("\n", issues.Take(30)), L("S.EC.IssuesTitle", issues.Count),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private List<string> Validate() => ZoneGraphValidator.Validate(Zones, Connections);

        private void BtnRelayout_Click(object sender, RoutedEventArgs e)
        {
            ComputePositions();
            RebuildGraph();
            FitToView();
            UpdateStatus(L("S.EC.Relayout"));
        }

        private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => FitToView();

        private void FitToView()
        {
            if (_positions.Count == 0 || CanvasHost.ActualWidth <= 0) { return; }
            double minX = _positions.Values.Min(p => p.X), maxX = _positions.Values.Max(p => p.X);
            double minY = _positions.Values.Min(p => p.Y), maxY = _positions.Values.Max(p => p.Y);
            double pad = 70;
            double w = Math.Max(1, maxX - minX + pad * 2);
            double h = Math.Max(1, maxY - minY + pad * 2);
            double scale = Math.Clamp(Math.Min(CanvasHost.ActualWidth / w, CanvasHost.ActualHeight / h), 0.2, 3.0);
            CanvasScale.ScaleX = CanvasScale.ScaleY = scale;
            CanvasTranslate.X = (CanvasHost.ActualWidth  - (minX + maxX) * scale) / 2;
            CanvasTranslate.Y = (CanvasHost.ActualHeight - (minY + maxY) * scale) / 2;
            TxtZoomLabel.Text = $"{scale * 100:0}%";
        }

        // ── Load / Save (Phase C round-trip) ─────────────────────────────────────────

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            var dlg = new OpenFileDialog
            {
                Title = L("S.EC.LoadTitle"),
                Filter = L("S.EC.LoadFilter"),
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var loaded = JsonSerializer.Deserialize<RmgTemplate>(json, JsonOptions);
                if (loaded is null) { UpdateStatus(L("S.EC.LoadFail")); return; }
                LoadTemplate(loaded);
                _currentPath = dlg.FileName;
                UpdateStatus(L("S.EC.Loaded", IOPath.GetFileName(dlg.FileName), Zones.Count, Connections.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.EC.LoadErr", ex.Message), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadTemplate(RmgTemplate loaded)
        {
            _template = loaded;
            _topology = MapTopology.Default;
            _dirty = false;
            UpdateTitle();
            _selected = null; _connectFrom = null; _connectMode = false;
            // Validate all zones on load
            foreach (var z in Zones)
                ValidateZone(z);
            ComputePositions();
            AutoGenerateConnectionNames();
            // All road syncing handled by RebuildGraph with proper pre-auto snapshot
            RebuildGraph();
            FitToView();
            BuildInspector();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();

            // Hard-blocking validation: empty connection names
            var emptyNameConns = Connections.Where(c => string.IsNullOrWhiteSpace(c.Name)).ToList();
            if (emptyNameConns.Count > 0)
            {
                var connList = string.Join(", ", emptyNameConns.Select(c => $"'{c.From}' → '{c.To}'"));
                MessageBox.Show(this,
                    $"Невозможно сохранить: {emptyNameConns.Count} связ(ь/и) имеют пустое имя.\n\n" +
                    $"Затронутые связи: {connList}\n\n" +
                    "Исправьте: задайте имя для каждой связи в инспекторе (поле «Имя связи»).",
                    L("S.EC.SaveValidateTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var issues = Validate();
            if (issues.Count > 0)
            {
                var go = MessageBox.Show(this,
                    L("S.EC.SaveValidate", issues.Count, string.Join("\n", issues.Take(10))),
                    L("S.EC.SaveValidateTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (go != MessageBoxResult.Yes) return;
            }

            var dlg = new SaveFileDialog
            {
                Title = L("S.EC.SaveTitle"),
                Filter = L("S.EC.SaveFilter"),
                DefaultExt = ".rmg.json",
                FileName = _currentPath is not null ? IOPath.GetFileName(_currentPath)
                                                    : $"{_template.Name}.rmg.json",
            };
            if (_currentPath is not null) dlg.InitialDirectory = IOPath.GetDirectoryName(_currentPath);
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                // Pre-save cleanup: remove ContentSidLimit entries with empty sid
                if (_template.ContentCountLimits != null)
                {
                    foreach (var limit in _template.ContentCountLimits)
                        limit.Limits?.RemoveAll(l => string.IsNullOrEmpty(l.Sid));
                }
                if (_template.MandatoryContent != null)
                {
                    foreach (var mg in _template.MandatoryContent)
                        mg.Content?.RemoveAll(c => string.IsNullOrEmpty(c.Sid));
                }

                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_template, JsonOptions));
                _currentPath = dlg.FileName;
                _dirty = false;
                UpdateTitle();
                UpdateStatus(L("S.EC.Saved", IOPath.GetFileName(dlg.FileName)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.EC.SaveErr", ex.Message), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // ── Connection Management ────────────────────────────────────────────────────────────

        private void BtnConnectionManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_connectionManagerWindow?.IsVisible == true)
                {
                    _connectionManagerWindow.Focus();
                    return;
                }
                var managerWindow = new ConnectionManagerWindow(_template, this);
                _connectionManagerWindow = managerWindow;
                managerWindow.Closed += (_, _) => _connectionManagerWindow = null;
                managerWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Ошибка при открытии менеджера связей:\n\n{ex.GetType().Name}: {ex.Message}\n\nStack:\n{ex.StackTrace}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Misc ──────────────────────────────────────────────────────────────────────

        /// <summary>Test/screenshot hook: select a zone by name (used by --shoot-editor verification).</summary>
        internal void DebugSelectZone(string name)
        {
            var z = Zones.FirstOrDefault(zz => string.Equals(zz.Name, name, StringComparison.Ordinal));
            if (z is not null) Select(z);
        }

        internal void MarkDirty() { _dirty = true; UpdateTitle(); }

        private void UpdateTitle()
        {
            string file = _currentPath is not null ? IOPath.GetFileName(_currentPath) : L("S.CB.Untitled");
            Title = L("S.EC.WinTitle", file) + (_dirty ? "*" : "");
        }

        public void UpdateStatus(string text) => TxtStatus.Text = text;

        /// <summary>Localization shortcut.</summary>
        private static string L(string key, params object[] args) => Services.Localization.LocalizationManager.T(key, args);

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_dirty)
            {
                var r = MessageBox.Show(this,
                    L("S.EC.UnsavedMsg"),
                    L("S.EC.UnsavedTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) { e.Cancel = true; return; }
            }
            base.OnClosing(e);
        }
    }
}
