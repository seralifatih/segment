using System;
using System.Windows;
using System.Windows.Threading;

namespace Segment.App.Services
{
    public static class LearningManager
    {
        private static DispatcherTimer? _timer;
        private static INotificationService _notificationService = new WpfNotificationService();

        // HAFIZA
        private static string _lastSourceText = "";
        private static string _lastAiOutput = "";
        private static string _lastClipboardContent = "";
        private static bool _isWaitingForFeedback = false;

        private const double MinSimilarity = 0.5;
        private const double MaxSimilarity = 0.99;

        // Allow tests to inject a mock notification service
        public static void SetNotificationService(INotificationService service)
        {
            _notificationService = service;
        }

        public static void Initialize(Window mainWindow)
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1); // 1 saniyede bir kontrol
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        public static void StartMonitoring(string source, string aiOutput)
        {
            _lastSourceText = source;
            _lastAiOutput = aiOutput;
            _lastClipboardContent = aiOutput.Trim();
            _isWaitingForFeedback = true;
        }

        // Expose for testing
        public static void ProcessUserEdit(string source, string aiOutput, string userOutput)
        {
            _lastSourceText = source;
            _lastAiOutput = aiOutput;
            _isWaitingForFeedback = true;
            AnalyzeChange(userOutput);
        }

        // Reset state for testing (does NOT reset notification service - use SetNotificationService separately)
        public static void ResetForTesting()
        {
            _lastSourceText = "";
            _lastAiOutput = "";
            _lastClipboardContent = "";
            _isWaitingForFeedback = false;
            // Don't reset notification service here - let tests set it explicitly
        }

        private static void OnTimerTick(object? sender, EventArgs e)
        {
            // Takip modunda değilsek hiç işlem yapma (Sıfır CPU kullanımı)
            if (!_isWaitingForFeedback) return;

            try
            {
                if (!System.Windows.Clipboard.ContainsText()) return;

                string currentClipboard = System.Windows.Clipboard.GetText().Trim();

                // Pano değişmediyse çık
                if (currentClipboard == _lastClipboardContent) return;

                // Değişikliği kaydet
                _lastClipboardContent = currentClipboard;

                // Kendi koyduğumuz metinse çık
                if (currentClipboard == _lastAiOutput.Trim()) return;

                // Analiz et
                AnalyzeChange(currentClipboard);
            }
            catch { }
        }

        private static void AnalyzeChange(string userText)
        {
            // Check if frozen (safe for tests where GlossaryService might not be fully initialized)
            if (GlossaryService.CurrentProfile?.IsFrozen == true)
            {
                return;
            }

            double similarity = StringHelper.CalculateSimilarity(_lastAiOutput, userText);

            // Filtreler
            if (similarity < MinSimilarity)
            {
                _isWaitingForFeedback = false; // Çok farklı, takibi bırak
                return;
            }

            if (similarity > MaxSimilarity) return; // Neredeyse aynı, devam et

            var change = TermDetective.Analyze(_lastSourceText, _lastAiOutput, userText);

            if (change != null)
            {
                change.FullSourceText = _lastSourceText;

                _notificationService.ShowToast(change);

                _isWaitingForFeedback = false;
            }
        }
    }
}