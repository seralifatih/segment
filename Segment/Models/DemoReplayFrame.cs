namespace Segment.App.Models
{
    public class DemoReplayFrame
    {
        public int StepNumber { get; set; }
        public string ClauseId { get; set; } = "";
        public string SourceText { get; set; } = "";
        public string SuggestedTranslation { get; set; } = "";
        public string ExpectedEditHint { get; set; } = "";
    }
}
