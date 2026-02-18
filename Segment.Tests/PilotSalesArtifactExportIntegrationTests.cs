using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PilotSalesArtifactExportIntegrationTests
    {
        [Fact]
        public void ExportPackage_Should_Generate_All_Artifacts_And_Zip()
        {
            string outputRoot = Path.Combine(Path.GetTempPath(), "SegmentPilotSalesArtifactsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRoot);

            try
            {
                var session = new BenchmarkSession
                {
                    Id = "pilot-session-001",
                    PilotName = "Northwind Legal Pilot",
                    WeekCaptures =
                    {
                        new BenchmarkWeekCapture
                        {
                            WeekNumber = 1,
                            PeriodType = BenchmarkPeriodType.Baseline,
                            SegmentMetrics =
                            {
                                new BenchmarkSegmentMetric
                                {
                                    Segment = CustomerSegment.FreelancerLegal,
                                    SampleCount = 100,
                                    AverageMinutesPerTask = 11,
                                    TerminologyViolationCount = 20,
                                    AcceptanceCount = 65,
                                    EditCount = 35
                                }
                            }
                        },
                        new BenchmarkWeekCapture
                        {
                            WeekNumber = 2,
                            PeriodType = BenchmarkPeriodType.SegmentAssisted,
                            SegmentMetrics =
                            {
                                new BenchmarkSegmentMetric
                                {
                                    Segment = CustomerSegment.AgencyLegal,
                                    SampleCount = 110,
                                    AverageMinutesPerTask = 7,
                                    TerminologyViolationCount = 9,
                                    AcceptanceCount = 87,
                                    EditCount = 22
                                }
                            }
                        }
                    }
                };

                var config = new PilotSalesTemplateConfiguration
                {
                    SecurityPrivacyOnePagerTemplate = "Security memo for {{AgencyName}}. Confidence={{ConfidenceSummary}}",
                    PilotSuccessCriteriaTemplate = "Success sheet: Time={{TimeSavedPct}}, Violations={{ViolationReductionPct}}",
                    PricingProposalSummaryTemplate = "Pricing: Monthly={{EstimatedMonthlyCostSavingsUsd}}, Annual={{EstimatedAnnualCostSavingsUsd}}",
                    EstimatedMonthlyTaskVolume = 320,
                    FreelancerHourlyRateUsd = 80
                };

                var service = new PilotSalesArtifactExportService();
                PilotSalesExportPackage package = service.ExportPackage(session, outputRoot, config, packageName: "northwind-pilot");

                package.Artifacts.Should().HaveCount(6);
                Directory.Exists(package.PackageDirectory).Should().BeTrue();
                File.Exists(package.ZipPath).Should().BeTrue();

                package.Artifacts.Select(x => x.TextPath).Should().OnlyContain(path => File.Exists(path));
                package.Artifacts.Select(x => x.PdfPath).Should().OnlyContain(path => File.Exists(path));
                package.Artifacts.Select(x => new FileInfo(x.PdfPath).Length).Should().OnlyContain(x => x > 100);

                string pricingArtifactText = File.ReadAllText(package.Artifacts.Single(x => x.Key == "pricing_proposal_summary").TextPath);
                pricingArtifactText.Should().Contain("Pricing: Monthly=$");
                pricingArtifactText.Should().Contain("Annual=$");

                using var zip = ZipFile.OpenRead(package.ZipPath);
                zip.Entries.Count(x => x.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).Should().Be(6);
                zip.Entries.Count(x => x.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).Should().Be(6);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(outputRoot))
                    {
                        Directory.Delete(outputRoot, recursive: true);
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
