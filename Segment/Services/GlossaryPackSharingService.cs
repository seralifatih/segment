using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class GlossaryPackSharingService : IGlossaryPackSharingService
    {
        private readonly IAttributionAnalyticsService _attributionAnalyticsService;

        public GlossaryPackSharingService(IAttributionAnalyticsService attributionAnalyticsService)
        {
            _attributionAnalyticsService = attributionAnalyticsService;
        }

        public GlossaryPackMetadata ExportLegalGlossaryPack(string filePath, string exportedByUserId, string packName, string referralCode, bool isGlobal = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            var metadata = new GlossaryPackMetadata
            {
                PackId = Guid.NewGuid().ToString("N"),
                PackName = string.IsNullOrWhiteSpace(packName) ? "Legal Glossary Pack" : packName.Trim(),
                ExportedByUserId = exportedByUserId ?? "",
                ExportedAtUtc = DateTime.UtcNow,
                ReferralCode = referralCode ?? ""
            };

            var sourceTerms = isGlobal
                ? GlossaryService.GlobalProfile.Terms.FindAll()
                : GlossaryService.CurrentProfile.Terms.FindAll();

            var legalTerms = sourceTerms
                .Where(x => !string.IsNullOrWhiteSpace(x.Source) && !string.IsNullOrWhiteSpace(x.Target))
                .Select(CloneTerm)
                .ToList();

            var document = new GlossaryPackDocument
            {
                Metadata = metadata,
                Terms = legalTerms
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));

            return metadata;
        }

        public GlossaryPackImportResult ImportGlossaryPack(string filePath, string importedByUserId, bool isGlobal = true)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Glossary pack file not found.", filePath);
            }

            var document = JsonSerializer.Deserialize<GlossaryPackDocument>(File.ReadAllText(filePath))
                ?? throw new InvalidOperationException("Glossary pack content is invalid.");

            int inserted = GlossaryService.AddTerms(document.Terms, isGlobal);

            _attributionAnalyticsService.RecordGlossaryPackImport(new GlossaryPackImportRecord
            {
                PackId = document.Metadata.PackId,
                ImportedByUserId = importedByUserId ?? "",
                ImportedAtUtc = DateTime.UtcNow,
                ExportedByUserId = document.Metadata.ExportedByUserId,
                ReferralCode = document.Metadata.ReferralCode,
                InsertedTermCount = inserted
            });

            return new GlossaryPackImportResult
            {
                InsertedTermCount = inserted,
                Metadata = document.Metadata
            };
        }

        private static TermEntry CloneTerm(TermEntry term)
        {
            return new TermEntry
            {
                Source = term.Source,
                Target = term.Target,
                Context = term.Context,
                Pos = term.Pos,
                CreatedBy = term.CreatedBy,
                CreatedAt = term.CreatedAt,
                LastUsed = term.LastUsed,
                UsageCount = term.UsageCount,
                IsUserConfirmed = term.IsUserConfirmed
            };
        }

        private class GlossaryPackDocument
        {
            public GlossaryPackMetadata Metadata { get; set; } = new();
            public List<TermEntry> Terms { get; set; } = new();
        }
    }
}
