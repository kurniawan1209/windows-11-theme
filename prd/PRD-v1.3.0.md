# Product Requirements Document (PRD)

## Custom Windows Dock + Notch Application

**Document Version:** 1.3.0

**Target Platform:** Windows 11 (Build 22000+ / 22621+ Recommended)

**Technology Stack:** C#, WPF (.NET 8.0/10.0-windows10.0.22621.0), Win32 Interop, Windows GSMTC, DWM Backdrops (Mica / Acrylic)

**Development Architecture:** Hybrid WSL2 (Repository/Editing in WSL; Native Windows SDK for Build/Execution)

---

## 1. Executive Summary & Goals

The objective of this project is to build a unified, modular desktop customization suite for Windows 11 that behaves like a standard consumer application. Users have full autonomy to toggle major components and individual modules on or off:

1. **Admin / Control Panel on Startup:** The application starts into a modern Windows 11 Fluent Control Center where users can visually review, toggle, and configure all features before or while desktop overlays run.
2. **Independent Core Features:** Either the **Floating Dock** or the **Interactive Notch** can be toggled completely off, allowing the user to run only the dock, only the notch, or both simultaneously.
3. **Modular Sub-Features:** Individual components within each interface (media controls, system indicators, auto-hide, running indicators) can be enabled or disabled via settings and the control panel.
4. **Standard App Lifecycle:** Background system tray presence with a standard context menu for quick toggle access, Control Panel reopening, and clean application exit. Closing the Control Panel minimizes to the system tray.

---

## 2. Architecture & Tech Stack

| Component | Choice / Implementation | Reason / Constraint |
| --- | --- | --- |
| **Language** | C# | Native performance, deep Windows API interop. |
| **UI Framework** | WPF (XAML) | Hardware-accelerated graphics, robust styling/animation system. |
| **Target Framework** | `net8.0-windows10.0.22621.0` or `net10.0-windows10.0.22621.0` | Targets Windows 11 22H2+ SDK; direct WinRT access (GSMTC, Bluetooth). |
| **Desktop Visuals** | Win32 DWM Backdrops (`dwmapi.dll`) | Hardware-accelerated native Windows 11 **Mica** (`DWMWA_SYSTEMBACKDROP_TYPE = 2`) or **Acrylic** (`DWMWA_SYSTEMBACKDROP_TYPE = 3`). |
| **Windowing Mode** | Multi-Window, `ShutdownMode="OnExplicitShutdown"` | Controlled lifecycle: windows are shown, hidden, or closed independently based on user toggles. |
| **App Management** | WinForms `NotifyIcon` + Control Panel Window | Native system tray icon and Control Center providing toggles (Enable/Disable Dock, Enable/Disable Notch, Settings, Exit). |
| **Configuration** | JSON via `System.Text.Json` | Stored under `%LOCALAPPDATA%\CustomDesktop\settings.json`. Zero external database overhead. |
| **Development Setup** | Hybrid WSL2 + Windows Host | Source files in WSL2; compilation, debugging, and execution run via Windows host .NET SDK. |

---

## 3. Modularity & Feature Toggle Architecture

The application runs as a single background process managing two independent desktop window controllers and an Admin / Control Panel. When a feature is toggled off, its window resources and background event listeners are detached to ensure zero resource consumption.

```
                     ┌───────────────────────────────┐
                     │       App.xaml.cs Entry       │
                     │  (ShutdownMode=Explicit)      │
                     └───────────────┬───────────────┘
                                     │
                     ┌───────────────┴───────────────┐
                     ▼                               ▼
       ┌───────────────────────────┐   ┌───────────────────────────┐
       │   Control Panel Window    │   │     Settings Service      │
       │   (Primary Admin UI)      │◄──┤  (settings.json watcher)  │
       │  - Master Dock Toggle     │   └─────────────┬─────────────┘
       │  - Master Notch Toggle    │                 │
       │  - Granular Sub-Toggles   │                 │
       └─────────────┬─────────────┘                 │
                     │                               ▼
                     │                 ┌───────────────────────────┐
                     │                 │    System Tray Manager    │
                     │                 │   - Quick Toggles         │
                     │                 │   - Control Panel / Exit  │
                     │                 └─────────────┬─────────────┘
                     ▼                               ▼
          ┌──────────────────────────────────────────┘
          ▼                     ▼
┌──────────────────┐  ┌──────────────────┐
│   Dock Window    │  │   Notch Window   │
│ [Enabled: True]  │  │ [Enabled: True]  │
│  - Launcher      │  │  - Clock/Date    │
│  - Auto-Hide     │  │  - Media Controls│
│  - Running Dots  │  │  - System Badges │
└──────────────────┘  └──────────────────┘
```

### Dynamic State Behaviors:

* **Startup Presentation:** On initial launch, the Control Panel window opens centered on screen.
* **Dock Disabled:** `DockWindow.Close()` (or `Hide()`), process monitoring hooks unsubscribed.
* **Notch Disabled:** `NotchWindow.Close()` (or `Hide()`), GSMTC and CoreAudio listeners detached.
* **Both Overlays Disabled:** Overlays are unmounted; user can manage settings via Control Panel or tray.
* **Control Panel Closed:** Minimizes to system tray when `CloseToTray: true`; process stays idle (< 0.05% CPU).
* **Tray Icon Double-Click:** Re-opens the Control Panel immediately.
* **Exit:** Explicit termination of all windows, removal of tray icon, and process exit.

---

## 4. Window Behavior & Low-Level Specifications

Both `DockWindow` and `NotchWindow` must strictly adhere to the following desktop window constraints:

1. **Non-Focus-Stealing (`WS_EX_NOACTIVATE = 0x08000000`):**
* Interacting with the dock or notch never steals keyboard focus from active games, editors, or fullscreen tools.
* `WM_MOUSEACTIVATE` returns `MA_NOACTIVATE`.

2. **Task Switcher Exclusion (`WS_EX_TOOLWINDOW = 0x00000080`):**
* Neither overlay window appears in `Alt + Tab` or the Windows 11 Task View.
* `ShowInTaskbar="False"` configured in XAML for overlays. The Control Panel window is a standard window.

3. **Z-Ordering (`Topmost`):**
* Overlay windows stay pinned topmost on the desktop layer without overriding full-screen exclusive DirectX apps.

4. **DPI & Multi-Monitor Support:**
* Declared `PerMonitorV2` in `app.manifest`.
* Monitor bounds queried via `MonitorFromWindow` and `GetMonitorInfo` to accurately position windows across mixed scaling factors.

5. **Windows 11 Native Backdrops & Corners:**
* Hardware-accelerated Mica or Acrylic backdrops applied via `DwmSetWindowAttribute`.

---

## 5. Feature Specifications

### 5.1 Floating Dock

* **Master Toggle:** `Dock.Enabled` (true/false).
* **Positioning:** Centered horizontally along the bottom edge of the selected display.
* **Item Management:**
  * Displays pinned apps and running applications.
  * Launch apps via `Process.Start`.
  * Dynamic reordering via drag-and-drop.
  * Pin/Unpin actions via context menu.
* **Running Indicators (Toggleable):** Dot indicators beneath active apps; clicking an active app restores its window (`SetForegroundWindow`).
* **Auto-Hide (Toggleable):** Slides out of view when the mouse leaves; edge-hover restores visibility. Configurable delay.

---

### 5.2 Interactive Notch

* **Master Toggle:** `Notch.Enabled` (true/false).
* **Compact Mode (Pill):** Displays current time, compact date, and minimal system badges.
* **Expanded Mode:** Triggered via click (or optional hover). Smoothly expands downward to expose:
  * Media playback card (GSMTC).
  * Master volume slider.
  * Quick-toggle action buttons (Wi-Fi, Bluetooth).
  * System notification summary.

---

### 5.3 System Indicators & Controls (Modular Toggles)

Each indicator within the Notch can be individually toggled on or off:

* **Sound / Volume (`ShowSound`):** Master volume slider and mute status via CoreAudio COM APIs (`IMMDeviceEnumerator` / `AudioEndpointVolume`).
* **Battery (`ShowBattery`):** Percentage and charging status. Auto-hides on desktops without a battery (`NoSystemBattery`).
* **Network / Wi-Fi (`ShowNetwork`):** Connection indicator. Action opens `ms-settings:network`.
* **Bluetooth (`ShowBluetooth`):** Connection status. Action opens `ms-settings:bluetooth`.
* **System Tray Visibility Guide:** In-app guide explaining how to hide original Windows 11 taskbar icons (`ms-settings:taskbar`). No unsupported Explorer injection.

---

### 5.4 Media Integration (Windows GSMTC)

* **Master Module Toggle:** `ShowMedia` (true/false).
* **Capabilities:** Detects active sessions (Spotify, Chrome, Apple Music, VLC), displays track title, artist, and album artwork, and handles Play/Pause/Next/Previous commands.
* **Zero Session Behavior:** Section collapses automatically when no active audio is detected.

---

### 5.5 App Management & Control Panel UI

* **Control Panel Window (Admin UI):**
  * Launches on startup or via system tray icon.
  * Fluent Windows 11 dark-mode card interface.
  * Master switches with real-time status badges for Dock and Notch.
  * Granular switches for each module (Auto-Hide, Running Dots, Sound, Battery, Network, Bluetooth, Media).
  * System preferences (Launch on Startup, Close to Tray, Animation Toggle).
  * Direct action buttons: "Minimize to Tray", "Open Config Folder", "Exit App".

* **System Tray Menu:**
  * Toggle Dock (Checked / Unchecked)
  * Toggle Notch (Checked / Unchecked)
  * Control Panel... (Restores Control Center)
  * Exit

---

## 6. Non-Functional Requirements & Performance Targets

1. **Idle Resource Consumption:** `< 0.5%` CPU usage when idle; `< 0.05%` CPU when overlays are closed and minimized to tray.
2. **Memory Footprint:** `< 90 MB` working set during normal operation; `< 35 MB` when overlays are disabled (tray-only mode).
3. **No Polling Anti-Patterns:** Event-driven architecture for CoreAudio, Network, and GSMTC.
4. **Thread Safety:** Marshaled updates to the WPF UI thread via `Dispatcher.InvokeAsync`.

---

## 7. Implementation Milestones

```
[Milestone 1: App Lifecycle, Tray Shell & Control Panel] (CURRENT MILESTONE)
  ├── Admin / Control Panel Window on Startup (Master & Granular Feature Switches)
  ├── System Tray NotifyIcon Setup (Context Menu: Dock, Notch, Control Panel, Exit)
  ├── Settings Engine (JSON serialization under %LOCALAPPDATA%)
  └── Dynamic Window Instantiation / Teardown Logic (Overlays mount/unmount reactively)

[Milestone 2: Floating Dock]
  ├── Win32 No-Activate Window Base (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW)
  ├── Windows 11 DWM Backdrop (Mica / Acrylic)
  ├── Pinned Apps Model & AppLauncherService
  ├── Drag-and-Drop Reordering
  └── Auto-Hide Animations

[Milestone 3: Interactive Notch]
  ├── Top-Centered Pill Geometry & Positioning
  ├── DispatcherTimer Clock/Date
  └── Compact <-> Expanded State Storyboards

[Milestone 4: Modular System Controls & Media]
  ├── SoundService (CoreAudio COM)
  ├── BatteryService (PowerStatus fallback)
  ├── NetworkService & Bluetooth Shortcuts
  └── WinRT GSMTC Integration

[Milestone 5: Advanced Customization & Polishing]
  ├── Advanced Settings Configuration (Icon sizes, custom paddings, backdrop selection)
  ├── Auto-Start with Windows Registry Run key integration
  └── Multi-Monitor & Mixed DPI Scaling Verification
```

---

## 8. Data Contract: `AppSettings.cs` Reference

```json
{
  "Version": "1.3.0",
  "Dock": {
    "Enabled": true,
    "IconSize": 48,
    "AutoHide": true,
    "AutoHideDelayMs": 300,
    "ShowRunningIndicators": true,
    "TargetDisplayIndex": 0,
    "BackdropType": "Acrylic",
    "PinnedApps": [
      {
        "Name": "File Explorer",
        "ExecutablePath": "explorer.exe",
        "Arguments": ""
      }
    ]
  },
  "Notch": {
    "Enabled": true,
    "WidthCompact": 180,
    "HeightCompact": 32,
    "WidthExpanded": 420,
    "HeightExpanded": 240,
    "ShowClock": true,
    "ShowBattery": true,
    "ShowSound": true,
    "ShowNetwork": true,
    "ShowBluetooth": true,
    "ShowMedia": true,
    "ExpandOnHover": false,
    "BackdropType": "Mica"
  },
  "System": {
    "EnableAnimations": true,
    "LaunchOnStartup": false,
    "CloseToTray": true
  }
}
```
