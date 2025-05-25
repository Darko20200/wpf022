// 8Utilities/PathUtility.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace YafesV2.Utilities
{
    /// <summary>
    /// V1'deki path işlemlerini modern hale getiren utility sınıfı
    /// V1'deki GetDriverBoosterPath, GetRevoUninstallerPath metodlarından adapte edilmiştir
    /// </summary>
    public static class PathUtility
    {
        #region Standard Paths

        /// <summary>
        /// Uygulama veri klasörünü alır
        /// </summary>
        public static string GetApplicationDataPath(string applicationName = "YafesV2")
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, applicationName);
        }

        /// <summary>
        /// Temp klasörünü alır
        /// </summary>
        public static string GetTempPath(string subFolder = null)
        {
            var tempPath = Path.GetTempPath();
            return string.IsNullOrEmpty(subFolder) ? tempPath : Path.Combine(tempPath, subFolder);
        }

        /// <summary>
        /// Downloads klasörünü alır
        /// </summary>
        public static string GetDownloadsPath()
        {
            // Windows Downloads klasörü
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
                {
                    var downloadsPath = key?.GetValue("{374DE290-123F-4565-9164-39C4925E467B}")?.ToString();
                    if (!string.IsNullOrEmpty(downloadsPath) && Directory.Exists(downloadsPath))
                        return downloadsPath;
                }
            }
            catch { }

            // Fallback
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Downloads");
        }

        /// <summary>
        /// Desktop klasörünü alır
        /// </summary>
        public static string GetDesktopPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        /// <summary>
        /// Documents klasörünü alır
        /// </summary>
        public static string GetDocumentsPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        #endregion

        #region Program Paths

        /// <summary>
        /// V1'deki GetDriverBoosterPath mantığının genelleştirilmiş versiyonu
        /// </summary>
        public static string FindProgramExecutable(string programName, List<string> possiblePaths = null)
        {
            try
            {
                // 1. Registry'den kurulum yolunu bul
                var registryPath = FindProgramInRegistry(programName);
                if (!string.IsNullOrEmpty(registryPath) && File.Exists(registryPath))
                    return registryPath;

                // 2. Program Files klasörlerinde ara
                var programFilesPaths = GetProgramFilesPaths(programName);
                foreach (var path in programFilesPaths)
                {
                    if (File.Exists(path))
                        return path;
                }

                // 3. Özel yolları kontrol et
                if (possiblePaths != null)
                {
                    foreach (var path in possiblePaths)
                    {
                        var expandedPath = Environment.ExpandEnvironmentVariables(path);
                        if (File.Exists(expandedPath))
                            return expandedPath;
                    }
                }

                // 4. PATH environment variable'da ara
                var pathVariable = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathVariable))
                {
                    var paths = pathVariable.Split(';');
                    foreach (var path in paths)
                    {
                        var exePath = Path.Combine(path, $"{programName}.exe");
                        if (File.Exists(exePath))
                            return exePath;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Registry'de program arar (V1'deki registry kontrol mantığı)
        /// </summary>
        public static string FindProgramInRegistry(string programName)
        {
            try
            {
                var uninstallKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var keyPath in uninstallKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key == null) continue;

                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                if (string.IsNullOrEmpty(displayName)) continue;

                                if (displayName.Contains(programName, StringComparison.OrdinalIgnoreCase))
                                {
                                    // InstallLocation'ı al
                                    var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrEmpty(installLocation))
                                    {
                                        var possibleExe = Path.Combine(installLocation, $"{programName}.exe");
                                        if (File.Exists(possibleExe))
                                            return possibleExe;

                                        // Program adına göre exe ara
                                        var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                                        var matchingExe = exeFiles.FirstOrDefault(f =>
                                            Path.GetFileNameWithoutExtension(f).Contains(programName, StringComparison.OrdinalIgnoreCase));

                                        if (!string.IsNullOrEmpty(matchingExe))
                                            return matchingExe;
                                    }

                                    // DisplayIcon'dan yol çıkar
                                    var displayIcon = subKey.GetValue("DisplayIcon")?.ToString();
                                    if (!string.IsNullOrEmpty(displayIcon) && File.Exists(displayIcon))
                                    {
                                        return displayIcon;
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Program Files klasörlerinde program arar
        /// </summary>
        public static List<string> GetProgramFilesPaths(string programName)
        {
            var paths = new List<string>();

            try
            {
                var programFolders = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (var programFolder in programFolders)
                {
                    if (string.IsNullOrEmpty(programFolder)) continue;

                    // Doğrudan program klasörü
                    var directPath = Path.Combine(programFolder, programName, $"{programName}.exe");
                    paths.Add(directPath);

                    // Alt klasörlerde ara
                    try
                    {
                        var subDirs = Directory.GetDirectories(programFolder, $"*{programName}*", SearchOption.TopDirectoryOnly);
                        foreach (var subDir in subDirs)
                        {
                            var exeFiles = Directory.GetFiles(subDir, "*.exe", SearchOption.AllDirectories);
                            paths.AddRange(exeFiles);
                        }
                    }
                    catch
                    {
                        // Erişim hatası - devam et
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return paths.Distinct().ToList();
        }

        #endregion

        #region Browser Paths

        /// <summary>
        /// Browser data klasörlerini bulur (V1'deki Opera path mantığı)
        /// </summary>
        public static BrowserPaths GetBrowserPaths(BrowserType browser)
        {
            var paths = new BrowserPaths { Browser = browser };

            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                switch (browser)
                {
                    case BrowserType.Chrome:
                        paths.UserDataPath = Path.Combine(localAppData, @"Google\Chrome\User Data");
                        paths.DefaultProfilePath = Path.Combine(paths.UserDataPath, "Default");
                        paths.ExecutablePath = FindProgramExecutable("chrome");
                        break;

                    case BrowserType.Opera:
                        paths.UserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera Stable");
                        paths.DefaultProfilePath = paths.UserDataPath;
                        paths.ExecutablePath = FindProgramExecutable("opera");
                        break;

                    case BrowserType.Firefox:
                        paths.UserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox\Profiles");
                        paths.ExecutablePath = FindProgramExecutable("firefox");
                        // Firefox profile'ı dinamik olarak bulunur
                        if (Directory.Exists(paths.UserDataPath))
                        {
                            var profileDirs = Directory.GetDirectories(paths.UserDataPath);
                            paths.DefaultProfilePath = profileDirs.FirstOrDefault(d => d.Contains("default"));
                        }
                        break;

                    case BrowserType.Edge:
                        paths.UserDataPath = Path.Combine(localAppData, @"Microsoft\Edge\User Data");
                        paths.DefaultProfilePath = Path.Combine(paths.UserDataPath, "Default");
                        paths.ExecutablePath = FindProgramExecutable("msedge");
                        break;
                }

                // Executable bulunamazsa alternatif yolları dene
                if (string.IsNullOrEmpty(paths.ExecutablePath))
                {
                    paths.ExecutablePath = FindBrowserExecutable(browser);
                }
            }
            catch
            {
                // Error handling
            }

            return paths;
        }

        /// <summary>
        /// Browser executable'ı bulur
        /// </summary>
        private static string FindBrowserExecutable(BrowserType browser)
        {
            var browserExeNames = browser switch
            {
                BrowserType.Chrome => new[] { "chrome.exe", "googlechrome.exe" },
                BrowserType.Opera => new[] { "opera.exe", "launcher.exe" },
                BrowserType.Firefox => new[] { "firefox.exe" },
                BrowserType.Edge => new[] { "msedge.exe", "microsoftedge.exe" },
                _ => new string[0]
            };

            foreach (var exeName in browserExeNames)
            {
                var path = FindProgramExecutable(Path.GetFileNameWithoutExtension(exeName));
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            return null;
        }

        #endregion

        #region Driver Paths

        /// <summary>
        /// Driver klasörlerini alır (V1'deki driver path mantığı)
        /// </summary>
        public static DriverPaths GetDriverPaths()
        {
            return new DriverPaths
            {
                SystemDriversPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "drivers"),
                DownloadedDriversPath = Path.Combine(GetApplicationDataPath(), "Drivers"),
                TempDriversPath = Path.Combine(GetTempPath(), "YafesV2_Drivers"),
                CacheDriversPath = Path.Combine(GetApplicationDataPath(), "Cache", "Drivers")
            };
        }

        #endregion

        #region Path Validation

        /// <summary>
        /// Path'in geçerli olup olmadığını kontrol eder
        /// </summary>
        public static bool IsValidPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                // Path.GetFullPath will throw exception if path is invalid
                Path.GetFullPath(path);

                // Check for invalid characters
                var invalidChars = Path.GetInvalidPathChars();
                return !path.Any(c => invalidChars.Contains(c));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Dosya adının geçerli olup olmadığını kontrol eder
        /// </summary>
        public static bool IsValidFileName(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return false;

                var invalidChars = Path.GetInvalidFileNameChars();
                return !fileName.Any(c => invalidChars.Contains(c));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Path'in yazılabilir olup olmadığını kontrol eder
        /// </summary>
        public static bool IsPathWritable(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var testFile = Path.Combine(path, $"test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Path Utilities

        /// <summary>
        /// Relative path'i absolute path'e çevirir
        /// </summary>
        public static string GetAbsolutePath(string relativePath, string basePath = null)
        {
            try
            {
                if (Path.IsPathRooted(relativePath))
                    return relativePath;

                basePath ??= Directory.GetCurrentDirectory();
                return Path.GetFullPath(Path.Combine(basePath, relativePath));
            }
            catch
            {
                return relativePath;
            }
        }

        /// <summary>
        /// İki path'in aynı olup olmadığını kontrol eder
        /// </summary>
        public static bool ArePathsEqual(string path1, string path2)
        {
            try
            {
                if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                    return false;

                var fullPath1 = Path.GetFullPath(path1);
                var fullPath2 = Path.GetFullPath(path2);

                return string.Equals(fullPath1, fullPath2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Path'den güvenli klasör adı oluşturur
        /// </summary>
        public static string GetSafeDirectoryName(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
                return "Default";

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = string.Join("_", directoryName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            // Windows reserved names
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reservedNames.Contains(safeName.ToUpperInvariant()))
            {
                safeName = $"_{safeName}";
            }

            return safeName;
        }

        /// <summary>
        /// Nested klasör yapısını oluşturur
        /// </summary>
        public static bool CreateDirectoryStructure(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Browser yolları bilgisi
    /// </summary>
    public class BrowserPaths
    {
        public BrowserType Browser { get; set; }
        public string ExecutablePath { get; set; }
        public string UserDataPath { get; set; }
        public string DefaultProfilePath { get; set; }
        public string PreferencesPath => !string.IsNullOrEmpty(DefaultProfilePath) ? Path.Combine(DefaultProfilePath, "Preferences") : null;
        public string BookmarksPath => !string.IsNullOrEmpty(DefaultProfilePath) ? Path.Combine(DefaultProfilePath, "Bookmarks") : null;
        public bool IsInstalled => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
    }

    /// <summary>
    /// Driver yolları bilgisi
    /// </summary>
    public class DriverPaths
    {
        public string SystemDriversPath { get; set; }
        public string DownloadedDriversPath { get; set; }
        public string TempDriversPath { get; set; }
        public string CacheDriversPath { get; set; }
    }

    #endregion

    #region Enums

    public enum BrowserType
    {
        Chrome,
        Opera,
        Firefox,
        Edge,
        Safari
    }

    #endregion