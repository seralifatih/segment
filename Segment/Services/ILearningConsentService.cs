using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ILearningConsentService
    {
        LearningConsentOutcome ApplyDecision(
            DetectedChange change,
            LearningConsentOption option,
            System.Func<LearningConflictPrompt, LearningConflictDecision>? conflictResolver = null);
    }
}
