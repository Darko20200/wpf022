using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// IObit Driver Booster özel kurulum yöneticisi
    /// Driver Booster'ı kurar ve yapılandırır
    /// </summary>
    public class DriverBoosterInstaller : BaseInstaller
    {
        #region Constants

        private const string DRIVER_BOOSTER_URL = "https://cdn.iobit.com/dl/driver_booster_setup.exe";
        private const string DRIVER_BOOSTER_REGISTRY = @"SOFTWARE\IObit\Driver Booster";
        private const string DRIVER_BOOSTER_UNINSTALL = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Driver Booster_is1";

        #endregion

        #region Properties

        public bool EnableAutoScan { get; set; } = true;
        public bool EnableAutoUpdate { get; set; } = false; // Ücretsiz sürümde sınırlı
        public bool CreateDesktopShortcut { get; set; } = true;
        public bool EnableStartupScan { get; set; } = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// Driver Booster kurulum işlemi
        /// </summary>
        public override async Task<InstallationResult> InstallAsync()
        {
            IsInstalling = true;
            Progress = 0;

            try
            {
                // 1. Driver Booster zaten kurulu mu kontrol et
                if (IsInstalled())
                {
                    Status = "Driver Booster zaten kurulu - yapılandırma kontrol ediliyor";
                    await ConfigureDriverBooster();
                    Progress = 100;
                    return InstallationResult.AlreadyInstalled;
                }

                // 2. Driver Booster'ı indir
                Status = "Driver Booster indiriliyor...";
                string installerPath = await DownloadDriverBooster();
                Progress = 30;

                // 3. Driver Booster'ı kur
                Status = "Driver Booster kuruluyor...";
                bool installSuccess = await InstallDriverBooster(installerPath);
                Progress = 70;

                if (!installSuccess)
                {
                    Status = "Driver Booster kurulumu başarısız";
                    return InstallationResult.InstallationFailed;
                }

                // 4. Kurulum sonrası yapılandırma
                Status = "Driver Booster yapılandırılıyor...";
                await ConfigureDriverBooster();
                Progress = 90;

                // 5. İlk tarama (isteğe bağlı)
                if (EnableAutoScan)
                {
                    Status = "İlk driver taraması başlatılıyor...";
                    await InitialDriverScan();
                }

                Status = "Driver Booster kurulumu ve yapılandırması tamamlandı";
                Progress = 100;
                return InstallationResult.Success;
            }
            catch (Exception ex)
            {
                Status = $"Driver Booster kurulum hatası: {ex.Message}";
                return InstallationResult.Error;
            }
            finally
            {
                IsInstalling = false;
                CleanupInstaller();
            }
        }

        /// <summary>
        /// Driver Booster kurulu mu kontrol eder
        /// </summary>
        public override bool IsInstalled()
        {
            try
            {
                // Registry kontrolü
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(DRIVER_BOOSTER_UNINSTALL))
                {
                    if (key != null)
                        return true;
                }

                // Alternatif registry yolu
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(DRIVER_BOOSTER_REGISTRY))
                {
                    if (key != null)
                        return true;
                }

                // Executable dosya kontrolü
                string driverBoosterPath = GetDriverBoosterPath();
                return !string.IsNullOrEmpty(driverBoosterPath) && File.Exists(driverBoosterPath);
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
            return "IObit Driver Booster";
        }

        /// <summary>
        /// Driver Booster sürüm bilgisini alır
        /// </summary>
        public string GetDriverBoosterVersion()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(DRIVER_BOOSTER_UNINSTALL))
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
        /// Driver taraması başlatır
        /// </summary>
        public async Task<bool> StartDriverScanAsync()
        {
            try
            {
                Status = "Driver taraması başlatılıyor...";

                string driverBoosterPath = GetDriverBoosterPath();
                if (string.IsNullOrEmpty(driverBoosterPath))
                {
                    Status = "Driver Booster bulunamadı";
                    return false;
                }

                // Driver Booster'ı tarama parametresi ile başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = driverBoosterPath,
                    Arguments = "/scan",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Driver taraması tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Driver tarama hatası: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Driver Booster installer'ını indirir
        /// </summary>
        private async Task<string> DownloadDriverBooster()
        {
            try
            {
                Status = "Driver Booster installer indiriliyor...";

                string fileName = "driver_booster_setup.exe";
                return await DownloadFileAsync(DRIVER_BOOSTER_URL, fileName);
            }
            catch (Exception ex)
            {
                throw new Exception($"Driver Booster indirme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Driver Booster kurulum işlemi
        /// </summary>
        private async Task<bool> InstallDriverBooster(string installerPath)
        {
            try
            {
                Status = "Driver Booster kuruluyor...";

                // Driver Booster sessiz kurulum parametreleri
                string arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES";

                // Desktop shortcut ayarı
                if (!CreateDesktopShortcut)
                {
                    arguments += " /NODESKTOP";
                }

                bool success = await ExecuteInstallerAsync(installerPath, arguments);

                if (success)
                {
                    // Kurulum tamamlanmasını bekle
                    await Task.Delay(5000);

                    // Driver Booster'ın gerçekten kurulup kurulmadığını kontrol et
                    return IsInstalled();
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Driver Booster kurulum hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Driver Booster yapılandırma işlemi
        /// </summary>
        private async Task ConfigureDriverBooster()
        {
            try
            {
                Status = "Driver Booster ayarları yapılandırılıyor...";

                await Task.Delay(1000); // Driver Booster'ın başlamasını bekle

                // Registry ayarlarını yapılandır
                await ConfigureRegistrySettings();

                // Config dosyasını yapılandır (varsa)
                await ConfigureConfigFile();

                Status = "Driver Booster yapılandırması tamamlandı";
            }
            catch (Exception ex)
            {
                Status = $"Driver Booster yapılandırma hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Registry ayarlarını yapılandırır
        /// </summary>
        private async Task ConfigureRegistrySettings()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\IObit\Driver Booster"))
                    {
                        if (key != null)
                        {
                            // Auto scan ayarı
                            key.SetValue("AutoScan", EnableAutoScan ? 1 : 0, RegistryValueKind.DWord);

                            // Auto update ayarı (ücretsiz sürümde sınırlı)
                            key.SetValue("AutoUpdate", EnableAutoUpdate ? 1 : 0, RegistryValueKind.DWord);

                            // Startup scan ayarı
                            key.SetValue("StartupScan", EnableStartupScan ? 1 : 0, RegistryValueKind.DWord);

                            // Notification ayarları
                            key.SetValue("ShowNotifications", 1, RegistryValueKind.DWord);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Registry ayar hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Config dosyasını yapılandırır
        /// </summary>
        private async Task ConfigureConfigFile()
        {
            try
            {
                string configPath = GetDriverBoosterConfigPath();
                if (string.IsNullOrEmpty(configPath))
                    return;

                await Task.Run(() =>
                {
                    // Driver Booster config dosyası (varsa) düzenle
                    // Bu IObit'in özel formatına bağlı
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config dosyası ayar hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// İlk driver taramasını başlatır
        /// </summary>
        private async Task InitialDriverScan()
        {
            try
            {
                Status = "İlk driver taraması başlatılıyor...";

                // Driver Booster'ın ilk taramayı otomatik yapmasını bekle
                await Task.Delay(3000);

                // Manuel tarama başlat
                await StartDriverScanAsync();
            }
            catch (Exception ex)
            {
                Status = $"İlk tarama hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Driver Booster executable yolunu bulur
        /// </summary>
        private string GetDriverBoosterPath()
        {
            try
            {
                // Registry'den kurulum yolunu al
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(DRIVER_BOOSTER_UNINSTALL))
                {
                    string installLocation = key?.GetValue("InstallLocation")?.ToString();
                    if (!string.IsNullOrEmpty(installLocation))
                    {
                        string driverBoosterExe = Path.Combine(installLocation, "DriverBooster.exe");
                        if (File.Exists(driverBoosterExe))
                            return driverBoosterExe;
                    }
                }

                // Alternatif yolları kontrol et
                string[] possiblePaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IObit", "Driver Booster", "DriverBooster.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "IObit", "Driver Booster", "DriverBooster.exe")
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
        /// Driver Booster config dosya yolunu alır
        /// </summary>
        private string GetDriverBoosterConfigPath()
        {
            try
            {
                string driverBoosterPath = GetDriverBoosterPath();
                if (string.IsNullOrEmpty(driverBoosterPath))
                    return null;

                string configDir = Path.GetDirectoryName(driverBoosterPath);
                return Path.Combine(configDir, "Config", "DriverBooster.ini");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Installer dosyasını temizler
        /// </summary>
        private void CleanupInstaller()
        {
            try
            {
                string installerPath = Path.Combine(DownloadsDirectory, "driver_booster_setup.exe");
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
        /// Driver Booster'ı başlatır
        /// </summary>
        public async Task<bool> LaunchDriverBoosterAsync()
        {
            try
            {
                string driverBoosterPath = GetDriverBoosterPath();
                if (string.IsNullOrEmpty(driverBoosterPath))
                    return false;

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = driverBoosterPath,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(startInfo);
                await Task.Delay(1000);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Driver Booster ayarlarını sıfırlar
        /// </summary>
        public async Task ResetDriverBoosterSettings()
        {
            try
            {
                Status = "Driver Booster ayarları sıfırlanıyor...";

                await Task.Run(() =>
                {
                    // Registry ayarlarını sil
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\IObit\Driver Booster", false);
                    }
                    catch { }

                    // Config dosyasını sil
                    string configPath = GetDriverBoosterConfigPath();
                    if (File.Exists(configPath))
                    {
                        File.Delete(configPath);
                    }
                });

                Status = "Driver Booster ayarları sıfırlandı";
            }
            catch (Exception ex)
            {
                Status = $"Ayar sıfırlama hatası: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Driver Booster güncellemesini kontrol eder
        /// </summary>
        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                Status = "Driver Booster güncellemesi kontrol ediliyor...";

                string driverBoosterPath = GetDriverBoosterPath();
                if (string.IsNullOrEmpty(driverBoosterPath))
                    return false;

                // Driver Booster'ı güncelleme kontrolü ile başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = driverBoosterPath,
                    Arguments = "/checkupdate",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Güncelleme kontrolü tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Güncelleme kontrol hatası: {ex.Message}";
                return false;
            }
        }

        #endregion
    }
}