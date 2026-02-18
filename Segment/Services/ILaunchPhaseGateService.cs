using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ILaunchPhaseGateService
    {
        bool IsFeatureEnabled(string feature, LaunchUserContext userContext);
        bool CanInviteUser(LaunchUserContext userContext);
    }
}
