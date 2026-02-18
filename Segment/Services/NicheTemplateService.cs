using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class NicheTemplateService : INicheTemplateService
    {
        private const int CurrentSchemaVersion = 1;
        private readonly string _configPath;
        private readonly IReadOnlyList<NicheProjectTemplate> _builtInTemplates;
        private readonly Dictionary<string, ProjectNicheConfiguration> _projectConfigurations;

        public NicheTemplateService(string? basePath = null)
        {
            string effectiveBasePath = string.IsNullOrWhiteSpace(basePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp")
                : basePath;
            Directory.CreateDirectory(effectiveBasePath);
            _configPath = Path.Combine(effectiveBasePath, "niche-project-configs.json");
            _builtInTemplates = BuildTemplates();
            _projectConfigurations = LoadConfigurations(_configPath);
        }

        public IReadOnlyList<NicheProjectTemplate> GetBuiltInTemplates()
        {
            return _builtInTemplates;
        }

        public ProjectNicheConfiguration CreateProjectFromTemplate(string templateId, string projectProfileName, string targetLanguage)
        {
            NicheProjectTemplate template = _builtInTemplates.FirstOrDefault(x => string.Equals(x.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Template '{templateId}' not found.");

            string profileName = string.IsNullOrWhiteSpace(projectProfileName) ? template.Name : projectProfileName.Trim();
            GlossaryService.GetOrCreateProfile(profileName);

            var terms = template.StarterGlossaryTerms
                .Where(IsValidTerm)
                .Select(x => CloneTerm(x, template.Domain, targetLanguage))
                .ToList();
            GlossaryService.AddTerms(terms, isGlobal: false);

            var config = new ProjectNicheConfiguration
            {
                ProjectProfileName = profileName,
                Domain = template.Domain,
                StyleHints = template.StyleHints.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                EnabledQaChecks = template.EnabledQaChecks.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
            };

            UpsertConfiguration(config);
            SettingsService.Current.ActiveDomain = template.Domain.ToString();
            SettingsService.Save();
            return config;
        }

        public void ExportPack(string filePath, string projectProfileName, string exportedByUserId, string packName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            string profileName = string.IsNullOrWhiteSpace(projectProfileName)
                ? GlossaryService.CurrentProfile.Name
                : projectProfileName.Trim();
            GlossaryService.GetOrCreateProfile(profileName);

            if (!TryGetProjectConfiguration(profileName, out ProjectNicheConfiguration config))
            {
                DomainVertical inferredDomain = Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsed)
                    ? parsed
                    : DomainVertical.Legal;
                config = new ProjectNicheConfiguration
                {
                    ProjectProfileName = profileName,
                    Domain = inferredDomain,
                    StyleHints = new DomainProfileService().GetProfile(inferredDomain).DefaultStyleHints.ToList(),
                    EnabledQaChecks = new DomainQaPluginConfiguration().GetEnabledPluginIds(inferredDomain).ToList()
                };
            }

            var pack = new NichePackDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Metadata = new NichePackMetadata
                {
                    PackId = Guid.NewGuid().ToString("N"),
                    PackName = string.IsNullOrWhiteSpace(packName) ? $"{profileName} Niche Pack" : packName.Trim(),
                    ExportedByUserId = exportedByUserId ?? "",
                    ExportedAtUtc = DateTime.UtcNow
                },
                Domain = config.Domain,
                SourceLanguage = "English",
                TargetLanguage = SettingsService.Current.TargetLanguage,
                StyleHints = config.StyleHints,
                EnabledQaChecks = config.EnabledQaChecks,
                GlossaryTerms = GlossaryService.CurrentProfile.Terms.FindAll()
                    .Where(IsValidTerm)
                    .Select(x => CloneTerm(x, config.Domain, SettingsService.Current.TargetLanguage))
                    .ToList()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            File.WriteAllText(filePath, SerializePack(pack));
        }

        public NichePackImportResult ImportPack(string filePath, string projectProfileName, string targetLanguage, NichePackConflictMode conflictMode)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Niche pack file not found.", filePath);
            }

            NichePackDocument pack = DeserializePack(File.ReadAllText(filePath));
            ValidatePack(pack);

            string profileName = string.IsNullOrWhiteSpace(projectProfileName) ? "Imported Niche Project" : projectProfileName.Trim();
            GlossaryService.GetOrCreateProfile(profileName);

            int duplicateConflictCount = 0;
            var deduplicated = DeduplicateTerms(pack.GlossaryTerms, conflictMode, ref duplicateConflictCount);

            int inserted = 0;
            int updated = 0;
            int skipped = 0;
            var toUpsert = new List<TermEntry>();
            foreach (TermEntry candidate in deduplicated)
            {
                var existing = GlossaryService.CurrentProfile.Terms.FindById(candidate.Source);
                if (existing == null)
                {
                    inserted++;
                    toUpsert.Add(CloneTerm(candidate, pack.Domain, targetLanguage));
                    continue;
                }

                if (conflictMode == NichePackConflictMode.KeepExisting)
                {
                    skipped++;
                    duplicateConflictCount++;
                    continue;
                }

                if (string.Equals(existing.Target, candidate.Target, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                updated++;
                duplicateConflictCount++;
                toUpsert.Add(CloneTerm(candidate, pack.Domain, targetLanguage));
            }

            if (toUpsert.Count > 0)
            {
                GlossaryService.AddTerms(toUpsert, isGlobal: false);
            }

            var config = new ProjectNicheConfiguration
            {
                ProjectProfileName = profileName,
                Domain = pack.Domain,
                StyleHints = pack.StyleHints.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                EnabledQaChecks = pack.EnabledQaChecks.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
            };

            UpsertConfiguration(config);
            SettingsService.Current.ActiveDomain = pack.Domain.ToString();
            SettingsService.Save();

            return new NichePackImportResult
            {
                InsertedTermCount = inserted,
                UpdatedTermCount = updated,
                SkippedTermCount = skipped,
                DuplicateConflictCount = duplicateConflictCount,
                Domain = pack.Domain,
                TargetProfileName = profileName,
                Metadata = pack.Metadata
            };
        }

        public bool TryGetProjectConfiguration(string projectProfileName, out ProjectNicheConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(projectProfileName))
            {
                configuration = new ProjectNicheConfiguration();
                return false;
            }

            return _projectConfigurations.TryGetValue(projectProfileName.Trim(), out configuration!);
        }

        public void SaveProjectConfiguration(ProjectNicheConfiguration configuration)
        {
            if (configuration == null || string.IsNullOrWhiteSpace(configuration.ProjectProfileName))
            {
                throw new ArgumentException("Project configuration is required.", nameof(configuration));
            }

            UpsertConfiguration(configuration);
        }

        public string SerializePack(NichePackDocument pack)
        {
            return JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
        }

        public NichePackDocument DeserializePack(string json)
        {
            var pack = JsonSerializer.Deserialize<NichePackDocument>(json ?? string.Empty)
                ?? throw new InvalidOperationException("Niche pack could not be parsed.");
            ValidatePack(pack);
            return pack;
        }

        private void UpsertConfiguration(ProjectNicheConfiguration config)
        {
            _projectConfigurations[config.ProjectProfileName] = config;
            PersistConfigurations(_configPath, _projectConfigurations.Values);
        }

        private static Dictionary<string, ProjectNicheConfiguration> LoadConfigurations(string path)
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, ProjectNicheConfiguration>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var items = JsonSerializer.Deserialize<List<ProjectNicheConfiguration>>(File.ReadAllText(path)) ?? new List<ProjectNicheConfiguration>();
                return items
                    .Where(x => !string.IsNullOrWhiteSpace(x.ProjectProfileName))
                    .GroupBy(x => x.ProjectProfileName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, ProjectNicheConfiguration>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void PersistConfigurations(string path, IEnumerable<ProjectNicheConfiguration> configurations)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var ordered = configurations
                .OrderBy(x => x.ProjectProfileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            File.WriteAllText(path, JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void ValidatePack(NichePackDocument pack)
        {
            if (pack.SchemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Unsupported niche pack schema version: {pack.SchemaVersion}.");
            }

            if (pack.Metadata == null || string.IsNullOrWhiteSpace(pack.Metadata.PackName))
            {
                throw new InvalidOperationException("Pack metadata is invalid.");
            }

            if (!Enum.IsDefined(typeof(DomainVertical), pack.Domain))
            {
                throw new InvalidOperationException($"Invalid domain: {pack.Domain}.");
            }

            if (pack.GlossaryTerms == null)
            {
                throw new InvalidOperationException("Glossary terms are missing.");
            }

            foreach (var term in pack.GlossaryTerms)
            {
                if (!IsValidTerm(term))
                {
                    throw new InvalidOperationException("Pack contains invalid glossary terms.");
                }
            }
        }

        private static List<TermEntry> DeduplicateTerms(IReadOnlyList<TermEntry> terms, NichePackConflictMode conflictMode, ref int duplicateConflictCount)
        {
            var grouped = (terms ?? new List<TermEntry>())
                .Where(IsValidTerm)
                .GroupBy(x => x.Source.Trim(), StringComparer.OrdinalIgnoreCase);
            var deduplicated = new List<TermEntry>();

            foreach (var group in grouped)
            {
                if (group.Count() > 1)
                {
                    duplicateConflictCount += group.Count() - 1;
                }

                TermEntry winner = conflictMode == NichePackConflictMode.OverwriteExisting
                    ? group.Last()
                    : group.First();
                deduplicated.Add(winner);
            }

            return deduplicated;
        }

        private static bool IsValidTerm(TermEntry? term)
        {
            return term != null
                && !string.IsNullOrWhiteSpace(term.Source)
                && !string.IsNullOrWhiteSpace(term.Target);
        }

        private static TermEntry CloneTerm(TermEntry term, DomainVertical domain, string targetLanguage)
        {
            return new TermEntry
            {
                Source = term.Source.Trim(),
                Target = term.Target.Trim(),
                Context = term.Context ?? "",
                Pos = term.Pos ?? "",
                CreatedBy = string.IsNullOrWhiteSpace(term.CreatedBy) ? "niche-template" : term.CreatedBy,
                CreatedAt = term.CreatedAt == default ? DateTime.UtcNow : term.CreatedAt,
                LastUsed = DateTime.UtcNow,
                UsageCount = Math.Max(1, term.UsageCount),
                IsUserConfirmed = term.IsUserConfirmed,
                DomainVertical = domain,
                SourceLanguage = string.IsNullOrWhiteSpace(term.SourceLanguage) ? "English" : term.SourceLanguage,
                TargetLanguage = string.IsNullOrWhiteSpace(term.TargetLanguage) ? targetLanguage : term.TargetLanguage,
                ScopeType = GlossaryScopeType.Project,
                ScopeOwnerId = GlossaryService.CurrentProfile.Name,
                Priority = term.Priority,
                LastAcceptedAt = term.LastAcceptedAt ?? DateTime.UtcNow
            };
        }

        private static IReadOnlyList<NicheProjectTemplate> BuildTemplates()
        {
            return new List<NicheProjectTemplate>
            {
                BuildTemplate("legal", "Legal", DomainVertical.Legal, new[] { LegalDomainQaPlugin.Id }, new[] { "formal_register", "clause_integrity", "defined_term_consistency" }, new[] { ("shall", "zorundadir"), ("party", "taraf"), ("governing law", "uygulanacak hukuk") }),
                BuildTemplate("patent", "Patent", DomainVertical.Patent, Array.Empty<string>(), new[] { "claim_scope_precision", "technical_consistency", "no_interpretive_expansion" }, new[] { ("claim", "istem"), ("embodiment", "uygulama bicimi"), ("prior art", "bilinen teknik") }),
                BuildTemplate("medical", "Medical", DomainVertical.Medical, new[] { MedicalDomainQaPlugin.Id }, new[] { "safety_first_clarity", "clinical_tone", "unit_integrity" }, new[] { ("adverse event", "advers olay"), ("dosage", "doz"), ("contraindication", "kontrendikasyon") }),
                BuildTemplate("financial", "Financial", DomainVertical.Financial, new[] { FinancialDomainQaPlugin.Id }, new[] { "audit_ready_language", "numeric_precision", "disclosure_consistency" }, new[] { ("net income", "net gelir"), ("gross margin", "brut marj"), ("liability", "yukumluluk") }),
                BuildTemplate("game-localization", "Game Localization", DomainVertical.GameLocalization, Array.Empty<string>(), new[] { "character_voice", "lore_consistency", "ui_brevity" }, new[] { ("quest", "gorev"), ("skill tree", "yetenek agaci"), ("cooldown", "bekleme suresi") }),
                BuildTemplate("subtitling", "Subtitling", DomainVertical.Subtitling, new[] { SubtitlingDomainQaPlugin.Id }, new[] { "brevity", "line_break_discipline", "readability" }, new[] { ("episode", "bolum"), ("subtitle", "altyazi"), ("narrator", "anlatici") }),
                BuildTemplate("ecommerce", "E-commerce", DomainVertical.Ecommerce, Array.Empty<string>(), new[] { "conversion_clarity", "attribute_accuracy", "brand_voice" }, new[] { ("free shipping", "ucretsiz kargo"), ("in stock", "stokta"), ("return policy", "iade politikasi") }),
                BuildTemplate("customer-support", "Customer Support", DomainVertical.CustomerSupport, Array.Empty<string>(), new[] { "empathy", "clear_next_step", "resolution_focused" }, new[] { ("ticket", "destek kaydi"), ("escalation", "ust seviyeye aktarim"), ("service outage", "hizmet kesintisi") })
            };
        }

        private static NicheProjectTemplate BuildTemplate(
            string id,
            string name,
            DomainVertical domain,
            IReadOnlyList<string> qaChecks,
            IReadOnlyList<string> styleHints,
            IReadOnlyList<(string Source, string Target)> terms)
        {
            return new NicheProjectTemplate
            {
                TemplateId = id,
                Name = name,
                Domain = domain,
                EnabledQaChecks = qaChecks,
                StyleHints = styleHints,
                StarterGlossaryTerms = terms.Select(x => new TermEntry
                {
                    Source = x.Source,
                    Target = x.Target,
                    CreatedBy = "segment-builtin-template",
                    SourceLanguage = "English",
                    ScopeType = GlossaryScopeType.Project
                }).ToList()
            };
        }
    }
}
