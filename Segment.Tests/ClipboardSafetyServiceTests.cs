using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ClipboardSafetyServiceTests
    {
        [Fact]
        public void EvaluateOverwrite_Should_Allow_When_Clipboard_Unchanged()
        {
            var decision = ClipboardSafetyService.EvaluateOverwrite("before", "before");

            decision.AllowOverwrite.Should().BeTrue();
        }

        [Fact]
        public void EvaluateOverwrite_Should_Block_When_Clipboard_Changed()
        {
            var decision = ClipboardSafetyService.EvaluateOverwrite("before", "changed by user");

            decision.AllowOverwrite.Should().BeFalse();
            decision.Reason.Should().Contain("Clipboard changed");
        }
    }
}
