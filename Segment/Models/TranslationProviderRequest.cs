using System.Collections.Generic;

namespace Segment.App.Models
{
    public class TranslationProviderRequest
    {
        public string InputText { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public string PromptPolicy { get; set; } = "";
        public IReadOnlyDictionary<string, string> GlossaryHints { get; set; } = new Dictionary<string, string>();
        public bool RequiresStreaming { get; set; }
        public bool IsShortSegmentMode { get; set; }
        public int RequestBudgetMs { get; set; } = 700;
    }
}
