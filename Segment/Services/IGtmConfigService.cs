using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IGtmConfigService
    {
        GtmConfig LoadConfig();
        void SaveConfig(GtmConfig config);
        LaunchPhase GetActiveLaunchPhase();
        IReadOnlyList<GtmKpiTarget> GetKpiTargetsByPhase(LaunchPhase phase);
    }
}
