using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class PmfDashboardService : IPmfDashboardService, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<PmfUsageEvent> _events;

        public PmfDashboardService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "pmf_analytics.db");

            _database = new LiteDatabase(dbPath);
            _events = _database.GetCollection<PmfUsageEvent>("pmf_usage_events");
            _events.EnsureIndex(x => x.CapturedAtUtc);
            _events.EnsureIndex(x => x.UserIdHash);
            _events.EnsureIndex(x => x.IngestionKey, unique: true);
        }

        public void RecordEvent(PmfUsageEvent usageEvent)
        {
            if (usageEvent == null) throw new ArgumentNullException(nameof(usageEvent));
            if (string.IsNullOrWhiteSpace(usageEvent.UserIdHash))
            {
                throw new ArgumentException("UserIdHash is required. Raw IDs are not accepted.", nameof(usageEvent));
            }

            usageEvent.UserIdHash = usageEvent.UserIdHash.Trim();
            usageEvent.SegmentsCompleted = Math.Max(0, usageEvent.SegmentsCompleted);
            usageEvent.GlossarySuggestionsServed = Math.Max(0, usageEvent.GlossarySuggestionsServed);
            usageEvent.GlossarySuggestionsAccepted = Math.Max(0, usageEvent.GlossarySuggestionsAccepted);
            usageEvent.TerminologyViolationCount = Math.Max(0, usageEvent.TerminologyViolationCount);
            usageEvent.LatencyMs = Math.Max(0, usageEvent.LatencyMs);
            usageEvent.CapturedAtUtc = usageEvent.CapturedAtUtc == default
                ? DateTime.UtcNow
                : usageEvent.CapturedAtUtc.ToUniversalTime();
            if (usageEvent.CapturedAtUtc > DateTime.UtcNow.AddMinutes(5))
            {
                usageEvent.CapturedAtUtc = DateTime.UtcNow;
            }

            if (string.IsNullOrWhiteSpace(usageEvent.IngestionKey))
            {
                usageEvent.IngestionKey = BuildIngestionKey(usageEvent);
            }

            int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _events.Upsert(usageEvent);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    Thread.Sleep(20 * attempt);
                }
            }
        }

        public PmfDashboardSnapshot GetDashboardSnapshot(DateTime windowStartUtc, DateTime windowEndUtc)
        {
            DateTime start = windowStartUtc.ToUniversalTime();
            DateTime end = windowEndUtc.ToUniversalTime();
            if (end <= start)
            {
                throw new ArgumentException("Window end must be greater than window start.");
            }

            var inWindow = _events.Find(x => x.CapturedAtUtc >= start && x.CapturedAtUtc < end).ToList();
            int dayCount = Math.Max(1, (int)Math.Ceiling((end - start).TotalDays));

            int dau = inWindow
                .Where(x => x.CapturedAtUtc >= end.AddDays(-1))
                .Select(x => x.UserIdHash)
                .Distinct(StringComparer.Ordinal)
                .Count();

            int wau = inWindow
                .Select(x => x.UserIdHash)
                .Distinct(StringComparer.Ordinal)
                .Count();

            int totalSegments = inWindow.Sum(x => x.SegmentsCompleted);
            int totalGlossaryServed = inWindow.Sum(x => x.GlossarySuggestionsServed);
            int totalGlossaryAccepted = inWindow.Sum(x => x.GlossarySuggestionsAccepted);
            int totalViolations = inWindow.Sum(x => x.TerminologyViolationCount);
            var latencies = inWindow.Select(x => x.LatencyMs).Where(x => x > 0).OrderBy(x => x).ToList();

            double p50 = Percentile(latencies, 0.50);
            double p95 = Percentile(latencies, 0.95);

            var allEventsUntilEnd = _events.Find(x => x.CapturedAtUtc < end).ToList();
            double retentionWeek4 = ComputeWeek4Retention(allEventsUntilEnd, end);
            double pilotToPaid = ComputePilotToPaid(inWindow);
            double churn = ComputeChurnRate(inWindow);

            return new PmfDashboardSnapshot
            {
                WindowStartUtc = start,
                WindowEndUtc = end,
                Dau = dau,
                Wau = wau,
                SegmentsPerDay = dayCount == 0 ? 0 : (double)totalSegments / dayCount,
                RetentionWeek4 = retentionWeek4,
                GlossaryReuseRate = totalGlossaryServed == 0 ? 0 : (double)totalGlossaryAccepted / totalGlossaryServed,
                TerminologyViolationRate = totalSegments == 0 ? 0 : (double)totalViolations / totalSegments,
                P50LatencyMs = p50,
                P95LatencyMs = p95,
                PilotToPaidConversion = pilotToPaid,
                ChurnRate = churn
            };
        }

        public IReadOnlyList<PmfDashboardSnapshot> GetWeeklySnapshots(int weekCount, DateTime? anchorUtc = null)
        {
            int safeCount = Math.Max(1, weekCount);
            DateTime anchor = (anchorUtc ?? DateTime.UtcNow).ToUniversalTime().Date;
            var snapshots = new List<PmfDashboardSnapshot>(safeCount);
            for (int i = 0; i < safeCount; i++)
            {
                DateTime end = anchor.AddDays(-(7 * i));
                DateTime start = end.AddDays(-7);
                snapshots.Add(GetDashboardSnapshot(start, end));
            }

            return snapshots.OrderBy(x => x.WindowStartUtc).ToList();
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private static double ComputeWeek4Retention(IReadOnlyList<PmfUsageEvent> events, DateTime windowEndUtc)
        {
            var byUser = events
                .GroupBy(x => x.UserIdHash)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.CapturedAtUtc).ToList(), StringComparer.Ordinal);

            var eligibleUsers = new List<string>();
            int retained = 0;

            foreach (var pair in byUser)
            {
                var userEvents = pair.Value;
                if (userEvents.Count == 0) continue;
                DateTime first = userEvents[0].CapturedAtUtc;
                if ((windowEndUtc - first).TotalDays < 28)
                {
                    continue;
                }

                eligibleUsers.Add(pair.Key);
                DateTime week4Start = first.AddDays(21);
                DateTime week4End = first.AddDays(35);
                bool activeWeek4 = userEvents.Any(x => x.CapturedAtUtc >= week4Start && x.CapturedAtUtc < week4End);
                if (activeWeek4) retained++;
            }

            return eligibleUsers.Count == 0 ? 0 : (double)retained / eligibleUsers.Count;
        }

        private static double ComputePilotToPaid(IReadOnlyList<PmfUsageEvent> events)
        {
            var pilotUsers = events.Where(x => x.IsPilotUser).Select(x => x.UserIdHash).Distinct(StringComparer.Ordinal).ToList();
            if (pilotUsers.Count == 0) return 0;

            int converted = events
                .Where(x => x.IsPilotUser && x.ConvertedToPaid)
                .Select(x => x.UserIdHash)
                .Distinct(StringComparer.Ordinal)
                .Count();

            return (double)converted / pilotUsers.Count;
        }

        private static double ComputeChurnRate(IReadOnlyList<PmfUsageEvent> events)
        {
            var activeUsers = events.Select(x => x.UserIdHash).Distinct(StringComparer.Ordinal).ToList();
            if (activeUsers.Count == 0) return 0;

            int churned = events
                .Where(x => x.Churned)
                .Select(x => x.UserIdHash)
                .Distinct(StringComparer.Ordinal)
                .Count();

            return (double)churned / activeUsers.Count;
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

        private static string BuildIngestionKey(PmfUsageEvent usageEvent)
        {
            return string.Join("|",
                usageEvent.UserIdHash.Trim(),
                usageEvent.CapturedAtUtc.ToUniversalTime().ToString("O"),
                usageEvent.Segment,
                usageEvent.SegmentsCompleted,
                usageEvent.IsPilotUser,
                usageEvent.ConvertedToPaid,
                usageEvent.Churned);
        }
    }
}
