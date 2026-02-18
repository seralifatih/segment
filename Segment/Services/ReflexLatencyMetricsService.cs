using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public sealed class ReflexLatencyMetricsService
    {
        private static readonly Lazy<ReflexLatencyMetricsService> LazyInstance = new(() => new ReflexLatencyMetricsService());
        public static ReflexLatencyMetricsService Instance => LazyInstance.Value;

        private readonly object _syncRoot = new();
        private readonly Queue<ReflexLatencySample> _samples = new();
        private readonly int _windowSize;
        private readonly StructuredLogger _logger;

        public ReflexLatencyMetricsService(int windowSize = 200, StructuredLogger? logger = null)
        {
            _windowSize = Math.Max(20, windowSize);
            _logger = logger ?? new StructuredLogger();
        }

        public void Record(ReflexLatencySample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            ReflexLatencySnapshot snapshot;
            lock (_syncRoot)
            {
                _samples.Enqueue(sample);
                while (_samples.Count > _windowSize)
                {
                    _samples.Dequeue();
                }

                snapshot = BuildSnapshotInternal();
            }

            _logger.Info("reflex_latency_event", new Dictionary<string, string>
            {
                ["short_segment"] = sample.IsShortSegment.ToString(),
                ["source_length"] = sample.SourceLength.ToString(),
                ["capture_to_request_start_ms"] = sample.CaptureToRequestStartMs.ToString("F2"),
                ["provider_roundtrip_ms"] = sample.ProviderRoundtripMs.ToString("F2"),
                ["response_to_render_ms"] = sample.ResponseToRenderMs.ToString("F2"),
                ["end_to_end_ms"] = sample.EndToEndMs.ToString("F2"),
                ["provider_used"] = sample.ProviderUsed,
                ["used_fallback_provider"] = sample.UsedFallbackProvider.ToString(),
                ["budget_enforced"] = sample.BudgetEnforced.ToString(),
                ["budget_exceeded"] = sample.BudgetExceeded.ToString()
            });

            if (snapshot.SampleCount > 0 && snapshot.SampleCount % 10 == 0)
            {
                _logger.Info("reflex_latency_summary", new Dictionary<string, string>
                {
                    ["window_samples"] = snapshot.SampleCount.ToString(),
                    ["short_samples"] = snapshot.ShortSegmentSampleCount.ToString(),
                    ["end_to_end_p50_ms"] = snapshot.EndToEndP50Ms.ToString("F2"),
                    ["end_to_end_p95_ms"] = snapshot.EndToEndP95Ms.ToString("F2")
                });
            }
        }

        public ReflexLatencySnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                return BuildSnapshotInternal();
            }
        }

        internal void ResetForTests()
        {
            lock (_syncRoot)
            {
                _samples.Clear();
            }
        }

        internal static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0) return 0;
            if (sortedValues.Count == 1) return sortedValues[0];

            double index = (sortedValues.Count - 1) * percentile;
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return sortedValues[lower];

            double weight = index - lower;
            return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * weight;
        }

        private ReflexLatencySnapshot BuildSnapshotInternal()
        {
            var all = _samples.ToList();
            var shortSegments = all.Where(x => x.IsShortSegment).ToList();
            var source = shortSegments.Count > 0 ? shortSegments : all;

            List<double> capture = source.Select(x => x.CaptureToRequestStartMs).OrderBy(x => x).ToList();
            List<double> roundtrip = source.Select(x => x.ProviderRoundtripMs).OrderBy(x => x).ToList();
            List<double> render = source.Select(x => x.ResponseToRenderMs).OrderBy(x => x).ToList();
            List<double> endToEnd = source.Select(x => x.EndToEndMs).OrderBy(x => x).ToList();

            return new ReflexLatencySnapshot
            {
                WindowComputedAtUtc = DateTime.UtcNow,
                SampleCount = all.Count,
                ShortSegmentSampleCount = shortSegments.Count,
                CaptureToRequestStartP50Ms = Percentile(capture, 0.50),
                CaptureToRequestStartP95Ms = Percentile(capture, 0.95),
                ProviderRoundtripP50Ms = Percentile(roundtrip, 0.50),
                ProviderRoundtripP95Ms = Percentile(roundtrip, 0.95),
                ResponseToRenderP50Ms = Percentile(render, 0.50),
                ResponseToRenderP95Ms = Percentile(render, 0.95),
                EndToEndP50Ms = Percentile(endToEnd, 0.50),
                EndToEndP95Ms = Percentile(endToEnd, 0.95)
            };
        }
    }
}
