namespace Segment.App.Models
{
    public class NichePackImportResult
    {
        public int InsertedTermCount { get; set; }
        public int UpdatedTermCount { get; set; }
        public int SkippedTermCount { get; set; }
        public int DuplicateConflictCount { get; set; }
        public DomainVertical Domain { get; set; } = DomainVertical.Legal;
        public string TargetProfileName { get; set; } = "";
        public NichePackMetadata Metadata { get; set; } = new();
    }
}
