using System;
using System.Collections.Generic;

namespace Segment.App.Models
{
    public class PmfDashboardSnapshot
    {
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public int Dau { get; set; }
        public int Wau { get; set; }
        public double SegmentsPerDay { get; set; }
        public double RetentionWeek4 { get; set; }
        public double GlossaryReuseRate { get; set; }
        public double TerminologyViolationRate { get; set; }
        public double P50LatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double PilotToPaidConversion { get; set; }
        public double ChurnRate { get; set; }
        public Dictionary<string, double> ToMetricMap()
        {
            return new Dictionary<string, double>
            {
                [PmfMetricKeys.Dau] = Dau,
                [PmfMetricKeys.Wau] = Wau,
                [PmfMetricKeys.SegmentsPerDay] = SegmentsPerDay,
                [PmfMetricKeys.RetentionWeek4] = RetentionWeek4,
                [PmfMetricKeys.GlossaryReuseRate] = GlossaryReuseRate,
                [PmfMetricKeys.TermViolationsRate] = TerminologyViolationRate,
                [PmfMetricKeys.P50LatencyMs] = P50LatencyMs,
                [PmfMetricKeys.P95LatencyMs] = P95LatencyMs,
                [PmfMetricKeys.PilotToPaidConversion] = PilotToPaidConversion,
                [PmfMetricKeys.ChurnRate] = ChurnRate
            };
        }
    }
}
