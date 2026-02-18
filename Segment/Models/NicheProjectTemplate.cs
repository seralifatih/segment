using System.Collections.Generic;

namespace Segment.App.Models
{
    public class NicheProjectTemplate
    {
        public string TemplateId { get; set; } = "";
        public string Name { get; set; } = "";
        public DomainVertical Domain { get; set; } = DomainVertical.Legal;
        public IReadOnlyList<string> StyleHints { get; set; } = new List<string>();
        public IReadOnlyList<string> EnabledQaChecks { get; set; } = new List<string>();
        public IReadOnlyList<TermEntry> StarterGlossaryTerms { get; set; } = new List<TermEntry>();
    }
}
