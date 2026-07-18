using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEraTemplateEditor.Models.Generated;

namespace OldenEraTemplateEditor.Models
{
    public class PoolGroup
    {
        public int Weight { get; set; }
        public List<string> IncludeLists { get; set; } = new();
        public List<PoolContentItem>? Content { get; set; }
    }

    public class ValueDistribution
    {
        public List<int> PriceBounds { get; set; } = new();
        public List<int> Weights { get; set; } = new();
    }

    public class PoolContentItem
    {
        public string Sid { get; set; } = "";
        public int Weight { get; set; }
        public string? Biome { get; set; }
    }

    public class PoolBanEntry
    {
        public string Sid { get; set; } = "";
    }

    public class GamePool
    {
        public string Name { get; set; } = "";
        public ValueDistribution? ValueDistribution { get; set; }
        public List<PoolGroup> Groups { get; set; } = new();
        public List<PoolBanEntry>? Bans { get; set; }
        /// <summary>Optional viewer tag used to group synthetic/template pools into viewer categories
        /// (e.g. "mandatory", "content_limits"). Real GameData pools leave this null.</summary>
        public string? Tag { get; set; }
        /// <summary>When non-null, the game template (catalog key) this pool was parsed from.
        /// Used to group pools under the "Specific-template" viewer category.</summary>
        public string? SourceTemplate { get; set; }
        /// <summary>Display string: pool name plus, for template-sourced pools, the originating
        /// template (UI only — stored/serialized values remain the bare <see cref="Name"/>).</summary>
        public override string ToString() => SourceTemplate != null ? $"{Name}  ·  {SourceTemplate}" : Name;
    }

    public class ContentListEntry
    {
        public string Name { get; set; } = "";
        public List<PoolContentItem> Content { get; set; } = new();
        public override string ToString() => Name;
    }

    public class GameDataRoot
    {
        public List<GamePool>? Pools { get; set; }
        public List<ContentListEntry>? Lists { get; set; }
    }

    public class GamePoolDataLoader
    {
        private static List<GamePool>? _allPools;
        private static List<ContentListEntry>? _allContentLists;
        private static bool _loaded;
        private static string _status = "";
        private static string _dataPath = "";
        private static string _customPoolsPath = "";
        private static List<GamePool> _customPools = new();

        public static string Status => _status;
        public static string DataPath => _dataPath;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _allContentLists = new List<ContentListEntry>();
            _allPools = new List<GamePool>();

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var curDir = Directory.GetCurrentDirectory();

            string[] paths = new[]
            {
                Path.Combine(baseDir, "GameData.json"),
                Path.Combine(curDir, "GameData.json"),
                Path.Combine(baseDir, "Resources", "GameData.json"),
                Path.Combine(curDir, "Resources", "GameData.json"),
            };

            string? foundPath = null;
            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    foundPath = p;
                    break;
                }
            }

            string? json = null;
            if (foundPath != null)
            {
                _dataPath = foundPath;
                json = File.ReadAllText(foundPath);
            }
            else
            {
                // Single-file publish does not extract GameData.json to disk — fall back to the
                // embedded resource so the data is always available inside the .exe bundle.
                var asm = typeof(GamePoolDataLoader).Assembly;
                var resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("GameData.json", StringComparison.OrdinalIgnoreCase));
                if (resName != null)
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        json = reader.ReadToEnd();
                        _dataPath = $"<embedded: {resName}>";
                    }
                }
            }

            if (json == null)
            {
                _status = $"⚠ Файл не найден. Искали: {string.Join(", ", paths)}";
                _dataPath = paths[0];
                return;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                var data = JsonSerializer.Deserialize<GameDataRoot>(json, options);
                if (data != null)
                {
                    _allPools = data.Pools ?? new List<GamePool>();
                    _allContentLists = data.Lists ?? new List<ContentListEntry>();
                }
                else
                {
                    _allPools = new List<GamePool>();
                    _allContentLists = new List<ContentListEntry>();
                }
            }
            catch (Exception ex)
            {
                _status = $"⚠ Ошибка: {ex.Message}";
                return;
            }

            // Load custom pools
            LoadCustomPools();

            _status = $"✓ Загружено: {_allPools.Count} пулов ({_customPools.Count} своих), {_allContentLists.Count} листов";
        }

        public static void AddPool(GamePool pool)
        {
            Load();
            _allPools ??= new List<GamePool>();
            _allPools.Add(pool);
            _customPools.Add(pool);
            SaveCustomPools();
        }

        private static void LoadCustomPools()
        {
            _customPools = new List<GamePool>();
            _customPoolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomPools.json");

            if (!File.Exists(_customPoolsPath))
                return;

            try
            {
                var json = File.ReadAllText(_customPoolsPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var pools = JsonSerializer.Deserialize<List<GamePool>>(json, options);
                if (pools != null)
                {
                    _customPools = pools;
                    _allPools.AddRange(_customPools);
                }
            }
            catch { }
        }

        private static void SaveCustomPools()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // Match the real GameData pool schema (camelCase: name, groups,
                    // includeLists, valueDistribution, priceBounds, weights).
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                var json = JsonSerializer.Serialize(_customPools, options);
                File.WriteAllText(_customPoolsPath, json);
            }
            catch { }
        }

        public static List<GamePool> GetAllPools()
        {
            Load();
            return _allPools ?? new List<GamePool>();
        }

        /// <summary>
        /// Returns the full pool universe: real GameData pools (incl. created <c>custom_</c> pools)
        /// plus every <c>mandatoryContent</c> / <c>contentCountLimits</c> pool parsed from ALL game
        /// templates in the catalog. Each template-sourced pool has <see cref="GamePool.SourceTemplate"/>
        /// set to its catalog key, and its flattened content rows are returned in <paramref name="templateItems"/>
        /// keyed by the same <see cref="GamePool"/> instance so the viewer can display inner objects.
        /// </summary>
        public static List<GamePool> GetAllPoolsWithTemplates(out Dictionary<GamePool, List<PoolItemInfo>> templateItems)
        {
            Load();
            var result = new List<GamePool>(_allPools ?? new List<GamePool>());
            templateItems = new Dictionary<GamePool, List<PoolItemInfo>>();

            foreach (var key in GameContentCatalog.TemplateNames)
            {
                try
                {
                    var (pools, items) = TemplatePoolBuilder.Build(
                        GameContentCatalog.GetMandatoryContent(key).ToList(),
                        GameContentCatalog.GetContentCountLimits(key).ToList());
                    foreach (var p in pools)
                    {
                        p.SourceTemplate = key;
                        templateItems[p] = items[p];
                        result.Add(p);
                    }
                }
                catch { /* skip a malformed template entry */ }
            }

            return result;
        }

        public static List<ContentListEntry> GetAllContentLists()
        {
            Load();
            return _allContentLists ?? new List<ContentListEntry>();
        }

        public static ContentListEntry? GetContentList(string name)
        {
            Load();
            return _allContentLists?.FirstOrDefault(l => l.Name == name);
        }

        public static List<PoolItemInfo> GetPoolItems(GamePool pool)
        {
            var result = new List<PoolItemInfo>();

            if (pool.Groups == null)
                return result;

            foreach (var group in pool.Groups)
            {
                foreach (var listName in group.IncludeLists)
                {
                    var list = GetContentList(listName);
                    if (list != null)
                    {
                        foreach (var item in list.Content)
                        {
                            result.Add(new PoolItemInfo
                            {
                                ListName = listName,
                                Sid = item.Sid,
                                Weight = item.Weight,
                                Biome = item.Biome,
                                GroupWeight = group.Weight
                            });
                        }
                    }
                    else
                    {
                        result.Add(new PoolItemInfo
                        {
                            ListName = listName,
                            Sid = "(список не найден)",
                            Weight = group.Weight,
                            Biome = null,
                            GroupWeight = group.Weight,
                            IsMissing = true
                        });
                    }
                }

                if (group.Content != null)
                {
                    foreach (var item in group.Content)
                    {
                        result.Add(new PoolItemInfo
                        {
                            ListName = $"Группа (вес: {group.Weight})",
                            Sid = item.Sid,
                            Weight = item.Weight,
                            Biome = item.Biome,
                            GroupWeight = group.Weight
                        });
                    }
                }
            }

            return result;
        }
    }

    public class PoolItemInfo
    {
        public string ListName { get; set; } = "";
        public string Sid { get; set; } = "";
        public int Weight { get; set; }
        public string? Biome { get; set; }
        public int GroupWeight { get; set; }
        public bool IsMissing { get; set; }
        /// <summary>Free-form settings text (e.g. isMine/isGuarded/rules or maxCount) for template pools.</summary>
        public string? Detail { get; set; }
        /// <summary>The <c>variant</c> field of a mandatory-content item or content-limit item.</summary>
        public int? Variant { get; set; }
        /// <summary>The <c>maxCount</c> of a content-limit item (0 means absent/none on display).</summary>
        public int MaxCount { get; set; }
    }

    /// <summary>
    /// Builds synthetic <see cref="GamePool"/> entries (and their display rows) for the
    /// template-level <c>mandatoryContent</c> / <c>contentCountLimits</c> blocks so they can be
    /// shown, view-only, inside the pool-contents viewer under tagged categories.
    /// </summary>
    public static class TemplatePoolBuilder
    {
        public static (List<GamePool> Pools, Dictionary<GamePool, List<PoolItemInfo>> Items) Build(
            List<MandatoryContentGroup>? mandatory,
            List<ContentCountLimit>? limits)
        {
            var pools = new List<GamePool>();
            var items = new Dictionary<GamePool, List<PoolItemInfo>>();

            if (mandatory != null)
            {
                foreach (var g in mandatory)
                {
                    var pool = new GamePool { Name = g.Name, Tag = "mandatory" };
                    var rows = new List<PoolItemInfo>();
                    if (g.Content != null)
                    {
                        foreach (var ci in g.Content)
                        {
                            var sb = new StringBuilder();
                            if (ci.IsMine == true) sb.Append("isMine; ");
                            if (ci.IsGuarded == true) sb.Append("isGuarded; ");
                            if (ci.Rules != null)
                            {
                                foreach (var r in ci.Rules)
                                {
                                    var args = r.Args != null ? string.Join(",", r.Args) : "";
                                    sb.Append($"{r.Type}[{args}] {r.TargetMin}-{r.TargetMax} w{r.Weight}; ");
                                }
                            }
                            var detail = sb.Length > 2 ? sb.ToString(0, sb.Length - 2) : "";
                            rows.Add(new PoolItemInfo
                            {
                                ListName = "обязательный контент",
                                Sid = ci.Sid
                                      ?? (ci.IncludeLists != null && ci.IncludeLists.Count > 0
                                          ? string.Join(", ", ci.IncludeLists)
                                          : ""),
                                Variant = ci.Variant,
                                Detail = detail,
                            });
                        }
                    }
                    pools.Add(pool);
                    items[pool] = rows;
                }
            }

            if (limits != null)
            {
                foreach (var l in limits)
                {
                    var pool = new GamePool { Name = l.Name, Tag = "content_limits" };
                    var rows = new List<PoolItemInfo>();
                    if (l.Limits != null)
                    {
                        foreach (var sl in l.Limits)
                        {
                            var sid = sl.Sid;
                            if (string.IsNullOrEmpty(sid) && sl.IncludeLists != null && sl.IncludeLists.Count > 0)
                                sid = string.Join(", ", sl.IncludeLists);
                            else if (string.IsNullOrEmpty(sid) && sl.Content != null && sl.Content.Count > 0)
                                sid = sl.Content[0].Sid ?? "";

                            rows.Add(new PoolItemInfo
                            {
                                ListName = "лимиты количества",
                                Sid = sid ?? "",
                                Variant = sl.Variant,
                                MaxCount = sl.MaxCount,
                                Weight = sl.MaxCount,
                                // UI-only display: show just the max-count value (the " * " placeholder
                                // stood for this number). Real MaxCount is serialized from ContentCountLimit.Limits.
                                // Absent / zero maxCount is shown as an empty cell.
                                Detail = sl.MaxCount == 0 ? "" : sl.MaxCount.ToString(),
                            });
                        }
                    }
                    pools.Add(pool);
                    items[pool] = rows;
                }
            }

            return (pools, items);
        }
    }
}
