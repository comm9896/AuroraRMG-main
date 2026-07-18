using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
using OldenEraTemplateEditor.Models.Generated;
using OldenEraTemplateEditor.Services.ContentManagement;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.GameData;
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
        internal static readonly JsonSerializerOptions JsonOptions = Olden_Era___Template_Editor.Services.JsonExport.Options;

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
        private double  _gridSize = 50.0; // Grid cell size in pixels (adjustable via toolbar)
        private const double GridSizeMin = 20.0, GridSizeMax = 200.0, GridSizeStep = 10.0;

        // Mirror creation mode (vertical left/right split)
        private bool _mirrorMode;
        private bool _mirrorProperties = true;   // mirror zone property edits to the twin
        private bool _mirrorConnections = true;  // mirror connections (and their settings) to the twins
        private bool _applyingMirror; // guards against recursive mirroring
        private readonly Dictionary<string, string> _mirrorMap = new(StringComparer.Ordinal); // zone<->mirror (both directions)
        private readonly Dictionary<string, string> _connectionMirrorMap = new(StringComparer.Ordinal); // connection<->mirror (both directions)
        private readonly HashSet<string> _lockedZones = new(StringComparer.Ordinal); // immovable zones (e.g. hub)
        private bool _dragLeftSide;     // which side the dragged zone started on (mirror clamp)
        private bool _dragZoneLocked;   // dragged zone is immovable
        private Line? _mirrorDivider;
        private TextBlock? _mirrorDividerLabel;
        private System.Windows.Shapes.Rectangle? _mirrorBand;
        private Point _mousePosOnCanvas; // last known mouse position on canvas
        private object? _selected; // Zone or Connection
        private Window? _connectionManagerWindow; // Connection manager window reference
        private Zone? _copiedZone; // Zone data for copy/paste
        private Connection? _copiedConnection; // Connection properties for copy/paste
        private Dictionary<string, string> _hotkeys = new(); // Action → "Ctrl+Key"

        /// <summary>Raised after a template is imported via the "загрузить .rmg.json" button, so the
        /// host (main window) can re-import it in edit mode with the same restriction logic.</summary>
        public event Action<RmgTemplate>? TemplateImported;

        public TemplateEditorWindow(RmgTemplate? template = null, MapTopology topology = MapTopology.Default)
        {
            InitializeComponent();
            _template = template ?? NewEmptyTemplate();
            _topology = topology;
            _hotkeys = GetDefaultHotkeys();
            // Override with saved values
            foreach (var kv in AppSettings.Current.Hotkeys)
                _hotkeys[kv.Key] = kv.Value;
            // Override with config.json hotkeys (user-editable file next to the exe)
            foreach (var kv in Services.ConfigJson.Current.Hotkeys)
                _hotkeys[kv.Key] = kv.Value;

            // Add hotkey labels under toolbar buttons
            Loaded += (_, _) =>
            {
                AddHotkeyLabelsToToolbar();
                ComputePositions();
                RebuildGraph();
                BtnGridSnap.Background = _gridSnap
                    ? new SolidColorBrush(Color.FromRgb(40, 60, 40))
                    : null;
                UpdateGridSizeLabel();
                FitToView();
                UpdateTitle();
                UpdateCanvasHintVisibility();
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
                    Math.Round(p.X / _gridSize) * _gridSize,
                    Math.Round(p.Y / _gridSize) * _gridSize);
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
            if (_mirrorDivider is not null)
            {
                GraphCanvas.Children.Remove(_mirrorDivider);
                _mirrorDivider = null;
            }
            if (_mirrorDividerLabel is not null)
            {
                GraphCanvas.Children.Remove(_mirrorDividerLabel);
                _mirrorDividerLabel = null;
            }
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
            if (_mirrorMode) DrawMirrorDivider();
        }

        /// <summary>Draws a subtle reference grid (cell size = <see cref="_gridSize"/>) that
        /// pans/zooms with the graph. The cell size is adjustable from the toolbar.</summary>
        private void DrawGrid()
        {
            double cell = _gridSize;
            var drawing = new GeometryDrawing
            {
                Geometry = new RectangleGeometry(new Rect(0, 0, cell, cell)),
                Pen = new Pen(new SolidColorBrush(GridLine), 1),
            };
            var brush = new DrawingBrush
            {
                Drawing = drawing, TileMode = TileMode.Tile, Stretch = Stretch.None,
                Viewport = new Rect(0, 0, cell, cell), ViewportUnits = BrushMappingMode.Absolute,
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

            int spawns = z.MainObjects?.Count(o => o.Type == "Spawn") ?? 0;

            innerPanel.Children.Add(new TextBlock
            {
                Text = $"🏰{castles + spawns}",
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
                        if (string.IsNullOrWhiteSpace(v)) { z.Size = 1.0; MirrorMarkDirty(z); RefreshNode(z); }
                        else if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.Size = d; MirrorMarkDirty(z); RefreshNode(z); }
                    }, mainSection);
                AddComboField(L("S.EC.Layout"), KnownValues.ZoneLayouts, z.Layout,
                    v => { z.Layout = v; MirrorMarkDirty(z); RefreshNode(z); }, mainSection);

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
                    MirrorMarkDirty(z);
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
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.DiplomacyModifier = d; MirrorMarkDirty(z); } }, g1);
                var g2 = AddExpanderSection(L("S.EC.GuardMult"), guardPanel);
                AddTextField(L("S.EC.GuardMult"), (z.GuardMultiplier ?? 1.0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardMultiplier = d; MirrorMarkDirty(z); } }, g2);
                var g3 = AddExpanderSection(L("S.EC.GuardCutoff"), guardPanel);
                AddTextField(L("S.EC.GuardCutoff"), (z.GuardCutoffValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.GuardCutoffValue = i; MirrorMarkDirty(z); } }, g3);
                var g4 = AddExpanderSection(L("S.EC.GuardRandom"), guardPanel);
                AddTextField(L("S.EC.GuardRandom"), (z.GuardRandomization ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardRandomization = d; MirrorMarkDirty(z); } }, g4);
                var g5 = AddExpanderSection(L("S.EC.GuardWeeklyInc"), guardPanel);
                AddTextField(L("S.EC.GuardWeeklyInc"), (z.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardWeeklyIncrement = d; MirrorMarkDirty(z); } }, g5);
                var g6 = AddExpanderSection(L("S.EC.GuardReactDist"), guardPanel);
                AddIntListField(L("S.EC.GuardReactDist"), z.GuardReactionDistribution,
                    v => { z.GuardReactionDistribution = v; MirrorMarkDirty(z); }, g6);
                var g7 = AddExpanderSection(L("S.EC.EncounterHoles"), guardPanel);
                AddTextField(L("S.EC.AffectedEnc"), (z.EncounterHolesSettings?.AffectedEncounters ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.EncounterHolesSettings ??= new(); z.EncounterHolesSettings.AffectedEncounters = d; MirrorMarkDirty(z); } }, g7,
                    "Принимает аргументы от 0 до 1.\n\nКомментарий от Mont: Это пустые клетки в точке интереса (там, где стоит охраняемый или неохраняемый объект, здание/артефакт и т.д.). Используется эта функция на шаблонах типа анархии (без правил), где можно \"воровать\" всякие вкусности, не пробивая охрану.");
                AddTextField(L("S.EC.TwoHoleEnc"), (z.EncounterHolesSettings?.TwoHoleEncounters ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.EncounterHolesSettings ??= new(); z.EncounterHolesSettings.TwoHoleEncounters = d; MirrorMarkDirty(z); } }, g7,
                    "Принимает аргументы от 0 до 1.\n\nКомментарий от Mont: Это пустые клетки в точке интереса (там, где стоит охраняемый или неохраняемый объект, здание/артефакт и т.д.). Используется эта функция на шаблонах типа анархии (без правил), где можно \"воровать\" всякие вкусности, не пробивая охрану.");

                var poolsPanel = InspectorFieldsPools;

                var viewPoolsBtn = new System.Windows.Controls.Button
                {
                    Content = L("S.EC.BtnViewPools"),
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 11
                };
                viewPoolsBtn.Click += (_, _) =>
                {
                    try
                    {
                        // View-only: show the full pool universe (game pools + every template's
                        // mandatory_content / content_count_limits pools) with category filtering.
                        var viewer = new ContentPoolViewerWindow(selectionMode: false);
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
                    Content = L("S.EC.BtnCreatePool"),
                    Margin = new Thickness(0, 0, 0, 12),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 11
                };
                createPoolBtn.Click += (_, _) =>
                {
                    var creator = new ContentPoolCreatorWindow();
                    creator.Owner = this;
                    creator.PoolCreated += (poolName, poolItems) =>
                    {
                        try
                        {
                            // Add custom_ prefix for filtering
                            var fullName = poolName.StartsWith("custom_") ? poolName : "custom_" + poolName;

                            // One group per selected list; the weight written for that list at
                            // creation time becomes the group's weight, and the list name goes
                            // into includeLists (matching the real GameData pool schema).
                            var groups = new List<PoolGroup>();
                            foreach (var kv in poolItems)
                                groups.Add(new PoolGroup
                                {
                                    Weight = kv.Value,
                                    IncludeLists = new List<string> { kv.Key },
                                });

                            var newPool = new GamePool
                            {
                                Name = fullName,
                                // Hardcoded value distribution (matches the reference pool format).
                                ValueDistribution = new ValueDistribution
                                {
                                    PriceBounds = new List<int> { 3999, 6999, 12999, 15999 },
                                    Weights     = new List<int> { 6, 8, 10, 6, 0 },
                                },
                                Groups = groups,
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
                AddCategoryPicker(L("S.EC.GuardedPool"),
                    new List<string> { "Guarded", "Unguarded", "Random", "Template-specific", "Созданные" },
                    z.GuardedContentPool, v => { z.GuardedContentPool = v; MirrorMarkDirty(z); }, p1);
                var p2 = AddExpanderSection(L("S.EC.UnguardedPool"), poolsPanel);
                AddCategoryPicker(L("S.EC.UnguardedPool"),
                    new List<string> { "Guarded", "Unguarded", "Random", "Template-specific", "Созданные" },
                    z.UnguardedContentPool, v => { z.UnguardedContentPool = v; MirrorMarkDirty(z); }, p2);
                var p3 = AddExpanderSection(L("S.EC.ResourcesPool"), poolsPanel);
                AddCategoryPicker(L("S.EC.ResourcesPool"),
                    new List<string> { "Resources" },
                    z.ResourcesContentPool, v => { z.ResourcesContentPool = v; MirrorMarkDirty(z); }, p3);
                var p4 = AddExpanderSection(L("S.EC.MandatoryContent"), poolsPanel);
                AddCategoryPicker(L("S.EC.MandatoryContent"),
                    new List<string> { "обязательный контент", "Созданные" },
                    z.MandatoryContent, v => { z.MandatoryContent = v; MirrorMarkDirty(z); }, p4);
                var p5 = AddExpanderSection(L("S.EC.ContentCountLimits"), poolsPanel);
                AddCategoryPicker(L("S.EC.ContentCountLimits"),
                    new List<string> { "лимиты количества контента", "Созданные" },
                    z.ContentCountLimits, v => { z.ContentCountLimits = v; MirrorMarkDirty(z); }, p5);

                var contentPanel = InspectorFieldsContent;
                var c1 = AddExpanderSection(L("S.EC.GuardedVal"), contentPanel);
                AddTextField(L("S.EC.GuardedVal"), (z.GuardedContentValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.GuardedContentValue = i; MirrorMarkDirty(z); RefreshNode(z); } }, c1);
                var c2 = AddExpanderSection(L("S.EC.GuardedValPerArea"), contentPanel);
                AddTextField(L("S.EC.GuardedValPerArea"), (z.GuardedContentValuePerArea ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.GuardedContentValuePerArea = i; MirrorMarkDirty(z); RefreshNode(z); } }, c2);
                var c3 = AddExpanderSection(L("S.EC.UnguardedVal"), contentPanel);
                AddTextField(L("S.EC.UnguardedVal"), (z.UnguardedContentValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.UnguardedContentValue = i; MirrorMarkDirty(z); RefreshNode(z); } }, c3);
                var c4 = AddExpanderSection(L("S.EC.UnguardedValPerArea"), contentPanel);
                AddTextField(L("S.EC.UnguardedValPerArea"), (z.UnguardedContentValuePerArea ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.UnguardedContentValuePerArea = i; MirrorMarkDirty(z); RefreshNode(z); } }, c4);
                var c5 = AddExpanderSection(L("S.EC.ResourcesVal"), contentPanel);
                AddTextField(L("S.EC.ResourcesVal"), (z.ResourcesValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.ResourcesValue = i; MirrorMarkDirty(z); RefreshNode(z); } }, c5);
                var c6 = AddExpanderSection(L("S.EC.ResourcesValPerArea"), contentPanel);
                AddTextField(L("S.EC.ResourcesValPerArea"), (z.ResourcesValuePerArea ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.ResourcesValuePerArea = i; MirrorMarkDirty(z); RefreshNode(z); } }, c6);

                var biomePanel = InspectorFieldsBiome;
                var b1 = AddExpanderSection(L("S.EC.CrossroadsPos"), biomePanel);
                AddTextField(L("S.EC.CrossroadsPos"), (z.CrossroadsPosition ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { z.CrossroadsPosition = i; MirrorMarkDirty(z); } }, b1);
                var b2 = AddExpanderSection(L("S.EC.ZoneBiome"), biomePanel);
                AddBiomeSelector(L("S.EC.ZoneBiome"), z.ZoneBiome, v => { z.ZoneBiome = v; MirrorMarkDirty(z); }, b2);
                var b3 = AddExpanderSection(L("S.EC.ContentBiome"), biomePanel);
                AddBiomeSelector(L("S.EC.ContentBiome"), z.ContentBiome, v => { z.ContentBiome = v; MirrorMarkDirty(z); }, b3);
                var b4 = AddExpanderSection(L("S.EC.MetaBiome"), biomePanel);
                AddBiomeSelector(L("S.EC.MetaBiome"), z.MetaObjectsBiome, v => { z.MetaObjectsBiome = v; MirrorMarkDirty(z); }, b4);
                var b5 = AddExpanderSection(L("S.EC.Roads"), biomePanel);
                AddRoadList(L("S.EC.Roads"), z.Roads, v => { z.Roads = v; MirrorMarkDirty(z); }, b5);

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
                    Content = L("S.EC.BtnAutoRoad"),
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
                    MirrorMarkDirty(z);
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
                    c.Name = v; MarkDirty(); MirrorConnectionMarkDirty(c);
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
                    { c.Length = d; MarkDirty(); MirrorConnectionMarkDirty(c); RebuildGraph(); }
                };
                lengthPanel.Children.Add(lengthBox);

                AddComboField(L("S.EC.Type"), connectionTypes, c.ConnectionType, v =>
                {
                    c.ConnectionType = v;
                    MarkDirty(); MirrorConnectionMarkDirty(c);
                    lengthPanel.Visibility = v == "Proximity" ? Visibility.Visible : Visibility.Collapsed;
                }, mainPanel);

                // Guard value
                AddTextField(L("S.EC.GuardValue"), (c.GuardValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { c.GuardValue = i; MarkDirty(); MirrorConnectionMarkDirty(c); } }, mainPanel);

                mainPanel.Children.Add(lengthPanel);

                // Road checkbox
                AddCheckField(L("S.EC.Road"), c.Road == true, v =>
                {
                    c.Road = v;
                    MarkDirty(); MirrorConnectionMarkDirty(c);
                    string connName = c.Name ?? $"{c.From}-{c.To}";
                    if (v)
                    {
                        AutoGenerateRoadsForConnection(c);
                        // Also generate roads inside the mirrored zones of the twin connection.
                        if (_mirrorMode && _mirrorConnections &&
                            _connectionMirrorMap.TryGetValue(c.Name, out var twinName))
                        {
                            var twin = Connections.FirstOrDefault(x => x.Name == twinName);
                            if (twin != null) AutoGenerateRoadsForConnection(twin);
                        }
                    }
                    else
                    {
                        // Road turned OFF: delete the roads referencing this connection and rebuild any
                        // castle-less hub star from the remaining Road==true connections. In mirror mode
                        // the twin connection's roads are cleared symmetrically too.
                        var zFrom = Zones.FirstOrDefault(z => z.Name == c.From);
                        var zTo = Zones.FirstOrDefault(z => z.Name == c.To);
                        RemoveConnectionRoads(zFrom, connName);
                        RemoveConnectionRoads(zTo, connName);
                        RebuildCastleLessStar(zFrom);
                        RebuildCastleLessStar(zTo);

                        if (_mirrorMode && _mirrorConnections &&
                            _connectionMirrorMap.TryGetValue(c.Name, out var twinName))
                        {
                            var twin = Connections.FirstOrDefault(x => x.Name == twinName);
                            if (twin != null)
                            {
                                string twinConnName = twin.Name ?? $"{twin.From}-{twin.To}";
                                var tzFrom = Zones.FirstOrDefault(z => z.Name == twin.From);
                                var tzTo = Zones.FirstOrDefault(z => z.Name == twin.To);
                                RemoveConnectionRoads(tzFrom, twinConnName);
                                RemoveConnectionRoads(tzTo, twinConnName);
                                RebuildCastleLessStar(tzFrom);
                                RebuildCastleLessStar(tzTo);
                            }
                        }
                    }
                    RebuildGraph();
                }, mainPanel);

                // Guard escape
                AddCheckField(L("S.EC.GuardEscape"), c.GuardEscape == true,
                    v => { c.GuardEscape = v; MarkDirty(); MirrorConnectionMarkDirty(c); }, mainPanel);

                // Guard weekly increment
                AddTextField(L("S.EC.GuardWeeklyIncConn"), (c.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { c.GuardWeeklyIncrement = d; MarkDirty(); MirrorConnectionMarkDirty(c); } }, mainPanel);

                // Guard randomization
                AddTextField(L("S.EC.GuardRandomizationConn"), (c.GuardRandomization ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { c.GuardRandomization = d; MarkDirty(); MirrorConnectionMarkDirty(c); } }, mainPanel);

                // Gate placement
                AddComboField(L("S.EC.GatePlacement"), KnownValues.GatePlacements, c.GatePlacement,
                    v => { c.GatePlacement = v; MarkDirty(); MirrorConnectionMarkDirty(c); }, mainPanel);

                // Gate placement args (multi-select, visible only for NearZone)
                var gateArgsPanel = new StackPanel { Visibility = c.GatePlacement == "NearZone" ? Visibility.Visible : Visibility.Collapsed };
                AddSectionLabel(L("S.EC.GatePlacementArgs"), gateArgsPanel);
                var gateArgsListBox = new ListBox
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    MinHeight = 80,
                    SelectionMode = SelectionMode.Multiple,
                    Background = (Brush)FindResource("BrushInput"),
                    Foreground = (Brush)FindResource("BrushText"),
                };
                var allZoneNames = Zones.Select(z => z.Name).Where(n => !string.IsNullOrEmpty(n) && n != c.From && n != c.To).ToArray();
                foreach (var zn in allZoneNames)
                    _ = gateArgsListBox.Items.Add(zn);
                if (c.GatePlacementArgs is { Count: > 0 })
                {
                    foreach (var item in gateArgsListBox.Items)
                    {
                        if (c.GatePlacementArgs.Contains(item as string))
                            gateArgsListBox.SelectedItems.Add(item);
                    }
                }
                gateArgsListBox.SelectionChanged += (_, _) =>
                {
                    c.GatePlacementArgs = gateArgsListBox.SelectedItems.Count > 0
                        ? gateArgsListBox.SelectedItems.Cast<string>().ToList()
                        : null;
                    MarkDirty(); MirrorConnectionMarkDirty(c);
                };
                gateArgsPanel.Children.Add(gateArgsListBox);
                mainPanel.Children.Add(gateArgsPanel);
                // Toggle gate args panel when gate placement changes
                // Find the gate placement combo that was just added
                foreach (var child in mainPanel.Children)
                {
                    if (child is ComboBox combo && combo.Items.Contains("NearZone"))
                    {
                        combo.SelectionChanged += (_, _) =>
                        {
                            var sel = combo.SelectedItem as string ?? combo.Text;
                            gateArgsPanel.Visibility = sel == "NearZone" ? Visibility.Visible : Visibility.Collapsed;
                        };
                        break;
                    }
                }

                // Advanced section (guardZone + guardMatchGroup, rarely used)
                var advancedExpander = new Expander
                {
                    Header = L("S.EC.Advanced"),
                    IsExpanded = false,
                    Margin = new Thickness(0, 8, 0, 4),
                };
                var advancedContent = new StackPanel();
                advancedExpander.Content = advancedContent;
                mainPanel.Children.Add(advancedExpander);

                var zoneNames = Zones.Select(z => z.Name).Where(n => n != null).ToArray()!;
                AddComboField(L("S.EC.GuardZone"), zoneNames, c.GuardZone,
                    v => { c.GuardZone = v; MarkDirty(); MirrorConnectionMarkDirty(c); }, advancedContent);

                AddCheckField("Одновременный отряд", c.SimTurnSquad == true,
                    v => { c.SimTurnSquad = v; MarkDirty(); MirrorConnectionMarkDirty(c); }, advancedContent);

                AddTextField("Группа сопоставления охраны", c.GuardMatchGroup ?? "",
                    v => { c.GuardMatchGroup = v; MarkDirty(); MirrorConnectionMarkDirty(c); }, advancedContent);

                advancedContent.Children.Add(new TextBlock
                {
                    Text = L("S.EC.AdvancedNote"),
                    Foreground = (Brush)FindResource("BrushTextDim"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 4),
                });

                // ── Portal rules panel ──────────────────────────────────────
                var portalPanel = new StackPanel { Visibility = c.ConnectionType == "Portal" ? Visibility.Visible : Visibility.Collapsed };
                mainPanel.Children.Add(portalPanel);

                // Listen for connection type changes to toggle portal panel
                // We need to hook into the type combo. Since AddComboField creates its own,
                // we patch by re-applying after the combo's handler
                // Actually, we can rebuild on type change; but easier: find the type combo in the panel.
                // Instead, we handle it in the type combo's existing handler via lengthPanel visibility logic.
                // We'll add our own visibility toggle there. Let's override the type combo handler below.

                // Helper to build a rules panel
                Panel BuildRuleList(List<ContentPlacementRule>? rules, Action<List<ContentPlacementRule>> onChanged)
                {
                    var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

                    if (rules != null)
                    {
                        for (int i = 0; i < rules.Count; i++)
                        {
                            var rule = rules[i];
                            var idx = i; // capture
                            var rulePanel = new StackPanel
                            {
                                Margin = new Thickness(4, 4, 4, 4),
                                Background = (Brush)FindResource("BrushInput"),
                            };

                            // Type
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
                                {
                                    rule.Type = s;
                                    MarkDirty();
                                }
                            };
                            rulePanel.Children.Add(new TextBlock { Text = L("S.EC.PlacementRuleType"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11 });
                            rulePanel.Children.Add(typeCombo);

                            // Args
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
                                MarkDirty();
                            };
                            rulePanel.Children.Add(new TextBlock { Text = L("S.EC.PlacementRuleArgs"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11 });
                            rulePanel.Children.Add(argsBox);

                            // TargetMin
                            var tminBox = new TextBox
                            {
                                Text = (rule.TargetMin ?? 0).ToString(CultureInfo.InvariantCulture),
                                Margin = new Thickness(0, 2, 0, 2),
                            };
                            tminBox.LostFocus += (_, _) =>
                            {
                                if (double.TryParse(tminBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                                { rule.TargetMin = d; MarkDirty(); }
                            };
                            rulePanel.Children.Add(new TextBlock { Text = L("S.EC.PlacementRuleTargetMin"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11 });
                            rulePanel.Children.Add(tminBox);

                            // TargetMax
                            var tmaxBox = new TextBox
                            {
                                Text = (rule.TargetMax ?? 0).ToString(CultureInfo.InvariantCulture),
                                Margin = new Thickness(0, 2, 0, 2),
                            };
                            tmaxBox.LostFocus += (_, _) =>
                            {
                                if (double.TryParse(tmaxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                                { rule.TargetMax = d; MarkDirty(); }
                            };
                            rulePanel.Children.Add(new TextBlock { Text = L("S.EC.PlacementRuleTargetMax"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11 });
                            rulePanel.Children.Add(tmaxBox);

                            // Weight
                            var wBox = new TextBox
                            {
                                Text = (rule.Weight ?? 1).ToString(CultureInfo.InvariantCulture),
                                Margin = new Thickness(0, 2, 0, 2),
                            };
                            wBox.LostFocus += (_, _) =>
                            {
                                if (double.TryParse(wBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                                { rule.Weight = d; MarkDirty(); }
                            };
                            rulePanel.Children.Add(new TextBlock { Text = L("S.EC.PlacementRuleWeight"), Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11 });
                            rulePanel.Children.Add(wBox);

                            // Remove button
                            var removeBtn = new Button
                            {
                                Content = L("S.EC.RemoveRule"),
                                Margin = new Thickness(0, 4, 0, 0),
                                Padding = new Thickness(8, 2, 8, 2),
                                Cursor = Cursors.Hand,
                            };
                            removeBtn.Click += (_, _) =>
                            {
                                var list = c.PortalPlacementRulesFrom ?? new List<ContentPlacementRule>();
                                if (list.Contains(rule)) list = c.PortalPlacementRulesFrom!;
                                else if (c.PortalPlacementRulesTo?.Contains(rule) == true) list = c.PortalPlacementRulesTo!;
                                list.RemoveAt(idx);
                                MarkDirty();
                                BuildInspector();
                            };
                            rulePanel.Children.Add(removeBtn);

                            outer.Children.Add(rulePanel);
                        }
                    }

                    var addBtn = new Button
                    {
                        Content = L("S.EC.AddRule"),
                        Margin = new Thickness(0, 4, 0, 0),
                        Padding = new Thickness(8, 2, 8, 2),
                        Cursor = Cursors.Hand,
                    };
                    addBtn.Click += (_, _) =>
                    {
                        var list = rules ?? new List<ContentPlacementRule>();
                        list.Add(new ContentPlacementRule { Type = "Crossroads", Args = null, TargetMin = 0, TargetMax = 0, Weight = 1 });
                        onChanged(list);
                        MarkDirty();
                        BuildInspector();
                    };
                    outer.Children.Add(addBtn);
                    return outer;
                }

                // Portal rules From
                AddSectionLabel(L("S.EC.PortalRulesFrom"), portalPanel);
                portalPanel.Children.Add(BuildRuleList(c.PortalPlacementRulesFrom, list => c.PortalPlacementRulesFrom = list));

                // Portal rules To
                AddSectionLabel(L("S.EC.PortalRulesTo"), portalPanel);
                portalPanel.Children.Add(BuildRuleList(c.PortalPlacementRulesTo, list => c.PortalPlacementRulesTo = list));

                // Patch the type combo to toggle portal panel visibility.
                // Walk mainPanel children to find the combo added for connection type.
                foreach (var child in mainPanel.Children)
                {
                    if (child is ComboBox combo && combo.Items.Contains("Portal") && combo.Items.Contains("Proximity"))
                    {
                        var origSelectionChanged = typeof(ComboBox).GetField("SelectionChangedEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        combo.SelectionChanged += (_, _) =>
                        {
                            var sel = combo.SelectedItem as string;
                            portalPanel.Visibility = sel == "Portal" ? Visibility.Visible : Visibility.Collapsed;
                        };
                        break;
                    }
                }
            }

            // ── Connection copy/paste buttons (in "Основное" tab) ──
            if (_selected is Connection cc)
            {
                var copyPastePanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

                var btnCopy = new Button
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x70, 0x40)),
                    Foreground = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                btnCopy.Content = MakeHotkeyButtonContent("📋 Копировать свойства связи", "CopyConnectionProps");
                btnCopy.Click += (_, _) => CopyConnectionProps();

                var btnPaste = new Button
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x70, 0x40)),
                    Foreground = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsEnabled = _copiedConnection != null,
                };
                btnPaste.Content = MakeHotkeyButtonContent("📋 Вставить свойства", "PasteConnectionProps");
                btnPaste.Click += (_, _) => PasteConnectionProps();

                InspectorFieldsMain?.Children.Add(copyPastePanel);
            }

            else
            {
                TxtInspectorHint.Text = string.Empty;
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

            // Hold-city win condition (top of the main-object editor)
            AddCheckField(L("S.EC.MoHoldCity"), mo.HoldCityWinCon == true, v => { mo.HoldCityWinCon = v; MarkDirty(); }, panel);

            // Track controls that need to be hidden for GladiatorArena
            var guardFields = new List<FrameworkElement>();
            var factionFields = new List<FrameworkElement>();
            var placementFields = new List<FrameworkElement>();

            // Combo references shared between Owner and Spawn handlers (declared up-front for closure scope)
            ComboBox? ownerCombo = null;
            ComboBox? playerCombo = null;

            // Object type selector (label + combo are added below the "remove guard if owned" flag)
            var typeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            foreach (var t in KnownValues.MainObjectTypes) typeCombo.Items.Add(t);
            typeCombo.SelectedItem = mo.Type;

            // Guard chance
            var gcPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoGuardChance"), gcPanel);
            var gcBox = new TextBox { Text = (mo.GuardChance ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 8) };
            gcBox.LostFocus += (_, _) => { if (double.TryParse(gcBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardChance = d; MirrorMarkDirty(z); };
            gcPanel.Children.Add(gcBox);
            guardFields.Add(gcPanel);

            // Guard value
            var gvPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoGuardValue"), gvPanel);
            var gvBox = new TextBox { Text = (mo.GuardValue ?? 0).ToString(), Margin = new Thickness(0, 0, 0, 8) };
            gvBox.LostFocus += (_, _) => { if (int.TryParse(gvBox.Text, out var v)) mo.GuardValue = v; MirrorMarkDirty(z); };
            gvPanel.Children.Add(gvBox);
            guardFields.Add(gvPanel);

            // Guard weekly increment
            var gwPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoGuardWeeklyInc"), gwPanel);
            var gwBox = new TextBox { Text = (mo.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 8) };
            gwBox.LostFocus += (_, _) => { if (double.TryParse(gwBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardWeeklyIncrement = d; MirrorMarkDirty(z); };
            gwPanel.Children.Add(gwBox);
            guardFields.Add(gwPanel);

            // Buildings construction
            var buildPanel = new StackPanel();
            AddSectionLabel(L("S.EC.MoBuildings"), buildPanel);
            var buildCombo = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            foreach (var b in KnownValues.BuildingsConstructionSids) buildCombo.Items.Add(b);
            buildCombo.Text = mo.BuildingsConstructionSid ?? "";
            buildCombo.LostFocus += (_, _) => { mo.BuildingsConstructionSid = buildCombo.Text.Trim(); MirrorMarkDirty(z); };
            buildCombo.SelectionChanged += (_, _) => { if (buildCombo.SelectedItem is string s) { mo.BuildingsConstructionSid = s; MirrorMarkDirty(z); } };
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
                MirrorMarkDirty(z);
            };
            removeGuardCheck.Unchecked += (_, _) =>
            {
                mo.RemoveGuardIfHasOwner = false;
                UpdateGuardFieldsVisibility();
                MirrorMarkDirty(z);
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
                    MirrorMarkDirty(z);
                }
            };
            facArgsPanel.Children.Add(factionArgsCombo);

            // Spawn main objects are configured via the "Spawn" field below (shown only for main objects of type Spawn).
            bool isFirstMainObject = z.MainObjects?.IndexOf(mo) == 0;

            // Owner (visible only for City)
            var ownerPanel = new StackPanel { Visibility = mo.Type == "City" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel(L("S.EC.MoOwner"), ownerPanel);
            ownerCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
            ownerCombo.Items.Add("");
            foreach (var p in KnownValues.SpawnPlayers) ownerCombo.Items.Add(p);
            ownerCombo.SelectedItem = mo.Owner ?? "";
            ownerCombo.SelectionChanged += (_, _) =>
            {
                var newOwner = ownerCombo.SelectedItem as string;
                bool hasOwner = !string.IsNullOrEmpty(newOwner);

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

                // Owner set separately from spawn must not reassign spawn/owner
                if (playerCombo != null) playerCombo.SelectedItem = mo.Spawn ?? "";
                if (ownerCombo != null) ownerCombo.SelectedItem = mo.Owner ?? "";

                UpdateGuardFieldsVisibility();
                MirrorMarkDirty(z);
            };
            ownerPanel.Children.Add(ownerCombo);
            panel.Children.Add(ownerPanel);
            panel.Children.Add(removeGuardCheck);

            // Object type label + selector — placed BELOW the "remove guard if owned" flag (per UI spec).
            AddSectionLabel(L("S.EC.MoType"), panel);
            panel.Children.Add(typeCombo);

            // Spawn field panel (only for Spawn type, placed after removeGuardCheck so the handler can reference it)
            var spawnPanel = new StackPanel { Visibility = mo.Type == "Spawn" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel(L("S.EC.Spawn"), spawnPanel);
            playerCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 200 };
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
                    EnsureUniqueSpawns(Zones);
                    SyncZoneLayoutForSpawn(z, mo.Spawn);
                    if (playerCombo != null) playerCombo.SelectedItem = mo.Spawn ?? "";
                    if (ownerCombo != null) ownerCombo.SelectedItem = mo.Owner ?? "";
                    UpdateGuardFieldsVisibility();
                    MirrorMarkDirty(z);
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
            placeCombo.SelectionChanged += (_, _) => { if (placeCombo.SelectedItem is string s) { mo.Placement = s; MirrorMarkDirty(z); } };
            placePanel.Children.Add(placeCombo);

            // PlacementArgs (textbox for Uniform/other)
            var placeArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            AddSectionLabel("PlacementArgs", placeArgsPanel);
            var placeArgsBox = new TextBox { Text = mo.PlacementArgs is { Count: > 0 } ? string.Join(", ", mo.PlacementArgs) : "" };
            placeArgsBox.LostFocus += (_, _) =>
            {
                var text = placeArgsBox.Text.Trim();
                mo.PlacementArgs = text.Length > 0 ? new List<string>(text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : null;
                MirrorMarkDirty(z);
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
                    MirrorMarkDirty(z);
                }
            };
            connArgsPanel.Children.Add(connArgsCombo);

            // NearZone zone selector
            var nearZonePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Visibility = mo.Placement == "NearZone" ? Visibility.Visible : Visibility.Collapsed };
            AddSectionLabel("NearZone", nearZonePanel);
            var nearZoneCombo = BuildNearZoneCombo(mo);
            nearZonePanel.Children.Add(nearZoneCombo);

            // Show/hide args based on placement selection
            void UpdatePlacementVisibility(string? sel)
            {
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
            }

            placeCombo.SelectionChanged += (_, _) =>
            {
                if (placeCombo.SelectedItem is string s)
                {
                    mo.Placement = s;
                    MirrorMarkDirty(z);
                    UpdatePlacementVisibility(s);
                }
            };

            placementFields.Add(placePanel);
            placementFields.Add(placeArgsPanel);
            placementFields.Add(connArgsPanel);
            placementFields.Add(nearZonePanel);

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
                // The placement section group (Placement combo) follows the type; the
                // Connection / NearZone / PlacementArgs sub-panels are gated purely
                // on the current Placement value via UpdatePlacementVisibility.
                placePanel.Visibility = showPlacement ? Visibility.Visible : Visibility.Collapsed;
                UpdatePlacementVisibility(mo.Placement);

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

            // Add all controls to panel in order (typeCombo + its label already added above the spawn panel)
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
                MirrorMarkDirty(z);
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
                typeCombo.SelectionChanged += (_, _) => { if (typeCombo.SelectedItem is string s) { mo.Type = s; MirrorMarkDirty(z); RefreshNode(z); } };
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
                        MirrorMarkDirty(z);
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
                gcBox.LostFocus += (_, _) => { if (double.TryParse(gcBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardChance = d; MirrorMarkDirty(z); };
                var gvBox = new TextBox { Text = (mo.GuardValue ?? 0).ToString(), Margin = new Thickness(4, 0, 0, 4) };
                gvBox.LostFocus += (_, _) => { if (int.TryParse(gvBox.Text, out var v)) mo.GuardValue = v; MirrorMarkDirty(z); };
                var gwBox = new TextBox { Text = (mo.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 4, 4) };
                gwBox.LostFocus += (_, _) => { if (double.TryParse(gwBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardWeeklyIncrement = d; MirrorMarkDirty(z); };
                var buildCombo = new ComboBox { IsEditable = false, Margin = new Thickness(4, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var b in KnownValues.BuildingsConstructionSids) buildCombo.Items.Add(b);
                buildCombo.SelectedItem = mo.BuildingsConstructionSid ?? "";
                buildCombo.SelectionChanged += (_, _) => { if (buildCombo.SelectedItem is string s) { mo.BuildingsConstructionSid = s; MirrorMarkDirty(z); } };
                var facCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 4, 4), MaxDropDownHeight = 200 };
                foreach (var f in KnownValues.SelectorTypes) facCombo.Items.Add(f);
                facCombo.SelectedItem = mo.Faction?.Type ?? "";
                facCombo.SelectionChanged += (_, _) => { if (facCombo.SelectedItem is string s) { if (mo.Faction == null) mo.Faction = new TypedSelector(); mo.Faction.Type = s; MirrorMarkDirty(z); } };
                var placeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(4, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var p in KnownValues.MainObjectPlacements) placeCombo.Items.Add(p);
                placeCombo.SelectedItem = mo.Placement ?? "";
                placeCombo.SelectionChanged += (_, _) => { if (placeCombo.SelectedItem is string s) { mo.Placement = s; MirrorMarkDirty(z); } };

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
                        MirrorMarkDirty(z);
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
                gcBox.LostFocus += (_, _) => { if (double.TryParse(gcBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardChance = d; MirrorMarkDirty(z); };
                gcPanel.Children.Add(gcBox);
                guardFields.Add(gcPanel);

                // Guard value
                var gvPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoGuardValue"), gvPanel);
                var gvBox = new TextBox { Text = (mo.GuardValue ?? 0).ToString(), Margin = new Thickness(0, 0, 0, 4) };
                gvBox.LostFocus += (_, _) => { if (int.TryParse(gvBox.Text, out var v)) mo.GuardValue = v; MirrorMarkDirty(z); };
                gvPanel.Children.Add(gvBox);
                guardFields.Add(gvPanel);

                // Guard weekly increment
                var gwPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoGuardWeeklyInc"), gwPanel);
                var gwBox = new TextBox { Text = (mo.GuardWeeklyIncrement ?? 0).ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 0, 4) };
                gwBox.LostFocus += (_, _) => { if (double.TryParse(gwBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) mo.GuardWeeklyIncrement = d; MirrorMarkDirty(z); };
                gwPanel.Children.Add(gwBox);
                guardFields.Add(gwPanel);

                // Buildings construction
                var buildPanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoBuildings"), buildPanel);
                var buildCombo = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var b in KnownValues.BuildingsConstructionSids) buildCombo.Items.Add(b);
                buildCombo.Text = mo.BuildingsConstructionSid ?? "";
                buildCombo.LostFocus += (_, _) => { mo.BuildingsConstructionSid = buildCombo.Text.Trim(); MirrorMarkDirty(z); };
                buildCombo.SelectionChanged += (_, _) => { if (buildCombo.SelectedItem is string s) { mo.BuildingsConstructionSid = s; MirrorMarkDirty(z); } };
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
                        MirrorMarkDirty(z);
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

                    mo.Owner = hasOwner ? newOwner : null;

                    // Owner set separately from spawn must not reassign spawn/owner
                    ownerCombo.SelectedItem = mo.Owner ?? "";

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
                    MirrorMarkDirty(z);
                };
                ownerPanel.Children.Add(ownerCombo);

                // Placement
                var placePanel = new StackPanel();
                AddSectionLabel(L("S.EC.MoPlacement"), placePanel);
                var placeCombo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 4), MaxDropDownHeight = 200 };
                foreach (var p in KnownValues.MainObjectPlacements) placeCombo.Items.Add(p);
                placeCombo.SelectedItem = mo.Placement ?? "";
                placeCombo.SelectionChanged += (_, _) => { if (placeCombo.SelectedItem is string s) { mo.Placement = s; MirrorMarkDirty(z); } };
                placePanel.Children.Add(placeCombo);

                // PlacementArgs (textbox for Uniform/other)
                var placeArgsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
                AddSectionLabel("PlacementArgs", placeArgsPanel);
                var placeArgsBox = new TextBox { Text = mo.PlacementArgs is { Count: > 0 } ? string.Join(", ", mo.PlacementArgs) : "" };
                placeArgsBox.LostFocus += (_, _) =>
                {
                    var text = placeArgsBox.Text.Trim();
                    mo.PlacementArgs = text.Length > 0 ? new List<string>(text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : null;
                    MirrorMarkDirty(z);
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
                        MirrorMarkDirty(z);
                    }
                };
                connArgsPanel.Children.Add(connArgsCombo);

                // NearZone zone selector
                var nearZonePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = mo.Placement == "NearZone" ? Visibility.Visible : Visibility.Collapsed };
                AddSectionLabel("NearZone", nearZonePanel);
                var nearZoneCombo = BuildNearZoneCombo(mo);
                nearZonePanel.Children.Add(nearZoneCombo);

                // Show/hide args based on placement selection
                void UpdatePlacementVisibility(string? sel)
                {
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
                }

                placeCombo.SelectionChanged += (_, _) =>
                {
                    if (placeCombo.SelectedItem is string s)
                    {
                        mo.Placement = s;
                        MirrorMarkDirty(z);
                        UpdatePlacementVisibility(s);
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
                    // Gate the placement section via the placementFields list (indices:
                    // 0 = Placement combo, 1 = PlacementArgs, 2 = Connection, 3 = NearZone)
                    // so we don't capture panels declared later in this method.
                    var sel = mo.Placement;
                    placementFields[0].Visibility = showPlacement ? Visibility.Visible : Visibility.Collapsed;
                    placementFields[1].Visibility = sel == "Center" || sel == "NearZone" || sel == "Connection" ? Visibility.Collapsed : Visibility.Visible;
                    placementFields[2].Visibility = sel == "Connection" ? Visibility.Visible : Visibility.Collapsed;
                    placementFields[3].Visibility = sel == "NearZone" ? Visibility.Visible : Visibility.Collapsed;
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
                        MirrorMarkDirty(z);
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

                MirrorMarkDirty(z);
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
                            MirrorMarkDirty(z);
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

        /// <summary>
        /// Like <see cref="AddStringListPicker"/> but the "add" control opens the categorized
        /// pool-content viewer in selection mode, restricted to the given <paramref name="allowedCategories"/>
        /// (e.g. guarded/unguarded/random/specific-template/created for the Guarded &amp; Unguarded
        /// pickers, resources for the resources picker, specific-template for mandatory/limits).
        /// Pools are sourced from the full universe (all game templates' mandatory_content /
        /// content_count_limits plus game &amp; created pools).
        /// </summary>
        private void AddCategoryPicker(string label, List<string> allowedCategories, List<string>? current, Action<List<string>> onCommit, Panel panel)
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

            var addBtn = new System.Windows.Controls.Button
            {
                Content = "➕ Добавить пул…",
                FontSize = 11,
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4),
            };
            addBtn.Click += (_, _) =>
            {
                var viewer = new ContentPoolViewerWindow(selectionMode: true, allowedCategories: allowedCategories, selected: current);
                viewer.Owner = this;
                if (viewer.ShowDialog() == true)
                {
                    var list = current != null ? new List<string>(current) : new List<string>();
                    foreach (var s in viewer.SelectedPools)
                        if (!list.Contains(s)) list.Add(s);
                    onCommit(list);
                    BuildInspector();
                }
            };
            innerPanel.Children.Add(addBtn);

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
                if (zone.MainObjects == null || zone.MainObjects.Count == 0) continue;
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

                // Part C2: every MainObject (Uniform/Default/Center/…) → road connection that
                // touches this zone. Mirrors H3TParser.AddRoadToZone so a Uniform MO gets a
                // road reaching it (not just Connection- or Center-placed MOs). Connection-placed
                // MOs are handled by the per-MO loop below, so skip them here.
                foreach (var conn in Connections)
                {
                    if (conn.Road != true || conn.Name == null) continue;
                    if (!string.Equals(conn.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(conn.To, zone.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    for (int i = 0; i < zone.MainObjects.Count; i++)
                    {
                        var mo = zone.MainObjects[i];
                        if (mo.Placement == "Connection") continue;
                        AddUniqueRoad(zone, "MainObject", [i.ToString()], "Connection", [conn.Name]);
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

        /// <summary>
        /// Builds the road for a castle-less zone given its sorted incident connection names.
        /// Returns null when no road should be added (no incident connections, or this connection
        /// is the star anchor). A single incident connection keeps the legacy self-referencing loop.
        /// Pure/testable — does not touch editor state.
        /// </summary>
        public static Road? BuildCastleLessRoad(string connectionName, IReadOnlyList<string> incidentConnections)
        {
            if (incidentConnections.Count < 2)
            {
                if (incidentConnections.Count == 1)
                {
                    return new Road
                    {
                        Type = "Stone",
                        From = new RoadEndpoint { Type = "Connection", Args = [connectionName] },
                        To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
                    };
                }
                return null;
            }

            string anchor = incidentConnections[0];
            // The anchor connection only gets spokes; never a self-loop.
            if (string.Equals(connectionName, anchor, StringComparison.OrdinalIgnoreCase))
                return null;

            return new Road
            {
                Type = "Stone",
                From = new RoadEndpoint { Type = "Connection", Args = [anchor] },
                To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
            };
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
                // Castle-less zone (e.g. a hub): build a star among the zone's road connections only.
                // A connection participates in the star solely when its Road flag is set, so toggling
                // one connection never promotes sibling connections of the same zone to Road (the
                // phantom-anchor bleed). Each road links two distinct road connections — never a self-loop.
                var incident = RoadIncidentConnections(zone, Connections)
                    .Distinct()
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList();

                newRoad = BuildCastleLessRoad(connectionName, incident);
                if (newRoad == null)
                    return;
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

        /// <summary>
        /// Removes every road in the zone that references the given connection (From/To == Connection[connectionName]).
        /// Pure with respect to editor state aside from the zone's road list.
        /// </summary>
        private static void RemoveConnectionRoads(Zone? zone, string connectionName)
        {
            if (zone?.Roads == null) return;
            zone.Roads.RemoveAll(r =>
            {
                if (r.From?.Type == "Connection" && r.From.Args?.FirstOrDefault() == connectionName) return true;
                if (r.To?.Type == "Connection" && r.To.Args?.FirstOrDefault() == connectionName) return true;
                return false;
            });
        }

        /// <summary>
        /// Rebuilds the castle-less hub star for a zone from the connections that currently have Road==true.
        /// Castle zones are left untouched (their roads are explicit MainObject->Connection links). This is
        /// used after a connection's Road flag is toggled OFF so the star reflects only surviving road links.
        /// </summary>
        private void RebuildCastleLessStar(Zone? zone)
        {
            if (zone == null || zone.Roads == null) return;
            int castleCount = zone.MainObjects?.Count(o => o.Type == "City" || o.Type == "AbandonedOutpost") ?? 0;
            if (castleCount > 0) return;

            zone.Roads.RemoveAll(r => r.From?.Type == "Connection" || r.To?.Type == "Connection");
            var roadConns = Connections
                .Where(c => c.Road == true
                         && (string.Equals(c.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(c.To, zone.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            foreach (var cn in roadConns) AddRoadToZone(zone, cn);
        }

        /// <summary>
        /// Returns the names of connections that touch the given zone AND have Road==true. Used to build
        /// the castle-less hub star so that a connection only ever participates when its Road flag is set —
        /// this prevents toggling one connection from promoting a sibling connection to Road (the
        /// phantom-anchor bleed observed in mirror mode). Public/static for unit testing.
        /// </summary>
        public static IEnumerable<string> RoadIncidentConnections(Zone zone, IEnumerable<Connection> connections)
        {
            if (zone?.Name == null) yield break;
            foreach (var c in connections)
            {
                if (c.Road == true && !string.IsNullOrEmpty(c.Name)
                    && (string.Equals(c.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(c.To, zone.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return c.Name!;
            }
        }

        /// <summary>
        /// Ensures every non-empty <c>spawn</c> argument is unique across all zones. Duplicates are
        /// automatically reassigned to the first free value from the canonical 9-value set
        /// (empty + Player1..Player8). For the FIRST main object of a zone, the matching <c>owner</c>
        /// is kept in sync with the (possibly reassigned) spawn. Called live from the spawn/owner
        /// handlers and once before save so exported templates never contain duplicate spawn args.
        /// </summary>
        public static void EnsureUniqueSpawns(RmgTemplate template)
        {
            if (template?.Variants == null) return;
            foreach (var v in template.Variants)
                if (v?.Zones != null) EnsureUniqueSpawns(v.Zones);
        }

        /// <summary>Normalizes spawn uniqueness across the given zones (see the template overload).</summary>
        public static void EnsureUniqueSpawns(IEnumerable<Zone> zones)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var z in zones)
            {
                if (z?.MainObjects == null) continue;
                bool isFirst = true;
                foreach (var mo in z.MainObjects)
                {
                    if (string.IsNullOrEmpty(mo.Spawn)) { isFirst = false; continue; }
                    string finalSpawn;
                    if (taken.Contains(mo.Spawn))
                    {
                        // Reassign to the first free player from Player1..Player8, else leave empty.
                        var free = KnownValues.SpawnPlayers.FirstOrDefault(p => !taken.Contains(p));
                        finalSpawn = free; // null when all 8 are already taken
                        if (!string.IsNullOrEmpty(finalSpawn)) taken.Add(finalSpawn);
                    }
                    else
                    {
                        finalSpawn = mo.Spawn;
                        taken.Add(mo.Spawn);
                    }
                    mo.Spawn = finalSpawn;
                    // Sync owner to spawn only for the first main object and only when owner is still empty
                    // (an explicitly-set owner must not be overwritten by spawn reassignment).
                    if (isFirst && finalSpawn != null && string.IsNullOrEmpty(mo.Owner))
                        mo.Owner = finalSpawn;
                    isFirst = false;
                }
            }
        }

        /// <summary>
        /// Keeps the zone <c>layout</c> in sync with its spawn: a non-empty spawn sets
        /// <c>zone_layout_spawn</c>; clearing the spawn reverts it only if it still holds that value.
        /// </summary>
        private void SyncZoneLayoutForSpawn(Zone z, string? spawn)
        {
            if (!string.IsNullOrEmpty(spawn))
                z.Layout = "zone_layout_spawn";
            else if (z.Layout == "zone_layout_spawn")
                z.Layout = null;
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
            if (_mirrorMap.TryGetValue(old, out var partner))
            {
                _mirrorMap.Remove(old);
                _mirrorMap.Remove(partner);
                _mirrorMap[newName] = partner;
                _mirrorMap[partner] = newName;
            }
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
                _dragZoneLocked = _lockedZones.Contains(z.Name);
                _dragLeftSide = _positions[z.Name].X < GraphCanvas.Width / 2;
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
            _mousePosOnCanvas = e.GetPosition(GraphCanvas);
            if (_dragZone is not null && e.LeftButton == MouseButtonState.Pressed)
            {
                if (_dragZoneLocked) return; // immovable (e.g. hub)
                var p = e.GetPosition(GraphCanvas) - _dragGrab;
                p = SnapToGrid(p);
                if (_mirrorMode)
                {
                    double mid = GraphCanvas.Width / 2;
                    double r = NodeRadius(_dragZone);
                    double min = r, max = GraphCanvas.Width - r;
                    p.X = Math.Max(min, Math.Min(max, p.X));
                    // Keep the zone on its own side of the mirror axis.
                    p.X = _dragLeftSide ? Math.Min(p.X, mid) : Math.Max(p.X, mid);
                }
                _positions[_dragZone.Name] = p;
                _movedWhileDragging = true;
                RepositionZone(_dragZone, p);
                if (_mirrorMode && !_applyingMirror &&
                    _mirrorMap.TryGetValue(_dragZone.Name, out var mirrorName))
                {
                    var mz = GetZoneByName(mirrorName);
                    if (mz is not null)
                    {
                        _applyingMirror = true;
                        try
                        {
                            var mp = Mirror(p);
                            _positions[mirrorName] = mp;
                            RepositionZone(mz, mp);
                        }
                        finally { _applyingMirror = false; }
                    }
                }
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
                GuardValue = 3000,
                GuardWeeklyIncrement = 0.15,
                GuardRandomization = 0.05,
                Length = 1,
                GuardEscape = false,
                SimTurnSquad = true,
                GatePlacement = "Center",
            };
            if (connType == "Proximity") conn.Length = 0.94;
            Connections.Add(conn);
            if (_mirrorMode && _mirrorConnections) MirrorConnection(conn);
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

        private void BtnGridBigger_Click(object sender, RoutedEventArgs e)
            => ChangeGridSize(+GridSizeStep);

        private void BtnGridSmaller_Click(object sender, RoutedEventArgs e)
            => ChangeGridSize(-GridSizeStep);

        private void ChangeGridSize(double delta)
        {
            double next = Math.Clamp(_gridSize + delta, GridSizeMin, GridSizeMax);
            if (next == _gridSize) return;
            _gridSize = next;
            UpdateGridSizeLabel();
            RebuildGraph(); // redraw the grid at the new cell size
        }

        private void UpdateGridSizeLabel()
        {
            // grid size indicator removed
        }

        // ── Mirror creation mode (vertical left/right split) ───────────────────────
        private void BtnMirror_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MirrorSettingsWindow(_mirrorMode, _mirrorProperties, _mirrorConnections);
            dlg.Owner = this;
            if (dlg.ShowDialog() != true) return;

            _mirrorProperties = dlg.MirrorProps;
            _mirrorConnections = dlg.MirrorConns;
            if (dlg.Enable)
            {
                // Turn on mirror creation from a clean slate: wipe the canvas and all of its
                // contained information so nothing stale re-surfaces later as a mirrored twin.
                ClearCanvasForMirror();
                _mirrorMode = true;
                MarkDirty();
                RebuildGraph();
            }
            else
            {
                DisableMirrorMode();
            }
            BtnMirror.Background = _mirrorMode
                ? new SolidColorBrush(Color.FromRgb(40, 60, 40))
                : null;
            UpdateStatus(_mirrorMode ? L("S.EC.MirrorOn") : L("S.EC.MirrorOff"));
        }

        private Point Mirror(Point p) => new(GraphCanvas.Width - p.X, p.Y);

        private Zone? GetZoneByName(string n) => Zones.FirstOrDefault(z => z.Name == n);

        private Zone CloneZone(Zone src, Point pos)
        {
            var clone = JsonSerializer.Deserialize<Zone>(
                JsonSerializer.Serialize(src, JsonExport.Options), JsonExport.Options)!;
            clone.Name = UniqueZoneName();
            // Spawn is a unique per-zone argument — do not copy it to the mirror twin
            // (it gets a unique one via EnsureUniqueSpawns on save).
            if (clone.MainObjects != null)
            {
                for (int i = 0; i < clone.MainObjects.Count; i++)
                {
                    clone.MainObjects[i].Spawn = null;
                    if (i == 0 && clone.MainObjects[i].Type == "City")
                        clone.MainObjects[i].Owner = null;
                }
            }
            Zones.Add(clone);
            _positions[clone.Name] = pos;
            return clone;
        }

        /// <summary>
        /// Fully clears the canvas (zones, connections, positions and all mirror state) so that
        /// enabling mirror creation starts from a blank slate and no previous information reappears.
        /// </summary>
        private void ClearCanvasForMirror()
        {
            RemoveMirrorDivider();
            Zones.Clear();
            _positions.Clear();
            _mirrorMap.Clear();
            _connectionMirrorMap.Clear();
            _lockedZones.Clear();
            if (Variant.Connections != null) Variant.Connections.Clear();
            GraphCanvas.Children.Clear();
            _mirrorBand = null;
        }

        private void DisableMirrorMode()
        {
            RemoveMirrorDivider();
            // Keep _mirrorMap / _connectionMirrorMap / _lockedZones so existing twins persist
            // and re-enabling the mode won't create duplicates.
            _mirrorMode = false;
        }

        private void DrawMirrorDivider()
        {
            if (_mirrorDivider is not null) return;
            double midX = GraphCanvas.Width / 2;

            // Subtle full-height band so the split is clearly visible
            var band = new System.Windows.Shapes.Rectangle
            {
                Width = 10,
                Height = GraphCanvas.Height,
                Fill = new SolidColorBrush(Color.FromRgb(200, 168, 87)),
                Opacity = 0.12,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(band, midX - 5);
            Canvas.SetTop(band, 0);
            Panel.SetZIndex(band, -2);

            _mirrorDivider = new Line
            {
                X1 = midX,
                Y1 = 0,
                X2 = midX,
                Y2 = GraphCanvas.Height,
                Stroke = new SolidColorBrush(Color.FromRgb(200, 168, 87)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                IsHitTestVisible = false,
            };
            Panel.SetZIndex(_mirrorDivider, -1);

            var label = new TextBlock
            {
                Text = L("S.EC.Mirror"),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 168, 87)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(label, midX + 6);
            Canvas.SetTop(label, 8);
            Panel.SetZIndex(label, -1);
            _mirrorDividerLabel = label;

            _mirrorBand = band;
            GraphCanvas.Children.Add(band);
            GraphCanvas.Children.Add(_mirrorDivider);
            GraphCanvas.Children.Add(label);
        }

        private void RemoveMirrorDivider()
        {
            if (_mirrorDivider is null) return;
            GraphCanvas.Children.Remove(_mirrorDivider);
            _mirrorDivider = null;
            if (_mirrorDividerLabel is not null)
            {
                GraphCanvas.Children.Remove(_mirrorDividerLabel);
                _mirrorDividerLabel = null;
            }
            if (_mirrorBand is not null)
            {
                GraphCanvas.Children.Remove(_mirrorBand);
                _mirrorBand = null;
            }
        }

        private void MirrorZoneProperties(Zone src)
        {
            if (!_mirrorMode || !_mirrorProperties) return;
            if (!_mirrorMap.TryGetValue(src.Name, out var twinName)) return;
            var twin = GetZoneByName(twinName);
            if (twin is null) return;
            // Capture the twin's own Spawn values (unique per-zone argument — must NOT be mirrored)
            var twinSpawns = twin.MainObjects?.Select(mo => mo.Spawn).ToList();
            // Deep-clone so the twin gets its own independent lists (e.g. MainObjects)
            var clone = JsonSerializer.Deserialize<Zone>(
                JsonSerializer.Serialize(src, JsonExport.Options), JsonExport.Options)!;
            clone.Name = twin.Name;
            foreach (var prop in typeof(Zone).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite || prop.Name == nameof(Zone.Name)) continue;
                prop.SetValue(twin, prop.GetValue(clone));
            }
            ApplyAutoLogicToTwin(twin);
            RemapMirrorReferences(twin);
            // Restore the twin's own Spawn (unique per-zone argument — must NOT be mirrored).
            // Owner is intentionally NOT synced here (see the "Same owner" zone flag instead).
            if (twin.MainObjects != null && twinSpawns != null && twin.MainObjects.Count == twinSpawns.Count)
            {
                for (int i = 0; i < twin.MainObjects.Count; i++)
                    twin.MainObjects[i].Spawn = twinSpawns[i];
            }
            RebuildGraph();
        }

        /// <summary>
        /// Re-points the twin's cross-references (MainObject placement args and zone roads)
        /// from the source zone's connections/zones to the twin's mirrored counterparts.
        /// </summary>
        private void RemapMirrorReferences(Zone twin)
        {
            if (twin.MainObjects != null)
            {
                foreach (var mo in twin.MainObjects)
                {
                    if (mo.PlacementArgs is { Count: > 0 })
                    {
                        var arg = mo.PlacementArgs[0];
                        if (mo.Placement == "Connection" && _connectionMirrorMap.TryGetValue(arg, out var mc))
                            mo.PlacementArgs[0] = mc;
                        else if (mo.Placement == "NearZone" && _mirrorMap.TryGetValue(arg, out var mz))
                            mo.PlacementArgs[0] = mz;
                    }
                }
            }
            if (twin.Roads != null)
            {
                foreach (var r in twin.Roads)
                {
                    if (r.To != null && r.To.Args is { Count: > 0 })
                    {
                        var arg = r.To.Args[0];
                        if (r.To.Type == "Connection" && _connectionMirrorMap.TryGetValue(arg, out var mc))
                            r.To.Args = [mc];
                        else if (r.To.Type == "Zone" && _mirrorMap.TryGetValue(arg, out var mz))
                            r.To.Args = [mz];
                    }
                    if (r.From != null && r.From.Args is { Count: > 0 })
                    {
                        var arg = r.From.Args[0];
                        if (r.From.Type == "Connection" && _connectionMirrorMap.TryGetValue(arg, out var mc))
                            r.From.Args = [mc];
                        else if (r.From.Type == "Zone" && _mirrorMap.TryGetValue(arg, out var mz))
                            r.From.Args = [mz];
                    }
                }
            }
        }

        /// <summary>
        /// Re-points a zone's Owner/Spawn to free players, mirroring the conflict-resolution
        /// done by the inspector combo and PasteCopiedZone. <paramref name="used"/> is the set of
        /// already-taken players (Owner+Spawn) across OTHER zones; it is updated in place as
        /// assignments are made. Applies the same side effects (RemoveGuardIfHasOwner, cleared
        /// guards, Match faction). Pure w.r.t. UI — does NOT rename the zone. Returns true if any
        /// reassignment happened.
        /// </summary>
        public static bool ResolvePlayerConflicts(Zone zone, HashSet<string> used)
        {
            if (zone.MainObjects == null) return false;
            bool changed = false;
            foreach (var mo in zone.MainObjects)
            {
                // Clear non-applicable player args (mirror of ValidateZone)
                if (mo.Type != "City") mo.Owner = null;
                if (mo.Type != "Spawn") mo.Spawn = null;

                // Spawn conflict resolution (owner auto-assignment was removed)
                if (!string.IsNullOrEmpty(mo.Spawn))
                {
                    if (used.Contains(mo.Spawn))
                    {
                        var free = KnownValues.SpawnPlayers.FirstOrDefault(p => !used.Contains(p));
                        if (free != null)
                        {
                            used.Remove(mo.Spawn);
                            mo.Spawn = free;
                            used.Add(free);
                            changed = true;
                        }
                        else
                        {
                            mo.Spawn = null;
                            changed = true;
                        }
                    }
                    else
                    {
                        used.Add(mo.Spawn);
                    }
                    if (!string.IsNullOrEmpty(mo.Spawn))
                    {
                        mo.RemoveGuardIfHasOwner = true;
                        mo.GuardChance = null;
                        mo.GuardValue = null;
                        mo.GuardWeeklyIncrement = null;
                    }
                }
            }
            return changed;
        }

        /// <summary>Inherits spawn conflict resolution for a twin zone (owner/spawn copied via deep clone).</summary>
        private void ApplyAutoLogicToTwin(Zone twin)
        {
            if (twin.MainObjects == null) return;
            ResolvePlayerConflicts(twin, GetUsedPlayers(twin.Name));
        }

        private void MirrorMarkDirty(Zone z)
        {
            MirrorZoneProperties(z);
            MarkDirty();
        }

        private void MirrorConnection(Connection src)
        {
            if (!_mirrorMode || !_mirrorConnections) return;
            // Map each endpoint to its twin if it has one; otherwise keep the endpoint as-is
            // (e.g. a hub, which is intentionally not mirrored).
            string fromTwin = _mirrorMap.TryGetValue(src.From, out var ft) ? ft : src.From;
            string toTwin   = _mirrorMap.TryGetValue(src.To,   out var tt) ? tt : src.To;
            // Nothing to mirror if neither endpoint has a twin (e.g. hub-to-hub).
            if (fromTwin == src.From && toTwin == src.To) return;
            // Skip the connection that already links a zone to its own mirror.
            if (fromTwin == src.To || toTwin == src.From) return;
            // Skip if an equivalent mirrored connection already exists.
            if (Connections.Any(c =>
                    (c.From == fromTwin && c.To == toTwin) ||
                    (c.From == toTwin && c.To == fromTwin))) return;

            var copy = JsonSerializer.Deserialize<Connection>(
                JsonSerializer.Serialize(src, JsonExport.Options), JsonExport.Options)!;
            string name = $"{src.ConnectionType}-{fromTwin}-{toTwin}";
            while (Connections.Any(c => c.Name == name)) name += "_";
            copy.Name = name;
            copy.From = fromTwin;
            copy.To = toTwin;
            Connections.Add(copy);
            _connectionMirrorMap[src.Name] = copy.Name;
            _connectionMirrorMap[copy.Name] = src.Name;
        }

        private void MirrorConnectionMarkDirty(Connection c)
        {
            if (!_mirrorMode || !_mirrorConnections) return;
            if (!_connectionMirrorMap.TryGetValue(c.Name, out var twinName)) return;
            var twin = Connections.FirstOrDefault(x => x.Name == twinName);
            if (twin is null) return;
            foreach (var prop in typeof(Connection).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite ||
                    prop.Name == nameof(Connection.Name) ||
                    prop.Name == nameof(Connection.From) ||
                    prop.Name == nameof(Connection.To)) continue;
                prop.SetValue(twin, prop.GetValue(c));
            }
            MarkDirty();
        }

        /// <summary>Snaps a point to the nearest grid intersection.</summary>
        private Point SnapToGrid(Point p)
        {
            if (!_gridSnap) return p;
            return new Point(
                Math.Round(p.X / _gridSize) * _gridSize,
                Math.Round(p.Y / _gridSize) * _gridSize
            );
        }

        private void BtnAddZone_Click(object sender, RoutedEventArgs e) => AddZoneAt(ViewportCenterInCanvas());

        /// <summary>Adds a new zone at the given canvas position and selects it.</summary>
        /// <summary>Builds a new zone with editor defaults, registers it, and returns it.</summary>
        private Zone CreateZone(string name, Point pos)
        {
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
                DiplomacyModifier = -0.5,
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
            return z;
        }

        /// <summary>Adds a new zone at the given canvas position and selects it.</summary>
        private void AddZoneAt(Point pos)
        {
            string name = UniqueZoneName();
            var z = CreateZone(name, pos);
            if (_mirrorMode && !_applyingMirror)
            {
                _applyingMirror = true;
                try
                {
                    var m = CloneZone(z, Mirror(pos));
                    _mirrorMap[z.Name] = m.Name;
                    _mirrorMap[m.Name] = z.Name;
                }
                finally { _applyingMirror = false; }
            }
            MarkDirty();
            RebuildGraph();
            Select(z);
            UpdateStatus(L("S.EC.ZoneAdded", name));
        }

        /// <summary>Creates the immovable shared "hub" zone at the graph center (never mirrored).</summary>
        public void CreateHubZoneExternally()
        {
            string name = UniqueZoneName("hub");
            var z = CreateZone(name, new Point(GraphCanvas.Width / 2, GraphCanvas.Height / 2));
            z.Layout = "zone_layout_center";
            _lockedZones.Add(z.Name);
            MarkDirty();
            RebuildGraph();
            Select(z);
            UpdateStatus(L("S.EC.HubCreated", name));
        }

        private string UniqueZoneName()
        {
            for (int i = 1; ; i++)
            {
                string n = $"Zone-{i}";
                if (!Zones.Any(z => string.Equals(z.Name, n, StringComparison.Ordinal))) return n;
            }
        }

        /// <summary>Generates a unique zone name based on a preferred base name.</summary>
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

            string pressed = FormatKeyGesture(e.Key, Keyboard.Modifiers);
            string action = _hotkeys.FirstOrDefault(kv => kv.Value == pressed).Key;

            // Map action → handler
            switch (action)
            {
                case "CopyZone":       CopySelectedZone(); e.Handled = true; return;
                case "PasteZone":      PasteCopiedZone(); e.Handled = true; return;
                case "Delete":         BtnDelete_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "ConnectMode":    BtnConnectMode_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "Validate":       BtnValidate_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "Save":           BtnSave_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "Relayout":       BtnRelayout_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "ExportPng":      BtnExportPng_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "Mirror":         BtnMirror_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "LoadTemplate":   BtnLoad_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "AddZone":        AddZoneAt(_mousePosOnCanvas); e.Handled = true; return;
                case "CopyConnections":BtnCopyConnections_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "CopyConnectionProps": CopyConnectionProps(); e.Handled = true; return;
                case "PasteConnectionProps": PasteConnectionProps(); e.Handled = true; return;
                case "ConnManager":    BtnConnectionManager_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "Orientation":    BtnOrientation_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "JsonPreview":    BtnJsonPreview_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "GridSnap":       BtnGridSnap_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "ZoomIn":         BtnZoomIn_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "ZoomOut":        BtnZoomOut_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case "ZoomReset":      BtnZoomReset_Click(this, new RoutedEventArgs()); e.Handled = true; return;
            }

            // Built-in fallbacks (always active)
            if (e.Key == Key.Escape)
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

        private static string FormatKeyGesture(Key key, ModifierKeys mod)
        {
            var parts = new List<string>();
            if (mod.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mod.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mod.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin))
                parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        private void BtnHotkeys_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new HotkeySettingsWindow(_hotkeys) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _hotkeys = dlg.GetHotkeys();
                UpdateHotkeyLabels();
                UpdateStatus("Горячие клавиши обновлены");

                // Сохраняем в config.json
                var cfg = Services.ConfigJson.Current;
                foreach (var kv in _hotkeys)
                    cfg.Hotkeys[kv.Key] = kv.Value;
                cfg.Save();
            }
        }

        private readonly Dictionary<Button, TextBlock> _hotkeyLabels = new();

        private void AddHotkeyLabelsToToolbar()
        {
            var map = new (Button btn, string action)[]
            {
                (BtnLoad, "LoadTemplate"), (BtnAddZone, "AddZone"),
                (BtnCopyZone, "CopyZone"), (BtnPasteZone, "PasteZone"),
                (BtnConnectMode, "ConnectMode"), (BtnDelete, "Delete"),
                (BtnSave, "Save"), (BtnExportPng, "ExportPng"),
                (BtnOrientation, "Orientation"), (BtnMirror, "Mirror"),
                (BtnConnectionManager, "ConnManager"),
                (BtnValidate, "Validate"), (BtnJsonPreview, "JsonPreview"),
                (BtnGridSnap, "GridSnap"), (BtnRelayout, "Relayout"),
            };
            foreach (var (btn, action) in map)
            {
                string key = _hotkeys.TryGetValue(action, out var v) ? v : "";
                if (string.IsNullOrEmpty(key)) continue;

                // Inject hotkey label INSIDE the button at the bottom edge
                var originalContent = (btn.Content as string) ?? "";
                var innerStack = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                innerStack.Children.Add(new TextBlock
                {
                    Text = originalContent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, -2),
                });
                innerStack.Children.Add(new TextBlock
                {
                    Text = key,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontSize = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, -2, 0, 0),
                });
                btn.Content = innerStack;
            }
        }

        private void UpdateHotkeyLabels()
        {
            foreach (var (btn, lbl) in _hotkeyLabels)
                lbl.Text = "";
            // Re-apply all by calling Add again
            AddHotkeyLabelsToToolbar();
        }

        private static Dictionary<string, string> GetDefaultHotkeys() => new()
        {
            ["CopyZone"] = "Ctrl+C",
            ["PasteZone"] = "Ctrl+V",
            ["Delete"] = "Delete",
            ["ConnectMode"] = "Z",
            ["Validate"] = "Ctrl+Shift+V",
            ["Save"] = "Ctrl+S",
            ["Relayout"] = "Ctrl+R",
            ["ExportPng"] = "Ctrl+E",
            ["Mirror"] = "Ctrl+M",
            ["LoadTemplate"] = "Ctrl+D",
            ["AddZone"] = "A",
            ["CopyConnections"] = "",
            ["CopyConnectionProps"] = "Ctrl+Shift+Z",
            ["PasteConnectionProps"] = "Ctrl+Z",
            ["ConnManager"] = "Ctrl+Shift+M",
            ["Orientation"] = "Ctrl+O",
            ["JsonPreview"] = "Ctrl+J",
            ["GridSnap"] = "G",
            ["ZoomIn"] = "Ctrl+=",
            ["ZoomOut"] = "Ctrl+-",
            ["ZoomReset"] = "Ctrl+0",
        };

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

        private void PruneConnectionMirrorMap()
        {
            foreach (var name in _connectionMirrorMap.Keys.ToList())
                if (!Connections.Any(c => c.Name == name))
                    _connectionMirrorMap.Remove(name);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected is Zone z)
            {
                // Delete the twin zone (and its connections) as well.
                if (_mirrorMap.TryGetValue(z.Name, out var mPartner))
                {
                    var twin = GetZoneByName(mPartner);
                    if (twin != null)
                    {
                        foreach (var conn in Connections.Where(c => c.From == mPartner || c.To == mPartner).ToList())
                            RemoveRoadReferences(conn.Name ?? $"{conn.From}-{conn.To}");
                        Connections.RemoveAll(c => c.From == mPartner || c.To == mPartner);
                        Zones.Remove(twin);
                        _positions.Remove(mPartner);
                    }
                    _mirrorMap.Remove(z.Name);
                    _mirrorMap.Remove(mPartner);
                }
                var removed = Connections.Where(c => c.From == z.Name || c.To == z.Name).ToList();
                foreach (var conn in removed)
                    RemoveRoadReferences(conn.Name ?? $"{conn.From}-{conn.To}");
                int removedConns = Connections.RemoveAll(c => c.From == z.Name || c.To == z.Name);
                Zones.Remove(z);
                _positions.Remove(z.Name);
                PruneConnectionMirrorMap();
                MarkDirty();
                RebuildGraph();
                Select(null);
                UpdateStatus(L("S.EC.ZoneDeleted", z.Name, removedConns));
            }
            else if (_selected is Connection c)
            {
                RemoveRoadReferences(c.Name ?? $"{c.From}-{c.To}");
                Connections.Remove(c);
                if (_connectionMirrorMap.TryGetValue(c.Name, out var cPartner))
                {
                    _connectionMirrorMap.Remove(c.Name);
                    _connectionMirrorMap.Remove(cPartner);
                }
                PruneConnectionMirrorMap();
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

            // Check for duplicate Spawn across zones (owner auto-assignment was removed)
            var usedPlayers = GetUsedPlayers();

            bool hadConflict = false;
            if (copy.MainObjects != null)
            {
                foreach (var mo in copy.MainObjects)
                {
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
                        "Некоторые спавны уже были заняты в других зонах. Спавны скопированной зоны были переназначены на свободных игроков.",
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

        // ── Copy / paste connection properties ──────────────────────────────

        /// <summary>Build a vertical button content (label + hotkey hint on the bottom edge).</summary>
        private System.Windows.FrameworkElement MakeHotkeyButtonContent(string label, string action)
        {
            string key = _hotkeys.TryGetValue(action, out var v) ? v
                       : Services.ConfigJson.Current.Hotkeys.TryGetValue(action, out var c) ? c : "";
            var inner = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
            };
            inner.Children.Add(new TextBlock
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, -2),
            });
            if (!string.IsNullOrEmpty(key))
                inner.Children.Add(new TextBlock
                {
                    Text = key,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontSize = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, -2, 0, 0),
                });
            return inner;
        }

        private void CopyConnectionProps()
        {
            if (_selected is not Connection cc) { UpdateStatus(L("S.EC.SelectConnFirst")); return; }
            _copiedConnection = new Connection
            {
                ConnectionType = cc.ConnectionType,
                GuardValue = cc.GuardValue,
                GuardEscape = cc.GuardEscape,
                SimTurnSquad = cc.SimTurnSquad,
                GuardWeeklyIncrement = cc.GuardWeeklyIncrement,
                GuardMatchGroup = cc.GuardMatchGroup,
                Road = cc.Road,
                GatePlacement = cc.GatePlacement,
                GatePlacementArgs = cc.GatePlacementArgs != null ? [.. cc.GatePlacementArgs] : null,
                GuardRandomization = cc.GuardRandomization,
                Length = cc.Length,
                PortalPlacementRulesFrom = cc.PortalPlacementRulesFrom != null
                    ? [.. cc.PortalPlacementRulesFrom] : null,
                PortalPlacementRulesTo = cc.PortalPlacementRulesTo != null
                    ? [.. cc.PortalPlacementRulesTo] : null,
            };
            UpdateStatus($"Скопированы свойства связи '{cc.Name}'");
            BuildInspector();
        }

        private void PasteConnectionProps()
        {
            if (_selected is not Connection cc) { UpdateStatus(L("S.EC.SelectConnFirst")); return; }
            if (_copiedConnection is null) { UpdateStatus(L("S.EC.NothingCopied")); return; }

            cc.ConnectionType = _copiedConnection.ConnectionType;
            cc.GuardValue = _copiedConnection.GuardValue;
            cc.GuardEscape = _copiedConnection.GuardEscape;
            cc.SimTurnSquad = _copiedConnection.SimTurnSquad;
            cc.GuardWeeklyIncrement = _copiedConnection.GuardWeeklyIncrement;
            cc.GuardMatchGroup = _copiedConnection.GuardMatchGroup;
            cc.GatePlacement = _copiedConnection.GatePlacement;
            cc.GatePlacementArgs = _copiedConnection.GatePlacementArgs != null
                ? [.. _copiedConnection.GatePlacementArgs] : null;
            cc.GuardRandomization = _copiedConnection.GuardRandomization;
            cc.Length = _copiedConnection.Length;
            cc.PortalPlacementRulesFrom = _copiedConnection.PortalPlacementRulesFrom != null
                ? [.. _copiedConnection.PortalPlacementRulesFrom] : null;
            cc.PortalPlacementRulesTo = _copiedConnection.PortalPlacementRulesTo != null
                ? [.. _copiedConnection.PortalPlacementRulesTo] : null;

            bool hadRoad = cc.Road == true;
            cc.Road = _copiedConnection.Road;
            if (cc.Road == true && !hadRoad)
                AutoGenerateRoadsForConnection(cc);
            else if (cc.Road != true && hadRoad)
            {
                string connName = cc.Name ?? $"{cc.From}-{cc.To}";
                RemoveConnectionRoads(Zones.FirstOrDefault(z => z.Name == cc.From), connName);
                RemoveConnectionRoads(Zones.FirstOrDefault(z => z.Name == cc.To), connName);
                RebuildCastleLessStar(Zones.FirstOrDefault(z => z.Name == cc.From));
                RebuildCastleLessStar(Zones.FirstOrDefault(z => z.Name == cc.To));
            }

            cc.Name = $"{cc.From.ToUpperInvariant()}-{cc.To.ToUpperInvariant()}";

            MarkDirty();
            MirrorConnectionMarkDirty(cc);
            RebuildGraph();
            BuildInspector();
            UpdateStatus($"Свойства вставлены в '{cc.Name}'");
        }

        private void BtnJsonPreview_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            var window = new JsonPreviewWindow(this);
            window.Show();
        }

        private void BtnOrientation_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            var dlg = new OrientationWindow(Variant) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                RebuildGraph();
                UpdateTitle();
                UpdateStatus(L("S.EC.OrientationApplied"));
            }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            try
            {
                var dlg = new EditorHelpWindow();
                dlg.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка открытия справки:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void BtnCopyConnections_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            var dlg = new CopyConnectionsWindow(Zones, Connections, AutoGenerateRoadsForConnection)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true && dlg.Applied)
            {
                RebuildGraph();
                UpdateStatus("Связи скопированы");
            }
        }

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
            var dlg = new ImageImportWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                LoadTemplate(dlg.Result);
                _currentPath = null;
                _topology = MapTopology.Random;
                ComputePositions();
                RebuildGraph();
                FitToView();
                UpdateStatus(L("S.EC.Loaded", dlg.Result.Name, Zones.Count, Connections.Count));
                // Notify the host (main window) so it re-imports in edit mode with the same
                // restriction logic that applies to a direct main-window import.
                TemplateImported?.Invoke(dlg.Result);
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
            // Restore editor-saved zone positions (AuroraRMG block) so a re-import keeps the
            // exact layout; zones absent from the block keep their auto-derived position.
            ApplyAuroraRmgPositions(loaded, _positions);
            AutoGenerateConnectionNames();
            // All road syncing handled by RebuildGraph with proper pre-auto snapshot
            RebuildGraph();
            FitToView();
            UpdateCanvasHintVisibility();
            BuildInspector();
        }

        /// <summary>Shows the canvas controls hint only for a fresh, empty template; once zones exist
        /// (e.g. after importing/loading a .rmg.json) the hint is hidden to avoid clutter.</summary>
        private void UpdateCanvasHintVisibility()
        {
            if (TxtCanvasHint != null)
                TxtCanvasHint.Visibility = Zones.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Overrides auto-derived <paramref name="positions"/> with the coordinates stored in the
        /// template's "AuroraRMG" block. Zones not present in the block keep their computed position.
        /// </summary>
        public static void ApplyAuroraRmgPositions(RmgTemplate tmpl, Dictionary<string, Point> positions)
        {
            var block = tmpl.AuroraRmg?.Zones;
            if (block == null) return;
            foreach (var zc in block)
                if (positions.ContainsKey(zc.Name))
                    positions[zc.Name] = new Point(zc.X, zc.Y);
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
                // If name still default ("Свой шаблон" / "Custom template"), replace with filename
                string defaultName = L("S.M.014");
                if (string.Equals(_template.Name, defaultName, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(_template.Name))
                {
                    _template.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                }

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

                // Ensure spawn args are unique across zones before exporting
                EnsureUniqueSpawns(_template);

                // Persist current zone canvas positions so a re-import restores the exact layout.
                _template.AuroraRmg = new AuroraRmgCoords
                {
                    Zones = _positions
                        .Where(kv => Zones.Any(z => z.Name == kv.Key))
                        .Select(kv => new ZoneCoord { Name = kv.Key, X = kv.Value.X, Y = kv.Value.Y })
                        .ToList()
                };

                // Snap the map size to the nearest supported project size (e.g. decoded HotA sizes
                // like 36/72/108/180/216/252 are not in the supported set, so they round to it).
                if (_template.SizeX > 0) _template.SizeX = KnownValues.NearestMapSize(_template.SizeX);
                if (_template.SizeZ > 0) _template.SizeZ = KnownValues.NearestMapSize(_template.SizeZ);

                File.WriteAllText(dlg.FileName, TemplateGenerator.StripAndNormalizeForSave(_template, JsonOptions));

                // Always emit a PNG preview sidecar next to the .rmg.json with the same base name.
                try { TemplatePreviewPngWriter.Save(_template, TemplatePreviewPngWriter.GetSidecarPath(dlg.FileName), _topology); }
                catch { /* preview sidecar is best-effort */ }

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
