using System;
using System.Windows;
using Segment.App.Services;

namespace Segment.App.Views
{
    public partial class LearningWidget : Window
    {
        private DetectedChange _change;
        // Dilleri burada saklayalım
        private string _srcLang = "English"; // Varsayılan
        private string _trgLang = "Turkish";

        public LearningWidget(DetectedChange change)
        {
            InitializeComponent();
            _change = change;

            // 1. Dilleri Ayarlardan Çek
            // SettingsService'de SourceLanguage yoksa varsayılan English kabul edelim.
            // Ama TargetLanguage kesin var.
            var settings = SettingsService.Current;
            _trgLang = settings.TargetLanguage ?? "Turkish";

            // Eğer SourceLanguage ayarın yoksa şimdilik "English" kalsın veya "Auto" ise "English" yap.
            // İleride buraya detected language de gelebilir.
            _srcLang = "English";

            // 2. Arayüzü Güncelle (XAML'daki Label'lar)
            SourceLangLabel.Text = $"{_srcLang} (Source)";
            TargetLangLabel.Text = $"{_trgLang} (Target)";

            OldTermBox.Text = change.SourceTerm;
            NewTermBox.Text = change.NewTerm;

            ProjectNameText.Text = $"({GlossaryService.CurrentProfile.Name})";

            PositionWindow();
            AnimateEntry();

            // 3. Lemma İşlemini Başlat
            AutoLemmatize();
        }

        private async void AutoLemmatize()
        {
            OldTermBox.Opacity = 0.5;
            NewTermBox.Opacity = 0.5;

            // Dilleri de gönderiyoruz 🌍
            var result = await LemmaService.AlignAndLemmatizeAsync(
                _change.FullSourceText,
                _change.SourceTerm,
                _change.NewTerm,
                _srcLang,
                _trgLang
            );

            OldTermBox.Text = result.SourceLemma;
            NewTermBox.Text = result.TargetLemma;

            OldTermBox.Opacity = 1;
            NewTermBox.Opacity = 1;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string source = OldTermBox.Text.Trim();
            string target = NewTermBox.Text.Trim();

            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                bool isGlobal = ScopeGlobal.IsChecked == true;

                // Servise soruyoruz: "Bu terim yeni mi?"
                // Not: AddTerm metodunu "Check" modunda çağırmıyoruz, direkt eklemeye çalışıyoruz 
                // ama metodun mantığını "AddOrUpdate" yerine önce "Check" yapacak şekilde revize edelim mi?
                // Vibe Coding ruhuna uygun olarak: Servis zaten ekliyor. 
                // Biz burada UI tarafında "Kullanıcıya sormadan ekleme" mantığını kuralım.

                // ÖNCE KONTROL ET (Daha güvenli)
                var targetProfile = isGlobal ? GlossaryService.GlobalProfile : GlossaryService.CurrentProfile;
                var existing = targetProfile.Terms.FindById(source);
                bool exists = existing != null;

                if (exists)
                {
                    // Eski değeri alalım ki kullanıcı neyi değiştirdiğini bilsin
                    var oldVal = existing.Target;

                    // Eğer değer zaten aynıysa işlem yapma, kapat
                    if (oldVal.Equals(target, StringComparison.OrdinalIgnoreCase))
                    {
                        this.Close();
                        return;
                    }

                    var result = System.Windows.MessageBox.Show(
                        $"'{source}' is already defined as '{oldVal}' in {(isGlobal ? "Global" : "Project")} scope.\n\nOverwrite with '{target}'?",
                        "⚠️ Conflict Detected",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.No)
                    {
                        return; // İptal et, pencere açık kalsın
                    }
                }

                // Kullanıcı onayladı veya terim zaten yok -> Kaydet
                GlossaryService.AddTerm(source, target, isGlobal);
            }
            this.Close();
        }

        private void Ignore_Click(object sender, RoutedEventArgs e) => this.Close();

        private void PositionWindow()
        {
            var desktop = SystemParameters.WorkArea;
            this.Left = desktop.Right - this.Width - 20;
            this.Top = desktop.Bottom - this.Height - 20;
        }

        private void AnimateEntry()
        {
            this.Opacity = 0;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(0.3));
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}