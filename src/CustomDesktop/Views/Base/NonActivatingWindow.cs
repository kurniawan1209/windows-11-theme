using System;
using System.Windows;
using System.Windows.Interop;
using CustomDesktop.Core.Native;

namespace CustomDesktop.Views.Base;

public class NonActivatingWindow : Window
{
    public IntPtr Handle { get; private set; }

    public NonActivatingWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        Background = System.Windows.Media.Brushes.Transparent;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        Handle = helper.Handle;

        if (Handle != IntPtr.Size && Handle != IntPtr.Zero)
        {
            // Apply WS_EX_NOACTIVATE and WS_EX_TOOLWINDOW
            IntPtr exStyle = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE);
            long newExStyle = exStyle.ToInt64() | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(newExStyle));

            // Attach message hook to intercept WM_MOUSEACTIVATE
            var source = HwndSource.FromHwnd(Handle);
            source?.AddHook(WndProc);

            // Enable Windows 11 immersive dark mode by default
            DwmApi.EnableImmersiveDarkMode(Handle, true);
        }
    }

    protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(User32.MA_NOACTIVATE);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Repositions the window to the bottom-center of the primary monitor using full screen height so it floats in the taskbar region.
    /// </summary>
    public void PositionAtBottomCenter(double offsetFromBottom = 10)
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        Left = Math.Round((screenWidth - Width) / 2.0);
        Top = Math.Round(screenHeight - Height - offsetFromBottom);
    }

    /// <summary>
    /// Repositions the window to the top-center of the primary monitor.
    /// </summary>
    public void PositionAtTopCenter(double offsetFromTop = 0)
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        Left = Math.Round((screenWidth - Width) / 2.0);
        Top = Math.Round(offsetFromTop);
    }
}
