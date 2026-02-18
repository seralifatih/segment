using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class DecisionGateEvaluator : IDecisionGateEvaluator
    {
        private static readonly HashSet<string> CriticalMetrics = new(StringComparer.OrdinalIgnoreCase)
        {
            PmfMetricKeys.RetentionWeek4,
            PmfMetricKeys.TermViolationsRate,
            PmfMetricKeys.P95LatencyMs,
            PmfMetricKeys.ChurnRate
        };

        private readonly IGtmConfigService _gtmConfigService;

        public DecisionGateEvaluator(IGtmConfigService gtmConfigService)
        {
            _gtmConfigService = gtmConfigService;
        }

        public GateDecisionResult Evaluate(LaunchPhase phase, PmfDashboardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var targets = _gtmConfigService.GetKpiTargetsByPhase(phase);
            var map = snapshot.ToMetricMap();
            var evaluations = new List<GateMetricEvaluation>();

            foreach (var target in targets)
            {
                if (!map.TryGetValue(target.MetricKey, out double actual))
                {
                    continue;
                }

                evaluations.Add(new GateMetricEvaluation
                {
                    MetricKey = target.MetricKey,
                    ActualValue = actual,
                    Threshold = target.Threshold,
                    ComparisonOperator = target.ComparisonOperator,
                    Passed = Compare(actual, target.Threshold, target.ComparisonOperator)
                });
            }

            int failed = evaluations.Count(x => !x.Passed);
            int passed = evaluations.Count - failed;
            int criticalFailures = evaluations.Count(x => !x.Passed && CriticalMetrics.Contains(x.MetricKey));
            GateRecommendation recommendation;
            string reason;

            if (evaluations.Count == 0)
            {
                recommendation = GateRecommendation.Hold;
                reason = "No phase KPI targets were evaluable from the current dashboard snapshot.";
            }
            else if (criticalFailures >= 2)
            {
                recommendation = GateRecommendation.Rollback;
                reason = "Multiple critical KPI failures detected.";
            }
            else if (failed == 0)
            {
                recommendation = GateRecommendation.Advance;
                reason = "All tracked KPI targets passed.";
            }
            else
            {
                recommendation = GateRecommendation.Hold;
                reason = "Mixed KPI performance. Stabilize before phase transition.";
            }

            return new GateDecisionResult
            {
                Phase = phase,
                Recommendation = recommendation,
                PassedCount = passed,
                FailedCount = failed,
                Evaluations = evaluations,
                Reason = reason
            };
        }

        private static bool Compare(double actual, double threshold, KpiComparisonOperator comparisonOperator)
        {
            return comparisonOperator switch
            {
                KpiComparisonOperator.GreaterThan => actual > threshold,
                KpiComparisonOperator.GreaterThanOrEqual => actual >= threshold,
                KpiComparisonOperator.LessThan => actual < threshold,
                KpiComparisonOperator.LessThanOrEqual => actual <= threshold,
                KpiComparisonOperator.Equal => Math.Abs(actual - threshold) < 1e-9,
                _ => false
            };
        }
    }
}
