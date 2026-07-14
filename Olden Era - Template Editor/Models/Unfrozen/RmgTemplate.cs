using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class RmgTemplate
    {
        /// <summary>
        /// Editor-only block serializing each zone's canvas position so a re-import restores
        /// the exact layout. Written as the first property (before "name") for easy scanning.
        /// </summary>
        [JsonPropertyName("AuroraRMG")]
        public AuroraRmgCoords? AuroraRmg { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("gameMode")]
        public string? GameMode { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("displayWinCondition")]
        public string? DisplayWinCondition { get; set; }

        [JsonPropertyName("sizeX")]
        public int SizeX { get; set; }

        [JsonPropertyName("sizeZ")]
        public int SizeZ { get; set; }

        [JsonPropertyName("gameRules")]
        public GameRules? GameRules { get; set; }

        [JsonPropertyName("valueOverrides")]
        public List<ValueOverride>? ValueOverrides { get; set; }

        [JsonPropertyName("globalBans")]
        public GlobalBans? GlobalBans { get; set; }

        [JsonPropertyName("variants")]
        public List<Variant>? Variants { get; set; }

        [JsonPropertyName("zoneLayouts")]
        public List<ZoneLayout>? ZoneLayouts { get; set; }

        [JsonPropertyName("contentPools")]
        public List<object>? ContentPools { get; set; }

        [JsonPropertyName("contentLists")]
        public List<object>? ContentLists { get; set; }

        // These two blocks are written LAST (after contentPools/contentLists) to mirror the
        // game's .rmg.json layout, where mandatoryContent / contentCountLimits close the file.
        [JsonPropertyName("mandatoryContent")]
        public List<MandatoryContentGroup>? MandatoryContent { get; set; }

        [JsonPropertyName("contentCountLimits")]
        public List<ContentCountLimit>? ContentCountLimits { get; set; }
    }
}
