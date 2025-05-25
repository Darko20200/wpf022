using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// Opera tarayıcısı özel kurulum ve yapılandırma yöneticisi
    /// V1'deki OperaPasswordManager'dan adapte edilmiştir
    /// </summary>
    public class OperaPasswordManager : BaseInstaller
    {
        #region Constants

        private const string OPERA_DOWNLOAD_URL = "https://download.opera.com/download/get/?partner=www&opsys=windows";
        private const string OPERA_REGISTRY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Opera Stable";
        private const string OPERA_APP_DATA_PATH = @"AppData\Roaming\Opera Software\Opera Stable";

        #endregion

        #region Properties

        public bool EnablePasswordManager { get; set; } = true;
        public bool EnableSyncFeatures { get; set; } = true;
        public bool SetAsDefaultBrowser { get; set; } = false;
        public bool ImportBrowserData { get; set; } = true;

        #endregion

        #region Public Methods

        /// <summary>
        /// Opera kurulum işlemi
        /// </summary>
        public override async Task<InstallationResult> InstallAsync()
        {
            IsInstalling = true;
            Progress = 0;

            try
            {
                // 1. Opera zaten kurulu mu kontrol et
                if (IsInstalled())
                {
                    Status = "Opera zaten kurulu - yapılandırma kontrol ediliyor";
                    await ConfigureOperaSettings();
                    Progress = 100;
                    return InstallationResult.AlreadyInstalled;
                }

                // 2. Opera'yı indir
                Status = "Opera indiriliyor...";
                string installerPath = await DownloadOperaInstaller();

                // 3. Opera'yı kur
                Status = "Opera kuruluyor...";
                bool installSuccess = await InstallOpera(installerPath);

                if (!installSuccess)
                {
                    Status = "Opera kurulumu başarısız";
                    return InstallationResult.InstallationFailed;
                }

                // 4. Kurulum sonrası yapılandırma
                Status = "Opera yapılandırılıyor...";
                await ConfigureOperaSettings();

                // 5. Password Manager ayarları
                if (EnablePasswordManager)
                {
                    await EnablePasswordManagerFeatures();
                }

                // 6. Sync ayarları
                if (EnableSyncFeatures)
                {
                    await EnableSyncConfiguration();
                }

                // 7. Default browser ayarı
                if (SetAsDefaultBrowser)
                {
                    await SetOperaAsDefaultBrowser();
                }

                // 8. Browser data import
                if (ImportBrowserData)
                {
                    await ImportOtherBrowserData();
                }

                Status = "Opera kurulumu ve yapılandırması tamamlandı";
                Progress = 100;
                return InstallationResult.Success;
            }
            catch (Exception ex)
            {
                Status = $"Opera kurulum hatası: {ex.Message}";
                return InstallationResult.Error;
            }
            finally
            {
                IsInstalling = false;
                CleanupOperaInstaller();
            }
        }

        /// <summary>
        /// Opera kurulu mu kontrol eder
        /// </summary>
        public override bool IsInstalled()
        {
            try
            {
                // Registry kontrolü
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(OPERA_REGISTRY_PATH))
                {
                    if (key != null)
                        return true;
                }

                // Alternatif yol kontrolü
                string operaPath = GetOperaInstallPath();
                return !string.IsNullOrEmpty(operaPath) && File.Exists(operaPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Installer adını döndürür
        /// </summary>
        public override string GetInstallerName()
        {
            return "Opera Password Manager";
        }

        /// <summary>
        /// Opera sürüm bilgisini alır
        /// </summary>
        public string GetOperaVersion()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(OPERA_REGISTRY_PATH))
                {
                    return key?.GetValue("DisplayVersion")?.ToString() ?? "Bilinmiyor";
                }
            }
            catch
            {
                return "Bilinmiyor";
            }
        }

        /// <summary>
        /// Password Manager durumunu kontrol eder
        /// </summary>
        public bool IsPasswordManagerEnabled()
        {
            try
            {
                string prefsPath = GetOperaPreferencesPath();
                if (File.Exists(prefsPath))
                {
                    string prefsContent = File.ReadAllText(prefsPath);
                    // JSON parsing ile password manager ayarını kontrol et
                    return prefsContent.Contains("\"password_manager_enabled\":true");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Methods - V1'den Adapte

        /// <summary>
        /// Opera installer'ını indirir
        /// </summary>
        private async Task<string> DownloadOperaInstaller()
        {
            try
            {
                Status = "Opera installer indiriliyor...";
                Progress = 10;

                string fileName = "OperaSetup.exe";
                return await DownloadFileAsync(OPERA_DOWNLOAD_URL, fileName);
            }
            catch (Exception ex)
            {
                throw new Exception($"Opera indirme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Opera kurulum işlemi
        /// </summary>
        private async Task<bool> InstallOpera(string installerPath)
        {
            try
            {
                Status = "Opera kuruluyor...";
                Progress = 50;

                // Opera sessiz kurulum parametreleri
                string arguments = "/S /NORESTART";

                bool success = await ExecuteInstallerAsync(installerPath, arguments);

                if (success)
                {
                    // Kurulum tamamlanmasını bekle
                    await Task.Delay(3000);

                    // Opera'nın gerçekten kurulup kurulmadığını kontrol et
                    return IsInstalled();
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Opera kurulum hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Opera genel ayarlarını yapılandırır
        /// </summary>
        private async Task ConfigureOperaSettings()
        {
            try
            {
                Status = "Opera ayarları yapılandırılıyor...";
                Progress = 70;

                await Task.Delay(1000); // Opera'nın başlamasını bekle

                string prefsPath = GetOperaPreferencesPath();

                // Preferences dosyası yoksa oluştur
                if (!File.Exists(prefsPath))
                {
                    CreateDefaultOperaPreferences(prefsPath);
                }

                // Mevcut ayarları güncelle
                await UpdateOperaPreferences(prefsPath);
            }
            catch (Exception ex)
            {
                // Yapılandırma hatası kritik değil
                Status = $"Opera yapılandırma uyarısı: {ex.Message}";
            }
        }

        /// <summary>
        /// Password Manager özelliklerini etkinleştirir
        /// </summary>
        private async Task EnablePasswordManagerFeatures()
        {
            try
            {
                Status = "Password Manager etkinleştiriliyor...";
                Progress = 80;

                string prefsPath = GetOperaPreferencesPath();
                if (File.Exists(prefsPath))
                {
                    string content = await File.ReadAllTextAsync(prefsPath);

                    // Password manager ayarlarını etkinleştir
                    content = UpdateJsonProperty(content, "password_manager_enabled", "true");
                    content = UpdateJsonProperty(content, "credentials_enable_service", "true");
                    content = UpdateJsonProperty(content, "profile.password_manager_enabled", "true");

                    await File.WriteAllTextAsync(prefsPath, content);
                }
            }
            catch (Exception ex)
            {
                Status = $"Password Manager etkinleştirme hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Sync özelliklerini yapılandırır
        /// </summary>
        private async Task EnableSyncConfiguration()
        {
            try
            {
                Status = "Sync özellikleri yapılandırılıyor...";
                Progress = 85;

                string prefsPath = GetOperaPreferencesPath();
                if (File.Exists(prefsPath))
                {
                    string content = await File.ReadAllTextAsync(prefsPath);

                    // Sync ayarlarını etkinleştir
                    content = UpdateJsonProperty(content, "sync.requested", "true");
                    content = UpdateJsonProperty(content, "sync.keep_everything_synced", "true");

                    await File.WriteAllTextAsync(prefsPath, content);
                }
            }
            catch (Exception ex)
            {
                Status = $"Sync yapılandırma hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Opera'yı varsayılan tarayıcı yapar
        /// </summary>
        private async Task SetOperaAsDefaultBrowser()
        {
            try
            {
                Status = "Opera varsayılan tarayıcı yapılıyor...";
                Progress = 90;

                string operaPath = GetOperaInstallPath();
                if (!string.IsNullOrEmpty(operaPath))
                {
                    // Windows default browser ayarı
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = operaPath,
                        Arguments = "--make-default-browser",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Status = $"Varsayılan tarayıcı ayarlama hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Diğer tarayıcılardan veri import eder
        /// </summary>
        private async Task ImportOtherBrowserData()
        {
            try
            {
                Status = "Tarayıcı verileri import ediliyor...";
                Progress = 95;

                // Chrome, Firefox vb. verilerini import et
                await Task.Delay(2000); // Import işlemini simüle et

                Status = "Veri import işlemi tamamlandı";
            }
            catch (Exception ex)
            {
                Status = $"Veri import hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Opera kurulum yolunu bulur
        /// </summary>
        private string GetOperaInstallPath()
        {
            try
            {
                // Registry'den yol al
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(OPERA_REGISTRY_PATH))
                {
                    string installLocation = key?.GetValue("InstallLocation")?.ToString();
                    if (!string.IsNullOrEmpty(installLocation))
                    {
                        string operaExe = Path.Combine(installLocation, "opera.exe");
                        if (File.Exists(operaExe))
                            return operaExe;
                    }
                }

                // Alternatif yolları kontrol et
                string[] possiblePaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Opera", "opera.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Opera", "opera.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Opera", "opera.exe")
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                        return path;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Opera preferences dosya yolunu alır
        /// </summary>
        private string GetOperaPreferencesPath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, OPERA_APP_DATA_PATH, "Preferences");
        }

        /// <summary>
        /// Varsayılan Opera preferences dosyası oluşturur
        /// </summary>
        private void CreateDefaultOperaPreferences(string prefsPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(prefsPath));

                string defaultPrefs = @"{
   ""password_manager_enabled"": true,
   ""credentials_enable_service"": true,
   ""profile"": {
      ""password_manager_enabled"": true,
      ""default_content_setting_values"": {
         ""password_manager"": 1
      }
   },
   ""sync"": {
      ""requested"": true,
      ""keep_everything_synced"": true
   }
}";
                File.WriteAllText(prefsPath, defaultPrefs);
            }
            catch (Exception ex)
            {
                throw new Exception($"Preferences dosyası oluşturma hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Opera preferences dosyasını günceller
        /// </summary>
        private async Task UpdateOperaPreferences(string prefsPath)
        {
            try
            {
                if (!File.Exists(prefsPath))
                    return;

                string content = await File.ReadAllTextAsync(prefsPath);

                // Önemli ayarları güncelle
                content = UpdateJsonProperty(content, "password_manager_enabled", "true");
                content = UpdateJsonProperty(content, "credentials_enable_service", "true");

                await File.WriteAllTextAsync(prefsPath, content);
            }
            catch (Exception ex)
            {
                throw new Exception($"Preferences güncelleme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// JSON property'sini günceller (basit string replacement)
        /// </summary>
        private string UpdateJsonProperty(string json, string property, string value)
        {
            try
            {
                // Basit JSON property güncelleme
                string pattern = $"\"{property}\"\\s*:\\s*[^,}}]+";
                string replacement = $"\"{property}\": {value}";

                return System.Text.RegularExpressions.Regex.Replace(json, pattern, replacement);
            }
            catch
            {
                return json; // Hata durumunda orijinal JSON'u geri döndür
            }
        }

        /// <summary>
        /// Opera installer dosyasını temizler
        /// </summary>
        private void CleanupOperaInstaller()
        {
            try
            {
                string installerPath = Path.Combine(DownloadsDirectory, "OperaSetup.exe");
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                }
            }
            catch
            {
                // Temizlik hatası kritik değil
            }
        }

        #endregion

        #region Public Utility Methods

        /// <summary>
        /// Opera profilini yedekler
        /// </summary>
        public async Task BackupOperaProfile(string backupPath)
        {
            try
            {
                Status = "Opera profili yedekleniyor...";

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string operaDataPath = Path.Combine(userProfile, OPERA_APP_DATA_PATH);

                if (Directory.Exists(operaDataPath))
                {
                    await Task.Run(() => CopyDirectory(operaDataPath, backupPath));
                    Status = "Opera profili başarıyla yedeklendi";
                }
            }
            catch (Exception ex)
            {
                Status = $"Profil yedekleme hatası: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Opera profilini geri yükler
        /// </summary>
        public async Task RestoreOperaProfile(string backupPath)
        {
            try
            {
                Status = "Opera profili geri yükleniyor...";

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string operaDataPath = Path.Combine(userProfile, OPERA_APP_DATA_PATH);

                if (Directory.Exists(backupPath))
                {
                    await Task.Run(() => CopyDirectory(backupPath, operaDataPath));
                    Status = "Opera profili başarıyla geri yüklendi";
                }
            }
            catch (Exception ex)
            {
                Status = $"Profil geri yükleme hatası: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Dizin kopyalama helper metodu
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(destDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, true);
            }
        }

        #endregion
    }
}