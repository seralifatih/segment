using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class NicheTelemetryService : INicheTelemetryService, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<NicheTelemetryEvent> _events;

        public NicheTelemetryService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "niche_telemetry.db");

            _database = new LiteDatabase(dbPath);
            _events = _database.GetCollection<NicheTelemetryEvent>("niche_telemetry_events");
            _events.EnsureIndex(x => x.CapturedAtUtc);
            _events.EnsureIndex(x => x.SegmentHash);
            _events.EnsureIndex(x => x.IngestionKey, unique: true);
        }

        public string HashSegment(string segment)
        {
            string safe = (segment ?? string.Empty).Trim();
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(safe));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return hex[..16];
        }

        public NicheTelemetryEvent BuildEvent(
            NicheTelemetryEventType eventType,
            DomainVertical domainVertical,
            string segmentHash,
            bool success,
            double latencyMs = 0,
            int blockedCount = 0,
            int overrideCount = 0,
            int glossaryHitCount = 0)
        {
            string safeHash = string.IsNullOrWhiteSpace(segmentHash) ? HashSegment(string.Empty) : segmentHash.Trim();
            var ev = new NicheTelemetryEvent
            {
                EventType = eventType,
                DomainVertical = domainVertical,
                SegmentHash = safeHash,
                Success = success,
                LatencyMs = Math.Max(0, latencyMs),
                BlockedCount = Math.Max(0, blockedCount),
                OverrideCount = Math.Max(0, overrideCount),
                GlossaryHitCount = Math.Max(0, glossaryHitCount),
                CapturedAtUtc = DateTime.UtcNow
            };
            ev.IngestionKey = BuildIngestionKey(ev);
            return ev;
        }

        public void RecordEvent(NicheTelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null) throw new ArgumentNullException(nameof(telemetryEvent));
            if (!SettingsService.Current.TelemetryUsageMetricsConsent)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(telemetryEvent.SegmentHash))
            {
                throw new ArgumentException("SegmentHash is required. Raw segment text is not accepted.", nameof(telemetryEvent));
            }

            telemetryEvent.SegmentHash = telemetryEvent.SegmentHash.Trim();
            telemetryEvent.LatencyMs = Math.Max(0, telemetryEvent.LatencyMs);
            telemetryEvent.BlockedCount = Math.Max(0, telemetryEvent.BlockedCount);
            telemetryEvent.OverrideCount = Math.Max(0, telemetryEvent.OverrideCount);
            telemetryEvent.GlossaryHitCount = Math.Max(0, telemetryEvent.GlossaryHitCount);
            telemetryEvent.CapturedAtUtc = telemetryEvent.CapturedAtUtc == default
                ? DateTime.UtcNow
                : telemetryEvent.CapturedAtUtc.ToUniversalTime();

            if (string.IsNullOrWhiteSpace(telemetryEvent.IngestionKey))
            {
                telemetryEvent.IngestionKey = BuildIngestionKey(telemetryEvent);
            }

            int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _events.Upsert(telemetryEvent);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    Thread.Sleep(20 * attempt);
                }
            }
        }

        public NicheTelemetryMetricsSnapshot GetMetricsSnapshot(DateTime windowStartUtc, DateTime windowEndUtc)
        {
            DateTime start = windowStartUtc.ToUniversalTime();
            DateTime end = windowEndUtc.ToUniversalTime();
            if (end <= start)
            {
                throw new ArgumentException("Window end must be greater than window start.");
            }

            List<NicheTelemetryEvent> events = _events
                .Find(x => x.CapturedAtUtc >= start && x.CapturedAtUtc < end)
                .ToList();

            int requested = events.Count(x => x.EventType == NicheTelemetryEventType.TranslationRequested);
            int completed = events.Count(x => x.EventType == NicheTelemetryEventType.TranslationCompleted && x.Success);
            int pasteCompleted = events.Count(x => x.EventType == NicheTelemetryEventType.PasteCompleted && x.Success);
            int pasteReverted = events.Count(x => x.EventType == NicheTelemetryEventType.PasteReverted);
            int blocked = events.Sum(x => x.EventType == NicheTelemetryEventType.GuardrailBlocked ? Math.Max(1, x.BlockedCount) : 0);
            int overridden = events.Sum(x => x.EventType == NicheTelemetryEventType.GuardrailOverridden ? Math.Max(1, x.OverrideCount) : 0);
            int glossaryHits = events.Sum(x => x.EventType == NicheTelemetryEventType.GlossaryTermApplied ? x.GlossaryHitCount : 0);

            List<double> latencies = events
                .Where(x => x.EventType == NicheTelemetryEventType.TranslationCompleted && x.LatencyMs > 0)
                .Select(x => x.LatencyMs)
                .OrderBy(x => x)
                .ToList();

            return new NicheTelemetryMetricsSnapshot
            {
                WindowStartUtc = start,
                WindowEndUtc = end,
                TranslationRequestedCount = requested,
                TranslationCompletedCount = completed,
                PasteCompletedCount = pasteCompleted,
                PasteRevertedCount = pasteReverted,
                GuardrailBlockedCount = blocked,
                GuardrailOverriddenCount = overridden,
                TotalGlossaryHits = glossaryHits,
                WorkflowCompletionRate = requested == 0 ? 0 : (double)pasteCompleted / requested,
                GlossaryReuseRate = completed == 0 ? 0 : (double)glossaryHits / completed,
                ViolationRate = completed == 0 ? 0 : (double)blocked / completed,
                P50LatencyMs = Percentile(latencies, 0.50),
                P95LatencyMs = Percentile(latencies, 0.95)
            };
        }

        public void ExportMetricsCsv(DateTime windowStartUtc, DateTime windowEndUtc, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Export path is required.", nameof(filePath));
            }

            NicheTelemetryMetricsSnapshot snapshot = GetMetricsSnapshot(windowStartUtc, windowEndUtc);
            var sb = new StringBuilder();
            sb.AppendLine("Metric,Value");
            sb.AppendLine($"WindowStartUtc,{snapshot.WindowStartUtc:O}");
            sb.AppendLine($"WindowEndUtc,{snapshot.WindowEndUtc:O}");
            sb.AppendLine($"TranslationRequestedCount,{snapshot.TranslationRequestedCount}");
            sb.AppendLine($"TranslationCompletedCount,{snapshot.TranslationCompletedCount}");
            sb.AppendLine($"PasteCompletedCount,{snapshot.PasteCompletedCount}");
            sb.AppendLine($"PasteRevertedCount,{snapshot.PasteRevertedCount}");
            sb.AppendLine($"GuardrailBlockedCount,{snapshot.GuardrailBlockedCount}");
            sb.AppendLine($"GuardrailOverriddenCount,{snapshot.GuardrailOverriddenCount}");
            sb.AppendLine($"TotalGlossaryHits,{snapshot.TotalGlossaryHits}");
            sb.AppendLine($"WorkflowCompletionRate,{Format(snapshot.WorkflowCompletionRate)}");
            sb.AppendLine($"GlossaryReuseRate,{Format(snapshot.GlossaryReuseRate)}");
            sb.AppendLine($"ViolationRate,{Format(snapshot.ViolationRate)}");
            sb.AppendLine($"P50LatencyMs,{Format(snapshot.P50LatencyMs)}");
            sb.AppendLine($"P95LatencyMs,{Format(snapshot.P95LatencyMs)}");

            string fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, sb.ToString());
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private static string BuildIngestionKey(NicheTelemetryEvent telemetryEvent)
        {
            return string.Join("|",
                telemetryEvent.CapturedAtUtc.ToUniversalTime().ToString("O"),
                telemetryEvent.EventType,
                telemetryEvent.DomainVertical,
                telemetryEvent.SegmentHash,
                telemetryEvent.Success,
                telemetryEvent.LatencyMs.ToString("F3", CultureInfo.InvariantCulture),
                telemetryEvent.BlockedCount,
                telemetryEvent.OverrideCount,
                telemetryEvent.GlossaryHitCount);
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            if (sortedValues.Count == 1) return sortedValues[0];

            double index = (sortedValues.Count - 1) * percentile;
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return sortedValues[lower];
            double weight = index - lower;
            return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * weight;
        }

        private static string Format(double value)
        {
            return value.ToString("F4", CultureInfo.InvariantCulture);
        }
    }
}
