using System.Threading;
using System.Threading.Tasks;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ITranslationProvider
    {
        string Name { get; }
        bool SupportsStreaming { get; }
        bool SupportsGlossaryHints { get; }

        Task<TranslationProviderResult> TranslateAsync(TranslationProviderRequest request, TranslationContext context, CancellationToken cancellationToken);
        Task<TranslationProviderHealthSnapshot> HealthCheckAsync(CancellationToken cancellationToken);
    }
}
