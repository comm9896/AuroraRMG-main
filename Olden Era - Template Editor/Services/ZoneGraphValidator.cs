using System.Collections.Generic;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Pure validation of a zone graph (zones + connections) for the visual editor.
    /// Surfaces the issues that break a template in-game: missing names, duplicate
    /// names, dangling connection endpoints, self-loops and isolated zones.
    /// </summary>
    public static class ZoneGraphValidator
    {
        private static string L(string key, params object[] args) => Localization.LocalizationManager.T(key, args);

        public static List<string> Validate(IReadOnlyList<Zone> zones, IReadOnlyList<Connection> connections)
        {
            var issues = new List<string>();
            var names = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name)) issues.Add(L("S.V.NoName"));
                else if (!names.Add(z.Name)) issues.Add(L("S.V.DupName", z.Name));
            }

            foreach (var c in connections)
            {
                if (!names.Contains(c.From)) issues.Add(L("S.V.Dangling", c.From));
                if (!names.Contains(c.To))   issues.Add(L("S.V.Dangling", c.To));
                if (string.Equals(c.From, c.To, System.StringComparison.Ordinal) && !string.IsNullOrEmpty(c.From))
                    issues.Add(L("S.V.SelfLoop", c.From));
            }

            // Duplicate connection names
            var connNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var c in connections)
            {
                if (!string.IsNullOrEmpty(c.Name) && !connNames.Add(c.Name))
                    issues.Add($"• Duplicate connection name: \"{c.Name}\". Connection names must be unique.");
            }

            if (zones.Count > 1)
            {
                var connected = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var c in connections) { connected.Add(c.From); connected.Add(c.To); }
                foreach (var z in zones)
                    if (!string.IsNullOrWhiteSpace(z.Name) && !connected.Contains(z.Name))
                        issues.Add(L("S.V.Isolated", z.Name));
            }

            // Player spawn checks (Player1..Player8 chain)
            var spawnIssues = ValidateSpawns(zones);
            issues.AddRange(spawnIssues);

            return issues;
        }

        /// <summary>
        /// Validates the player spawn chain across all zones:
        /// 1) at least one MainObject must have a Spawn of Player1..Player8;
        /// 2) the present spawns must form a contiguous chain (Player1, Player2, …)
        ///    without skipping any intermediate player.
        /// Returns an empty list when the template is valid.
        /// </summary>
        public static List<string> ValidateSpawns(IReadOnlyList<Zone> zones)
        {
            var issues = new List<string>();

            var present = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var z in zones)
            {
                if (z?.MainObjects == null) continue;
                foreach (var mo in z.MainObjects)
                    if (!string.IsNullOrEmpty(mo.Spawn) &&
                        System.Array.IndexOf(KnownValues.SpawnPlayers, mo.Spawn) >= 0)
                        present.Add(mo.Spawn);
            }

            if (present.Count == 0)
            {
                issues.Add(L("S.EC.NoSpawn"));
                return issues;
            }

            // Check for gaps in the chain: every player between Player1 and the highest
            // present player must also be present.
            int maxIdx = -1;
            foreach (var p in present)
                maxIdx = System.Math.Max(maxIdx, System.Array.IndexOf(KnownValues.SpawnPlayers, p));
            for (int i = 0; i <= maxIdx; i++)
            {
                var expected = KnownValues.SpawnPlayers[i];
                if (!present.Contains(expected))
                    issues.Add(L("S.EC.SpawnGap", expected));
            }

            return issues;
        }
    }
}
