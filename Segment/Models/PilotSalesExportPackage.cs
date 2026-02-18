using System;
using System.Collections.Generic;

namespace Segment.App.Models
{
    public class PilotSalesExportPackage
    {
        public string SessionId { get; set; } = "";
        public string PackageDirectory { get; set; } = "";
        public string ZipPath { get; set; } = "";
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public List<PilotSalesArtifactDocument> Artifacts { get; set; } = new();
    }
}
