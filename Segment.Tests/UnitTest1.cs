using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Segment.App.Services;
using Xunit;

namespace Segment.Tests;

[Collection("Database Tests")]
public class GlossaryServiceTests
{
    private sealed class GlossaryTestScope : IDisposable
    {
        private readonly string _basePath;
        public string BasePath => _basePath;

        public GlossaryTestScope()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentTests", Guid.NewGuid().ToString("N"));
            GlossaryService.InitializeForTests(_basePath);
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
                // Best-effort cleanup for test temp data
            }
        }
    }

    [Fact]
    public void Persistence_Should_Retain_Terms_Across_Reinitialization()
    {
        using var scope = new GlossaryTestScope();

        GlossaryService.AddTerm("hello", "merhaba", isGlobal: true);
        GlossaryService.DisposeForTests();
        GlossaryService.InitializeForTests(scope.BasePath);

        var entry = GlossaryService.GlobalProfile.Terms.FindById("hello");
        entry.Should().NotBeNull();
        entry!.Target.Should().Be("merhaba");
    }

    [Fact]
    public void Concurrency_Should_Add_Thousand_Unique_Terms()
    {
        using var scope = new GlossaryTestScope();

        Parallel.For(0, 1000, i =>
        {
            GlossaryService.AddTerm($"source-{i}", $"target-{i}", isGlobal: true);
        });

        var count = GlossaryService.GlobalProfile.Terms.Count();
        count.Should().Be(1000);
    }

    [Fact]
    public void EffectiveTerms_Should_Prefer_Project_Over_Global()
    {
        using var scope = new GlossaryTestScope();

        GlossaryService.AddTerm("agreement", "sozlesme", isGlobal: true);
        GlossaryService.GetOrCreateProfile("ProjectA");
        GlossaryService.AddTerm("agreement", "mukavele", isGlobal: false);

        var effective = GlossaryService.GetEffectiveTerms();
        effective.Should().ContainKey("agreement");
        effective["agreement"].Target.Should().Be("mukavele");
    }
}
