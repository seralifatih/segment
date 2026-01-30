using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class CollectionNamingTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _glossaryTestPath;

        public CollectionNamingTests()
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
        public void Collection_Names_Should_Be_Unique_For_Similar_Profile_Names()
        {
            // Arrange - Create profiles with names that would collide with old logic
            string profile1 = "Client-A";
            string profile2 = "Client_A";
            string profile3 = "Client A";

            // Act - Create profiles and add terms
            GlossaryService.GetOrCreateProfile(profile1);
            GlossaryService.AddTerm("term1", "terim1", isGlobal: false);

            GlossaryService.GetOrCreateProfile(profile2);
            GlossaryService.AddTerm("term2", "terim2", isGlobal: false);

            GlossaryService.GetOrCreateProfile(profile3);
            GlossaryService.AddTerm("term3", "terim3", isGlobal: false);

            // Assert - Each profile should have its own distinct term
            GlossaryService.GetOrCreateProfile(profile1);
            var terms1 = GlossaryService.GetEffectiveTerms();
            
            GlossaryService.GetOrCreateProfile(profile2);
            var terms2 = GlossaryService.GetEffectiveTerms();
            
            GlossaryService.GetOrCreateProfile(profile3);
            var terms3 = GlossaryService.GetEffectiveTerms();

            // Each profile should have exactly one unique term (no cross-contamination)
            terms1.Should().ContainKey("term1");
            terms1.Should().NotContainKey("term2");
            terms1.Should().NotContainKey("term3");

            terms2.Should().ContainKey("term2");
            terms2.Should().NotContainKey("term1");
            terms2.Should().NotContainKey("term3");

            terms3.Should().ContainKey("term3");
            terms3.Should().NotContainKey("term1");
            terms3.Should().NotContainKey("term2");
        }

        [Fact]
        public void Collection_Names_Should_Be_Deterministic()
        {
            // Arrange
            string profileName = "TestProfile-123";

            // Act - Get collection name via reflection (since it's private)
            var method = typeof(GlossaryService).GetMethod("GetTermsCollectionName", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            string name1 = (string)method!.Invoke(null, new object[] { profileName })!;
            string name2 = (string)method!.Invoke(null, new object[] { profileName })!;

            // Assert - Same input should always produce same output
            name1.Should().Be(name2);
            name1.Should().StartWith("terms_");
            name1.Should().HaveLength(70); // "terms_" (6) + SHA256 hex (64) = 70 chars
        }

        [Fact]
        public void Collection_Names_Should_Handle_Special_Characters()
        {
            // Arrange - Profile names with various special characters
            var specialNames = new[]
            {
                "Project@2024",
                "Client #1",
                "Test/Project",
                "Project (v2)",
                "Project:123",
                "中文项目", // Chinese characters
                "Проект", // Cyrillic
                "🚀 Rocket", // Emoji
            };

            // Act & Assert - All should create valid, unique collection names
            foreach (var name in specialNames)
            {
                GlossaryService.GetOrCreateProfile(name);
                GlossaryService.AddTerm($"term_{name}", $"terim_{name}", isGlobal: false);
                
                var terms = GlossaryService.GetEffectiveTerms();
                terms.Should().ContainKey($"term_{name}");
            }
        }

        [Fact]
        public void Empty_Or_Null_Profile_Name_Should_Default_Safely()
        {
            // Arrange & Act - Use reflection to test edge cases
            var method = typeof(GlossaryService).GetMethod("GetTermsCollectionName", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            string nameForEmpty = (string)method!.Invoke(null, new object[] { "" })!;
            string nameForWhitespace = (string)method!.Invoke(null, new object[] { "   " })!;

            // Assert - Should handle edge cases gracefully
            nameForEmpty.Should().StartWith("terms_");
            nameForWhitespace.Should().StartWith("terms_");
            
            // Both should map to "default"
            nameForEmpty.Should().Be(nameForWhitespace);
        }
    }
}
