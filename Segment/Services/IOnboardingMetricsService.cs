using System;
using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IOnboardingMetricsService
    {
        void Record(OnboardingMetricRecord record);
        IReadOnlyList<OnboardingMetricRecord> GetRecords(DateTime? fromUtc = null, DateTime? toUtc = null);
    }
}
