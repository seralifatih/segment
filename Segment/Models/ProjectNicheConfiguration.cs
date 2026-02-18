using System.Collections.Generic;

namespace Segment.App.Models
{
    public class ExternalProjectProfileMapping
    {
        public string ConnectorId { get; set; } = "";
        public string ExternalProjectId { get; set; } = "";
        public string ExternalClientId { get; set; } = "";
        public string ExternalStyleGuideId { get; set; } = "";
        public IReadOnlyList<string> ExternalTags { get; set; } = new List<string>();
        public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public class ProjectNicheConfiguration
    {
        public string ProjectProfileName { get; set; } = "";
        public DomainVertical Domain { get; set; } = DomainVertical.Legal;
        public IReadOnlyList<string> StyleHints { get; set; } = new List<string>();
        public IReadOnlyList<string> EnabledQaChecks { get; set; } = new List<string>();
        public ExternalProjectProfileMapping ExternalMapping { get; set; } = new();
    }
}
