using System;
using System.IO;
using System.Drawing; // Bitmap, Graphics, Icon için
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms; // NotifyIcon için
using System.Linq;
using System.Collections.Generic;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

using NHotkey;
using NHotkey.Wpf;
using Segment.App.Views;
using Segment.App.Services;
using Segment.App.Models;
using System.Windows.Input;

namespace Segment.App
{
    public partial class App : Application
    {
        private FloatingPanel? panel;
        private NotifyIcon? _notifyIcon;
        private readonly string _crashLogPath;
        private Mutex? _singleInstanceMutex;

        // Desteklediğimiz diller listesi
        private readonly string[] commonLanguages = { "Turkish", "English", "German", "French", "Spanish", "Russian", "Japanese" };

        public App()
        {
            // Setup crash log path
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(appDataPath);
            _crashLogPath = Path.Combine(appDataPath, "crash_log.txt");

            // Subscribe to global exception handlers (Black Box)
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ========================================
            // TASK 1: Single Instance Lock (Mutex)
            // ========================================
            const string mutexName = "Global\\SegmentAppLock";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    "Segment is already running in the System Tray.",
                    "Segment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                Shutdown();
                return;
            }

            // ========================================
            // TASK 4: Set ShutdownMode to OnExplicitShutdown
            // ========================================
            // This prevents the app from closing when the welcome window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load settings and initialize services
            SettingsService.Load(); // Veya SettingsService.Instance.Load()
            EnsureActiveDomainLoaded();
            SetupTrayIcon();
            _ = Task.Run(() => TranslationService.WarmupSelectedProviderAsync());

            // ========================================
            // TASK 3: Auto-Update Check (Fire-and-forget)
            // ========================================
            _ = Task.Run(() => new Segment.Services.UpdateService().CheckForUpdatesAsync());

            // Register global hotkey
            try
            {
                RegisterConfiguredHotkeys();
            }
            catch { }

            // ========================================
            // TASK 4: First Run Experience
            // ========================================
            if (SettingsService.Current.IsFirstRun)
            {
                WelcomeWindow welcomeWindow = new WelcomeWindow();
                welcomeWindow.Closed += (s, args) =>
                {
                    _notifyIcon?.ShowBalloonTip(
                        3000,
                        "Segment is Ready 🚀",
                        "I'm running silently in the background.\nPress Ctrl+Space to translate!",
                        ToolTipIcon.Info
                    );
                };
                welcomeWindow.Show();
            }
            // If not first run, app starts silently in system tray
        }

        #region Black Box - Crash Logging

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception, "DispatcherUnhandledException");
            e.Handled = false; // Let the app crash after logging
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash(e.Exception, "UnobservedTaskException");
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex, "AppDomainUnhandledException");
            }
        }

        private void LogCrash(Exception ex, string source)
        {
            if (!SettingsService.Current.TelemetryCrashDiagnosticsConsent)
            {
                return;
            }

            try
            {
                bool minimize = SettingsService.Current.MinimizeDiagnosticLogging
                    || SettingsService.Current.ConfidentialProjectLocalOnly
                    || string.Equals(SettingsService.Current.ConfidentialityMode, "LocalOnly", StringComparison.OrdinalIgnoreCase);

                string safeMessage = SensitiveDataRedactor.Redact(ex.Message ?? string.Empty);
                string safeStack = SensitiveDataRedactor.Redact(ex.StackTrace ?? string.Empty);
                string safeInner = SensitiveDataRedactor.Redact(ex.InnerException?.ToString() ?? "None");

                string crashDetails =
                    "==============================\n" +
                    "CRASH REPORT\n" +
                    "==============================\n" +
                    $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Source:    {source}\n" +
                    $"Message:   {safeMessage}\n\n" +
                    "Stack Trace:\n" +
                    $"{(minimize ? "[omitted by privacy mode]" : safeStack)}\n\n" +
                    "Inner Exception:\n" +
                    $"{(minimize ? "[omitted by privacy mode]" : safeInner)}\n" +
                    "==============================\n\n";

                File.AppendAllText(_crashLogPath, crashDetails);

                _notifyIcon?.ShowBalloonTip(
                    3000,
                    "Segment Crashed",
                    "An error occurred. Details saved to crash_log.txt",
                    ToolTipIcon.Error
                );
            }
            catch
            {
                // If logging fails, we can't do much about it
            }
        }

        #endregion

        #region Procedural Tray Icon

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = GeneratePlaceholderIcon();
            _notifyIcon.Text = "Segment (Ctrl+Space)";
            _notifyIcon.Visible = true;

            // Menüyü oluştur
            RefreshTrayMenu();

            // Çift tık -> Ayarlar
            _notifyIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private Icon GeneratePlaceholderIcon()
        {
            using (Bitmap bitmap = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Draw a purple background
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(138, 43, 226))) // BlueViolet
                {
                    g.FillRectangle(bgBrush, 0, 0, 16, 16);
                }

                // Draw a white box/square in the center to represent "Segment"
                using (SolidBrush fgBrush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(fgBrush, 4, 4, 8, 8);
                }

                // Draw a simple border
                using (Pen borderPen = new Pen(Color.FromArgb(100, 30, 180), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, 15, 15);
                }

                // Convert bitmap to Icon
                IntPtr hIcon = bitmap.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);
                
                // Note: We return the icon but don't dispose it here
                // The icon will be disposed when the NotifyIcon is disposed
                return icon;
            }
        }

        #endregion

        // Menüyü dinamik olarak oluşturuyoruz ki dil değişince tik işareti güncellensin
        public void RefreshTrayMenu()
        {
            if (_notifyIcon == null) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            string currentLang = SettingsService.Current.TargetLanguage;

            // --- 1. TARGET LANGUAGE MENU ---
            ToolStripMenuItem langMenu = new ToolStripMenuItem("Target Language");

            // ÖZEL DURUM: Eğer kullanıcının seçtiği dil standart listede yoksa,
            // onu en başa 'Custom' olarak ekle.
            bool isCustom = !commonLanguages.Contains(currentLang);

            if (isCustom && !string.IsNullOrEmpty(currentLang))
            {
                ToolStripMenuItem customItem = new ToolStripMenuItem($"Currently: {currentLang}");
                customItem.Checked = true;
                // Tıklayınca bir şey yapmasına gerek yok, zaten seçili.
                langMenu.DropDownItems.Add(customItem);
                langMenu.DropDownItems.Add(new ToolStripSeparator());
            }

            // Standart Listeyi Döngüyle Ekle
            foreach (var lang in commonLanguages)
            {
                ToolStripMenuItem langItem = new ToolStripMenuItem(lang);

                // Eğer standart dillerden biri seçiliyse tik koy
                if (currentLang == lang)
                {
                    langItem.Checked = true;
                }

                langItem.Click += (s, e) => ChangeLanguageFromTray(lang);
                langMenu.DropDownItems.Add(langItem);
            }

            menu.Items.Add(langMenu);

            // --- SEPARATOR ---
            menu.Items.Add(new ToolStripSeparator());

            // --- 2. SETTINGS ---
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("Settings Window...");
            settingsItem.Click += (s, e) => OpenSettings();
            menu.Items.Add(settingsItem);

            // --- 3. EXIT ---
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ShutdownApp();
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void ChangeLanguageFromTray(string newLang)
        {
            // 1. Ayarı güncelle
            SettingsService.Current.TargetLanguage = newLang;
            SettingsService.Save();

            // 2. Menüyü yenile (Tik işaretini güncellemek için)
            RefreshTrayMenu();
        }

        private void OpenSettings()
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is SettingsWindow)
                {
                    win.Activate();
                    return;
                }
            }

            SettingsWindow settingsWin = new SettingsWindow();

            // Ayarlar penceresi kapandığında Tray menüsünü güncelle
            // (Çünkü kullanıcı pencereden dili değiştirmiş olabilir)
            settingsWin.Closed += (s, e) => RefreshTrayMenu();

            settingsWin.Show();
        }

        private void ShutdownApp()
        {
            _notifyIcon?.Dispose();
            Shutdown();
        }

        private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            e.Handled = true;
            if (panel == null)
            {
                panel = new FloatingPanel();
                panel.Closed += (s, args) => panel = null;
            }
            panel.HandleSmartHotkey();
        }

        private void OnTranslateSelectionInPlaceHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            e.Handled = true;
            if (panel == null)
            {
                panel = new FloatingPanel();
                panel.Closed += (s, args) => panel = null;
            }

            panel.HandleTranslateSelectionInPlaceFallback();
        }

        private void RegisterConfiguredHotkeys()
        {
            try
            {
                HotkeyManager.Current.Remove("ShowSegment");
                HotkeyManager.Current.Remove("TranslateSelectionInPlace");
            }
            catch
            {
                // Best effort remove before replace.
            }

            bool showValid = HotkeyBindingService.TryParse(
                SettingsService.Current.ShowPanelHotkey,
                "ShowSegment",
                out HotkeyBinding showBinding);
            bool inPlaceValid = HotkeyBindingService.TryParse(
                SettingsService.Current.TranslateSelectionInPlaceHotkey,
                "TranslateSelectionInPlace",
                out HotkeyBinding inPlaceBinding);

            if (!showValid)
            {
                showBinding = new HotkeyBinding { Name = "ShowSegment", Key = Key.Space, Modifiers = ModifierKeys.Control };
                SettingsService.Current.ShowPanelHotkey = "Ctrl+Space";
            }

            if (!inPlaceValid)
            {
                inPlaceBinding = new HotkeyBinding { Name = "TranslateSelectionInPlace", Key = Key.Space, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
                SettingsService.Current.TranslateSelectionInPlaceHotkey = "Ctrl+Shift+Space";
            }

            IReadOnlyList<string> conflicts = HotkeyBindingService.FindConflicts(showBinding, inPlaceBinding);
            if (conflicts.Count > 0)
            {
                showBinding = new HotkeyBinding { Name = "ShowSegment", Key = Key.Space, Modifiers = ModifierKeys.Control };
                inPlaceBinding = new HotkeyBinding { Name = "TranslateSelectionInPlace", Key = Key.Space, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
                SettingsService.Current.ShowPanelHotkey = "Ctrl+Space";
                SettingsService.Current.TranslateSelectionInPlaceHotkey = "Ctrl+Shift+Space";
                _notifyIcon?.ShowBalloonTip(
                    4000,
                    "Segment Hotkey Conflict",
                    "Configured hotkeys conflicted. Defaults were restored.",
                    ToolTipIcon.Warning);
            }

            SettingsService.Save();
            HotkeyManager.Current.AddOrReplace("ShowSegment", showBinding.Key, showBinding.Modifiers, OnHotkeyPressed);
            HotkeyManager.Current.AddOrReplace("TranslateSelectionInPlace", inPlaceBinding.Key, inPlaceBinding.Modifiers, OnTranslateSelectionInPlaceHotkeyPressed);
        }

        public void RefreshHotkeys()
        {
            RegisterConfiguredHotkeys();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }

        private static void EnsureActiveDomainLoaded()
        {
            var domainService = new DomainProfileService();
            if (!Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical configuredDomain))
            {
                configuredDomain = DomainVertical.Legal;
            }

            var resolvedProfile = domainService.GetProfile(configuredDomain);
            if (!string.Equals(SettingsService.Current.ActiveDomain, resolvedProfile.Id.ToString(), StringComparison.Ordinal))
            {
                SettingsService.Current.ActiveDomain = resolvedProfile.Id.ToString();
                SettingsService.Save();
            }
        }
    }
}

