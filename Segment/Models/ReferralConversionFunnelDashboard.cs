namespace Segment.App.Models
{
    public class ReferralConversionFunnelDashboard
    {
        public int RegisteredUsers { get; set; }
        public int GlossaryImportedUsers { get; set; }
        public int TranslationQualifiedUsers { get; set; }
        public int PaidConvertedUsers { get; set; }
        public int RewardGrantedUsers { get; set; }
        public double GlossaryImportRate { get; set; }
        public double TranslationQualifiedRate { get; set; }
        public double PaidConversionRate { get; set; }
        public double RewardGrantRate { get; set; }
    }
}
