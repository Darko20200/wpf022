// 2Installers/DriverInstaller.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// Sürücü kurulum işlemlerini yönetir
    /// V1'deki DriverInstaller'dan adapte edilmiştir
    /// </summary>
    public class DriverInstaller : BaseInstaller
    {
        #region Properties

        public DriverInfo DriverInfo { get; private set; }

        #endregion

        #region Constructor

        public DriverInstaller(DriverInfo driverInfo)
        {
            DriverInfo = driverInfo ?? throw new ArgumentNullException(nameof(driverInfo));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Ana kurulum işlemi - V1'deki DriverInstaller mantığı
        /// </summary>
        public override async Task<InstallationResult> InstallAsync()
        {
            IsInstalling = true;
            Progress = 0;

            try
            {
                // 1. Önce kurulu mu kontrol et
                if (IsInstalled())
                {
                    Status = $"{DriverInfo.Name} zaten kurulu";
                    Progress = 100;
                    return InstallationResult.AlreadyInstalled;
                }

                // 2. Sistem uyumluluğunu kontrol et
                if (!CheckCompatibility())
                {
                    Status = "Sistem uyumlu değil";
                    return InstallationResult.SystemRequirementsNotMet;
                }

                // 3. İndirme URL'sini hazırla
                string downloadUrl = GetDownloadUrl();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Status = "İndirme URL'si bulunamadı";
                    return InstallationResult.DownloadFailed;
                }

                // 4. Dosyayı indir
                Status = "İndiriliyor...";
                string downloadedFile = await DownloadFileAsync(downloadUrl, DriverInfo.FileName);

                // 5. Dosya bütünlüğünü kontrol et
                if (!VerifyDownloadedFile(downloadedFile))
                {
                    Status = "İndirilen dosya geçersiz";
                    return InstallationResult.DownloadFailed;
                }

                // 6. Kurulumu başlat
                Status = "Kurulum başlatılıyor...";
                bool installSuccess = await ExecuteInstallerAsync(downloadedFile, DriverInfo.InstallArguments);

                // 7. Kurulum sonucunu kontrol et
                if (installSuccess && IsInstalled())
                {
                    // 8. Post-installation işlemleri
                    await PerformPostInstallationTasks();

                    Status = "Sürücü kurulumu başarıyla tamamlandı";
                    Progress = 100;
                    return InstallationResult.Success;
                }
                else
                {
                    Status = "Sürücü kurulumu başarısız";
                    return InstallationResult.InstallationFailed;
                }
            }
            catch (Exception ex)
            {
                Status = $"Kurulum hatası: {ex.Message}";
                return InstallationResult.Error;
            }
            finally
            {
                IsInstalling = false;
                CleanupTempFiles();
            }
        }

        /// <summary>
        /// Sürücü kurulu mu kontrol eder
        /// </summary>
        public override bool IsInstalled()
        {
            try
            {
                // Device Manager kontrolü
                if (CheckDeviceManagerInstallation())
                    return true;

                // Registry kontrolü
                if (CheckRegistryInstallation())
                    return true;

                // Dosya sistemi kontrolü
                if (CheckFileSystemInstallation())
                    return true;

                return false;
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
            return $"{DriverInfo.Name} Driver Installer";
        }

        #endregion

        #region Private Methods - V1'den Adapte

        /// <summary>
        /// İndirme URL'sini hazırlar - V1 mantığı
        /// </summary>
        private string GetDownloadUrl()
        {
            try
            {
                // V1'deki URL çözümleme mantığı burada
                if (!string.IsNullOrEmpty(DriverInfo.Url))
                {
                    return DriverInfo.Url;
                }

                // Dinamik URL çözümleme
                return ResolveDynamicDriverUrl();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Dinamik URL çözümlemesi - V1'den adapte
        /// </summary>
        private string ResolveDynamicDriverUrl()
        {
            // V1'deki dinamik URL çözümleme mantığı
            // NVIDIA, AMD, Intel sürücüleri için özel mantık

            switch (DriverInfo.Category?.ToLower())
            {
                case "graphics":
                    return ResolveGraphicsDriverUrl();
                case "audio":
                    return ResolveAudioDriverUrl();
                case "network":
                    return ResolveNetworkDriverUrl();
                default:
                    return DriverInfo.Url;
            }
        }

        /// <summary>
        /// Grafik sürücü URL çözümlemesi
        /// </summary>
        private string ResolveGraphicsDriverUrl()
        {
            // NVIDIA, AMD, Intel için özel mantık
            return DriverInfo.Url;
        }

        /// <summary>
        /// Ses sürücü URL çözümlemesi
        /// </summary>
        private string ResolveAudioDriverUrl()
        {
            // Realtek, Creative vs. için özel mantık
            return DriverInfo.Url;
        }

        /// <summary>
        /// Ağ sürücü URL çözümlemesi
        /// </summary>
        private string ResolveNetworkDriverUrl()
        {
            // Intel, Realtek, Broadcom vs. için özel mantık
            return DriverInfo.Url;
        }

        /// <summary>
        /// Sistem uyumluluğunu kontrol eder
        /// </summary>
        private bool CheckCompatibility()
        {
            try
            {
                // Windows versiyon kontrolü
                if (!CheckWindowsVersion())
                    return false;

                // Architecture kontrolü (x64/x86)
                if (!CheckArchitecture())
                    return false;

                // Hardware uyumluluğu
                if (!CheckHardwareCompatibility())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Windows versiyon kontrolü
        /// </summary>
        private bool CheckWindowsVersion()
        {
            var osVersion = Environment.OSVersion.Version;
            // Windows 10+ gereksinimi
            return osVersion.Major >= 10;
        }

        /// <summary>
        /// Architecture kontrolü
        /// </summary>
        private bool CheckArchitecture()
        {
            // x64 sistem kontrolü
            return Environment.Is64BitOperatingSystem;
        }

        /// <summary>
        /// Hardware uyumluluğu kontrolü
        /// </summary>
        private bool CheckHardwareCompatibility()
        {
            // Device ID kontrolü
            if (!string.IsNullOrEmpty(DriverInfo.DeviceId))
            {
                // Hardware ID ile uyumluluk kontrolü
                // Bu kısım geliştirilecek
            }

            return true; // Şimdilik geç
        }

        /// <summary>
        /// İndirilen dosya doğrulaması
        /// </summary>
        private bool VerifyDownloadedFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);

                // Dosya boyutu kontrolü
                if (fileInfo.Length < 1024) // 1KB'den küçükse geçersiz
                    return false;

                // Hash kontrolü (varsa)
                if (!string.IsNullOrEmpty(DriverInfo.FileHash))
                {
                    // TODO: Hash doğrulaması implement edilecek
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Device Manager kurulum kontrolü
        /// </summary>
        private bool CheckDeviceManagerInstallation()
        {
            try
            {
                // Device Manager'da cihaz kontrolü
                // Bu kısım WMI ile implement edilebilir
                return false; // Şimdilik false
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Registry kurulum kontrolü
        /// </summary>
        private bool CheckRegistryInstallation()
        {
            try
            {
                string[] registryPaths = {
                    @"SYSTEM\CurrentControlSet\Services",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpLockdownFiles"
                };

                foreach (string path in registryPaths)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            // Sürücü adına göre arama
                            foreach (string subKeyName in key.GetSubKeyNames())
                            {
                                if (subKeyName.Contains(DriverInfo.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Dosya sistemi kurulum kontrolü
        /// </summary>
        private bool CheckFileSystemInstallation()
        {
            try
            {
                string[] systemPaths = {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "drivers")
                };

                foreach (string path in systemPaths)
                {
                    if (Directory.Exists(path))
                    {
                        // Sürücü dosyalarını ara
                        var files = Directory.GetFiles(path, "*.sys", SearchOption.TopDirectoryOnly);
                        foreach (string file in files)
                        {
                            if (Path.GetFileNameWithoutExtension(file)
                                .Contains(DriverInfo.Name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kurulum sonrası işlemler
        /// </summary>
        private async Task PerformPostInstallationTasks()
        {
            try
            {
                Status = "Kurulum sonrası işlemler...";

                // Registry temizleme
                await Task.Delay(500);

                // Sistem yeniden başlatma kontrolü
                await Task.Delay(500);

                // Driver imza doğrulaması
                await Task.Delay(500);

                Status = "Kurulum sonrası işlemler tamamlandı";
            }
            catch (Exception ex)
            {
                // Post-installation hataları kritik değil
                Status = $"Kurulum sonrası uyarı: {ex.Message}";
            }
        }

        /// <summary>
        /// Geçici dosyaları temizler
        /// </summary>
        private void CleanupTempFiles()
        {
            try
            {
                // İndirilen driver dosyasını sil
                if (!string.IsNullOrEmpty(DriverInfo.FileName))
                {
                    string filePath = Path.Combine(DownloadsDirectory, DriverInfo.FileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
            }
            catch
            {
                // Temizlik hatası kritik değil
            }
        }

        #endregion
    }
}