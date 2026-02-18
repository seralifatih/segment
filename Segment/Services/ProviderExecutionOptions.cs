using System;

namespace Segment.App.Services
{
    public class ProviderExecutionOptions
    {
        public int? MaxRetriesOverride { get; set; }
        public TimeSpan? AttemptTimeoutOverride { get; set; }
    }
}
