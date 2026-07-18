using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class ValueOverride
    {
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = string.Empty;

        [JsonPropertyName("variant")]
        public int? Variant { get; set; }

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }

        // NOTE: "value" (object-value redistribution) has no equivalent in the game's
        // valueOverrides schema, so it is intentionally NOT serialized. The field exists
        // only for the (currently inactive) UI; it is never written to .rmg.json.
        public int? Value { get; set; }
    }

    public class GlobalBans
    {
        [JsonPropertyName("items")]
        public List<string>? Items { get; set; }

        [JsonPropertyName("magics")]
        public List<string>? Magics { get; set; }

        [JsonPropertyName("heroes")]
        public List<string>? Heroes { get; set; }
    }
}
