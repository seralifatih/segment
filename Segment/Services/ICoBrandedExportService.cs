using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ICoBrandedExportService
    {
        void ExportPilotOutcomeSummaryCsv(PilotRoiReport report, CoBrandedExportOptions options, string filePath);
        void ExportPilotOutcomeSummaryPdf(PilotRoiReport report, CoBrandedExportOptions options, string filePath);
        void ExportGlossaryQualityReportCsv(GlossaryQualityReport report, CoBrandedExportOptions options, string filePath);
        void ExportGlossaryQualityReportPdf(GlossaryQualityReport report, CoBrandedExportOptions options, string filePath);
    }
}
