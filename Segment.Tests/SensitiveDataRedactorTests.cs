using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class SensitiveDataRedactorTests
    {
        [Fact]
        public void Redact_Should_Scrub_Known_Sensitive_Patterns()
        {
            string input =
                "Email john.doe@example.com phone +1 415-555-1212 ssn 123-45-6789 card 4111 1111 1111 1111 " +
                "Bearer abcdefghijklmnop api_key=secretvalue12345 account 998877665544.";

            string redacted = SensitiveDataRedactor.Redact(input);

            redacted.Should().NotContain("john.doe@example.com");
            redacted.Should().NotContain("415-555-1212");
            redacted.Should().NotContain("123-45-6789");
            redacted.Should().NotContain("4111 1111 1111 1111");
            redacted.Should().NotContain("abcdefghijklmnop");
            redacted.Should().NotContain("secretvalue12345");
            redacted.Should().NotContain("998877665544");

            redacted.Should().Contain("[email]");
            redacted.Should().Contain("[phone]");
            redacted.Should().Contain("[ssn]");
            redacted.Should().Contain("[card]");
            redacted.Should().Contain("[token]");
            redacted.Should().Contain("[secret]");
            redacted.Should().Contain("[number]");
        }
    }
}
