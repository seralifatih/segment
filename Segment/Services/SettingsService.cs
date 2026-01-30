using System.IO;
using System.Text.Json;

namespace Segment.App.Services
{
    public class AppConfig
    {
        public string TargetLanguage { get; set; } = "Turkish";

        // Seçenekler: "Google", "Ollama", "Custom"
        public string AiProvider { get; set; } = "Google";

        // --- Google ---
        public string GoogleApiKey { get; set; } = "";
        public string GoogleModel { get; set; } = "gemma-3-27b-it";

        // --- Ollama (Local) ---
        public string OllamaUrl { get; set; } = "http://localhost:11434/api/generate";
        public string OllamaModel { get; set; } = "llama3.2";

        // --- CUSTOM / OPENAI COMPATIBLE (YENİ) ---
        // DeepSeek, Groq, OpenRouter, OpenAI vb. hepsi buraya girer.
        public string CustomBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string CustomApiKey { get; set; } = "";
        public string CustomModel { get; set; } = "gpt-4o-mini";

        // --- First Run Flag ---
        public bool IsFirstRun { get; set; } = true;
    }

    public static class SettingsService
    {
        private static readonly string SettingsPath = "settings.json";
        public static AppConfig Current { get; private set; }

        static SettingsService() { Load(); }

        public static void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try { Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(SettingsPath)) ?? new AppConfig(); }
                catch { Current = new AppConfig(); }
            }
            else { Current = new AppConfig(); Save(); }
        }

        public static void Save()
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}