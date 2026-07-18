using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Xunit;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor;
using Olden_Era___Template_Editor.Localization;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models.Generated;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace OldenEraTemplateEditor.Tests;

public class ConnectionSerializationTests
{
    [Fact]
    public void Serialize_Connection_IncludesAllNewProperties()
    {
        var conn = new Connection
        {
            Name = "test",
            From = "A",
            To = "B",
            ConnectionType = "Portal",
            GuardValue = 150,
            GuardZone = "Zone1",
            GuardEscape = true,
            SimTurnSquad = true,
            GuardWeeklyIncrement = 0.5,
            GuardMatchGroup = "my_group",
            GatePlacement = "Center",
            GatePlacementArgs = new List<string> { "Zone2", "Zone3" },
            GuardRandomization = 0.1,
            Road = true,
            Length = 0.8,
            PortalPlacementRulesFrom = new List<ContentPlacementRule>
            {
                new() { Type = "Crossroads", Args = new List<string> { "arg1" }, TargetMin = 0.0, TargetMax = 0.0, Weight = 2 }
            },
            PortalPlacementRulesTo = new List<ContentPlacementRule>
            {
                new() { Type = "Crossroads", Args = null, TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }
            },
        };

        var json = JsonSerializer.Serialize(conn, new JsonSerializerOptions { WriteIndented = false });
        var deserialized = JsonSerializer.Deserialize<Connection>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("test", deserialized.Name);
        Assert.Equal("Portal", deserialized.ConnectionType);
        Assert.Equal(150, deserialized.GuardValue);
        Assert.True(deserialized.GuardEscape);
        Assert.True(deserialized.SimTurnSquad);
        Assert.Equal(0.5, deserialized.GuardWeeklyIncrement);
        Assert.Equal("my_group", deserialized.GuardMatchGroup);
        Assert.Equal("Center", deserialized.GatePlacement);
        Assert.Equal(2, deserialized.GatePlacementArgs!.Count);
        Assert.Equal("Zone2", deserialized.GatePlacementArgs[0]);
        Assert.Equal("Zone3", deserialized.GatePlacementArgs[1]);
        Assert.Equal(0.1, deserialized.GuardRandomization);
        Assert.True(deserialized.Road);
        Assert.Equal(0.8, deserialized.Length);
        Assert.Single(deserialized.PortalPlacementRulesFrom!);
        Assert.Equal("Crossroads", deserialized.PortalPlacementRulesFrom![0].Type);
        Assert.Equal("arg1", deserialized.PortalPlacementRulesFrom![0].Args![0]);
        Assert.Equal(2, deserialized.PortalPlacementRulesFrom![0].Weight);
        Assert.Single(deserialized.PortalPlacementRulesTo!);
        Assert.Equal(1, deserialized.PortalPlacementRulesTo![0].Weight);
    }

    [Fact]
    public void Serialize_Connection_Minimal()
    {
        var conn = new Connection
        {
            From = "A", To = "B",
        };

        var json = JsonSerializer.Serialize(conn);
        var deserialized = JsonSerializer.Deserialize<Connection>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("A", deserialized.From);
        Assert.Equal("B", deserialized.To);
        Assert.Null(deserialized.GuardValue);
        Assert.Null(deserialized.GuardEscape);
        Assert.Null(deserialized.SimTurnSquad);
        Assert.Null(deserialized.GuardWeeklyIncrement);
        Assert.Null(deserialized.GuardMatchGroup);
        Assert.Null(deserialized.GatePlacement);
        Assert.Null(deserialized.GuardRandomization);
        Assert.Null(deserialized.Length);
        Assert.Null(deserialized.PortalPlacementRulesFrom);
        Assert.Null(deserialized.PortalPlacementRulesTo);
    }

    [Fact]
    public void JsonPropertyNames_MatchGameFormat()
    {
        var conn = new Connection
        {
            GuardRandomization = 0.05,
            GuardMatchGroup = "test_group",
            GatePlacement = "Center",
        };

        var json = JsonSerializer.Serialize(conn);
        Assert.Contains("\"guardRandomization\"", json);
        Assert.Contains("\"guardMatchGroup\"", json);
        Assert.Contains("\"gatePlacement\"", json);
        Assert.Contains("\"gatePlacementArgs\"", json);
        Assert.Contains("\"portalPlacementRulesFrom\"", json);
        Assert.Contains("\"portalPlacementRulesTo\"", json);
    }
}

public class LocalizationKeysTests
{
    [Fact]
    public void RuAndEnHaveSameKeys()
    {
        var ruKeys = Strings.Ru.Keys.OrderBy(k => k).ToList();
        var enKeys = Strings.En.Keys.OrderBy(k => k).ToList();

        Assert.Equal(ruKeys.Count, enKeys.Count);
        for (int i = 0; i < ruKeys.Count; i++)
            Assert.Equal(ruKeys[i], enKeys[i]);
    }

    [Fact]
    public void NewConnectionKeysExist()
    {
        var keys = new[]
        {
            "S.EC.GuardRandomizationConn",
            "S.EC.GatePlacement",
            "S.EC.GatePlacementArgs",
            "S.EC.Advanced",
            "S.EC.AdvancedNote",
            "S.EC.PortalRulesFrom",
            "S.EC.PortalRulesTo",
            "S.EC.PlacementRuleType",
            "S.EC.PlacementRuleArgs",
            "S.EC.PlacementRuleTargetMin",
            "S.EC.PlacementRuleTargetMax",
            "S.EC.PlacementRuleWeight",
            "S.EC.AddRule",
            "S.EC.RemoveRule",
        };

        foreach (var key in keys)
        {
            Assert.True(Strings.Ru.ContainsKey(key), $"Missing RU key: {key}");
            Assert.True(Strings.En.ContainsKey(key), $"Missing EN key: {key}");
        }
    }
}

public class ContentPlacementRuleSerializationTests
{
    [Fact]
    public void Serialize_ContentPlacementRule_RoundTrip()
    {
        var rule = new ContentPlacementRule
        {
            Type = "Crossroads",
            Args = new List<string> { "zone1", "zone2" },
            TargetMin = 1.0,
            TargetMax = 5.0,
            Weight = 3,
        };

        var json = JsonSerializer.Serialize(rule);
        var deserialized = JsonSerializer.Deserialize<ContentPlacementRule>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Crossroads", deserialized.Type);
        Assert.Equal(2, deserialized.Args!.Count);
        Assert.Equal("zone1", deserialized.Args[0]);
        Assert.Equal("zone2", deserialized.Args[1]);
        Assert.Equal(1.0, deserialized.TargetMin);
        Assert.Equal(5.0, deserialized.TargetMax);
        Assert.Equal(3, deserialized.Weight);
    }

    [Fact]
    public void JsonPropertyNames_MatchGameFormat()
    {
        var rule = new ContentPlacementRule
        {
            Type = "Crossroads",
            Args = new List<string>(),
            TargetMin = 0,
            TargetMax = 0,
            Weight = 1,
        };

        var json = JsonSerializer.Serialize(rule);
        Assert.Contains("\"type\"", json);
        Assert.Contains("\"args\"", json);
        Assert.Contains("\"targetMin\"", json);
        Assert.Contains("\"targetMax\"", json);
        Assert.Contains("\"weight\"", json);
    }

    [Fact]
    public void Deserialize_GameFormat()
    {
        var json = @"{
            ""type"": ""Crossroads"",
            ""args"": [],
            ""targetMin"": 0.0,
            ""targetMax"": 0.0,
            ""weight"": 2
        }";

        var rule = JsonSerializer.Deserialize<ContentPlacementRule>(json);
        Assert.NotNull(rule);
        Assert.Equal("Crossroads", rule.Type);
        Assert.NotNull(rule.Args);
        Assert.Empty(rule.Args);
        Assert.Equal(0.0, rule.TargetMin);
        Assert.Equal(0.0, rule.TargetMax);
        Assert.Equal(2, rule.Weight);
    }
}

public class SpawnUniquenessTests
{
    private static Zone Zone(string name, params (string Type, string? Owner, string? Spawn)[] mos) => new()
    {
        Name = name,
        MainObjects = mos.Select(m => new MainObject { Type = m.Type, Owner = m.Owner, Spawn = m.Spawn }).ToList(),
    };

    private static RmgTemplate Template(params Zone[] zones) => new()
    {
        Variants = [new Variant { Zones = [.. zones] }],
    };

    [Fact]
    public void EnsureUniqueSpawns_AssignsDistinctPlayersAcrossZones()
    {
        var z1 = Zone("A", ("City", null, "Player1"));
        var z2 = Zone("B", ("City", null, "Player1"));
        var z3 = Zone("C", ("City", null, "Player1"));

        TemplateEditorWindow.EnsureUniqueSpawns(Template(z1, z2, z3));

        Assert.Equal("Player1", z1.MainObjects[0].Spawn);
        Assert.Equal("Player2", z2.MainObjects[0].Spawn);
        Assert.Equal("Player3", z3.MainObjects[0].Spawn);
    }

    [Fact]
    public void EnsureUniqueSpawns_CapsAtNineValues_OverflowBecomesEmpty()
    {
        var zones = new List<Zone>();
        for (int i = 1; i <= 9; i++)
            zones.Add(Zone($"Z{i}", ("City", null, "Player1")));

        TemplateEditorWindow.EnsureUniqueSpawns(Template(zones.ToArray()));

        var spawns = zones.Select(z => z.MainObjects[0].Spawn).ToList();
        Assert.Equal(new[] { "Player1", "Player2", "Player3", "Player4", "Player5", "Player6", "Player7", "Player8", null }, spawns);
    }

    [Fact]
    public void EnsureUniqueSpawns_LeavesEmptyAndUnsetSpawnUntouched()
    {
        var z = Zone("A", ("City", null, null), ("Spawn", null, "Player1"));
        TemplateEditorWindow.EnsureUniqueSpawns(Template(z));
        Assert.Null(z.MainObjects[0].Spawn);
        Assert.Equal("Player1", z.MainObjects[1].Spawn);
    }

    [Fact]
    public void EnsureUniqueSpawns_SyncsOwnerOnFirstMainObject()
    {
        var z = Zone("A", ("City", null, "Player5"));
        TemplateEditorWindow.EnsureUniqueSpawns(Template(z));
        Assert.Equal("Player5", z.MainObjects[0].Owner);

        var z2 = Zone("B", ("City", null, null));
        TemplateEditorWindow.EnsureUniqueSpawns(Template(z2));
        Assert.Null(z2.MainObjects[0].Owner);
    }

    [Fact]
    public void EnsureUniqueSpawns_ReassignsDuplicateOnNonFirstMainObject()
    {
        var z = Zone("A", ("City", null, "Player2"), ("Spawn", null, "Player2"));
        TemplateEditorWindow.EnsureUniqueSpawns(Template(z));
        // First MO keeps Player2 (no conflict); second MO duplicate is reassigned to the free Player1
        Assert.Equal("Player2", z.MainObjects[0].Spawn);
        Assert.Equal("Player1", z.MainObjects[1].Spawn);
    }
}

public class CastleLessRoadTests
{
    private static List<string> Sorted(params string[] names) => [.. names.OrderBy(n => n, StringComparer.Ordinal)];

    [Fact]
    public void TwoIncidentConnections_ProducesOneRoadNoSelfLoop()
    {
        var road = TemplateEditorWindow.BuildCastleLessRoad("B", Sorted("A", "B"));
        Assert.NotNull(road);
        Assert.Equal("A", road!.From!.Args[0]);
        Assert.Equal("B", road.To!.Args[0]);
        Assert.NotEqual(road.From.Args[0], road.To!.Args[0]);
    }

    [Fact]
    public void AnchorConnection_ReturnsNull()
    {
        // The first (alphabetically smallest) incident connection is the star anchor and gets no road.
        Assert.Null(TemplateEditorWindow.BuildCastleLessRoad("A", Sorted("A", "B", "C", "D")));
    }

    [Fact]
    public void FourIncidentConnections_SpokesFromAnchor_NoSelfLoop()
    {
        var names = Sorted("Direct-Zone-1-hub", "Direct-Zone-2-hub", "Direct-Zone-3-hub", "Direct-Zone-4-hub");
        var anchor = names[0];
        foreach (var n in names.Skip(1))
        {
            var road = TemplateEditorWindow.BuildCastleLessRoad(n, names);
            Assert.NotNull(road);
            Assert.Equal(anchor, road!.From!.Args[0]);
            Assert.Equal(n, road.To!.Args[0]);
            Assert.NotEqual(road.From.Args[0], road.To!.Args[0]); // no self-loop
        }
    }

    [Fact]
    public void SingleIncidentConnection_ReturnsSelfLoop()
    {
        var road = TemplateEditorWindow.BuildCastleLessRoad("Only", Sorted("Only"));
        Assert.NotNull(road);
        Assert.Equal("Only", road!.From!.Args[0]);
        Assert.Equal("Only", road.To!.Args[0]);
    }

    [Fact]
    public void NoIncidentConnections_ReturnsNull()
    {
        Assert.Null(TemplateEditorWindow.BuildCastleLessRoad("X", []));
    }

    private static Connection Conn(string name, string from, string to, bool road) =>
        new() { Name = name, From = from, To = to, Road = road };

    [Fact]
    public void RoadIncidentConnections_OnlyRoadFlaggedSiblingsParticipate()
    {
        // Reproduces the mirror-mode bleed: a hub with 4 incident connections where only ONE is Road==true.
        // The star must involve ONLY the road connection — never promote the other siblings to Road.
        var hub = new Zone { Name = "hub" };
        var conns = new List<Connection>
        {
            Conn("Direct-Zone-1-hub", "Zone-1", "hub", road: true),
            Conn("Direct-Zone-2-hub", "Zone-2", "hub", road: false),
            Conn("Direct-Zone-3-hub", "Zone-3", "hub", road: false),
            Conn("Direct-Zone-4-hub", "Zone-4", "hub", road: false),
        };

        var incident = TemplateEditorWindow.RoadIncidentConnections(hub, conns).ToList();

        Assert.Single(incident);
        Assert.Equal("Direct-Zone-1-hub", incident[0]);
    }

    [Fact]
    public void RoadIncidentConnections_TwoRoadFlagged_ReturnsBoth()
    {
        var hub = new Zone { Name = "hub" };
        var conns = new List<Connection>
        {
            Conn("A-hub", "A", "hub", road: true),
            Conn("B-hub", "B", "hub", road: true),
            Conn("C-hub", "C", "hub", road: false),
        };

        var incident = TemplateEditorWindow.RoadIncidentConnections(hub, conns).ToList();

        Assert.Equal(2, incident.Count);
        Assert.Contains("A-hub", incident);
        Assert.Contains("B-hub", incident);
        Assert.DoesNotContain("C-hub", incident);
    }

    [Fact]
    public void RoadIncidentConnections_NoRoadFlagged_ReturnsEmpty()
    {
        var hub = new Zone { Name = "hub" };
        var conns = new List<Connection>
        {
            Conn("A-hub", "A", "hub", road: false),
            Conn("B-hub", "B", "hub", road: false),
        };

        Assert.Empty(TemplateEditorWindow.RoadIncidentConnections(hub, conns));
    }

    [Fact]
    public void OneRoadFlaggedHub_StarIsSelfLoop_NoSiblingPromoted()
    {
        // With a single Road==true incident connection the hub gets a self-loop and the non-road
        // siblings stay out of the star — so a reload cannot promote them to Road.
        var names = new List<string> { "Direct-Zone-1-hub" }; // only the road connection remains
        var road = TemplateEditorWindow.BuildCastleLessRoad("Direct-Zone-1-hub", names);
        Assert.NotNull(road);
        Assert.Equal("Direct-Zone-1-hub", road!.From!.Args[0]);
        Assert.Equal("Direct-Zone-1-hub", road.To!.Args[0]);
    }
}

public class GameDataEmbeddedResourceTests
{
    [Fact]
    public void Load_ReadsGameDataFromEmbeddedResource()
    {
        // In the test output there is no GameData.json on disk, so Load() must fall back to the
        // embedded resource (the same path the single-file published .exe uses).
        GamePoolDataLoader.Load();
        Assert.StartsWith("✓", GamePoolDataLoader.Status);
        Assert.NotEmpty(GamePoolDataLoader.GetAllPools());
    }
}

public class ForceDirectedLayoutTests
{
    private static Zone Z(string name) => new() { Name = name };

    private static Connection C(string from, string to) => new()
    {
        Name = $"{from}-{to}",
        From = from,
        To = to,
        ConnectionType = "Road",
        Road = true,
    };

    private static RmgTemplate BuildTemplate(params Connection[] conns)
    {
        var zones = conns
            .SelectMany(c => new[] { c.From, c.To })
            .Distinct(StringComparer.Ordinal)
            .Select(Z)
            .ToArray();
        return new RmgTemplate
        {
            Variants = [new Variant { Zones = [.. zones], Connections = [.. conns] }],
        };
    }

    [Theory]
    [InlineData(MapTopology.Default)]
    [InlineData(MapTopology.HubAndSpoke)]
    [InlineData(MapTopology.Chain)]
    [InlineData(MapTopology.SharedWeb)]
    public void ForceDirected_NoOverlappingZones(MapTopology topology)
    {
        var conns = new[]
        {
            C("hub", "s1"), C("hub", "s2"), C("hub", "s3"), C("hub", "s4"),
            C("d1", "d2"),
        };
        var template = BuildTemplate(conns);
        var layout = TemplatePreviewPngWriter.ComputeLayout(template, topology);
        var radius = TemplatePreviewPngWriter.GetLastZoneRadius();

        Assert.Equal(7, layout.Count);

        var names = layout.Keys.ToList();
        for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                var a = layout[names[i]];
                var b = layout[names[j]];
                double dist = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                Assert.True(dist >= 2.0 * radius - 1.0,
                    $"[{topology}] Zones {names[i]} and {names[j]} overlap (dist={dist:F1}, min={2.0 * radius:F1})");
            }
    }

    [Fact]
    public void ForceDirected_ConnectedZonesAreCloserThanUnconnected()
    {
        var conns = new[]
        {
            C("hub", "s1"), C("hub", "s2"), C("hub", "s3"), C("hub", "s4"),
            C("d1", "d2"),
        };
        var template = BuildTemplate(conns);
        var layout = TemplatePreviewPngWriter.ComputeLayout(template, MapTopology.Default);

        double Edge(string from, string to)
        {
            var a = layout[from];
            var b = layout[to];
            return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        var connected = conns.Select(c => Edge(c.From, c.To)).ToList();
        var names = layout.Keys.ToList();
        var unconnected = new List<double>();
        for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                bool linked = conns.Any(c =>
                    (c.From == names[i] && c.To == names[j]) ||
                    (c.From == names[j] && c.To == names[i]));
                if (!linked) unconnected.Add(Edge(names[i], names[j]));
            }

        double meanConnected = connected.Average();
        double meanUnconnected = unconnected.Average();
        Assert.True(meanConnected < meanUnconnected,
            $"Connected zones should be closer on average (connected={meanConnected:F1}, unconnected={meanUnconnected:F1})");
    }

    [Fact]
    public void ForceDirected_IsDeterministic()
    {
        var conns = new[]
        {
            C("hub", "s1"), C("hub", "s2"), C("hub", "s3"), C("hub", "s4"),
            C("d1", "d2"),
        };
        var template = BuildTemplate(conns);

        var l1 = TemplatePreviewPngWriter.ComputeLayout(template, MapTopology.Default);
        var l2 = TemplatePreviewPngWriter.ComputeLayout(template, MapTopology.Default);

        foreach (var name in l1.Keys)
            Assert.Equal(l1[name], l2[name]);
    }
}

public class AuroraRmgCoordTests
{
    [Fact]
    public void Serialize_WritesAuroraRmgBlockBeforeName()
    {
        var template = new RmgTemplate
        {
            Name = "MyTemplate",
            Variants = [new Variant { Zones = [new Zone { Name = "A" }, new Zone { Name = "B" }] }],
            AuroraRmg = new AuroraRmgCoords
            {
                Zones =
                [
                    new ZoneCoord { Name = "A", X = 120.5, Y = 240 },
                    new ZoneCoord { Name = "B", X = 10, Y = 20 },
                ],
            },
        };

        var json = JsonSerializer.Serialize(template, JsonExport.Options);
        int auroraIdx = json.IndexOf("\"AuroraRMG\"", System.StringComparison.Ordinal);
        int nameIdx = json.IndexOf("\"name\"", System.StringComparison.Ordinal);
        Assert.True(auroraIdx >= 0 && auroraIdx < nameIdx, "AuroraRMG block must appear before name");
        // Coordinate keys must be the non-colliding "coords"/"id", not the game's "zones"/"name".
        Assert.True(json.Contains("\"coords\"", System.StringComparison.Ordinal), "coordinate array key must be 'coords'");
        Assert.True(json.Contains("\"id\"", System.StringComparison.Ordinal), "coordinate entry key must be 'id'");

        var round = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options);
        Assert.NotNull(round?.AuroraRmg?.Zones);
        var a = round!.AuroraRmg!.Zones!.First(z => z.Name == "A");
        Assert.Equal(120.5, a.X);
        Assert.Equal(240.0, a.Y);
    }

    [Fact]
    public void ApplyAuroraRmgPositions_OverridesMatchedZones_KeepsOthers()
    {
        var tmpl = new RmgTemplate
        {
            AuroraRmg = new AuroraRmgCoords
            {
                Zones =
                [
                    new ZoneCoord { Name = "A", X = 5, Y = 6 },
                    new ZoneCoord { Name = "B", X = 7, Y = 8 },
                ],
            },
        };
        var positions = new Dictionary<string, System.Windows.Point>(StringComparer.Ordinal)
        {
            ["A"] = new System.Windows.Point(0, 0),
            ["B"] = new System.Windows.Point(1, 1),
            ["C"] = new System.Windows.Point(2, 2),
        };

        TemplateEditorWindow.ApplyAuroraRmgPositions(tmpl, positions);

        Assert.Equal(5, positions["A"].X);
        Assert.Equal(6, positions["A"].Y);
        Assert.Equal(7, positions["B"].X);
        Assert.Equal(8, positions["B"].Y);
        Assert.Equal(2, positions["C"].X); // absent from block → untouched
    }

    [Fact]
    public void ApplyAuroraRmgPositions_NullBlock_NoOp()
    {
        var tmpl = new RmgTemplate { AuroraRmg = null };
        var positions = new Dictionary<string, System.Windows.Point>(StringComparer.Ordinal)
        {
            ["A"] = new System.Windows.Point(1, 2),
        };
        TemplateEditorWindow.ApplyAuroraRmgPositions(tmpl, positions);
        Assert.Equal(1, positions["A"].X);
    }
}

public class H3TAndMapSizeTests
{
    [Theory]
    [InlineData(1, 36)]
    [InlineData(2, 36)]
    [InlineData(4, 72)]
    [InlineData(8, 72)]
    [InlineData(9, 108)]
    [InlineData(16, 144)]
    [InlineData(18, 108)]
    [InlineData(25, 180)]
    [InlineData(32, 144)]
    [InlineData(36, 216)]
    [InlineData(49, 252)]
    [InlineData(50, 180)]
    [InlineData(72, 216)]
    [InlineData(99, 252)]
    [InlineData(0, 160)]    // unknown code → fallback
    [InlineData(123, 160)]  // unknown code → fallback
    public void DecodeH3TMapSize_DecodesToSurfaceSize(int code, int expected)
    {
        Assert.Equal(expected, H3TParser.DecodeH3TMapSize(code));
    }

    [Theory]
    [InlineData(36, 64)]
    [InlineData(72, 80)]
    [InlineData(108, 112)]
    [InlineData(144, 144)]
    [InlineData(180, 176)]
    [InlineData(216, 208)]
    [InlineData(252, 256)]
    [InlineData(64, 64)]
    [InlineData(0, 64)]     // non-positive → smallest supported
    public void NearestMapSize_SnapsToSupportedSize(int size, int expected)
    {
        Assert.Equal(expected, KnownValues.NearestMapSize(size));
    }
}

public class TemplatePoolsViewerTests
{
    private static (List<MandatoryContentGroup> Mc, List<ContentCountLimit> Cl) SampleData()
    {
        var mc = new List<MandatoryContentGroup>
        {
            new MandatoryContentGroup
            {
                Name = "mandatory_content_spawns",
                Content = new List<ContentItem>
                {
                    new ContentItem { Sid = "mine_wood", IsMine = true, IsGuarded = true },
                    new ContentItem
                    {
                        Sid = "watchtower", IsMine = false, IsGuarded = true,
                        Rules = new List<ContentPlacementRule>
                        {
                            new ContentPlacementRule { Type = "Crossroads", Args = new List<string>(), TargetMin = 0.10, TargetMax = 0.20, Weight = 1 },
                        },
                    },
                },
            },
        };
        var cl = new List<ContentCountLimit>
        {
            new ContentCountLimit
            {
                Name = "content_limits_center",
                Limits = new List<ContentSidLimit>
                {
                    new ContentSidLimit { Sid = "beer_fountain", MaxCount = 1 },
                    new ContentSidLimit { Sid = "market", MaxCount = 1 },
                },
            },
        };
        return (mc, cl);
    }

    [Fact]
    public void TemplatePoolBuilder_TagsMandatoryAndContentLimitPools()
    {
        var (mc, cl) = SampleData();
        var (pools, items) = TemplatePoolBuilder.Build(mc, cl);

        Assert.Equal(2, pools.Count);
        var mcPool = pools.First(p => p.Name == "mandatory_content_spawns");
        Assert.Equal("mandatory", mcPool.Tag);
        var clPool = pools.First(p => p.Name == "content_limits_center");
        Assert.Equal("content_limits", clPool.Tag);

        var mcRows = items[mcPool];
        Assert.Contains(mcRows, r => r.Sid == "mine_wood" && (r.Detail ?? "").Contains("isMine"));
        Assert.Contains(mcRows, r => r.Sid == "watchtower" && (r.Detail ?? "").Contains("Crossroads"));
        var clRows = items[clPool];
        // Detail is display-only and shows just the max-count value; the real MaxCount is carried in Weight (serialized separately).
        Assert.Contains(clRows, r => r.Sid == "beer_fountain" && r.Detail == "1" && r.Weight == 1);
    }

    [Fact]
    public void TemplatePoolBuilder_DisplaysIncludeListsAndOptionalVariantMaxCount()
    {
        var mc = new List<MandatoryContentGroup>
        {
            new MandatoryContentGroup
            {
                Name = "mandatory_content_spawns",
                Content = new List<ContentItem>
                {
                    new ContentItem { Sid = "explicit_sid", Variant = 3 },
                    new ContentItem { IncludeLists = new List<string> { "basic_content_list_building_hero_stats_and_skills_tier_3" } },
                    new ContentItem { Sid = "variant_neg", Variant = -1 },
                    new ContentItem { Sid = "no_variant_mc" },
                },
            },
        };
        var cl = new List<ContentCountLimit>
        {
            new ContentCountLimit
            {
                Name = "content_limits_center",
                Limits = new List<ContentSidLimit>
                {
                    new ContentSidLimit { Sid = "beer_fountain", Variant = 2, MaxCount = 6 },
                    new ContentSidLimit { IncludeLists = new List<string> { "some_include_list" }, MaxCount = 4 },
                    new ContentSidLimit { Sid = "no_variant", MaxCount = 5 },
                    new ContentSidLimit { Sid = "no_maxcount", Variant = 1, MaxCount = 0 },
                },
            },
        };

        var (pools, items) = TemplatePoolBuilder.Build(mc, cl);
        var mcPool = pools.First(p => p.Tag == "mandatory");
        var clPool = pools.First(p => p.Tag == "content_limits");
        var mcRows = items[mcPool];
        var clRows = items[clPool];

        // Mandatory "вариант" column uses the item variant field (null -> empty, -1 -> -1).
        Assert.Contains(mcRows, r => r.Sid == "explicit_sid" && r.Variant == 3);
        Assert.Contains(mcRows, r => r.Sid == "basic_content_list_building_hero_stats_and_skills_tier_3" && r.Variant == null);
        Assert.Contains(mcRows, r => r.Sid == "variant_neg" && r.Variant == -1);
        Assert.Contains(mcRows, r => r.Sid == "no_variant_mc" && r.Variant == null);

        // Content limits: Sid can come from the explicit sid or the include-list name.
        Assert.Contains(clRows, r => r.Sid == "beer_fountain" && r.Variant == 2 && r.MaxCount == 6 && r.Detail == "6");
        Assert.Contains(clRows, r => r.Sid == "some_include_list" && r.Variant == null && r.MaxCount == 4 && r.Detail == "4");
        Assert.Contains(clRows, r => r.Sid == "no_variant" && r.Variant == null && r.MaxCount == 5 && r.Detail == "5");
        // Absent maxCount (0) is shown as an empty cell; variant is still shown when present.
        Assert.Contains(clRows, r => r.Sid == "no_maxcount" && r.Variant == 1 && r.MaxCount == 0 && r.Detail == "");
    }

    [Fact]
    public void Serialize_WritesMandatoryContentAndLimitsAtEnd()
    {
        var (mc, cl) = SampleData();
        var template = new RmgTemplate
        {
            Name = "T",
            Variants = [new Variant { Zones = [new Zone { Name = "A" }] }],
            ContentPools = new List<object>(),
            ContentLists = new List<object>(),
            MandatoryContent = mc,
            ContentCountLimits = cl,
        };

        var json = JsonSerializer.Serialize(template, JsonExport.Options);
        int mcIdx = json.IndexOf("\"mandatoryContent\"", System.StringComparison.Ordinal);
        int clIdx = json.IndexOf("\"contentCountLimits\"", System.StringComparison.Ordinal);
        int listsIdx = json.IndexOf("\"contentLists\"", System.StringComparison.Ordinal);
        int varIdx = json.IndexOf("\"variants\"", System.StringComparison.Ordinal);

        Assert.True(listsIdx >= 0, "contentLists must be present");
        Assert.True(mcIdx > listsIdx, "mandatoryContent must be written after contentLists");
        Assert.True(clIdx > listsIdx, "contentCountLimits must be written after contentLists");
        Assert.True(clIdx > mcIdx, "contentCountLimits must be written after mandatoryContent");
        Assert.True(mcIdx > varIdx, "mandatoryContent must be written after variants");

        var round = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options);
        Assert.NotNull(round!.MandatoryContent);
        Assert.Equal("mandatory_content_spawns", round.MandatoryContent[0].Name);
        Assert.Equal("mine_wood", round.MandatoryContent[0].Content![0].Sid);
        Assert.NotNull(round.ContentCountLimits);
        Assert.Equal("content_limits_center", round.ContentCountLimits[0].Name);
        Assert.Equal(1, round.ContentCountLimits[0].Limits![0].MaxCount);
    }
}

public class GameContentCatalogTests
{
    [Fact]
    public void Catalog_LoadsRealPools()
    {
        // Pools are stored per template (not merged by name across files).
        Assert.True(GameContentCatalog.TemplateNames.Count > 0, "templates must be parsed");

        bool foundMc = false, foundCl = false;
        foreach (var key in GameContentCatalog.TemplateNames)
        {
            var mc = GameContentCatalog.GetMandatoryContent(key);
            var cl = GameContentCatalog.GetContentCountLimits(key);
            if (mc != null && mc.Count > 0) foundMc = true;
            if (cl != null && cl.Count > 0) foundCl = true;
        }
        Assert.True(foundMc, "at least one template must have mandatoryContent pools");
        Assert.True(foundCl, "at least one template must have contentCountLimits pools");
    }

    [Fact]
    public void Catalog_ExposesTemplateFiles()
    {
        // Each template key maps back to its source .rmg.json file.
        Assert.NotEmpty(GameContentCatalog.TemplateNames);
        foreach (var key in GameContentCatalog.TemplateNames)
        {
            var file = GameContentCatalog.TemplateFile(key);
            Assert.NotNull(file);
            Assert.EndsWith(".rmg.json", file, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CatalogContent_ResolvesRolesToRealContent()
    {
        // Pools vary per template, so pick a template that actually contains each pool.
        string? mcKey = GameContentCatalog.TemplateNames.FirstOrDefault(k =>
            GameContentCatalog.TryFindMandatoryContent(k, out _, "mandatory_content_spawns"));
        Assert.NotNull(mcKey);
        CatalogContent.UseTemplate(mcKey!);

        Assert.True(CatalogContent.TryGetMc("spawn", out var mc) && mc != null);
        Assert.NotNull(mc!.Content);
        Assert.NotEmpty(mc.Content);
        Assert.All(mc.Content, c => Assert.True(!string.IsNullOrEmpty(c.Sid) || (c.IncludeLists != null && c.IncludeLists.Count > 0)));

        string? clKey = GameContentCatalog.TemplateNames.FirstOrDefault(k =>
            GameContentCatalog.TryFindContentCountLimits(k, out _, "content_limits_side"));
        Assert.NotNull(clKey);
        CatalogContent.UseTemplate(clKey!);

        Assert.True(CatalogContent.TryGetCl("side", out var cl) && cl != null);
        Assert.NotNull(cl!.Limits);
        Assert.NotEmpty(cl.Limits);

        // Role resolves to a real pool name, not the legacy side_A convention.
        CatalogContent.UseTemplate(mcKey!);
        Assert.NotEqual("mandatory_content_side_A", CatalogContent.McName("spawn"));
    }

    [Fact]
    public void Generator_EmitsNonEmptyMandatoryContentAndLimits()
    {
        // Pick a base template whose pools actually carry content (not just the first alphabetically).
        string? baseTemplate = GameContentCatalog.TemplateNames.FirstOrDefault(k =>
        {
            var mc = GameContentCatalog.GetMandatoryContent(k);
            return mc.Any(g => g.Content != null && g.Content.Count > 0);
        }) ?? CatalogContent.ResolveBaseTemplate(null);
        Assert.NotNull(baseTemplate);

        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            MapSize = 160,
            Topology = MapTopology.Balanced,
            BaseTemplate = baseTemplate,
        };
        var template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.MandatoryContent);
        Assert.NotEmpty(template.MandatoryContent);
        var withContent = template.MandatoryContent
            .Where(g => g.Content != null && g.Content.Count > 0).ToList();
        Assert.NotEmpty(withContent);
        foreach (var g in withContent)
            Assert.All(g.Content!, c =>
                Assert.True(!string.IsNullOrEmpty(c.Sid) || (c.IncludeLists != null && c.IncludeLists.Count > 0)));

        Assert.NotNull(template.ContentCountLimits);
        Assert.NotEmpty(template.ContentCountLimits);
        var withLimits = template.ContentCountLimits
            .Where(g => g.Limits != null && g.Limits.Count > 0).ToList();
        Assert.NotEmpty(withLimits);
    }

    [Fact]
    public void Generator_DoesNotThrowWithoutExplicitBaseTemplate()
    {
        // The main "Create template" button builds settings via BuildSettings(), which leaves
        // BaseTemplate null. Generate must fall back to auto-resolution instead of crashing.
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            MapSize = 160,
            Topology = MapTopology.Balanced,
            // BaseTemplate intentionally left null
        };
        var template = TemplateGenerator.Generate(settings);
        Assert.NotNull(template.MandatoryContent);
        Assert.NotEmpty(template.MandatoryContent);
    }
}

public class EditorHelpWindowTests
{
    [Fact]
    public void EditorHelpWindow_OpensWithoutCrash()
    {
        Exception? caught = null;
        var sta = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var app = new System.Windows.Application();
                    app.DispatcherUnhandledException += (s, e) =>
                    {
                        caught ??= e.Exception;
                        e.Handled = true;
                    };
                    var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    for (int i = 0; i < 8 && dir != null && !File.Exists(
                             Path.Combine(dir.FullName, "Olden Era - Template Editor", "Themes", "MedievalTheme.xaml")); i++)
                        dir = dir.Parent;
                    var themePath = Path.Combine(
                        dir!.FullName, "Olden Era - Template Editor", "Themes", "MedievalTheme.xaml");
                    var rd = new System.Windows.ResourceDictionary
                    {
                        Source = new Uri(themePath, UriKind.Absolute),
                    };
                    app.Resources.MergedDictionaries.Add(rd);
                }
                var w = new EditorHelpWindow();
                w.Show();
                // Expand every Expander (incl. "Словарик") to reproduce the crash on toggle.
                var expanders = w.FindVisualChildren<System.Windows.Controls.Expander>();
                foreach (var exp in expanders)
                {
                    exp.IsExpanded = true;
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
                }
                Thread.Sleep(200);
                w.Close();
            }
            catch (Exception ex) { caught = ex; }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        if (caught != null)
            Assert.Fail($"EditorHelpWindow crashed on expand: {caught.GetType()}: {caught.Message}\n{caught.StackTrace}");
        Assert.Null(caught);
    }
}

// Small helper to walk the visual tree (file-scoped namespace above).
internal static class VisualTreeHelperEx
{
    public static IEnumerable<T> FindVisualChildren<T>(this System.Windows.DependencyObject parent)
        where T : System.Windows.DependencyObject
    {
        if (parent == null) yield break;
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var desc in child.FindVisualChildren<T>())
                yield return desc;
        }
    }
}
