using FluentAssertions;
using Segment.App.Services;
using System.Windows.Input;

namespace Segment.Tests
{
    public class HotkeyBindingServiceTests
    {
        [Fact]
        public void TryParse_Should_Parse_Modifiers_And_Key()
        {
            bool parsed = HotkeyBindingService.TryParse("Ctrl+Shift+Space", "InPlace", out var binding);

            parsed.Should().BeTrue();
            binding.Key.Should().Be(Key.Space);
            binding.Modifiers.Should().Be(ModifierKeys.Control | ModifierKeys.Shift);
        }

        [Fact]
        public void FindConflicts_Should_Return_Conflicting_Bindings()
        {
            HotkeyBindingService.TryParse("Ctrl+Space", "A", out var a);
            HotkeyBindingService.TryParse("Ctrl+Space", "B", out var b);

            var conflicts = HotkeyBindingService.FindConflicts(a, b);

            conflicts.Should().NotBeEmpty();
            conflicts[0].Should().Contain("A");
            conflicts[0].Should().Contain("B");
        }
    }
}
