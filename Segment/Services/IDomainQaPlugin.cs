using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IDomainQaPlugin
    {
        string PluginId { get; }
        IReadOnlyList<GuardrailResult> Evaluate(string sourceText, string translatedText, TranslationContext context);
    }
}
