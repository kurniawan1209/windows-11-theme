using System;
using System.Drawing;
using System.Windows.Forms;
using CustomDesktop.Core;
using CustomDesktop.Core.Models;
using CustomDesktop.Services;

namespace CustomDesktop.Tray;

public class TrayIconManager : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly WindowManager _windowManager;
    private readonly Action? _onOpenControlPanel;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _dockMenuItem;
    private readonly ToolStripMenuItem _notchMenuItem;
    private bool _isDisposed;

    public TrayIconManager(ISettingsService settingsService, WindowManager windowManager, Action? onOpenControlPanel = null)
    {
        _settingsService = settingsService;
        _windowManager = windowManager;
        _onOpenControlPanel = onOpenControlPanel;

        var contextMenu = new ContextMenuStrip();

        // 1. Toggle Dock
        _dockMenuItem = new ToolStripMenuItem("Enable Dock")
        {
            CheckOnClick = true,
            Checked = _settingsService.CurrentSettings.Dock.Enabled
        };
        _dockMenuItem.CheckedChanged += OnDockMenuItemCheckedChanged;
        contextMenu.Items.Add(_dockMenuItem);

        // 2. Toggle Notch
        _notchMenuItem = new ToolStripMenuItem("Enable Notch")
        {
            CheckOnClick = true,
            Checked = _settingsService.CurrentSettings.Notch.Enabled
        };
        _notchMenuItem.CheckedChanged += OnNotchMenuItemCheckedChanged;
        contextMenu.Items.Add(_notchMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // 3. Control Panel Menu Item
        var controlPanelItem = new ToolStripMenuItem("Control Panel...");
        controlPanelItem.Click += (_, _) => _onOpenControlPanel?.Invoke();
        contextMenu.Items.Add(controlPanelItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // 4. Exit
        var exitItem = new ToolStripMenuItem("Exit Custom Desktop");
        exitItem.Click += OnExitClicked;
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "Custom Desktop (WinTheme)",
            Icon = CreateAppIcon(),
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => _onOpenControlPanel?.Invoke();

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnDockMenuItemCheckedChanged(object? sender, EventArgs e)
    {
        _windowManager.SetDockEnabled(_dockMenuItem.Checked);
    }

    private void OnNotchMenuItemCheckedChanged(object? sender, EventArgs e)
    {
        _windowManager.SetNotchEnabled(_notchMenuItem.Checked);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        if (_dockMenuItem.Checked != settings.Dock.Enabled)
        {
            _dockMenuItem.Checked = settings.Dock.Enabled;
        }

        if (_notchMenuItem.Checked != settings.Notch.Enabled)
        {
            _notchMenuItem.Checked = settings.Notch.Enabled;
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _windowManager.TeardownAll();
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon CreateAppIcon()
    {
        string localIco = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(localIco))
        {
            try
            {
                return new Icon(localIco, 32, 32);
            }
            catch { }
        }

        try
        {
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using var brush = new SolidBrush(Color.FromArgb(24, 24, 27));
                g.FillEllipse(brush, 2, 2, 28, 28);

                using var pen = new Pen(Color.FromArgb(56, 189, 248), 2.5f);
                g.DrawEllipse(pen, 3, 3, 26, 26);

                using var centerBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
                g.FillEllipse(centerBrush, 12, 12, 8, 8);
            }

            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
