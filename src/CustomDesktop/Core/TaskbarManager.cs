using System;
using System.Collections.Generic;
using CustomDesktop.Core.Native;

namespace CustomDesktop.Core;

public class TaskbarManager : IDisposable
{
    private bool _isHidden;
    private bool _isDisposed;

    public TaskbarManager()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreTaskbar();
    }

    public void HideTaskbar()
    {
        if (_isHidden) return;

        try
        {
            // Primary monitor taskbar
            IntPtr primaryTray = User32.FindWindow("Shell_TrayWnd", null);
            if (primaryTray != IntPtr.Zero)
            {
                User32.ShowWindow(primaryTray, User32.SW_HIDE);
                User32.SetWindowPos(primaryTray, IntPtr.Zero, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_HIDEWINDOW);
            }

            // Secondary monitor taskbars
            foreach (var hwnd in GetSecondaryTaskbars())
            {
                User32.ShowWindow(hwnd, User32.SW_HIDE);
                User32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_HIDEWINDOW);
            }

            _isHidden = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error hiding taskbar: {ex.Message}");
        }
    }

    public void RestoreTaskbar()
    {
        if (!_isHidden) return;

        try
        {
            IntPtr primaryTray = User32.FindWindow("Shell_TrayWnd", null);
            if (primaryTray != IntPtr.Zero)
            {
                User32.ShowWindow(primaryTray, User32.SW_SHOW);
                User32.SetWindowPos(primaryTray, IntPtr.Zero, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
            }

            foreach (var hwnd in GetSecondaryTaskbars())
            {
                User32.ShowWindow(hwnd, User32.SW_SHOW);
                User32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
            }

            _isHidden = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error restoring taskbar: {ex.Message}");
        }
    }

    private static List<IntPtr> GetSecondaryTaskbars()
    {
        var list = new List<IntPtr>();
        IntPtr current = IntPtr.Zero;

        while (true)
        {
            current = User32.FindWindowEx(IntPtr.Zero, current, "Shell_SecondaryTrayWnd", null);
            if (current == IntPtr.Zero)
            {
                break;
            }
            list.Add(current);
        }

        return list;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            RestoreTaskbar();
        }
        GC.SuppressFinalize(this);
    }
}
