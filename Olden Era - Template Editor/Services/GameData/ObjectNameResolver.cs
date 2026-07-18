using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor.Services.GameData
{
    /// <summary>
    /// Resolves map-object SIDs to their localized display names using the game's
    /// <c>Lang/&lt;lang&gt;/texts/mapObjects.json</c> token file (key <c>&lt;sid&gt;_name</c> → text).
    /// Falls back to the raw SID when the game is not installed or the token is missing.
    /// </summary>
    public sealed class ObjectNameResolver
    {
        public static ObjectNameResolver Instance { get; } = new();

        private readonly object _gate = new();
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);

        private static string LangFolder(AppLanguage lang) =>
            lang == AppLanguage.En ? "english" : "russian";

        /// <summary>Returns the localized display name for an object SID, or the SID itself as fallback.</summary>
        public string Resolve(string sid, AppLanguage? lang = null)
        {
            lang ??= LocalizationManager.Instance.CurrentLanguage;
            var map = GetMap(lang.Value);
            if (map.TryGetValue(sid, out var name) && name.Length > 0)
                return name;
            return sid;
        }

        private Dictionary<string, string> GetMap(AppLanguage lang)
        {
            var folder = LangFolder(lang);
            lock (_gate)
            {
                if (_cache.TryGetValue(folder, out var cached))
                    return cached;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var core = GameCatalogService.Instance.LocateCoreZip();
                if (core is not null && File.Exists(core))
                {
                    using var fs = new FileStream(core, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                    var entry = zip.GetEntry($"Lang/{folder}/texts/mapObjects.json")
                              ?? zip.GetEntry("Lang/english/texts/mapObjects.json");
                    if (entry is not null)
                    {
                        using var doc = JsonDocument.Parse(entry.Open());
                        if (doc.RootElement.TryGetProperty("tokens", out var tokens) &&
                            tokens.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var tok in tokens.EnumerateArray())
                            {
                                if (tok.TryGetProperty("sid", out var sidEl) &&
                                    tok.TryGetProperty("text", out var textEl) &&
                                    sidEl.ValueKind == JsonValueKind.String &&
                                    textEl.ValueKind == JsonValueKind.String)
                                {
                                    var key = sidEl.GetString()!;
                                    if (key.Length > 0 && key.EndsWith("_name", StringComparison.Ordinal))
                                        map[key[..^"_name".Length]] = textEl.GetString()!;
                                }
                            }
                        }
                    }
                }
            }
            catch { /* malformed loc file → empty map → raw SID fallback */ }

            lock (_gate)
            {
                _cache[folder] = map;
            }
            return map;
        }

        /// <summary>Clears the per-language caches (call when the UI language changes).</summary>
        public void Reset()
        {
            lock (_gate)
            {
                _cache.Clear();
            }
        }
    }
}
