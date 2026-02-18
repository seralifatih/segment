using System.Collections.Generic;

namespace Segment.App.Models
{
    public class DomainRulePack
    {
        public bool RequireTerminologyChecks { get; set; } = true;
        public bool RequireNumericChecks { get; set; } = true;
        public bool RequireDateChecks { get; set; } = true;
        public IReadOnlyList<string> DisallowedPhrases { get; set; } = new List<string>();
        public DomainRuleSeverity WarningSeverity { get; set; } = DomainRuleSeverity.Medium;
        public DomainRuleSeverity ErrorSeverity { get; set; } = DomainRuleSeverity.High;
    }
}
