using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TranslationResultCacheServiceTests
    {
        [Fact]
        public void BuildKey_Should_Be_Deterministic_And_DomainAware()
        {
            string key1 = TranslationResultCacheService.BuildKey("Hello", "English", "Turkish", "v1", "Legal");
            string key2 = TranslationResultCacheService.BuildKey("Hello", "English", "Turkish", "v1", "Legal");
            string key3 = TranslationResultCacheService.BuildKey("Hello", "English", "Turkish", "v1", "Medical");

            key1.Should().Be(key2);
            key1.Should().NotBe(key3);
        }
    }
}
