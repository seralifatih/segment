using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PmfDashboardIntegrationTests
    {
        [Fact]
        public void Aggregation_Pipeline_Should_Produce_Weekly_Snapshot_And_Export()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentPmfDashboardTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                DateTime end = new DateTime(2026, 01, 15, 0, 0, 0, DateTimeKind.Utc);
                DateTime start = end.AddDays(-7);

                using var dashboardService = new PmfDashboardService(basePath);
                using var configService = new GtmConfigService(basePath);

                dashboardService.RecordEvent(new PmfUsageEvent
                {
                    CapturedAtUtc = end.AddDays(-1),
                    UserIdHash = "u1_hash",
                    Segment = CustomerSegment.FreelancerLegal,
                    SegmentsCompleted = 20,
                    GlossarySuggestionsServed = 6,
                    GlossarySuggestionsAccepted = 4,
                    TerminologyViolationCount = 2,
                    LatencyMs = 1000,
                    IsPilotUser = true
                });
                dashboardService.RecordEvent(new PmfUsageEvent
                {
                    CapturedAtUtc = end.AddDays(-1),
                    UserIdHash = "u2_hash",
                    Segment = CustomerSegment.AgencyLegal,
                    SegmentsCompleted = 25,
                    GlossarySuggestionsServed = 10,
                    GlossarySuggestionsAccepted = 5,
                    TerminologyViolationCount = 3,
                    LatencyMs = 1200,
                    IsPilotUser = true,
                    ConvertedToPaid = true
                });
                dashboardService.RecordEvent(new PmfUsageEvent
                {
                    CapturedAtUtc = end.AddDays(-3),
                    UserIdHash = "u3_hash",
                    Segment = CustomerSegment.EnterpriseLegal,
                    SegmentsCompleted = 25,
                    GlossarySuggestionsServed = 4,
                    GlossarySuggestionsAccepted = 1,
                    TerminologyViolationCount = 2,
                    LatencyMs = 800,
                    Churned = true
                });
                dashboardService.RecordEvent(new PmfUsageEvent
                {
                    CapturedAtUtc = end.AddDays(-40),
                    UserIdHash = "u_old_1",
                    Segment = CustomerSegment.FreelancerLegal,
                    SegmentsCompleted = 5,
                    LatencyMs = 700
                });
                dashboardService.RecordEvent(new PmfUsageEvent
                {
                    CapturedAtUtc = end.AddDays(-12),
                    UserIdHash = "u_old_1",
                    Segment = CustomerSegment.FreelancerLegal,
                    SegmentsCompleted = 5,
                    LatencyMs = 750
                });
                dashboardService.RecordEvent(new PmfUsageEvent
                {
                    CapturedAtUtc = end.AddDays(-40),
                    UserIdHash = "u_old_2",
                    Segment = CustomerSegment.AgencyLegal,
                    SegmentsCompleted = 5,
                    LatencyMs = 900
                });

                var snapshot = dashboardService.GetDashboardSnapshot(start, end);
                snapshot.Dau.Should().Be(2);
                snapshot.Wau.Should().Be(3);
                snapshot.SegmentsPerDay.Should().BeApproximately(10, 0.01);
                snapshot.GlossaryReuseRate.Should().BeApproximately(0.50, 0.0001);
                snapshot.TerminologyViolationRate.Should().BeApproximately(0.10, 0.0001);
                snapshot.P95LatencyMs.Should().BeGreaterThan(1100);
                snapshot.PilotToPaidConversion.Should().BeApproximately(0.5, 0.0001);
                snapshot.ChurnRate.Should().BeApproximately(1.0 / 3.0, 0.0001);
                snapshot.RetentionWeek4.Should().BeApproximately(0.5, 0.0001);

                var evaluator = new DecisionGateEvaluator(configService);
                var decision = evaluator.Evaluate(LaunchPhase.PrivateBeta, snapshot);

                var exporter = new PmfSnapshotExportService();
                string csvPath = Path.Combine(basePath, "pmf-weekly.csv");
                string pdfPath = Path.Combine(basePath, "pmf-weekly.pdf");
                exporter.ExportWeeklyCsv(snapshot, decision, csvPath);
                exporter.ExportWeeklyPdf(snapshot, decision, pdfPath);

                File.Exists(csvPath).Should().BeTrue();
                File.Exists(pdfPath).Should().BeTrue();
                new FileInfo(csvPath).Length.Should().BeGreaterThan(20);
                new FileInfo(pdfPath).Length.Should().BeGreaterThan(100);
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
