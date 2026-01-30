using Xunit;

namespace Segment.Tests
{
    // This collection ensures that all tests that use GlossaryService run sequentially
    // to avoid conflicts with the shared database state
    [CollectionDefinition("Database Tests", DisableParallelization = true)]
    public class DatabaseTestCollection
    {
    }
}
