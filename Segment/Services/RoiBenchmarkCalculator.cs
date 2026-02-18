using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class RoiBenchmarkCalculator : IRoiBenchmarkCalculator
    {
        public PilotRoiReport Calculate(BenchmarkSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var baselineCaptures = session.WeekCaptures.Where(x => x.PeriodType == BenchmarkPeriodType.Baseline).ToList();
            var assistedCaptures = session.WeekCaptures.Where(x => x.PeriodType == BenchmarkPeriodType.SegmentAssisted).ToList();

            var baseline = Aggregate(baselineCaptures);
            var assisted = Aggregate(assistedCaptures);

            double timeSaved = baseline.AverageMinutesPerTask <= 0
                ? 0
                : ((baseline.AverageMinutesPerTask - assisted.AverageMinutesPerTask) / baseline.AverageMinutesPerTask) * 100.0;

            double violationReduction = baseline.ViolationRate <= 0
                ? 0
                : ((baseline.ViolationRate - assisted.ViolationRate) / baseline.ViolationRate) * 100.0;

            double acceptanceDelta = assisted.AcceptanceRate - baseline.AcceptanceRate;
            double editDelta = assisted.EditRate - baseline.EditRate;

            return new PilotRoiReport
            {
                SessionId = session.Id,
                GeneratedAtUtc = DateTime.UtcNow,
                BaselineAverageMinutesPerTask = baseline.AverageMinutesPerTask,
                AssistedAverageMinutesPerTask = assisted.AverageMinutesPerTask,
                TimeSavedPercentage = timeSaved,
                BaselineViolationRate = baseline.ViolationRate,
                AssistedViolationRate = assisted.ViolationRate,
                ViolationReductionPercentage = violationReduction,
                BaselineAcceptanceRate = baseline.AcceptanceRate,
                AssistedAcceptanceRate = assisted.AcceptanceRate,
                AcceptanceRateDelta = acceptanceDelta,
                BaselineEditRate = baseline.EditRate,
                AssistedEditRate = assisted.EditRate,
                EditRateDelta = editDelta,
                TotalBaselineSamples = baseline.TotalSamples,
                TotalAssistedSamples = assisted.TotalSamples,
                ConfidenceSummary = BuildConfidenceSummary(baseline.TotalSamples, assisted.TotalSamples, timeSaved, violationReduction)
            };
        }

        private static AggregateMetrics Aggregate(List<BenchmarkWeekCapture> captures)
        {
            var metrics = captures.SelectMany(x => x.SegmentMetrics).ToList();

            int totalSamples = metrics.Sum(x => Math.Max(0, x.SampleCount));
            if (totalSamples == 0)
            {
                return new AggregateMetrics();
            }

            double weightedMinutes = metrics.Sum(x => Math.Max(0, x.SampleCount) * Math.Max(0, x.AverageMinutesPerTask));
            int violations = metrics.Sum(x => Math.Max(0, x.TerminologyViolationCount));
            int acceptances = metrics.Sum(x => Math.Max(0, x.AcceptanceCount));
            int edits = metrics.Sum(x => Math.Max(0, x.EditCount));

            return new AggregateMetrics
            {
                TotalSamples = totalSamples,
                AverageMinutesPerTask = weightedMinutes / totalSamples,
                ViolationRate = (double)violations / totalSamples,
                AcceptanceRate = (double)acceptances / totalSamples,
                EditRate = (double)edits / totalSamples
            };
        }

        private static string BuildConfidenceSummary(int baselineSamples, int assistedSamples, double timeSavedPct, double violationReductionPct)
        {
            int minSamples = Math.Min(baselineSamples, assistedSamples);

            if (minSamples >= 120 && timeSavedPct >= 15 && violationReductionPct >= 15)
            {
                return "High confidence: strong sample size and clear improvements in time and terminology quality.";
            }

            if (minSamples >= 60 && (timeSavedPct >= 8 || violationReductionPct >= 8))
            {
                return "Medium confidence: directional gains detected with moderate sample size.";
            }

            return "Low confidence: insufficient sample size or weak improvement signal.";
        }

        private sealed class AggregateMetrics
        {
            public int TotalSamples { get; set; }
            public double AverageMinutesPerTask { get; set; }
            public double ViolationRate { get; set; }
            public double AcceptanceRate { get; set; }
            public double EditRate { get; set; }
        }
    }
}
