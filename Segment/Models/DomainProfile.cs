using System.Collections.Generic;

namespace Segment.App.Models
{
    public class DomainProfile
    {
        public DomainVertical Id { get; set; } = DomainVertical.Legal;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DomainRiskLevel RiskLevel { get; set; } = DomainRiskLevel.Medium;
        public IReadOnlyList<string> DefaultChecks { get; set; } = new List<string>();
        public IReadOnlyList<string> DefaultStyleHints { get; set; } = new List<string>();
        public string RecommendedProviderPolicy { get; set; } = "";
    }
}
