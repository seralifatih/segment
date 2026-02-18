using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class GuardrailLatencyBenchmarkTests
    {
        [Fact]
        public void Validate_Latency_For_Short_Segments_Should_Stay_Within_Guardrails()
        {
            var engine = new TranslationGuardrailEngine();
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["governing law"] = "uygulanacak hukuk"
                }
            };

            var samples = new List<double>();
            for (int i = 0; i < 200; i++)
            {
                string source = $"The governing law shall apply to party ACME {i} on 2026-03-01.";
                string target = $"Uygulanacak hukuk ACME {i} tarafi icin 2026-03-01 tarihinde uygulanacaktir.";

                var sw = Stopwatch.StartNew();
                _ = engine.Validate(source, target, context);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }

            samples.Sort();
            double p50 = Percentile(samples, 0.50);
            double p95 = Percentile(samples, 0.95);

            p50.Should().BeLessThan(20);
            p95.Should().BeLessThan(60);
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
    }
}
