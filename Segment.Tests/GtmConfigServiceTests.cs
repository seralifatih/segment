using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class GtmConfigServiceTests
    {
        [Fact]
        public void LoadSave_RoundTrip_Should_Persist_Config_And_Increment_Version()
        {
            string basePath = CreateTempPath();

            try
            {
                using (var service = new GtmConfigService(basePath))
                {
                    var config = service.LoadConfig();
                    config.ActiveLaunchPhase = LaunchPhase.PaidPilot;
                    config.CohortSizeTargets[LaunchPhase.PrivateBeta] = 45;
                    config.PricingPlans.Add(new PricingPlanDefinition
                    {
                        PlanId = "agency-legal-pilot",
                        Segment = CustomerSegment.AgencyLegal,
                        MonthlyPrice = 199m,
                        Entitlements = { "10 seats", "pilot-support" }
                    });

                    int previousVersion = config.ConfigVersion;
                    service.SaveConfig(config);
                    config.ConfigVersion.Should().BeGreaterThan(previousVersion);
                }

                using (var service = new GtmConfigService(basePath))
                {
                    var reloaded = service.LoadConfig();
                    reloaded.ActiveLaunchPhase.Should().Be(LaunchPhase.PaidPilot);
                    reloaded.CohortSizeTargets[LaunchPhase.PrivateBeta].Should().Be(45);
                    reloaded.PricingPlans.Should().Contain(x => x.PlanId == "agency-legal-pilot");
                    reloaded.ConfigVersion.Should().BeGreaterThan(1);
                }
            }
            finally
            {
                DeleteTempPath(basePath);
            }
        }

        [Fact]
        public void GetKpiTargetsByPhase_Should_Return_Only_Targets_For_Phase()
        {
            string basePath = CreateTempPath();

            try
            {
                using var service = new GtmConfigService(basePath);

                var privateBetaTargets = service.GetKpiTargetsByPhase(LaunchPhase.PrivateBeta);
                privateBetaTargets.Should().NotBeEmpty();
                privateBetaTargets.Should().OnlyContain(x => x.Phase == LaunchPhase.PrivateBeta);
                privateBetaTargets.Should().Contain(x =>
                    x.MetricKey == "retention_30d" &&
                    x.ComparisonOperator == KpiComparisonOperator.GreaterThanOrEqual &&
                    Math.Abs(x.Threshold - 0.70) < 0.0001);
                privateBetaTargets.Should().Contain(x =>
                    x.MetricKey == "p95_latency_ms" &&
                    x.ComparisonOperator == KpiComparisonOperator.LessThanOrEqual &&
                    Math.Abs(x.Threshold - 1500) < 0.0001);
            }
            finally
            {
                DeleteTempPath(basePath);
            }
        }

        private static string CreateTempPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "SegmentGtmConfigTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTempPath(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
