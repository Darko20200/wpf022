using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// Revo Uninstaller özel kurulum yöneticisi
    /// Revo Uninstaller'ı kurar ve yapılandırır
    /// </summary>
    public class RevoUninstallerInstaller : BaseInstaller
    {
        #region Constants

        private const string REVO_DOWNLOAD_URL = "https://download.revouninstaller.com/download/revosetup.exe";
        private const string REVO_REGISTRY_PATH = @"SOFTWARE\VS Revo Group\Revo Uninstaller";
        private const string REVO_UNINSTALL_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Revo Uninstaller";

        #endregion

        #region Properties

        public bool CreateDesktopShortcut { get; set; } = true;
        public bool CreateStartMenuShortcut { get; set; } = true;
        public bool EnableAutoStartup { get; set; } = false;
        public bool EnableRealTimeMonitoring { get; set; } = true;
        public string ScanLevel { get; set; } = "Advanced"; // Safe, Moderate, Advanced

        #endregion

        #region Public Methods

        /// <summary>
        /// Revo Uninstaller kurulum işlemi
        /// </summary>
        public override async Task<InstallationResult> InstallAsync()
        {
            IsInstalling = true;
            Progress = 0;

            try
            {
                // 1. Revo Uninstaller zaten kurulu mu kontrol et
                if (IsInstalled())
                {
                    Status = "Revo Uninstaller zaten kurulu - yapılandırma kontrol ediliyor";
                    await ConfigureRevoUninstaller();
                    Progress = 100;
                    return InstallationResult.AlreadyInstalled;
                }

                // 2. Revo Uninstaller'ı indir
                Status = "Revo Uninstaller indiriliyor...";
                string installerPath = await DownloadRevoUninstaller();
                Progress = 30;

                // 3. Revo Uninstaller'ı kur
                Status = "Revo Uninstaller kuruluyor...";
                bool installSuccess = await InstallRevoUninstaller(installerPath);
                Progress = 70;

                if (!installSuccess)
                {
                    Status = "Revo Uninstaller kurulumu başarısız";
                    return InstallationResult.InstallationFailed;
                }

                // 4. Kurulum sonrası yapılandırma
                Status = "Revo Uninstaller yapılandırılıyor...";
                await ConfigureRevoUninstaller();
                Progress = 90;

                // 5. Real-time monitoring ayarı
                if (EnableRealTimeMonitoring)
                {
                    await EnableMonitoring();
                }

                Status = "Revo Uninstaller kurulumu ve yapılandırması tamamlandı";
                Progress = 100;
                return InstallationResult.Success;
            }
            catch (Exception ex)
            {
                Status = $"Revo Uninstaller kurulum hatası: {ex.Message}";
                return InstallationResult.Error;
            }
            finally
            {
                IsInstalling = false;
                CleanupInstaller();
            }
        }

        /// <summary>
        /// Revo Uninstaller kurulu mu kontrol eder
        /// </summary>
        public override bool IsInstalled()
        {
            try
            {
                // Registry kontrolü
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(REVO_UNINSTALL_PATH))
                {
                    if (key != null)
                        return true;
                }

                // Revo registry yolu
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(REVO_REGISTRY_PATH))
                {
                    if (key != null)
                        return true;
                }

                // Executable dosya kontrolü
                string revoPath = GetRevoUninstallerPath();
                return !string.IsNullOrEmpty(revoPath) && File.Exists(revoPath);
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
            return "Revo Uninstaller";
        }

        /// <summary>
        /// Revo Uninstaller sürüm bilgisini alır
        /// </summary>
        public string GetRevoVersion()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(REVO_UNINSTALL_PATH))
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
        /// Program kaldırma işlemi başlatır
        /// </summary>
        public async Task<bool> UninstallProgramAsync(string programName)
        {
            try
            {
                Status = $"{programName} kaldırılıyor...";

                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                {
                    Status = "Revo Uninstaller bulunamadı";
                    return false;
                }

                // Revo Uninstaller'ı belirli program ile başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
                    Arguments = $"/mu \"{programName}\"",
                    UseShellExecute = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = $"{programName} kaldırma işlemi tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Program kaldırma hatası: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Revo Uninstaller installer'ını indirir
        /// </summary>
        private async Task<string> DownloadRevoUninstaller()
        {
            try
            {
                Status = "Revo Uninstaller installer indiriliyor...";

                string fileName = "revosetup.exe";
                return await DownloadFileAsync(REVO_DOWNLOAD_URL, fileName);
            }
            catch (Exception ex)
            {
                throw new Exception($"Revo Uninstaller indirme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Revo Uninstaller kurulum işlemi
        /// </summary>
        private async Task<bool> InstallRevoUninstaller(string installerPath)
        {
            try
            {
                Status = "Revo Uninstaller kuruluyor...";

                // Revo Uninstaller sessiz kurulum parametreleri
                var arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES";

                // Desktop shortcut ayarı
                if (!CreateDesktopShortcut)
                {
                    arguments += " /NODESKTOP";
                }

                bool success = await ExecuteInstallerAsync(installerPath, arguments);

                if (success)
                {
                    // Kurulum tamamlanmasını bekle
                    await Task.Delay(4000);

                    // Revo Uninstaller'ın gerçekten kurulup kurulmadığını kontrol et
                    return IsInstalled();
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Revo Uninstaller kurulum hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Revo Uninstaller yapılandırma işlemi
        /// </summary>
        private async Task ConfigureRevoUninstaller()
        {
            try
            {
                Status = "Revo Uninstaller ayarları yapılandırılıyor...";

                await Task.Delay(1000); // Revo'nun başlamasını bekle

                // Registry ayarlarını yapılandır
                await ConfigureRegistrySettings();

                // Config dosyasını yapılandır (varsa)
                await ConfigureConfigFile();

                Status = "Revo Uninstaller yapılandırması tamamlandı";
            }
            catch (Exception ex)
            {
                Status = $"Revo Uninstaller yapılandırma hatası: {ex.Message}";
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
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\VS Revo Group\Revo Uninstaller\Settings"))
                    {
                        if (key != null)
                        {
                            // Tarama seviyesi ayarı
                            int scanLevelValue = ScanLevel.ToLower() switch
                            {
                                "safe" => 0,
                                "moderate" => 1,
                                "advanced" => 2,
                                _ => 1
                            };
                            key.SetValue("ScanLevel", scanLevelValue, RegistryValueKind.DWord);

                            // Real-time monitoring
                            key.SetValue("RealTimeMonitoring", EnableRealTimeMonitoring ? 1 : 0, RegistryValueKind.DWord);

                            // Auto startup
                            key.SetValue("AutoStartup", EnableAutoStartup ? 1 : 0, RegistryValueKind.DWord);

                            // Diğer ayarlar
                            key.SetValue("ShowSystemComponents", 0, RegistryValueKind.DWord);
                            key.SetValue("ShowUpdates", 0, RegistryValueKind.DWord);
                            key.SetValue("ConfirmUninstall", 1, RegistryValueKind.DWord);
                        }
                    }

                    // Startup ayarı
                    if (EnableAutoStartup)
                    {
                        using (RegistryKey startupKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            string revoPath = GetRevoUninstallerPath();
                            if (!string.IsNullOrEmpty(revoPath))
                            {
                                startupKey?.SetValue("RevoUninstaller", $"\"{revoPath}\" /startup");
                            }
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
                string configPath = GetRevoConfigPath();
                if (string.IsNullOrEmpty(configPath))
                    return;

                await Task.Run(() =>
                {
                    // Revo Uninstaller config dosyası (varsa) düzenle
                    if (File.Exists(configPath))
                    {
                        string content = File.ReadAllText(configPath);

                        // Config içeriğini güncelle
                        content = UpdateConfigValue(content, "ScanLevel", GetScanLevelValue());
                        content = UpdateConfigValue(content, "RealTimeMonitoring", EnableRealTimeMonitoring ? "1" : "0");

                        File.WriteAllText(configPath, content);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config dosyası ayar hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Real-time monitoring'i etkinleştirir
        /// </summary>
        private async Task EnableMonitoring()
        {
            try
            {
                Status = "Real-time monitoring etkinleştiriliyor...";

                string revoPath = GetRevoUninstallerPath();
                if (!string.IsNullOrEmpty(revoPath))
                {
                    // Monitoring service'i başlat
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = revoPath,
                        Arguments = "/startmonitoring",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        await process.WaitForExitAsync();
                    }
                }

                Status = "Real-time monitoring etkinleştirildi";
            }
            catch (Exception ex)
            {
                Status = $"Monitoring etkinleştirme hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Revo Uninstaller executable yolunu bulur
        /// </summary>
        private string GetRevoUninstallerPath()
        {
            try
            {
                // Registry'den yol al
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(REVO_UNINSTALL_PATH))
                {
                    string installLocation = key?.GetValue("InstallLocation")?.ToString();
                    if (!string.IsNullOrEmpty(installLocation))
                    {
                        string revoExe = Path.Combine(installLocation, "RevoUninst.exe");
                        if (File.Exists(revoExe))
                            return revoExe;
                    }
                }

                // Alternatif yolları kontrol et
                string[] possiblePaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VS Revo Group", "Revo Uninstaller", "RevoUninst.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VS Revo Group", "Revo Uninstaller", "RevoUninst.exe")
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
        /// Revo config dosya yolunu alır
        /// </summary>
        private string GetRevoConfigPath()
        {
            try
            {
                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                    return null;

                string revoDir = Path.GetDirectoryName(revoPath);
                return Path.Combine(revoDir, "RevoUninst.ini");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tarama seviyesi değerini alır
        /// </summary>
        private string GetScanLevelValue()
        {
            return ScanLevel.ToLower() switch
            {
                "safe" => "0",
                "moderate" => "1",
                "advanced" => "2",
                _ => "1"
            };
        }

        /// <summary>
        /// Config değerini günceller
        /// </summary>
        private string UpdateConfigValue(string content, string key, string value)
        {
            try
            {
                string pattern = $"{key}=.*";
                string replacement = $"{key}={value}";

                return System.Text.RegularExpressions.Regex.Replace(content, pattern, replacement);
            }
            catch
            {
                return content;
            }
        }

        /// <summary>
        /// Installer dosyasını temizler
        /// </summary>
        private void CleanupInstaller()
        {
            try
            {
                string installerPath = Path.Combine(DownloadsDirectory, "revosetup.exe");
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
        /// Revo Uninstaller'ı başlatır
        /// </summary>
        public async Task<bool> LaunchRevoUninstallerAsync()
        {
            try
            {
                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                    return false;

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
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
        /// Kurulu program listesini alır
        /// </summary>
        public async Task<string[]> GetInstalledProgramsAsync()
        {
            try
            {
                Status = "Kurulu programlar taranıyor...";

                var programs = new System.Collections.Generic.List<string>();

                await Task.Run(() =>
                {
                    string[] uninstallKeys = {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                    };

                    foreach (string keyPath in uninstallKeys)
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                foreach (string subKeyName in key.GetSubKeyNames())
                                {
                                    using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                                    {
                                        string displayName = subKey?.GetValue("DisplayName")?.ToString();
                                        string uninstallString = subKey?.GetValue("UninstallString")?.ToString();

                                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uninstallString))
                                        {
                                            // Sistem bileşenlerini filtrele
                                            if (!IsSystemComponent(displayName))
                                            {
                                                programs.Add(displayName);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                Status = $"{programs.Count} program tespit edildi";
                return programs.ToArray();
            }
            catch (Exception ex)
            {
                Status = $"Program tarama hatası: {ex.Message}";
                return new string[0];
            }
        }

        /// <summary>
        /// Sistem bileşeni mi kontrol eder
        /// </summary>
        private bool IsSystemComponent(string programName)
        {
            string[] systemComponents = {
                "Microsoft Visual C++",
                "Microsoft .NET",
                "Windows Update",
                "Security Update",
                "KB",
                "Hotfix"
            };

            string lowerName = programName.ToLower();
            foreach (string component in systemComponents)
            {
                if (lowerName.Contains(component.ToLower()))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Registry temizleme işlemi başlatır
        /// </summary>
        public async Task<bool> CleanRegistryAsync()
        {
            try
            {
                Status = "Registry temizleniyor...";

                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                {
                    Status = "Revo Uninstaller bulunamadı";
                    return false;
                }

                // Registry temizleme modunu başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
                    Arguments = "/registry",
                    UseShellExecute = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Registry temizleme tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Registry temizleme hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Autostart programlarını yönetir
        /// </summary>
        public async Task<bool> ManageStartupProgramsAsync()
        {
            try
            {
                Status = "Başlangıç programları yönetiliyor...";

                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                {
                    Status = "Revo Uninstaller bulunamadı";
                    return false;
                }

                // Autostart manager'ı başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
                    Arguments = "/autostart",
                    UseShellExecute = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Başlangıç programları yönetimi tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Başlangıç programları yönetim hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Browser temizleme işlemi başlatır
        /// </summary>
        public async Task<bool> CleanBrowsersAsync()
        {
            try
            {
                Status = "Tarayıcılar temizleniyor...";

                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                {
                    Status = "Revo Uninstaller bulunamadı";
                    return false;
                }

                // Browser cleaner'ı başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
                    Arguments = "/browsers",
                    UseShellExecute = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Tarayıcı temizleme tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Tarayıcı temizleme hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Junk dosyaları temizler
        /// </summary>
        public async Task<bool> CleanJunkFilesAsync()
        {
            try
            {
                Status = "Gereksiz dosyalar temizleniyor...";

                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                {
                    Status = "Revo Uninstaller bulunamadı";
                    return false;
                }

                // Junk file cleaner'ı başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
                    Arguments = "/junkfiles",
                    UseShellExecute = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Gereksiz dosya temizleme tamamlandı";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Gereksiz dosya temizleme hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Sistem geri yükleme noktası oluşturur
        /// </summary>
        public async Task<bool> CreateRestorePointAsync(string description = "Revo Uninstaller Backup")
        {
            try
            {
                Status = "Sistem geri yükleme noktası oluşturuluyor...";

                // PowerShell komutu ile restore point oluştur
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas" // Admin hakları gerekli
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = "Sistem geri yükleme noktası oluşturuldu";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Geri yükleme noktası oluşturma hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Revo Uninstaller ayarlarını sıfırlar
        /// </summary>
        public async Task ResetRevoSettings()
        {
            try
            {
                Status = "Revo Uninstaller ayarları sıfırlanıyor...";

                await Task.Run(() =>
                {
                    // Registry ayarlarını sil
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\VS Revo Group\Revo Uninstaller", false);
                    }
                    catch { }

                    // Startup registry'sinden kaldır
                    try
                    {
                        using (RegistryKey startupKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            startupKey?.DeleteValue("RevoUninstaller", false);
                        }
                    }
                    catch { }

                    // Config dosyasını sil
                    string configPath = GetRevoConfigPath();
                    if (File.Exists(configPath))
                    {
                        File.Delete(configPath);
                    }
                });

                Status = "Revo Uninstaller ayarları sıfırlandı";
            }
            catch (Exception ex)
            {
                Status = $"Ayar sıfırlama hatası: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Revo Uninstaller güncellemesini kontrol eder
        /// </summary>
        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                Status = "Revo Uninstaller güncellemesi kontrol ediliyor...";

                string revoPath = GetRevoUninstallerPath();
                if (string.IsNullOrEmpty(revoPath))
                    return false;

                // Güncelleme kontrolü ile başlat
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = revoPath,
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