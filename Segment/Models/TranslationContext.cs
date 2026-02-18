using System.Collections.Generic;

namespace Segment.App.Models
{
    public class TranslationContext
    {
        public DomainVertical Domain { get; set; } = DomainVertical.Legal;
        public string SourceLanguage { get; set; } = "English";
        public string TargetLanguage { get; set; } = "Turkish";
        public IReadOnlyDictionary<string, string> LockedTerminology { get; set; } = new Dictionary<string, string>();
        public IReadOnlyList<string> ActiveStyleHints { get; set; } = new List<string>();
        public IReadOnlyList<string> EnabledQaChecks { get; set; } = new List<string>();
        public string AccountId { get; set; } = "";
        public bool StrictQaMode { get; set; }
    }
}
