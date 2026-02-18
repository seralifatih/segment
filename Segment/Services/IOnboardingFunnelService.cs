using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IOnboardingFunnelService
    {
        OnboardingDecision Process(OnboardingProfile profile);
    }
}
