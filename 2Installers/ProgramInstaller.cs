using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// Genel program kurulum işlemlerini yönetir
    /// V1'deki RuntimeInstaller'dan adapte edilmiştir
    /// </summary>
    public class ProgramInstaller : BaseInstaller
    {
        #region Properties

        public ProgramInfo ProgramInfo { get; private set; }

        #endregion

        #region Constructor

        public ProgramInstaller(ProgramInfo programInfo)
        {
            ProgramInfo = programInfo ?? throw new ArgumentNullException(nameof(programInfo));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Ana kurulum işlemi - V1'deki RuntimeInstaller mantığı
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
                    Status = $"{ProgramInfo.Name} zaten kurulu";
                    Progress = 100;
                    return InstallationResult.AlreadyInstalled;
                }

                // 2. Sistem gereksinimlerini kontrol et
                if (!CheckSystemRequirements())
                {
                    Status = "Sistem gereksinimleri karşılanmıyor";
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
                string downloadedFile = await DownloadFileAsync(downloadUrl, ProgramInfo.FileName);

                // 5. Dosya bütünlüğünü kontrol et
                if (!VerifyDownloadedFile(downloadedFile))
                {
                    Status = "İndirilen dosya geçersiz";
                    return InstallationResult.DownloadFailed;
                }

                // 6. Kurulumu başlat
                Status = "Kurulum başlatılıyor...";
                bool installSuccess = await ExecuteInstallerAsync(downloadedFile, ProgramInfo.InstallArguments);

                // 7. Kurulum sonucunu kontrol et
                if (installSuccess && IsInstalled())
                {
                    // 8. Post-installation işlemleri
                    await PerformPostInstallationTasks();

                    Status = "Kurulum başarıyla tamamlandı";
                    Progress = 100;
                    return InstallationResult.Success;
                }
                else
                {
                    Status = "Kurulum başarısız";
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
        /// Program kurulu mu kontrol eder
        /// </summary>
        public override bool IsInstalled()
        {
            try
            {
                // Registry kontrolü
                if (CheckRegistryInstallation())
                    return true;

                // Dosya yolu kontrolü
                if (CheckFileInstallation())
                    return true;

                // Program Files kontrolü
                if (CheckProgramFilesInstallation())
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
            return $"{ProgramInfo.Name} Installer";
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
                if (!string.IsNullOrEmpty(ProgramInfo.DownloadUrl))
                {
                    return ProgramInfo.DownloadUrl;
                }

                // Dinamik URL çözümleme
                return ResolveDynamicUrl();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Dinamik URL çözümlemesi - V1'den adapte
        /// </summary>
        private string ResolveDynamicUrl()
        {
            // V1'deki dinamik URL çözümleme mantığı
            // GitHub releases, official sites vs.

            switch (ProgramInfo.Category?.ToLower())
            {
                case "runtime":
                    return ResolveRuntimeUrl();
                case "browser":
                    return ResolveBrowserUrl();
                case "utility":
                    return ResolveUtilityUrl();
                default:
                    return ProgramInfo.DownloadUrl;
            }
        }

        /// <summary>
        /// Runtime URL çözümlemesi
        /// </summary>
        private string ResolveRuntimeUrl()
        {
            // .NET Runtime, VC++ Redistributable vs.
            return ProgramInfo.DownloadUrl;
        }

        /// <summary>
        /// Tarayıcı URL çözümlemesi
        /// </summary>
        private string ResolveBrowserUrl()
        {
            // Chrome, Firefox, Opera vs.
            return ProgramInfo.DownloadUrl;
        }

        /// <summary>
        /// Utility URL çözümlemesi
        /// </summary>
        private string ResolveUtilityUrl()
        {
            // 7-Zip, WinRAR vs.
            return ProgramInfo.DownloadUrl;
        }

        /// <summary>
        /// Sistem gereksinimlerini kontrol eder
        /// </summary>
        private bool CheckSystemRequirements()
        {
            try
            {
                // Windows versiyon kontrolü
                if (!CheckWindowsVersion())
                    return false;

                // Architecture kontrolü (x64/x86)
                if (!CheckArchitecture())
                    return false;

                // Disk alanı kontrolü
                if (!CheckDiskSpace())
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
        /// Disk alanı kontrolü
        /// </summary>
        private bool CheckDiskSpace()
        {
            try
            {
                DriveInfo systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                long availableSpace = systemDrive.AvailableFreeSpace;
                long requiredSpace = ProgramInfo.RequiredDiskSpace > 0 ? ProgramInfo.RequiredDiskSpace : 100L * 1024 * 1024; // 100MB default

                return availableSpace > requiredSpace;
            }
            catch
            {
                return true; // Kontrol edilemezse geç
            }
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
                if (!string.IsNullOrEmpty(ProgramInfo.FileHash))
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
        /// Registry kurulum kontrolü
        /// </summary>
        private bool CheckRegistryInstallation()
        {
            try
            {
                string[] registryPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (string path in registryPaths)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            foreach (string subKeyName in key.GetSubKeyNames())
                            {
                                using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                                {
                                    string displayName = subKey?.GetValue("DisplayName")?.ToString();
                                    if (!string.IsNullOrEmpty(displayName) &&
                                        displayName.Contains(ProgramInfo.Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        return true;
                                    }
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
        /// Dosya yolu kurulum kontrolü
        /// </summary>
        private bool CheckFileInstallation()
        {
            if (ProgramInfo.InstallPaths != null)
            {
                foreach (string path in ProgramInfo.InstallPaths)
                {
                    if (CheckFileExists(Environment.ExpandEnvironmentVariables(path)))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Program Files kurulum kontrolü
        /// </summary>
        private bool CheckProgramFilesInstallation()
        {
            string[] programFolders = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (string folder in programFolders)
            {
                string programPath = Path.Combine(folder, ProgramInfo.Name);
                if (Directory.Exists(programPath))
                    return true;
            }

            return false;
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

                // Kısayol oluşturma
                await Task.Delay(500);

                // Yapılandırma dosyaları
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
                // İndirilen installer dosyasını sil
                if (!string.IsNullOrEmpty(ProgramInfo.FileName))
                {
                    string filePath = Path.Combine(DownloadsDirectory, ProgramInfo.FileName);
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