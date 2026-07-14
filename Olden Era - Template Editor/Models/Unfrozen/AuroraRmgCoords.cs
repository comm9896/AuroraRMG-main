using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    /// <summary>
    /// Editor-only metadata block that persists each zone's canvas position so a re-import
    /// restores the exact layout (including manual drags) instead of re-deriving it.
    /// Serialized as the first top-level property ("AuroraRMG") for easy textual scanning.
    /// </summary>
    public class AuroraRmgCoords
    {
        // NOTE: the JSON key is "coords" (not "zones") on purpose — "zones" collides with the
        // game's real variants[].zones[] schema and breaks loading the template.
        [JsonPropertyName("coords")]
        public List<ZoneCoord>? Zones { get; set; }
    }

    /// <summary>
    /// A single saved zone position. <see cref="Name"/> matches a zone in the template;
    /// <see cref="X"/>/<see cref="Y"/> are the editor's logical canvas coordinates.
    /// </summary>
    public class ZoneCoord
    {
        // NOTE: the JSON key is "id" (not "name") on purpose — "name" collides with the game's
        // real zone/template name schema and breaks loading the template.
        [JsonPropertyName("id")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
