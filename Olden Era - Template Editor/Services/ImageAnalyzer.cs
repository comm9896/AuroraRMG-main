using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Analyses a HotA template diagram image and produces an <see cref="RmgTemplate"/>.
    /// Uses only WPF built-in APIs (WriteableBitmap + pixel sampling) for zone detection.
    /// No external NuGet packages required — single-file compatible.
    /// </summary>
    public static class ImageAnalyzer
    {
        // ── Content value ratios (from 68 official templates, 1014 zones) ────
        private static readonly Dictionary<string, double[]> ContentRatios = new()
        {
            ["zone_layout_supertreasure_zone"] = [0.9212, 0.0014, 0.0703, 0.0000, 0.0070, 0.0001],
            ["zone_layout_treasure_zone"]      = [0.8528, 0.0008, 0.0813, 0.0001, 0.0649, 0.0001],
            ["zone_layout_treasure"]           = [0.8528, 0.0008, 0.0813, 0.0001, 0.0649, 0.0001],
            ["zone_layout_treasures"]          = [0.8769, 0.0000, 0.0718, 0.0000, 0.0513, 0.0000],
            ["zone_layout_sides"]              = [0.8158, 0.0044, 0.1176, 0.0001, 0.0621, 0.0000],
            ["zone_layout_side_zone"]          = [0.9168, 0.0006, 0.0065, 0.0001, 0.0759, 0.0001],
            ["zone_layout_spawns"]             = [0.6651, 0.0059, 0.1571, 0.0008, 0.1710, 0.0001],
            ["zone_layout_player_spawn"]       = [0.8140, 0.0000, 0.1163, 0.0000, 0.0698, 0.0000],
            ["zone_layout_ai_spawn"]           = [0.7490, 0.0000, 0.1445, 0.0000, 0.1064, 0.0000],
            ["zone_layout_spawn"]              = [0.7507, 0.0002, 0.1434, 0.0000, 0.1056, 0.0000],
            ["zone_layout_start_zone"]         = [0.7507, 0.0002, 0.1434, 0.0000, 0.1056, 0.0000],
            ["zone_layout_center"]             = [0.9369, 0.0096, 0.0382, 0.0002, 0.0151, 0.0000],
            ["zone_layout_center_zone"]        = [0.9369, 0.0096, 0.0382, 0.0002, 0.0151, 0.0000],
            ["zone_layout_second_spawn"]       = [0.7892, 0.0000, 0.0902, 0.0000, 0.1206, 0.0000],
            ["zone_layout_side_spawn_zone"]    = [0.8273, 0.0000, 0.0899, 0.0000, 0.0827, 0.0000],
            ["zone_layout_back"]               = [0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            ["zone_layout_leaf"]               = [0.9130, 0.0000, 0.0570, 0.0000, 0.0300, 0.0000],
            ["zone_layout_wincondition_zone"]  = [1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
        };

        // ── Default content pools per zone type ────────────────────────────
        private static readonly Dictionary<string, (string[] Guarded, string[] Unguarded,
            string[] Resources, string[] Mandatory, string[] Limits)> DefaultPools = new()
        {
            ["zone_layout_supertreasure_zone"] = (
                ["content_pool_template_mini_nosta_guarded_supertreasure_zone"],
                ["content_pool_template_mini_nosta_unguarded_supertreasure_zone"],
                ["content_pool_general_resources_start_zone_very_poor"],
                ["mandatory_content_treasure"],
                ["content_limits_treasure"]),
            ["zone_layout_treasure_zone"] = (
                ["content_pool_template_kerberos_guarded_treasure_zone"],
                ["content_pool_template_kerberos_unguarded_treasure_zone"],
                ["content_pool_general_resources_treasure_zone_rich"],
                ["mandatory_content_center"],
                ["content_limits_center_1", "content_limits_center_2",
                 "content_limits_center_3", "content_limits_center_4",
                 "content_limits_center_5", "content_limits_center_6"]),
            ["zone_layout_sides"] = (
                ["classic_template_pool_random_t1_base"],
                ["classic_template_pool_random_unguarded_t1_base"],
                ["content_pool_general_resources_start_zone_poor"],
                ["mandatory_content_spawns"],
                ["content_limits_spawns"]),
            ["zone_layout_spawns"] = (
                ["content_pool_template_mini_nosta_guarded_start_zone"],
                ["content_pool_template_mini_nosta_unguarded_start_zone"],
                ["content_pool_general_resources_start_zone_very_poor"],
                ["mandatory_content_spawn"],
                ["content_limits_spawn"]),
            ["zone_layout_center"] = (
                ["content_pool_template_mini_nosta_guarded_supertreasure_zone"],
                ["content_pool_template_mini_nosta_unguarded_supertreasure_zone"],
                ["content_pool_general_resources_start_zone_very_poor"],
                ["mandatory_content_treasure"],
                ["content_limits_treasure"]),
            ["zone_layout_side_zone"] = (
                ["classic_template_pool_random_t2_item"],
                ["classic_template_pool_random_unguarded_t2_item"],
                ["content_pool_general_resources_start_zone_medium"],
                ["mandatory_content_side"],
                ["content_limits_side"]),
            ["zone_layout_leaf"] = (
                ["classic_template_pool_random_t3_item"],
                ["classic_template_pool_random_unguarded_t3_item"],
                ["content_pool_general_resources_start_zone_medium"],
                ["mandatory_content_side"],
                ["content_limits_side"]),
        };

        static ImageAnalyzer()
        {
            // Propagate pools to alias layouts
            string[][] aliases = [
                ["zone_layout_treasure", "zone_layout_treasures"],
                ["zone_layout_start_zone", "zone_layout_spawn", "zone_layout_player_spawn",
                 "zone_layout_ai_spawn", "zone_layout_second_spawn"],
                ["zone_layout_side_spawn_zone"],
                ["zone_layout_center_zone"],
                ["zone_layout_back", "zone_layout_wincondition_zone"]];
            string[] sources = [
                "zone_layout_treasure_zone",
                "zone_layout_spawns",
                "zone_layout_side_zone",
                "zone_layout_center",
                "zone_layout_supertreasure_zone"];
            for (int g = 0; g < aliases.Length; g++)
                foreach (var key in aliases[g])
                    if (!DefaultPools.ContainsKey(key) && DefaultPools.TryGetValue(sources[g], out var src))
                        DefaultPools[key] = src;
        }

        // ── Zone colour ranges (HSV) — tuned for HotA template diagrams ────
        private static readonly (byte HMin, byte HMax, byte SMin, byte SMax,
            byte VMin, byte VMax, string Layout, string Prefix)[] ColorRanges =
        [
            // Red → player_spawn (H≈0, S>100)
            (0, 12, 100, 255, 100, 255, "zone_layout_player_spawn", "Spawn"),
            (160, 180, 100, 255, 100, 255, "zone_layout_player_spawn", "Spawn"),
            // Blue → start_zone (H≈100-135)
            (90, 135, 40, 255, 100, 255, "zone_layout_start_zone", "StartZone"),
            // Gold/Yellow → supertreasure (H≈15-38, low-to-high saturation)
            (15, 38, 5, 255, 180, 255, "zone_layout_supertreasure_zone", "SuperTreasure"),
            // Orange/Brown → side_zone (H≈10-25, S>50)
            (10, 25, 50, 255, 100, 255, "zone_layout_side_zone", "SideZone"),
            // Teal/Cyan → side_spawn_zone (H≈80-100)
            (80, 100, 30, 255, 100, 255, "zone_layout_side_spawn_zone", "SideSpawn"),
            // Green → leaf (H≈35-85)
            (35, 85, 30, 255, 100, 255, "zone_layout_leaf", "Leaf"),
            // Pink/Purple → second_spawn (H≈140-170)
            (140, 170, 30, 255, 100, 255, "zone_layout_second_spawn", "SecondSpawn"),
            // Silver/Grey → treasure_zone (S<40, V 150-230)
            (0, 180, 0, 40, 150, 230, "zone_layout_treasure_zone", "Treasure"),
            // White → sides (S<25, V>220)
            (0, 180, 0, 25, 220, 255, "zone_layout_sides", "Side"),
        ];

        // ── Public API ─────────────────────────────────────────────────────

        public enum DetectionMethod
        {
            ColourFlood,
            Contour,
        }

        public static RmgTemplate Analyze(string imagePath, DetectionMethod method,
            IProgress<string>? progress = null)
        {
            var bitmap = LoadBitmap(imagePath);
            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;

            var pixels = new byte[w * h * 4];
            bitmap.CopyPixels(pixels, w * 4, 0);

            progress?.Report("Detecting zones...");
            var zones = method switch
            {
                DetectionMethod.Contour => DetectZonesContour(pixels, w, h),
                _ => DetectZonesColourFlood(pixels, w, h),
            };
            progress?.Report($"Found {zones.Count} zones");

            // Assign names BEFORE connection detection so connections reference valid names
            AssignZoneNames(zones);

            progress?.Report("Detecting connections...");
            var connections = DetectConnections(pixels, w, h, zones);
            progress?.Report($"Found {connections.Count} connections");

            progress?.Report("Building template...");
            return BuildTemplate(zones, connections, Path.GetFileNameWithoutExtension(imagePath), w, h);
        }

        // ── Bitmap loading ─────────────────────────────────────────────────

        private static BitmapSource LoadBitmap(string path)
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        // ── Method 1: Colour flood-fill ───────────────────────────────────
        // Each pixel classified by HSV → flood-fill per class → filter by size.

        private static List<DetectedZone> DetectZonesColourFlood(byte[] pixels, int w, int h)
        {
            // Scale area filter based on image size
            int imgArea = w * h;
            int minArea = Math.Max(4000, imgArea / 3000);  // ~0.03% of image
            int maxArea = Math.Min(200000, imgArea / 20);   // ~5% of image
            int minDim = Math.Max(60, (int)Math.Sqrt(minArea));

            // Classify every pixel into a colour class (-1 = background)
            var classMap = new short[w * h];
            for (int i = 0; i < w * h; i++)
            {
                int off = i * 4;
                BgraToHsv(pixels, off, out byte hv, out byte sv, out byte vv);
                classMap[i] = ColourClass(hv, sv, vv);
            }

            var visited = new bool[w * h];
            var candidates = new List<DetectedZone>();
            int id = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    short cls = classMap[idx];
                    if (cls < 0 || visited[idx]) continue;

                    var bounds = new BoundingBox(x, y);
                    FloodFillClass(classMap, visited, w, h, x, y, cls, ref bounds);
                    int area = bounds.Area;

                    if (area < minArea || area > maxArea) continue;
                    if (bounds.Width < minDim || bounds.Height < minDim) continue;

                    double aspect = (double)bounds.Width / bounds.Height;
                    if (aspect < 0.4 || aspect > 2.5) continue;

                    string layout = cls switch
                    {
                        0 => "zone_layout_player_spawn",
                        1 => "zone_layout_start_zone",
                        2 => "zone_layout_supertreasure_zone",
                        3 => "zone_layout_treasure_zone",
                        4 => "zone_layout_leaf",
                        5 => "zone_layout_side_spawn_zone",
                        6 => "zone_layout_second_spawn",
                        _ => "zone_layout_sides",
                    };

                    candidates.Add(new DetectedZone
                    {
                        Id = id++,
                        Layout = layout,
                        Prefix = LayoutToPrefix(layout),
                        Cx = bounds.CenterX,
                        Cy = bounds.CenterY,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Radius = Math.Max(bounds.Width, bounds.Height) / 2,
                    });
                }
            }

            // Deduplicate: merge any two zones whose bounding boxes overlap
            candidates.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
            var result = new List<DetectedZone>();
            foreach (var z in candidates)
            {
                bool merged = false;
                for (int i = 0; i < result.Count; i++)
                {
                    var r = result[i];
                    // Check bounding box overlap
                    if (z.Cx - z.Radius < r.Cx + r.Radius &&
                        z.Cx + z.Radius > r.Cx - r.Radius &&
                        z.Cy - z.Radius < r.Cy + r.Radius &&
                        z.Cy + z.Radius > r.Cy - r.Radius)
                    {
                        // Overlapping — keep the larger one, but prefer gold/silver over mixed
                        int zArea = z.Width * z.Height;
                        int rArea = r.Width * r.Height;
                        if (zArea > rArea)
                            result[i] = z;
                        merged = true;
                        break;
                    }
                }
                if (!merged) result.Add(z);
            }

            return result;
        }

        // ── Method 2: Brightness contour detection ────────────────────────
        // Flood-fill from BRIGHT pixels (zone interiors). Dark borders separate zones.

        private static List<DetectedZone> DetectZonesContour(byte[] pixels, int w, int h)
        {
            int imgArea = w * h;
            int minArea = Math.Max(4000, imgArea / 3000);
            int maxArea = Math.Min(200000, imgArea / 50);
            int minDim = Math.Max(80, (int)Math.Sqrt(minArea));

            // Build brightness map
            var bright = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                int off = i * 4;
                byte r = pixels[off + 2], g = pixels[off + 1], b = pixels[off];
                bright[i] = (byte)((r + g + b) / 3);
            }

            // Mask: zone interiors (bright but not background-white)
            var mask = new bool[w * h];
            for (int i = 0; i < w * h; i++)
                mask[i] = bright[i] > 80 && bright[i] < 235;

            var visited = new bool[w * h];
            var candidates = new List<DetectedZone>();
            int id = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (visited[idx] || !mask[idx]) continue;

                    var bounds = new BoundingBox(x, y);
                    FloodFillMask(mask, visited, w, h, x, y, ref bounds);

                    int area = bounds.Area;
                    if (area < minArea || area > maxArea) continue;
                    if (bounds.Width < minDim || bounds.Height < minDim) continue;

                    double aspect = (double)bounds.Width / bounds.Height;
                    if (aspect < 0.4 || aspect > 2.5) continue;

                    // Classify by dominant colour inside bounding box
                    string layout = ClassifyZoneColour(pixels, w,
                        bounds.XMin, bounds.YMin, bounds.Width, bounds.Height);

                    candidates.Add(new DetectedZone
                    {
                        Id = id++,
                        Layout = layout,
                        Prefix = LayoutToPrefix(layout),
                        Cx = bounds.CenterX,
                        Cy = bounds.CenterY,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Radius = Math.Max(bounds.Width, bounds.Height) / 2,
                    });
                }
            }

            // Overlap dedup
            candidates.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
            var result = new List<DetectedZone>();
            foreach (var z in candidates)
            {
                bool merged = false;
                for (int i = 0; i < result.Count; i++)
                {
                    var r = result[i];
                    if (z.Cx - z.Radius < r.Cx + r.Radius &&
                        z.Cx + z.Radius > r.Cx - r.Radius &&
                        z.Cy - z.Radius < r.Cy + r.Radius &&
                        z.Cy + z.Radius > r.Cy - r.Radius)
                    {
                        if (z.Width * z.Height > r.Width * r.Height)
                            result[i] = z;
                        merged = true;
                        break;
                    }
                }
                if (!merged) result.Add(z);
            }
            return result;
        }

        private static void FloodFillMask(bool[] mask, bool[] visited, int w, int h,
            int startX, int startY, ref BoundingBox bounds)
        {
            var stack = new Stack<(int X, int Y)>();
            stack.Push((startX, startY));
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                int idx = y * w + x;
                if (visited[idx] || !mask[idx]) continue;
                visited[idx] = true;
                bounds.Expand(x, y);
                stack.Push((x + 1, y));
                stack.Push((x - 1, y));
                stack.Push((x, y + 1));
                stack.Push((x, y - 1));
            }
        }

        private static string ClassifyZoneColour(byte[] pixels, int imgW,
            int bx, int by, int bw, int bh)
        {
            int gold = 0, red = 0, blue = 0, silver = 0;
            int step = Math.Max(5, Math.Min(bw, bh) / 8);

            for (int y = by + step; y < by + bh - step; y += step)
            {
                for (int x = bx + step; x < bx + bw - step; x += step)
                {
                    int off = (y * imgW + x) * 4;
                    BgraToHsv(pixels, off, out byte hv, out byte sv, out byte vv);

                    if (vv < 100) continue;
                    if (sv < 5 && vv > 200) { silver++; continue; }
                    if (hv <= 12 && sv > 80) red++;
                    else if (hv >= 15 && hv <= 38 && sv > 30) gold++;
                    else if (hv >= 90 && hv <= 135 && sv > 30) blue++;
                    else if (sv < 40) silver++;
                }
            }

            int total = gold + red + blue + silver;
            if (total == 0) return "zone_layout_sides";
            if (red > total * 0.3) return "zone_layout_player_spawn";
            if (blue > total * 0.3) return "zone_layout_start_zone";
            if (gold > total * 0.3) return "zone_layout_supertreasure_zone";
            return "zone_layout_treasure_zone";
        }

        /// <summary>Classify a pixel into a colour class for zone detection.</summary>
        private static short ColourClass(byte h, byte s, byte v)
        {
            if (v < 80) return -1; // dark (borders, icons, lines)
            // Red: H≤12, S>80
            if (h <= 12 && s > 80 && v > 100) return 0;
            // Blue: H:90-135, S>40
            if (h >= 90 && h <= 135 && s > 40 && v > 100) return 1;
            // Gold: H:15-38, S>20, V>160
            if (h >= 15 && h <= 38 && s > 20 && v > 160) return 2;
            // Silver/grey/white: S<40, V>120
            if (s < 40 && v > 120) return 3;
            // Green: H:35-85, S>30
            if (h >= 35 && h <= 85 && s > 30 && v > 100) return 4;
            // Teal/Cyan: H:80-100, S>30
            if (h >= 80 && h <= 100 && s > 30 && v > 100) return 5;
            // Pink/Magenta: H:140-170, S>30
            if (h >= 140 && h <= 170 && s > 30 && v > 100) return 6;
            return -1; // unclassified
        }

        private static void FloodFillClass(short[] cls, bool[] visited, int w, int h,
            int startX, int startY, short targetClass, ref BoundingBox bounds)
        {
            var stack = new Stack<(int X, int Y)>();
            stack.Push((startX, startY));
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                int idx = y * w + x;
                if (visited[idx] || cls[idx] != targetClass) continue;
                visited[idx] = true;
                bounds.Expand(x, y);
                stack.Push((x + 1, y));
                stack.Push((x - 1, y));
                stack.Push((x, y + 1));
                stack.Push((x, y - 1));
            }
        }

        private static string LayoutToPrefix(string layout) => layout switch
        {
            "zone_layout_supertreasure_zone" => "SuperTreasure",
            "zone_layout_treasure_zone" => "Treasure",
            "zone_layout_player_spawn" => "Spawn",
            "zone_layout_start_zone" => "StartZone",
            "zone_layout_sides" => "Side",
            "zone_layout_side_zone" => "SideZone",
            "zone_layout_side_spawn_zone" => "SideSpawn",
            "zone_layout_leaf" => "Leaf",
            "zone_layout_second_spawn" => "SecondSpawn",
            "zone_layout_ai_spawn" => "AISpawn",
            "zone_layout_center" => "Center",
            "zone_layout_back" => "Back",
            _ => "Zone",
        };

        private struct BoundingBox
        {
            public int XMin, XMax, YMin, YMax;
            public BoundingBox(int x, int y) { XMin = XMax = x; YMin = YMax = y; }
            public int Width => XMax - XMin + 1;
            public int Height => YMax - YMin + 1;
            public int Area => Width * Height;
            public int CenterX => (XMin + XMax) / 2;
            public int CenterY => (YMin + YMax) / 2;
            public void Expand(int x, int y)
            {
                if (x < XMin) XMin = x; if (x > XMax) XMax = x;
                if (y < YMin) YMin = y; if (y > YMax) YMax = y;
            }
        }

        private static void BgraToHsv(byte[] pixels, int offset, out byte h, out byte s, out byte v)
        {
            byte b = pixels[offset], g = pixels[offset + 1], r = pixels[offset + 2];
            byte max = Math.Max(r, Math.Max(g, b));
            byte min = Math.Min(r, Math.Min(g, b));
            v = max;
            s = max == 0 ? (byte)0 : (byte)((max - min) * 255 / max);
            if (max == min) { h = 0; return; }
            double hue;
            if (max == r) hue = 60.0 * (g - b) / (max - min);
            else if (max == g) hue = 120.0 + 60.0 * (b - r) / (max - min);
            else hue = 240.0 + 60.0 * (r - g) / (max - min);
            h = (byte)((hue < 0 ? hue + 360 : hue) / 2.0);
        }

        // ── Zone naming + default content ──────────────────────────────────

        private static void AssignZoneNames(List<DetectedZone> zones)
        {
            var counters = new Dictionary<string, int>();
            foreach (var z in zones)
            {
                if (!counters.TryGetValue(z.Prefix, out int c)) c = 0;
                counters[z.Prefix] = ++c;
                z.Name = $"{z.Prefix}-{c}";

                // Set default chest value based on zone type
                z.ChestValue = z.Layout switch
                {
                    "zone_layout_supertreasure_zone" => 300,
                    "zone_layout_treasure_zone" => 200,
                    "zone_layout_player_spawn" => 100,
                    "zone_layout_start_zone" => 100,
                    _ => 150,
                };
            }
        }

        // ── Connection detection ───────────────────────────────────────────

        private static List<DetectedConnection> DetectConnections(byte[] pixels, int w, int h,
            List<DetectedZone> zones)
        {
            if (zones.Count < 2) return [];

            // Build masks
            var darkMask = new bool[w * h];
            var redMask = new bool[w * h];
            for (int i = 0; i < w * h; i++)
            {
                int off = i * 4;
                byte r = pixels[off + 2], g = pixels[off + 1], b = pixels[off];
                byte avg = (byte)((r + g + b) / 3);
                darkMask[i] = avg < 120;

                BgraToHsv(pixels, off, out byte hv, out byte sv, out byte vv);
                redMask[i] = (hv <= 12 || hv >= 160) && sv > 80 && vv > 100;
            }

            var connections = new List<DetectedConnection>();

            for (int i = 0; i < zones.Count; i++)
            {
                for (int j = i + 1; j < zones.Count; j++)
                {
                    var a = zones[i];
                    var b = zones[j];
                    int dist = (int)Math.Sqrt((a.Cx - b.Cx) * (a.Cx - b.Cx) +
                                              (a.Cy - b.Cy) * (a.Cy - b.Cy));
                    if (dist < 100 || dist > 1000) continue;

                    int mx = (a.Cx + b.Cx) / 2;
                    int my = (a.Cy + b.Cy) / 2;

                    // Skip if midpoint is near any zone (within 1.5x radius)
                    bool insideZone = false;
                    foreach (var z in zones)
                    {
                        int dx = mx - z.Cx, dy = my - z.Cy;
                        int threshold = (int)(z.Radius * 1.5);
                        if (dx * dx + dy * dy < threshold * threshold)
                        { insideZone = true; break; }
                    }
                    if (insideZone) continue;

                    // Method 1: Dark pixels along the line (threshold 0.10)
                    int linePx = 0, darkPx = 0;
                    int steps = dist / 4;
                    for (int s = 1; s < steps - 1; s++)
                    {
                        double t = (double)s / steps;
                        int px = (int)(a.Cx + t * (b.Cx - a.Cx));
                        int py = (int)(a.Cy + t * (b.Cy - a.Cy));
                        if (px >= 0 && px < w && py >= 0 && py < h)
                        {
                            linePx++;
                            if (darkMask[py * w + px]) darkPx++;
                        }
                    }
                    bool hasLine = linePx > 0 && (double)darkPx / linePx > 0.15;

                    // Method 2: Red numbers near midpoint (>15 red pixels in 30×30)
                    int redCount = 0;
                    int r = 30;
                    for (int dy = -r; dy <= r; dy += 2)
                    {
                        for (int dx = -r; dx <= r; dx += 2)
                        {
                            int px = mx + dx, py = my + dy;
                            if (px >= 0 && px < w && py >= 0 && py < h && redMask[py * w + px])
                                redCount++;
                        }
                    }
                    bool hasRedNumbers = redCount > 15;

                    // Keep connection if BOTH line pixels AND red numbers detected
                    if (hasLine && hasRedNumbers)
                    {
                        int weight = EstimateWeight(redCount);
                        connections.Add(new DetectedConnection
                        {
                            From = a.Name,
                            To = b.Name,
                            Weight = weight,
                            IsRoad = true,
                        });
                    }
                }
            }

            // Deduplicate by zone pair
            var seen = new HashSet<(string, string)>();
            var result = new List<DetectedConnection>();
            foreach (var c in connections)
            {
                var key = string.Compare(c.From, c.To, StringComparison.Ordinal) < 0
                    ? (c.From, c.To) : (c.To, c.From);
                if (seen.Add(key))
                    result.Add(c);
            }

            return result;
        }

        private static int EstimateWeight(int redPixels)
        {
            // Heuristic: more red pixels = higher weight number
            if (redPixels < 50) return 6;
            if (redPixels < 100) return 12;
            if (redPixels < 200) return 18;
            if (redPixels < 350) return 24;
            return 36;
        }

        // ── Template builder ───────────────────────────────────────────────

        private static RmgTemplate BuildTemplate(List<DetectedZone> zones,
            List<DetectedConnection> connections, string name, int imgW, int imgH)
        {
            // Names already assigned by AssignZoneNames()
            return new RmgTemplate
            {
                Name = name,
                GameMode = "Classic",
                Description = "Imported from image",
                SizeX = 160,
                SizeZ = 160,
                Variants =
                [
                    new Variant
                    {
                        Orientation = new Orientation { Mode = "MinimalBoundingSquare" },
                        Border = new Border { CornerRadius = 0.0, ObstaclesWidth = 3 },
                        Zones = zones.Select(z => BuildZone(z, imgW, imgH)).ToList(),
                        Connections = connections.Select((c, i) => new Connection
                        {
                            Name = $"Connection-{i + 1}",
                            From = c.From,
                            To = c.To,
                            ConnectionType = "Default",
                            GuardValue = c.Weight * 1000,
                            Road = c.IsRoad,
                        }).ToList(),
                    }
                ],
            };
        }

        private static Zone BuildZone(DetectedZone dz, int imgW, int imgH)
        {
            double chest = dz.ChestValue * 1000.0;
            var ratios = ContentRatios.GetValueOrDefault(dz.Layout, ContentRatios["zone_layout_sides"]);

            var zone = new Zone
            {
                Name = dz.Name,
                Layout = dz.Layout,
                // Normalize pixel coords to [0,1] for the preview renderer
                GeneratorPosition = ((double)dz.Cx / imgW, (double)dz.Cy / imgH),
                GuardedContentValue = (int)(chest * ratios[0]),
                GuardedContentValuePerArea = (int)(chest * ratios[1]),
                UnguardedContentValue = (int)(chest * ratios[2]),
                UnguardedContentValuePerArea = (int)(chest * ratios[3]),
                ResourcesValue = (int)(chest * ratios[4]),
                ResourcesValuePerArea = (int)(chest * ratios[5]),
                MainObjects =
                [
                    new MainObject
                    {
                        Type = "City",
                        BuildingsConstructionSid = "default_buildings_construction",
                        Faction = new TypedSelector { Type = "MatchMainObject", Args = ["0"] },
                        Placement = "Center",
                    }
                ],
            };

            if (DefaultPools.TryGetValue(dz.Layout, out var pools))
            {
                zone.GuardedContentPool = pools.Guarded.ToList();
                zone.UnguardedContentPool = pools.Unguarded.ToList();
                zone.ResourcesContentPool = pools.Resources.ToList();
                zone.MandatoryContent = pools.Mandatory.ToList();
                zone.ContentCountLimits = pools.Limits.ToList();
            }

            return zone;
        }

        // ── Internal model ─────────────────────────────────────────────────

        public class DetectedZone
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Layout { get; set; } = "";
            public string Prefix { get; set; } = "";
            public int Cx { get; set; }
            public int Cy { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Radius { get; set; }
            public int ChestValue { get; set; }
        }

        public class DetectedConnection
        {
            public string From { get; set; } = "";
            public string To { get; set; } = "";
            public int Weight { get; set; }
            public bool IsRoad { get; set; }
        }
    }
}
