using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ITranslationGuardrailEngine
    {
        GuardrailValidationResult Validate(string sourceText, string translatedText, TranslationContext context);
    }
}
