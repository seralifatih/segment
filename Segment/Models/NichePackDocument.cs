using System.Collections.Generic;

namespace Segment.App.Models
{
    public class NichePackDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public NichePackMetadata Metadata { get; set; } = new();
        public DomainVertical Domain { get; set; } = DomainVertical.Legal;
        public string SourceLanguage { get; set; } = "English";
        public string TargetLanguage { get; set; } = "";
        public IReadOnlyList<string> StyleHints { get; set; } = new List<string>();
        public IReadOnlyList<string> EnabledQaChecks { get; set; } = new List<string>();
        public IReadOnlyList<TermEntry> GlossaryTerms { get; set; } = new List<TermEntry>();
    }
}
