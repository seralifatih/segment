using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class GlossaryProfile
    {
        public string Name { get; set; } = "Default";
        public ITermCollection Terms { get; set; } = new NullTermCollection();
        public bool IsFrozen { get; set; }

        private sealed class NullTermCollection : ITermCollection
        {
            public IEnumerable<TermEntry> FindAll() => Array.Empty<TermEntry>();
            public TermEntry? FindById(string source) => null;
            public bool Upsert(TermEntry entry) => false;
            public bool Delete(string source) => false;
            public int Count() => 0;
        }
    }

    public static class GlossaryService
    {
        private const string GlobalProfileName = "Global";
        private static readonly object SyncRoot = new();
        private static readonly StructuredLogger Logger = new StructuredLogger();

        private static Dictionary<string, TermEntry>? _cachedEffectiveTerms;

        private static string DefaultBasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
        private static string? _basePathOverride;
        private static string BasePath => _basePathOverride ?? DefaultBasePath;

        private static IGlossaryStore? _store;

        public static GlossaryProfile GlobalProfile { get; private set; } = new() { Name = GlobalProfileName };
        public static GlossaryProfile CurrentProfile { get; private set; } = new() { Name = "Default" };
        public static List<GlossaryProfile> Profiles { get; private set; } = new();

        static GlossaryService()
        {
            Initialize();
        }

        private static void Initialize()
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(BasePath);
                _store?.Dispose();
                _store = new GlossarySqliteStore(BasePath, Logger);

                LoadGlobalProfile();
                LoadProjectProfiles();
            }
        }

        private static void LoadGlobalProfile()
        {
            EnsureStore().UpsertScope(GlobalProfileName, isGlobal: true, isFrozen: false);
            GlobalProfile = new GlossaryProfile
            {
                Name = GlobalProfileName,
                IsFrozen = false,
                Terms = new StoreBackedTermCollection(EnsureStore(), GlobalProfileName)
            };
        }

        public static void RemoveTerm(string sourceLemma, bool isGlobal)
        {
            lock (SyncRoot)
            {
                var targetProfile = isGlobal ? GlobalProfile : CurrentProfile;
                targetProfile.Terms.Delete(sourceLemma);
                _cachedEffectiveTerms = null;
            }
        }

        public static void LoadProjectProfiles()
        {
            lock (SyncRoot)
            {
                var scopes = EnsureStore().GetScopes();
                Profiles = scopes
                    .Where(x => !x.IsGlobal)
                    .Select(x => new GlossaryProfile
                    {
                        Name = x.Name,
                        IsFrozen = x.IsFrozen,
                        Terms = new StoreBackedTermCollection(EnsureStore(), x.Name)
                    })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (Profiles.Count == 0)
                {
                    GetOrCreateProfile("Default");
                }
                else
                {
                    CurrentProfile = Profiles[0];
                }

                _cachedEffectiveTerms = null;
            }
        }

        public static void GetOrCreateProfile(string name)
        {
            lock (SyncRoot)
            {
                string safeName = string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();

                var existing = Profiles.FirstOrDefault(p => p.Name.Equals(safeName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    CurrentProfile = existing;
                }
                else
                {
                    var newProfile = new GlossaryProfile
                    {
                        Name = safeName,
                        Terms = new StoreBackedTermCollection(EnsureStore(), safeName)
                    };
                    Profiles.Add(newProfile);
                    CurrentProfile = newProfile;
                    SaveProfile(newProfile);
                }

                _cachedEffectiveTerms = null;
            }
        }

        public static void SaveProfile(GlossaryProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name)) return;
            lock (SyncRoot)
            {
                bool isGlobal = string.Equals(profile.Name, GlobalProfileName, StringComparison.OrdinalIgnoreCase);
                EnsureStore().UpsertScope(profile.Name.Trim(), isGlobal, profile.IsFrozen);
            }
        }

        public static bool AddTerm(string sourceLemma, string targetLemma, bool isGlobal)
        {
            lock (SyncRoot)
            {
                var targetProfile = isGlobal ? GlobalProfile : CurrentProfile;
                var existing = targetProfile.Terms.FindById(sourceLemma);
                bool isNew = existing == null;

                var entry = existing ?? new TermEntry { Source = sourceLemma, CreatedAt = DateTime.UtcNow };
                entry.Source = sourceLemma;
                entry.Target = targetLemma;
                entry.LastUsed = DateTime.UtcNow;
                entry.UsageCount = (existing?.UsageCount ?? 0) + 1;
                entry.Context = isGlobal ? "global" : targetProfile.Name;
                entry.ScopeType = InferScopeType(targetProfile.Name, isGlobal, entry.ScopeType);
                entry.ScopeOwnerId = InferScopeOwnerId(targetProfile.Name, isGlobal, entry.ScopeOwnerId);
                entry.DomainVertical = InferDomain(entry.DomainVertical);
                entry.SourceLanguage = string.IsNullOrWhiteSpace(entry.SourceLanguage) ? "English" : entry.SourceLanguage.Trim();
                entry.TargetLanguage = string.IsNullOrWhiteSpace(entry.TargetLanguage) ? SettingsService.Current.TargetLanguage : entry.TargetLanguage.Trim();
                entry.LastAcceptedAt = DateTime.UtcNow;

                targetProfile.Terms.Upsert(entry);

                if (!isNew && !string.Equals(existing?.Target, targetLemma, StringComparison.OrdinalIgnoreCase))
                {
                    ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                    {
                        EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                        AccountId = SettingsService.Current.AccountId,
                        Decision = "overwrite_single",
                        ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                        ProviderRoute = SettingsService.Current.AiProvider,
                        RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                        Details = $"Term '{sourceLemma}' updated in {(isGlobal ? "global" : "project")} scope.",
                        Metadata = new Dictionary<string, string>
                        {
                            ["term"] = sourceLemma,
                            ["existing_target"] = existing?.Target ?? "",
                            ["new_target"] = targetLemma,
                            ["scope"] = isGlobal ? "global" : "project"
                        }
                    });
                }

                _cachedEffectiveTerms = null;
                return isNew;
            }
        }

        public static Dictionary<string, TermEntry> GetEffectiveTerms()
        {
            lock (SyncRoot)
            {
                if (_cachedEffectiveTerms != null)
                {
                    return _cachedEffectiveTerms;
                }

                var effective = new Dictionary<string, TermEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in GlobalProfile.Terms.FindAll())
                {
                    if (!string.IsNullOrWhiteSpace(entry.Source))
                    {
                        effective[entry.Source] = entry;
                    }
                }

                foreach (var entry in CurrentProfile.Terms.FindAll())
                {
                    if (!string.IsNullOrWhiteSpace(entry.Source))
                    {
                        effective[entry.Source] = entry;
                    }
                }

                _cachedEffectiveTerms = effective;
                return effective;
            }
        }

        public static int AddTerms(IEnumerable<TermEntry> terms, bool isGlobal)
        {
            if (terms == null) return 0;

            lock (SyncRoot)
            {
                var targetProfile = isGlobal ? GlobalProfile : CurrentProfile;
                int count = 0;

                foreach (var entry in terms)
                {
                    if (entry == null) continue;
                    if (string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Target)) continue;

                    entry.Source = entry.Source.Trim();
                    entry.Target = entry.Target.Trim();
                    if (string.IsNullOrWhiteSpace(entry.Context))
                    {
                        entry.Context = isGlobal ? "global" : targetProfile.Name;
                    }

                    entry.ScopeType = InferScopeType(targetProfile.Name, isGlobal, entry.ScopeType);
                    entry.ScopeOwnerId = InferScopeOwnerId(targetProfile.Name, isGlobal, entry.ScopeOwnerId);
                    entry.DomainVertical = InferDomain(entry.DomainVertical);
                    entry.SourceLanguage = string.IsNullOrWhiteSpace(entry.SourceLanguage) ? "English" : entry.SourceLanguage.Trim();
                    entry.TargetLanguage = string.IsNullOrWhiteSpace(entry.TargetLanguage) ? SettingsService.Current.TargetLanguage : entry.TargetLanguage.Trim();
                    entry.LastAcceptedAt ??= DateTime.UtcNow;

                    var existing = targetProfile.Terms.FindById(entry.Source);
                    if (existing != null && !string.Equals(existing.Target, entry.Target, StringComparison.OrdinalIgnoreCase))
                    {
                        ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                        {
                            EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                            AccountId = SettingsService.Current.AccountId,
                            Decision = "overwrite_bulk_import",
                            ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                            ProviderRoute = SettingsService.Current.AiProvider,
                            RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                            Details = $"Imported term conflict resolved by overwrite for '{entry.Source}'.",
                            Metadata = new Dictionary<string, string>
                            {
                                ["term"] = entry.Source,
                                ["existing_target"] = existing.Target ?? "",
                                ["new_target"] = entry.Target ?? "",
                                ["scope"] = isGlobal ? "global" : "project"
                            }
                        });
                    }

                    targetProfile.Terms.Upsert(entry);
                    count++;
                }

                _cachedEffectiveTerms = null;
                return count;
            }
        }

        internal static string GetTermsCollectionName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = "default";
            }

            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(profileName));
            var hexHash = BitConverter.ToString(hashBytes)
                .Replace("-", "")
                .ToLowerInvariant();
            return $"terms_{hexHash}";
        }

        public static IReadOnlyList<TermEntry> GetAllTermsForResolution()
        {
            lock (SyncRoot)
            {
                var all = new List<TermEntry>();

                foreach (var entry in GlobalProfile.Terms.FindAll())
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Source)) continue;
                    all.Add(WithInferredMetadata(entry, GlobalProfile.Name, isGlobal: true));
                }

                foreach (var profile in Profiles)
                {
                    foreach (var entry in profile.Terms.FindAll())
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.Source)) continue;
                        all.Add(WithInferredMetadata(entry, profile.Name, isGlobal: false));
                    }
                }

                return all;
            }
        }

        public static void RecordResolutionConflict(GlossaryResolutionConflictRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            lock (SyncRoot)
            {
                record.CapturedAtUtc = record.CapturedAtUtc == default ? DateTime.UtcNow : record.CapturedAtUtc.ToUniversalTime();
                EnsureStore().RecordConflict(record);
            }
        }

        public static IReadOnlyList<GlossaryResolutionConflictRecord> GetResolutionConflicts()
        {
            lock (SyncRoot)
            {
                return EnsureStore()
                    .GetConflicts()
                    .OrderBy(x => x.CapturedAtUtc)
                    .ToList();
            }
        }

        public static IReadOnlyList<TermUsageLogRecord> GetUsageLogs(int limit = 200)
        {
            lock (SyncRoot)
            {
                return EnsureStore().GetUsageLogs(limit);
            }
        }

        public static void RecordUsage(TermUsageLogRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            lock (SyncRoot)
            {
                record.CapturedAtUtc = record.CapturedAtUtc == default ? DateTime.UtcNow : record.CapturedAtUtc.ToUniversalTime();
                EnsureStore().RecordUsage(record);
            }
        }

        public static string GetGlossaryVersionToken()
        {
            lock (SyncRoot)
            {
                var effective = GetEffectiveTerms();
                if (effective.Count == 0)
                {
                    return "empty";
                }

                var fingerprint = string.Join("|", effective
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"{x.Key.Trim().ToLowerInvariant()}=>{(x.Value?.Target ?? string.Empty).Trim().ToLowerInvariant()}"));

                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()[..16];
            }
        }

        internal static void InitializeForTests(string basePath)
        {
            lock (SyncRoot)
            {
                _store?.Dispose();
                _store = null;
                _basePathOverride = basePath;
                _cachedEffectiveTerms = null;
                Initialize();
            }
        }

        internal static void DisposeForTests()
        {
            lock (SyncRoot)
            {
                _store?.Dispose();
                _store = null;
                _basePathOverride = null;
                _cachedEffectiveTerms = null;
            }
        }

        private static IGlossaryStore EnsureStore()
        {
            return _store ?? throw new InvalidOperationException("Glossary store is not initialized.");
        }

        private static TermEntry WithInferredMetadata(TermEntry entry, string profileName, bool isGlobal)
        {
            entry.ScopeType = InferScopeType(profileName, isGlobal, entry.ScopeType);
            entry.ScopeOwnerId = InferScopeOwnerId(profileName, isGlobal, entry.ScopeOwnerId);
            entry.DomainVertical = InferDomain(entry.DomainVertical);
            entry.SourceLanguage = string.IsNullOrWhiteSpace(entry.SourceLanguage) ? "English" : entry.SourceLanguage;
            entry.TargetLanguage = string.IsNullOrWhiteSpace(entry.TargetLanguage) ? SettingsService.Current.TargetLanguage : entry.TargetLanguage;
            return entry;
        }

        private static GlossaryScopeType InferScopeType(string profileName, bool isGlobal, GlossaryScopeType existing)
        {
            if (isGlobal)
            {
                return existing == GlossaryScopeType.System
                    ? GlossaryScopeType.System
                    : GlossaryScopeType.User;
            }

            if (existing != GlossaryScopeType.Project)
            {
                return existing;
            }

            if (profileName.StartsWith("system", StringComparison.OrdinalIgnoreCase)) return GlossaryScopeType.System;
            if (profileName.StartsWith("team", StringComparison.OrdinalIgnoreCase)) return GlossaryScopeType.Team;
            if (profileName.StartsWith("session", StringComparison.OrdinalIgnoreCase)) return GlossaryScopeType.Session;
            return GlossaryScopeType.Project;
        }

        private static string InferScopeOwnerId(string profileName, bool isGlobal, string existing)
        {
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            if (isGlobal)
            {
                return SettingsService.Current.AccountId ?? "account-local";
            }

            return profileName;
        }

        private static DomainVertical InferDomain(DomainVertical existing)
        {
            if (Enum.IsDefined(typeof(DomainVertical), existing))
            {
                return existing;
            }

            return Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsed)
                ? parsed
                : DomainVertical.Legal;
        }

        private sealed class StoreBackedTermCollection : ITermCollection
        {
            private readonly IGlossaryStore _store;
            private readonly string _scopeName;

            public StoreBackedTermCollection(IGlossaryStore store, string scopeName)
            {
                _store = store;
                _scopeName = scopeName;
            }

            public IEnumerable<TermEntry> FindAll() => _store.GetTerms(_scopeName);
            public TermEntry? FindById(string source) => _store.FindTerm(_scopeName, source);

            public bool Upsert(TermEntry entry)
            {
                _store.UpsertTerm(_scopeName, entry);
                return true;
            }

            public bool Delete(string source)
            {
                return _store.DeleteTerm(_scopeName, source);
            }

            public int Count()
            {
                return _store.CountTerms(_scopeName);
            }
        }
    }
}
