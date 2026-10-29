using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CustomDesktop.Core.Models;
using CustomDesktop.Services;

namespace CustomDesktop.ViewModels;

public enum NotchTab
{
    Media,
    Wifi,
    Bluetooth
}

public class NotchViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly SoundService _soundService;
    private readonly MediaService _mediaService;
    private readonly WifiService _wifiService;
    private readonly BluetoothService _bluetoothService;
    private readonly BatteryService _batteryService;
    private readonly DispatcherTimer _clockTimer;
    private int _clockTickCount;

    private static readonly Brush _chargingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
    private static readonly Brush _criticalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
    private static readonly Brush _warningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
    private static readonly Brush _normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4E7"));

    private bool _isDisposed;
    private bool _isExpanded;
    private NotchTab _activeTab = NotchTab.Media;

    private string _timeString = string.Empty;
    private string _dateStringCompact = string.Empty;
    private string _dateStringFull = string.Empty;

    // View interaction hooks
    public event Action? RequestExpand;
    public event Action? RequestCollapse;

    // Notch Dimensions & Geometry
    public int WidthCompact => _settingsService.CurrentSettings.Notch.WidthCompact;
    public int HeightCompact => _settingsService.CurrentSettings.Notch.HeightCompact;
    public int WidthExpanded => _settingsService.CurrentSettings.Notch.WidthExpanded;
    public int HeightExpanded => _settingsService.CurrentSettings.Notch.HeightExpanded;
    public int MarginFromScreen => _settingsService.CurrentSettings.Notch.MarginFromScreen;
    public double Opacity => _settingsService.CurrentSettings.Notch.Opacity;
    public double CornerRadius => _settingsService.CurrentSettings.Notch.CornerRadius;

    public CornerRadius CornerRadiusValue => MarginFromScreen > 0
        ? new CornerRadius(CornerRadius)
        : new CornerRadius(0, 0, CornerRadius, CornerRadius);

    public bool ShowClock => _settingsService.CurrentSettings.Notch.ShowClock;
    public bool ShowBattery => _settingsService.CurrentSettings.Notch.ShowBattery;
    public bool ShowSound => _settingsService.CurrentSettings.Notch.ShowSound;
    public bool ShowNetwork => _settingsService.CurrentSettings.Notch.ShowNetwork;
    public bool ShowBluetooth => _settingsService.CurrentSettings.Notch.ShowBluetooth;
    public bool ShowMedia => _settingsService.CurrentSettings.Notch.ShowMedia;
    public bool ExpandOnHover => _settingsService.CurrentSettings.Notch.ExpandOnHover;
    public bool EnableAnimations => _settingsService.CurrentSettings.System.EnableAnimations;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                if (value)
                {
                    OnOpened();
                }
            }
        }
    }

    public NotchTab ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(IsMediaTabActive));
                OnPropertyChanged(nameof(IsWifiTabActive));
                OnPropertyChanged(nameof(IsBluetoothTabActive));
                LoadActiveTabData();
            }
        }
    }

    public bool IsMediaTabActive => ActiveTab == NotchTab.Media;
    public bool IsWifiTabActive => ActiveTab == NotchTab.Wifi;
    public bool IsBluetoothTabActive => ActiveTab == NotchTab.Bluetooth;

    // Clock
    public string TimeString
    {
        get => _timeString;
        private set => SetProperty(ref _timeString, value);
    }

    public string DateStringCompact
    {
        get => _dateStringCompact;
        private set => SetProperty(ref _dateStringCompact, value);
    }

    public string DateStringFull
    {
        get => _dateStringFull;
        private set => SetProperty(ref _dateStringFull, value);
    }

    // Audio & Volume
    public int MasterVolumePercent
    {
        get => (int)Math.Round(_soundService.MasterVolume * 100);
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            _soundService.MasterVolume = clamped / 100f;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeIcon));
        }
    }

    public bool IsMuted
    {
        get => _soundService.IsMuted;
        set
        {
            _soundService.IsMuted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeIcon));
        }
    }

    public string VolumeIcon
    {
        get
        {
            if (IsMuted || MasterVolumePercent == 0) return "🔇";
            if (MasterVolumePercent < 40) return "🔉";
            return "🔊";
        }
    }

    // Media & Spotify
    public string MediaTitle => _mediaService.Title;
    public string MediaArtist => _mediaService.Artist;
    public ImageSource? MediaThumbnail => _mediaService.Thumbnail;
    public bool IsPlaying => _mediaService.IsPlaying;
    public bool IsSpotifyRunning => _mediaService.IsSpotifyRunning;
    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

    // Wi-Fi
    public ObservableCollection<WifiNetworkItem> AvailableWifiNetworks { get; } = new();
    private bool _isWifiRefreshing;
    public bool IsWifiRefreshing
    {
        get => _isWifiRefreshing;
        private set => SetProperty(ref _isWifiRefreshing, value);
    }

    // Bluetooth
    public ObservableCollection<BluetoothDeviceItem> BluetoothDevices { get; } = new();
    private bool _isBluetoothRefreshing;
    public bool IsBluetoothRefreshing
    {
        get => _isBluetoothRefreshing;
        private set => SetProperty(ref _isBluetoothRefreshing, value);
    }

    // Battery
    public bool HasBattery => _batteryService.HasBattery;
    public int BatteryPercent => _batteryService.BatteryPercent;
    public bool IsBatteryCharging => _batteryService.IsCharging;
    public bool IsBatteryVisible => ShowBattery && HasBattery;
    public string BatteryDisplayText => $"{BatteryPercent}%";

    public Brush BatteryStatusBrush
    {
        get
        {
            if (IsBatteryCharging) return _chargingBrush;
            if (BatteryPercent <= 15) return _criticalBrush;
            if (BatteryPercent <= 25) return _warningBrush;
            return _normalBrush;
        }
    }

    // Commands
    public ICommand ToggleExpandCommand { get; }
    public ICommand CollapseCommand { get; }
    public ICommand SelectMediaTabCommand { get; }
    public ICommand SelectWifiTabCommand { get; }
    public ICommand SelectBluetoothTabCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand LaunchSpotifyCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand NextTrackCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand OpenNetworkFlyoutCommand { get; }
    public ICommand OpenBluetoothSettingsCommand { get; }
    public ICommand RefreshWifiCommand { get; }
    public ICommand RefreshBluetoothCommand { get; }
    public ICommand DisconnectWifiCommand { get; }
    public ICommand ConnectWifiCommand { get; }
    public ICommand PromptWifiPasswordCommand { get; }
    public ICommand CancelWifiPasswordCommand { get; }
    public ICommand SubmitWifiPasswordCommand { get; }
    public ICommand ConnectBluetoothCommand { get; }
    public ICommand DisconnectBluetoothCommand { get; }

    public NotchViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;

        _soundService = new SoundService();
        _soundService.VolumeChanged += OnSoundVolumeChanged;

        _mediaService = new MediaService();
        _mediaService.MediaPropertiesChanged += OnMediaChanged;
        _mediaService.PlaybackStatusChanged += OnPlaybackChanged;

        _wifiService = new WifiService();
        _bluetoothService = new BluetoothService();
        _batteryService = new BatteryService();
        _batteryService.BatteryStateChanged += OnBatteryStateChanged;

        ToggleExpandCommand = new RelayCommand(ToggleExpand);
        CollapseCommand = new RelayCommand(Collapse);

        SelectMediaTabCommand = new RelayCommand(() => ActiveTab = NotchTab.Media);
        SelectWifiTabCommand = new RelayCommand(() => ActiveTab = NotchTab.Wifi);
        SelectBluetoothTabCommand = new RelayCommand(() => ActiveTab = NotchTab.Bluetooth);

        ToggleMuteCommand = new RelayCommand(() =>
        {
            _soundService.ToggleMute();
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(VolumeIcon));
        });

        LaunchSpotifyCommand = new RelayCommand(() =>
        {
            _mediaService.LaunchSpotify();
            Task.Delay(1000).ContinueWith(_ =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    OnPropertyChanged(nameof(IsSpotifyRunning));
                });
            });
        });

        PlayPauseCommand = new RelayCommand(() => _ = _mediaService.TogglePlayPauseAsync());
        NextTrackCommand = new RelayCommand(() => _ = _mediaService.NextAsync());
        PreviousTrackCommand = new RelayCommand(() => _ = _mediaService.PreviousAsync());

        OpenNetworkFlyoutCommand = new RelayCommand(() => _wifiService.OpenNetworkFlyout());
        OpenBluetoothSettingsCommand = new RelayCommand(() => _bluetoothService.OpenBluetoothSettings());

        RefreshWifiCommand = new RelayCommand(RefreshWifiNetworks);
        RefreshBluetoothCommand = new RelayCommand(() => _ = RefreshBluetoothDevicesAsync());

        DisconnectWifiCommand = new RelayCommand<WifiNetworkItem>(async item =>
        {
            if (item != null) item.IsConnecting = true;
            await _wifiService.DisconnectAsync();
            if (item != null) item.IsConnecting = false;
            RefreshWifiNetworks();
        });

        ConnectWifiCommand = new RelayCommand<WifiNetworkItem>(async item =>
        {
            if (item == null) return;
            if (item.NeedsPassword)
            {
                PromptWifiPassword(item);
                return;
            }

            item.IsConnecting = true;
            item.ErrorMessage = null;
            var res = await _wifiService.ConnectAsync(item.Ssid);
            item.IsConnecting = false;
            if (!res.Success)
            {
                item.ErrorMessage = res.Message;
            }
            RefreshWifiNetworks();
        });

        PromptWifiPasswordCommand = new RelayCommand<WifiNetworkItem>(item =>
        {
            if (item == null) return;
            PromptWifiPassword(item);
        });

        CancelWifiPasswordCommand = new RelayCommand<WifiNetworkItem>(item =>
        {
            if (item == null) return;
            item.IsPasswordPromptOpen = false;
            item.ErrorMessage = null;
        });

        SubmitWifiPasswordCommand = new RelayCommand<Tuple<WifiNetworkItem, string>>(async tuple =>
        {
            if (tuple == null) return;
            var item = tuple.Item1;
            var password = tuple.Item2;
            if (item == null) return;

            item.IsConnecting = true;
            item.ErrorMessage = null;

            var res = await _wifiService.ConnectWithPasswordAsync(item.Ssid, password ?? "");
            item.IsConnecting = false;

            if (res.Success)
            {
                item.IsPasswordPromptOpen = false;
                item.ErrorMessage = null;
                RefreshWifiNetworks();
            }
            else
            {
                item.ErrorMessage = res.Message;
            }
        });

        ConnectBluetoothCommand = new RelayCommand<BluetoothDeviceItem>(async item =>
        {
            if (item == null || item.IsBusy) return;
            item.IsBusy = true;
            item.ErrorMessage = null;
            var res = await _bluetoothService.ConnectDeviceAsync(item);
            item.IsBusy = false;
            if (!res.Success)
            {
                item.ErrorMessage = res.Message;
            }
            _ = RefreshBluetoothDevicesAsync();
        });

        DisconnectBluetoothCommand = new RelayCommand<BluetoothDeviceItem>(async item =>
        {
            if (item == null || item.IsBusy) return;
            item.IsBusy = true;
            item.ErrorMessage = null;
            var res = await _bluetoothService.DisconnectDeviceAsync(item);
            item.IsBusy = false;
            if (!res.Success)
            {
                item.ErrorMessage = res.Message;
            }
            _ = RefreshBluetoothDevicesAsync();
        });

        UpdateTime();
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) =>
        {
            UpdateTime();
            _clockTickCount++;
            if (_clockTickCount % 5 == 0)
            {
                _batteryService.Refresh();
            }
        };
        _clockTimer.Start();
    }

    private void OnBatteryStateChanged()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(HasBattery));
            OnPropertyChanged(nameof(BatteryPercent));
            OnPropertyChanged(nameof(IsBatteryCharging));
            OnPropertyChanged(nameof(IsBatteryVisible));
            OnPropertyChanged(nameof(BatteryDisplayText));
            OnPropertyChanged(nameof(BatteryStatusBrush));
        });
    }

    private void OnSoundVolumeChanged(float newVolume, bool isMuted)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(MasterVolumePercent));
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(VolumeIcon));
        });
    }

    private void OnMediaChanged()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(MediaTitle));
            OnPropertyChanged(nameof(MediaArtist));
            OnPropertyChanged(nameof(MediaThumbnail));
            OnPropertyChanged(nameof(IsSpotifyRunning));
        });
    }

    private void OnPlaybackChanged()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(PlayPauseIcon));
        });
    }

    private void OnOpened()
    {
        OnPropertyChanged(nameof(IsSpotifyRunning));
        Task.Delay(260).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (IsExpanded)
                {
                    _ = _mediaService.RefreshMediaPropertiesAsync();
                    LoadActiveTabData();
                }
            });
        });
    }

    private void LoadActiveTabData()
    {
        switch (ActiveTab)
        {
            case NotchTab.Media:
                OnPropertyChanged(nameof(IsSpotifyRunning));
                _ = _mediaService.RefreshMediaPropertiesAsync();
                break;
            case NotchTab.Wifi:
                RefreshWifiNetworks();
                break;
            case NotchTab.Bluetooth:
                _ = RefreshBluetoothDevicesAsync();
                break;
        }
    }

    public void PromptWifiPassword(WifiNetworkItem target)
    {
        foreach (var net in AvailableWifiNetworks)
        {
            if (net != target && net.IsPasswordPromptOpen)
            {
                net.IsPasswordPromptOpen = false;
                net.ErrorMessage = null;
            }
        }
        target.ErrorMessage = null;
        target.IsPasswordPromptOpen = true;
    }

    public void RefreshWifiNetworks()
    {
        if (IsWifiRefreshing) return;
        IsWifiRefreshing = true;

        Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            var networks = _wifiService.ScanAvailableNetworks();
            long elapsed = sw.ElapsedMilliseconds;
            if (elapsed < 650)
            {
                await Task.Delay((int)(650 - elapsed));
            }

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                AvailableWifiNetworks.Clear();
                foreach (var net in networks)
                {
                    AvailableWifiNetworks.Add(net);
                }
                IsWifiRefreshing = false;
            });
        });
    }

    public async Task RefreshBluetoothDevicesAsync()
    {
        if (IsBluetoothRefreshing) return;
        IsBluetoothRefreshing = true;

        var sw = Stopwatch.StartNew();
        var devices = await _bluetoothService.GetDevicesAsync();
        long elapsed = sw.ElapsedMilliseconds;
        if (elapsed < 650)
        {
            await Task.Delay((int)(650 - elapsed));
        }

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            BluetoothDevices.Clear();
            foreach (var dev in devices)
            {
                BluetoothDevices.Add(dev);
            }
            IsBluetoothRefreshing = false;
        });
    }

    public void ToggleExpand()
    {
        if (IsExpanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    public void Expand()
    {
        if (!IsExpanded)
        {
            IsExpanded = true;
            RequestExpand?.Invoke();
        }
    }

    public void Collapse()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            RequestCollapse?.Invoke();
        }
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        TimeString = now.ToString("h:mm tt");
        DateStringCompact = now.ToString("ddd, MMM d");
        DateStringFull = now.ToString("dddd, MMMM d, yyyy");
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        OnPropertyChanged(nameof(WidthCompact));
        OnPropertyChanged(nameof(HeightCompact));
        OnPropertyChanged(nameof(WidthExpanded));
        OnPropertyChanged(nameof(HeightExpanded));
        OnPropertyChanged(nameof(MarginFromScreen));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(CornerRadiusValue));
        OnPropertyChanged(nameof(ShowClock));
        OnPropertyChanged(nameof(ShowBattery));
        OnPropertyChanged(nameof(IsBatteryVisible));
        OnPropertyChanged(nameof(ShowSound));
        OnPropertyChanged(nameof(ShowNetwork));
        OnPropertyChanged(nameof(ShowBluetooth));
        OnPropertyChanged(nameof(ShowMedia));
        OnPropertyChanged(nameof(ExpandOnHover));
        OnPropertyChanged(nameof(EnableAnimations));
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _clockTimer.Stop();
            _settingsService.SettingsChanged -= OnSettingsChanged;

            _soundService.VolumeChanged -= OnSoundVolumeChanged;
            _soundService.Dispose();

            _mediaService.MediaPropertiesChanged -= OnMediaChanged;
            _mediaService.PlaybackStatusChanged -= OnPlaybackChanged;
            _mediaService.Dispose();

            _wifiService.Dispose();
            _bluetoothService.Dispose();
            _batteryService.BatteryStateChanged -= OnBatteryStateChanged;
            _batteryService.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
