// 7Services/NetworkService.cs
using System;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace YafesV2.Services
{
    /// <summary>
    /// V1'deki network kontrollerini modern hale getiren service
    /// V1'deki IsInternetAvailable() metodundan adapte edilmiştir
    /// </summary>
    public class NetworkService : INotifyPropertyChanged, IDisposable
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<NetworkStatusChangedEventArgs> NetworkStatusChanged;

        #endregion

        #region Properties

        private bool _isInternetAvailable;
        public bool IsInternetAvailable
        {
            get => _isInternetAvailable;
            private set
            {
                if (_isInternetAvailable != value)
                {
                    var oldStatus = _isInternetAvailable;
                    _isInternetAvailable = value;
                    OnPropertyChanged(nameof(IsInternetAvailable));
                    OnNetworkStatusChanged(oldStatus, value);
                }
            }
        }

        private string _networkStatus;
        public string NetworkStatus
        {
            get => _networkStatus;
            private set
            {
                _networkStatus = value;
                OnPropertyChanged(nameof(NetworkStatus));
            }
        }

        private long _latency;
        public long Latency
        {
            get => _latency;
            private set
            {
                _latency = value;
                OnPropertyChanged(nameof(Latency));
            }
        }

        /// <summary>
        /// Ping test edilecek host'lar (V1'deki Google DNS mantığı)
        /// </summary>
        public List<string> TestHosts { get; set; } = new List<string>
        {
            "8.8.8.8",        // Google DNS - V1'den
            "1.1.1.1",        // Cloudflare DNS
            "8.8.4.4",        // Google DNS Secondary
            "208.67.222.222"  // OpenDNS
        };

        /// <summary>
        /// HTTP test edilecek URL'ler
        /// </summary>
        public List<string> TestUrls { get; set; } = new List<string>
        {
            "https://www.google.com",
            "https://www.microsoft.com",
            "https://www.cloudflare.com"
        };

        private readonly HttpClient _httpClient;
        private readonly System.Threading.Timer _statusTimer;

        #endregion

        #region Constructor

        public NetworkService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "YafesV2/2.0 NetworkService");

            // Periyodik status kontrolü (her 30 saniyede)
            _statusTimer = new System.Threading.Timer(async _ => await CheckNetworkStatusAsync(),
                null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            NetworkStatus = "Kontrol ediliyor...";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// V1'deki IsInternetAvailable() metodunun modern versiyonu
        /// </summary>
        public async Task<bool> CheckInternetAvailabilityAsync()
        {
            try
            {
                // Önce network interface kontrolü
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    NetworkStatus = "Ağ adaptörü aktif değil";
                    return false;
                }

                // Ping testi (V1'deki mantık)
                var pingSuccess = await TestPingConnectivityAsync();
                if (pingSuccess)
                {
                    // HTTP testi
                    var httpSuccess = await TestHttpConnectivityAsync();
                    if (httpSuccess)
                    {
                        NetworkStatus = $"İnternet bağlantısı aktif (Ping: {Latency}ms)";
                        return true;
                    }
                }

                NetworkStatus = "İnternet bağlantısı yok";
                return false;
            }
            catch (Exception ex)
            {
                NetworkStatus = $"Bağlantı kontrolü hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// V1'deki ping mantığını kullanarak bağlantı testi
        /// </summary>
        public async Task<bool> TestPingConnectivityAsync()
        {
            try
            {
                using (var ping = new Ping())
                {
                    foreach (var host in TestHosts)
                    {
                        try
                        {
                            // V1'deki 2000ms timeout mantığı
                            var reply = await ping.SendPingAsync(host, 2000);
                            if (reply != null && reply.Status == IPStatus.Success)
                            {
                                Latency = reply.RoundtripTime;
                                return true;
                            }
                        }
                        catch
                        {
                            // Bu host'ta hata, diğerine geç
                            continue;
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
        /// HTTP bağlantı testi
        /// </summary>
        public async Task<bool> TestHttpConnectivityAsync()
        {
            try
            {
                foreach (var url in TestUrls)
                {
                    try
                    {
                        using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Bu URL'de hata, diğerine geç
                        continue;
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
        /// Network bilgilerini alır
        /// </summary>
        public NetworkInformation GetNetworkInformation()
        {
            var info = new NetworkInformation();

            try
            {
                info.IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
                info.IsInternetAvailable = IsInternetAvailable;
                info.Latency = Latency;
                info.Status = NetworkStatus;

                // Aktif network interface'leri
                var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .ToList();

                info.ActiveInterfaceCount = activeInterfaces.Count;
                info.NetworkInterfaces = activeInterfaces.Select(ni => new NetworkInterfaceInfo
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    Type = ni.NetworkInterfaceType.ToString(),
                    Speed = ni.Speed,
                    OperationalStatus = ni.OperationalStatus.ToString()
                }).ToList();

                // IP adresleri
                foreach (var ni in activeInterfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            info.IPAddresses.Add(addr.Address.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                info.Status = $"Network bilgisi alınırken hata: {ex.Message}";
            }

            return info;
        }

        /// <summary>
        /// Belirli bir host'a ping atar
        /// </summary>
        public async Task<PingResult> PingHostAsync(string host, int timeout = 5000)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, timeout);
                    return new PingResult
                    {
                        Host = host,
                        IsSuccess = reply?.Status == IPStatus.Success,
                        RoundtripTime = reply?.RoundtripTime ?? -1,
                        Status = reply?.Status.ToString() ?? "Unknown",
                        Timestamp = DateTime.Now
                    };
                }
            }
            catch (Exception ex)
            {
                return new PingResult
                {
                    Host = host,
                    IsSuccess = false,
                    RoundtripTime = -1,
                    Status = $"Error: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// URL'e HTTP testi yapar
        /// </summary>
        public async Task<HttpTestResult> TestUrlAsync(string url, int timeout = 10000)
        {
            try
            {
                var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
                var startTime = DateTime.Now;

                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    var endTime = DateTime.Now;
                    return new HttpTestResult
                    {
                        Url = url,
                        IsSuccess = response.IsSuccessStatusCode,
                        StatusCode = (int)response.StatusCode,
                        ResponseTime = (endTime - startTime).TotalMilliseconds,
                        Status = response.StatusCode.ToString(),
                        Timestamp = DateTime.Now
                    };
                }
            }
            catch (Exception ex)
            {
                return new HttpTestResult
                {
                    Url = url,
                    IsSuccess = false,
                    StatusCode = -1,
                    ResponseTime = -1,
                    Status = $"Error: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Comprehensive network test - V1'deki mantığın gelişmiş versiyonu
        /// </summary>
        public async Task<NetworkTestResult> RunComprehensiveTestAsync()
        {
            var result = new NetworkTestResult();
            result.StartTime = DateTime.Now;

            try
            {
                // 1. Network interface kontrolü
                result.IsNetworkInterfaceAvailable = NetworkInterface.GetIsNetworkAvailable();

                // 2. Ping testleri
                var pingTasks = TestHosts.Select(host => PingHostAsync(host));
                result.PingResults = (await Task.WhenAll(pingTasks)).ToList();
                result.PingSuccessCount = result.PingResults.Count(p => p.IsSuccess);

                // 3. HTTP testleri
                var httpTasks = TestUrls.Select(url => TestUrlAsync(url));
                result.HttpResults = (await Task.WhenAll(httpTasks)).ToList();
                result.HttpSuccessCount = result.HttpResults.Count(h => h.IsSuccess);

                // 4. Genel değerlendirme
                result.IsInternetAvailable = result.PingSuccessCount > 0 && result.HttpSuccessCount > 0;
                result.AverageLatency = result.PingResults.Where(p => p.IsSuccess).Average(p => p.RoundtripTime);

                result.Status = result.IsInternetAvailable ? "İnternet bağlantısı aktif" : "İnternet bağlantısı yok";
            }
            catch (Exception ex)
            {
                result.Status = $"Test hatası: {ex.Message}";
            }

            result.EndTime = DateTime.Now;
            result.Duration = result.EndTime - result.StartTime;

            return result;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Periyodik network status kontrolü
        /// </summary>
        private async Task CheckNetworkStatusAsync()
        {
            try
            {
                IsInternetAvailable = await CheckInternetAvailabilityAsync();
            }
            catch
            {
                IsInternetAvailable = false;
                NetworkStatus = "Bağlantı kontrolü başarısız";
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnNetworkStatusChanged(bool oldStatus, bool newStatus)
        {
            NetworkStatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(oldStatus, newStatus));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _statusTimer?.Dispose();
            _httpClient?.Dispose();
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Network bilgileri modeli
    /// </summary>
    public class NetworkInformation
    {
        public bool IsNetworkAvailable { get; set; }
        public bool IsInternetAvailable { get; set; }
        public long Latency { get; set; }
        public string Status { get; set; }
        public int ActiveInterfaceCount { get; set; }
        public List<NetworkInterfaceInfo> NetworkInterfaces { get; set; } = new List<NetworkInterfaceInfo>();
        public List<string> IPAddresses { get; set; } = new List<string>();
    }

    public class NetworkInterfaceInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public long Speed { get; set; }
        public string OperationalStatus { get; set; }
    }

    /// <summary>
    /// Ping test sonucu
    /// </summary>
    public class PingResult
    {
        public string Host { get; set; }
        public bool IsSuccess { get; set; }
        public long RoundtripTime { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// HTTP test sonucu
    /// </summary>
    public class HttpTestResult
    {
        public string Url { get; set; }
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public double ResponseTime { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Comprehensive network test sonucu
    /// </summary>
    public class NetworkTestResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsNetworkInterfaceAvailable { get; set; }
        public bool IsInternetAvailable { get; set; }
        public string Status { get; set; }

        public List<PingResult> PingResults { get; set; } = new List<PingResult>();
        public List<HttpTestResult> HttpResults { get; set; } = new List<HttpTestResult>();

        public int PingSuccessCount { get; set; }
        public int HttpSuccessCount { get; set; }
        public double AverageLatency { get; set; }
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Network status değişikliği event args
    /// </summary>
    public class NetworkStatusChangedEventArgs : EventArgs
    {
        public bool OldStatus { get; }
        public bool NewStatus { get; }
        public DateTime Timestamp { get; }

        public NetworkStatusChangedEventArgs(bool oldStatus, bool newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Timestamp = DateTime.Now;
        }
    }

    #endregion
}