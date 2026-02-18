using System;
using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class GtmReliabilityHardeningTests
    {
        [Fact]
        public void OnboardingFunnel_Should_Retry_Metrics_Write_For_Transient_Failure()
        {
            var gtmConfigService = new StubGtmConfigService();
            var qualification = new OnboardingQualificationService();
            var flakyMetrics = new FlakyOnboardingMetricsService(failAttempts: 2);
            var funnel = new OnboardingFunnelService(gtmConfigService, qualification, flakyMetrics);

            var profile = new OnboardingProfile
            {
                Role = OnboardingRole.Agency,
                DomainFocus = "legal contracts",
                WeeklyLegalVolumeEstimate = 90,
                ConfidentialityRequirementLevel = ConfidentialityRequirementLevel.High,
                IntendsGlossaryUsage = true
            };

            var decision = funnel.Process(profile);

            decision.Should().NotBeNull();
            flakyMetrics.Records.Should().ContainSingle();
            flakyMetrics.Records[0].CorrelationId.Should().NotBeNullOrWhiteSpace();
        }

        private sealed class FlakyOnboardingMetricsService : IOnboardingMetricsService
        {
            private int _remainingFailures;
            public List<OnboardingMetricRecord> Records { get; } = new();

            public FlakyOnboardingMetricsService(int failAttempts)
            {
                _remainingFailures = failAttempts;
            }

            public void Record(OnboardingMetricRecord record)
            {
                if (_remainingFailures > 0)
                {
                    _remainingFailures--;
                    throw new InvalidOperationException("Transient write failure.");
                }

                Records.Add(record);
            }

            public IReadOnlyList<OnboardingMetricRecord> GetRecords(DateTime? fromUtc = null, DateTime? toUtc = null)
            {
                return Records;
            }
        }

        private sealed class StubGtmConfigService : IGtmConfigService
        {
            public GtmConfig LoadConfig() => new() { ActiveLaunchPhase = LaunchPhase.PaidPilot };
            public void SaveConfig(GtmConfig config) { }
            public LaunchPhase GetActiveLaunchPhase() => LaunchPhase.PaidPilot;
            public IReadOnlyList<GtmKpiTarget> GetKpiTargetsByPhase(LaunchPhase phase) => Array.Empty<GtmKpiTarget>();
            public void Dispose() { }
        }
    }
}
