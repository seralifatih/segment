using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class DecisionGateEvaluatorTests
    {
        [Fact]
        public void Evaluate_Should_Recommend_Advance_When_All_Metrics_Pass()
        {
            string path = CreateTempPath();
            try
            {
                using var configService = new GtmConfigService(path);
                SeedTargets(configService, LaunchPhase.PrivateBeta);

                var evaluator = new DecisionGateEvaluator(configService);
                var snapshot = new PmfDashboardSnapshot
                {
                    RetentionWeek4 = 0.72,
                    TerminologyViolationRate = 0.02,
                    P95LatencyMs = 1200,
                    ChurnRate = 0.08
                };

                var result = evaluator.Evaluate(LaunchPhase.PrivateBeta, snapshot);

                result.Recommendation.Should().Be(GateRecommendation.Advance);
                result.FailedCount.Should().Be(0);
            }
            finally
            {
                DeleteTempPath(path);
            }
        }

        [Fact]
        public void Evaluate_Should_Recommend_Rollback_On_Multiple_Critical_Failures()
        {
            string path = CreateTempPath();
            try
            {
                using var configService = new GtmConfigService(path);
                SeedTargets(configService, LaunchPhase.PaidPilot);

                var evaluator = new DecisionGateEvaluator(configService);
                var snapshot = new PmfDashboardSnapshot
                {
                    RetentionWeek4 = 0.55,
                    TerminologyViolationRate = 0.08,
                    P95LatencyMs = 1700,
                    ChurnRate = 0.20
                };

                var result = evaluator.Evaluate(LaunchPhase.PaidPilot, snapshot);

                result.Recommendation.Should().Be(GateRecommendation.Rollback);
                result.FailedCount.Should().BeGreaterThanOrEqualTo(2);
            }
            finally
            {
                DeleteTempPath(path);
            }
        }

        [Fact]
        public void Evaluate_Should_Recommend_Hold_On_Mixed_Performance()
        {
            string path = CreateTempPath();
            try
            {
                using var configService = new GtmConfigService(path);
                SeedTargets(configService, LaunchPhase.PaidPilot);

                var evaluator = new DecisionGateEvaluator(configService);
                var snapshot = new PmfDashboardSnapshot
                {
                    RetentionWeek4 = 0.78,
                    TerminologyViolationRate = 0.05,
                    P95LatencyMs = 950,
                    ChurnRate = 0.07
                };

                var result = evaluator.Evaluate(LaunchPhase.PaidPilot, snapshot);

                result.Recommendation.Should().Be(GateRecommendation.Hold);
                result.PassedCount.Should().BeGreaterThan(0);
                result.FailedCount.Should().BeGreaterThan(0);
            }
            finally
            {
                DeleteTempPath(path);
            }
        }

        private static void SeedTargets(GtmConfigService configService, LaunchPhase phase)
        {
            var config = configService.LoadConfig();
            config.KpiTargets = new List<GtmKpiTarget>
            {
                new() { MetricKey = PmfMetricKeys.RetentionWeek4, Threshold = 0.70, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = phase },
                new() { MetricKey = PmfMetricKeys.TermViolationsRate, Threshold = 0.03, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = phase },
                new() { MetricKey = PmfMetricKeys.P95LatencyMs, Threshold = 1500, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = phase },
                new() { MetricKey = PmfMetricKeys.ChurnRate, Threshold = 0.10, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = phase }
            };
            configService.SaveConfig(config);
        }

        private static string CreateTempPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "SegmentDecisionGateTests", Guid.NewGuid().ToString("N"));
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
