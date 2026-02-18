using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IAttributionAnalyticsService
    {
        void RecordGlossaryPackImport(GlossaryPackImportRecord record);
        AttributionAnalyticsSnapshot GetAttributionSnapshot();
        void TrackFreelancerDomainActivation(string userId, string emailDomain);
        AgencyExpansionTriggerResult EvaluateAgencyExpansion(string emailDomain, int freelancerThreshold = 3);
    }
}
