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
    }
}
