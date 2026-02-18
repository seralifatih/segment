using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IRevenuePlanningService
    {
        RevenuePlanningSnapshot BuildSnapshot(
            ArrScenarioPlan plan,
            IReadOnlyList<RevenueActualSnapshot> actuals,
            PmfDashboardSnapshot? latestPmfSnapshot = null,
            PmfDashboardSnapshot? baselinePmfSnapshot = null,
            RevenueRiskThresholds? riskThresholds = null);
    }
}
