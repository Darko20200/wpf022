// 7Services/ResourceService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel;
using YafesV2.Models;

namespace YafesV2.Services
{
    /// <summary>
    /// V1'deki embedded resource mantığını modern hale getiren service
    /// V1'deki ExtractEmbeddedResource metodundan adapte edilmiştir
    /// </summary>
    public class ResourceService : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ResourceExtractionEventArgs> ResourceExtracted;
        public event EventHandler<ResourceProgressEventArgs> ExtractionProgress;

        #endregion

        #region Properties

        /// <summary>
        /// Mevcut assembly
        /// </summary>
        private readonly Assembly _assembly;

        /// <summary>
        /// Çıkarma klasörü
        /// </summary>
        public string ExtractionDirectory { get; set; }

        /// <summary>
        /// Cache klasörü
        /// </summary>
        public string CacheDirectory { get; set; }

        /// <summary>
        /// Gömülü kaynaklar cache'lensin mi
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Mevcut gömülü kaynakların listesi
        /// </summary>
        public List<EmbeddedResourceInfo> AvailableResources { get; private set; }

        #endregion

        #region Constructor

        public ResourceService()
        {
            _assembly = Assembly.GetExecutingAssembly();
            InitializeDirectories();
            LoadAvailableResources();
        }

        public ResourceService(string extractionDirectory, string cacheDirectory = null) : this()
        {
            ExtractionDirectory = extractionDirectory;
            CacheDirectory = cacheDirectory ?? Path.Combine(extractionDirectory, "cache");
            InitializeDirectories();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// V1'deki ExtractEmbeddedResource metodunun modern versiyonu
        /// </summary>
        public async Task<ResourceExtractionResult> ExtractResourceAsync(
            string resourceName,
            string outputFilePath,
            IProgress<ResourceProgressEventArgs> progress = null)
        {
            try
            {
                // Cache kontrol
                if (EnableCaching)
                {
                    var cachedPath = GetCachedResourcePath(resourceName);
                    if (File.Exists(cachedPath))
                    {
                        // Cache'den kopyala
                        await CopyFileAsync(cachedPath, outputFilePath, progress);
                        return ResourceExtractionResult.Success(outputFilePath, true);
                    }
                }

                // Resource'u bul
                var actualResourceName = FindResourceName(resourceName);
                if (string.IsNullOrEmpty(actualResourceName))
                {
                    return ResourceExtractionResult.Failed($"Resource bulunamadı: {resourceName}");
                }

                // Stream al
                using (var resourceStream = _assembly.GetManifestResourceStream(actualResourceName))
                {
                    if (resourceStream == null)
                    {
                        return ResourceExtractionResult.Failed($"Resource stream alınamadı: {actualResourceName}");
                    }

                    var expectedSize = resourceStream.Length;

                    // Progress başlat
                    var progressArgs = new ResourceProgressEventArgs(resourceName, 0, expectedSize);
                    progress?.Report(progressArgs);
                    OnExtractionProgress(progressArgs);

                    // Output dizinini oluştur
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

                    // Dosyaya yaz
                    using (var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                    {
                        await CopyStreamWithProgressAsync(resourceStream, fileStream, resourceName, progress);
                    }

                    // Dosya boyutunu kontrol et (V1'deki mantık)
                    var fileInfo = new FileInfo(outputFilePath);
                    if (fileInfo.Length < expectedSize * 0.9) // En az %90'ı olmalı
                    {
                        return ResourceExtractionResult.Failed($"Çıkarılan dosya eksik. Beklenen: {expectedSize}, Gerçek: {fileInfo.Length}");
                    }

                    // Cache'e kopyala
                    if (EnableCaching)
                    {
                        await CacheResourceAsync(resourceName, outputFilePath);
                    }

                    OnResourceExtracted(new ResourceExtractionEventArgs(resourceName, outputFilePath, true, null));
                    return ResourceExtractionResult.Success(outputFilePath, false);
                }
            }
            catch (Exception ex)
            {
                OnResourceExtracted(new ResourceExtractionEventArgs(resourceName, outputFilePath, false, ex.Message));
                return ResourceExtractionResult.Failed($"Resource çıkarma hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// V1'deki batch extraction mantığı
        /// </summary>
        public async Task<Dictionary<string, ResourceExtractionResult>> ExtractMultipleResourcesAsync(
            Dictionary<string, string> resourceToPathMapping,
            IProgress<MultipleResourceProgressEventArgs> progress = null)
        {
            var results = new Dictionary<string, ResourceExtractionResult>();
            var totalCount = resourceToPathMapping.Count;
            var completedCount = 0;

            foreach (var kvp in resourceToPathMapping)
            {
                var individualProgress = new Progress<ResourceProgressEventArgs>(args =>
                {
                    var multiProgress = new MultipleResourceProgressEventArgs(
                        completedCount, totalCount, kvp.Key, args.Progress);
                    progress?.Report(multiProgress);
                });

                var result = await ExtractResourceAsync(kvp.Key, kvp.Value, individualProgress);
                results[kvp.Key] = result;

                completedCount++;

                var finalProgress = new MultipleResourceProgressEventArgs(
                    completedCount, totalCount, kvp.Key, 100);
                progress?.Report(finalProgress);
            }

            return results;
        }

        /// <summary>
        /// V1'deki resource verification mantığı
        /// </summary>
        public bool VerifyResourceExists(string resourceName)
        {
            return !string.IsNullOrEmpty(FindResourceName(resourceName));
        }

        /// <summary>
        /// Resource bilgisini alır
        /// </summary>
        public EmbeddedResourceInfo GetResourceInfo(string resourceName)
        {
            var actualName = FindResourceName(resourceName);
            if (string.IsNullOrEmpty(actualName))
                return null;

            try
            {
                using (var stream = _assembly.GetManifestResourceStream(actualName))
                {
                    return new EmbeddedResourceInfo
                    {
                        Name = resourceName,
                        ActualName = actualName,
                        Size = stream?.Length ?? 0,
                        IsAvailable = stream != null,
                        Assembly = _assembly.GetName().Name
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tüm mevcut resource'ları listeler (V1'deki ListEmbeddedResources mantığı)
        /// </summary>
        public List<EmbeddedResourceInfo> GetAllAvailableResources()
        {
            return AvailableResources.ToList();
        }

        /// <summary>
        /// Resource cache'ini temizler
        /// </summary>
        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                {
                    Directory.Delete(CacheDirectory, true);
                    Directory.CreateDirectory(CacheDirectory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache temizleme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Cache boyutunu alır
        /// </summary>
        public long GetCacheSize()
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                    return 0;

                return Directory.GetFiles(CacheDirectory, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Cache'deki dosya sayısını alır
        /// </summary>
        public int GetCacheFileCount()
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                    return 0;

                return Directory.GetFiles(CacheDirectory, "*", SearchOption.AllDirectories).Length;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Dizinleri başlatır
        /// </summary>
        private void InitializeDirectories()
        {
            if (string.IsNullOrEmpty(ExtractionDirectory))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                ExtractionDirectory = Path.Combine(appData, "YafesV2", "Extracted");
            }

            if (string.IsNullOrEmpty(CacheDirectory))
            {
                CacheDirectory = Path.Combine(ExtractionDirectory, "cache");
            }

            Directory.CreateDirectory(ExtractionDirectory);
            Directory.CreateDirectory(CacheDirectory);
        }

        /// <summary>
        /// Mevcut resource'ları yükler
        /// </summary>
        private void LoadAvailableResources()
        {
            AvailableResources = new List<EmbeddedResourceInfo>();

            try
            {
                var resourceNames = _assembly.GetManifestResourceNames();
                foreach (var resourceName in resourceNames)
                {
                    try
                    {
                        using (var stream = _assembly.GetManifestResourceStream(resourceName))
                        {
                            AvailableResources.Add(new EmbeddedResourceInfo
                            {
                                Name = GetFriendlyName(resourceName),
                                ActualName = resourceName,
                                Size = stream?.Length ?? 0,
                                IsAvailable = stream != null,
                                Assembly = _assembly.GetName().Name
                            });
                        }
                    }
                    catch
                    {
                        // Resource okunamazsa atla
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Resource listesi yüklenirken hata: {ex.Message}");
            }
        }

        /// <summary>
        /// V1'deki resource name matching mantığı
        /// </summary>
        private string FindResourceName(string resourceName)
        {
            var resourceNames = _assembly.GetManifestResourceNames();

            // Tam eşleşme
            var exactMatch = resourceNames.FirstOrDefault(r =>
                r.Equals(resourceName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null) return exactMatch;

            // YafesV2.Resources. prefix ile
            var prefixedMatch = resourceNames.FirstOrDefault(r =>
                r.Equals($"YafesV2.Resources.{resourceName}", StringComparison.OrdinalIgnoreCase));
            if (prefixedMatch != null) return prefixedMatch;

            // Dosya adı ile eşleşme (V1'deki mantık)
            var fileNameMatch = resourceNames.FirstOrDefault(r =>
                r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fileNameMatch != null) return fileNameMatch;

            // Contains kontrolü
            var containsMatch = resourceNames.FirstOrDefault(r =>
                r.Contains(resourceName, StringComparison.OrdinalIgnoreCase));

            return containsMatch;
        }

        /// <summary>
        /// Resource name'den friendly name oluşturur
        /// </summary>
        private string GetFriendlyName(string resourceName)
        {
            // YafesV2.Resources. prefix'ini kaldır
            if (resourceName.StartsWith("YafesV2.Resources."))
            {
                return resourceName.Substring("YafesV2.Resources.".Length);
            }

            return resourceName;
        }

        /// <summary>
        /// Cache'deki resource yolunu alır
        /// </summary>
        private string GetCachedResourcePath(string resourceName)
        {
            var safeFileName = GetSafeFileName(resourceName);
            return Path.Combine(CacheDirectory, safeFileName);
        }

        /// <summary>
        /// Güvenli dosya adı oluşturur
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Stream'i progress ile kopyalar (V1'deki mantık)
        /// </summary>
        private async Task CopyStreamWithProgressAsync(
            Stream source,
            Stream destination,
            string resourceName,
            IProgress<ResourceProgressEventArgs> progress)
        {
            var buffer = new byte[8192];
            long totalBytesRead = 0;
            long totalBytes = source.Length;
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                    var progressArgs = new ResourceProgressEventArgs(resourceName, progressPercentage, totalBytes);
                    progress?.Report(progressArgs);
                    OnExtractionProgress(progressArgs);
                }
            }
        }

        /// <summary>
        /// Dosyayı progress ile kopyalar
        /// </summary>
        private async Task CopyFileAsync(
            string sourcePath,
            string destinationPath,
            IProgress<ResourceProgressEventArgs> progress)
        {
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await CopyStreamWithProgressAsync(sourceStream, destStream, Path.GetFileName(destinationPath), progress);
            }
        }

        /// <summary>
        /// Resource'u cache'e kopyalar
        /// </summary>
        private async Task CacheResourceAsync(string resourceName, string sourcePath)
        {
            try
            {
                var cachedPath = GetCachedResourcePath(resourceName);
                var cachedDir = Path.GetDirectoryName(cachedPath);
                Directory.CreateDirectory(cachedDir);

                await CopyFileAsync(sourcePath, cachedPath, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache kaydetme hatası: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnResourceExtracted(ResourceExtractionEventArgs args)
        {
            ResourceExtracted?.Invoke(this, args);
        }

        protected virtual void OnExtractionProgress(ResourceProgressEventArgs args)
        {
            ExtractionProgress?.Invoke(this, args);
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Embedded resource bilgisi
    /// </summary>
    public class EmbeddedResourceInfo
    {
        public string Name { get; set; }
        public string ActualName { get; set; }
        public long Size { get; set; }
        public bool IsAvailable { get; set; }
        public string Assembly { get; set; }

        public string FormattedSize
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = Size;
                int order = 0;

                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }

                return $"{len:0.##} {sizes[order]}";
            }
        }
    }

    /// <summary>
    /// Resource extraction sonucu
    /// </summary>
    public class ResourceExtractionResult
    {
        public bool IsSuccess { get; private set; }
        public string FilePath { get; private set; }
        public string ErrorMessage { get; private set; }
        public bool IsFromCache { get; private set; }

        private ResourceExtractionResult(bool isSuccess, string filePath, string errorMessage, bool isFromCache)
        {
            IsSuccess = isSuccess;
            FilePath = filePath;
            ErrorMessage = errorMessage;
            IsFromCache = isFromCache;
        }

        public static ResourceExtractionResult Success(string filePath, bool isFromCache) =>
            new ResourceExtractionResult(true, filePath, null, isFromCache);

        public static ResourceExtractionResult Failed(string errorMessage) =>
            new ResourceExtractionResult(false, null, errorMessage, false);
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Resource extraction event args
    /// </summary>
    public class ResourceExtractionEventArgs : EventArgs
    {
        public string ResourceName { get; }
        public string OutputPath { get; }
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }
        public DateTime Timestamp { get; }

        public ResourceExtractionEventArgs(string resourceName, string outputPath, bool isSuccess, string errorMessage)
        {
            ResourceName = resourceName;
            OutputPath = outputPath;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Resource progress event args
    /// </summary>
    public class ResourceProgressEventArgs : EventArgs
    {
        public string ResourceName { get; }
        public int Progress { get; }
        public long TotalBytes { get; }
        public DateTime Timestamp { get; }

        public ResourceProgressEventArgs(string resourceName, int progress, long totalBytes)
        {
            ResourceName = resourceName;
            Progress = progress;
            TotalBytes = totalBytes;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Multiple resource progress event args
    /// </summary>
    public class MultipleResourceProgressEventArgs : EventArgs
    {
        public int CompletedCount { get; }
        public int TotalCount { get; }
        public string CurrentResource { get; }
        public int CurrentProgress { get; }
        public int OverallProgress => (CompletedCount * 100) / TotalCount;

        public MultipleResourceProgressEventArgs(int completedCount, int totalCount, string currentResource, int currentProgress)
        {
            CompletedCount = completedCount;
            TotalCount = totalCount;
            CurrentResource = currentResource;
            CurrentProgress = currentProgress;
        }
    }

    #endregion
}