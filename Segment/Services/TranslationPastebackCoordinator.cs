using Segment.App.Models;
using System.Linq;

namespace Segment.App.Services
{
    public class TranslationPastebackCoordinator
    {
        private readonly ITranslationGuardrailEngine _guardrailEngine;
        private readonly ITranslationQaService _qaService;

        public TranslationPastebackCoordinator(ITranslationGuardrailEngine guardrailEngine)
            : this(guardrailEngine, new TranslationQaService())
        {
        }

        public TranslationPastebackCoordinator(ITranslationGuardrailEngine guardrailEngine, ITranslationQaService qaService)
        {
            _guardrailEngine = guardrailEngine;
            _qaService = qaService;
        }

        public PastebackDecision Evaluate(string sourceText, string translatedText, TranslationContext context)
        {
            GuardrailValidationResult domainValidation = _guardrailEngine.Validate(sourceText, translatedText, context);
            GuardrailValidationResult qaValidation = _qaService.Evaluate(sourceText, translatedText, context);
            var merged = domainValidation.Results
                .Concat(qaValidation.Results)
                .ToList();

            return new PastebackDecision
            {
                Validation = new GuardrailValidationResult { Results = merged }
            };
        }
    }
}
