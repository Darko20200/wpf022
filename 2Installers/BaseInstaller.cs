using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;
using YafesV2.Models;

namespace YafesV2.Installers
{
    /// <summary>
    /// Tüm installer sınıflarının temel sınıfı
    /// Ortak kurulum işlevlerini sağlar
    /// </summary>
    public abstract class BaseInstaller : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<InstallationProgressEventArgs> ProgressChanged;
        public event EventHandler<InstallationStatusEventArgs> StatusChanged;

        #endregion

        #region Properties

        private int _progress;
        public int Progress
        {
            get => _progress;
            protected set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
                OnProgressChanged(value);
            }
        }

        private string _status;
        public string Status
        {
            get => _status;
            protected set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnStatusChanged(value);
            }
        }

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            protected set
            {
                _isInstalling = value;
                OnPropertyChanged(nameof(IsInstalling));
            }
        }

        protected string TempDirectory { get; private set; }
        protected string DownloadsDirectory { get; private set; }
        protected HttpClient HttpClient { get; private set; }

        #endregion

        #region Constructor

        protected BaseInstaller()
        {
            InitializeDirectories();
            InitializeHttpClient();
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Kurulum işlemini başlatır - her installer kendi implementasyonunu yapar
        /// </summary>
        public abstract Task<InstallationResult> InstallAsync();

        /// <summary>
        /// Program kurulu mu kontrol eder
        /// </summary>
        public abstract bool IsInstalled();

        /// <summary>
        /// Installer'ın adını döndürür
        /// </summary>
        public abstract string GetInstallerName();

        #endregion

        #region Protected Virtual Methods

        /// <summary>
        /// Dosya indirme işlemi
        /// </summary>
        protected virtual async Task<string> DownloadFileAsync(string url, string fileName = null)
        {
            try
            {
                Status = "İndiriliyor...";

                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                }

                string filePath = Path.Combine(DownloadsDirectory, fileName);

                using (var response = await HttpClient.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var totalBytesRead = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var bytesRead = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                Progress = (int)((totalBytesRead * 50) / totalBytes); // İndirme %50'ye kadar
                            }
                        }
                    }
                }

                Status = "İndirme tamamlandı";
                return filePath;
            }
            catch (Exception ex)
            {
                Status = $"İndirme hatası: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Executable dosyayı çalıştırır
        /// </summary>
        protected virtual async Task<bool> ExecuteInstallerAsync(string filePath, string arguments = "")
        {
            try
            {
                Status = "Kurulum başlatılıyor...";
                Progress = 60;

                var startInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    Progress = 80;
                    Status = "Kurulum devam ediyor...";

                    await process.WaitForExitAsync();

                    Progress = 100;
                    Status = process.ExitCode == 0 ? "Kurulum tamamlandı" : "Kurulum hatası";

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Kurulum hatası: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Registry kontrolü yapar
        /// </summary>
        protected virtual bool CheckRegistryForProgram(string programName)
        {
            try
            {
                // Registry kontrolü burada yapılacak
                // Microsoft.Win32.Registry kullanarak
                return false; // Placeholder
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Dosya/klasör varlığı kontrolü
        /// </summary>
        protected virtual bool CheckFileExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        #endregion

        #region Private Methods

        private void InitializeDirectories()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string yafesPath = Path.Combine(appData, "YafesV2");

            TempDirectory = Path.Combine(yafesPath, "Temp");
            DownloadsDirectory = Path.Combine(yafesPath, "Downloads");

            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(DownloadsDirectory);
        }

        private void InitializeHttpClient()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("User-Agent",
                "YafesV2/2.0 (Windows NT; Installer)");
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnProgressChanged(int progress)
        {
            ProgressChanged?.Invoke(this, new InstallationProgressEventArgs(progress));
        }

        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, new InstallationStatusEventArgs(status));
        }

        #endregion

        #region IDisposable

        public virtual void Dispose()
        {
            HttpClient?.Dispose();
        }

        #endregion
    }

    #region Event Args Classes

    public class InstallationProgressEventArgs : EventArgs
    {
        public int Progress { get; }
        public InstallationProgressEventArgs(int progress) => Progress = progress;
    }

    public class InstallationStatusEventArgs : EventArgs
    {
        public string Status { get; }
        public InstallationStatusEventArgs(string status) => Status = status;
    }

    #endregion
}