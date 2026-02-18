namespace Segment.App.Models
{
    public class GlossaryQualityReport
    {
        public string WorkspaceId { get; set; } = "";
        public int TotalTerms { get; set; }
        public int ConfirmedTerms { get; set; }
        public int RecentlyUsedTerms { get; set; }
        public double ConfirmationRate { get; set; }
        public double RecentUsageRate { get; set; }
        public double EstimatedViolationRate { get; set; }
    }
}
