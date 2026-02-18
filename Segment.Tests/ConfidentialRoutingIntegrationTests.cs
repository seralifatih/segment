using System.Threading.Tasks;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ConfidentialRoutingIntegrationTests
    {
        private static readonly object SettingsSync = new();

        [Fact]
        public async Task SuggestAsync_Should_Block_Cloud_Routing_When_Confidential_LocalOnly_Is_Enabled()
        {
            string originalProvider;
            bool originalConfidentialMode;
            string originalConfidentialityMode;

            lock (SettingsSync)
            {
                originalProvider = SettingsService.Current.AiProvider;
                originalConfidentialMode = SettingsService.Current.ConfidentialProjectLocalOnly;
                originalConfidentialityMode = SettingsService.Current.ConfidentialityMode;

                SettingsService.Current.AiProvider = "Google";
                SettingsService.Current.ConfidentialityMode = "LocalOnly";
                SettingsService.Current.ConfidentialProjectLocalOnly = false;
            }

            try
            {
                string result = await TranslationService.SuggestAsync("Confidential legal clause sample.");
                result.Should().StartWith("ERROR:");
                result.Should().Contain("Cloud routing is blocked");
            }
            finally
            {
                lock (SettingsSync)
                {
                    SettingsService.Current.AiProvider = originalProvider;
                    SettingsService.Current.ConfidentialProjectLocalOnly = originalConfidentialMode;
                    SettingsService.Current.ConfidentialityMode = originalConfidentialityMode;
                }
            }
        }
    }
}
