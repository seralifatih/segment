using System;
using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public sealed class GlossaryMigrationReport
    {
        public bool MigrationAttempted { get; set; }
        public bool MigrationSucceeded { get; set; }
        public string Source { get; set; } = "none";
        public int MigratedProfileCount { get; set; }
        public int MigratedTermCount { get; set; }
        public int MigratedConflictCount { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class TermScopeRecord
    {
        public string Name { get; set; } = "";
        public bool IsGlobal { get; set; }
        public bool IsFrozen { get; set; }
    }

    public interface IGlossaryStore : IDisposable
    {
        int SchemaVersion { get; }
        GlossaryMigrationReport LastMigrationReport { get; }

        IReadOnlyList<TermScopeRecord> GetScopes();
        void UpsertScope(string name, bool isGlobal, bool isFrozen);

        TermEntry? FindTerm(string scopeName, string source);
        IReadOnlyList<TermEntry> GetTerms(string scopeName);
        void UpsertTerm(string scopeName, TermEntry entry);
        bool DeleteTerm(string scopeName, string source);
        int CountTerms(string scopeName);

        void RecordConflict(GlossaryResolutionConflictRecord record);
        IReadOnlyList<GlossaryResolutionConflictRecord> GetConflicts();

        void RecordUsage(TermUsageLogRecord record);
        IReadOnlyList<TermUsageLogRecord> GetUsageLogs(int limit = 200);
    }
}
