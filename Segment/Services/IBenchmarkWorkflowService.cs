using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IBenchmarkWorkflowService
    {
        BenchmarkSession StartSession(string pilotName);
        BenchmarkSession CaptureWeek(string sessionId, int weekNumber, BenchmarkPeriodType periodType, IReadOnlyList<BenchmarkSegmentMetric> metrics);
        BenchmarkSession GetSession(string sessionId);
        PilotRoiReport GenerateSummaryReport(string sessionId);
    }
}
