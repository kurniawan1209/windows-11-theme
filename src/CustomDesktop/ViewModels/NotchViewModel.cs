using System;
using System.Windows;
using CustomDesktop.Core.Models;
using CustomDesktop.Services;

namespace CustomDesktop.ViewModels;

public class NotchViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private bool _isDisposed;

    public int WidthCompact => _settingsService.CurrentSettings.Notch.WidthCompact;
    public int HeightCompact => _settingsService.CurrentSettings.Notch.HeightCompact;
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

    public NotchViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        OnPropertyChanged(nameof(WidthCompact));
        OnPropertyChanged(nameof(HeightCompact));
        OnPropertyChanged(nameof(MarginFromScreen));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(CornerRadiusValue));
        OnPropertyChanged(nameof(ShowClock));
        OnPropertyChanged(nameof(ShowBattery));
        OnPropertyChanged(nameof(ShowSound));
        OnPropertyChanged(nameof(ShowNetwork));
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _settingsService.SettingsChanged -= OnSettingsChanged;
        }
        GC.SuppressFinalize(this);
    }
}
