using System.Text.RegularExpressions;

namespace Segment.App.Services
{
    public static class SensitiveDataRedactor
    {
        private static readonly Regex EmailRegex = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LongNumberRegex = new(@"\b\d{5,}\b", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"\b(?:\+?\d{1,3}[-.\s]?)?(?:\(?\d{2,4}\)?[-.\s])\d{3}[-.\s]\d{4}\b", RegexOptions.Compiled);
        private static readonly Regex SsnRegex = new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);
        private static readonly Regex CreditCardRegex = new(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled);
        private static readonly Regex BearerRegex = new(@"(?i)\bBearer\s+[A-Za-z0-9\-._~+/]+=*\b", RegexOptions.Compiled);
        private static readonly Regex ApiKeyRegex = new(@"(?i)\b(api[_-]?key|token|secret)\b\s*[:=]\s*['""]?[A-Za-z0-9\-_]{8,}['""]?", RegexOptions.Compiled);

        public static string Redact(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string scrubbed = value;
            scrubbed = EmailRegex.Replace(scrubbed, "[email]");
            scrubbed = SsnRegex.Replace(scrubbed, "[ssn]");
            scrubbed = CreditCardRegex.Replace(scrubbed, "[card]");
            scrubbed = PhoneRegex.Replace(scrubbed, "[phone]");
            scrubbed = BearerRegex.Replace(scrubbed, "Bearer [token]");
            scrubbed = ApiKeyRegex.Replace(scrubbed, "[secret]");
            scrubbed = LongNumberRegex.Replace(scrubbed, "[number]");
            return scrubbed;
        }
    }
}
