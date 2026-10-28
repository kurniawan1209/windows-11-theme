using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using CustomDesktop.Core.Models;
using CustomDesktop.Services;

namespace CustomDesktop.ViewModels;

public class ControlPanelViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    public ControlPanelViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;

        OpenSettingsFolderCommand = new RelayCommand(OpenSettingsFolder);
        ExitAppCommand = new RelayCommand(ExitApplication);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        OnPropertyChanged(nameof(DockEnabled));
        OnPropertyChanged(nameof(DockStatusText));
        OnPropertyChanged(nameof(DockAutoHide));
        OnPropertyChanged(nameof(DockShowRunningIndicators));
        OnPropertyChanged(nameof(DockHideWindowsTaskbar));
        OnPropertyChanged(nameof(DockIconSize));
        OnPropertyChanged(nameof(DockItemGap));
        OnPropertyChanged(nameof(DockMarginFromScreen));
        OnPropertyChanged(nameof(DockOpacity));
        OnPropertyChanged(nameof(DockOpacityPercent));
        OnPropertyChanged(nameof(DockCornerRadius));
        OnPropertyChanged(nameof(DockBackdropType));

        OnPropertyChanged(nameof(NotchEnabled));
        OnPropertyChanged(nameof(NotchStatusText));
        OnPropertyChanged(nameof(NotchShowClock));
        OnPropertyChanged(nameof(NotchShowBattery));
        OnPropertyChanged(nameof(NotchShowSound));
        OnPropertyChanged(nameof(NotchShowNetwork));
        OnPropertyChanged(nameof(NotchShowBluetooth));
        OnPropertyChanged(nameof(NotchShowMedia));
        OnPropertyChanged(nameof(NotchWidthCompact));
        OnPropertyChanged(nameof(NotchHeightCompact));
        OnPropertyChanged(nameof(NotchWidthExpanded));
        OnPropertyChanged(nameof(NotchHeightExpanded));
        OnPropertyChanged(nameof(NotchExpandOnHover));
        OnPropertyChanged(nameof(NotchMarginFromScreen));
        OnPropertyChanged(nameof(NotchOpacity));
        OnPropertyChanged(nameof(NotchOpacityPercent));
        OnPropertyChanged(nameof(NotchCornerRadius));
        OnPropertyChanged(nameof(NotchBackdropType));

        OnPropertyChanged(nameof(LaunchOnStartup));
        OnPropertyChanged(nameof(CloseToTray));
        OnPropertyChanged(nameof(EnableAnimations));
    }

    // --- Dock Properties ---
    public bool DockEnabled
    {
        get => _settingsService.CurrentSettings.Dock.Enabled;
        set
        {
            if (_settingsService.CurrentSettings.Dock.Enabled != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.Enabled = value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DockStatusText));
            }
        }
    }

    public string DockStatusText => DockEnabled ? "ACTIVE" : "DISABLED";

    public bool DockAutoHide
    {
        get => _settingsService.CurrentSettings.Dock.AutoHide;
        set
        {
            if (_settingsService.CurrentSettings.Dock.AutoHide != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.AutoHide = value);
                OnPropertyChanged();
            }
        }
    }

    public bool DockShowRunningIndicators
    {
        get => _settingsService.CurrentSettings.Dock.ShowRunningIndicators;
        set
        {
            if (_settingsService.CurrentSettings.Dock.ShowRunningIndicators != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.ShowRunningIndicators = value);
                OnPropertyChanged();
            }
        }
    }

    public bool DockHideWindowsTaskbar
    {
        get => _settingsService.CurrentSettings.Dock.HideWindowsTaskbar;
        set
        {
            if (_settingsService.CurrentSettings.Dock.HideWindowsTaskbar != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.HideWindowsTaskbar = value);
                OnPropertyChanged();
            }
        }
    }

    public int DockIconSize
    {
        get => _settingsService.CurrentSettings.Dock.IconSize;
        set
        {
            if (_settingsService.CurrentSettings.Dock.IconSize != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.IconSize = value);
                OnPropertyChanged();
            }
        }
    }

    public int DockItemGap
    {
        get => _settingsService.CurrentSettings.Dock.ItemGap;
        set
        {
            if (_settingsService.CurrentSettings.Dock.ItemGap != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.ItemGap = value);
                OnPropertyChanged();
            }
        }
    }

    public int DockMarginFromScreen
    {
        get => _settingsService.CurrentSettings.Dock.MarginFromScreen;
        set
        {
            if (_settingsService.CurrentSettings.Dock.MarginFromScreen != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.MarginFromScreen = value);
                OnPropertyChanged();
            }
        }
    }

    public double DockOpacity
    {
        get => _settingsService.CurrentSettings.Dock.Opacity;
        set
        {
            if (Math.Abs(_settingsService.CurrentSettings.Dock.Opacity - value) > 0.01)
            {
                _settingsService.UpdateSettings(s => s.Dock.Opacity = Math.Round(value, 2));
                OnPropertyChanged();
                OnPropertyChanged(nameof(DockOpacityPercent));
            }
        }
    }

    public int DockOpacityPercent
    {
        get => (int)Math.Round(DockOpacity * 100);
        set => DockOpacity = value / 100.0;
    }

    public double DockCornerRadius
    {
        get => _settingsService.CurrentSettings.Dock.CornerRadius;
        set
        {
            if (Math.Abs(_settingsService.CurrentSettings.Dock.CornerRadius - value) > 0.5)
            {
                _settingsService.UpdateSettings(s => s.Dock.CornerRadius = Math.Round(value, 0));
                OnPropertyChanged();
            }
        }
    }

    public string DockBackdropType
    {
        get => _settingsService.CurrentSettings.Dock.BackdropType;
        set
        {
            if (_settingsService.CurrentSettings.Dock.BackdropType != value)
            {
                _settingsService.UpdateSettings(s => s.Dock.BackdropType = value);
                OnPropertyChanged();
            }
        }
    }

    // --- Notch Properties ---
    public bool NotchEnabled
    {
        get => _settingsService.CurrentSettings.Notch.Enabled;
        set
        {
            if (_settingsService.CurrentSettings.Notch.Enabled != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.Enabled = value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotchStatusText));
            }
        }
    }

    public string NotchStatusText => NotchEnabled ? "ACTIVE" : "DISABLED";

    public int NotchWidthCompact
    {
        get => _settingsService.CurrentSettings.Notch.WidthCompact;
        set
        {
            if (_settingsService.CurrentSettings.Notch.WidthCompact != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.WidthCompact = value);
                OnPropertyChanged();
            }
        }
    }

    public int NotchHeightCompact
    {
        get => _settingsService.CurrentSettings.Notch.HeightCompact;
        set
        {
            if (_settingsService.CurrentSettings.Notch.HeightCompact != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.HeightCompact = value);
                OnPropertyChanged();
            }
        }
    }

    public int NotchWidthExpanded
    {
        get => _settingsService.CurrentSettings.Notch.WidthExpanded;
        set
        {
            if (_settingsService.CurrentSettings.Notch.WidthExpanded != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.WidthExpanded = value);
                OnPropertyChanged();
            }
        }
    }

    public int NotchHeightExpanded
    {
        get => _settingsService.CurrentSettings.Notch.HeightExpanded;
        set
        {
            if (_settingsService.CurrentSettings.Notch.HeightExpanded != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.HeightExpanded = value);
                OnPropertyChanged();
            }
        }
    }

    public bool NotchExpandOnHover
    {
        get => _settingsService.CurrentSettings.Notch.ExpandOnHover;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ExpandOnHover != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ExpandOnHover = value);
                OnPropertyChanged();
            }
        }
    }

    public int NotchMarginFromScreen
    {
        get => _settingsService.CurrentSettings.Notch.MarginFromScreen;
        set
        {
            if (_settingsService.CurrentSettings.Notch.MarginFromScreen != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.MarginFromScreen = value);
                OnPropertyChanged();
            }
        }
    }

    public double NotchOpacity
    {
        get => _settingsService.CurrentSettings.Notch.Opacity;
        set
        {
            if (Math.Abs(_settingsService.CurrentSettings.Notch.Opacity - value) > 0.01)
            {
                _settingsService.UpdateSettings(s => s.Notch.Opacity = Math.Round(value, 2));
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotchOpacityPercent));
            }
        }
    }

    public int NotchOpacityPercent
    {
        get => (int)Math.Round(NotchOpacity * 100);
        set => NotchOpacity = value / 100.0;
    }

    public double NotchCornerRadius
    {
        get => _settingsService.CurrentSettings.Notch.CornerRadius;
        set
        {
            if (Math.Abs(_settingsService.CurrentSettings.Notch.CornerRadius - value) > 0.5)
            {
                _settingsService.UpdateSettings(s => s.Notch.CornerRadius = Math.Round(value, 0));
                OnPropertyChanged();
            }
        }
    }

    public bool NotchShowClock
    {
        get => _settingsService.CurrentSettings.Notch.ShowClock;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ShowClock != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ShowClock = value);
                OnPropertyChanged();
            }
        }
    }

    public bool NotchShowBattery
    {
        get => _settingsService.CurrentSettings.Notch.ShowBattery;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ShowBattery != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ShowBattery = value);
                OnPropertyChanged();
            }
        }
    }

    public bool NotchShowSound
    {
        get => _settingsService.CurrentSettings.Notch.ShowSound;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ShowSound != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ShowSound = value);
                OnPropertyChanged();
            }
        }
    }

    public bool NotchShowNetwork
    {
        get => _settingsService.CurrentSettings.Notch.ShowNetwork;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ShowNetwork != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ShowNetwork = value);
                OnPropertyChanged();
            }
        }
    }

    public bool NotchShowBluetooth
    {
        get => _settingsService.CurrentSettings.Notch.ShowBluetooth;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ShowBluetooth != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ShowBluetooth = value);
                OnPropertyChanged();
            }
        }
    }

    public bool NotchShowMedia
    {
        get => _settingsService.CurrentSettings.Notch.ShowMedia;
        set
        {
            if (_settingsService.CurrentSettings.Notch.ShowMedia != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.ShowMedia = value);
                OnPropertyChanged();
            }
        }
    }

    public string NotchBackdropType
    {
        get => _settingsService.CurrentSettings.Notch.BackdropType;
        set
        {
            if (_settingsService.CurrentSettings.Notch.BackdropType != value)
            {
                _settingsService.UpdateSettings(s => s.Notch.BackdropType = value);
                OnPropertyChanged();
            }
        }
    }

    // --- System Properties ---
    public bool LaunchOnStartup
    {
        get => _settingsService.CurrentSettings.System.LaunchOnStartup;
        set
        {
            if (_settingsService.CurrentSettings.System.LaunchOnStartup != value)
            {
                _settingsService.UpdateSettings(s => s.System.LaunchOnStartup = value);
                OnPropertyChanged();
            }
        }
    }

    public bool CloseToTray
    {
        get => _settingsService.CurrentSettings.System.CloseToTray;
        set
        {
            if (_settingsService.CurrentSettings.System.CloseToTray != value)
            {
                _settingsService.UpdateSettings(s => s.System.CloseToTray = value);
                OnPropertyChanged();
            }
        }
    }

    public bool EnableAnimations
    {
        get => _settingsService.CurrentSettings.System.EnableAnimations;
        set
        {
            if (_settingsService.CurrentSettings.System.EnableAnimations != value)
            {
                _settingsService.UpdateSettings(s => s.System.EnableAnimations = value);
                OnPropertyChanged();
            }
        }
    }

    // --- Commands ---
    public ICommand OpenSettingsFolderCommand { get; }
    public ICommand ExitAppCommand { get; }

    private void OpenSettingsFolder()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "CustomDesktop");
            Directory.CreateDirectory(appFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = appFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open folder: {ex.Message}");
        }
    }

    private void ExitApplication()
    {
        System.Windows.Application.Current.Shutdown();
    }
}
