using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using CustomDesktop.Core.Native;

namespace CustomDesktop.Services;

public class RunningAppInfo
{
    public IntPtr WindowHandle { get; set; }
    public List<IntPtr> WindowHandles { get; } = new();
    public uint ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class RunningAppsService : IDisposable
{
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _debounceTimer;
    private readonly uint _currentProcessId;
    private readonly User32.WinEventDelegate _winEventDelegate;
    private readonly ConcurrentDictionary<uint, (string ProcessName, string ExePath)> _processInfoCache = new();
    private IntPtr _hookCreateDestroy;
    private IntPtr _hookForeground;
    private bool _isDisposed;

    public event Action<List<RunningAppInfo>>? RunningAppsChanged;

    public RunningAppsService()
    {
        _currentProcessId = (uint)Process.GetCurrentProcess().Id;

        // Keep delegate alive as a field reference to prevent GC collection
        _winEventDelegate = OnWinEvent;

        // Steady-state periodic polling (500ms)
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _pollTimer.Tick += (_, _) => ScanRunningApps();

        // 50ms debounce for rapid WinEvent burst messages
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            ScanRunningApps();
        };
    }

    public void Start()
    {
        _pollTimer.Start();

        // Install Win32 hooks for real-time window create/destroy and foreground switch events
        try
        {
            _hookCreateDestroy = User32.SetWinEventHook(
                User32.EVENT_OBJECT_CREATE,
                User32.EVENT_OBJECT_DESTROY,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);

            _hookForeground = User32.SetWinEventHook(
                User32.EVENT_SYSTEM_FOREGROUND,
                User32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to install WinEventHook: {ex.Message}");
        }

        ScanRunningApps();
    }

    public void Stop()
    {
        _pollTimer.Stop();
        _debounceTimer.Stop();

        if (_hookCreateDestroy != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_hookCreateDestroy);
            _hookCreateDestroy = IntPtr.Zero;
        }

        if (_hookForeground != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_hookForeground);
            _hookForeground = IntPtr.Zero;
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only respond to top-level window events (OBJID_WINDOW is 0)
        if (idObject != 0 || hWnd == IntPtr.Zero) return;

        // Reset debounce timer on incoming events
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void ScanRunningApps()
    {
        var runningApps = new List<RunningAppInfo>();
        var appByKey = new Dictionary<string, RunningAppInfo>(StringComparer.OrdinalIgnoreCase);
        var activePids = new HashSet<uint>();

        User32.EnumWindows((hWnd, lParam) =>
        {
            if (!User32.IsWindowVisible(hWnd)) return true;

            // Filter out DWM cloaked windows (suspended UWP, background virtual desktop windows)
            if (DwmApi.IsWindowCloaked(hWnd)) return true;

            int length = User32.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            long exStyle = User32.GetWindowLongPtr(hWnd, User32.GWL_EXSTYLE).ToInt64();
            if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0) return true;

            // Check non-zero dimensions to exclude hidden message or stub windows
            if (!User32.GetWindowRect(hWnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
            {
                return true;
            }

            User32.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return true;

            // If it is our own process, only discover the Control Panel window, never our dock/notch overlays
            if (pid == _currentProcessId)
            {
                var sbOwn = new StringBuilder(length + 1);
                User32.GetWindowText(hWnd, sbOwn, sbOwn.Capacity);
                string ownTitle = sbOwn.ToString();

                if (!ownTitle.Contains("Control Center", StringComparison.OrdinalIgnoreCase) &&
                    !ownTitle.Contains("Custom Desktop", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            try
            {
                activePids.Add(pid);

                // Use cached process information when available to avoid high-frequency allocations
                if (!_processInfoCache.TryGetValue(pid, out var procInfo))
                {
                    using var proc = Process.GetProcessById((int)pid);
                    string procName = proc.ProcessName;
                    string exePath = GetProcessPath(pid, proc);
                    procInfo = (procName, exePath);
                    _processInfoCache[pid] = procInfo;
                }

                string procNameVal = procInfo.ProcessName;
                string exePathVal = procInfo.ExePath;

                // Skip known desktop shells / background services
                if (procNameVal.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    var sb = new StringBuilder(length + 1);
                    User32.GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    // Only include actual Explorer file windows, skip Program Manager and shell trays
                    if (title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(title))
                    {
                        return true;
                    }
                }
                else if (procNameVal.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                         procNameVal.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                         procNameVal.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var titleBuilder = new StringBuilder(length + 1);
                User32.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string windowTitle = titleBuilder.ToString();

                // Group by process executable to keep dock clean and aggregate window handles
                string key = !string.IsNullOrEmpty(exePathVal) ? exePathVal : procNameVal;
                if (appByKey.TryGetValue(key, out var existing))
                {
                    if (!existing.WindowHandles.Contains(hWnd))
                    {
                        existing.WindowHandles.Add(hWnd);
                    }
                }
                else
                {
                    var app = new RunningAppInfo
                    {
                        WindowHandle = hWnd,
                        ProcessId = pid,
                        ProcessName = procNameVal,
                        ExecutablePath = exePathVal,
                        Title = windowTitle
                    };
                    app.WindowHandles.Add(hWnd);
                    appByKey[key] = app;
                    runningApps.Add(app);
                }
            }
            catch
            {
                // Process may have exited during scan
            }

            return true;
        }, IntPtr.Zero);

        // Prune terminated PIDs from cache periodically
        if (_processInfoCache.Count > activePids.Count + 50)
        {
            foreach (var cachedPid in _processInfoCache.Keys)
            {
                if (!activePids.Contains(cachedPid))
                {
                    _processInfoCache.TryRemove(cachedPid, out _);
                }
            }
        }

        RunningAppsChanged?.Invoke(runningApps);
    }

    private static string GetProcessPath(uint pid, Process proc)
    {
        try
        {
            string? fileName = proc.MainModule?.FileName;
            if (!string.IsNullOrEmpty(fileName)) return fileName;
        }
        catch
        {
            // Process.MainModule can throw for 32/64-bit mismatch or elevated privileges
        }

        // Robust fallback: QueryFullProcessImageName via PROCESS_QUERY_LIMITED_INFORMATION
        IntPtr hProc = Kernel32.OpenProcess(Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc != IntPtr.Zero)
        {
            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (Kernel32.QueryFullProcessImageName(hProc, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                Kernel32.CloseHandle(hProc);
            }
        }

        return string.Empty;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            Stop();
        }
        GC.SuppressFinalize(this);
    }
}
