using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IGlossaryQualityReportService
    {
        GlossaryQualityReport BuildReport(string workspaceId);
    }
}
