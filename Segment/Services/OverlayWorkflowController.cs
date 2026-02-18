using Segment.App.Models;
using System;
using System.Collections.Generic;

namespace Segment.App.Services
{
    public class OverlayWorkflowController
    {
        public OverlayWorkflowState CurrentState { get; private set; } = OverlayWorkflowState.Captured;
        public string LastError { get; private set; } = string.Empty;

        private static readonly IReadOnlyDictionary<OverlayWorkflowState, OverlayWorkflowState[]> AllowedTransitions =
            new Dictionary<OverlayWorkflowState, OverlayWorkflowState[]>
            {
                [OverlayWorkflowState.Captured] = new[] { OverlayWorkflowState.Translating, OverlayWorkflowState.Error },
                [OverlayWorkflowState.Translating] = new[] { OverlayWorkflowState.Ready, OverlayWorkflowState.Error },
                [OverlayWorkflowState.Ready] = new[] { OverlayWorkflowState.Applied, OverlayWorkflowState.Translating, OverlayWorkflowState.Error },
                [OverlayWorkflowState.Applied] = new[] { OverlayWorkflowState.Captured, OverlayWorkflowState.Ready, OverlayWorkflowState.Translating, OverlayWorkflowState.Error },
                [OverlayWorkflowState.Error] = new[] { OverlayWorkflowState.Captured, OverlayWorkflowState.Translating }
            };

        public bool TryTransition(OverlayWorkflowState nextState, string? error = null)
        {
            if (!AllowedTransitions.TryGetValue(CurrentState, out var allowed) || Array.IndexOf(allowed, nextState) < 0)
            {
                return false;
            }

            CurrentState = nextState;
            LastError = nextState == OverlayWorkflowState.Error ? (error ?? "Unknown reflex flow error.") : string.Empty;
            return true;
        }

        public void MarkCaptured() => CurrentState = OverlayWorkflowState.Captured;
        public void MarkTranslating() => TryTransition(OverlayWorkflowState.Translating);
        public void MarkReady() => TryTransition(OverlayWorkflowState.Ready);
        public void MarkApplied() => TryTransition(OverlayWorkflowState.Applied);
        public void MarkError(string? error = null) => TryTransition(OverlayWorkflowState.Error, error);

        public string BuildLabel()
        {
            return CurrentState switch
            {
                OverlayWorkflowState.Captured => "Captured",
                OverlayWorkflowState.Translating => "Translating",
                OverlayWorkflowState.Ready => "Ready",
                OverlayWorkflowState.Applied => "Applied",
                OverlayWorkflowState.Error => "Error",
                _ => "Captured"
            };
        }
    }
}
