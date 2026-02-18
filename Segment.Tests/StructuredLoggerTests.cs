using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class StructuredLoggerTests
    {
        [Fact]
        public void Scrub_Should_Remove_Emails_And_Long_Numbers()
        {
            var logger = new StructuredLogger();
            string input = "Contact john.doe@example.com with case 123456789.";

            string scrubbed = logger.Scrub(input);

            scrubbed.Should().Contain("[email]");
            scrubbed.Should().Contain("[number]");
            scrubbed.Should().NotContain("john.doe@example.com");
            scrubbed.Should().NotContain("123456789");
        }
    }
}
