using System.Collections.Generic;

namespace Segment.App.Models
{
    public class DemoDatasetPackage
    {
        public string DatasetId { get; set; } = "legal-demo-v1";
        public string Name { get; set; } = "Legal Demo Dataset";
        public List<DemoLegalClauseSample> Clauses { get; set; } = new();
    }
}
