using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class OnboardingMetricsService : IOnboardingMetricsService, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<OnboardingMetricRecord> _collection;

        public OnboardingMetricsService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(resolvedBasePath);

            string dbPath = Path.Combine(resolvedBasePath, "onboarding_metrics.db");
            _database = new LiteDatabase(dbPath);
            _collection = _database.GetCollection<OnboardingMetricRecord>("onboarding_metrics");
            _collection.EnsureIndex(x => x.CreatedAtUtc);
            _collection.EnsureIndex(x => x.LaunchPhase);
            _collection.EnsureIndex(x => x.Outcome);
            _collection.EnsureIndex(x => x.CorrelationId, unique: true);
        }

        public void Record(OnboardingMetricRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(record.CorrelationId))
            {
                record.CorrelationId = Guid.NewGuid().ToString("N");
            }

            int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _collection.Upsert(record);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    Thread.Sleep(25 * attempt);
                }
            }
        }

        public IReadOnlyList<OnboardingMetricRecord> GetRecords(DateTime? fromUtc = null, DateTime? toUtc = null)
        {
            DateTime from = (fromUtc ?? DateTime.MinValue).ToUniversalTime();
            DateTime to = (toUtc ?? DateTime.MaxValue).ToUniversalTime();
            return _collection
                .Find(x => x.CreatedAtUtc >= from && x.CreatedAtUtc <= to)
                .OrderBy(x => x.CreatedAtUtc)
                .ToList();
        }

        public void Dispose()
        {
            _database.Dispose();
        }
    }
}
