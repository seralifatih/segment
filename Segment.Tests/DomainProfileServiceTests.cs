using System;
using System.Linq;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class DomainProfileServiceTests
    {
        [Fact]
        public void GetProfiles_Should_Return_All_Domain_Verticals()
        {
            var service = new DomainProfileService();

            var profiles = service.GetProfiles();

            profiles.Should().HaveCount(8);
            profiles.Select(x => x.Id).Should().BeEquivalentTo(Enum.GetValues<DomainVertical>());
            profiles.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Name));
            profiles.Should().OnlyContain(x => x.DefaultChecks.Count > 0);
            profiles.Should().OnlyContain(x => x.DefaultStyleHints.Count > 0);
            profiles.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.RecommendedProviderPolicy));
        }

        [Fact]
        public void GetRulePack_Should_Return_Seeded_Defaults_For_Each_Domain()
        {
            var service = new DomainProfileService();

            foreach (DomainVertical domain in Enum.GetValues<DomainVertical>())
            {
                DomainRulePack pack = service.GetRulePack(domain);
                pack.RequireTerminologyChecks.Should().BeTrue();
                pack.RequireNumericChecks.Should().BeTrue();
                pack.DisallowedPhrases.Should().NotBeNull();
                pack.DisallowedPhrases.Should().HaveCountGreaterThan(0);
                ((int)pack.ErrorSeverity).Should().BeGreaterThanOrEqualTo((int)pack.WarningSeverity);
            }
        }
    }
}
