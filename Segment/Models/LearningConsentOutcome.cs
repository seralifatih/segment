namespace Segment.App.Models
{
    public class LearningConsentOutcome
    {
        public bool Saved { get; set; }
        public bool IsGlobalScope { get; set; }
        public bool Skipped { get; set; }
        public bool RequiresConflictResolution { get; set; }
        public bool ConflictResolvedWithOverwrite { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
