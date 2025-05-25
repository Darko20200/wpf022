using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace YafesV2.Managers
{
    /// <summary>
    /// UI state ve window management için manager sınıfı
    /// Theme, notification, window yönetimi
    /// </summary>
    public class UIManager : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        public event EventHandler<NotificationEventArgs> NotificationRequested;
        public event EventHandler<WindowStateEventArgs> WindowStateChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Mevcut tema
        /// </summary>
        private string _currentTheme = "Dark";
        public string CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    var oldTheme = _currentTheme;
                    _currentTheme = value;
                    OnPropertyChanged(nameof(CurrentTheme));
                    ApplyTheme(value);
                    OnThemeChanged(oldTheme, value);
                }
            }
        }

        /// <summary>
        /// Ana pencere referansı
        /// </summary>
        public Window MainWindow { get; set; }

        /// <summary>
        /// Aktif window'lar
        /// </summary>
        private readonly Dictionary<string, Window> _activeWindows;

        /// <summary>
        /// UI busy durumu
        /// </summary>
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                UpdateCursor();
            }
        }

        /// <summary>
        /// Status bar mesajı
        /// </summary>
        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        /// <summary>
        /// Progress değeri (0-100)
        /// </summary>
        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = Math.Max(0, Math.Min(100, value));
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        /// <summary>
        /// Progress görünür mü
        /// </summary>
        private bool _isProgressVisible;
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set
            {
                _isProgressVisible = value;
                OnPropertyChanged(nameof(IsProgressVisible));
            }
        }

        /// <summary>
        /// Bildirimler etkin mi
        /// </summary>
        public bool NotificationsEnabled { get; set; } = true;

        /// <summary>
        /// Mevcut temalar
        /// </summary>
        public List<string> AvailableThemes { get; private set; }

        #endregion

        #region Singleton

        private static UIManager _instance;
        private static readonly object _lock = new object();

        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new UIManager();
                    }
                }
                return _instance;
            }
        }

        private UIManager()
        {
            _activeWindows = new Dictionary<string, Window>();
            InitializeThemes();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// UI Manager'ı başlatır
        /// </summary>
        public void Initialize(Window mainWindow)
        {
            MainWindow = mainWindow;
            RegisterWindow("MainWindow", mainWindow);

            // Main window events
            mainWindow.StateChanged += MainWindow_StateChanged;
            mainWindow.Closing += MainWindow_Closing;

            ApplyTheme(CurrentTheme);
            StatusMessage = "Hazır";
        }

        /// <summary>
        /// Yeni window kaydeder
        /// </summary>
        public void RegisterWindow(string name, Window window)
        {
            if (!_activeWindows.ContainsKey(name))
            {
                _activeWindows[name] = window;

                // Window events
                window.Closed += (s, e) => _activeWindows.Remove(name);
            }
        }

        /// <summary>
        /// Window'u gösterir
        /// </summary>
        public void ShowWindow(string name)
        {
            if (_activeWindows.TryGetValue(name, out var window))
            {
                window.Show();
                window.Activate();
            }
        }

        /// <summary>
        /// Window'u gizler
        /// </summary>
        public void HideWindow(string name)
        {
            if (_activeWindows.TryGetValue(name, out var window))
            {
                window.Hide();
            }
        }

        /// <summary>
        /// Window'u kapatır
        /// </summary>
        public void CloseWindow(string name)
        {
            if (_activeWindows.TryGetValue(name, out var window))
            {
                window.Close();
            }
        }

        /// <summary>
        /// Bildirim gösterir
        /// </summary>
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information)
        {
            if (!NotificationsEnabled) return;

            var notification = new NotificationEventArgs(title, message, type);
            OnNotificationRequested(notification);
        }

        /// <summary>
        /// Toast notification gösterir
        /// </summary>
        public async Task ShowToastAsync(string message, int durationMs = 3000)
        {
            ShowNotification("YafesV2", message, NotificationType.Information);
            await Task.Delay(durationMs);
        }

        /// <summary>
        /// Error dialog gösterir
        /// </summary>
        public MessageBoxResult ShowError(string message, string title = "Hata")
        {
            return MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Warning dialog gösterir
        /// </summary>
        public MessageBoxResult ShowWarning(string message, string title = "Uyarı")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Information dialog gösterir
        /// </summary>
        public MessageBoxResult ShowInformation(string message, string title = "Bilgi")
        {
            return MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Confirmation dialog gösterir
        /// </summary>
        public MessageBoxResult ShowConfirmation(string message, string title = "Onay")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        /// <summary>
        /// Progress gösterir
        /// </summary>
        public void ShowProgress(string message = "İşlem devam ediyor...")
        {
            StatusMessage = message;
            IsProgressVisible = true;
            ProgressValue = 0;
        }

        /// <summary>
        /// Progress günceller
        /// </summary>
        public void UpdateProgress(int value, string message = null)
        {
            ProgressValue = value;
            if (!string.IsNullOrEmpty(message))
            {
                StatusMessage = message;
            }
        }

        /// <summary>
        /// Progress gizler
        /// </summary>
        public void HideProgress()
        {
            IsProgressVisible = false;
            ProgressValue = 0;
            StatusMessage = "Hazır";
        }

        /// <summary>
        /// Busy durumunu ayarlar
        /// </summary>
        public void SetBusy(bool busy, string message = null)
        {
            IsBusy = busy;
            if (!string.IsNullOrEmpty(message))
            {
                StatusMessage = message;
            }
            else if (!busy)
            {
                StatusMessage = "Hazır";
            }
        }

        /// <summary>
        /// Ana pencereyi merkeze alır
        /// </summary>
        public void CenterMainWindow()
        {
            if (MainWindow != null)
            {
                MainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>
        /// Pencere boyutunu ayarlar
        /// </summary>
        public void SetWindowSize(int width, int height)
        {
            if (MainWindow != null)
            {
                MainWindow.Width = width;
                MainWindow.Height = height;
            }
        }

        /// <summary>
        /// Tüm window'ları kapatır
        /// </summary>
        public void CloseAllWindows()
        {
            var windows = new List<Window>(_activeWindows.Values);
            foreach (var window in windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    // Ignore individual close errors
                }
            }
            _activeWindows.Clear();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Mevcut temaları başlatır
        /// </summary>
        private void InitializeThemes()
        {
            AvailableThemes = new List<string>
            {
                "Light",
                "Dark",
                "Blue",
                "Green"
            };
        }

        /// <summary>
        /// Tema uygular
        /// </summary>
        private void ApplyTheme(string themeName)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var resources = Application.Current.Resources;

                    switch (themeName.ToLower())
                    {
                        case "dark":
                            ApplyDarkTheme(resources);
                            break;
                        case "light":
                            ApplyLightTheme(resources);
                            break;
                        case "blue":
                            ApplyBlueTheme(resources);
                            break;
                        case "green":
                            ApplyGreenTheme(resources);
                            break;
                        default:
                            ApplyDarkTheme(resources);
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme application error: {ex.Message}");
            }
        }

        /// <summary>
        /// Dark tema uygular
        /// </summary>
        private void ApplyDarkTheme(ResourceDictionary resources)
        {
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(33, 37, 41));
            resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(52, 58, 64));
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 123, 255));
            resources["TextBrush"] = new SolidColorBrush(Colors.White);
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(73, 80, 87));
        }

        /// <summary>
        /// Light tema uygular
        /// </summary>
        private void ApplyLightTheme(ResourceDictionary resources)
        {
            resources["PrimaryBrush"] = new SolidColorBrush(Colors.White);
            resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(248, 249, 250));
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 123, 255));
            resources["TextBrush"] = new SolidColorBrush(Colors.Black);
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(206, 212, 218));
        }

        /// <summary>
        /// Blue tema uygular
        /// </summary>
        private void ApplyBlueTheme(ResourceDictionary resources)
        {
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(13, 110, 253));
            resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(33, 136, 255));
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            resources["TextBrush"] = new SolidColorBrush(Colors.White);
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(108, 117, 125));
        }

        /// <summary>
        /// Green tema uygular
        /// </summary>
        private void ApplyGreenTheme(ResourceDictionary resources)
        {
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(25, 135, 84));
            resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(32, 201, 151));
            resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            resources["TextBrush"] = new SolidColorBrush(Colors.White);
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(108, 117, 125));
        }

        /// <summary>
        /// Mouse cursor'ı günceller
        /// </summary>
        private void UpdateCursor()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainWindow != null)
                {
                    MainWindow.Cursor = IsBusy ? System.Windows.Input.Cursors.Wait : null;
                }
            });
        }

        /// <summary>
        /// Ana pencere durum değişikliği
        /// </summary>
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                OnWindowStateChanged(window.WindowState);
            }
        }

        /// <summary>
        /// Ana pencere kapanma eventi
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Kapanmadan önce diğer window'ları kapat
            CloseAllWindows();
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnThemeChanged(string oldTheme, string newTheme)
        {
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, newTheme));
        }

        protected virtual void OnNotificationRequested(NotificationEventArgs args)
        {
            NotificationRequested?.Invoke(this, args);
        }

        protected virtual void OnWindowStateChanged(WindowState newState)
        {
            WindowStateChanged?.Invoke(this, new WindowStateEventArgs(newState));
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// Bildirim türleri
    /// </summary>
    public enum NotificationType
    {
        Information,
        Warning,
        Error,
        Success
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Tema değişikliği event args
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public string OldTheme { get; }
        public string NewTheme { get; }

        public ThemeChangedEventArgs(string oldTheme, string newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }

    /// <summary>
    /// Bildirim event args
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        public string Title { get; }
        public string Message { get; }
        public NotificationType Type { get; }
        public DateTime Timestamp { get; }

        public NotificationEventArgs(string title, string message, NotificationType type)
        {
            Title = title;
            Message = message;
            Type = type;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Window durum event args
    /// </summary>
    public class WindowStateEventArgs : EventArgs
    {
        public WindowState WindowState { get; }

        public WindowStateEventArgs(WindowState windowState)
        {
            WindowState = windowState;
        }
    }

    #endregion
}