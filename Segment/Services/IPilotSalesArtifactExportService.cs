using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPilotSalesArtifactExportService
    {
        PilotSalesExportPackage ExportPackage(
            BenchmarkSession session,
            string outputDirectory,
            PilotSalesTemplateConfiguration? templateConfiguration = null,
            string? packageName = null);
    }
}
