using System;

namespace Segment.App.Models
{
    public class PilotSalesArtifactDocument
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string TextPath { get; set; } = "";
        public string PdfPath { get; set; } = "";
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
