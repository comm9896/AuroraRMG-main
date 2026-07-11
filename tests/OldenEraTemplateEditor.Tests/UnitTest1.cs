using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Localization;

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
