using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class GlossarySqliteStoreMigrationTests : IDisposable
    {
        private readonly string _basePath;

        public GlossarySqliteStoreMigrationTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentGlossarySqliteTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
        }

        [Fact]
        public void Should_Migrate_Legacy_Json_On_First_Run()
        {
            string globalPath = Path.Combine(_basePath, "Global");
            string projectsPath = Path.Combine(_basePath, "Projects");
            Directory.CreateDirectory(globalPath);
            Directory.CreateDirectory(projectsPath);

            var globalProfile = new
            {
                Name = "Global",
                Terms = new Dictionary<string, TermEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["agreement"] = new TermEntry { Source = "agreement", Target = "sozlesme", Context = "global" }
                },
                IsFrozen = false
            };
            File.WriteAllText(Path.Combine(globalPath, "glossary.json"), JsonSerializer.Serialize(globalProfile));

            var projectProfile = new
            {
                Name = "ProjectA",
                Terms = new Dictionary<string, TermEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["party"] = new TermEntry { Source = "party", Target = "taraf", Context = "ProjectA" }
                },
                IsFrozen = true
            };
            File.WriteAllText(Path.Combine(projectsPath, "projectA.json"), JsonSerializer.Serialize(projectProfile));

            using var store = new GlossarySqliteStore(_basePath);

            store.LastMigrationReport.MigrationSucceeded.Should().BeTrue();
            store.LastMigrationReport.Source.Should().Be("json");
            store.FindTerm("Global", "agreement")!.Target.Should().Be("sozlesme");
            store.FindTerm("ProjectA", "party")!.Target.Should().Be("taraf");
            store.GetScopes().Should().Contain(x => x.Name == "ProjectA" && x.IsFrozen);
        }

        [Fact]
        public void Should_Upgrade_V1_Schema_To_Current()
        {
            string dbPath = Path.Combine(_basePath, "glossary.sqlite");
            using (var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS SchemaVersion(version INTEGER PRIMARY KEY, applied_at_utc TEXT NOT NULL, note TEXT NOT NULL);
INSERT INTO SchemaVersion(version, applied_at_utc, note) VALUES (1, '2026-01-01T00:00:00.0000000Z', 'legacy_v1');

CREATE TABLE IF NOT EXISTS TermScope(name TEXT PRIMARY KEY COLLATE NOCASE, is_global INTEGER NOT NULL, is_frozen INTEGER NOT NULL DEFAULT 0, created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS TermEntry(term_id INTEGER PRIMARY KEY AUTOINCREMENT, scope_name TEXT NOT NULL, source TEXT NOT NULL, source_normalized TEXT NOT NULL, target TEXT NOT NULL, context TEXT NOT NULL, pos TEXT NOT NULL, created_by TEXT NOT NULL, created_at_utc TEXT NOT NULL, last_used_utc TEXT NOT NULL, usage_count INTEGER NOT NULL, is_user_confirmed INTEGER NOT NULL, domain_vertical INTEGER NOT NULL, source_language TEXT NOT NULL, target_language TEXT NOT NULL, scope_type INTEGER NOT NULL, scope_owner_id TEXT NOT NULL, priority INTEGER NOT NULL, last_accepted_at_utc TEXT, UNIQUE(scope_name, source_normalized));
CREATE TABLE IF NOT EXISTS TermConflict(conflict_id INTEGER PRIMARY KEY AUTOINCREMENT, captured_at_utc TEXT NOT NULL, source_term TEXT NOT NULL, domain_vertical INTEGER NOT NULL, source_language TEXT NOT NULL, target_language TEXT NOT NULL, candidate_count INTEGER NOT NULL, winner_target TEXT NOT NULL, winner_scope_type INTEGER NOT NULL, winner_priority INTEGER NOT NULL, winner_reason TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }

            using var store = new GlossarySqliteStore(_basePath);
            store.SchemaVersion.Should().Be(2);

            using var verifyConn = new SqliteConnection($"Data Source={dbPath};Mode=ReadWrite");
            verifyConn.Open();
            using var verify = verifyConn.CreateCommand();
            verify.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='TermUsageLog';";
            var tableName = verify.ExecuteScalar()?.ToString();
            tableName.Should().Be("TermUsageLog");
        }

        [Fact]
        public void Should_Persist_And_Read_Conflict_Records()
        {
            using var store = new GlossarySqliteStore(_basePath);
            store.RecordConflict(new GlossaryResolutionConflictRecord
            {
                CapturedAtUtc = DateTime.UtcNow,
                SourceTerm = "agreement",
                DomainVertical = DomainVertical.Legal,
                SourceLanguage = "English",
                TargetLanguage = "Turkish",
                CandidateCount = 2,
                WinnerTarget = "sozlesme",
                WinnerScopeType = GlossaryScopeType.Project,
                WinnerPriority = 10,
                WinnerReason = "scope_priority"
            });

            var conflicts = store.GetConflicts();
            conflicts.Should().NotBeEmpty();
            conflicts[^1].SourceTerm.Should().Be("agreement");
            conflicts[^1].WinnerTarget.Should().Be("sozlesme");
        }

        [Fact]
        public void Failed_Upsert_Should_Roll_Back_Transaction()
        {
            using var store = new GlossarySqliteStore(_basePath);
            store.UpsertScope("ProjectA", isGlobal: false, isFrozen: false);
            store.UpsertTerm("ProjectA", new TermEntry { Source = "alpha", Target = "alfa" });

            int before = store.CountTerms("ProjectA");

            Action act = () => store.UpsertTerm("ProjectA", new TermEntry { Source = "broken", Target = "" });
            act.Should().Throw<InvalidOperationException>();

            int after = store.CountTerms("ProjectA");
            after.Should().Be(before);
        }

        public void Dispose()
        {
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
