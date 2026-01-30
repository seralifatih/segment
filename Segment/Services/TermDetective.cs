using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Segment.App.Services
{
    // Değişiklik verisini taşıyan paket
    public class DetectedChange
    {
        public string SourceTerm { get; set; } = "";
        public string OldTerm { get; set; } = "";
        public string NewTerm { get; set; } = "";
        public string FullSourceText { get; set; } = ""; // Alignment için şart
    }

    public static class TermDetective
    {
        public static DetectedChange? Analyze(string source, string aiOutput, string userOutput)
        {
            // 1. Basit Eşitlik ve Boşluk Kontrolü
            if (string.IsNullOrWhiteSpace(aiOutput) || string.IsNullOrWhiteSpace(userOutput)) return null;
            if (aiOutput.Trim() == userOutput.Trim()) return null;

            // 2. Tokenize (Kelimelere böl - Türkçe karakter destekli)
            var aiWords = Tokenize(aiOutput);
            var userWords = Tokenize(userOutput);

            // --- AKILLI DIFF ALGORİTMASI 🧠 ---
            // Sadece kelime sayısı eşitliğine bakmak yerine,
            // cümlenin başını ve sonunu eşleştirip ortadaki farkı buluyoruz.

            int startMatch = 0;
            int endMatch = 0;

            // A. Baştan ne kadar uyuşuyor?
            int minLen = Math.Min(aiWords.Count, userWords.Count);
            while (startMatch < minLen &&
                   aiWords[startMatch].Equals(userWords[startMatch], StringComparison.OrdinalIgnoreCase))
            {
                startMatch++;
            }

            // B. Sondan ne kadar uyuşuyor? (Baştan uyuşanlara bindirmeden)
            while (endMatch < (minLen - startMatch) &&
                   aiWords[aiWords.Count - 1 - endMatch].Equals(userWords[userWords.Count - 1 - endMatch], StringComparison.OrdinalIgnoreCase))
            {
                endMatch++;
            }

            // C. Ortada kalan farkı çıkar
            // Örn: AI=[Lütfen, hemen, gönderin]  User=[Lütfen, iletin]
            // startMatch=1 (Lütfen), endMatch=0
            // AI Diff = "hemen gönderin"
            // User Diff = "iletin"

            var aiDiff = aiWords.Skip(startMatch).Take(aiWords.Count - startMatch - endMatch).ToList();
            var userDiff = userWords.Skip(startMatch).Take(userWords.Count - startMatch - endMatch).ToList();

            // Güvenlik: Eğer fark çok büyükse (cümlenin yarısından fazlası değiştiyse) widget açma.
            // Bu "Terminoloji" değil "Yeniden Yazma"dır.
            if (aiDiff.Count > 4 || userDiff.Count > 4) return null;

            // Fark yoksa çık
            if (aiDiff.Count == 0 && userDiff.Count == 0) return null;

            // Listeyi stringe çevir
            string oldTerm = string.Join(" ", aiDiff);
            string newTerm = string.Join(" ", userDiff);

            // BINGO!
            return new DetectedChange
            {
                SourceTerm = oldTerm, // Geçici kaynak (LemmaService düzeltecek)
                OldTerm = oldTerm,
                NewTerm = newTerm,
                FullSourceText = source
            };
        }

        // --- YARDIMCI METODLAR ---

        private static List<string> Tokenize(string text)
        {
            // Türkçe karakterleri (ğ, ş, ı, ö, ü, ç) ve kesme işaretini koruyan Regex
            return Regex.Split(text.Trim(), @"[^\wçğıöşüÇĞIÖŞÜ']+")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
        }
    }
}