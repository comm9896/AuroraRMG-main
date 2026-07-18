using System;

namespace OldenEraTemplateEditor.Models
{
    public class ConnectionInfo
    {
        public int Number { get; set; }
        public Connection? Connection { get; set; }
        public string? FromZoneName { get; set; }
        public string? ToZoneName { get; set; }
        public string? DisplayText { get; set; }
        public bool IsModified { get; set; }
        public string? TypeName { get; set; }
        public string? GuardValue { get; set; }
        public string? RoadFlag { get; set; }
    }
}
