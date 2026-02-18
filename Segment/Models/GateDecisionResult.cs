using System.Collections.Generic;

namespace Segment.App.Models
{
    public class GateDecisionResult
    {
        public LaunchPhase Phase { get; set; }
        public GateRecommendation Recommendation { get; set; } = GateRecommendation.Hold;
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public IReadOnlyList<GateMetricEvaluation> Evaluations { get; set; } = new List<GateMetricEvaluation>();
        public string Reason { get; set; } = "";
    }
}
