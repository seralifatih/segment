using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class OnboardingFunnelIntegrationTests
    {
        [Fact]
        public void PrivateBeta_Should_Route_NonLegal_To_Rejected()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.PrivateBeta);
            var metrics = new InMemoryOnboardingMetricsService();
            var funnel = new OnboardingFunnelService(scope.ConfigService, new OnboardingQualificationService(), metrics);

            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Freelancer,
                DomainFocus = "general localization",
                WeeklyLegalVolumeEstimate = 40,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.Standard,
                IntendsGlossaryUsage = false
            };

            var decision = funnel.Process(profile);

            decision.Outcome.Should().Be(OnboardingOutcome.Rejected);
            metrics.Records.Should().ContainSingle();
            metrics.Records[0].Outcome.Should().Be(OnboardingOutcome.Rejected);
        }

        [Fact]
        public void PrivateBeta_Should_Route_MidFit_Legal_To_Waitlist()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.PrivateBeta);
            var metrics = new InMemoryOnboardingMetricsService();
            var funnel = new OnboardingFunnelService(scope.ConfigService, new OnboardingQualificationService(), metrics);

            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Freelancer,
                DomainFocus = "legal document review",
                WeeklyLegalVolumeEstimate = 20,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.Standard,
                IntendsGlossaryUsage = false
            };

            var decision = funnel.Process(profile);

            decision.Outcome.Should().Be(OnboardingOutcome.Waitlist);
        }

        [Fact]
        public void PaidPilot_Should_Route_HighFit_To_Accepted()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.PaidPilot);
            var metrics = new InMemoryOnboardingMetricsService();
            var funnel = new OnboardingFunnelService(scope.ConfigService, new OnboardingQualificationService(), metrics);

            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Agency,
                DomainFocus = "legal contract translation",
                WeeklyLegalVolumeEstimate = 120,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.High,
                IntendsGlossaryUsage = true
            };

            var decision = funnel.Process(profile);

            decision.Outcome.Should().Be(OnboardingOutcome.Accepted);
        }

        private sealed class InMemoryOnboardingMetricsService : IOnboardingMetricsService
        {
            public List<OnboardingMetricRecord> Records { get; } = new();

            public void Record(OnboardingMetricRecord record)
            {
                Records.Add(record);
            }

            public IReadOnlyList<OnboardingMetricRecord> GetRecords(System.DateTime? fromUtc = null, System.DateTime? toUtc = null)
            {
                var query = Records.AsEnumerable();
                if (fromUtc.HasValue)
                {
                    query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
                }

                if (toUtc.HasValue)
                {
                    query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
                }

                return query.ToList();
            }
        }

        private sealed class GtmScope : System.IDisposable
        {
            private readonly string _basePath;
            public GtmConfigService ConfigService { get; }

            public GtmScope()
            {
                _basePath = Path.Combine(Path.GetTempPath(), "SegmentOnboardingFunnelTests", System.Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_basePath);
                ConfigService = new GtmConfigService(_basePath);
                ConfigService.LoadConfig();
            }

            public void SetPhase(LaunchPhase phase)
            {
                var config = ConfigService.LoadConfig();
                config.ActiveLaunchPhase = phase;
                ConfigService.SaveConfig(config);
            }

            public void Dispose()
            {
                ConfigService.Dispose();
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
}
