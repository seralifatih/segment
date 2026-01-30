using System;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TmxImportServiceTests : IDisposable
    {
        private readonly string _testDirectory;

        public TmxImportServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"SegmentTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public void Import_Should_Parse_Valid_TMX_Structure()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg>agreement</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>sözleşme</seg>
      </tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en"">
        <seg>submit</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>gönder</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "test.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert
            terms.Should().HaveCount(2);
            
            var first = terms[0];
            first.Source.Should().Be("agreement");
            first.Target.Should().Be("sözleşme");
            first.Context.Should().Be("tmx");
            first.CreatedBy.Should().Be("tmx-import");
            
            var second = terms[1];
            second.Source.Should().Be("submit");
            second.Target.Should().Be("gönder");
        }

        [Fact]
        public void Import_Should_Map_Languages_By_Code()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en-US""/>
  <body>
    <tu>
      <tuv xml:lang=""en-US"">
        <seg>sign</seg>
      </tuv>
      <tuv xml:lang=""tr-TR"">
        <seg>imzala</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "lang-code.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act - Pass "Turkish" which should normalize to "tr"
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert
            terms.Should().HaveCount(1);
            terms[0].Source.Should().Be("sign");
            terms[0].Target.Should().Be("imzala");
        }

        [Fact]
        public void Import_Should_Skip_Incomplete_Translation_Units()
        {
            // Arrange - One TU has only one tuv (missing target)
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg>valid source</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>geçerli hedef</seg>
      </tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en"">
        <seg>incomplete entry</seg>
      </tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en"">
        <seg>another valid</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>başka geçerli</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "incomplete.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert - Should only return 2 complete entries, skipping the incomplete one
            terms.Should().HaveCount(2);
            terms[0].Source.Should().Be("valid source");
            terms[1].Source.Should().Be("another valid");
        }

        [Fact]
        public void Import_Should_Handle_Empty_Segments_Gracefully()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg></seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>hedef</seg>
      </tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en"">
        <seg>kaynak</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg></seg>
      </tuv>
    </tu>
    <tu>
      <tuv xml:lang=""en"">
        <seg>valid</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>geçerli</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "empty-segs.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert - Should skip entries with empty segments
            terms.Should().HaveCount(1);
            terms[0].Source.Should().Be("valid");
            terms[0].Target.Should().Be("geçerli");
        }

        [Fact]
        public void Import_Should_Trim_Whitespace_From_Segments()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg>  sign  </seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>
          imzala
        </seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "whitespace.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert
            terms.Should().HaveCount(1);
            terms[0].Source.Should().Be("sign");
            terms[0].Target.Should().Be("imzala");
        }

        [Fact]
        public void Import_Should_Fallback_To_First_Two_Tuvs_When_Language_Not_Specified()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg>first</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>ilk</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "no-lang.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act - No target language specified
            var terms = TmxImportService.Import(filePath);

            // Assert - Should use first tuv as source, second as target
            terms.Should().HaveCount(1);
            terms[0].Source.Should().Be("first");
            terms[0].Target.Should().Be("ilk");
        }

        [Fact]
        public void Import_Should_Throw_When_File_Not_Found()
        {
            // Act & Assert
            Action act = () => TmxImportService.Import(Path.Combine(_testDirectory, "nonexistent.tmx"));
            act.Should().Throw<FileNotFoundException>();
        }

        [Fact]
        public void Import_Should_Throw_When_Path_Is_Empty()
        {
            // Act & Assert
            Action act = () => TmxImportService.Import("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Import_Should_Handle_Multiple_Languages_In_TMX()
        {
            // Arrange - TMX with 3 languages
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg>hello</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>merhaba</seg>
      </tuv>
      <tuv xml:lang=""de"">
        <seg>hallo</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "multi-lang.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act - Request Turkish as target
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert - Should extract en->tr pair
            terms.Should().HaveCount(1);
            terms[0].Source.Should().Be("hello");
            terms[0].Target.Should().Be("merhaba");
        }

        [Fact]
        public void Import_Should_Set_Metadata_Correctly()
        {
            // Arrange
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""test"" srclang=""en""/>
  <body>
    <tu>
      <tuv xml:lang=""en"">
        <seg>test</seg>
      </tuv>
      <tuv xml:lang=""tr"">
        <seg>deneme</seg>
      </tuv>
    </tu>
  </body>
</tmx>";

            string filePath = Path.Combine(_testDirectory, "metadata.tmx");
            File.WriteAllText(filePath, tmxContent);

            // Act
            var terms = TmxImportService.Import(filePath, "Turkish");

            // Assert - Verify all metadata fields
            var term = terms[0];
            term.Context.Should().Be("tmx");
            term.CreatedBy.Should().Be("tmx-import");
            term.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            term.LastUsed.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            term.UsageCount.Should().Be(0);
            term.IsUserConfirmed.Should().BeTrue();
        }
    }
}
