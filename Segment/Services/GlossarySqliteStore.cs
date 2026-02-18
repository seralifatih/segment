using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using LiteDB;
using Microsoft.Data.Sqlite;
using Segment.App.Models;

namespace Segment.App.Services
{
    public sealed class GlossarySqliteStore : IGlossaryStore
    {
        private readonly object _syncRoot = new();
        private readonly string _basePath;
        private readonly string _dbPath;
        private readonly StructuredLogger _logger;
        private readonly SqliteConnection _connection;

        public int SchemaVersion { get; }
        public GlossaryMigrationReport LastMigrationReport { get; private set; } = new();

        public GlossarySqliteStore(string basePath, StructuredLogger? logger = null)
        {
            _basePath = string.IsNullOrWhiteSpace(basePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp")
                : basePath;
            Directory.CreateDirectory(_basePath);

            _dbPath = Path.Combine(_basePath, "glossary.sqlite");
            _logger = logger ?? new StructuredLogger(_basePath);

            _connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared");
            _connection.Open();
            ConfigureConnection(_connection);

            SchemaVersion = RunSchemaMigrations(_connection);
            EnsureDefaultScopes();
            TryMigrateLegacyStoresIfNeeded();
        }

        public IReadOnlyList<TermScopeRecord> GetScopes()
        {
            lock (_syncRoot)
            {
                var scopes = new List<TermScopeRecord>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
SELECT name, is_global, is_frozen
FROM TermScope
ORDER BY CASE WHEN is_global = 1 THEN 0 ELSE 1 END, name COLLATE NOCASE;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    scopes.Add(new TermScopeRecord
                    {
                        Name = reader.GetString(0),
                        IsGlobal = reader.GetInt64(1) == 1,
                        IsFrozen = reader.GetInt64(2) == 1
                    });
                }

                return scopes;
            }
        }

        public void UpsertScope(string name, bool isGlobal, bool isFrozen)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Scope name is required.", nameof(name));
            }

            lock (_syncRoot)
            {
                using var tx = _connection.BeginTransaction();
                UpsertScopeInternal(name.Trim(), isGlobal, isFrozen, tx);
                tx.Commit();
            }
        }

        public TermEntry? FindTerm(string scopeName, string source)
        {
            if (string.IsNullOrWhiteSpace(scopeName) || string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            lock (_syncRoot)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
SELECT source, target, context, pos, created_by, created_at_utc, last_used_utc, usage_count,
       is_user_confirmed, domain_vertical, source_language, target_language, scope_type,
       scope_owner_id, priority, last_accepted_at_utc
FROM TermEntry
WHERE scope_name = $scope_name AND source_normalized = $source_normalized
LIMIT 1;";
                cmd.Parameters.AddWithValue("$scope_name", scopeName.Trim());
                cmd.Parameters.AddWithValue("$source_normalized", NormalizeSource(source));

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                return ReadTermEntry(reader);
            }
        }

        public IReadOnlyList<TermEntry> GetTerms(string scopeName)
        {
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                return Array.Empty<TermEntry>();
            }

            lock (_syncRoot)
            {
                var terms = new List<TermEntry>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
SELECT source, target, context, pos, created_by, created_at_utc, last_used_utc, usage_count,
       is_user_confirmed, domain_vertical, source_language, target_language, scope_type,
       scope_owner_id, priority, last_accepted_at_utc
FROM TermEntry
WHERE scope_name = $scope_name
ORDER BY source COLLATE NOCASE;";
                cmd.Parameters.AddWithValue("$scope_name", scopeName.Trim());
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    terms.Add(ReadTermEntry(reader));
                }

                return terms;
            }
        }

        public void UpsertTerm(string scopeName, TermEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(scopeName)) throw new ArgumentException("Scope name is required.", nameof(scopeName));
            if (string.IsNullOrWhiteSpace(entry.Source)) throw new ArgumentException("Term source is required.", nameof(entry));

            lock (_syncRoot)
            {
                using var tx = _connection.BeginTransaction();
                string safeScope = scopeName.Trim();
                UpsertScopeInternal(safeScope, IsGlobalScope(safeScope), isFrozen: false, tx);
                UpsertTermInternal(safeScope, entry, tx);
                tx.Commit();
            }
        }

        public bool DeleteTerm(string scopeName, string source)
        {
            if (string.IsNullOrWhiteSpace(scopeName) || string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            lock (_syncRoot)
            {
                using var tx = _connection.BeginTransaction();

                using var cmd = _connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
DELETE FROM TermEntry
WHERE scope_name = $scope_name AND source_normalized = $source_normalized;";
                cmd.Parameters.AddWithValue("$scope_name", scopeName.Trim());
                cmd.Parameters.AddWithValue("$source_normalized", NormalizeSource(source));
                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                {
                    InsertUsageInternal(new TermUsageLogRecord
                    {
                        CapturedAtUtc = DateTime.UtcNow,
                        ScopeName = scopeName.Trim(),
                        Source = source.Trim(),
                        Action = "delete",
                        Success = true,
                        Metadata = "{}"
                    }, tx);
                }

                tx.Commit();
                return rows > 0;
            }
        }

        public int CountTerms(string scopeName)
        {
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                return 0;
            }

            lock (_syncRoot)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM TermEntry WHERE scope_name = $scope_name;";
                cmd.Parameters.AddWithValue("$scope_name", scopeName.Trim());
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public void RecordConflict(GlossaryResolutionConflictRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            lock (_syncRoot)
            {
                using var tx = _connection.BeginTransaction();
                InsertConflictInternal(record, tx);
                tx.Commit();
            }
        }

        public IReadOnlyList<GlossaryResolutionConflictRecord> GetConflicts()
        {
            lock (_syncRoot)
            {
                var records = new List<GlossaryResolutionConflictRecord>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
SELECT conflict_id, captured_at_utc, source_term, domain_vertical, source_language, target_language,
       candidate_count, winner_target, winner_scope_type, winner_priority, winner_reason
FROM TermConflict
ORDER BY captured_at_utc;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new GlossaryResolutionConflictRecord
                    {
                        CapturedAtUtc = ParseUtc(reader.GetString(1)),
                        SourceTerm = reader.GetString(2),
                        DomainVertical = (DomainVertical)reader.GetInt32(3),
                        SourceLanguage = reader.GetString(4),
                        TargetLanguage = reader.GetString(5),
                        CandidateCount = reader.GetInt32(6),
                        WinnerTarget = reader.GetString(7),
                        WinnerScopeType = (GlossaryScopeType)reader.GetInt32(8),
                        WinnerPriority = reader.GetInt32(9),
                        WinnerReason = reader.GetString(10)
                    });
                }

                return records;
            }
        }

        public void RecordUsage(TermUsageLogRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            lock (_syncRoot)
            {
                using var tx = _connection.BeginTransaction();
                InsertUsageInternal(record, tx);
                tx.Commit();
            }
        }

        public IReadOnlyList<TermUsageLogRecord> GetUsageLogs(int limit = 200)
        {
            int safeLimit = Math.Max(1, Math.Min(5000, limit));
            lock (_syncRoot)
            {
                var records = new List<TermUsageLogRecord>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
SELECT usage_id, captured_at_utc, scope_name, source, action, success, metadata
FROM TermUsageLog
ORDER BY usage_id DESC
LIMIT $limit;";
                cmd.Parameters.AddWithValue("$limit", safeLimit);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new TermUsageLogRecord
                    {
                        Id = reader.GetInt64(0),
                        CapturedAtUtc = ParseUtc(reader.GetString(1)),
                        ScopeName = reader.GetString(2),
                        Source = reader.GetString(3),
                        Action = reader.GetString(4),
                        Success = reader.GetInt64(5) == 1,
                        Metadata = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                    });
                }

                return records;
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public static int RunSchemaMigrations(SqliteConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            EnsureSchemaVersionTable(connection);
            int version = ReadCurrentSchemaVersion(connection);

            if (version < 1)
            {
                ApplyVersion1(connection);
                WriteSchemaVersion(connection, 1, "Initial SQLite glossary schema");
                version = 1;
            }

            if (version < 2)
            {
                ApplyVersion2(connection);
                WriteSchemaVersion(connection, 2, "Add term usage log table");
                version = 2;
            }

            return version;
        }

        private void EnsureDefaultScopes()
        {
            lock (_syncRoot)
            {
                using var tx = _connection.BeginTransaction();
                UpsertScopeInternal("Global", isGlobal: true, isFrozen: false, tx);
                UpsertScopeInternal("Default", isGlobal: false, isFrozen: false, tx);
                tx.Commit();
            }
        }

        private void TryMigrateLegacyStoresIfNeeded()
        {
            try
            {
                if (HasExistingSqliteTermData())
                {
                    LastMigrationReport = new GlossaryMigrationReport
                    {
                        MigrationAttempted = false,
                        MigrationSucceeded = true,
                        Source = "sqlite_primary_existing"
                    };
                    return;
                }

                GlossaryMigrationReport report = TryMigrateFromLiteDb();
                if (!report.MigrationSucceeded)
                {
                    report = TryMigrateFromJson();
                }

                if (!report.MigrationAttempted)
                {
                    report = new GlossaryMigrationReport
                    {
                        MigrationAttempted = false,
                        MigrationSucceeded = true,
                        Source = "none"
                    };
                }

                LastMigrationReport = report;
                if (report.MigrationSucceeded)
                {
                    _logger.Info("glossary_storage_migration_success", new Dictionary<string, string>
                    {
                        ["source"] = report.Source,
                        ["profiles"] = report.MigratedProfileCount.ToString(CultureInfo.InvariantCulture),
                        ["terms"] = report.MigratedTermCount.ToString(CultureInfo.InvariantCulture),
                        ["conflicts"] = report.MigratedConflictCount.ToString(CultureInfo.InvariantCulture)
                    });
                }
                else if (report.MigrationAttempted)
                {
                    _logger.Info("glossary_storage_migration_failed", new Dictionary<string, string>
                    {
                        ["source"] = report.Source,
                        ["failure_reason"] = report.FailureReason
                    });
                }
            }
            catch (Exception ex)
            {
                LastMigrationReport = new GlossaryMigrationReport
                {
                    MigrationAttempted = true,
                    MigrationSucceeded = false,
                    Source = "unknown",
                    FailureReason = "unexpected_error"
                };

                _logger.Error("glossary_storage_migration_failed", ex, new Dictionary<string, string>
                {
                    ["source"] = "unknown",
                    ["failure_reason"] = "unexpected_error"
                });
            }
        }

        private GlossaryMigrationReport TryMigrateFromLiteDb()
        {
            string legacyDbPath = Path.Combine(_basePath, "glossary.db");
            if (!File.Exists(legacyDbPath))
            {
                return new GlossaryMigrationReport();
            }

            var report = new GlossaryMigrationReport
            {
                MigrationAttempted = true,
                Source = "litedb"
            };

            try
            {
                using var legacyDb = new LiteDatabase(legacyDbPath);
                var profiles = legacyDb.GetCollection<LegacyProfileRecord>("profiles")
                    .FindAll()
                    .ToList();

                var allScopes = new List<TermScopeRecord>
                {
                    new() { Name = "Global", IsGlobal = true, IsFrozen = false }
                };
                allScopes.AddRange(profiles.Select(x => new TermScopeRecord
                {
                    Name = string.IsNullOrWhiteSpace(x.Name) ? "Default" : x.Name.Trim(),
                    IsGlobal = false,
                    IsFrozen = x.IsFrozen
                }));

                var migratedTerms = new List<(string scope, TermEntry entry)>();
                foreach (var scope in allScopes.DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    string collectionName = GlossaryService.GetTermsCollectionName(scope.Name);
                    var coll = legacyDb.GetCollection<TermEntry>(collectionName);
                    foreach (var entry in coll.FindAll())
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Target))
                        {
                            continue;
                        }

                        migratedTerms.Add((scope.Name, CloneTerm(entry)));
                    }
                }

                var conflicts = legacyDb.GetCollection<GlossaryResolutionConflictRecord>("glossary_resolution_conflicts")
                    .FindAll()
                    .ToList();

                lock (_syncRoot)
                {
                    using var tx = _connection.BeginTransaction();
                    foreach (var scope in allScopes.DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        UpsertScopeInternal(scope.Name, scope.IsGlobal, scope.IsFrozen, tx);
                    }

                    foreach (var item in migratedTerms)
                    {
                        UpsertTermInternal(item.scope, item.entry, tx);
                    }

                    foreach (var conflict in conflicts)
                    {
                        InsertConflictInternal(conflict, tx);
                    }

                    tx.Commit();
                }

                report.MigrationSucceeded = true;
                report.MigratedProfileCount = allScopes.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                report.MigratedTermCount = migratedTerms.Count;
                report.MigratedConflictCount = conflicts.Count;
                return report;
            }
            catch (Exception ex)
            {
                report.MigrationSucceeded = false;
                report.FailureReason = ex.GetType().Name;
                _logger.Error("glossary_storage_migration_failed", ex, new Dictionary<string, string>
                {
                    ["source"] = "litedb",
                    ["failure_reason"] = report.FailureReason
                });
                return report;
            }
        }

        private GlossaryMigrationReport TryMigrateFromJson()
        {
            string globalJsonPath = Path.Combine(_basePath, "Global", "glossary.json");
            string projectsPath = Path.Combine(_basePath, "Projects");
            var projectJsonFiles = Directory.Exists(projectsPath)
                ? Directory.GetFiles(projectsPath, "*.json")
                : Array.Empty<string>();

            if (!File.Exists(globalJsonPath) && projectJsonFiles.Length == 0)
            {
                return new GlossaryMigrationReport();
            }

            var report = new GlossaryMigrationReport
            {
                MigrationAttempted = true,
                Source = "json"
            };

            try
            {
                var scopes = new List<TermScopeRecord>
                {
                    new() { Name = "Global", IsGlobal = true, IsFrozen = false }
                };
                var migratedTerms = new List<(string scope, TermEntry entry)>();

                if (File.Exists(globalJsonPath))
                {
                    LegacyGlossaryProfile? global = DeserializeLegacyProfile(globalJsonPath);
                    if (global?.Terms != null)
                    {
                        foreach (var kvp in global.Terms)
                        {
                            var entry = kvp.Value;
                            if (entry == null) continue;
                            entry.Source = string.IsNullOrWhiteSpace(entry.Source) ? kvp.Key : entry.Source;
                            if (string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Target)) continue;
                            migratedTerms.Add(("Global", CloneTerm(entry)));
                        }
                    }
                }

                foreach (string file in projectJsonFiles)
                {
                    LegacyGlossaryProfile? profile = DeserializeLegacyProfile(file);
                    if (profile == null) continue;

                    string scopeName = string.IsNullOrWhiteSpace(profile.Name)
                        ? Path.GetFileNameWithoutExtension(file)
                        : profile.Name.Trim();
                    scopes.Add(new TermScopeRecord
                    {
                        Name = scopeName,
                        IsGlobal = false,
                        IsFrozen = profile.IsFrozen
                    });

                    foreach (var kvp in profile.Terms ?? new Dictionary<string, TermEntry>(StringComparer.OrdinalIgnoreCase))
                    {
                        var entry = kvp.Value;
                        if (entry == null) continue;
                        entry.Source = string.IsNullOrWhiteSpace(entry.Source) ? kvp.Key : entry.Source;
                        if (string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Target)) continue;
                        migratedTerms.Add((scopeName, CloneTerm(entry)));
                    }
                }

                lock (_syncRoot)
                {
                    using var tx = _connection.BeginTransaction();
                    foreach (var scope in scopes.DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        UpsertScopeInternal(scope.Name, scope.IsGlobal, scope.IsFrozen, tx);
                    }

                    foreach (var item in migratedTerms)
                    {
                        UpsertTermInternal(item.scope, item.entry, tx);
                    }

                    tx.Commit();
                }

                report.MigrationSucceeded = true;
                report.MigratedProfileCount = scopes.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                report.MigratedTermCount = migratedTerms.Count;
                report.MigratedConflictCount = 0;
                return report;
            }
            catch (Exception ex)
            {
                report.MigrationSucceeded = false;
                report.FailureReason = ex.GetType().Name;
                _logger.Error("glossary_storage_migration_failed", ex, new Dictionary<string, string>
                {
                    ["source"] = "json",
                    ["failure_reason"] = report.FailureReason
                });
                return report;
            }
        }

        private bool HasExistingSqliteTermData()
        {
            lock (_syncRoot)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM TermEntry;";
                long count = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                return count > 0;
            }
        }

        private static void ConfigureConnection(SqliteConnection connection)
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = @"
PRAGMA foreign_keys=ON;
PRAGMA journal_mode=WAL;
PRAGMA synchronous=FULL;";
            pragma.ExecuteNonQuery();
        }

        private static void EnsureSchemaVersionTable(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS SchemaVersion
(
    version INTEGER PRIMARY KEY,
    applied_at_utc TEXT NOT NULL,
    note TEXT NOT NULL
);";
            cmd.ExecuteNonQuery();
        }

        private static int ReadCurrentSchemaVersion(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM SchemaVersion;";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static void WriteSchemaVersion(SqliteConnection connection, int version, string note)
        {
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO SchemaVersion(version, applied_at_utc, note)
VALUES($version, $applied_at_utc, $note);";
            cmd.Parameters.AddWithValue("$version", version);
            cmd.Parameters.AddWithValue("$applied_at_utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$note", note);
            cmd.ExecuteNonQuery();
            tx.Commit();
        }

        private static void ApplyVersion1(SqliteConnection connection)
        {
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TermScope
(
    name TEXT PRIMARY KEY COLLATE NOCASE,
    is_global INTEGER NOT NULL,
    is_frozen INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS TermEntry
(
    term_id INTEGER PRIMARY KEY AUTOINCREMENT,
    scope_name TEXT NOT NULL,
    source TEXT NOT NULL,
    source_normalized TEXT NOT NULL,
    target TEXT NOT NULL,
    context TEXT NOT NULL,
    pos TEXT NOT NULL,
    created_by TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    last_used_utc TEXT NOT NULL,
    usage_count INTEGER NOT NULL,
    is_user_confirmed INTEGER NOT NULL,
    domain_vertical INTEGER NOT NULL,
    source_language TEXT NOT NULL,
    target_language TEXT NOT NULL,
    scope_type INTEGER NOT NULL,
    scope_owner_id TEXT NOT NULL,
    priority INTEGER NOT NULL,
    last_accepted_at_utc TEXT,
    FOREIGN KEY(scope_name) REFERENCES TermScope(name) ON DELETE CASCADE,
    UNIQUE(scope_name, source_normalized)
);

CREATE INDEX IF NOT EXISTS idx_term_lookup
ON TermEntry(scope_name, source_normalized);

CREATE INDEX IF NOT EXISTS idx_term_resolution
ON TermEntry(source_normalized, domain_vertical, source_language, target_language);

CREATE TABLE IF NOT EXISTS TermConflict
(
    conflict_id INTEGER PRIMARY KEY AUTOINCREMENT,
    captured_at_utc TEXT NOT NULL,
    source_term TEXT NOT NULL,
    domain_vertical INTEGER NOT NULL,
    source_language TEXT NOT NULL,
    target_language TEXT NOT NULL,
    candidate_count INTEGER NOT NULL,
    winner_target TEXT NOT NULL,
    winner_scope_type INTEGER NOT NULL,
    winner_priority INTEGER NOT NULL,
    winner_reason TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_term_conflict_captured
ON TermConflict(captured_at_utc);";
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        private static void ApplyVersion2(SqliteConnection connection)
        {
            using var tx = connection.BeginTransaction();

            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TermUsageLog
(
    usage_id INTEGER PRIMARY KEY AUTOINCREMENT,
    captured_at_utc TEXT NOT NULL,
    scope_name TEXT NOT NULL,
    source TEXT NOT NULL,
    action TEXT NOT NULL,
    success INTEGER NOT NULL,
    metadata TEXT NOT NULL,
    FOREIGN KEY(scope_name) REFERENCES TermScope(name) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_term_usage_scope_captured
ON TermUsageLog(scope_name, captured_at_utc);

CREATE INDEX IF NOT EXISTS idx_term_usage_source
ON TermUsageLog(source);";
            cmd.ExecuteNonQuery();

            tx.Commit();
        }

        private void UpsertScopeInternal(string name, bool isGlobal, bool isFrozen, SqliteTransaction tx)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO TermScope(name, is_global, is_frozen, created_at_utc, updated_at_utc)
VALUES($name, $is_global, $is_frozen, $created_at_utc, $updated_at_utc)
ON CONFLICT(name) DO UPDATE SET
    is_global = excluded.is_global,
    is_frozen = excluded.is_frozen,
    updated_at_utc = excluded.updated_at_utc;";
            string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$is_global", isGlobal ? 1 : 0);
            cmd.Parameters.AddWithValue("$is_frozen", isFrozen ? 1 : 0);
            cmd.Parameters.AddWithValue("$created_at_utc", now);
            cmd.Parameters.AddWithValue("$updated_at_utc", now);
            cmd.ExecuteNonQuery();
        }

        private void UpsertTermInternal(string scopeName, TermEntry entry, SqliteTransaction tx)
        {
            string source = (entry.Source ?? string.Empty).Trim();
            string target = (entry.Target ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidOperationException("Term source/target cannot be empty.");
            }

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO TermEntry
(scope_name, source, source_normalized, target, context, pos, created_by, created_at_utc,
 last_used_utc, usage_count, is_user_confirmed, domain_vertical, source_language,
 target_language, scope_type, scope_owner_id, priority, last_accepted_at_utc)
VALUES
($scope_name, $source, $source_normalized, $target, $context, $pos, $created_by, $created_at_utc,
 $last_used_utc, $usage_count, $is_user_confirmed, $domain_vertical, $source_language,
 $target_language, $scope_type, $scope_owner_id, $priority, $last_accepted_at_utc)
ON CONFLICT(scope_name, source_normalized) DO UPDATE SET
    source = excluded.source,
    target = excluded.target,
    context = excluded.context,
    pos = excluded.pos,
    created_by = excluded.created_by,
    created_at_utc = excluded.created_at_utc,
    last_used_utc = excluded.last_used_utc,
    usage_count = excluded.usage_count,
    is_user_confirmed = excluded.is_user_confirmed,
    domain_vertical = excluded.domain_vertical,
    source_language = excluded.source_language,
    target_language = excluded.target_language,
    scope_type = excluded.scope_type,
    scope_owner_id = excluded.scope_owner_id,
    priority = excluded.priority,
    last_accepted_at_utc = excluded.last_accepted_at_utc;";

            cmd.Parameters.AddWithValue("$scope_name", scopeName);
            cmd.Parameters.AddWithValue("$source", source);
            cmd.Parameters.AddWithValue("$source_normalized", NormalizeSource(source));
            cmd.Parameters.AddWithValue("$target", target);
            cmd.Parameters.AddWithValue("$context", entry.Context ?? string.Empty);
            cmd.Parameters.AddWithValue("$pos", entry.Pos ?? string.Empty);
            cmd.Parameters.AddWithValue("$created_by", entry.CreatedBy ?? "user");
            cmd.Parameters.AddWithValue("$created_at_utc", ToUtcText(entry.CreatedAt));
            cmd.Parameters.AddWithValue("$last_used_utc", ToUtcText(entry.LastUsed));
            cmd.Parameters.AddWithValue("$usage_count", Math.Max(0, entry.UsageCount));
            cmd.Parameters.AddWithValue("$is_user_confirmed", entry.IsUserConfirmed ? 1 : 0);
            cmd.Parameters.AddWithValue("$domain_vertical", (int)entry.DomainVertical);
            cmd.Parameters.AddWithValue("$source_language", entry.SourceLanguage ?? string.Empty);
            cmd.Parameters.AddWithValue("$target_language", entry.TargetLanguage ?? string.Empty);
            cmd.Parameters.AddWithValue("$scope_type", (int)entry.ScopeType);
            cmd.Parameters.AddWithValue("$scope_owner_id", entry.ScopeOwnerId ?? string.Empty);
            cmd.Parameters.AddWithValue("$priority", entry.Priority);
            cmd.Parameters.AddWithValue("$last_accepted_at_utc", entry.LastAcceptedAt.HasValue ? ToUtcText(entry.LastAcceptedAt.Value) : (object)DBNull.Value);
            cmd.ExecuteNonQuery();

            InsertUsageInternal(new TermUsageLogRecord
            {
                CapturedAtUtc = DateTime.UtcNow,
                ScopeName = scopeName,
                Source = source,
                Action = "upsert",
                Success = true,
                Metadata = "{}"
            }, tx);
        }

        private void InsertConflictInternal(GlossaryResolutionConflictRecord record, SqliteTransaction tx)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO TermConflict
(captured_at_utc, source_term, domain_vertical, source_language, target_language, candidate_count,
 winner_target, winner_scope_type, winner_priority, winner_reason)
VALUES
($captured_at_utc, $source_term, $domain_vertical, $source_language, $target_language, $candidate_count,
 $winner_target, $winner_scope_type, $winner_priority, $winner_reason);";
            cmd.Parameters.AddWithValue("$captured_at_utc", ToUtcText(record.CapturedAtUtc));
            cmd.Parameters.AddWithValue("$source_term", record.SourceTerm ?? string.Empty);
            cmd.Parameters.AddWithValue("$domain_vertical", (int)record.DomainVertical);
            cmd.Parameters.AddWithValue("$source_language", record.SourceLanguage ?? string.Empty);
            cmd.Parameters.AddWithValue("$target_language", record.TargetLanguage ?? string.Empty);
            cmd.Parameters.AddWithValue("$candidate_count", record.CandidateCount);
            cmd.Parameters.AddWithValue("$winner_target", record.WinnerTarget ?? string.Empty);
            cmd.Parameters.AddWithValue("$winner_scope_type", (int)record.WinnerScopeType);
            cmd.Parameters.AddWithValue("$winner_priority", record.WinnerPriority);
            cmd.Parameters.AddWithValue("$winner_reason", record.WinnerReason ?? string.Empty);
            cmd.ExecuteNonQuery();
        }

        private void InsertUsageInternal(TermUsageLogRecord record, SqliteTransaction tx)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO TermUsageLog
(captured_at_utc, scope_name, source, action, success, metadata)
VALUES
($captured_at_utc, $scope_name, $source, $action, $success, $metadata);";
            cmd.Parameters.AddWithValue("$captured_at_utc", ToUtcText(record.CapturedAtUtc));
            cmd.Parameters.AddWithValue("$scope_name", string.IsNullOrWhiteSpace(record.ScopeName) ? "Default" : record.ScopeName.Trim());
            cmd.Parameters.AddWithValue("$source", record.Source?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("$action", record.Action?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("$success", record.Success ? 1 : 0);
            cmd.Parameters.AddWithValue("$metadata", record.Metadata ?? string.Empty);
            cmd.ExecuteNonQuery();
        }

        private static bool IsGlobalScope(string scopeName)
        {
            return string.Equals(scopeName?.Trim(), "Global", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSource(string source)
        {
            return (source ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string ToUtcText(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseUtc(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static TermEntry ReadTermEntry(SqliteDataReader reader)
        {
            return new TermEntry
            {
                Source = reader.GetString(0),
                Target = reader.GetString(1),
                Context = reader.GetString(2),
                Pos = reader.GetString(3),
                CreatedBy = reader.GetString(4),
                CreatedAt = ParseUtc(reader.GetString(5)),
                LastUsed = ParseUtc(reader.GetString(6)),
                UsageCount = reader.GetInt32(7),
                IsUserConfirmed = reader.GetInt64(8) == 1,
                DomainVertical = (DomainVertical)reader.GetInt32(9),
                SourceLanguage = reader.GetString(10),
                TargetLanguage = reader.GetString(11),
                ScopeType = (GlossaryScopeType)reader.GetInt32(12),
                ScopeOwnerId = reader.GetString(13),
                Priority = reader.GetInt32(14),
                LastAcceptedAt = reader.IsDBNull(15) ? null : ParseUtc(reader.GetString(15))
            };
        }

        private static TermEntry CloneTerm(TermEntry entry)
        {
            return new TermEntry
            {
                Source = entry.Source,
                Target = entry.Target,
                Context = entry.Context,
                Pos = entry.Pos,
                CreatedBy = entry.CreatedBy,
                CreatedAt = entry.CreatedAt,
                LastUsed = entry.LastUsed,
                UsageCount = entry.UsageCount,
                IsUserConfirmed = entry.IsUserConfirmed,
                DomainVertical = entry.DomainVertical,
                SourceLanguage = entry.SourceLanguage,
                TargetLanguage = entry.TargetLanguage,
                ScopeType = entry.ScopeType,
                ScopeOwnerId = entry.ScopeOwnerId,
                Priority = entry.Priority,
                LastAcceptedAt = entry.LastAcceptedAt
            };
        }

        private static LegacyGlossaryProfile? DeserializeLegacyProfile(string path)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<LegacyGlossaryProfile>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        private sealed class LegacyProfileRecord
        {
            [BsonId]
            public string Name { get; set; } = string.Empty;
            public bool IsFrozen { get; set; }
        }

        private sealed class LegacyGlossaryProfile
        {
            public string Name { get; set; } = "Default";
            public Dictionary<string, TermEntry> Terms { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IsFrozen { get; set; }
        }
    }
}
