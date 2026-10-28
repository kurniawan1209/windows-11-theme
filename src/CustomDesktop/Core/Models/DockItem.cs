using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using CustomDesktop.Core.Native;
using CustomDesktop.Services;
using CustomDesktop.ViewModels;

namespace CustomDesktop.Core.Models;

public class WindowSelectionItem
{
    public IntPtr WindowHandle { get; set; }
    public string Title { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }
}

public class DockItem : ViewModelBase
{
    private string _name = string.Empty;
    private string _executablePath = string.Empty;
    private string _arguments = string.Empty;
    private IntPtr _windowHandle = IntPtr.Zero;
    private List<IntPtr> _windowHandles = new();
    private ImageSource? _icon;
    private bool _isPinned;
    private bool _isRunning;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public IntPtr WindowHandle
    {
        get => _windowHandle;
        set => SetProperty(ref _windowHandle, value);
    }

    public List<IntPtr> WindowHandles
    {
        get => _windowHandles;
        set
        {
            if (SetProperty(ref _windowHandles, value))
            {
                OnPropertyChanged(nameof(HasMultipleWindows));
            }
        }
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(PinToggleText));
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public bool HasMultipleWindows => WindowHandles.Count > 1;
    public string PinToggleText => IsPinned ? "Unpin from dock" : "Pin to dock";

    public ICommand ClickCommand { get; }
    public ICommand CloseWindowCommand { get; }
    public ICommand CloseAllWindowsCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand NewInstanceCommand { get; }
    public ICommand RunAsAdminCommand { get; }
    public ICommand TogglePinCommand { get; }

    public event Action<DockItem>? PinToggled;
    public event Action<DockItem, List<WindowSelectionItem>>? RequestWindowPicker;

    public DockItem()
    {
        ClickCommand = new RelayCommand(ActivateOrLaunch);
        CloseWindowCommand = new RelayCommand(CloseWindow);
        CloseAllWindowsCommand = new RelayCommand(CloseAllWindows);
        MinimizeCommand = new RelayCommand(Minimize);
        RestoreCommand = new RelayCommand(Restore);
        NewInstanceCommand = new RelayCommand(LaunchNewInstance);
        RunAsAdminCommand = new RelayCommand(RunAsAdmin);
        TogglePinCommand = new RelayCommand(() => PinToggled?.Invoke(this));
    }

    public void LaunchNewInstance()
    {
        if (string.IsNullOrEmpty(ExecutablePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = Arguments,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch new instance of {Name}: {ex.Message}");
        }
    }

    public void RunAsAdmin()
    {
        if (string.IsNullOrEmpty(ExecutablePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = Arguments,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch {Name} as admin: {ex.Message}");
        }
    }

    public void CloseWindow()
    {
        IntPtr target = WindowHandle != IntPtr.Zero ? WindowHandle : WindowHandles.FirstOrDefault();
        if (target != IntPtr.Zero)
        {
            User32.PostMessage(target, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void CloseAllWindows()
    {
        foreach (var handle in WindowHandles.ToList())
        {
            if (handle != IntPtr.Zero)
            {
                User32.PostMessage(handle, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }

    public void Minimize()
    {
        IntPtr target = WindowHandle != IntPtr.Zero ? WindowHandle : WindowHandles.FirstOrDefault();
        if (target != IntPtr.Zero)
        {
            User32.ShowWindowAsync(target, User32.SW_MINIMIZE);
        }
    }

    public void Restore()
    {
        IntPtr target = WindowHandle != IntPtr.Zero ? WindowHandle : WindowHandles.FirstOrDefault();
        if (target != IntPtr.Zero)
        {
            User32.ForceSetForegroundWindow(target);
        }
    }

    public void ActivateOrLaunch()
    {
        try
        {
            // Windows native behavior: Shift + Click always spawns a new instance
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                LaunchNewInstance();
                return;
            }

            // If app is not running or has no valid handles, launch it
            if (!IsRunning || (WindowHandle == IntPtr.Zero && WindowHandles.Count == 0))
            {
                LaunchNewInstance();
                return;
            }

            // Ensure WindowHandles contains at least WindowHandle
            if (WindowHandles.Count == 0 && WindowHandle != IntPtr.Zero)
            {
                WindowHandles.Add(WindowHandle);
            }

            // Remove closed or non-existent handles
            WindowHandles.RemoveAll(h => !User32.IsWindowVisible(h) && !User32.IsIconic(h));

            if (WindowHandles.Count == 0)
            {
                IsRunning = false;
                LaunchNewInstance();
                return;
            }

            IntPtr foregroundWnd = User32.GetForegroundWindow();

            // Single Window Scenario: Toggle between Focus and Minimize
            if (WindowHandles.Count == 1)
            {
                IntPtr target = WindowHandles[0];
                if (foregroundWnd == target)
                {
                    // Already focused -> Minimize (Windows taskbar click-to-minimize behavior)
                    User32.ShowWindowAsync(target, User32.SW_MINIMIZE);
                }
                else
                {
                    // Inactive or Minimized -> Restore and bring to Foreground
                    User32.ForceSetForegroundWindow(target);
                }
                return;
            }

            // Multi-Window Scenario: Restore both windows and trigger selection picker
            foreach (var h in WindowHandles)
            {
                if (User32.IsIconic(h))
                {
                    User32.ShowWindowAsync(h, User32.SW_RESTORE);
                }
            }

            var items = GetWindowSelectionItems();
            if (RequestWindowPicker != null && items.Count > 0)
            {
                RequestWindowPicker.Invoke(this, items);
                return;
            }

            // Fallback cycling if no UI picker attached
            int activeIndex = WindowHandles.IndexOf(foregroundWnd);
            if (activeIndex >= 0)
            {
                int nextIndex = (activeIndex + 1) % WindowHandles.Count;
                User32.ForceSetForegroundWindow(WindowHandles[nextIndex]);
            }
            else
            {
                User32.ForceSetForegroundWindow(WindowHandles[0]);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to activate or launch {Name}: {ex.Message}");
        }
    }

    public List<WindowSelectionItem> GetWindowSelectionItems()
    {
        var list = new List<WindowSelectionItem>();
        foreach (var h in WindowHandles)
        {
            if (User32.IsWindowVisible(h) || User32.IsIconic(h))
            {
                var sb = new StringBuilder(256);
                User32.GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) title = Name;

                var icon = IconExtractor.GetIconForWindow(h) ?? Icon;

                list.Add(new WindowSelectionItem
                {
                    WindowHandle = h,
                    Title = title,
                    Icon = icon
                });
            }
        }
        return list;
    }
}
