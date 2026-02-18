using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface INicheTelemetryService
    {
        string HashSegment(string segment);
        NicheTelemetryEvent BuildEvent(
            NicheTelemetryEventType eventType,
            DomainVertical domainVertical,
            string segmentHash,
            bool success,
            double latencyMs = 0,
            int blockedCount = 0,
            int overrideCount = 0,
            int glossaryHitCount = 0);
        void RecordEvent(NicheTelemetryEvent telemetryEvent);
        NicheTelemetryMetricsSnapshot GetMetricsSnapshot(DateTime windowStartUtc, DateTime windowEndUtc);
        void ExportMetricsCsv(DateTime windowStartUtc, DateTime windowEndUtc, string filePath);
    }
}
