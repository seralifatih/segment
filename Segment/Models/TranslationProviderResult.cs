namespace Segment.App.Models
{
    public class TranslationProviderResult
    {
        public bool Success { get; set; }
        public string OutputText { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public string ProviderUsed { get; set; } = "";
        public bool UsedFallbackProvider { get; set; }
        public bool BudgetEnforced { get; set; }
        public bool BudgetExceeded { get; set; }
        public double ProviderRoundtripMs { get; set; }

        public static TranslationProviderResult Ok(string output)
        {
            return new TranslationProviderResult
            {
                Success = true,
                OutputText = output ?? string.Empty
            };
        }

        public static TranslationProviderResult Fail(string error)
        {
            return new TranslationProviderResult
            {
                Success = false,
                ErrorMessage = error ?? "Provider request failed."
            };
        }
    }
}
