using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class OnboardingQualificationServiceTests
    {
        private readonly OnboardingQualificationService _service = new();

        [Fact]
        public void EarlyPhase_Should_Reject_NonLegal_Domain()
        {
            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Freelancer,
                DomainFocus = "general marketing localization",
                WeeklyLegalVolumeEstimate = 80,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.Standard,
                IntendsGlossaryUsage = true
            };

            var decision = _service.Evaluate(profile, LaunchPhase.PrivateBeta);

            decision.Outcome.Should().Be(OnboardingOutcome.Rejected);
            decision.Explanation.Should().Contain("legal-focused");
        }

        [Fact]
        public void Should_Reject_When_Weekly_Volume_Below_Threshold()
        {
            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Agency,
                DomainFocus = "legal contracts and compliance",
                WeeklyLegalVolumeEstimate = 5,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.High,
                IntendsGlossaryUsage = true
            };

            var decision = _service.Evaluate(profile, LaunchPhase.PaidPilot);

            decision.Outcome.Should().Be(OnboardingOutcome.Rejected);
            decision.Explanation.Should().Contain("Minimum weekly legal workload");
        }

        [Fact]
        public void Should_Accept_HighIntent_Legal_Profile()
        {
            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Enterprise,
                DomainFocus = "legal due diligence and M&A support",
                WeeklyLegalVolumeEstimate = 150,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.Strict,
                IntendsGlossaryUsage = true
            };

            var decision = _service.Evaluate(profile, LaunchPhase.PaidPilot);

            decision.Outcome.Should().Be(OnboardingOutcome.Accepted);
            decision.Score.Should().BeGreaterThanOrEqualTo(75);
        }
    }
}
