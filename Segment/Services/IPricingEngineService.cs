using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPricingEngineService
    {
        PricingConfiguration GetConfiguration();
        PlanEntitlements ResolveEntitlements(PricingPlan plan);
        ResolvedPricingPackage ResolvePackage(SubscriptionSelection selection);
        IReadOnlyList<PricingPlan> GetUpgradePaths(PricingPlan currentPlan);
        PlanTransitionResult Upgrade(SubscriptionSelection currentSelection, PricingPlan targetPlan, int requestedSeats);
        PlanTransitionResult Downgrade(SubscriptionSelection currentSelection, PricingPlan targetPlan, int requestedSeats);
    }
}
