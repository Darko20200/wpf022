using System;
using System.ComponentModel;

namespace YafesV2.Models
{
    /// <summary>
    /// İndirme durumu enum'u
    /// </summary>
    public enum DownloadStatus
    {
        Pending = 0,
        Starting = 1,
        Downloading = 2,
        Paused = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6
    }

    /// <summary>
    /// İndirme bilgileri modeli
    /// Download tracking ve progress için
    /// </summary>
    public class DownloadInfo : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Dosya adı
        /// </summary>
        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        /// <summary>
        /// İndirme URL'i
        /// </summary>
        private string _url;
        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }

        /// <summary>
        /// Toplam dosya boyutu (bytes)
        /// </summary>
        private long _totalBytes;
        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                _totalBytes = value;
                OnPropertyChanged(nameof(TotalBytes));
                OnPropertyChanged(nameof(TotalSizeFormatted));
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        /// <summary>
        /// İndirilen byte sayısı
        /// </summary>
        private long _downloadedBytes;
        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set
            {
                _downloadedBytes = value;
                OnPropertyChanged(nameof(DownloadedBytes));
                OnPropertyChanged(nameof(DownloadedSizeFormatted));
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        /// <summary>
        /// İndirme yüzdesi (0-100)
        /// </summary>
        public int ProgressPercentage
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                return (int)((DownloadedBytes * 100) / TotalBytes);
            }
        }

        /// <summary>
        /// İndirme durumu
        /// </summary>
        private DownloadStatus _status;
        public DownloadStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        /// <summary>
        /// İndirme başlangıç zamanı
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// İndirme bitiş zamanı
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// İndirme hızı (bytes/second)
        /// </summary>
        private long _downloadSpeed;
        public long DownloadSpeed
        {
            get => _downloadSpeed;
            set
            {
                _downloadSpeed = value;
                OnPropertyChanged(nameof(DownloadSpeed));
                OnPropertyChanged(nameof(DownloadSpeedFormatted));
            }
        }

        /// <summary>
        /// Hata mesajı (varsa)
        /// </summary>
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        /// <summary>
        /// Geçici dosya yolu
        /// </summary>
        public string TempFilePath { get; set; }

        /// <summary>
        /// Hedef dosya yolu
        /// </summary>
        public string TargetFilePath { get; set; }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Formatlanmış toplam boyut
        /// </summary>
        public string TotalSizeFormatted => FormatBytes(TotalBytes);

        /// <summary>
        /// Formatlanmış indirilen boyut
        /// </summary>
        public string DownloadedSizeFormatted => FormatBytes(DownloadedBytes);

        /// <summary>
        /// Formatlanmış indirme hızı
        /// </summary>
        public string DownloadSpeedFormatted => $"{FormatBytes(DownloadSpeed)}/s";

        /// <summary>
        /// Durum metni
        /// </summary>
        public string StatusText
        {
            get
            {
                return Status switch
                {
                    DownloadStatus.Pending => "Beklemede",
                    DownloadStatus.Starting => "Başlatılıyor",
                    DownloadStatus.Downloading => "İndiriliyor",
                    DownloadStatus.Paused => "Duraklatıldı",
                    DownloadStatus.Completed => "Tamamlandı",
                    DownloadStatus.Failed => "Başarısız",
                    DownloadStatus.Cancelled => "İptal Edildi",
                    _ => "Bilinmiyor"
                };
            }
        }

        /// <summary>
        /// Tahmini kalan süre
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining
        {
            get
            {
                if (DownloadSpeed <= 0 || Status != DownloadStatus.Downloading)
                    return null;

                var remainingBytes = TotalBytes - DownloadedBytes;
                if (remainingBytes <= 0) return TimeSpan.Zero;

                var secondsRemaining = remainingBytes / DownloadSpeed;
                return TimeSpan.FromSeconds(secondsRemaining);
            }
        }

        /// <summary>
        /// Formatlanmış kalan süre
        /// </summary>
        public string EstimatedTimeRemainingFormatted
        {
            get
            {
                var eta = EstimatedTimeRemaining;
                if (!eta.HasValue) return "Bilinmiyor";

                if (eta.Value.TotalDays >= 1)
                    return $"{eta.Value.Days}g {eta.Value.Hours}s";
                else if (eta.Value.TotalHours >= 1)
                    return $"{eta.Value.Hours}s {eta.Value.Minutes}d";
                else
                    return $"{eta.Value.Minutes}d {eta.Value.Seconds}s";
            }
        }

        #endregion

        #region Constructor

        public DownloadInfo()
        {
            Status = DownloadStatus.Pending;
            StartTime = DateTime.Now;
        }

        public DownloadInfo(string url, string fileName) : this()
        {
            Url = url;
            FileName = fileName;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// İndirmeyi başlatır
        /// </summary>
        public void Start()
        {
            Status = DownloadStatus.Starting;
            StartTime = DateTime.Now;
        }

        /// <summary>
        /// İndirmeyi tamamlar
        /// </summary>
        public void Complete()
        {
            Status = DownloadStatus.Completed;
            EndTime = DateTime.Now;
            DownloadedBytes = TotalBytes;
        }

        /// <summary>
        /// İndirmeyi başarısız yapar
        /// </summary>
        public void Fail(string errorMessage)
        {
            Status = DownloadStatus.Failed;
            EndTime = DateTime.Now;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// İndirmeyi iptal eder
        /// </summary>
        public void Cancel()
        {
            Status = DownloadStatus.Cancelled;
            EndTime = DateTime.Now;
        }

        /// <summary>
        /// İndirmeyi duraklatır
        /// </summary>
        public void Pause()
        {
            if (Status == DownloadStatus.Downloading)
            {
                Status = DownloadStatus.Paused;
            }
        }

        /// <summary>
        /// İndirmeyi devam ettirir
        /// </summary>
        public void Resume()
        {
            if (Status == DownloadStatus.Paused)
            {
                Status = DownloadStatus.Downloading;
            }
        }

        /// <summary>
        /// Progress günceller
        /// </summary>
        public void UpdateProgress(long downloadedBytes, long? totalBytes = null)
        {
            DownloadedBytes = downloadedBytes;
            if (totalBytes.HasValue)
                TotalBytes = totalBytes.Value;

            if (Status == DownloadStatus.Starting || Status == DownloadStatus.Pending)
            {
                Status = DownloadStatus.Downloading;
            }
        }

        /// <summary>
        /// Download hızını hesaplar
        /// </summary>
        public void CalculateSpeed()
        {
            var elapsed = DateTime.Now - StartTime;
            if (elapsed.TotalSeconds > 0)
            {
                DownloadSpeed = (long)(DownloadedBytes / elapsed.TotalSeconds);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Byte'ları formatlar (KB, MB, GB)
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double doubleBytes = bytes;

            while (doubleBytes >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                doubleBytes /= 1024;
                suffixIndex++;
            }

            return $"{doubleBytes:0.##} {suffixes[suffixIndex]}";
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}