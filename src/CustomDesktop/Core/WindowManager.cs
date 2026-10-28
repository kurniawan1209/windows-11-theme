using System;
using System.Windows;
using CustomDesktop.Core.Models;
using CustomDesktop.Services;
using CustomDesktop.ViewModels;
using CustomDesktop.Views;

namespace CustomDesktop.Core;

public class WindowManager : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly RunningAppsService _runningAppsService;
    private readonly TaskbarManager _taskbarManager;

    private DockViewModel? _dockViewModel;
    private NotchViewModel? _notchViewModel;
    private DockWindow? _dockWindow;
    private NotchWindow? _notchWindow;
    private bool _isDisposed;

    public WindowManager(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _runningAppsService = new RunningAppsService();
        _taskbarManager = new TaskbarManager();

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    public void Initialize()
    {
        ApplySettings(_settingsService.CurrentSettings);
    }

    public void SetDockEnabled(bool enabled)
    {
        _settingsService.UpdateSettings(s => s.Dock.Enabled = enabled);
    }

    public void SetNotchEnabled(bool enabled)
    {
        _settingsService.UpdateSettings(s => s.Notch.Enabled = enabled);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Application.Current?.Dispatcher.InvokeAsync(() => ApplySettings(settings));
    }

    private void ApplySettings(AppSettings settings)
    {
        if (_isDisposed) return;

        // Manage Windows Taskbar Hiding
        if (settings.Dock.Enabled && settings.Dock.HideWindowsTaskbar)
        {
            _taskbarManager.HideTaskbar();
        }
        else
        {
            _taskbarManager.RestoreTaskbar();
        }

        // Manage Dock Window
        if (settings.Dock.Enabled)
        {
            if (_dockWindow == null)
            {
                _dockViewModel ??= new DockViewModel(_settingsService, _runningAppsService);
                _dockWindow = new DockWindow(_dockViewModel);
                _dockWindow.Closed += (_, _) => _dockWindow = null;
                _dockWindow.Show();
            }
        }
        else
        {
            if (_dockWindow != null)
            {
                var win = _dockWindow;
                _dockWindow = null;
                win.Close();
            }
        }

        // Manage Notch Window
        if (settings.Notch.Enabled)
        {
            if (_notchWindow == null)
            {
                _notchViewModel ??= new NotchViewModel(_settingsService);
                _notchWindow = new NotchWindow(_notchViewModel);
                _notchWindow.Closed += (_, _) => _notchWindow = null;
                _notchWindow.Show();
            }
        }
        else
        {
            if (_notchWindow != null)
            {
                var win = _notchWindow;
                _notchWindow = null;
                win.Close();
            }
        }
    }

    public void TeardownAll()
    {
        _taskbarManager.RestoreTaskbar();

        _dockWindow?.Close();
        _dockWindow = null;

        _notchWindow?.Close();
        _notchWindow = null;

        _dockViewModel?.Dispose();
        _notchViewModel?.Dispose();
        _runningAppsService.Dispose();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _settingsService.SettingsChanged -= OnSettingsChanged;
            TeardownAll();
            _taskbarManager.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
