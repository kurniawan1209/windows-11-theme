using System;
using System.Collections.Generic;
using System.IO;
using CustomDesktop.Core.Models;

namespace CustomDesktop.Services;

public static class TaskbarShortcutHelper
{
    public static List<PinnedAppConfig> GetWindowsTaskbarPinnedApps()
    {
        var result = new List<PinnedAppConfig>();

        string taskbarDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        if (!Directory.Exists(taskbarDir))
        {
            return result;
        }

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        dynamic? shell = shellType != null ? Activator.CreateInstance(shellType) : null;

        var lnkFiles = Directory.GetFiles(taskbarDir, "*.lnk");
        foreach (var file in lnkFiles)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string target = string.Empty;
                string args = string.Empty;

                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(file);
                    target = shortcut.TargetPath ?? string.Empty;
                    args = shortcut.Arguments ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(target))
                {
                    if (name.Equals("File Explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        target = "explorer.exe";
                    }
                    else
                    {
                        target = IconExtractor.ResolveExecutablePath(name);
                    }
                }

                if (!string.IsNullOrWhiteSpace(target))
                {
                    result.Add(new PinnedAppConfig
                    {
                        Name = name,
                        ExecutablePath = target,
                        Arguments = args
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading shortcut {file}: {ex.Message}");
            }
        }

        return result;
    }
}
