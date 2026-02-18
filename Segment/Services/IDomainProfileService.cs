using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IDomainProfileService
    {
        IReadOnlyList<DomainProfile> GetProfiles();
        DomainProfile GetProfile(DomainVertical domain);
        DomainRulePack GetRulePack(DomainVertical domain);
    }
}
