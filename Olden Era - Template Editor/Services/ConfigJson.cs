using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// User-editable config.json next to the executable.
    /// Auto-created with defaults if missing. Lets the user tweak hotkeys and other
    /// preferences without recompiling — just edit config.json in a text editor.
    /// </summary>
    public sealed class ConfigJson
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        [JsonPropertyName("hotkeys")]
        public Dictionary<string, string> Hotkeys { get; set; } = new()
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
        };

        [JsonPropertyName("editor")]
        public EditorConfig Editor { get; set; } = new();

        /// <summary>Path: next to the executable, named config.json.</summary>
        private static string FilePath
        {
            get
            {
                string? dir = Path.GetDirectoryName(
                    Environment.GetCommandLineArgs()[0]);
                return Path.Combine(dir ?? ".", "config.json");
            }
        }

        // ── Singleton-ish ──────────────────────────────────────────────────

        private static ConfigJson? _current;
        public static ConfigJson Current => _current ??= Load();

        public static ConfigJson Load()
        {
            string path = FilePath;
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<ConfigJson>(json, JsonOptions);
                    if (cfg != null) return cfg;
                }
            }
            catch
            {
                // corrupt → fall back to defaults
            }
            // Create with defaults
            var defaults = new ConfigJson();
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
                File.SetAttributes(path, FileAttributes.Hidden);
            }
            catch { /* best effort */ }
            return defaults;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
                if (!File.GetAttributes(FilePath).HasFlag(FileAttributes.Hidden))
                    File.SetAttributes(FilePath, FileAttributes.Hidden);
            }
            catch { /* best effort */ }
        }
    }

    public sealed class EditorConfig
    {
        /// <summary>Default grid snap size in pixels.</summary>
        [JsonPropertyName("gridSize")]
        public double GridSize { get; set; } = 50;

        /// <summary>Default zone radius in pixels.</summary>
        [JsonPropertyName("zoneRadius")]
        public double ZoneRadius { get; set; } = 24;
    }
}