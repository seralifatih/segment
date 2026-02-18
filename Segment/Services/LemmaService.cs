using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Segment.App.Services
{
    public static class LemmaService
    {
        // ARTIK DİL BİLGİSİNİ PARAMETRE OLARAK ALIYORUZ
        public static async Task<(string SourceLemma, string TargetLemma)> AlignAndLemmatizeAsync(string originalContext, string oldTerm, string newTerm, string srcLang, string trgLang)
        {
            try
            {
                string safeContext = PromptSafetySanitizer.SanitizeUntrustedSourceText(originalContext);
                string safeOldTerm = PromptSafetySanitizer.SanitizeGlossaryConstraint(oldTerm);
                string safeNewTerm = PromptSafetySanitizer.SanitizeGlossaryConstraint(newTerm);

                // Prompt artık dinamik: {srcLang} -> {trgLang}
                string prompt = $@"
Act as a strictly bilingual linguistic alignment engine.
User corrected a translation from {srcLang} to {trgLang}.

Original {srcLang} Sentence: ""{safeContext}""
Old {trgLang} Translation Chunk: ""{safeOldTerm}""
New {trgLang} Corrected Chunk: ""{safeNewTerm}""

TASK:
1. Identify the CORE terminology change.
2. SOURCE must be the {srcLang} word/phrase from the Original Sentence.
3. TARGET must be the {trgLang} lemma from the New Corrected Chunk.
4. EXCLUDE modifiers unless they are part of a compound noun.

CRITICAL: 
- 'source_lemma' MUST BE {srcLang}.
- 'target_lemma' MUST BE {trgLang}.

Return JSON ONLY:
{{
  ""source_lemma"": ""string"",
  ""target_lemma"": ""string""
}}
";
                string jsonResponse = await TranslationService.SuggestAsync(prompt);

                // Temizlik
                jsonResponse = jsonResponse.Replace("```json", "").Replace("```", "").Trim();
                if (jsonResponse.Contains("{") && jsonResponse.Contains("}"))
                {
                    int start = jsonResponse.IndexOf("{");
                    int end = jsonResponse.LastIndexOf("}");
                    jsonResponse = jsonResponse.Substring(start, end - start + 1);
                }

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);

                // JSON Key'leri artık evrensel (source_lemma, target_lemma)
                string sLemma = PromptSafetySanitizer.SanitizeGlossaryConstraint(doc.RootElement.GetProperty("source_lemma").GetString() ?? safeOldTerm);
                string tLemma = PromptSafetySanitizer.SanitizeGlossaryConstraint(doc.RootElement.GetProperty("target_lemma").GetString() ?? safeNewTerm);

                return (sLemma.ToLower(), tLemma.ToLower());
            }
            catch
            {
                return (oldTerm, newTerm);
            }
        }
    }
}
