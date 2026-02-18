using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IDecisionGateEvaluator
    {
        GateDecisionResult Evaluate(LaunchPhase phase, PmfDashboardSnapshot snapshot);
    }
}
