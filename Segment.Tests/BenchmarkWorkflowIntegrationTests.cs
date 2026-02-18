using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class BenchmarkWorkflowIntegrationTests
    {
        [Fact]
        public void Benchmark_Lifecycle_Should_Complete_And_Export_Report()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentBenchmarkTests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                using var workflow = new BenchmarkWorkflowService(basePath: basePath);
                var session = workflow.StartSession("Pilot-A");

                workflow.CaptureWeek(session.Id, 1, BenchmarkPeriodType.Baseline, CreateMetrics(avgMinutes: 11, violations: 24, samples: 80));
                workflow.CaptureWeek(session.Id, 2, BenchmarkPeriodType.SegmentAssisted, CreateMetrics(avgMinutes: 8.5, violations: 16, samples: 90));
                workflow.CaptureWeek(session.Id, 3, BenchmarkPeriodType.SegmentAssisted, CreateMetrics(avgMinutes: 7.8, violations: 12, samples: 95));
                var completed = workflow.CaptureWeek(session.Id, 4, BenchmarkPeriodType.SegmentAssisted, CreateMetrics(avgMinutes: 7.1, violations: 10, samples: 100));

                completed.IsCompleted.Should().BeTrue();
                completed.FinalReport.Should().NotBeNull();
                completed.FinalReport!.TimeSavedPercentage.Should().BeGreaterThan(0);
                completed.FinalReport!.ViolationReductionPercentage.Should().BeGreaterThan(0);

                var exporter = new PilotRoiReportExportService();
                string csvPath = Path.Combine(basePath, "pilot-roi.csv");
                string pdfPath = Path.Combine(basePath, "pilot-roi.pdf");
                exporter.ExportCsv(completed.FinalReport!, csvPath);
                exporter.ExportPdf(completed.FinalReport!, pdfPath);

                File.Exists(csvPath).Should().BeTrue();
                File.Exists(pdfPath).Should().BeTrue();
                new FileInfo(csvPath).Length.Should().BeGreaterThan(10);
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

        [Fact]
        public void Benchmark_Lifecycle_Should_Reject_Invalid_Week_Rule()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentBenchmarkTests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                using var workflow = new BenchmarkWorkflowService(basePath: basePath);
                var session = workflow.StartSession("Pilot-B");

                System.Action action = () =>
                    workflow.CaptureWeek(session.Id, 1, BenchmarkPeriodType.SegmentAssisted, CreateMetrics(avgMinutes: 9, violations: 10, samples: 60));

                action.Should().Throw<System.InvalidOperationException>()
                    .WithMessage("*Week 1 must be captured as baseline*");
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

        private static IReadOnlyList<BenchmarkSegmentMetric> CreateMetrics(double avgMinutes, int violations, int samples)
        {
            return new List<BenchmarkSegmentMetric>
            {
                new()
                {
                    Segment = CustomerSegment.FreelancerLegal,
                    SampleCount = samples,
                    AverageMinutesPerTask = avgMinutes,
                    TerminologyViolationCount = violations,
                    AcceptanceCount = (int)(samples * 0.72),
                    EditCount = (int)(samples * 0.28)
                },
                new()
                {
                    Segment = CustomerSegment.AgencyLegal,
                    SampleCount = samples,
                    AverageMinutesPerTask = avgMinutes + 1.5,
                    TerminologyViolationCount = violations + 3,
                    AcceptanceCount = (int)(samples * 0.68),
                    EditCount = (int)(samples * 0.32)
                }
            };
        }
    }
}
