using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPlanEntitlementService
    {
        PlanEntitlements ResolveActiveEntitlements();
        EntitlementCheckResult CheckFeature(EntitlementFeature feature);
        bool IsConfidentialityModeAllowed(string mode);
        string GetActivePackageLabel();
        string BuildEntitlementSummary();
    }
}
