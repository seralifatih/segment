using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IOnboardingQualificationService
    {
        OnboardingDecision Evaluate(OnboardingProfile profile, LaunchPhase phase);
    }
}
