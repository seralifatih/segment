using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TextRedactionServiceTests
    {
        [Fact]
        public void Redact_And_Restore_Should_Be_Reversible()
        {
            var service = new TextRedactionService();
            string input = "John Smith (ID AB-123456) email john.smith@example.com owes 1200.";

            var redacted = service.Redact(input);
            redacted.RedactedText.Should().NotContain("John Smith");
            redacted.RedactedText.Should().NotContain("AB-123456");
            redacted.RedactedText.Should().NotContain("john.smith@example.com");
            redacted.RedactedText.Should().NotContain("1200");
            redacted.TokenToOriginalMap.Count.Should().BeGreaterThan(0);

            string restored = service.Restore(redacted.RedactedText, redacted);
            restored.Should().Be(input);
        }
    }
}
