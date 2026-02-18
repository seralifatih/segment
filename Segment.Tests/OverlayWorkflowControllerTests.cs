using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class OverlayWorkflowControllerTests
    {
        [Fact]
        public void BuildLabel_Should_Return_Expected_State_Label()
        {
            var controller = new OverlayWorkflowController();

            controller.MarkCaptured();
            controller.BuildLabel().Should().Be("Captured");

            controller.MarkTranslating();
            controller.BuildLabel().Should().Be("Translating");

            controller.MarkReady();
            controller.BuildLabel().Should().Be("Ready");

            controller.MarkApplied();
            controller.BuildLabel().Should().Be("Applied");
        }

        [Fact]
        public void State_Transitions_Should_Update_CurrentState()
        {
            var controller = new OverlayWorkflowController();

            controller.MarkCaptured();
            controller.CurrentState.Should().Be(OverlayWorkflowState.Captured);

            controller.MarkTranslating();
            controller.CurrentState.Should().Be(OverlayWorkflowState.Translating);

            controller.MarkReady();
            controller.CurrentState.Should().Be(OverlayWorkflowState.Ready);

            controller.MarkApplied();
            controller.CurrentState.Should().Be(OverlayWorkflowState.Applied);
        }

        [Fact]
        public void TryTransition_Should_Reject_Invalid_Transition()
        {
            var controller = new OverlayWorkflowController();

            bool changed = controller.TryTransition(OverlayWorkflowState.Applied);

            changed.Should().BeFalse();
            controller.CurrentState.Should().Be(OverlayWorkflowState.Captured);
        }

        [Fact]
        public void MarkError_Should_Set_Error_State_And_Message()
        {
            var controller = new OverlayWorkflowController();

            controller.MarkTranslating();
            controller.MarkError("Provider request timed out.");

            controller.CurrentState.Should().Be(OverlayWorkflowState.Error);
            controller.LastError.Should().Contain("timed out");
        }
    }
}
