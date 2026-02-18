using System;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ReferralServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly ReferralService _service;

        public ReferralServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentReferralTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _service = new ReferralService(_basePath);
        }

        [Fact]
        public void GrantRewardIfEligible_Should_Grant_When_All_Milestones_Met()
        {
            string code = _service.CreateReferralCode("referrer-1");
            DateTime signupAt = DateTime.UtcNow.AddDays(-5);
            _service.RegisterReferredUser("referred-1", code, signupAt, "freelancer@agency.com");

            _service.RecordGlossaryImportedMilestone("referred-1", DateTime.UtcNow.AddDays(-4));
            _service.RecordTranslatedSegmentsMilestone("referred-1", additionalSegments: 120, DateTime.UtcNow.AddDays(-2));
            _service.RecordPaidConversionMilestone("referred-1", DateTime.UtcNow.AddDays(-1));

            var result = _service.GrantRewardIfEligible("referred-1", requiredTranslatedSegments: 100, conversionWindowDays: 14);

            result.Eligible.Should().BeTrue();
            result.RewardGranted.Should().BeTrue();
            result.AlreadyRewarded.Should().BeFalse();
            result.ReferrerUserId.Should().Be("referrer-1");
        }

        [Fact]
        public void GrantRewardIfEligible_Should_Reject_When_Conversion_Outside_Window()
        {
            string code = _service.CreateReferralCode("referrer-2");
            DateTime signupAt = DateTime.UtcNow.AddDays(-30);
            _service.RegisterReferredUser("referred-2", code, signupAt, "freelancer@agency.com");

            _service.RecordGlossaryImportedMilestone("referred-2", DateTime.UtcNow.AddDays(-29));
            _service.RecordTranslatedSegmentsMilestone("referred-2", additionalSegments: 150, DateTime.UtcNow.AddDays(-20));
            _service.RecordPaidConversionMilestone("referred-2", DateTime.UtcNow.AddDays(-1));

            var result = _service.GrantRewardIfEligible("referred-2", requiredTranslatedSegments: 100, conversionWindowDays: 14);

            result.Eligible.Should().BeFalse();
            result.RewardGranted.Should().BeFalse();
            result.Reason.Should().Contain("milestones");
        }

        [Fact]
        public void EvaluateAgencyExpansion_Should_Trigger_For_Multiple_Freelancers_In_Same_Domain()
        {
            _service.TrackFreelancerDomainActivation("user-1", "a@agencylegal.com");
            _service.TrackFreelancerDomainActivation("user-2", "b@agencylegal.com");
            _service.TrackFreelancerDomainActivation("user-3", "c@agencylegal.com");

            var trigger = _service.EvaluateAgencyExpansion("agencylegal.com", freelancerThreshold: 3);

            trigger.Triggered.Should().BeTrue();
            trigger.UniqueFreelancerCount.Should().Be(3);
            trigger.Message.Should().Contain("Prompt Legal Team upgrade");
        }

        public void Dispose()
        {
            _service.Dispose();
            try
            {
                if (Directory.Exists(_basePath))
                {
                    Directory.Delete(_basePath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
