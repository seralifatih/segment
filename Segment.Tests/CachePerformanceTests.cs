using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class CachePerformanceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _glossaryTestPath;

        public CachePerformanceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"SegmentTests_{Guid.NewGuid()}");
            _glossaryTestPath = Path.Combine(_testDirectory, "glossary");
            Directory.CreateDirectory(_testDirectory);
            GlossaryService.InitializeForTests(_glossaryTestPath);

            // Add some test data
            for (int i = 1; i <= 100; i++)
            {
                GlossaryService.AddTerm($"term{i}", $"terim{i}", isGlobal: false);
            }
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
        public void GetEffectiveTerms_Should_Use_Cache_On_Subsequent_Calls()
        {
            // Act - First call (cache miss, should query DB)
            var sw1 = Stopwatch.StartNew();
            var terms1 = GlossaryService.GetEffectiveTerms();
            sw1.Stop();

            // Act - Second call (cache hit, should be instant)
            var sw2 = Stopwatch.StartNew();
            var terms2 = GlossaryService.GetEffectiveTerms();
            sw2.Stop();

            // Assert
            terms1.Should().HaveCount(100);
            terms2.Should().HaveCount(100);
            
            // Second call should be significantly faster (at least 10x)
            sw2.ElapsedTicks.Should().BeLessThan(sw1.ElapsedTicks / 5, 
                "cached call should be much faster than DB query");

            // Verify they return the same dictionary instance (reference equality)
            object.ReferenceEquals(terms1, terms2).Should().BeTrue(
                "cache should return the exact same dictionary instance");
        }

        [Fact]
        public void Cache_Should_Invalidate_After_AddTerm()
        {
            // Arrange - Prime the cache
            var terms1 = GlossaryService.GetEffectiveTerms();
            terms1.Should().HaveCount(100);

            // Act - Add a new term (should invalidate cache)
            GlossaryService.AddTerm("newterm", "yeniterim", isGlobal: false);

            // Get terms again (should rebuild cache with new term)
            var terms2 = GlossaryService.GetEffectiveTerms();

            // Assert
            terms2.Should().HaveCount(101);
            terms2.Should().ContainKey("newterm");
            terms2["newterm"].Target.Should().Be("yeniterim");

            // Should be a different dictionary instance after invalidation
            object.ReferenceEquals(terms1, terms2).Should().BeFalse(
                "cache should be rebuilt after AddTerm");
        }

        [Fact]
        public void Cache_Should_Invalidate_After_RemoveTerm()
        {
            // Arrange - Prime the cache
            var terms1 = GlossaryService.GetEffectiveTerms();
            terms1.Should().ContainKey("term1");

            // Act - Remove a term (should invalidate cache)
            GlossaryService.RemoveTerm("term1", isGlobal: false);

            // Get terms again
            var terms2 = GlossaryService.GetEffectiveTerms();

            // Assert
            terms2.Should().HaveCount(99);
            terms2.Should().NotContainKey("term1");

            // Should be a different dictionary instance
            object.ReferenceEquals(terms1, terms2).Should().BeFalse(
                "cache should be rebuilt after RemoveTerm");
        }

        [Fact]
        public void Cache_Should_Invalidate_After_Profile_Switch()
        {
            // Arrange - Prime the cache with current profile
            var terms1 = GlossaryService.GetEffectiveTerms();
            terms1.Should().HaveCount(100);

            // Act - Switch to a new profile
            GlossaryService.GetOrCreateProfile("TestProfile2");
            GlossaryService.AddTerm("profileterm", "profilterim", isGlobal: false);

            // Get terms again
            var terms2 = GlossaryService.GetEffectiveTerms();

            // Assert - Should have new profile's term
            terms2.Should().ContainKey("profileterm");
            
            // Should be a different dictionary instance after profile switch
            object.ReferenceEquals(terms1, terms2).Should().BeFalse(
                "cache should be rebuilt after profile switch");
        }
    }
}
