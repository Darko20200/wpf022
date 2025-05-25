// 8Utilities/FileUtility.cs
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace YafesV2.Utilities
{
    /// <summary>
    /// V1'deki dosya işlemlerini modern hale getiren utility sınıfı
    /// V1'deki CleanupTempFiles, VerifyDownloadedFile metodlarından adapte edilmiştir
    /// </summary>
    public static class FileUtility
    {
        #region File Operations

        /// <summary>
        /// V1'deki dosya kopyalama mantığını modern hale getirir
        /// </summary>
        public static async Task<bool> CopyFileAsync(string sourcePath, string destinationPath,
            IProgress<int> progress = null, bool overwrite = true)
        {
            try
            {
                if (!File.Exists(sourcePath))
                    return false;

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                using (var destStream = new FileStream(destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew))
                {
                    var buffer = new byte[8192];
                    long totalBytes = sourceStream.Length;
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await destStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                            progress?.Report(progressPercentage);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Klasörü recursive olarak kopyalar (V1'deki CopyDirectory mantığı)
        /// </summary>
        public static async Task<bool> CopyDirectoryAsync(string sourceDir, string destDir,
            bool overwrite = true, IProgress<DirectoryCopyProgress> progress = null)
        {
            try
            {
                if (!Directory.Exists(sourceDir))
                    return false;

                Directory.CreateDirectory(destDir);

                var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
                var totalFiles = files.Length;
                var completedFiles = 0;

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(sourceDir, file);
                    var destFile = Path.Combine(destDir, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                    var fileProgress = new Progress<int>(p =>
                    {
                        var overallProgress = new DirectoryCopyProgress
                        {
                            CompletedFiles = completedFiles,
                            TotalFiles = totalFiles,
                            CurrentFile = Path.GetFileName(file),
                            CurrentFileProgress = p
                        };
                        progress?.Report(overallProgress);
                    });

                    await CopyFileAsync(file, destFile, fileProgress, overwrite);
                    completedFiles++;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// V1'deki CleanupTempFiles mantığının gelişmiş versiyonu
        /// </summary>
        public static async Task<CleanupResult> CleanupDirectoryAsync(string directoryPath,
            CleanupOptions options = null)
        {
            options ??= new CleanupOptions();
            var result = new CleanupResult { DirectoryPath = directoryPath };

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    result.IsSuccess = true;
                    result.Message = "Klasör zaten mevcut değil";
                    return result;
                }

                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                result.TotalFiles = files.Length;

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);

                        // Boyut filtresi
                        if (options.MaxFileSize.HasValue && fileInfo.Length > options.MaxFileSize.Value)
                            continue;

                        // Yaş filtresi
                        if (options.OlderThanDays.HasValue)
                        {
                            var age = DateTime.Now - fileInfo.LastWriteTime;
                            if (age.TotalDays < options.OlderThanDays.Value)
                                continue;
                        }

                        // Extension filtresi
                        if (options.FileExtensions?.Any() == true)
                        {
                            if (!options.FileExtensions.Contains(fileInfo.Extension.ToLower()))
                                continue;
                        }

                        // Pattern filtresi
                        if (!string.IsNullOrEmpty(options.FileNamePattern))
                        {
                            if (!fileInfo.Name.Contains(options.FileNamePattern, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        File.Delete(file);
                        result.DeletedFiles++;
                        result.FreedSpace += fileInfo.Length;
                    }
                    catch
                    {
                        result.FailedFiles++;
                    }
                }

                // Boş klasörleri sil
                if (options.RemoveEmptyDirectories)
                {
                    await RemoveEmptyDirectoriesAsync(directoryPath);
                }

                result.IsSuccess = true;
                result.Message = $"{result.DeletedFiles} dosya silindi, {FormatFileSize(result.FreedSpace)} alan serbest bırakıldı";
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Boş klasörleri recursive olarak siler
        /// </summary>
        public static async Task RemoveEmptyDirectoriesAsync(string directoryPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    foreach (var directory in Directory.GetDirectories(directoryPath))
                    {
                        RemoveEmptyDirectoriesAsync(directory).Wait();

                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            Directory.Delete(directory);
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }
            });
        }

        #endregion

        #region File Verification

        /// <summary>
        /// V1'deki VerifyDownloadedFile mantığının gelişmiş versiyonu
        /// </summary>
        public static FileVerificationResult VerifyFile(string filePath, FileVerificationOptions options = null)
        {
            options ??= new FileVerificationOptions();
            var result = new FileVerificationResult { FilePath = filePath };

            try
            {
                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Dosya bulunamadı";
                    return result;
                }

                var fileInfo = new FileInfo(filePath);
                result.FileSize = fileInfo.Length;
                result.LastModified = fileInfo.LastWriteTime;

                // Boyut kontrolü (V1'deki mantık)
                if (options.MinimumSize.HasValue && fileInfo.Length < options.MinimumSize.Value)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Dosya çok küçük: {fileInfo.Length} bytes (minimum: {options.MinimumSize.Value})";
                    return result;
                }

                if (options.ExpectedSize.HasValue)
                {
                    var tolerance = options.SizeTolerance ?? 0.1; // %10 tolerans
                    var minSize = options.ExpectedSize.Value * (1 - tolerance);
                    var maxSize = options.ExpectedSize.Value * (1 + tolerance);

                    if (fileInfo.Length < minSize || fileInfo.Length > maxSize)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Dosya boyutu beklenen aralıkta değil. Gerçek: {fileInfo.Length}, Beklenen: {options.ExpectedSize.Value}";
                        return result;
                    }
                }

                // Hash kontrolü
                if (!string.IsNullOrEmpty(options.ExpectedHash))
                {
                    var calculatedHash = CalculateFileHash(filePath, options.HashAlgorithm);
                    if (!calculatedHash.Equals(options.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Hash kontrolü başarısız";
                        result.CalculatedHash = calculatedHash;
                        return result;
                    }
                    result.CalculatedHash = calculatedHash;
                }

                // Magic number kontrolü (dosya tipi)
                if (options.ExpectedFileType != FileType.Unknown)
                {
                    var detectedType = DetectFileType(filePath);
                    if (detectedType != options.ExpectedFileType)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Dosya tipi uyumsuz. Beklenen: {options.ExpectedFileType}, Tespit edilen: {detectedType}";
                        result.DetectedFileType = detectedType;
                        return result;
                    }
                    result.DetectedFileType = detectedType;
                }

                result.IsValid = true;
                result.ErrorMessage = "Dosya doğrulandı";
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage