using System.Collections.Generic;
using LiteDB;

namespace Segment.App.Models
{
    public class GtmConfig
    {
        public int ConfigVersion { get; set; } = 1;
        public LaunchPhase ActiveLaunchPhase { get; set; } = LaunchPhase.Phase0Readiness;
        public Dictionary<LaunchPhase, int> CohortSizeTargets { get; set; } = new();
        public List<GtmKpiTarget> KpiTargets { get; set; } = new();
        public List<PricingPlanDefinition> PricingPlans { get; set; } = new();
    }

    internal class GtmConfigDocument
    {
        [BsonId]
        public string Id { get; set; } = "default";
        public int SchemaVersion { get; set; } = 1;
        public GtmConfig Config { get; set; } = new();
    }
}
