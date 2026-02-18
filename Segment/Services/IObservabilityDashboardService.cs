using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IObservabilityDashboardService
    {
        GrowthObservabilityDashboard BuildDashboard(int pilotWeekWindow = 12);
    }
}
