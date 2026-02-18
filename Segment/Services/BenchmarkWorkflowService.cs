using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class BenchmarkWorkflowService : IBenchmarkWorkflowService, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<BenchmarkSession> _sessions;
        private readonly IRoiBenchmarkCalculator _calculator;

        public BenchmarkWorkflowService(IRoiBenchmarkCalculator? calculator = null, string? basePath = null)
        {
            _calculator = calculator ?? new RoiBenchmarkCalculator();
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");

            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "pilot_benchmark.db");

            _database = new LiteDatabase(dbPath);
            _sessions = _database.GetCollection<BenchmarkSession>("benchmark_sessions");
            _sessions.EnsureIndex(x => x.PilotName);
            _sessions.EnsureIndex(x => x.CreatedAtUtc);
        }

        public BenchmarkSession StartSession(string pilotName)
        {
            if (string.IsNullOrWhiteSpace(pilotName))
            {
                throw new ArgumentException("Pilot name is required.", nameof(pilotName));
            }

            var session = new BenchmarkSession
            {
                PilotName = pilotName.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _sessions.Insert(session);
            return session;
        }

        public BenchmarkSession CaptureWeek(string sessionId, int weekNumber, BenchmarkPeriodType periodType, IReadOnlyList<BenchmarkSegmentMetric> metrics)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID is required.", nameof(sessionId));
            }

            if (weekNumber < 1 || weekNumber > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(weekNumber), "Week number must be between 1 and 4.");
            }

            ValidateWeekPeriod(weekNumber, periodType);

            var session = GetSession(sessionId);

            var weekCapture = new BenchmarkWeekCapture
            {
                WeekNumber = weekNumber,
                PeriodType = periodType,
                SegmentMetrics = metrics?.ToList() ?? new List<BenchmarkSegmentMetric>()
            };

            session.WeekCaptures.RemoveAll(x => x.WeekNumber == weekNumber);
            session.WeekCaptures.Add(weekCapture);
            session.WeekCaptures = session.WeekCaptures.OrderBy(x => x.WeekNumber).ToList();

            if (HasCompleteWorkflow(session))
            {
                session.FinalReport = _calculator.Calculate(session);
                session.IsCompleted = true;
            }
            else
            {
                session.IsCompleted = false;
            }

            _sessions.Update(session);
            return session;
        }

        public BenchmarkSession GetSession(string sessionId)
        {
            var session = _sessions.FindById(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Benchmark session not found: {sessionId}");
            }

            return session;
        }

        public PilotRoiReport GenerateSummaryReport(string sessionId)
        {
            var session = GetSession(sessionId);
            var report = _calculator.Calculate(session);
            session.FinalReport = report;
            session.IsCompleted = HasCompleteWorkflow(session);
            _sessions.Update(session);
            return report;
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private static void ValidateWeekPeriod(int weekNumber, BenchmarkPeriodType periodType)
        {
            if (weekNumber == 1 && periodType != BenchmarkPeriodType.Baseline)
            {
                throw new InvalidOperationException("Week 1 must be captured as baseline.");
            }

            if (weekNumber >= 2 && weekNumber <= 4 && periodType != BenchmarkPeriodType.SegmentAssisted)
            {
                throw new InvalidOperationException("Weeks 2-4 must be captured as Segment-assisted.");
            }
        }

        private static bool HasCompleteWorkflow(BenchmarkSession session)
        {
            if (session.WeekCaptures.Count < 4) return false;

            var week1 = session.WeekCaptures.FirstOrDefault(x => x.WeekNumber == 1);
            if (week1?.PeriodType != BenchmarkPeriodType.Baseline) return false;

            for (int week = 2; week <= 4; week++)
            {
                var assisted = session.WeekCaptures.FirstOrDefault(x => x.WeekNumber == week);
                if (assisted?.PeriodType != BenchmarkPeriodType.SegmentAssisted)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
