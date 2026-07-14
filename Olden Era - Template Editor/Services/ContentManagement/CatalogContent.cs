using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Models.Generated;

namespace OldenEraTemplateEditor.Services.ContentManagement;

/// <summary>
/// Bridges the auto-generated <see cref="GameContentCatalog"/> (real game mandatory-content /
/// content-count-limit pools parsed per template file) into the template generator.
///
/// Pools are kept SEPARATE per template (the same pool name can carry different content
/// in different game templates), so the generator selects ONE base template via
/// <see cref="UseTemplate"/> and every role lookup (<see cref="TryGetMc"/>,
/// <see cref="TryGetCl"/>, <see cref="McName"/>, <see cref="ClName"/>) resolves
/// the pool name <c>mandatory_content_&lt;role&gt;</c> / <c>content_limits_&lt;role&gt;</c>
/// WITHIN that template. The selected template stays in effect for the duration of one
/// <c>TemplateGenerator.Generate</c> call.
/// </summary>
public static class CatalogContent
{
    // Candidate real pool names per role (first present in the selected template wins).
    private static readonly Dictionary<string, string[]> McCandidates = new()
    {
        ["spawn"]    = new[] { "mandatory_content_spawns", "mandatory_content_spawn", "mandatory_content_player_spawn" },
        ["side"]     = new[] { "mandatory_content_side", "mandatory_content_sides" },
        ["sides"]    = new[] { "mandatory_content_sides", "mandatory_content_side" },
        ["treasure"] = new[] { "mandatory_content_treasures", "mandatory_content_treasure", "mandatory_content_supertreasures", "mandatory_content_supertreasure" },
        ["center"]   = new[] { "mandatory_content_center", "mandatory_content_supercenter", "mandatory_content_hub" },
        ["connector"] = new[] { "mandatory_content_connector" },
        ["leaf"]     = new[] { "mandatory_content_leaf" },
        ["trunk"]    = new[] { "mandatory_content_trunk" },
        ["country"]  = new[] { "mandatory_content_country" },
        ["hallway"]  = new[] { "mandatory_content_hallway" },
        ["branch"]   = new[] { "mandatory_content_branch" },
        ["second"]   = new[] { "mandatory_content_second_spawn", "mandatory_content_second_spawn_1" },
        ["ai"]       = new[] { "mandatory_content_ai_spawns" },
    };

    private static readonly Dictionary<string, string> McFallback = new()
    {
        ["spawn"] = "mandatory_content_spawns", ["side"] = "mandatory_content_side", ["sides"] = "mandatory_content_sides",
        ["treasure"] = "mandatory_content_treasure", ["center"] = "mandatory_content_center", ["connector"] = "mandatory_content_connector",
        ["leaf"] = "mandatory_content_leaf", ["trunk"] = "mandatory_content_trunk", ["country"] = "mandatory_content_country",
        ["hallway"] = "mandatory_content_hallway", ["branch"] = "mandatory_content_branch", ["second"] = "mandatory_content_second_spawn",
        ["ai"] = "mandatory_content_ai_spawns",
    };

    private static readonly Dictionary<string, string[]> ClCandidates = new()
    {
        ["spawn"]    = new[] { "content_limits_spawns", "content_limits_spawn" },
        ["side"]     = new[] { "content_limits_side" },
        ["sides"]    = new[] { "content_limits_sides" },
        ["treasure"] = new[] { "content_limits_treasures", "content_limits_treasure", "content_limits_supertreasures", "content_limits_supertreasure" },
        ["center"]   = new[] { "content_limits_center", "content_limits_supercenter" },
        ["connector"] = new[] { "content_limits_connector" },
        ["leaf"]     = new[] { "content_limits_leaf" },
        ["trunk"]    = new[] { "content_limits_trunk" },
        ["country"]  = new[] { "content_limits_country" },
        ["hallway"]  = new[] { "content_limits_hallway" },
        ["branch"]   = new[] { "content_limits_branch" },
        ["second"]   = new[] { "content_limits_second_spawn" },
        ["ai"]       = new[] { "content_limits_ai_spawns" },
        ["red"]      = new[] { "content_limits_red" },
        ["green"]    = new[] { "content_limits_green" },
        ["blue"]     = new[] { "content_limits_blue" },
        ["yellow"]   = new[] { "content_limits_yellow" },
        ["orange"]   = new[] { "content_limits_orange" },
        ["violet"]   = new[] { "content_limits_violet" },
    };

    private static readonly Dictionary<string, string> ClFallback = new()
    {
        ["spawn"] = "content_limits_spawns", ["side"] = "content_limits_side", ["sides"] = "content_limits_sides",
        ["treasure"] = "content_limits_treasure", ["center"] = "content_limits_center", ["connector"] = "content_limits_connector",
        ["leaf"] = "content_limits_leaf", ["trunk"] = "content_limits_trunk", ["country"] = "content_limits_country",
        ["hallway"] = "content_limits_hallway", ["branch"] = "content_limits_branch", ["second"] = "content_limits_second_spawn",
        ["ai"] = "content_limits_ai_spawns", ["red"] = "content_limits_red", ["green"] = "content_limits_green", ["blue"] = "content_limits_blue",
        ["yellow"] = "content_limits_yellow", ["orange"] = "content_limits_orange", ["violet"] = "content_limits_violet",
    };

    /// <summary>
    /// The template whose pools the next <c>TemplateGenerator.Generate</c> call should source
    /// from. Set once at the start of generation via <see cref="UseTemplate"/>. Null until then.
    /// </summary>
    private static string? _templateKey;

    /// <summary>
    /// Selects the base template (by <see cref="GameContentCatalog.TemplateNames"/> key) whose
    /// pools every subsequent role lookup resolves against. Call once per generation.
    /// </summary>
    public static void UseTemplate(string key) => _templateKey = key;

    /// <summary>
    /// Best-effort resolver used by curated entry points (presets / simple mode) that don't
    /// expose an explicit base-template picker: matches <paramref name="preferred"/> against the
    /// catalog's template names (exact, then substring) and falls back to the first template.
    /// Returns null only when the catalog is empty.
    /// </summary>
    public static string? ResolveBaseTemplate(string? preferred)
    {
        var names = GameContentCatalog.TemplateNames;
        if (names.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var exact = names.FirstOrDefault(n => string.Equals(n, preferred, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            var contains = names.FirstOrDefault(n => n.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0);
            if (contains != null) return contains;
        }
        return names[0];
    }

    public static string McName(string role) => FirstMc(role) ?? (McFallback.TryGetValue(role, out var f) ? f : "mandatory_content_" + role);
    public static string ClName(string role) => FirstCl(role) ?? (ClFallback.TryGetValue(role, out var f) ? f : "content_limits_" + role);

    public static bool TryGetMc(string role, out MandatoryContentGroup? group)
    {
        var name = FirstMc(role);
        if (name != null && GameContentCatalog.TryFindMandatoryContent(_templateKey, out var g, name))
        {
            group = Clone(g);
            return true;
        }
        group = null;
        return false;
    }

    public static bool TryGetCl(string role, out ContentCountLimit? group)
    {
        var name = FirstCl(role);
        if (name != null && GameContentCatalog.TryFindContentCountLimits(_templateKey, out var g, name))
        {
            group = Clone(g);
            return true;
        }
        group = null;
        return false;
    }

    private static string? FirstMc(string role)
    {
        if (!McCandidates.TryGetValue(role, out var cands)) return null;
        var list = GameContentCatalog.GetMandatoryContent(_templateKey);
        foreach (var c in cands)
            if (list.Any(g => string.Equals(g.Name, c, StringComparison.Ordinal))) return c;
        return null;
    }

    private static string? FirstCl(string role)
    {
        if (!ClCandidates.TryGetValue(role, out var cands)) return null;
        var list = GameContentCatalog.GetContentCountLimits(_templateKey);
        foreach (var c in cands)
            if (list.Any(g => string.Equals(g.Name, c, StringComparison.Ordinal))) return c;
        return null;
    }

        // Local clone options: Clone round-trips already-built managed objects (lists serialize as
        // arrays and re-deserialize as arrays), so it does not need the game-reader lenient converters.
        private static readonly JsonSerializerOptions CloneOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static T Clone<T>(T obj) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, CloneOptions), CloneOptions)!;
}
