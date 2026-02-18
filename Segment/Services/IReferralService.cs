using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IReferralService
    {
        string CreateReferralCode(string referrerUserId);
        string BuildReferralLink(string referralCode, string baseUrl);
        void RegisterReferredUser(string referredUserId, string referralCode, DateTime signupAtUtc, string emailDomain);
        void RecordGlossaryImportedMilestone(string referredUserId, DateTime? occurredAtUtc = null);
        void RecordTranslatedSegmentsMilestone(string referredUserId, int additionalSegments, DateTime? occurredAtUtc = null);
        void RecordPaidConversionMilestone(string referredUserId, DateTime? occurredAtUtc = null);
        ReferralRewardEligibilityResult GrantRewardIfEligible(string referredUserId, int requiredTranslatedSegments, int conversionWindowDays);
        ReferralConversionFunnelDashboard GetReferralConversionDashboard();
    }
}
