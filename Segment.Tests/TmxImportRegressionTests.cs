using System;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TmxImportRegressionTests : IDisposable
    {
        private readonly string _dir;

        public TmxImportRegressionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "SegmentTmxRegression", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [Fact]
        public void Import_Should_Still_Fallback_To_First_Two_Tuvs_When_Header_SourceLang_Is_Missing()
        {
            string tmxContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tmx version=""1.4"">
  <header creationtool=""regression-test""/>
  <body>
    <tu>
      <tuv xml:lang=""en""><seg>fallback source</seg></tuv>
      <tuv xml:lang=""tr""><seg>yedek hedef</seg></tuv>
      <tuv xml:lang=""de""><seg>ersatz ziel</seg></tuv>
    </tu>
  </body>
</tmx>";
            string path = Path.Combine(_dir, "fallback.tmx");
            File.WriteAllText(path, tmxContent);

            var terms = TmxImportService.Import(path);

            terms.Should().HaveCount(1);
            terms[0].Source.Should().Be("fallback source");
            terms[0].Target.Should().Be("yedek hedef");
            terms[0].Context.Should().Be("tmx");
            terms[0].CreatedBy.Should().Be("tmx-import");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_dir))
                {
                    Directory.Delete(_dir, true);
                }
            }
            catch
            {
            }
        }
    }
}
