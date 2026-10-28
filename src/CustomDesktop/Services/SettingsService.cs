using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CustomDesktop.Core.Models;

namespace CustomDesktop.Services;

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly object _lock = new();

    public AppSettings CurrentSettings { get; private set; }

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "CustomDesktop");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");

        CurrentSettings = new AppSettings();
        Load();
    }

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        bool needsSave = false;

                        if (!json.Contains("\"ItemGap\"") || !json.Contains("\"MarginFromScreen\""))
                        {
                            needsSave = true;
                        }

                        if (settings.Dock.ItemGap <= 0)
                        {
                            settings.Dock.ItemGap = 8;
                            needsSave = true;
                        }
                        if (settings.Dock.MarginFromScreen < 0)
                        {
                            settings.Dock.MarginFromScreen = 10;
                            needsSave = true;
                        }
                        if (settings.Notch.MarginFromScreen < 0)
                        {
                            settings.Notch.MarginFromScreen = 0;
                            needsSave = true;
                        }
                        if (settings.Dock.Opacity <= 0.05)
                        {
                            settings.Dock.Opacity = 0.85;
                            needsSave = true;
                        }
                        if (settings.Dock.CornerRadius <= 0.01)
                        {
                            settings.Dock.CornerRadius = 24.0;
                            needsSave = true;
                        }
                        if (settings.Notch.Opacity <= 0.05)
                        {
                            settings.Notch.Opacity = 0.92;
                            needsSave = true;
                        }
                        if (settings.Notch.CornerRadius <= 0.01)
                        {
                            settings.Notch.CornerRadius = 16.0;
                            needsSave = true;
                        }

                        // Clean up any broken pinned apps that cannot be resolved on this system
                        var brokenApps = settings.Dock.PinnedApps
                            .Where(p => !string.IsNullOrEmpty(p.ExecutablePath) &&
                                        !File.Exists(p.ExecutablePath) &&
                                        !File.Exists(IconExtractor.ResolveExecutablePath(p.ExecutablePath)))
                            .ToList();

                        foreach (var broken in brokenApps)
                        {
                            settings.Dock.PinnedApps.Remove(broken);
                            needsSave = true;
                        }

                        if (settings.Version != "1.7.0")
                        {
                            var taskbarApps = TaskbarShortcutHelper.GetWindowsTaskbarPinnedApps();
                            if (taskbarApps.Count > 0)
                            {
                                settings.Dock.PinnedApps = taskbarApps;
                            }
                            settings.Version = "1.7.0";
                            needsSave = true;
                        }

                        if (needsSave)
                        {
                            CurrentSettings = settings;
                            Save();
                            return;
                        }

                        CurrentSettings = settings;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            CurrentSettings = CreateDefaultSettings();
            Save();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
                string tempFile = _settingsFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _settingsFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }

    public void UpdateSettings(Action<AppSettings> updateAction)
    {
        lock (_lock)
        {
            updateAction(CurrentSettings);
            Save();
        }
        SettingsChanged?.Invoke(CurrentSettings);
    }

    private static AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings { Version = "1.7.0" };

        var taskbarApps = TaskbarShortcutHelper.GetWindowsTaskbarPinnedApps();
        if (taskbarApps.Count > 0)
        {
            settings.Dock.PinnedApps = taskbarApps;
        }
        else
        {
            settings.Dock.PinnedApps.Add(new PinnedAppConfig
            {
                Name = "File Explorer",
                ExecutablePath = "explorer.exe",
                Arguments = ""
            });

            settings.Dock.PinnedApps.Add(new PinnedAppConfig
            {
                Name = "Notepad",
                ExecutablePath = "notepad.exe",
                Arguments = ""
            });

            settings.Dock.PinnedApps.Add(new PinnedAppConfig
            {
                Name = "Terminal",
                ExecutablePath = "cmd.exe",
                Arguments = ""
            });
        }

        return settings;
    }
}
