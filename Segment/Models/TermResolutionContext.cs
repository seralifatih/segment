namespace Segment.App.Models
{
    public class TermResolutionContext
    {
        public DomainVertical DomainVertical { get; set; } = DomainVertical.Legal;
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public string UserId { get; set; } = "";
        public string TeamId { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string SessionId { get; set; } = "";
    }
}
