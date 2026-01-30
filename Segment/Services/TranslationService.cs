using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text.RegularExpressions; // Regex için gerekli
using Segment.App.Models; // TermEntry modeli için gerekli

namespace Segment.App.Services
{
    public static class TranslationService
    {
        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // --- ANA FONKSİYON ---
        public static async Task<string> SuggestAsync(string input)
        {
            var settings = SettingsService.Current;
            string targetLang = settings.TargetLanguage;

            // 1. Terminoloji Hafızasını Hazırla (GLOBAL + PROJE)
            string glossaryNotes = BuildGlossaryPrompt(input);

            // 2. Seçilen Sağlayıcıya Göre Yönlendir
            switch (settings.AiProvider)
            {
                case "Ollama":
                    return await TryOllamaAsync(input, targetLang, settings.OllamaUrl, settings.OllamaModel, glossaryNotes);

                case "Custom":
                    return await TryCustomAsync(input, targetLang, settings.CustomBaseUrl, settings.CustomApiKey, settings.CustomModel, glossaryNotes);

                case "Google":
                default:
                    return await TryGoogleAsync(input, targetLang, settings.GoogleApiKey, settings.GoogleModel, glossaryNotes);
            }
        }

        // --- YARDIMCI: Hafızayı Prompt'a Dönüştür ---
        private static string BuildGlossaryPrompt(string input)
        {
            // KRİTİK DÜZELTME: Sadece CurrentProfile değil, EffectiveTerms (Global + Proje) çağrılıyor.
            var terms = GlossaryService.GetEffectiveTerms();

            if (terms.Count == 0) return "";

            StringBuilder sb = new StringBuilder();
            bool hasRelevantTerms = false;

            foreach (var kvp in terms)
            {
                string sourceLemma = kvp.Key;   // Örn: "submit"
                TermEntry entry = kvp.Value;    // Örn: Target="ilet"

                // KRİTİK DÜZELTME: Regex \b ile kelime sınırı koruması (art != article)
                string pattern = $@"\b{Regex.Escape(sourceLemma)}\b";

                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    sb.AppendLine($"- Concept: '{sourceLemma}' MUST be translated as '{entry.Target}' (inflect as needed).");
                    hasRelevantTerms = true;
                }
            }

            if (hasRelevantTerms)
            {
                return "\n\nTERMINOLOGY INSTRUCTIONS:\n" +
                       "Apply the following terminology rules. Input matches are based on LEMMA forms.\n" +
                       "If the input contains a variation (plural, conjugated), convert it to the TARGET root and re-inflect.\n" +
                       sb.ToString();
            }

            return "";
        }

        // --- 1. GOOGLE GEMINI (Cloud) ---
        private static async Task<string> TryGoogleAsync(string input, string lang, string apiKey, string model, string glossaryNotes)
        {
            if (string.IsNullOrEmpty(apiKey)) return "ERROR: Google API Key is missing in Settings.";

            int maxRetries = 2;
            int delay = 1000;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    string fullPrompt = $"You are a professional translator. Translate this text into {lang}. Output ONLY the translation, no notes.{glossaryNotes}\n\nText: {input}";

                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new { role = "user", parts = new[] { new { text = fullPrompt } } }
                        }
                    };

                    string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    string jsonContent = JsonSerializer.Serialize(requestBody);

                    var response = await httpClient.PostAsync(url, new StringContent(jsonContent, Encoding.UTF8, "application/json"));

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (i < maxRetries) { await Task.Delay(delay); delay *= 2; continue; }
                        return "ERROR: Google Rate Limit Exceeded (429)";
                    }

                    if (!response.IsSuccessStatusCode) return $"ERROR: Google API ({response.StatusCode})";

                    string responseString = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(responseString);

                    if (doc.RootElement.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        if (parts.GetArrayLength() > 0)
                            return parts[0].GetProperty("text").GetString()?.Trim() ?? "";
                    }
                    return "ERROR: Empty response from Google.";
                }
                catch (Exception ex) { if (i == maxRetries) return $"ERROR: {ex.Message}"; }
            }
            return "ERROR: Connection failed.";
        }

        // --- 2. OLLAMA (Local / Offline) ---
        private static async Task<string> TryOllamaAsync(string input, string lang, string url, string model, string glossaryNotes)
        {
            try
            {
                if (!url.EndsWith("/api/generate") && !url.EndsWith("/api/chat"))
                    url = url.TrimEnd('/') + "/api/generate";

                string fullPrompt = $"Translate the following text into {lang}. Do not explain. Output only the translation.{glossaryNotes}\n\nText: {input}";

                var requestBody = new
                {
                    model = model,
                    prompt = fullPrompt,
                    stream = false,
                    options = new { temperature = 0.0 }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode) return $"ERROR: Ollama connection failed ({response.StatusCode}).";

                string responseString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("response", out JsonElement val))
                    return val.GetString()?.Trim() ?? "";

                return "ERROR: Unexpected Ollama response format.";
            }
            catch (Exception ex) { return $"ERROR: Is Ollama running? ({ex.Message})"; }
        }

        // --- 3. CUSTOM / OPENAI COMPATIBLE ---
        private static async Task<string> TryCustomAsync(string input, string lang, string baseUrl, string apiKey, string model, string glossaryNotes)
        {
            try
            {
                string endpoint = baseUrl.TrimEnd('/');
                if (!endpoint.EndsWith("/chat/completions")) endpoint += "/chat/completions";

                string systemContent = $"You are a professional translator. Translate the user's input into {lang}. Output ONLY the translated text.{glossaryNotes}";

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemContent },
                        new { role = "user", content = input }
                    },
                    temperature = 0.3
                };

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return $"ERROR: Custom API ({response.StatusCode})";

                string responseString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                {
                    var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                    return content?.Trim() ?? "";
                }
                return "ERROR: Empty response from Custom API.";
            }
            catch (Exception ex) { return $"ERROR: {ex.Message}"; }
        }
    }
}