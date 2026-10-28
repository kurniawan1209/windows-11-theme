# CustomDesktop (Win-Theme)

A modular, modern Windows 11 desktop customization suite featuring an interactive **Floating Dock**, dynamic **Top Notch**, and a centralized **Fluent Control Center**.

Built with **C#**, **WPF**, and **.NET 8.0** targeting native Windows 11 SDK (`net8.0-windows10.0.22621.0`) with direct Win32 API interop and DirectComposition alpha transparency.

---

## Features

### 1. Control Panel & App Lifecycle
- **Centralized Admin Center:** Opens on application startup to let you visually review, customize, and toggle desktop components in real-time.
- **System Tray Integration:** Runs discreetly in the Windows taskbar notification area with quick-toggle menus for Dock, Notch, Control Panel, and Exit.
- **Modular Toggle Architecture:** Enable only the Dock, only the Notch, or both simultaneously without interfering with each other.
- **Settings Engine:** Automatically persists configurations to `%LOCALAPPDATA%\CustomDesktop\settings.json`.

### 2. Floating Dock
- **Taskbar Replacement:** Seamlessly hides the native Windows taskbar (`Shell_TrayWnd`) and positions the dock at the bottom screen edge.
- **Dynamic Auto-Resizing:** Automatically recalculates width based on open and pinned items, expanding outward and contracting inward with smooth center-anchored cubic ease transitions.
- **Background-Only Opacity:** Background pill opacity can be adjusted (10% to 100%) while preserving 100% crisp foreground icon fidelity and glowing running indicator dots.
- **Native Taskbar Ergonomics:**
  - **Pinned Apps First:** Discovers user shortcuts directly from Windows Taskbar pinned shortcuts (`%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`).
  - **Chronological Running Apps:** Unpinned running apps append stably after pinned items.
  - **Multi-Window Sign & Selection Picker:** Single running windows show a single dot (`•`); multiple open windows display a dual dot (`• •`). Clicking opens a Windows 11 Fluent Window Picker with live previews and close buttons (`✕`).
  - **Click Actions:** Click restores/focuses background windows or minimizes the foreground window; Middle-click and Shift + Left-click spawn new app instances.
  - **Context Menu:** Modern Windows 11 right-click menu with options to launch, run as administrator, close window, close all windows, or toggle pin status.

### 3. Interactive Notch
- **Top-Anchored Pill:** Elegant Dynamic Island-style overlay flush against the top edge of your primary display.
- **Compact & Expanded Modes:** Displays current time, date, and system status indicators, expandable for media playback controls and volume.
- **Non-Focus-Stealing Architecture:** Implemented with `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW` so interacting with overlays never steals focus from games or active applications.

---

## Project Structure

```
win-theme/
├── .gitignore                      # Git ignore rules for .NET, VS, and build output
├── README.md                       # Project documentation
├── PRD.md                          # Latest Product Requirements Document (v1.10.0)
├── prd/                            # PRD version history (v1.3.0 - v1.10.0)
├── files/                          # Documentation assets and screenshots
└── src/
    └── CustomDesktop/              # WPF .NET 8.0 Application
        ├── App.xaml / App.xaml.cs  # Application entry point & explicit shutdown lifecycle
        ├── app.manifest            # PerMonitorV2 DPI awareness manifest
        ├── Assets/                 # Icons and image resources
        ├── Core/
        │   ├── Models/             # AppSettings and DockItem models
        │   ├── Native/             # Win32 P/Invoke declarations (User32, Shell32, DwmApi)
        │   ├── TaskbarManager.cs   # Windows taskbar hide/restore controller
        │   └── WindowManager.cs    # Window management helper
        ├── Services/
        │   ├── SettingsService.cs  # JSON configuration manager
        │   ├── RunningAppsService.cs# Window event hooks & active app discovery
        │   └── IconExtractor.cs    # High-resolution native icon extraction
        ├── Tray/
        │   └── TrayIconManager.cs  # System tray icon and context menu
        ├── ViewModels/             # MVVM ViewModels (ControlPanel, Dock, Notch)
        └── Views/                  # XAML Views
            ├── Base/               # Non-activating base window logic
            ├── ControlPanelWindow  # Admin Control Panel UI
            ├── DockWindow          # Floating Dock overlay
            └── NotchWindow         # Interactive Top Notch overlay
```

---

## Prerequisites

- **Operating System:** Windows 11 (Build 22000+ or Build 22621+ recommended)
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or .NET 10.0 preview) with Windows Desktop workload
- **IDE:** Visual Studio 2022 / JetBrains Rider / VS Code with C# Dev Kit

---

## Building and Running

### Build the Application
```powershell
dotnet build src/CustomDesktop/CustomDesktop.csproj
```

### Run the Application
```powershell
dotnet run --project src/CustomDesktop/CustomDesktop.csproj
```

### Publish Release Build
```powershell
dotnet publish src/CustomDesktop/CustomDesktop.csproj -c Release -r win-x64 --self-contained false
```

---

## Configuration

Settings are saved in JSON format under `%LOCALAPPDATA%\CustomDesktop\settings.json`:

```json
{
  "Version": "1.10.0",
  "Dock": {
    "Enabled": true,
    "IconSize": 48,
    "ItemGap": 8,
    "MarginFromScreen": 10,
    "Opacity": 0.85,
    "CornerRadius": 24,
    "AutoHide": false,
    "ShowRunningIndicators": true,
    "HideWindowsTaskbar": true
  },
  "Notch": {
    "Enabled": true,
    "WidthCompact": 220,
    "HeightCompact": 36,
    "Opacity": 0.92,
    "CornerRadius": 16,
    "ShowClock": true
  },
  "System": {
    "EnableAnimations": true,
    "CloseToTray": true
  }
}
```

---

## License

This project is licensed under the MIT License - see the LICENSE file for details.
