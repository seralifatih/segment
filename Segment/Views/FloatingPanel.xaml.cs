using System;
using System.Windows;
using System.Windows.Input;
using Segment.App.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Clipboard = System.Windows.Clipboard;
using Segment.App.Models;

namespace Segment.App.Views
{
    public partial class FloatingPanel : Window
    {
        private string _sourceText = "";
        private string _aiRawOutput = "";

        public FloatingPanel()
        {
            InitializeComponent();

            this.PreviewKeyDown += Window_PreviewKeyDown;
            this.MouseLeftButtonDown += (s, e) => { try { this.DragMove(); } catch { } };

            LoadProfiles();
            this.SourceInitialized += (s, e) => LearningManager.Initialize(this);
        }

        private void LoadProfiles()
        {
            ProfileCombo.ItemsSource = GlossaryService.Profiles.Select(p => p.Name).ToList();
            ProfileCombo.SelectedItem = GlossaryService.CurrentProfile.Name;

            // YENİ: Profil yüklendiğinde Freeze durumunu güncelle
            UpdateFreezeState();
        }

        private void ProfileCombo_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string text = ProfileCombo.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    GlossaryService.GetOrCreateProfile(text);
                    LoadProfiles();
                    ProfileCombo.SelectedItem = text;
                    InputText.Focus();

                    // YENİ: Yeni profil oluşturulunca durumunu kontrol et
                    UpdateFreezeState();
                }
            }
        }

        private void ProfileCombo_DropDownClosed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ProfileCombo.Text))
            {
                GlossaryService.GetOrCreateProfile(ProfileCombo.Text);
                // YENİ: Seçim değişince buton durumunu güncelle
                UpdateFreezeState();
            }
        }

        // --- YENİ: FREEZE MODE MANTIĞI 🧊 ---
        private void UpdateFreezeState()
        {
            if (GlossaryService.CurrentProfile != null)
            {
                // Butonu profilin kayıtlı durumuna göre ayarla
                FreezeButton.IsChecked = GlossaryService.CurrentProfile.IsFrozen;
            }
        }

        private void FreezeButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = GlossaryService.CurrentProfile;
            if (profile == null) return;

            // Durumu güncelle (Mavi basılıysa dondur, değilse çöz)
            profile.IsFrozen = FreezeButton.IsChecked == true;

            // Kaydet
            GlossaryService.SaveProfile(profile);
        }
        // -------------------------------------

        public async void HandleSmartHotkey()
        {
            try
            {
                if (this.IsVisible)
                {
                    CopyResultAndClose();
                    return;
                }

                string clipboardText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";

                this.Show();
                this.Activate();
                TermPanel.Visibility = Visibility.Collapsed;
                TermList.ItemsSource = null;

                // Her açılışta buton durumunu tazele (Garanti olsun)
                UpdateFreezeState();

                InputText.Text = clipboardText;
                InputText.Focus();
                InputText.SelectAll();

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    await PerformTranslationAsync(clipboardText);
                }
            }
            catch { SetEmptyState(); }
        }

        private async Task PerformTranslationAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _sourceText = text;
                OutputText.Text = "AI is translating...";
                OutputText.Opacity = 0.6;

                string result = await TranslationService.SuggestAsync(text);

                _aiRawOutput = result;
                OutputText.Text = result;

                CheckAndDisplayTerms(text);
            }
            catch (Exception ex) { OutputText.Text = $"ERROR: {ex.Message}"; }
            finally { OutputText.Opacity = 1.0; }
        }

        private void CheckAndDisplayTerms(string sourceText)
        {
            // EffectiveTerms kullanarak hem Global hem Proje kurallarını gösteriyoruz
            var allTerms = GlossaryService.GetEffectiveTerms();
            if (allTerms.Count == 0) return;

            var detectedTerms = new List<TermDisplay>();

            foreach (var kvp in allTerms)
            {
                // Basit contains kontrolü (Performans için)
                if (sourceText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    detectedTerms.Add(new TermDisplay
                    {
                        Source = kvp.Key,
                        Target = kvp.Value.Target
                    });
                }
            }

            if (detectedTerms.Count > 0)
            {
                TermList.ItemsSource = detectedTerms;
                TermPanel.Visibility = Visibility.Visible;
            }
        }

        private void CopyResultAndClose()
        {
            string finalUserOutput = OutputText.Text;

            if (!string.IsNullOrWhiteSpace(finalUserOutput) && !finalUserOutput.StartsWith("ERROR"))
            {
                try
                {
                    Clipboard.SetText(finalUserOutput);
                }
                catch { }

                LearningManager.StartMonitoring(_sourceText, finalUserOutput);

                this.Hide();
            }
        }

        private void SetEmptyState()
        {
            InputText.Text = "";
            OutputText.Text = "Ready for input...";
            InputText.Focus();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
            LoadProfiles();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopyResultAndClose();
                e.Handled = true;
            }
        }

        private async void InputText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                await PerformTranslationAsync(InputText.Text);
            }
        }
    }
}