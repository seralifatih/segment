using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPmfSnapshotExportService
    {
        void ExportWeeklyCsv(PmfDashboardSnapshot snapshot, GateDecisionResult decision, string filePath);
        void ExportWeeklyPdf(PmfDashboardSnapshot snapshot, GateDecisionResult decision, string filePath);
    }
}
