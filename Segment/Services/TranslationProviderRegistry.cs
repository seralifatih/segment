using System;
using System.Collections.Generic;
using System.Linq;

namespace Segment.App.Services
{
    public interface ITranslationProviderRegistry
    {
        void Register(ITranslationProvider provider);
        bool TryGet(string providerName, out ITranslationProvider provider);
        IReadOnlyList<ITranslationProvider> GetAll();
    }

    public class TranslationProviderRegistry : ITranslationProviderRegistry
    {
        private readonly Dictionary<string, ITranslationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

        public void Register(ITranslationProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(provider.Name)) throw new ArgumentException("Provider name is required.", nameof(provider));
            _providers[provider.Name.Trim()] = provider;
        }

        public bool TryGet(string providerName, out ITranslationProvider provider)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                provider = null!;
                return false;
            }

            return _providers.TryGetValue(providerName.Trim(), out provider!);
        }

        public IReadOnlyList<ITranslationProvider> GetAll()
        {
            return _providers.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
