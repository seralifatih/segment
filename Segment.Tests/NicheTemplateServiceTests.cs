using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class NicheTemplateServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly string _glossaryBasePath;
        private readonly NicheTemplateService _service;

        public NicheTemplateServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentNicheTemplateTests", Guid.NewGuid().ToString("N"));
            _glossaryBasePath = Path.Combine(_basePath, "glossary");
            Directory.CreateDirectory(_basePath);
            GlossaryService.InitializeForTests(_glossaryBasePath);
            _service = new NicheTemplateService(_basePath);
        }

        [Fact]
        public void SerializeDeserialize_Should_RoundTrip_NichePack()
        {
            var pack = new NichePackDocument
            {
                SchemaVersion = 1,
                Metadata = new NichePackMetadata
                {
                    PackId = "pack-1",
                    PackName = "Legal Starter"
                },
                Domain = DomainVertical.Legal,
                SourceLanguage = "English",
                TargetLanguage = "Turkish",
                StyleHints = new List<string> { "formal_register", "clause_integrity" },
                EnabledQaChecks = new List<string> { LegalDomainQaPlugin.Id },
                GlossaryTerms = new List<TermEntry>
                {
                    new() { Source = "governing law", Target = "uygulanacak hukuk" },
                    new() { Source = "party", Target = "taraf" }
                }
            };

            string json = _service.SerializePack(pack);
            NichePackDocument roundtrip = _service.DeserializePack(json);

            roundtrip.Metadata.PackName.Should().Be("Legal Starter");
            roundtrip.Domain.Should().Be(DomainVertical.Legal);
            roundtrip.EnabledQaChecks.Should().Contain(LegalDomainQaPlugin.Id);
            roundtrip.GlossaryTerms.Should().HaveCount(2);
        }

        [Fact]
        public void ImportPack_Should_KeepExisting_When_ConflictMode_Is_KeepExisting()
        {
            GlossaryService.GetOrCreateProfile("KeepExistingProject");
            GlossaryService.AddTerm("agreement", "anlasma", isGlobal: false);

            string packPath = Path.Combine(_basePath, "keep-existing.segmentniche.json");
            var pack = new NichePackDocument
            {
                SchemaVersion = 1,
                Metadata = new NichePackMetadata { PackName = "KeepExisting" },
                Domain = DomainVertical.Legal,
                GlossaryTerms = new List<TermEntry>
                {
                    new() { Source = "agreement", Target = "sozlesme" },
                    new() { Source = "agreement", Target = "mukavele" },
                    new() { Source = "party", Target = "taraf" }
                }
            };
            File.WriteAllText(packPath, _service.SerializePack(pack));

            NichePackImportResult result = _service.ImportPack(
                packPath,
                "KeepExistingProject",
                "Turkish",
                NichePackConflictMode.KeepExisting);

            result.InsertedTermCount.Should().Be(1);
            result.UpdatedTermCount.Should().Be(0);
            result.DuplicateConflictCount.Should().BeGreaterThan(0);
            GlossaryService.CurrentProfile.Terms.FindById("agreement")!.Target.Should().Be("anlasma");
            GlossaryService.CurrentProfile.Terms.FindById("party")!.Target.Should().Be("taraf");
        }

        [Fact]
        public void ImportPack_Should_OverwriteExisting_When_ConflictMode_Is_Overwrite()
        {
            GlossaryService.GetOrCreateProfile("OverwriteProject");
            GlossaryService.AddTerm("agreement", "anlasma", isGlobal: false);

            string packPath = Path.Combine(_basePath, "overwrite.segmentniche.json");
            var pack = new NichePackDocument
            {
                SchemaVersion = 1,
                Metadata = new NichePackMetadata { PackName = "Overwrite" },
                Domain = DomainVertical.Legal,
                GlossaryTerms = new List<TermEntry>
                {
                    new() { Source = "agreement", Target = "sozlesme" },
                    new() { Source = "agreement", Target = "mukavele" }
                }
            };
            File.WriteAllText(packPath, _service.SerializePack(pack));

            NichePackImportResult result = _service.ImportPack(
                packPath,
                "OverwriteProject",
                "Turkish",
                NichePackConflictMode.OverwriteExisting);

            result.UpdatedTermCount.Should().Be(1);
            result.DuplicateConflictCount.Should().BeGreaterThan(0);
            GlossaryService.CurrentProfile.Terms.FindById("agreement")!.Target.Should().Be("mukavele");
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
                // Best effort cleanup.
            }
        }
    }
}
