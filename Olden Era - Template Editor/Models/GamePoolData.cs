using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class PoolGroup
    {
        public int Weight { get; set; }
        public List<string> IncludeLists { get; set; } = new();
        public List<PoolContentItem>? Content { get; set; }
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
        public List<PoolGroup> Groups { get; set; } = new();
        public List<PoolBanEntry>? Bans { get; set; }
        public override string ToString() => Name;
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

            if (foundPath == null)
            {
                _status = $"⚠ Файл не найден. Искали: {string.Join(", ", paths)}";
                _dataPath = paths[0];
                return;
            }

            _dataPath = foundPath;

            try
            {
                var json = File.ReadAllText(foundPath);
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
                var options = new JsonSerializerOptions { WriteIndented = true };
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
    }
}
