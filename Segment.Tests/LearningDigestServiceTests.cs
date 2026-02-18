using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class LearningDigestServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly LearningDigestService _service;

        public LearningDigestServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentLearningDigestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _service = new LearningDigestService();
        }

        private void ResetStore()
        {
            GlossaryService.InitializeForTests(_basePath);
            GlossaryService.GetOrCreateProfile("Default");
        }

        [Fact]
        public void BuildWeeklyDigest_Should_Count_Learned_Terms_And_Unresolved_Conflicts()
        {
            ResetStore();
            DateTime now = new DateTime(2026, 2, 18, 12, 0, 0, DateTimeKind.Utc);

            GlossaryService.RecordUsage(new TermUsageLogRecord
            {
                CapturedAtUtc = now.AddDays(-1),
                ScopeName = "Default",
                Source = "agreement",
                Action = "learning_saved_project",
                Success = true,
                Metadata = "{}"
            });

            GlossaryService.RecordUsage(new TermUsageLogRecord
            {
                CapturedAtUtc = now.AddDays(-2),
                ScopeName = "Global",
                Source = "notice",
                Action = "learning_saved_global",
                Success = true,
                Metadata = "{}"
            });

            GlossaryService.RecordResolutionConflict(new GlossaryResolutionConflictRecord
            {
                CapturedAtUtc = now.AddDays(-1),
                SourceTerm = "governing law",
                WinnerTarget = string.Empty,
                WinnerReason = "learning_conflict_unresolved"
            });

            var digest = _service.BuildWeeklyDigest(now);

            digest.TermsLearned.Should().Be(2);
            digest.UnresolvedConflicts.Should().Be(1);
            digest.LearnedTerms.Should().Contain(new[] { "agreement", "notice" });
        }

        [Fact]
        public void BuildWeeklyDigest_Should_Ignore_Events_Outside_Seven_Days()
        {
            ResetStore();
            DateTime now = new DateTime(2026, 2, 18, 12, 0, 0, DateTimeKind.Utc);

            GlossaryService.RecordUsage(new TermUsageLogRecord
            {
                CapturedAtUtc = now.AddDays(-10),
                ScopeName = "Default",
                Source = "legacy",
                Action = "learning_saved_project",
                Success = true,
                Metadata = "{}"
            });

            var digest = _service.BuildWeeklyDigest(now);

            digest.TermsLearned.Should().Be(0);
            digest.UnresolvedConflicts.Should().Be(0);
        }

        public void Dispose()
        {
            GlossaryService.DisposeForTests();
            try
            {
                if (Directory.Exists(_basePath))
                {
                    Directory.Delete(_basePath, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
