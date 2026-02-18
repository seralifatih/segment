using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ITextRedactionService
    {
        RedactionResult Redact(string input);
        string Restore(string redactedText, RedactionResult mapping);
    }
}
