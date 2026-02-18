using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ITranslationQaService
    {
        GuardrailValidationResult Evaluate(string sourceText, string translatedText, TranslationContext context);
    }
}
