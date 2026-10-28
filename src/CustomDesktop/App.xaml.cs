using System;
using System.Windows;
using CustomDesktop.Core;
using CustomDesktop.Services;
using CustomDesktop.Tray;
using CustomDesktop.ViewModels;
using CustomDesktop.Views;

namespace CustomDesktop;

public partial class App : Application
{
    private ISettingsService? _settingsService;
    private WindowManager? _windowManager;
    private TrayIconManager? _trayIconManager;
    private ControlPanelViewModel? _controlPanelViewModel;
    private ControlPanelWindow? _controlPanelWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Unhandled Exception: {args.ExceptionObject}", "Custom Desktop Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Dispatcher Exception: {args.Exception.Message}", "Custom Desktop Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // 1. Initialize Settings Service
        _settingsService = new SettingsService();

        // 2. Initialize Window Manager
        _windowManager = new WindowManager(_settingsService);

        // 3. Initialize Control Panel UI & ViewModel
        _controlPanelViewModel = new ControlPanelViewModel(_settingsService);
        _controlPanelWindow = new ControlPanelWindow(_controlPanelViewModel);

        // 4. Initialize Tray Icon Manager with Control Panel callback
        _trayIconManager = new TrayIconManager(_settingsService, _windowManager, () =>
        {
            Dispatcher.InvokeAsync(() => _controlPanelWindow.ShowAndActivate());
        });

        // 5. Spin up initial desktop overlays based on saved settings
        _windowManager.Initialize();

        // 6. Show the Control Panel window on startup
        _controlPanelWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controlPanelWindow?.ShutdownWindow();
        _trayIconManager?.Dispose();
        _windowManager?.Dispose();
        base.OnExit(e);
    }
}
