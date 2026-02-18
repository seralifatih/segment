namespace Segment.App.Models
{
    public enum NicheTelemetryEventType
    {
        TranslationRequested = 0,
        TranslationCompleted = 1,
        GuardrailBlocked = 2,
        GuardrailOverridden = 3,
        GlossaryTermApplied = 4,
        SuggestionAccepted = 5,
        SuggestionEdited = 6,
        PasteCompleted = 7,
        PasteReverted = 8
    }
}
