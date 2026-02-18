using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ReflexLatencyMetricsServiceTests : IDisposable
    {
        private readonly bool _originalUsageConsent;

        public ReflexLatencyMetricsServiceTests()
        {
            _originalUsageConsent = SettingsService.Current.TelemetryUsageMetricsConsent;
            SettingsService.Current.TelemetryUsageMetricsConsent = true;
        }

        public void Dispose()
        {
            SettingsService.Current.TelemetryUsageMetricsConsent = _originalUsageConsent;
        }

        [Fact]
        public void GetSnapshot_Should_Use_ShortSegmentWindow_And_DeterministicPercentiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "segment_latency_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var service = new ReflexLatencyMetricsService(windowSize: 20, logger: new StructuredLogger(tempDir, enforceConsent: false));

            service.Record(BuildSample(100, true));
            service.Record(BuildSample(200, true));
            service.Record(BuildSample(300, true));
            service.Record(BuildSample(400, true));
            service.Record(BuildSample(500, true));
            service.Record(BuildSample(5000, false));

            ReflexLatencySnapshot snapshot = service.GetSnapshot();

            snapshot.SampleCount.Should().Be(6);
            snapshot.ShortSegmentSampleCount.Should().Be(5);
            snapshot.EndToEndP50Ms.Should().Be(300);
            snapshot.EndToEndP95Ms.Should().Be(480);
        }

        [Fact]
        public void Record_Should_Write_Event_And_Periodic_Summary_Log()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "segment_latency_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var service = new ReflexLatencyMetricsService(windowSize: 50, logger: new StructuredLogger(tempDir, enforceConsent: false));

            for (int i = 0; i < 10; i++)
            {
                service.Record(BuildSample(100 + i * 10, true));
            }

            string logPath = Path.Combine(tempDir, "structured_events.jsonl");
            File.Exists(logPath).Should().BeTrue();

            string[] lines = File.ReadAllLines(logPath);
            lines.Count(x => x.Contains("\"event\":\"reflex_latency_event\"", StringComparison.Ordinal)).Should().Be(10);
            lines.Count(x => x.Contains("\"event\":\"reflex_latency_summary\"", StringComparison.Ordinal)).Should().Be(1);
        }

        private static ReflexLatencySample BuildSample(double endToEndMs, bool isShortSegment)
        {
            return new ReflexLatencySample
            {
                IsShortSegment = isShortSegment,
                SourceLength = isShortSegment ? 80 : 800,
                CaptureToRequestStartMs = 20,
                ProviderRoundtripMs = endToEndMs - 40,
                ResponseToRenderMs = 20,
                EndToEndMs = endToEndMs,
                ProviderUsed = "Google",
                UsedFallbackProvider = false,
                BudgetEnforced = isShortSegment,
                BudgetExceeded = false
            };
        }
    }
}
