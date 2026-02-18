namespace Segment.App.Models
{
    public class TranslationExecutionResult
    {
        public string OutputText { get; set; } = "";
        public bool Success { get; set; }
        public string ProviderUsed { get; set; } = "";
        public bool UsedFallbackProvider { get; set; }
        public bool BudgetEnforced { get; set; }
        public bool BudgetExceeded { get; set; }
        public bool IsShortSegmentMode { get; set; }
        public double ProviderRoundtripMs { get; set; }
    }
}
