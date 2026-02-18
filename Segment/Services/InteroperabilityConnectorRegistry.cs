using System;
using System.Collections.Generic;
using System.Linq;

namespace Segment.App.Services
{
    public class InteroperabilityConnectorRegistry
    {
        private readonly IReadOnlyList<IInteroperabilityConnector> _connectors;

        public InteroperabilityConnectorRegistry(IEnumerable<IInteroperabilityConnector>? connectors = null)
        {
            _connectors = (connectors ?? new IInteroperabilityConnector[] { new TmxInteroperabilityConnector() })
                .Where(x => x != null)
                .ToList();
        }

        public IReadOnlyList<IInteroperabilityConnector> GetConnectors() => _connectors;

        public IInteroperabilityConnector ResolveForImport(string format)
        {
            var connector = _connectors.FirstOrDefault(x => x.CanImport(format));
            if (connector == null)
            {
                throw new InvalidOperationException($"No connector registered for import format '{format}'.");
            }

            return connector;
        }

        public IInteroperabilityConnector ResolveForExport(string format)
        {
            var connector = _connectors.FirstOrDefault(x => x.CanExport(format));
            if (connector == null)
            {
                throw new InvalidOperationException($"No connector registered for export format '{format}'.");
            }

            return connector;
        }
    }
}
