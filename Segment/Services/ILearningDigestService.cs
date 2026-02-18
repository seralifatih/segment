using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ILearningDigestService
    {
        WeeklyLearningDigest BuildWeeklyDigest(DateTime? utcNow = null);
    }
}
