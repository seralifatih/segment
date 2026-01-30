using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using Segment.App.Models; 
using LiteDB;

namespace Segment.App.Services
{
    // Profil yapımız da artık zengin
    public class GlossaryProfile
    {
        public string Name { get; set; } = "Default";
        // Dictionary yerine LiteDB koleksiyonu
        public ILiteCollection<TermEntry> Terms { get; set; }
        public bool IsFrozen { get; set; } = false;
    }

    public static class GlossaryService
    {
        private const string GlobalProfileName = "Global";
        private static readonly object SyncRoot = new();

        // Read-through cache for GetEffectiveTerms
        private static Dictionary<string, TermEntry>? _cachedEffectiveTerms;

        // ANA PATH: AppData/SegmentApp
        private static string DefaultBasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
        private static string? _basePathOverride;
        private static string BasePath => _basePathOverride ?? DefaultBasePath;

        // KLASÖR AYRIMI (Checklist Madde 3) ✔
        private static string GlobalPath => Path.Combine(BasePath, "Global");
        private static string ProjectsPath => Path.Combine(BasePath, "Projects");
        private static string DatabasePath => Path.Combine(BasePath, "glossary.db");

        private static LiteDatabase? Database;

        public static GlossaryProfile GlobalProfile { get; private set; } = new() { Name = GlobalProfileName };
        public static GlossaryProfile CurrentProfile { get; private set; } = new();
        public static List<GlossaryProfile> Profiles { get; private set; } = new();

        static GlossaryService()
        {
            Initialize();
        }

        private static void Initialize()
        {
            lock (SyncRoot)
            {
                InitializeDirectories();
                Database = new LiteDatabase(DatabasePath);
                MigrateJsonToLiteDbIfNeeded();
                LoadGlobalProfile();
                LoadProjectProfiles();
            }
        }

        private static void InitializeDirectories()
        {
            Directory.CreateDirectory(BasePath);
            Directory.CreateDirectory(GlobalPath);
            Directory.CreateDirectory(ProjectsPath);
        }

        // --- GLOBAL YÖNETİMİ ---
        private static void LoadGlobalProfile()
        {
            GlobalProfile = new GlossaryProfile
            {
                Name = GlobalProfileName,
                Terms = GetTermsCollection(GlobalProfileName)
            };
        }

        public static void RemoveTerm(string sourceLemma, bool isGlobal)
        {
            lock (SyncRoot)
            {
                var targetProfile = isGlobal ? GlobalProfile : CurrentProfile;
                targetProfile.Terms.Delete(sourceLemma);

                // Invalidate cache
                _cachedEffectiveTerms = null;
            }
        }

        // --- PROJECT YÖNETİMİ ---
        public static void LoadProjectProfiles()
        {
            Profiles.Clear();
            foreach (var record in ProfilesCollection.FindAll())
            {
                Profiles.Add(new GlossaryProfile
                {
                    Name = record.Name,
                    IsFrozen = record.IsFrozen,
                    Terms = GetTermsCollection(record.Name)
                });
            }

            if (Profiles.Count == 0) GetOrCreateProfile("Default");
            else CurrentProfile = Profiles.First();

            // Invalidate cache since current profile changed
            _cachedEffectiveTerms = null;
        }

        public static void GetOrCreateProfile(string name)
        {
            var existing = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                CurrentProfile = existing;
            }
            else
            {
                var newProfile = new GlossaryProfile { Name = name };
                newProfile.Terms = GetTermsCollection(newProfile.Name);
                Profiles.Add(newProfile);
                CurrentProfile = newProfile;
                SaveProfile(newProfile);
            }

            // Invalidate cache since current profile changed
            _cachedEffectiveTerms = null;
        }

        public static void SaveProfile(GlossaryProfile profile)
        {
            if (profile == null) return;
            ProfilesCollection.Upsert(new GlossaryProfileRecord
            {
                Name = profile.Name,
                IsFrozen = profile.IsFrozen
            });
        }

        // --- CORE: EKLEME (METADATA DESTEKLİ) ---
        // Geriye bool döner: True=Yeni Eklendi, False=Overwrite Yapıldı (UI uyarısı için)
        public static bool AddTerm(string sourceLemma, string targetLemma, bool isGlobal)
        {
            lock (SyncRoot)
            {
                var targetProfile = isGlobal ? GlobalProfile : CurrentProfile;
                var existing = targetProfile.Terms.FindById(sourceLemma);
                bool isNew = existing == null;

                var entry = existing ?? new TermEntry { Source = sourceLemma, CreatedAt = DateTime.Now };
                entry.Source = sourceLemma;
                entry.Target = targetLemma;
                entry.LastUsed = DateTime.Now;
                entry.UsageCount = (existing?.UsageCount ?? 0) + 1;
                entry.Context = isGlobal ? "global" : targetProfile.Name;

                // LiteDB Upsert
                targetProfile.Terms.Upsert(entry);

                // Invalidate cache
                _cachedEffectiveTerms = null;

                return isNew;
            }
        }

        // --- CORE: EFFECTIVE TERMS (Conflict Resolver) ---
        public static Dictionary<string, TermEntry> GetEffectiveTerms()
        {
            lock (SyncRoot)
            {
                // Return cached result if available
                if (_cachedEffectiveTerms != null)
                {
                    return _cachedEffectiveTerms;
                }

                // Dictionary Key: Source Lemma (agreement)
                var effective = new Dictionary<string, TermEntry>(StringComparer.OrdinalIgnoreCase);

                // 1. Global (Taban)
                foreach (var entry in GlobalProfile.Terms.FindAll())
                {
                    if (!string.IsNullOrWhiteSpace(entry.Source))
                        effective[entry.Source] = entry;
                }

                // 2. Project (Override)
                foreach (var entry in CurrentProfile.Terms.FindAll())
                {
                    // Varsa ezer, yoksa ekler.
                    // Project scope her zaman kazanır.
                    if (!string.IsNullOrWhiteSpace(entry.Source))
                        effective[entry.Source] = entry;
                }

                // Cache the result
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
                        entry.Context = isGlobal ? "global" : targetProfile.Name;

                    targetProfile.Terms.Upsert(entry);
                    count++;
                }

                // Invalidate cache
                _cachedEffectiveTerms = null;

                return count;
            }
        }

        private static ILiteCollection<TermEntry> GetTermsCollection(string profileName)
        {
            if (Database == null) throw new InvalidOperationException("Glossary database is not initialized.");
            var collection = Database.GetCollection<TermEntry>(GetTermsCollectionName(profileName));
            collection.EnsureIndex(x => x.Source, unique: true);
            return collection;
        }

        /// <summary>
        /// Generates a collision-resistant collection name using SHA256 hashing.
        /// Note: Changing this logic will break backward compatibility - existing profiles
        /// will lose their connection to stored terms as collection names will differ.
        /// </summary>
        private static string GetTermsCollectionName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                profileName = "default";

            // Use SHA256 to generate a deterministic, collision-resistant identifier
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(profileName));
                
                // Convert to lowercase hexadecimal string
                var hexHash = BitConverter.ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant();
                
                return $"terms_{hexHash}";
            }
        }

        private static ILiteCollection<GlossaryProfileRecord> ProfilesCollection =>
            Database?.GetCollection<GlossaryProfileRecord>("profiles")
            ?? throw new InvalidOperationException("Glossary database is not initialized.");

        internal static void InitializeForTests(string basePath)
        {
            lock (SyncRoot)
            {
                Database?.Dispose();
                Database = null;
                _basePathOverride = basePath;
                _cachedEffectiveTerms = null; // Invalidate cache for clean test state
                Initialize();
            }
        }

        internal static void DisposeForTests()
        {
            lock (SyncRoot)
            {
                Database?.Dispose();
                Database = null;
                _basePathOverride = null;
                _cachedEffectiveTerms = null; // Clear cache on disposal
            }
        }

        private static void MigrateJsonToLiteDbIfNeeded()
        {
            string globalJsonPath = Path.Combine(GlobalPath, "glossary.json");
            var projectJsonFiles = Directory.Exists(ProjectsPath)
                ? Directory.GetFiles(ProjectsPath, "*.json")
                : Array.Empty<string>();

            bool hasJson = File.Exists(globalJsonPath) || projectJsonFiles.Length > 0;
            if (!hasJson) return;

            bool hasDbData = ProfilesCollection.Count() > 0 || GetTermsCollection(GlobalProfileName).Count() > 0;
            if (hasDbData) return;

            if (File.Exists(globalJsonPath))
            {
                var legacyGlobal = DeserializeLegacyProfile(globalJsonPath);
                if (legacyGlobal?.Terms != null)
                    UpsertLegacyTerms(GlobalProfileName, legacyGlobal.Terms, true);
            }

            foreach (var file in projectJsonFiles)
            {
                var legacyProfile = DeserializeLegacyProfile(file);
                if (legacyProfile == null) continue;

                string profileName = !string.IsNullOrWhiteSpace(legacyProfile.Name)
                    ? legacyProfile.Name
                    : Path.GetFileNameWithoutExtension(file);

                ProfilesCollection.Upsert(new GlossaryProfileRecord
                {
                    Name = profileName,
                    IsFrozen = legacyProfile.IsFrozen
                });

                if (legacyProfile.Terms != null)
                    UpsertLegacyTerms(profileName, legacyProfile.Terms, false);
            }
        }

        private static void UpsertLegacyTerms(string profileName, Dictionary<string, TermEntry> terms, bool isGlobal)
        {
            var collection = GetTermsCollection(profileName);
            foreach (var kvp in terms)
            {
                if (kvp.Value == null) continue;
                kvp.Value.Source = string.IsNullOrWhiteSpace(kvp.Value.Source) ? kvp.Key : kvp.Value.Source;
                if (string.IsNullOrWhiteSpace(kvp.Value.Context))
                    kvp.Value.Context = isGlobal ? "global" : profileName;
                collection.Upsert(kvp.Value);
            }
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

        private class GlossaryProfileRecord
        {
            [BsonId]
            public string Name { get; set; } = "";
            public bool IsFrozen { get; set; }
        }

        private class LegacyGlossaryProfile
        {
            public string Name { get; set; } = "Default";
            public Dictionary<string, TermEntry> Terms { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IsFrozen { get; set; } = false;
        }
    }
}