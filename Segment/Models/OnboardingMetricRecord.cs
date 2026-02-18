using System;
using LiteDB;

namespace Segment.App.Models
{
    public class OnboardingMetricRecord
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string CorrelationId { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public LaunchPhase LaunchPhase { get; set; } = LaunchPhase.Phase0Readiness;
        public OnboardingRole Role { get; set; } = OnboardingRole.Freelancer;
        public string DomainFocus { get; set; } = "";
        public bool DomainIncludesLegal { get; set; }
        public int WeeklyLegalVolumeEstimate { get; set; }
        public ConfidentialityRequirementLevel ConfidentialityRequirementLevel { get; set; } = ConfidentialityRequirementLevel.Standard;
        public bool IntendsGlossaryUsage { get; set; }
        public int EligibilityScore { get; set; }
        public OnboardingOutcome Outcome { get; set; } = OnboardingOutcome.Waitlist;
    }
}
