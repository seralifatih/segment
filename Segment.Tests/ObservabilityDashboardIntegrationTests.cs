using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class ObservabilityDashboardIntegrationTests : IDisposable
    {
        private readonly string _basePath;
        private readonly OnboardingMetricsService _onboardingMetricsService;
        private readonly ReferralService _referralService;
        private readonly PmfDashboardService _pmfDashboardService;

        public ObservabilityDashboardIntegrationTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentObservabilityDashboardTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _onboardingMetricsService = new OnboardingMetricsService(_basePath);
            _referralService = new ReferralService(_basePath);
            _pmfDashboardService = new PmfDashboardService(_basePath);
        }

        [Fact]
        public void BuildDashboard_Should_Combine_Funnel_Referral_And_Pilot_Views()
        {
            _onboardingMetricsService.Record(new OnboardingMetricRecord { CorrelationId = "o1", Outcome = OnboardingOutcome.Accepted });
            _onboardingMetricsService.Record(new OnboardingMetricRecord { CorrelationId = "o2", Outcome = OnboardingOutcome.Waitlist });
            _onboardingMetricsService.Record(new OnboardingMetricRecord { CorrelationId = "o3", Outcome = OnboardingOutcome.Rejected });

            string referralCode = _referralService.CreateReferralCode("ref-1");
            _referralService.RegisterReferredUser("u-1", referralCode, DateTime.UtcNow.AddDays(-5), "u1@agency.com");
            _referralService.RecordGlossaryImportedMilestone("u-1");
            _referralService.RecordTranslatedSegmentsMilestone("u-1", 50);
            _referralService.RecordPaidConversionMilestone("u-1");

            DateTime now = DateTime.UtcNow;
            _pmfDashboardService.RecordEvent(new PmfUsageEvent
            {
                UserIdHash = "pilot-u1",
                CapturedAtUtc = now.AddDays(-2),
                Segment = CustomerSegment.AgencyLegal,
                SegmentsCompleted = 20,
                GlossarySuggestionsServed = 8,
                GlossarySuggestionsAccepted = 4,
                TerminologyViolationCount = 2,
                LatencyMs = 1100,
                IsPilotUser = true,
                ConvertedToPaid = true
            });

            var dashboardService = new ObservabilityDashboardService(
                _onboardingMetricsService,
                _referralService,
                _pmfDashboardService);

            GrowthObservabilityDashboard dashboard = dashboardService.BuildDashboard(12);

            dashboard.FunnelDropOff.TotalSignups.Should().Be(3);
            dashboard.FunnelDropOff.AcceptedCount.Should().Be(1);
            dashboard.ReferralConversion.RegisteredUsers.Should().Be(1);
            dashboard.ReferralConversion.PaidConvertedUsers.Should().Be(1);
            dashboard.PilotConversion.LatestPilotToPaidConversion.Should().BeGreaterThan(0);
        }

        public void Dispose()
        {
            _onboardingMetricsService.Dispose();
            _referralService.Dispose();
            _pmfDashboardService.Dispose();

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
