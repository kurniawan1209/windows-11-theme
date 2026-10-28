using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CustomDesktop.Core.Models;
using CustomDesktop.Services;

namespace CustomDesktop.ViewModels;

public class DockViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly RunningAppsService _runningAppsService;
    private bool _isDisposed;

    public ObservableCollection<DockItem> Items { get; } = new();

    public int IconSize => _settingsService.CurrentSettings.Dock.IconSize;
    public int ItemGap => _settingsService.CurrentSettings.Dock.ItemGap;
    public int MarginFromScreen => _settingsService.CurrentSettings.Dock.MarginFromScreen;
    public double Opacity => _settingsService.CurrentSettings.Dock.Opacity;
    public double CornerRadius => _settingsService.CurrentSettings.Dock.CornerRadius;
    public bool ShowRunningIndicators => _settingsService.CurrentSettings.Dock.ShowRunningIndicators;
    public bool AutoHide => _settingsService.CurrentSettings.Dock.AutoHide;
    public int AutoHideDelayMs => _settingsService.CurrentSettings.Dock.AutoHideDelayMs;
    public bool EnableAnimations => _settingsService.CurrentSettings.System.EnableAnimations;

    public double ItemButtonWidth => IconSize + ItemGap;

    public double CalculatedWidth
    {
        get
        {
            int count = Math.Max(1, Items.Count);
            double desired = (count * ItemButtonWidth) + 24;
            double maxWidth = SystemParameters.PrimaryScreenWidth - 40;
            return Math.Min(maxWidth, desired);
        }
    }

    public double CalculatedHeight => IconSize + 20;

    public DockViewModel(ISettingsService settingsService, RunningAppsService runningAppsService)
    {
        _settingsService = settingsService;
        _runningAppsService = runningAppsService;

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CalculatedWidth));
            OnPropertyChanged(nameof(CalculatedHeight));
        };

        _settingsService.SettingsChanged += OnSettingsChanged;
        _runningAppsService.RunningAppsChanged += OnRunningAppsChanged;

        InitializePinnedApps();
        _runningAppsService.Start();
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        OnPropertyChanged(nameof(IconSize));
        OnPropertyChanged(nameof(ItemGap));
        OnPropertyChanged(nameof(MarginFromScreen));
        OnPropertyChanged(nameof(ItemButtonWidth));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(ShowRunningIndicators));
        OnPropertyChanged(nameof(AutoHide));
        OnPropertyChanged(nameof(AutoHideDelayMs));
        OnPropertyChanged(nameof(EnableAnimations));
        OnPropertyChanged(nameof(CalculatedWidth));
        OnPropertyChanged(nameof(CalculatedHeight));
    }

    private void InitializePinnedApps()
    {
        Items.Clear();

        foreach (var pinned in _settingsService.CurrentSettings.Dock.PinnedApps)
        {
            string resolvedPath = IconExtractor.ResolveExecutablePath(pinned.ExecutablePath);
            if (!File.Exists(resolvedPath))
            {
                continue; // Never add items that do not exist on disk
            }

            var icon = IconExtractor.GetIconForPath(resolvedPath);
            if (icon == null) continue;

            var item = new DockItem
            {
                Name = pinned.Name,
                ExecutablePath = resolvedPath,
                Arguments = pinned.Arguments,
                IsPinned = true,
                IsRunning = false,
                Icon = icon
            };
            item.PinToggled += OnDockItemPinToggled;
            Items.Add(item);
        }

        OnPropertyChanged(nameof(CalculatedWidth));
        OnPropertyChanged(nameof(CalculatedHeight));
    }

    private void OnDockItemPinToggled(DockItem item)
    {
        if (item.IsPinned)
        {
            // Unpin from dock
            item.IsPinned = false;
            _settingsService.UpdateSettings(s =>
            {
                s.Dock.PinnedApps.RemoveAll(p => p.ExecutablePath.Equals(item.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                                                 p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            });

            if (!item.IsRunning)
            {
                Items.Remove(item);
            }
            else
            {
                // Move after all pinned items
                int lastPinnedIndex = -1;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].IsPinned) lastPinnedIndex = i;
                }

                int currentIndex = Items.IndexOf(item);
                if (currentIndex >= 0 && lastPinnedIndex >= 0 && currentIndex < lastPinnedIndex)
                {
                    Items.Move(currentIndex, lastPinnedIndex);
                }
            }
        }
        else
        {
            // Pin to dock
            item.IsPinned = true;
            _settingsService.UpdateSettings(s =>
            {
                if (!s.Dock.PinnedApps.Any(p => p.ExecutablePath.Equals(item.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    s.Dock.PinnedApps.Add(new PinnedAppConfig
                    {
                        Name = item.Name,
                        ExecutablePath = item.ExecutablePath,
                        Arguments = item.Arguments
                    });
                }
            });

            // Move to end of pinned items
            int targetIndex = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].IsPinned && Items[i] != item) targetIndex = i + 1;
            }

            int currentIndex = Items.IndexOf(item);
            if (currentIndex >= 0 && targetIndex < Items.Count && currentIndex != targetIndex)
            {
                Items.Move(currentIndex, targetIndex);
            }
        }

        OnPropertyChanged(nameof(CalculatedWidth));
    }

    public void ReorderPinnedApps(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count || oldIndex == newIndex)
        {
            return;
        }

        var sourceItem = Items[oldIndex];
        if (!sourceItem.IsPinned) return;

        // Ensure target is within the pinned boundary
        int pinnedCount = Items.Count(i => i.IsPinned);
        if (newIndex >= pinnedCount)
        {
            newIndex = pinnedCount - 1;
        }

        Items.Move(oldIndex, newIndex);

        // Synchronize and persist updated pinned order
        _settingsService.UpdateSettings(settings =>
        {
            var orderedPinned = Items.Where(i => i.IsPinned).ToList();
            var newPinnedConfigs = new List<PinnedAppConfig>();

            foreach (var item in orderedPinned)
            {
                var existing = settings.Dock.PinnedApps.FirstOrDefault(p =>
                    p.ExecutablePath.Equals(item.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    newPinnedConfigs.Add(new PinnedAppConfig
                    {
                        Name = existing.Name,
                        ExecutablePath = existing.ExecutablePath,
                        Arguments = existing.Arguments
                    });
                }
                else
                {
                    newPinnedConfigs.Add(new PinnedAppConfig
                    {
                        Name = item.Name,
                        ExecutablePath = item.ExecutablePath,
                        Arguments = item.Arguments
                    });
                }
            }

            settings.Dock.PinnedApps = newPinnedConfigs;
        });
    }

    private void OnRunningAppsChanged(List<RunningAppInfo> runningApps)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_isDisposed) return;

            var matchedRunningPids = new HashSet<uint>();

            // 1. Update running status on pinned apps
            foreach (var pinned in Items.Where(i => i.IsPinned))
            {
                var match = runningApps.FirstOrDefault(r =>
                    MatchesApp(pinned.ExecutablePath, pinned.Name, r.ExecutablePath, r.ProcessName));

                if (match != null)
                {
                    pinned.IsRunning = true;
                    pinned.WindowHandle = match.WindowHandle;
                    if (!pinned.WindowHandles.SequenceEqual(match.WindowHandles))
                    {
                        pinned.WindowHandles = new List<IntPtr>(match.WindowHandles);
                    }
                    matchedRunningPids.Add(match.ProcessId);

                    pinned.Icon ??= IconExtractor.GetIconForPath(match.ExecutablePath) ??
                                    IconExtractor.GetIconForWindow(match.WindowHandle);
                }
                else
                {
                    if (pinned.IsRunning)
                    {
                        pinned.IsRunning = false;
                        pinned.WindowHandle = IntPtr.Zero;
                        pinned.WindowHandles = new List<IntPtr>();
                    }
                }
            }

            // 2. Remove unpinned apps that are no longer active
            var unpinnedItemsToRemove = Items
                .Where(i => !i.IsPinned && !runningApps.Any(r => r.WindowHandles.Contains(i.WindowHandle) || r.WindowHandle == i.WindowHandle))
                .ToList();

            foreach (var toRemove in unpinnedItemsToRemove)
            {
                Items.Remove(toRemove);
            }

            // 3. Add or update active unpinned running apps in stable chronological order
            foreach (var active in runningApps)
            {
                if (matchedRunningPids.Contains(active.ProcessId)) continue;

                var existing = Items.FirstOrDefault(i => !i.IsPinned &&
                    (active.WindowHandles.Contains(i.WindowHandle) ||
                     (!string.IsNullOrEmpty(i.ExecutablePath) && i.ExecutablePath.Equals(active.ExecutablePath, StringComparison.OrdinalIgnoreCase))));

                if (existing != null)
                {
                    // Update window handles without modifying item position
                    if (!existing.WindowHandles.SequenceEqual(active.WindowHandles))
                    {
                        existing.WindowHandles = new List<IntPtr>(active.WindowHandles);
                    }
                    existing.WindowHandle = active.WindowHandle;
                    continue;
                }

                var icon = IconExtractor.GetIconForPath(active.ExecutablePath) ??
                           IconExtractor.GetIconForWindow(active.WindowHandle);

                if (icon == null) continue;

                string displayName = !string.IsNullOrWhiteSpace(active.Title) ? active.Title : active.ProcessName;

                var newItem = new DockItem
                {
                    Name = displayName,
                    ExecutablePath = active.ExecutablePath,
                    WindowHandle = active.WindowHandle,
                    WindowHandles = new List<IntPtr>(active.WindowHandles),
                    IsPinned = false,
                    IsRunning = true,
                    Icon = icon
                };
                newItem.PinToggled += OnDockItemPinToggled;
                Items.Add(newItem);
            }

            OnPropertyChanged(nameof(CalculatedWidth));
            OnPropertyChanged(nameof(CalculatedHeight));
        });
    }

    private static bool MatchesApp(string pinnedPath, string pinnedName, string runningPath, string runningProcName)
    {
        if (!string.IsNullOrEmpty(pinnedPath) && !string.IsNullOrEmpty(runningPath) &&
            pinnedPath.Equals(runningPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pinnedExeName = Path.GetFileNameWithoutExtension(pinnedPath);
        if (!string.IsNullOrEmpty(pinnedExeName) && pinnedExeName.Equals(runningProcName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _runningAppsService.RunningAppsChanged -= OnRunningAppsChanged;
        }
        GC.SuppressFinalize(this);
    }
}
