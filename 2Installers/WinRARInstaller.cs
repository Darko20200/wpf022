using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// WinRAR özel kurulum yöneticisi
    /// WinRAR'ı kurar ve yapılandırır
    /// </summary>
    public class WinRARInstaller : BaseInstaller
    {
        #region Constants

        private const string WINRAR_DOWNLOAD_URL_64 = "https://www.win-rar.com/fileadmin/winrar-versions/winrar/winrar-x64-611tr.exe";
        private const string WINRAR_DOWNLOAD_URL_32 = "https://www.win-rar.com/fileadmin/winrar-versions/winrar/winrar-x32-611tr.exe";
        private const string WINRAR_REGISTRY_PATH = @"SOFTWARE\WinRAR";
        private const string WINRAR_UNINSTALL_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinRAR archiver";

        #endregion

        #region Properties

        public bool AssociateArchiveFormats { get; set; } = true;
        public bool CreateDesktopShortcut { get; set; } = true;
        public bool AddToContextMenu { get; set; } = true;
        public bool StartMenuShortcut { get; set; } = true;
        public string Language { get; set; } = "Turkish";

        #endregion

        #region Public Methods

        /// <summary>
        /// WinRAR kurulum işlemi
        /// </summary>
        public override async Task<InstallationResult> InstallAsync()
        {
            IsInstalling = true;
            Progress = 0;

            try
            {
                // 1. WinRAR zaten kurulu mu kontrol et
                if (IsInstalled())
                {
                    Status = "WinRAR zaten kurulu - yapılandırma kontrol ediliyor";
                    await ConfigureWinRAR();
                    Progress = 100;
                    return InstallationResult.AlreadyInstalled;
                }

                // 2. Sistem mimarisine göre uygun versiyonu belirle
                string downloadUrl = Environment.Is64BitOperatingSystem ? WINRAR_DOWNLOAD_URL_64 : WINRAR_DOWNLOAD_URL_32;
                string fileName = Environment.Is64BitOperatingSystem ? "winrar-x64-611tr.exe" : "winrar-x32-611tr.exe";

                // 3. WinRAR'ı indir
                Status = "WinRAR indiriliyor...";
                string installerPath = await DownloadFileAsync(downloadUrl, fileName);
                Progress = 40;

                // 4. WinRAR'ı kur
                Status = "WinRAR kuruluyor...";
                bool installSuccess = await InstallWinRAR(installerPath);
                Progress = 80;

                if (!installSuccess)
                {
                    Status = "WinRAR kurulumu başarısız";
                    return InstallationResult.InstallationFailed;
                }

                // 5. Kurulum sonrası yapılandırma
                Status = "WinRAR yapılandırılıyor...";
                await ConfigureWinRAR();
                Progress = 95;

                // 6. Dosya ilişkilendirmeleri
                if (AssociateArchiveFormats)
                {
                    await SetupFileAssociations();
                }

                Status = "WinRAR kurulumu ve yapılandırması tamamlandı";
                Progress = 100;
                return InstallationResult.Success;
            }
            catch (Exception ex)
            {
                Status = $"WinRAR kurulum hatası: {ex.Message}";
                return InstallationResult.Error;
            }
            finally
            {
                IsInstalling = false;
                CleanupInstaller();
            }
        }

        /// <summary>
        /// WinRAR kurulu mu kontrol eder
        /// </summary>
        public override bool IsInstalled()
        {
            try
            {
                // Registry kontrolü
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(WINRAR_UNINSTALL_PATH))
                {
                    if (key != null)
                        return true;
                }

                // WinRAR registry yolu
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(WINRAR_REGISTRY_PATH))
                {
                    if (key != null)
                        return true;
                }

                // Executable dosya kontrolü
                string winrarPath = GetWinRARPath();
                return !string.IsNullOrEmpty(winrarPath) && File.Exists(winrarPath);
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
            return "WinRAR Archiver";
        }

        /// <summary>
        /// WinRAR sürüm bilgisini alır
        /// </summary>
        public string GetWinRARVersion()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(WINRAR_UNINSTALL_PATH))
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
        /// Arşiv dosyasını WinRAR ile açar
        /// </summary>
        public async Task<bool> OpenArchiveAsync(string archivePath)
        {
            try
            {
                if (!File.Exists(archivePath))
                    return false;

                string winrarPath = GetWinRARPath();
                if (string.IsNullOrEmpty(winrarPath))
                    return false;

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = winrarPath,
                    Arguments = $"\"{archivePath}\"",
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(startInfo);
                await Task.Delay(500);

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// WinRAR kurulum işlemi
        /// </summary>
        private async Task<bool> InstallWinRAR(string installerPath)
        {
            try
            {
                Status = "WinRAR kuruluyor...";

                // WinRAR sessiz kurulum parametreleri
                string arguments = "/S";

                bool success = await ExecuteInstallerAsync(installerPath, arguments);

                if (success)
                {
                    // Kurulum tamamlanmasını bekle
                    await Task.Delay(3000);

                    // WinRAR'ın gerçekten kurulup kurulmadığını kontrol et
                    return IsInstalled();
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"WinRAR kurulum hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// WinRAR yapılandırma işlemi
        /// </summary>
        private async Task ConfigureWinRAR()
        {
            try
            {
                Status = "WinRAR ayarları yapılandırılıyor...";

                await Task.Delay(1000); // WinRAR'ın başlamasını bekle

                // Registry ayarlarını yapılandır
                await ConfigureRegistrySettings();

                // WinRAR.ini dosyasını yapılandır
                await ConfigureIniFile();

                Status = "WinRAR yapılandırması tamamlandı";
            }
            catch (Exception ex)
            {
                Status = $"WinRAR yapılandırma hatası: {ex.Message}";
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
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\WinRAR\Setup"))
                    {
                        if (key != null)
                        {
                            // Arşiv formatları ilişkilendirme
                            if (AssociateArchiveFormats)
                            {
                                key.SetValue("Cascaded", 1, RegistryValueKind.DWord);
                                key.SetValue("ContextMenu", 1, RegistryValueKind.DWord);
                            }

                            // Desktop shortcut
                            key.SetValue("Desktop", CreateDesktopShortcut ? 1 : 0, RegistryValueKind.DWord);

                            // Start Menu shortcut
                            key.SetValue("StartMenu", StartMenuShortcut ? 1 : 0, RegistryValueKind.DWord);

                            // Dil ayarı
                            key.SetValue("Language", GetLanguageCode(Language), RegistryValueKind.String);
                        }
                    }

                    // WinRAR ana ayarları
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\WinRAR\General"))
                    {
                        if (key != null)
                        {
                            // Genel ayarlar
                            key.SetValue("ShowComment", 1, RegistryValueKind.DWord);
                            key.SetValue("ShowArcTime", 1, RegistryValueKind.DWord);
                            key.SetValue("LockArc", 0, RegistryValueKind.DWord);
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
        /// WinRAR.ini dosyasını yapılandırır
        /// </summary>
        private async Task ConfigureIniFile()
        {
            try
            {
                string iniPath = GetWinRARIniPath();
                if (string.IsNullOrEmpty(iniPath))
                    return;

                await Task.Run(() =>
                {
                    var iniContent = new System.Text.StringBuilder();

                    // Genel ayarlar
                    iniContent.AppendLine("[General]");
                    iniContent.AppendLine("Viewer=1");
                    iniContent.AppendLine("ShowComment=1");
                    iniContent.AppendLine("SFX=1");

                    // Sıkıştırma ayarları
                    iniContent.AppendLine("[Compression]");
                    iniContent.AppendLine("Method=3"); // Normal sıkıştırma
                    iniContent.AppendLine("Dictionary=4096"); // 4MB dictionary

                    // Arşiv ayarları
                    iniContent.AppendLine("[Archive]");
                    iniContent.AppendLine("Test=0");
                    iniContent.AppendLine("Lock=0");

                    File.WriteAllText(iniPath, iniContent.ToString());
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"INI dosyası ayar hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Dosya ilişkilendirmelerini ayarlar
        /// </summary>
        private async Task SetupFileAssociations()
        {
            try
            {
                Status = "Dosya ilişkilendirmeleri yapılandırılıyor...";

                await Task.Run(() =>
                {
                    string[] archiveExtensions = {
                        ".rar", ".zip", ".7z", ".tar", ".gz", ".bz2",
                        ".xz", ".cab", ".arj", ".lzh", ".ace", ".jar"
                    };

                    string winrarPath = GetWinRARPath();
                    if (string.IsNullOrEmpty(winrarPath))
                        return;

                    foreach (string ext in archiveExtensions)
                    {
                        try
                        {
                            SetFileAssociation(ext, "WinRAR.Archive", $"WinRAR Archive ({ext})", winrarPath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Dosya ilişkilendirme hatası {ext}: {ex.Message}");
                        }
                    }
                });

                Status = "Dosya ilişkilendirmeleri tamamlandı";
            }
            catch (Exception ex)
            {
                Status = $"Dosya ilişkilendirme hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Belirli bir dosya uzantısı için ilişkilendirme ayarlar
        /// </summary>
        private void SetFileAssociation(string extension, string progId, string description, string exePath)
        {
            try
            {
                // Extension registry key
                using (RegistryKey extKey = Registry.ClassesRoot.CreateSubKey(extension))
                {
                    extKey?.SetValue("", progId);
                }

                // ProgID registry key
                using (RegistryKey progKey = Registry.ClassesRoot.CreateSubKey(progId))
                {
                    progKey?.SetValue("", description);

                    using (RegistryKey iconKey = progKey?.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", $"{exePath},0");
                    }

                    using (RegistryKey commandKey = progKey?.CreateSubKey(@"shell\open\command"))
                    {
                        commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"File association error for {extension}: {ex.Message}");
            }
        }

        /// <summary>
        /// WinRAR executable yolunu bulur
        /// </summary>
        private string GetWinRARPath()
        {
            try
            {
                // Registry'den kurulum yolunu al
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(WINRAR_REGISTRY_PATH))
                {
                    string installPath = key?.GetValue("exe64")?.ToString() ?? key?.GetValue("exe32")?.ToString();
                    if (!string.IsNullOrEmpty(installPath) && File.Exists(installPath))
                        return installPath;
                }

                // Alternatif yolları kontrol et
                string[] possiblePaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "WinRAR.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "WinRAR.exe")
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
        /// WinRAR.ini dosya yolunu alır
        /// </summary>
        private string GetWinRARIniPath()
        {
            try
            {
                string winrarPath = GetWinRARPath();
                if (string.IsNullOrEmpty(winrarPath))
                    return null;

                string winrarDir = Path.GetDirectoryName(winrarPath);
                return Path.Combine(winrarDir, "WinRAR.ini");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Dil kodunu alır
        /// </summary>
        private string GetLanguageCode(string language)
        {
            return language.ToLower() switch
            {
                "turkish" => "tr",
                "english" => "en",
                "german" => "de",
                "french" => "fr",
                "spanish" => "es",
                "italian" => "it",
                "russian" => "ru",
                _ => "en"
            };
        }

        /// <summary>
        /// Installer dosyasını temizler
        /// </summary>
        private void CleanupInstaller()
        {
            try
            {
                string[] installerFiles = {
                    "winrar-x64-611tr.exe",
                    "winrar-x32-611tr.exe"
                };

                foreach (string fileName in installerFiles)
                {
                    string installerPath = Path.Combine(DownloadsDirectory, fileName);
                    if (File.Exists(installerPath))
                    {
                        File.Delete(installerPath);
                    }
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
        /// WinRAR'ı başlatır
        /// </summary>
        public async Task<bool> LaunchWinRARAsync()
        {
            try
            {
                string winrarPath = GetWinRARPath();
                if (string.IsNullOrEmpty(winrarPath))
                    return false;

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = winrarPath,
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
        /// Dosyaları arşivler
        /// </summary>
        public async Task<bool> CreateArchiveAsync(string[] filePaths, string archivePath, string compressionLevel = "3")
        {
            try
            {
                if (filePaths == null || filePaths.Length == 0)
                    return false;

                string winrarPath = GetWinRARPath();
                if (string.IsNullOrEmpty(winrarPath))
                    return false;

                Status = "Arşiv oluşturuluyor...";

                var arguments = new System.Text.StringBuilder();
                arguments.Append($"a -m{compressionLevel} \"{archivePath}\"");

                foreach (string filePath in filePaths)
                {
                    arguments.Append($" \"{filePath}\"");
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = winrarPath,
                    Arguments = arguments.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = process.ExitCode == 0 ? "Arşiv başarıyla oluşturuldu" : "Arşiv oluşturma hatası";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Arşiv oluşturma hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Arşivi çıkarır
        /// </summary>
        public async Task<bool> ExtractArchiveAsync(string archivePath, string extractPath)
        {
            try
            {
                if (!File.Exists(archivePath))
                    return false;

                string winrarPath = GetWinRARPath();
                if (string.IsNullOrEmpty(winrarPath))
                    return false;

                Status = "Arşiv çıkarılıyor...";

                Directory.CreateDirectory(extractPath);

                string arguments = $"x -y \"{archivePath}\" \"{extractPath}\\\"";

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = winrarPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                    Status = process.ExitCode == 0 ? "Arşiv başarıyla çıkarıldı" : "Arşiv çıkarma hatası";
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Arşiv çıkarma hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Arşiv içeriğini listeler
        /// </summary>
        public async Task<string[]> ListArchiveContentsAsync(string archivePath)
        {
            try
            {
                if (!File.Exists(archivePath))
                    return new string[0];

                string winrarPath = GetWinRARPath();
                if (string.IsNullOrEmpty(winrarPath))
                    return new string[0];

                string arguments = $"l \"{archivePath}\"";

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = winrarPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        // Output'u parse et ve dosya listesini döndür
                        return ParseArchiveList(output);
                    }
                }

                return new string[0];
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// WinRAR çıktısından dosya listesini parse eder
        /// </summary>
        private string[] ParseArchiveList(string output)
        {
            try
            {
                var files = new System.Collections.Generic.List<string>();
                var lines = output.Split('\n');
                bool inFileList = false;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.Contains("Name") && trimmedLine.Contains("Size"))
                    {
                        inFileList = true;
                        continue;
                    }

                    if (inFileList && !string.IsNullOrEmpty(trimmedLine) && trimmedLine != "---")
                    {
                        // Dosya satırını parse et
                        var parts = trimmedLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            files.Add(parts[parts.Length - 1]); // Son kısım dosya adı
                        }
                    }
                }

                return files.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// WinRAR ayarlarını sıfırlar
        /// </summary>
        public async Task ResetWinRARSettings()
        {
            try
            {
                Status = "WinRAR ayarları sıfırlanıyor...";

                await Task.Run(() =>
                {
                    // Registry ayarlarını sil
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\WinRAR", false);
                    }
                    catch { }

                    // INI dosyasını sil
                    string iniPath = GetWinRARIniPath();
                    if (File.Exists(iniPath))
                    {
                        File.Delete(iniPath);
                    }
                });

                Status = "WinRAR ayarları sıfırlandı";
            }
            catch (Exception ex)
            {
                Status = $"Ayar sıfırlama hatası: {ex.Message}";
                throw;
            }
        }

        #endregion
    }
}