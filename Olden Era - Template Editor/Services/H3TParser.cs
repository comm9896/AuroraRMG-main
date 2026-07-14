using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Parses HotA .h3t template files and converts to <see cref="RmgTemplate"/>.
    /// H3T format: tab-separated values with 140+ fields per row.
    /// Row 1: Zone definition (type, name, size, treasure, monsters, etc.)
    /// Row 2+: Connections (Zone 1 → Zone 2 with guard value and road flag)
    /// </summary>
    public static class H3TParser
    {
        // Field indices (0-based) from the H3T format
        private const int FIELD_ZONE_TYPE = 2;
        private const int FIELD_ZONE_NEW = 5;
        private const int FIELD_NAME = 7;
        private const int FIELD_DESCRIPTION = 8;
        private const int FIELD_ZONE_NAME = 15;
        private const int FIELD_MIN_SIZE = 16;
        private const int FIELD_MAX_SIZE = 17;
        private const int FIELD_ID = 28;
        private const int FIELD_HUMAN_START = 29;
        private const int FIELD_TREASURE = 31;
        private const int FIELD_BASE_SIZE = 33;
        private const int FIELD_SIZE = 34;
        private const int FIELD_OWNERSHIP = 38;
        private const int FIELD_STRENGTH = 85;
        private const int FIELD_NEUTRAL = 87;
        private const int FIELD_MONSTERS_DISPOSITION = 117;
        private const int FIELD_MONSTERS_JOINING = 119;
        private const int FIELD_ZONE_1 = 127;
        private const int FIELD_ZONE_2 = 128;
        private const int FIELD_VALUE = 129;
        private const int FIELD_ROAD = 132;
        private const int FIELD_CONNECTION_TYPE = 133;
        // [112] "Image settings" — zone placement coordinates (HotA bounds / point)
        // Format: "x1 y1 x2 y2" (rectangle bounds) | "x y" (centre point) | "x" / "" (auto)
        private const int FIELD_PLACEMENT_COORDS = 112;

        // Zone type codes → layout names
        private static readonly Dictionary<int, string> ZoneTypeMap = new()
        {
            [0] = "zone_layout_player_spawn",
            [1] = "zone_layout_ai_spawn",
            [2] = "zone_layout_start_zone",
            [3] = "zone_layout_side_zone",
            [4] = "zone_layout_treasure_zone",
            [5] = "zone_layout_supertreasure_zone",
            [6] = "zone_layout_center",
            [7] = "zone_layout_sides",
            [8] = "zone_layout_leaf",
            [9] = "zone_layout_back",
            [10] = "zone_layout_second_spawn",
            [11] = "zone_layout_side_spawn_zone",
            [12] = "zone_layout_wincondition_zone",
        };

        // HotA map-size codes → surface size (the "with underground" codes share the same
        // surface size as their "without underground" twins, and we only model the surface).
        // Real sizes: 36, 72, 108, 144, 180, 216, 252.
        private static readonly Dictionary<int, int> H3TMapSizeCodes = new()
        {
            [1] = 36,  [2] = 36,
            [4] = 72,  [8] = 72,
            [9] = 108, [16] = 144, [18] = 108,
            [25] = 180, [32] = 144,
            [36] = 216, [49] = 252, [50] = 180,
            [72] = 216, [99] = 252,
        };

        /// <summary>
        /// Decodes a HotA map-size code (1,2,4,8,9,16,18,25,32,36,49,50,72,99) to the real
        /// surface size. The underground flag is ignored. Unknown codes fall back to 160.
        /// </summary>
        public static int DecodeH3TMapSize(int code)
            => H3TMapSizeCodes.TryGetValue(code, out int size) ? size : 160;

        public static RmgTemplate Parse(string filepath)
        {
            var rawLines = File.ReadAllLines(filepath, System.Text.Encoding.GetEncoding("iso-8859-1"));

            // Merge lines with unterminated quotes (multi-line descriptions)
            var lines = MergeQuotedLines(rawLines);

            if (lines.Length < 4)
                throw new InvalidDataException("H3T file too short");

            var fields = lines[2].Split('\t');
            var template = new RmgTemplate
            {
                Name = "",
                GameMode = "Classic",
                Variants = [new Variant { Zones = [], Connections = [] }],
            };

            var zones = new List<Zone>();
            var connections = new List<Connection>();
            var zoneIds = new Dictionary<int, string>(); // id → name

            // Extract template name from header line (zoneNew=18 or 99, line index 3)
            string headerName = "";
            if (lines.Length > 3)
            {
                var headerParts = lines[3].Split('\t');
                string hdrZoneNew = GetField(headerParts, FIELD_ZONE_NEW);
                if (hdrZoneNew == "18" || hdrZoneNew == "99")
                {
                    headerName = GetField(headerParts, 7); // field[7] = template name
                    if (string.IsNullOrEmpty(headerName))
                        headerName = GetField(headerParts, FIELD_ZONE_NAME);
                }
            }
            template.Name = headerName;

            // ── First pass: collect raw sizes to find the mode ──
            var allZoneLines = new List<(string[] parts, bool isZone)>();
            var rawSizes = new List<double>();
            var seenZoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(3))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var p = line.Split('\t');
                string idCheck = GetField(p, FIELD_ID);
                // Deduplicate: only first occurrence of each zone ID is a real zone row
                // (Duel.h3t and similar templates repeat zone IDs for object data rows)
                bool isZone = !string.IsNullOrEmpty(idCheck) && seenZoneIds.Add(idCheck);
                allZoneLines.Add((p, isZone));
                if (isZone)
                {
                    string rawSz = GetField(p, FIELD_SIZE);
                    if (double.TryParse(rawSz, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double rawSzVal) && rawSzVal > 0)
                        rawSizes.Add(rawSzVal);
                }
            }

            // Mode = most common raw size; all others scale relative to it (mode becomes 1.0)
            double sizeMode = 1.0;
            if (rawSizes.Count > 0)
            {
                sizeMode = rawSizes
                    .GroupBy(s => s)
                    .OrderByDescending(g => g.Count())
                    .ThenByDescending(g => g.Key) // tie-break: prefer larger
                    .First()
                    .Key;
            }

            // ── Second pass: create zones with normalized Size ──
            foreach (var (parts, isZone) in allZoneLines)
            {
                if (!isZone) goto checkConnection;

                string zoneNew = GetField(parts, FIELD_ZONE_NEW);

                // Zone definition: any row with a valid numeric ID (field[28])
                // Includes header row (zoneNew=18) which carries the first zone's data
                var zone = ParseZone(parts, fields, template.Name ?? "", sizeMode);
                zones.Add(zone);
                int id = int.TryParse(GetField(parts, FIELD_ID), out int idVal2) ? idVal2 : zones.Count;
                zoneIds[id] = zone.Name;

                checkConnection:
                // Connection: fields Z1/Z2 are filled (can coexist with zone data on same row)
                string z1Check = GetField(parts, FIELD_ZONE_1);
                string z2Check = GetField(parts, FIELD_ZONE_2);
                if (!string.IsNullOrEmpty(z1Check) && !string.IsNullOrEmpty(z2Check))
                {
                    var conn = ParseConnection(parts, fields, zoneIds);
                    if (conn != null)
                        connections.Add(conn);
                }
            }

            // ── Auto-generate roads in zones from connections with Road=true ──
            AutoGenerateRoadsFromConnections(zones, connections);

            // Fallback: if header name was empty, use first zone name
            if (string.IsNullOrEmpty(template.Name) && zones.Count > 0 && !string.IsNullOrEmpty(zones[0].Name))
                template.Name = zones[0].Name;

            // If we only have 1 zone def but many connections, generate zones from connection refs
            if (zones.Count == 1 && connections.Count > 1)
            {
                var referencedIds = new HashSet<int>();
                foreach (var c in connections)
                {
                    if (int.TryParse(c.From?.Replace("Zone-", ""), out int fromId)) referencedIds.Add(fromId);
                    if (int.TryParse(c.To?.Replace("Zone-", ""), out int toId)) referencedIds.Add(toId);
                }
                foreach (int id in referencedIds.OrderBy(x => x))
                {
                    if (zones.Any(z => z.Name == $"Zone-{id}")) continue;
                    zones.Add(new Zone
                    {
                        Name = $"Zone-{id}",
                        Layout = "zone_layout_treasure_zone",
                        MainObjects = [new MainObject { Type = "City", Placement = "Center" }],
                        GuardedContentPool = ["classic_template_pool_random_t2_item"],
                        UnguardedContentPool = ["classic_template_pool_random_unguarded_t2_item"],
                        ResourcesContentPool = ["content_pool_general_resources_start_zone_medium"],
                    });
                }
                template.Variants[0].Zones = zones;
            }

            // Decode the HotA encoded map-size code to the real surface size.
            int decodedSize = 160;
            if (zones.Count > 0)
            {
                int code = int.TryParse(GetField(lines[3].Split('\t'), FIELD_MAX_SIZE), out int c) ? c : 0;
                decodedSize = DecodeH3TMapSize(code);
            }
            template.SizeX = decodedSize;
            template.SizeZ = decodedSize;

            template.Variants[0].Zones = zones;
            template.Variants[0].Connections = connections;
            template.Variants[0].Orientation = new Orientation { Mode = "MinimalBoundingSquare" };
            template.Variants[0].Border = new Border { CornerRadius = 0.0, ObstaclesWidth = 3 };

            // ── Fill any zones that lack placement coords ──
            // ComputeLayout (Random topology) requires GeneratorPosition on EVERY zone to
            // honour the stamped coordinates. Zones with no HotA [112] data ("x"/"") get a
            // deterministic spiral position around the centroid so they don't break the path.
            FillMissingPositions(zones);

            return template;
        }

        /// <summary>
        /// Parses HotA [112] "Image settings" placement coordinates.
        /// "x1 y1 x2 y2" → centre of the rectangle.  "x y" → point.  "x"/"" → null (auto).
        /// Coordinates are in HotA tile units with origin at map centre, Y-up.
        /// </summary>
        private static (double X, double Y)? ParsePlacementCoords(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var nums = new List<double>();
            foreach (var tok in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (double.TryParse(tok, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double v))
                    nums.Add(v);
                else
                    return null; // non-numeric token ("x") → auto placement
            }
            if (nums.Count == 4)
                return ((nums[0] + nums[2]) / 2.0, (nums[1] + nums[3]) / 2.0);
            if (nums.Count == 2)
                return (nums[0], nums[1]);
            return null;
        }

        /// <summary>
        /// Assigns deterministic spiral positions to zones without a GeneratorPosition so that
        /// ComputeLayout's "all zones have positions" fast-path stays active. The spiral keeps
        /// auto-placed zones near the layout centroid without overlapping stamped ones.
        /// </summary>
        private static void FillMissingPositions(List<Zone> zones)
        {
            var stamped = zones.Where(z => z.GeneratorPosition.HasValue).ToList();
            var missing = zones.Where(z => !z.GeneratorPosition.HasValue).ToList();
            if (missing.Count > 0 && stamped.Count > 0)
            {
                double cx = stamped.Average(z => z.GeneratorPosition!.Value.X);
                double cy = stamped.Average(z => z.GeneratorPosition!.Value.Y);

                // Spiral parameters (in HotA tile units, matching stamped coordinate scale).
                const double step = 120.0;
                const double angleStep = 2.399963; // golden angle (radians)
                for (int i = 0; i < missing.Count; i++)
                {
                    double r = step * Math.Sqrt(i + 1);
                    double a = i * angleStep;
                    missing[i].GeneratorPosition = (cx + r * Math.Cos(a), cy + r * Math.Sin(a));
                }
            }
            else if (missing.Count > 0)
            {
                // No stamped coords at all (e.g. Duel.h3t) — lay out on a centred spiral.
                const double step = 120.0;
                const double angleStep = 2.399963;
                for (int i = 0; i < missing.Count; i++)
                {
                    double r = step * Math.Sqrt(i + 1);
                    double a = i * angleStep;
                    missing[i].GeneratorPosition = (r * Math.Cos(a), r * Math.Sin(a));
                }
            }

            // ── Normalize to isotropic [0,1] (per-axis) ──
            // ComputeLayout's Random topology scales stamped positions to fill the canvas and
            // only enforces a minimum inter-zone distance when they are tighter than a zone
            // diameter. Raw HotA coordinates are anisotropic (e.g. X span 45 vs Y span 305 in
            // tiles); a uniform scale would collapse the shorter axis to a constant and the
            // spring-correction passes would then override its ratios. Mapping EACH axis
            // independently to [0,1] — exactly what ImageAnalyzer does for image import
            // (Cx/imgW, Cy/imgH) — gives ComputeLayout an isotropic input so both axes are
            // preserved. Aspect ratio is not kept (same as image import), but intra-axis
            // coordinate proportions are.
            var withPos = zones.Where(z => z.GeneratorPosition.HasValue).ToList();
            if (withPos.Count == 0) return;

            double minX = withPos.Min(z => z.GeneratorPosition!.Value.X);
            double maxX = withPos.Max(z => z.GeneratorPosition!.Value.X);
            double minY = withPos.Min(z => z.GeneratorPosition!.Value.Y);
            double maxY = withPos.Max(z => z.GeneratorPosition!.Value.Y);
            double spanX = maxX - minX;
            double spanY = maxY - minY;

            foreach (var z in withPos)
            {
                var p = z.GeneratorPosition!.Value;
                double nx = spanX > 1e-6 ? (p.X - minX) / spanX : 0.5;
                double ny = spanY > 1e-6 ? (p.Y - minY) / spanY : 0.5;
                z.GeneratorPosition = (nx, ny);
            }
        }

        private static Zone ParseZone(string[] parts, string[] fields, string templateName, double sizeMode = 1.0)
        {
            string zoneTypeStr = GetField(parts, FIELD_ZONE_TYPE);
            int zoneType = int.TryParse(zoneTypeStr, out int zt) ? zt : 4;
            string layout = ZoneTypeMap.GetValueOrDefault(zoneType, "zone_layout_treasure_zone");

            string name = GetField(parts, FIELD_ZONE_NAME);
            if (string.IsNullOrEmpty(name) || name == templateName)
                name = $"Zone-{GetField(parts, FIELD_ID)}";

            // Parse treasure values (Low/High/Count from fields 100-108)
            int.TryParse(GetField(parts, 100), out int guardedLow);
            int.TryParse(GetField(parts, 101), out int guardedHigh);
            int.TryParse(GetField(parts, 102), out int guardedCount);
            int.TryParse(GetField(parts, 103), out int unguardedLow);
            int.TryParse(GetField(parts, 104), out int unguardedHigh);
            int.TryParse(GetField(parts, 105), out int unguardedCount);
            int.TryParse(GetField(parts, 106), out int resourcesLow);
            int.TryParse(GetField(parts, 107), out int resourcesHigh);
            int.TryParse(GetField(parts, 108), out int resourcesCount);

            bool isHumanStart = GetField(parts, FIELD_HUMAN_START) == "x";
            bool isComputerStart = GetField(parts, 30) == "x";
            int.TryParse(GetField(parts, FIELD_OWNERSHIP), out int ownership);

            // Parse cities per zone (48-59)
            var cities = new List<string>();
            var cityNames = new[] { "Castle","Rampart","Tower","Inferno","Necropolis","Dungeon","Stronghold","Fortress","Conflux","Cove","Factory","Bulwark" };
            for (int i = 48; i <= 59; i++)
                if (GetField(parts, i) == "x") cities.Add(cityNames[i - 48]);

            // Parse terrain per zone (75-84)
            var terrain = new List<string>();
            var terrainNames = new[] { "Dirt","Sand","Grass","Snow","Swamp","Rough","Cave","Lava","Highlands","Wasteland" };
            for (int i = 75; i <= 84; i++)
                if (GetField(parts, i) == "x") terrain.Add(terrainNames[i - 75]);

            // Parse monsters per zone (87-99)
            var monsters = new List<string>();
            var monsterNames = new[] { "Neutral","Castle","Rampart","Tower","Inferno","Necropolis","Dungeon","Stronghold","Fortress","Conflux","Cove","Factory","Bulwark" };
            for (int i = 87; i <= 99; i++)
                if (GetField(parts, i) == "x") monsters.Add(monsterNames[i - 87]);

            // Parse resources per zone (60-66 player, 67-73 neutral)
            var resources = new Dictionary<string, int>();
            var resNames = new[] { "Wood","Mercury","Ore","Sulfur","Crystal","Gems","Gold" };
            for (int i = 60; i <= 66; i++)
            {
                string val = GetField(parts, i);
                if (int.TryParse(val, out int resVal) && resVal > 0)
                    resources[resNames[i - 60]] = resVal;
            }

            // Monster settings per zone
            string strength = GetField(parts, FIELD_STRENGTH);
            string monstersDisposition = GetField(parts, 117);
            string monstersJoining = GetField(parts, 119);
            bool monstersJoinMoney = GetField(parts, 120) == "x";

            // Object placement per zone
            string placement = GetField(parts, 109);
            string minObjects = GetField(parts, 111);
            bool forceNeutral = GetField(parts, 113) == "x";
            string zoneRepulsion = GetField(parts, 115);
            string terrainHint = GetField(parts, 123);

            // Placement coordinates (HotA [112] "Image settings") — drives canvas position.
            // Parsed as a centre point (rectangle) or point; null when auto-placed ("x"/"").
            string placementCoordsRaw = GetField(parts, FIELD_PLACEMENT_COORDS);
            (double X, double Y)? placementPos = ParsePlacementCoords(placementCoordsRaw);

            // Objects (encoded) per zone
            string objectsEncoded = GetField(parts, 22);

            // human_start → layout=zone_layout_spawn
            if (isHumanStart)
                layout = "zone_layout_spawn";

            // ── Compute content values from H3T treasure ranges ──
            // Raw values: (low + high) / 2 * count
            double rawGuarded  = AvgNonZero(guardedLow, guardedHigh) * guardedCount;
            double rawUnguarded = AvgNonZero(unguardedLow, unguardedHigh) * unguardedCount;
            double rawResources = AvgNonZero(resourcesLow, resourcesHigh) * resourcesCount;

            // Boost to match OE content density: gCV/uCV +80%, rV +20%
            rawGuarded  *= 1.8;
            rawUnguarded *= 1.8;
            rawResources *= 1.2;

            // Rule 1: resourcesHigh > 3000 → resources go to guarded
            double accGuarded = rawGuarded;
            if (resourcesHigh > 3000)
                accGuarded += rawResources;

            // Rule 2: unguarded ALWAYS goes to guarded
            accGuarded += rawUnguarded;

            // Rule 3: distribute by layout group proportions
            double finalGCV, finalUCV, finalRV;
            if (IsStartLayout(layout))
            {
                // Start-like: 73.9% gCV, 13.6% uCV, 12.5% rV
                finalGCV = accGuarded * 0.739;
                finalUCV = accGuarded * 0.136;
                finalRV  = (resourcesHigh > 3000) ? 0 : accGuarded * 0.125;
            }
            else
            {
                // Treasure-like: 94.0% gCV, 4.0% uCV, 2.0% rV
                finalGCV = accGuarded * 0.940;
                finalUCV = accGuarded * 0.040;
                finalRV  = (resourcesHigh > 3000) ? 0 : accGuarded * 0.020;
            }

            // Create zone with editor defaults (matches TemplateEditorWindow.CreateZone)
            var zone = new Zone
            {
                Name = name,
                Layout = layout,
                Size = TryGetDouble(parts, FIELD_SIZE) / sizeMode,
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.0,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentValue = (int)finalGCV,
                UnguardedContentValue = (int)finalUCV,
                ResourcesValue = (int)finalRV,
                MainObjects = [],
                // Placement from HotA [112]; Y is flipped to Y-down to match the canvas
                // convention used by ImageAnalyzer / ComputeLayout (origin top-left).
                GeneratorPosition = placementPos.HasValue
                    ? (placementPos.Value.X, -placementPos.Value.Y)
                    : null,
            };

            // human_start → Spawn main object with ownership (0→Player1, 1→Player2, ...)
            if (isHumanStart)
            {
                zone.MainObjects.Add(new MainObject
                {
                    Type = "Spawn",
                    Spawn = $"Player{ownership + 1}",
                    RemoveGuardIfHasOwner = true,
                    GuardChance = 1.0,
                    GuardValue = 5000,
                    GuardWeeklyIncrement = 0.20,
                });
            }
            // ── Town (City MainObject) ──
            // HotA zone-row layout (per user): [28]=zone Id, [33]=size, [34..39]=6 ignored
            // fields, [40]=player town COUNT, [43]=neutral town COUNT. A zone's Spawn MainObject
            // (created above ONLY for human starts) ALREADY represents its first player town, so
            // for human starts we add City only for EXTRA towns; for computer/other starts (no Spawn
            // created) every player town needs its own City:
            //   playerCities = isHumanStart ? max(0, playerTowns-1) : playerTowns
            //   totalCities  = playerCities + neutralTowns
            // Faction: fixed when exactly one town faction is allowed (fields[48-59]), else Random
            // (field[74]='x' → match to town). Mirrors the editor's Add MainObject → City defaults.
            int.TryParse(GetField(parts, 40), out int playerTowns);
            int.TryParse(GetField(parts, 43), out int neutralTowns);
            int playerCities = isHumanStart ? Math.Max(0, playerTowns - 1) : playerTowns;
            int totalCities = playerCities + neutralTowns;
            for (int c = 0; c < totalCities; c++)
            {
                var city = new MainObject
                {
                    Type = "City",
                    GuardChance = 1.0,
                    GuardValue = 5000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "default_buildings_construction",
                    Placement = "Uniform",
                };
                if (cities.Count == 1)
                    city.Faction = new TypedSelector { Type = "FromList", Args = [cities[0]] };
                else
                    city.Faction = new TypedSelector { Type = "Random", Args = [] };
                zone.MainObjects.Add(city);
            }

            // Auto-fill biome from terrain (single-terrain zones → FromList, multi-terrain → MatchMainObject)
            zone.ZoneBiome = ResolveTerrainBiome(terrain);

            // Auto-fill biome defaults (match editor's CreateZone)
            zone.ContentBiome ??= new BiomeSelector { Type = "MatchMainObject", Args = ["0"] };
            zone.MetaObjectsBiome ??= new BiomeSelector { Type = "MatchMainObject", Args = ["0"] };

            // Store additional parsed data in zone metadata
            zone.MetaData = new Dictionary<string, string>
            {
                ["guarded_low"] = guardedLow.ToString(),
                ["guarded_high"] = guardedHigh.ToString(),
                ["guarded_count"] = guardedCount.ToString(),
                ["unguarded_low"] = unguardedLow.ToString(),
                ["unguarded_high"] = unguardedHigh.ToString(),
                ["unguarded_count"] = unguardedCount.ToString(),
                ["resources_low"] = resourcesLow.ToString(),
                ["resources_high"] = resourcesHigh.ToString(),
                ["resources_count"] = resourcesCount.ToString(),
                ["cities"] = string.Join(",", cities),
                ["terrain"] = string.Join(",", terrain),
                ["monsters"] = string.Join(",", monsters),
                ["resources_start"] = string.Join(",", resources.Select(r => $"{r.Key}={r.Value}")),
                ["strength"] = strength,
                ["monsters_disposition"] = monstersDisposition,
                ["monsters_joining"] = monstersJoining,
                ["monsters_join_money"] = monstersJoinMoney.ToString(),
                ["placement"] = placement,
                ["min_objects"] = minObjects,
                ["force_neutral"] = forceNeutral.ToString(),
                ["zone_repulsion"] = zoneRepulsion,
                ["terrain_hint"] = terrainHint,
                ["objects_encoded"] = objectsEncoded,
                ["human_start"] = isHumanStart.ToString(),
                ["computer_start"] = isComputerStart.ToString(),
                ["ownership"] = ownership.ToString(),
                ["placement_coords"] = placementCoordsRaw,
            };

            // Assign default content pools based on layout
            AssignDefaultPools(zone, layout);

            return zone;
        }

        private static Connection? ParseConnection(string[] parts, string[] fields, Dictionary<int, string> zoneIds)
        {
            string z1Str = GetField(parts, FIELD_ZONE_1);
            string z2Str = GetField(parts, FIELD_ZONE_2);

            if (string.IsNullOrEmpty(z1Str) || string.IsNullOrEmpty(z2Str))
                return null;

            if (!int.TryParse(z1Str, out int z1) || !int.TryParse(z2Str, out int z2))
                return null;

            if (!zoneIds.TryGetValue(z1, out string? fromName)) fromName = $"Zone-{z1}";
            if (!zoneIds.TryGetValue(z2, out string? toName)) toName = $"Zone-{z2}";

            int.TryParse(GetField(parts, FIELD_VALUE), out int value);
            bool hasRoad = GetField(parts, FIELD_ROAD) == "+";

            string connTypeStr = GetField(parts, FIELD_CONNECTION_TYPE);
            string connType = connTypeStr switch
            {
                "1" => "Portal",
                "2" => "Proximity",
                "3" => "GladiatorArena",
                _ => "Default",
            };

            return new Connection
            {
                Name = $"Conn-{fromName}-{toName}",
                From = fromName,
                To = toName,
                ConnectionType = connType,
                GuardValue = value,
                Road = hasRoad,
            };
        }

        private static void AssignDefaultPools(Zone zone, string layout)
        {
            var pools = layout switch
            {
                "zone_layout_supertreasure_zone" => (
                    guarded: new[] { "content_pool_template_mini_nosta_guarded_supertreasure_zone" },
                    unguarded: new[] { "content_pool_template_mini_nosta_unguarded_supertreasure_zone" },
                    resources: new[] { "content_pool_general_resources_start_zone_very_poor" },
                    mandatory: new[] { "mandatory_content_treasure" },
                    limits: new[] { "content_limits_treasure" }),
                "zone_layout_treasure_zone" => (
                    guarded: new[] { "content_pool_template_kerberos_guarded_treasure_zone" },
                    unguarded: new[] { "content_pool_template_kerberos_unguarded_treasure_zone" },
                    resources: new[] { "content_pool_general_resources_treasure_zone_rich" },
                    mandatory: new[] { "mandatory_content_center" },
                    limits: new[] { "content_limits_center" }),
                "zone_layout_sides" or "zone_layout_spawn" => (
                    guarded: new[] { "content_pool_default_guarded" },
                    unguarded: new[] { "content_pool_default_unguarded" },
                    resources: new[] { "content_pool_default_resource" },
                    mandatory: new[] { "mandatory_content_spawns" },
                    limits: new[] { "content_limits_spawns" }),
                "zone_layout_player_spawn" or "zone_layout_ai_spawn" or "zone_layout_start_zone" => (
                    guarded: new[] { "content_pool_template_mini_nosta_guarded_start_zone" },
                    unguarded: new[] { "content_pool_template_mini_nosta_unguarded_start_zone" },
                    resources: new[] { "content_pool_general_resources_start_zone_very_poor" },
                    mandatory: new[] { "mandatory_content_spawn" },
                    limits: new[] { "content_limits_spawn" }),
                _ => (
                    guarded: new[] { "classic_template_pool_random_t2_item" },
                    unguarded: new[] { "classic_template_pool_random_unguarded_t2_item" },
                    resources: new[] { "content_pool_general_resources_start_zone_medium" },
                    mandatory: new[] { "mandatory_content_side" },
                    limits: new[] { "content_limits_side" }),
            };

            zone.GuardedContentPool = pools.guarded.ToList();
            zone.UnguardedContentPool = pools.unguarded.ToList();
            zone.ResourcesContentPool = pools.resources.ToList();
            zone.MandatoryContent = pools.mandatory.ToList();
            zone.ContentCountLimits = pools.limits.ToList();
        }

        private static string[] MergeQuotedLines(string[] rawLines)
        {
            var result = new List<string>();
            string? pending = null;

            foreach (var line in rawLines)
            {
                if (pending != null)
                {
                    pending += "\n" + line;
                    // Count quotes in the pending string
                    int quoteCount = pending.Count(c => c == '\"');
                    if (quoteCount % 2 == 0)
                    {
                        result.Add(pending);
                        pending = null;
                    }
                }
                else
                {
                    int quoteCount = line.Count(c => c == '\"');
                    if (quoteCount % 2 == 1)
                    {
                        // Unmatched quote — start accumulating
                        pending = line;
                    }
                    else
                    {
                        result.Add(line);
                    }
                }
            }

            if (pending != null)
                result.Add(pending);

            return result.ToArray();
        }

        private static string GetField(string[] parts, int index)
        {
            return index < parts.Length ? parts[index].Trim() : "";
        }

        private static double TryGetDouble(string[] parts, int index)
        {
            string val = GetField(parts, index);
            if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double result) && result > 0)
                return result;
            return 1.0; // fallback when missing/invalid → 1.0 / mode
        }

        /// <summary>
        /// Average of non-zero values among (a, b). If both are 0 → 0.
        /// Used to compute content values ignoring empty/placeholder zeroes.
        /// </summary>
        private static int AvgNonZero(int a, int b)
        {
            if (a != 0 && b != 0) return (a + b) / 2;
            if (a != 0) return a;
            if (b != 0) return b;
            return 0;
        }

        /// <summary>
        /// Layout group classification for H3T content value distribution.
        /// Start-like layouts get 73.9/13.6/12.5 split; treasure-like get 94/4/2.
        /// </summary>
        private static bool IsStartLayout(string layout) => layout is
            "zone_layout_spawn" or "zone_layout_spawns" or "zone_layout_start_zone" or
            "zone_layout_sides" or "zone_layout_side_zone" or "zone_layout_side_spawn_zone" or
            "zone_layout_ai_spawn" or "zone_layout_second_spawn" or "zone_layout_leaf" or
            "zone_layout_wincondition_zone" or "zone_layout_player_spawn" or "zone_layout_back";

        // ── H3T terrain → engine biome mapping ──
        // H3T terrains: Dirt, Sand, Grass, Snow, Swamp, Rough, Cave, Lava, Highlands, Wasteland
        // Engine biomes: Grass, Snow, Lava, Sand, Dirt, Deathland, Autumn
        private static readonly Dictionary<string, string> TerrainToBiome = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dirt"] = "Dirt",
            ["Sand"] = "Sand",
            ["Grass"] = "Grass",
            ["Snow"] = "Snow",
            ["Swamp"] = "Swamp",
            ["Rough"] = "Rough",
            ["Cave"] = "Cave",
            ["Lava"] = "Lava",
            ["Highlands"] = "Highlands",
            ["Wasteland"] = "Deathland",
        };

        /// <summary>
        /// Resolves zone terrain list to a biome selector.
        /// Single mapped terrain → FromList with that biome.
        /// Multiple mapped terrains or none → null (caller uses MatchMainObject default).
        /// </summary>
        private static BiomeSelector? ResolveTerrainBiome(List<string> terrain)
        {
            var mapped = terrain
                .Where(t => TerrainToBiome.TryGetValue(t, out _))
                .Select(t => TerrainToBiome[t])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (mapped.Count == 1)
                return new BiomeSelector { Type = "FromList", Args = [mapped[0]] };

            // Multi-terrain or no terrain → auto biome by main object (never null, so the
            // zone always has an automatically-selected biome in the inspector).
            return new BiomeSelector { Type = "MatchMainObject", Args = ["0"] };
        }

        // ── Auto-generate zone roads from connections with Road=true ──
        // Mirrors TemplateEditorWindow.AutoGenerateRoadsForConnection + AddRoadToZone

        /// <summary>
        /// After all connections are parsed, auto-generate Road entries in each zone
        /// for connections that have Road=true. Logic mirrors the editor's behavior.
        /// </summary>
        private static void AutoGenerateRoadsFromConnections(List<Zone> zones, List<Connection> connections)
        {
            foreach (var conn in connections)
            {
                if (conn.Road != true) continue;

                string connName = conn.Name ?? $"{conn.From}-{conn.To}";
                var fromZone = zones.FirstOrDefault(z => z.Name == conn.From);
                var toZone = zones.FirstOrDefault(z => z.Name == conn.To);

                if (fromZone != null) AddRoadToZone(fromZone, connName, zones, connections);
                if (toZone != null) AddRoadToZone(toZone, connName, zones, connections);
            }
        }

        /// <summary>
        /// Adds a road entry to a zone for a given connection.
        /// Zone with castle (City/AbandonedOutpost): MainObject[0] → Connection
        /// Castle-less zone: star pattern among road connections.
        /// </summary>
        private static void AddRoadToZone(Zone zone, string connectionName,
            List<Zone> zones, List<Connection> connections)
        {
            zone.Roads ??= [];

            int castleCount = zone.MainObjects?.Count(o =>
                o.Type == "City" || o.Type == "AbandonedOutpost") ?? 0;

            Road newRoad;
            if (castleCount > 0)
            {
                // Zone with castle: MainObject[0] → Connection
                newRoad = new Road
                {
                    From = new RoadEndpoint { Type = "MainObject", Args = ["0"] },
                    To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
                };
            }
            else
            {
                // Castle-less zone: star among road connections
                var incident = connections
                    .Where(c => c.Road == true
                            && (string.Equals(c.From, zone.Name, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(c.To, zone.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(c => c.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
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
                r.To?.Args?.FirstOrDefault() == newRoad.To?.Args?.FirstOrDefault());

            if (!exists)
                zone.Roads.Add(newRoad);
        }

        /// <summary>
        /// Builds the road for a castle-less zone. Single incident connection → self-loop.
        /// Multiple → star pattern from anchor to each spoke.
        /// </summary>
        private static Road? BuildCastleLessRoad(string connectionName, IReadOnlyList<string> incidentConnections)
        {
            if (incidentConnections.Count < 2)
            {
                if (incidentConnections.Count == 1)
                {
                    return new Road
                    {
                        From = new RoadEndpoint { Type = "Connection", Args = [connectionName] },
                        To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
                    };
                }
                return null;
            }

            string anchor = incidentConnections[0];
            // Anchor connection: no self-loop, just spokes from other connections
            if (string.Equals(connectionName, anchor, StringComparison.OrdinalIgnoreCase))
                return null;

            return new Road
            {
                From = new RoadEndpoint { Type = "Connection", Args = [anchor] },
                To = new RoadEndpoint { Type = "Connection", Args = [connectionName] }
            };
        }
    }
}
