using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class NicheTelemetryServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly NicheTelemetryService _service;
        private readonly bool _originalUsageConsent;

        public NicheTelemetryServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentNicheTelemetryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _originalUsageConsent = SettingsService.Current.TelemetryUsageMetricsConsent;
            SettingsService.Current.TelemetryUsageMetricsConsent = true;
            _service = new NicheTelemetryService(_basePath);
        }

        [Fact]
        public void BuildEvent_Should_Follow_Required_Payload_Shape()
        {
            string segmentHash = _service.HashSegment("confidential legal clause");
            var ev = _service.BuildEvent(
                NicheTelemetryEventType.TranslationCompleted,
                DomainVertical.Legal,
                segmentHash,
                success: true,
                latencyMs: 123.45,
                blockedCount: 2,
                overrideCount: 1,
                glossaryHitCount: 3);

            ev.EventType.Should().Be(NicheTelemetryEventType.TranslationCompleted);
            ev.DomainVertical.Should().Be(DomainVertical.Legal);
            ev.LatencyMs.Should().BeApproximately(123.45, 0.001);
            ev.Success.Should().BeTrue();
            ev.BlockedCount.Should().Be(2);
            ev.OverrideCount.Should().Be(1);
            ev.GlossaryHitCount.Should().Be(3);
            ev.SegmentHash.Should().NotBeNullOrWhiteSpace();
            ev.SegmentHash.Should().HaveLength(16);
            ev.IngestionKey.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void BuildEvent_And_Export_Should_Not_Contain_Raw_Text()
        {
            string rawSource = "ACME CORP shall transfer 5000 USD on 2026-04-01.";
            string rawTarget = "ACME CORP 2026-04-01 tarihinde 5000 USD transfer edecektir.";
            string segmentHash = _service.HashSegment(rawSource);

            var ev = _service.BuildEvent(
                NicheTelemetryEventType.TranslationCompleted,
                DomainVertical.Legal,
                segmentHash,
                success: true,
                latencyMs: 80,
                glossaryHitCount: 2);
            _service.RecordEvent(ev);

            string eventJson = JsonSerializer.Serialize(ev);
            eventJson.Should().NotContain(rawSource);
            eventJson.Should().NotContain(rawTarget);

            string csvPath = Path.Combine(_basePath, "metrics.csv");
            _service.ExportMetricsCsv(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), csvPath);
            string csv = File.ReadAllText(csvPath);
            csv.Should().NotContain(rawSource);
            csv.Should().NotContain(rawTarget);
        }

        [Fact]
        public void GetMetricsSnapshot_Should_Calculate_Workflow_And_Latency_Metrics()
        {
            string hash = _service.HashSegment("workflow sample");
            DateTime now = DateTime.UtcNow;

            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.TranslationRequested, DomainVertical.Legal, hash, true));
            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.TranslationCompleted, DomainVertical.Legal, hash, true, latencyMs: 100));
            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.GlossaryTermApplied, DomainVertical.Legal, hash, true, glossaryHitCount: 2));
            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.GuardrailBlocked, DomainVertical.Legal, hash, false, blockedCount: 1));
            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.GuardrailOverridden, DomainVertical.Legal, hash, true, overrideCount: 1));
            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.PasteCompleted, DomainVertical.Legal, hash, true));
            _service.RecordEvent(_service.BuildEvent(NicheTelemetryEventType.PasteReverted, DomainVertical.Legal, hash, true));

            NicheTelemetryMetricsSnapshot snapshot = _service.GetMetricsSnapshot(now.AddMinutes(-5), now.AddMinutes(5));
            snapshot.TranslationRequestedCount.Should().Be(1);
            snapshot.TranslationCompletedCount.Should().Be(1);
            snapshot.PasteCompletedCount.Should().Be(1);
            snapshot.PasteRevertedCount.Should().Be(1);
            snapshot.GuardrailBlockedCount.Should().Be(1);
            snapshot.GuardrailOverriddenCount.Should().Be(1);
            snapshot.TotalGlossaryHits.Should().Be(2);
            snapshot.WorkflowCompletionRate.Should().BeApproximately(1, 0.0001);
            snapshot.GlossaryReuseRate.Should().BeApproximately(2, 0.0001);
            snapshot.ViolationRate.Should().BeApproximately(1, 0.0001);
            snapshot.P50LatencyMs.Should().BeApproximately(100, 0.0001);
            snapshot.P95LatencyMs.Should().BeApproximately(100, 0.0001);
        }

        [Fact]
        public void RecordEvent_Should_Skip_When_UsageTelemetry_Consent_Is_Disabled()
        {
            SettingsService.Current.TelemetryUsageMetricsConsent = false;
            string hash = _service.HashSegment("consent-disabled");
            var ev = _service.BuildEvent(NicheTelemetryEventType.TranslationRequested, DomainVertical.Legal, hash, true);

            _service.RecordEvent(ev);

            NicheTelemetryMetricsSnapshot snapshot = _service.GetMetricsSnapshot(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            snapshot.TranslationRequestedCount.Should().Be(0);
            SettingsService.Current.TelemetryUsageMetricsConsent = true;
        }

        public void Dispose()
        {
            SettingsService.Current.TelemetryUsageMetricsConsent = _originalUsageConsent;
            _service.Dispose();
            try
            {
                if (Directory.Exists(_basePath))
                {
                    Directory.Delete(_basePath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
