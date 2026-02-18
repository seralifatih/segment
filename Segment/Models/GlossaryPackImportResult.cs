namespace Segment.App.Models
{
    public class GlossaryPackImportResult
    {
        public int InsertedTermCount { get; set; }
        public GlossaryPackMetadata Metadata { get; set; } = new();
    }
}
