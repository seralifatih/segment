using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class InteroperabilityFoundationIntegrationTests : IDisposable
    {
        private readonly string _root;
        private readonly string _glossaryPath;
        private readonly string _interopPath;

        public InteroperabilityFoundationIntegrationTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "SegmentInteropTests", Guid.NewGuid().ToString("N"));
            _glossaryPath = Path.Combine(_root, "glossary");
            _interopPath = Path.Combine(_root, "interop");
            Directory.CreateDirectory(_glossaryPath);
            Directory.CreateDirectory(_interopPath);
            GlossaryService.InitializeForTests(_glossaryPath);
        }

        [Fact]
        public void Tmx_Connector_RoundTrip_Should_Preserve_Terms_And_Language_Mapping()
        {
            GlossaryService.GetOrCreateProfile("InteropProject");
            GlossaryService.AddTerms(new[]
            {
                new TermEntry { Source = "agreement", Target = "sozlesme", SourceLanguage = "en", TargetLanguage = "tr" },
                new TermEntry { Source = "notice", Target = "bildirim", SourceLanguage = "en", TargetLanguage = "tr" }
            }, isGlobal: false);

            string tmxPath = Path.Combine(_interopPath, "project-memory.tmx");
            var service = new InteroperabilityService(new InteroperabilityConnectorRegistry(new IInteroperabilityConnector[] { new TmxInteroperabilityConnector() }));
            service.ExportTerms("tmx", tmxPath, isGlobal: false, profileName: "InteropProject", new InteropTermTransferOptions
            {
                SourceLanguage = "en",
                TargetLanguage = "tr"
            });

            GlossaryService.GetOrCreateProfile("ImportedProject");
            int imported = service.ImportTerms("tmx", tmxPath, isGlobal: false, new InteropTermTransferOptions
            {
                TargetLanguage = "Turkish"
            });

            imported.Should().Be(2);
            GlossaryService.CurrentProfile.Terms.FindById("agreement")!.Target.Should().Be("sozlesme");
            GlossaryService.CurrentProfile.Terms.FindById("notice")!.Target.Should().Be("bildirim");
            GlossaryService.CurrentProfile.Terms.FindById("agreement")!.SourceLanguage.Should().NotBeNullOrWhiteSpace();
            GlossaryService.CurrentProfile.Terms.FindById("agreement")!.TargetLanguage.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void ApplyExternalProjectMapping_Should_Persist_Connector_Metadata_Into_Project_Config()
        {
            var nicheTemplateService = new NicheTemplateService(_interopPath);
            var service = new InteroperabilityService(nicheTemplateService: nicheTemplateService);

            var mapping = new ExternalProjectProfileMapping
            {
                ConnectorId = "tmx-file",
                ExternalProjectId = "cat-project-42",
                ExternalClientId = "client-ax",
                ExternalStyleGuideId = "style-legal-v2",
                ExternalTags = new[] { "legal", "regulated" },
                Metadata = new Dictionary<string, string>
                {
                    ["locale"] = "en-TR",
                    ["source"] = "cat-adapter"
                }
            };

            service.ApplyExternalProjectMapping("MappedProject", mapping);

            bool found = nicheTemplateService.TryGetProjectConfiguration("MappedProject", out ProjectNicheConfiguration config);
            found.Should().BeTrue();
            config.ExternalMapping.ConnectorId.Should().Be("tmx-file");
            config.ExternalMapping.ExternalProjectId.Should().Be("cat-project-42");
            config.ExternalMapping.ExternalStyleGuideId.Should().Be("style-legal-v2");
            config.ExternalMapping.Metadata["locale"].Should().Be("en-TR");
        }

        public void Dispose()
        {
            GlossaryService.DisposeForTests();
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, true);
                }
            }
            catch
            {
            }
        }
    }
}
