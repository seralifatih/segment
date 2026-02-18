using System.Collections.Generic;

namespace Segment.App.Models
{
    public class InteropTermTransferOptions
    {
        public string SourceLanguage { get; set; } = "English";
        public string TargetLanguage { get; set; } = "Turkish";
        public string ContextTag { get; set; } = "interoperability";
        public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
