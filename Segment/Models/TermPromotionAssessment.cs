namespace Segment.App.Models
{
    public class TermPromotionAssessment
    {
        public bool IsEligible { get; set; }
        public double ConfidenceScore { get; set; }
        public double ReputationScore { get; set; }
        public string Reason { get; set; } = "";
    }
}
