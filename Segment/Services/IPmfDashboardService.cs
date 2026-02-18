using System;
using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPmfDashboardService
    {
        void RecordEvent(PmfUsageEvent usageEvent);
        PmfDashboardSnapshot GetDashboardSnapshot(DateTime windowStartUtc, DateTime windowEndUtc);
        IReadOnlyList<PmfDashboardSnapshot> GetWeeklySnapshots(int weekCount, DateTime? anchorUtc = null);
    }
}
