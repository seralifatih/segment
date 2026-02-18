using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ReferralAndKpiReliabilityTests
    {
        [Fact]
        public void ReferralMilestone_Should_Require_Registered_User()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentReferralReliabilityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                using var service = new ReferralService(basePath);
                Action act = () => service.RecordPaidConversionMilestone("unknown-user");
                act.Should().Throw<InvalidOperationException>();
            }
            finally
            {
                try
                {
                    if (Directory.Exists(basePath))
                    {
                        Directory.Delete(basePath, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        [Fact]
        public void KpiIngestion_Should_Be_Idempotent_For_Duplicate_Events()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentKpiIngestionReliabilityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                using var service = new PmfDashboardService(basePath);
                DateTime now = DateTime.UtcNow;
                var evt = new PmfUsageEvent
                {
                    IngestionKey = "dedupe-1",
                    CapturedAtUtc = now,
                    UserIdHash = "u-1",
                    Segment = CustomerSegment.FreelancerLegal,
                    SegmentsCompleted = 12,
                    GlossarySuggestionsServed = 4,
                    GlossarySuggestionsAccepted = 2,
                    TerminologyViolationCount = 1,
                    LatencyMs = 900,
                    IsPilotUser = true,
                    ConvertedToPaid = false
                };

                service.RecordEvent(evt);
                service.RecordEvent(evt);

                var snapshot = service.GetDashboardSnapshot(now.AddDays(-1), now.AddDays(1));
                snapshot.Wau.Should().Be(1);
                snapshot.SegmentsPerDay.Should().BeApproximately(6.0, 0.01);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(basePath))
                    {
                        Directory.Delete(basePath, recursive: true);
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
