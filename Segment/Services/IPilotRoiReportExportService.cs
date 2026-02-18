using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPilotRoiReportExportService
    {
        void ExportCsv(PilotRoiReport report, string filePath);
        void ExportPdf(PilotRoiReport report, string filePath);
    }
}
