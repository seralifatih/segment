using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IAccountMetadataService
    {
        AccountMetadata GetOrCreate(string accountId, string? displayName = null);
        AccountMetadata SetPartnerTags(string accountId, IEnumerable<string> tags, string? displayName = null);
    }
}
