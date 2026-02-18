using System;

namespace Segment.App.Models
{
    public class TranslationProviderHealthSnapshot
    {
        public string ProviderName { get; set; } = "";
        public TranslationProviderHealthStatus Status { get; set; } = TranslationProviderHealthStatus.Unknown;
        public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = "";
    }
}
