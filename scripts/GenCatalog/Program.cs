using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Parses the Olden Era map_templates folder for its real mandatoryContent / contentCountLimits
// pools and writes Models/Generated/GameContentCatalog.generated.cs. Run automatically by build.bat.
//
// Pools are kept SEPARATE per template file (keyed by file name, no merging by pool name):
// the same pool name can legitimately carry different content in different templates, so the
// generator stores each template's pools intact. The editor then picks ONE base template
// (explicit UI selection) and sources all of its pools from that single file.
//
// Uses System.Text.Json (not PowerShell ConvertTo-Json) so single-element arrays are never
// collapsed into scalars on the way out.
class Program
{
    static readonly HashSet<string> ArrayFields = new() {
        "mandatoryContent", "content", "rules", "args", "includeLists", "contentCountLimits", "limits"
    };

    // String-list fields whose elements may be written as numbers by the game
    // (e.g. rule "args":[0]). The catalog model types these as List<string>, so we
    // coerce numeric elements to their string form on the way in.
    static readonly HashSet<string> StringListFields = new() { "args", "includeLists" };

    static int Main()
    {
        string? td = FindTemplatesDir();

        // templateKey -> { "mandatoryContent": [...], "contentCountLimits": [...] }
        var templates = new SortedDictionary<string, JsonObject>();

        if (td != null)
        {
            foreach (var f in Directory.EnumerateFiles(td, "*.rmg.json"))
            {
                JsonNode? root;
                try { root = JsonNode.Parse(File.ReadAllText(f)); }
                catch { continue; }
                if (root == null) continue;
                var key = Path.GetFileNameWithoutExtension(f);
                if (string.IsNullOrEmpty(key)) continue;

                var mcArr = root["mandatoryContent"] as JsonArray;
                var clArr = root["contentCountLimits"] as JsonArray;

                var entry = new JsonObject();
                entry["mandatoryContent"] = mcArr != null ? Normalize(mcArr) : new JsonArray();
                entry["contentCountLimits"] = clArr != null ? Normalize(clArr) : new JsonArray();
                templates[key] = entry;
            }
        }

        var templatesNode = new JsonObject();
        foreach (var kv in templates)
            templatesNode[kv.Key] = kv.Value;

        var data = new JsonObject
        {
            ["templates"] = templatesNode
        };

        var opts = new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = data.ToJsonString(opts);
        var escaped = json.Replace("\"", "\"\"");

        var repoRoot = FindRepoRoot();
        var outDir = Path.Combine(repoRoot, "Olden Era - Template Editor", "Models", "Generated");
        Directory.CreateDirectory(outDir);
        var outFile = Path.Combine(outDir, "GameContentCatalog.generated.cs");
        var combined = Header + OpenDecl + escaped + CloseDecl + Footer;
        File.WriteAllText(outFile, combined, new UTF8Encoding(false));

        Console.WriteLine($"GenCatalog: wrote {templates.Count} template pools to {outFile}");
        return 0;
    }

    // Recursively normalizes known fields that the game sometimes writes as a bare object/string
    // (e.g. a single "rules" entry as {} instead of [{}], or "args":"0" instead of ["0"])
    // into arrays so the generated C# catalog (typed as List<..>) deserializes cleanly.
    // String-list fields (args/includeLists) also have numeric elements coerced to strings.
    // Always returns a detached (freshly cloned) tree so callers can re-parent freely.
    static JsonNode? Normalize(JsonNode? node)
    {
        if (node is JsonArray arr)
        {
            var na = new JsonArray();
            foreach (var e in arr) na.Add(Normalize(e));
            return na;
        }
        if (node is JsonObject obj)
        {
            var no = new JsonObject();
            foreach (var kv in obj)
            {
                var v = kv.Value;
                if (v == null) { no[kv.Key] = null; continue; }
                if (ArrayFields.Contains(kv.Key) && !(v is JsonArray))
                    no[kv.Key] = NormalizeToArray(kv.Key, v);
                else if (StringListFields.Contains(kv.Key) && v is JsonArray ja)
                    no[kv.Key] = CoerceStringArray(ja);
                else
                    no[kv.Key] = Normalize(v);
            }
            return no;
        }
        return node?.DeepClone();
    }

    static JsonArray NormalizeToArray(string key, JsonNode v)
    {
        var wrapped = new JsonArray(Normalize(v));
        return StringListFields.Contains(key) ? CoerceStringArray(wrapped) : wrapped;
    }

    // Returns a JsonArray where every element is a JSON string token: numeric elements
    // (e.g. rule args [0]) are converted to their textual form, strings are kept as-is.
    static JsonArray CoerceStringArray(JsonArray arr)
    {
        var na = new JsonArray();
        foreach (var e in arr)
        {
            if (e is JsonValue jv)
            {
                if (jv.TryGetValue(out int i)) na.Add(JsonValue.Create(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                else if (jv.TryGetValue(out double d)) na.Add(JsonValue.Create(d.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                else if (jv.TryGetValue(out string s)) na.Add(JsonValue.Create(s));
                else na.Add(e.DeepClone());
            }
            else na.Add(e.DeepClone());
        }
        return na;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null &&
               !Directory.Exists(Path.Combine(dir.FullName, "Olden Era - Template Editor")) &&
               !File.Exists(Path.Combine(dir.FullName, "build.bat")))
        {
            dir = dir.Parent;
        }
        return (dir ?? new DirectoryInfo(Directory.GetCurrentDirectory())).FullName;
    }

    static string? FindTemplatesDir()
    {
        var candidates = new List<string>();
        foreach (var steam in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        })
        {
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(vdf))
                    {
                        var m = Regex.Match(line, "\"(\\d+)\"\\s+\"([^\"]+)\"");
                        if (m.Success)
                        {
                            var lib = m.Groups[2].Value;
                            var td = Path.Combine(lib, "steamapps", "common",
                                "Heroes of Might and Magic Olden Era", "HeroesOldenEra_Data",
                                "StreamingAssets", "map_templates");
                            if (Directory.Exists(td)) return td;
                        }
                    }
                }
                catch { }
            }
        }

        candidates.Add(@"E:\SteamLibrary\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates");
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era", "HeroesOldenEra_Data", "StreamingAssets", "map_templates"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era", "HeroesOldenEra_Data", "StreamingAssets", "map_templates"));

        foreach (var c in candidates) if (Directory.Exists(c)) return c;
        return null;
    }

    const string Header = @"// <auto-generated>
// Generated by scripts/GenCatalog - DO NOT EDIT.
// Run automatically by build.bat before the build.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Models.Generated;

/// <summary>
/// Auto-generated catalog of real game mandatoryContent / contentCountLimits pools parsed from the
/// Olden Era map_templates folder. Pools are kept SEPARATE per template file (keyed by the
/// template's file name without extension) because the same pool name carries different content
/// in different templates. Pick one template via <see cref=""TemplateNames""/> and read its pools
/// with <see cref=""GetMandatoryContent""/> / <see cref=""GetContentCountLimits""/>.
/// </summary>
public static class GameContentCatalog
{
";

    const string OpenDecl = "    private const string DataJson = @\"";
    const string CloseDecl = "\";";

    const string Footer = @"
    public sealed class TemplateEntry
    {
        public List<MandatoryContentGroup>? mandatoryContent { get; set; }
        public List<ContentCountLimit>? contentCountLimits { get; set; }
    }

    private sealed class CatalogDoc
    {
        public Dictionary<string, TemplateEntry>? templates { get; set; }
    }

    private static readonly CatalogDoc _doc = JsonSerializer.Deserialize<CatalogDoc>(DataJson) ?? new CatalogDoc();
    private static readonly Dictionary<string, TemplateEntry> _templates =
        _doc.templates ?? new Dictionary<string, TemplateEntry>();

    public static IReadOnlyList<string> TemplateNames =>
        _templates.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyDictionary<string, TemplateEntry> Templates => _templates;

    public static string TemplateFile(string key) => key + "".rmg.json"";

    public static IReadOnlyList<MandatoryContentGroup> GetMandatoryContent(string? key) =>
        key != null && _templates.TryGetValue(key, out var t) && t.mandatoryContent != null
            ? t.mandatoryContent : Array.Empty<MandatoryContentGroup>();

    public static IReadOnlyList<ContentCountLimit> GetContentCountLimits(string? key) =>
        key != null && _templates.TryGetValue(key, out var t) && t.contentCountLimits != null
            ? t.contentCountLimits : Array.Empty<ContentCountLimit>();

    public static bool TryFindMandatoryContent(string? key, out MandatoryContentGroup? group, params string[] candidateNames)
    {
        var list = GetMandatoryContent(key);
        foreach (var n in candidateNames)
        {
            var found = list.FirstOrDefault(g => string.Equals(g.Name, n, StringComparison.Ordinal));
            if (found != null) { group = found; return true; }
        }
        group = null;
        return false;
    }

    public static bool TryFindContentCountLimits(string? key, out ContentCountLimit? group, params string[] candidateNames)
    {
        var list = GetContentCountLimits(key);
        foreach (var n in candidateNames)
        {
            var found = list.FirstOrDefault(g => string.Equals(g.Name, n, StringComparison.Ordinal));
            if (found != null) { group = found; return true; }
        }
        group = null;
        return false;
    }
}
";
}
