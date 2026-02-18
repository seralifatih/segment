using System.Collections.Generic;

namespace Segment.App.Models
{
    public class RedactionResult
    {
        public string RedactedText { get; set; } = "";
        public IReadOnlyDictionary<string, string> TokenToOriginalMap { get; set; } = new Dictionary<string, string>();
    }
}
