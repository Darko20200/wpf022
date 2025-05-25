namespace YafesV2.Models
{
    /// <summary>
    /// Kurulum işlemi sonuç durumları
    /// </summary>
    public enum InstallationResult
    {
        /// <summary>
        /// Kurulum başarıyla tamamlandı
        /// </summary>
        Success = 0,

        /// <summary>
        /// Program zaten kurulu
        /// </summary>
        AlreadyInstalled = 1,

        /// <summary>
        /// İndirme başarısız
        /// </summary>
        DownloadFailed = 2,

        /// <summary>
        /// Kurulum başarısız
        /// </summary>
        InstallationFailed = 3,

        /// <summary>
        /// Sistem gereksinimleri karşılanmıyor
        /// </summary>
        SystemRequirementsNotMet = 4,

        /// <summary>
        /// Kısmi başarı (bazı işlemler başarısız)
        /// </summary>
        PartialSuccess = 5,

        /// <summary>
        /// Bilinmeyen hata
        /// </summary>
        Error = 6,

        /// <summary>
        /// Kullanıcı işlemi iptal etti
        /// </summary>
        Cancelled = 7,

        /// <summary>
        /// Yetki yetersiz
        /// </summary>
        InsufficientPermissions = 8,

        /// <summary>
        /// Disk alanı yetersiz
        /// </summary>
        InsufficientDiskSpace = 9,

        /// <summary>
        /// Ağ bağlantısı hatası
        /// </summary>
        NetworkError = 10
    }
}