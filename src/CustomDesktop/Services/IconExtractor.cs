using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CustomDesktop.Core.Native;

namespace CustomDesktop.Services;

public static class IconExtractor
{
    private static readonly ConcurrentDictionary<string, ImageSource> PathCache = new(StringComparer.OrdinalIgnoreCase);

    public static string ResolveExecutablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        // 1. Direct file check
        if (File.Exists(path)) return path;

        string fileName = Path.GetFileName(path);

        // 2. Windows and System32 directories
        string system32Path = Path.Combine(Environment.SystemDirectory, fileName);
        if (File.Exists(system32Path)) return system32Path;

        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string winPath = Path.Combine(winDir, fileName);
        if (File.Exists(winPath)) return winPath;

        // 3. Search PATH environment variable
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), fileName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
        }

        // 4. Registry App Paths
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{fileName}");
            if (key != null)
            {
                var val = key.GetValue(null) as string;
                if (!string.IsNullOrEmpty(val) && File.Exists(val)) return val;
            }
        }
        catch { }

        return path;
    }

    public static ImageSource? GetIconForPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;

        string path = ResolveExecutablePath(rawPath);

        if (PathCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // Direct fallback for CustomDesktop application icon
        if (path.EndsWith("CustomDesktop.exe", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("CustomDesktop.dll", StringComparison.OrdinalIgnoreCase))
        {
            string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(localIco))
            {
                try
                {
                    using var sysIco = new Icon(localIco, 48, 48);
                    var src = ToBitmapSource(sysIco.Handle);
                    if (src != null)
                    {
                        PathCache[path] = src;
                        return src;
                    }
                }
                catch { }
            }
        }

        try
        {
            var shinfo = new Shell32.SHFILEINFO();
            uint flags = Shell32.SHGFI_ICON | Shell32.SHGFI_LARGEICON;

            IntPtr result = Shell32.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (result != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                var imageSource = ToBitmapSource(shinfo.hIcon);
                User32.DestroyIcon(shinfo.hIcon);

                if (imageSource != null)
                {
                    PathCache[path] = imageSource;
                    return imageSource;
                }
            }

            // Fallback: System.Drawing.Icon.ExtractAssociatedIcon
            if (File.Exists(path))
            {
                using var sysIcon = Icon.ExtractAssociatedIcon(path);
                if (sysIcon != null)
                {
                    var imageSource = ToBitmapSource(sysIcon.Handle);
                    if (imageSource != null)
                    {
                        PathCache[path] = imageSource;
                        return imageSource;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Icon extraction error for {path}: {ex.Message}");
        }

        // Clean fallback glyph instead of null
        var fallback = CreateFallbackIcon();
        if (fallback != null)
        {
            PathCache[path] = fallback;
        }
        return fallback;
    }

    public static ImageSource? GetIconForWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return null;

        try
        {
            IntPtr hIcon = User32.SendMessage(hWnd, User32.WM_GETICON, new IntPtr(User32.ICON_BIG), IntPtr.Zero);
            if (hIcon == IntPtr.Zero)
            {
                hIcon = User32.SendMessage(hWnd, User32.WM_GETICON, new IntPtr(User32.ICON_SMALL2), IntPtr.Zero);
            }
            if (hIcon == IntPtr.Zero)
            {
                hIcon = User32.GetClassLongPtr(hWnd, User32.GCLP_HICON);
            }

            if (hIcon != IntPtr.Zero)
            {
                return ToBitmapSource(hIcon);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Window icon extraction error: {ex.Message}");
        }

        return null;
    }

    private static BitmapSource? ToBitmapSource(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;

        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            if (bitmapSource.CanFreeze)
            {
                bitmapSource.Freeze();
            }

            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? CreateFallbackIcon()
    {
        try
        {
            using var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(39, 39, 42));
                g.FillEllipse(brush, 4, 4, 40, 40);

                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(56, 189, 248), 2.0f);
                g.DrawEllipse(pen, 5, 5, 38, 38);

                using var font = new Font(System.Drawing.FontFamily.GenericSansSerif, 14, System.Drawing.FontStyle.Bold);
                using var textBrush = new SolidBrush(System.Drawing.Color.FromArgb(244, 244, 245));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("◆", font, textBrush, new RectangleF(0, 0, 48, 48), sf);
            }

            IntPtr hIcon = bitmap.GetHicon();
            var bs = ToBitmapSource(hIcon);
            User32.DestroyIcon(hIcon);
            return bs;
        }
        catch
        {
            return null;
        }
    }
}
