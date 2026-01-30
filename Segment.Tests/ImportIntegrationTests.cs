using System;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class ImportIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _glossaryTestPath;

        public ImportIntegrationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"SegmentTests_{Guid.NewGuid()}");
            _glossaryTestPath = Path.Combine(_testDirectory, "glossary");
            Directory.CreateDirectory(_testDirectory);
            GlossaryService.InitializeForTests(_glossaryTestPath);
        }

        public void Dispose()
        {
            GlossaryService.DisposeForTests();
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void Import_Should_Add_All_Valid_Terms_To_Glossary()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en""><seg>term1</seg></tuv>
      <tuv xml:lang=""tr""><seg>terim1</seg></tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en""><seg>term2</seg></tuv>
      <tuv xml:lang=""tr""><seg>terim2</seg></tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en""><seg>term3</seg></tuv>
      <tuv xml:lang=""tr""><seg>terim3</seg></tuv>
    </tu>
  </body>
</tmx>";

            string tmxPath = Path.Combine(_testDirectory, "import-test.tmx");
            File.WriteAllText(tmxPath, tmxContent);

            // Act
            var terms = TmxImportService.Import(tmxPath, "Turkish");
            int insertedCount = GlossaryService.AddTerms(terms, isGlobal: true);

            // Assert
            insertedCount.Should().Be(3, "all 3 terms should be inserted");

            var effectiveTerms = GlossaryService.GetEffectiveTerms();
            effectiveTerms.Should().ContainKey("term1");
            effectiveTerms.Should().ContainKey("term2");
            effectiveTerms.Should().ContainKey("term3");

            effectiveTerms["term1"].Target.Should().Be("terim1");
            effectiveTerms["term2"].Target.Should().Be("terim2");
            effectiveTerms["term3"].Target.Should().Be("terim3");
        }

        [Fact]
        public void Import_Should_Skip_Invalid_Entries_Without_Crashing()
        {
            // Arrange - Mix of valid and invalid entries
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en""><seg>valid1</seg></tuv>
      <tuv xml:lang=""tr""><seg>geçerli1</seg></tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en""><seg></seg></tuv>
      <tuv xml:lang=""tr""><seg>empty source</seg></tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en""><seg>valid2</seg></tuv>
      <tuv xml:lang=""tr""><seg>geçerli2</seg></tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en""><seg>no target</seg></tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en""><seg>valid3</seg></tuv>
      <tuv xml:lang=""tr""><seg>geçerli3</seg></tuv>
    </tu>
  </body>
</tmx>";

            string tmxPath = Path.Combine(_testDirectory, "mixed-quality.tmx");
            File.WriteAllText(tmxPath, tmxContent);

            // Act
            var terms = TmxImportService.Import(tmxPath, "Turkish");
            int insertedCount = GlossaryService.AddTerms(terms, isGlobal: false);

            // Assert
            insertedCount.Should().Be(3, "only valid entries should be inserted");

            var effectiveTerms = GlossaryService.GetEffectiveTerms();
            effectiveTerms.Should().ContainKey("valid1");
            effectiveTerms.Should().ContainKey("valid2");
            effectiveTerms.Should().ContainKey("valid3");
            effectiveTerms.Should().NotContainKey("");
            effectiveTerms.Should().NotContainKey("no target");
        }

        [Fact]
        public void Import_Should_Update_Existing_Terms_Without_Duplication()
        {
            // Arrange - Import same TMX twice
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en""><seg>duplicate</seg></tuv>
      <tuv xml:lang=""tr""><seg>ilk değer</seg></tuv>
    </tu>
  </body>
</tmx>";

            string tmxPath = Path.Combine(_testDirectory, "duplicate.tmx");
            File.WriteAllText(tmxPath, tmxContent);

            // Act - Import twice
            var terms1 = TmxImportService.Import(tmxPath, "Turkish");
            GlossaryService.AddTerms(terms1, isGlobal: true);

            // Change the target value in TMX
            string updatedTmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en""><seg>duplicate</seg></tuv>
      <tuv xml:lang=""tr""><seg>yeni değer</seg></tuv>
    </tu>
  </body>
</tmx>";
            File.WriteAllText(tmxPath, updatedTmxContent);

            var terms2 = TmxImportService.Import(tmxPath, "Turkish");
            GlossaryService.AddTerms(terms2, isGlobal: true);

            // Assert - Should have only one entry with updated value
            var effectiveTerms = GlossaryService.GetEffectiveTerms();
            effectiveTerms.Should().ContainKey("duplicate");
            effectiveTerms["duplicate"].Target.Should().Be("yeni değer", "should be updated, not duplicated");

            // Count all global terms to verify no duplication
            var globalCount = GlossaryService.GlobalProfile.Terms.Count();
            globalCount.Should().Be(1, "should have exactly one term, not two");
        }

        [Fact]
        public void Import_Large_TMX_Should_Complete_Without_Errors()
        {
            // Arrange - Generate a large TMX with 100 entries
            var tmxBuilder = new System.Text.StringBuilder();
            tmxBuilder.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            tmxBuilder.AppendLine(@"<tmx version=""1.4"">");
            tmxBuilder.AppendLine(@"  <header creationtool=""test"" srclang=""en""/>");
            tmxBuilder.AppendLine(@"  <body>");

            for (int i = 1; i <= 100; i++)
            {
                tmxBuilder.AppendLine($@"    <tu>");
                tmxBuilder.AppendLine($@"      <tuv xml:lang=""en""><seg>term{i}</seg></tuv>");
                tmxBuilder.AppendLine($@"      <tuv xml:lang=""tr""><seg>terim{i}</seg></tuv>");
                tmxBuilder.AppendLine($@"    </tu>");
            }

            tmxBuilder.AppendLine(@"  </body>");
            tmxBuilder.AppendLine(@"</tmx>");

            string tmxPath = Path.Combine(_testDirectory, "large.tmx");
            File.WriteAllText(tmxPath, tmxBuilder.ToString());

            // Act
            var terms = TmxImportService.Import(tmxPath, "Turkish");
            int insertedCount = GlossaryService.AddTerms(terms, isGlobal: false);

            // Assert
            insertedCount.Should().Be(100);
            var effectiveTerms = GlossaryService.GetEffectiveTerms();
            effectiveTerms.Should().HaveCountGreaterOrEqualTo(100);
            effectiveTerms.Should().ContainKey("term1");
            effectiveTerms.Should().ContainKey("term50");
            effectiveTerms.Should().ContainKey("term100");
        }

        [Fact]
        public void Import_Should_Preserve_Context_From_TMX()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en""><seg>contextual</seg></tuv>
      <tuv xml:lang=""tr""><seg>bağlamsal</seg></tuv>
    </tu>
  </body>
</tmx>";

            string tmxPath = Path.Combine(_testDirectory, "context.tmx");
            File.WriteAllText(tmxPath, tmxContent);

            // Act
            var terms = TmxImportService.Import(tmxPath, "Turkish");
            GlossaryService.AddTerms(terms, isGlobal: true);

            // Assert
            var effectiveTerms = GlossaryService.GetEffectiveTerms();
            effectiveTerms["contextual"].Context.Should().Be("tmx");
            effectiveTerms["contextual"].CreatedBy.Should().Be("tmx-import");
        }
    }
}
