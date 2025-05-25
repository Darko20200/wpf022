// 7Services/DownloadService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using YafesV2.Models;

namespace YafesV2.Services
{
    /// <summary>
    /// V1'deki download mantığını modern async/await pattern ile implement eder
    /// V1'deki BaseInstaller.DownloadFileAsync metodundan adapte edilmiştir
    /// </summary>
    public class DownloadService : INotifyPropertyChanged, IDisposable
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DownloadProgressEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;

        #endregion

        #region Properties

        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, CancellationTokenSource> _activeCancellationTokens;
        private readonly Dictionary<string, DownloadInfo> _activeDownloads;

        /// <summary>
        /// Aktif download sayısı
        /// </summary>
        public int ActiveDownloadCount => _activeDownloads.Count;

        /// <summary>
        /// Maksimum eşzamanlı download sayısı
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 3;

        /// <summary>
        /// Download timeout (saniye)
        /// </summary>
        public int DownloadTimeout { get; set; } = 300;

        #endregion

        #region Constructor

        public DownloadService()
        {
            _httpClient = new HttpClient();
            _activeCancellationTokens = new Dictionary<string, CancellationTokenSource>();
            _activeDownloads = new Dictionary<string, DownloadInfo>();

            // V1'deki User-Agent pattern
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "YafesV2/2.0 (Windows NT; Installer)");
            _httpClient.Timeout = TimeSpan.FromSeconds(DownloadTimeout);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// V1'deki download mantığını modern hale getirir
        /// BaseInstaller.DownloadFileAsync'den adapte
        /// </summary>
        public async Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string fileName = null,
            IProgress<DownloadProgressEventArgs> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                return DownloadResult.Failed("URL boş olamaz");

            if (string.IsNullOrEmpty(fileName))
                fileName = Path.GetFileName(new Uri(url).AbsolutePath);

            if (string.IsNullOrEmpty(fileName))
                fileName = $"download_{Guid.NewGuid():N}.tmp";

            var downloadId = Guid.NewGuid().ToString();
            var fullPath = Path.Combine(targetPath, fileName);

            // Download info oluştur
            var downloadInfo = new DownloadInfo(url, fileName)
            {
                TargetFilePath = fullPath,
                TempFilePath = fullPath + ".tmp"
            };

            try
            {
                // Directory oluştur
                Directory.CreateDirectory(targetPath);

                // Active downloads'a ekle
                _activeDownloads[downloadId] = downloadInfo;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeCancellationTokens[downloadId] = cts;

                downloadInfo.Start();

                // V1'deki progress reporting mantığı
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    downloadInfo.TotalBytes = totalBytes;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(downloadInfo.TempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var totalBytesRead = 0L;
                        int bytesRead;
                        var lastProgressReport = DateTime.Now;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                            totalBytesRead += bytesRead;

                            downloadInfo.UpdateProgress(totalBytesRead, totalBytes);

                            // Progress reporting (her 100ms'de bir)
                            if ((DateTime.Now - lastProgressReport).TotalMilliseconds > 100)
                            {
                                downloadInfo.CalculateSpeed();
                                var progressArgs = new DownloadProgressEventArgs(downloadInfo);

                                progress?.Report(progressArgs);
                                OnDownloadProgressChanged(progressArgs);

                                lastProgressReport = DateTime.Now;
                            }
                        }
                    }
                }

                // Temp dosyayı asıl dosyaya taşı
                if (File.Exists(downloadInfo.TargetFilePath))
                    File.Delete(downloadInfo.TargetFilePath);

                File.Move(downloadInfo.TempFilePath, downloadInfo.TargetFilePath);

                downloadInfo.Complete();
                OnDownloadCompleted(new DownloadCompletedEventArgs(downloadInfo, true, null));

                return DownloadResult.Success(downloadInfo.TargetFilePath);
            }
            catch (OperationCanceledException)
            {
                downloadInfo.Cancel();
                OnDownloadCompleted(new DownloadCompletedEventArgs(downloadInfo, false, "İptal edildi"));
                return DownloadResult.Cancelled();
            }
            catch (Exception ex)
            {
                downloadInfo.Fail(ex.Message);
                OnDownloadCompleted(new DownloadCompletedEventArgs(downloadInfo, false, ex.Message));
                return DownloadResult.Failed(ex.Message);
            }
            finally
            {
                // Cleanup
                _activeDownloads.Remove(downloadId);
                if (_activeCancellationTokens.TryGetValue(downloadId, out var cts))
                {
                    cts.Dispose();
                    _activeCancellationTokens.Remove(downloadId);
                }

                // Temp dosyayı temizle
                try
                {
                    if (File.Exists(downloadInfo.TempFilePath))
                        File.Delete(downloadInfo.TempFilePath);
                }
                catch { }
            }
        }

        /// <summary>
        /// Eşzamanlı multiple download - V1'deki parallel download mantığı
        /// </summary>
        public async Task<Dictionary<string, DownloadResult>> DownloadMultipleAsync(
            Dictionary<string, string> urlToPathMapping,
            IProgress<MultipleDownloadProgressEventArgs> progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, DownloadResult>();
            var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
            var completedCount = 0;
            var totalCount = urlToPathMapping.Count;

            var tasks = urlToPathMapping.Select(async kvp =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await DownloadFileAsync(kvp.Key, Path.GetDirectoryName(kvp.Value),
                        Path.GetFileName(kvp.Value), cancellationToken: cancellationToken);

                    lock (results)
                    {
                        results[kvp.Key] = result;
                        completedCount++;

                        var progressArgs = new MultipleDownloadProgressEventArgs(
                            completedCount, totalCount, kvp.Key, result);
                        progress?.Report(progressArgs);
                    }

                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// Download'u iptal eder
        /// </summary>
        public void CancelDownload(string downloadId)
        {
            if (_activeCancellationTokens.TryGetValue(downloadId, out var cts))
            {
                cts.Cancel();
            }
        }

        /// <summary>
        /// Tüm aktif download'ları iptal eder
        /// </summary>
        public void CancelAllDownloads()
        {
            foreach (var cts in _activeCancellationTokens.Values)
            {
                cts.Cancel();
            }
        }

        /// <summary>
        /// Download durumunu alır
        /// </summary>
        public DownloadInfo GetDownloadInfo(string downloadId)
        {
            return _activeDownloads.TryGetValue(downloadId, out var info) ? info : null;
        }

        /// <summary>
        /// Tüm aktif download'ları alır
        /// </summary>
        public IEnumerable<DownloadInfo> GetActiveDownloads()
        {
            return _activeDownloads.Values;
        }

        #endregion

        #region Private Methods

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnDownloadProgressChanged(DownloadProgressEventArgs args)
        {
            DownloadProgressChanged?.Invoke(this, args);
        }

        protected virtual void OnDownloadCompleted(DownloadCompletedEventArgs args)
        {
            DownloadCompleted?.Invoke(this, args);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            CancelAllDownloads();

            foreach (var cts in _activeCancellationTokens.Values)
            {
                cts.Dispose();
            }
            _activeCancellationTokens.Clear();

            _httpClient?.Dispose();
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Download sonuç sınıfı
    /// </summary>
    public class DownloadResult
    {
        public bool IsSuccess { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorMessage { get; private set; }
        public DownloadResultType ResultType { get; private set; }

        private DownloadResult(bool isSuccess, string filePath, string errorMessage, DownloadResultType resultType)
        {
            IsSuccess = isSuccess;
            FilePath = filePath;
            ErrorMessage = errorMessage;
            ResultType = resultType;
        }

        public static DownloadResult Success(string filePath) =>
            new DownloadResult(true, filePath, null, DownloadResultType.Success);

        public static DownloadResult Failed(string errorMessage) =>
            new DownloadResult(false, null, errorMessage, DownloadResultType.Failed);

        public static DownloadResult Cancelled() =>
            new DownloadResult(false, null, "Download cancelled", DownloadResultType.Cancelled);
    }

    public enum DownloadResultType
    {
        Success,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Download progress event args
    /// </summary>
    public class DownloadProgressEventArgs : EventArgs
    {
        public DownloadInfo DownloadInfo { get; }
        public DateTime Timestamp { get; }

        public DownloadProgressEventArgs(DownloadInfo downloadInfo)
        {
            DownloadInfo = downloadInfo;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Download completed event args
    /// </summary>
    public class DownloadCompletedEventArgs : EventArgs
    {
        public DownloadInfo DownloadInfo { get; }
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }
        public DateTime Timestamp { get; }

        public DownloadCompletedEventArgs(DownloadInfo downloadInfo, bool isSuccess, string errorMessage)
        {
            DownloadInfo = downloadInfo;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Multiple download progress event args
    /// </summary>
    public class MultipleDownloadProgressEventArgs : EventArgs
    {
        public int CompletedCount { get; }
        public int TotalCount { get; }
        public string CurrentFileName { get; }
        public DownloadResult CurrentResult { get; }
        public int ProgressPercentage => (CompletedCount * 100) / TotalCount;

        public MultipleDownloadProgressEventArgs(int completedCount, int totalCount, string currentFileName, DownloadResult currentResult)
        {
            CompletedCount = completedCount;
            TotalCount = totalCount;
            CurrentFileName = currentFileName;
            CurrentResult = currentResult;
        }
    }

    #endregion
}