namespace Segment.App.Models
{
    public class ProviderRoutingDecision
    {
        public string RequestedProvider { get; set; } = "";
        public string EffectiveRoute { get; set; } = "";
        public ConfidentialityMode ConfidentialityMode { get; set; } = ConfidentialityMode.Standard;
        public bool ApplyRedactionBeforeCloudCall { get; set; }
        public bool IsLocalOnly { get; set; }
        public bool IsBlocked { get; set; }
        public string Reason { get; set; } = "";
    }
}
