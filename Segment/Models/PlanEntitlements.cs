namespace Segment.App.Models
{
    public class PlanEntitlements
    {
        public string GuardrailsLevel { get; set; } = "Standard";
        public string ConfidentialityModes { get; set; } = "Standard";
        public bool AdvancedGuardrails { get; set; }
        public bool SharedGlossary { get; set; }
        public bool AuditExport { get; set; }
        public bool Analytics { get; set; }
        public bool TeamAnalytics { get; set; }
        public SlaTier SlaTier { get; set; } = SlaTier.Standard;
    }
}
