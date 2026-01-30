using System.Windows;
using System.Windows.Controls;
using Segment.App.Services;
using Microsoft.Win32; // Registry için şart

namespace Segment.App.Views
{
    public partial class SettingsWindow : Window
    {
        private const string RunRegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "SegmentApp";

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Pencereyi sürükleme özelliği
            this.MouseLeftButtonDown += (s, e) => { try { this.DragMove(); } catch { } };
        }

        private void LoadSettings()
        {
            // 1. Dil Seçimi
            LanguageCombo.Text = SettingsService.Current.TargetLanguage;

            // 2. Provider Seçimi
            switch (SettingsService.Current.AiProvider)
            {
                case "Ollama": ProviderCombo.SelectedIndex = 1; break;
                case "Custom": ProviderCombo.SelectedIndex = 2; break;
                default: ProviderCombo.SelectedIndex = 0; break; // Google
            }

            // Google Bilgileri
            GoogleApiKeyBox.Password = SettingsService.Current.GoogleApiKey;
            GoogleModelBox.Text = SettingsService.Current.GoogleModel;

            // Ollama Bilgileri
            OllamaUrlBox.Text = SettingsService.Current.OllamaUrl;
            OllamaModelBox.Text = SettingsService.Current.OllamaModel;

            // Custom Bilgileri
            CustomUrlBox.Text = SettingsService.Current.CustomBaseUrl;
            CustomApiKeyBox.Password = SettingsService.Current.CustomApiKey;
            CustomModelBox.Text = SettingsService.Current.CustomModel;

            // 3. Startup Durumu
            StartupBox.IsChecked = IsStartupEnabled();

            UpdatePanelVisibility();
        }

        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePanelVisibility();
        }

        private void UpdatePanelVisibility()
        {
            if (GooglePanel == null || OllamaPanel == null || CustomPanel == null) return;

            GooglePanel.Visibility = Visibility.Collapsed;
            OllamaPanel.Visibility = Visibility.Collapsed;
            CustomPanel.Visibility = Visibility.Collapsed;

            if (ProviderCombo.SelectedIndex == 0) GooglePanel.Visibility = Visibility.Visible;
            else if (ProviderCombo.SelectedIndex == 1) OllamaPanel.Visibility = Visibility.Visible;
            else if (ProviderCombo.SelectedIndex == 2) CustomPanel.Visibility = Visibility.Visible;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Dil
            SettingsService.Current.TargetLanguage = LanguageCombo.Text;

            // Provider
            if (ProviderCombo.SelectedIndex == 0) SettingsService.Current.AiProvider = "Google";
            else if (ProviderCombo.SelectedIndex == 1) SettingsService.Current.AiProvider = "Ollama";
            else SettingsService.Current.AiProvider = "Custom";

            // Detaylar
            SettingsService.Current.GoogleApiKey = GoogleApiKeyBox.Password;
            SettingsService.Current.GoogleModel = GoogleModelBox.Text;

            SettingsService.Current.OllamaUrl = OllamaUrlBox.Text;
            SettingsService.Current.OllamaModel = OllamaModelBox.Text;

            SettingsService.Current.CustomBaseUrl = CustomUrlBox.Text;
            SettingsService.Current.CustomApiKey = CustomApiKeyBox.Password;
            SettingsService.Current.CustomModel = CustomModelBox.Text;

            // Dosyaya Kaydet
            SettingsService.Save();

            // Startup Ayarını Uygula
            SetStartup(StartupBox.IsChecked == true);

            this.Close();
        }

        private void ImportTmx_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "TMX files (*.tmx)|*.tmx|All files (*.*)|*.*",
                Title = "Import TMX"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var terms = TmxImportService.Import(dialog.FileName, SettingsService.Current.TargetLanguage);
                int inserted = GlossaryService.AddTerms(terms, isGlobal: true);
                System.Windows.MessageBox.Show($"Imported {inserted} terms into Global profile.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"TMX import failed: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // --- HELPER METODLAR (Startup) ---

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    return key.GetValue(AppName) != null;
                }
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (enable)
                    {
                        string? exePath = Environment.ProcessPath;
                        if (exePath != null && exePath.EndsWith(".dll"))
                        {
                            exePath = exePath.Replace(".dll", ".exe");
                        }

                        if (exePath != null)
                        {
                            key.SetValue(AppName, $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                // HATA ÇÖZÜMÜ: Açıkça System.Windows.MessageBox kullanıyoruz
                System.Windows.MessageBox.Show($"Could not change startup settings: {ex.Message}");
            }
        }
    }
}