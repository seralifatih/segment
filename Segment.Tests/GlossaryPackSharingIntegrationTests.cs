using System;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class GlossaryPackSharingIntegrationTests : IDisposable
    {
        private readonly string _basePath;
        private readonly string _glossaryBasePath;
        private readonly ReferralService _referralService;
        private readonly GlossaryPackSharingService _sharingService;

        public GlossaryPackSharingIntegrationTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentGlossaryPackTests", Guid.NewGuid().ToString("N"));
            _glossaryBasePath = Path.Combine(_basePath, "glossary");
            Directory.CreateDirectory(_basePath);

            GlossaryService.InitializeForTests(_glossaryBasePath);
            _referralService = new ReferralService(_basePath);
            _sharingService = new GlossaryPackSharingService(_referralService);
        }

        [Fact]
        public void Export_And_Import_GlossaryPack_Should_Record_Attribution()
        {
            GlossaryService.AddTerm("legal brief", "hukuki dosya", isGlobal: true);
            GlossaryService.AddTerm("contract clause", "sözleşme maddesi", isGlobal: true);

            string referralCode = _referralService.CreateReferralCode("owner-1");
            string packPath = Path.Combine(_basePath, "legal-pack.json");

            var metadata = _sharingService.ExportLegalGlossaryPack(
                filePath: packPath,
                exportedByUserId: "owner-1",
                packName: "Agency Legal Pack",
                referralCode: referralCode,
                isGlobal: true);

            GlossaryService.GetOrCreateProfile("ImportedPackProject");
            var importResult = _sharingService.ImportGlossaryPack(
                filePath: packPath,
                importedByUserId: "referred-importer-1",
                isGlobal: false);

            importResult.InsertedTermCount.Should().BeGreaterThan(0);
            importResult.Metadata.PackId.Should().Be(metadata.PackId);
            importResult.Metadata.ReferralCode.Should().Be(referralCode);

            var snapshot = _referralService.GetAttributionSnapshot();
            snapshot.TotalGlossaryPackImports.Should().BeGreaterThanOrEqualTo(1);
            snapshot.ImportsByReferralCode.Should().ContainKey(referralCode);
            snapshot.ImportsByReferralCode[referralCode].Should().BeGreaterThanOrEqualTo(1);
        }

        public void Dispose()
        {
            _referralService.Dispose();
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
