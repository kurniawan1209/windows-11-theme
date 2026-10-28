using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using CustomDesktop.Core.Native;
using CustomDesktop.ViewModels;

namespace CustomDesktop.Views;

public partial class ControlPanelWindow : Window
{
    private readonly ControlPanelViewModel _viewModel;
    private bool _isExplicitExit;

    public ControlPanelWindow(ControlPanelViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        LoadAppIcon();

        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void LoadAppIcon()
    {
        try
        {
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                Icon = bitmap;
            }
        }
        catch
        {
            // Fallback silently if icon cannot be loaded to avoid any crash
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            DwmApi.EnableImmersiveDarkMode(helper.Handle, true);
            DwmApi.SetWindowCorners(helper.Handle, DwmApi.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnMinimizeToTrayClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    public void ShutdownWindow()
    {
        _isExplicitExit = true;
        Close();
    }
}
