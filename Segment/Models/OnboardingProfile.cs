namespace Segment.App.Models
{
    public class OnboardingProfile
    {
        public OnboardingRole Role { get; set; } = OnboardingRole.Freelancer;
        public string DomainFocus { get; set; } = "";
        public int WeeklyLegalVolumeEstimate { get; set; }
        public ConfidentialityRequirementLevel ConfidentialityRequirementLevel { get; set; } = ConfidentialityRequirementLevel.Standard;
        public bool IntendsGlossaryUsage { get; set; }
    }
}
