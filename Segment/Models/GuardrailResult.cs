namespace Segment.App.Models
{
    public class GuardrailResult
    {
        public GuardrailSeverity Severity { get; set; } = GuardrailSeverity.Info;
        public string RuleId { get; set; } = "";
        public string Message { get; set; } = "";
        public string SuggestedFix { get; set; } = "";
        public bool IsBlocking { get; set; }
        public int SeverityScore { get; set; }
    }
}
