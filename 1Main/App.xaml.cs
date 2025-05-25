using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace YafesV2
{
    /// <summary>
    /// App.xaml için etkileşim mantığı
    /// YafesV2 uygulamasının ana giriş noktası
    /// </summary>
    public partial class App : Application
    {
        #region Uygulama Yaşam Döngüsü

        /// <summary>
        /// Uygulama başlatıldığında çalışır
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global hata yakalama ayarla
            SetupGlobalExceptionHandling();

            // Uygulama dizinlerini oluştur
            CreateApplicationFolders();

            // Uygulama başlangıcını logla
            LogApplicationStart();
        }

        /// <summary>
        /// Uygulama kapanırken çalışır
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            // Temizlik işlemleri
            LogApplicationExit();

            base.OnExit(e);
        }

        #endregion

        #region Global Hata Yönetimi

        /// <summary>
        /// Global hata yakalama sistemini kurar
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            // UI thread hataları
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Background thread hataları
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// UI thread'de oluşan hataları yakalar
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                string errorMessage = $"Beklenmeyen hata oluştu:\n\n{e.Exception.Message}";

                // Hatayı logla
                LogError(e.Exception);

                // Kullanıcıya göster
                MessageBox.Show(errorMessage, "YafesV2 - Hata",
                              MessageBoxButton.OK, MessageBoxImage.Error);

                // Uygulamanın çökmesini engelle
                e.Handled = true;
            }
            catch (Exception ex)
            {
                // En son çare - kritik hata
                MessageBox.Show($"Kritik sistem hatası: {ex.Message}",
                              "YafesV2 - Kritik Hata",
                              MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }

        /// <summary>
        /// Background thread'lerde oluşan hataları yakalar
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogError(ex);

                    MessageBox.Show($"Sistem hatası: {ex.Message}",
                                  "YafesV2 - Sistem Hatası",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch
            {
                // Son çare
                MessageBox.Show("Bilinmeyen kritik hata oluştu.",
                              "YafesV2 - Kritik Hata",
                              MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }

        #endregion

        #region Dosya ve Dizin Yönetimi

        /// <summary>
        /// Uygulama için gerekli klasörleri oluşturur
        /// </summary>
        private void CreateApplicationFolders()
        {
            try
            {
                string appDataPath = GetAppDataPath();

                // Gerekli klasörleri oluştur
                Directory.CreateDirectory(Path.Combine(appDataPath, "Logs"));
                Directory.CreateDirectory(Path.Combine(appDataPath, "Config"));
                Directory.CreateDirectory(Path.Combine(appDataPath, "Temp"));
                Directory.CreateDirectory(Path.Combine(appDataPath, "Downloads"));
            }
            catch (Exception ex)
            {
                // Klasör oluşturma hatası - kritik değil
                LogError(ex);
            }
        }

        /// <summary>
        /// Uygulama veri klasörü yolunu döndürür
        /// </summary>
        private string GetAppDataPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "YafesV2");
        }

        #endregion

        #region Loglama Sistemi

        /// <summary>
        /// Uygulama başlangıcını loglar
        /// </summary>
        private void LogApplicationStart()
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] YafesV2 uygulaması başlatıldı";
                WriteToLog("application.log", logEntry);
            }
            catch
            {
                // Loglama hatası - sessizce geç
            }
        }

        /// <summary>
        /// Uygulama kapanışını loglar
        /// </summary>
        private void LogApplicationExit()
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] YafesV2 uygulaması kapatıldı";
                WriteToLog("application.log", logEntry);
            }
            catch
            {
                // Loglama hatası - sessizce geç
            }
        }

        /// <summary>
        /// Hataları loglar
        /// </summary>
        private void LogError(Exception ex)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] HATA: {ex.Message}\n{ex.StackTrace}";
                WriteToLog("errors.log", logEntry);
            }
            catch
            {
                // Loglama hatası - sessizce geç
            }
        }

        /// <summary>
        /// Log dosyasına yazma işlemi
        /// </summary>
        private void WriteToLog(string fileName, string logEntry)
        {
            string logPath = Path.Combine(GetAppDataPath(), "Logs", fileName);
            File.AppendAllText(logPath, logEntry + Environment.NewLine);
        }

        #endregion
    }
}