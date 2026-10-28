# Product Requirements Document (PRD)

## Custom Windows Dock + Notch Application

**Document Version:** 1.9.0

**Target Platform:** Windows 11 (Build 22000+ / 22621+ Recommended)

**Technology Stack:** C#, WPF (.NET 8.0/10.0-windows10.0.22621.0), Win32 Interop, Windows GSMTC, DirectComposition Alpha Transparency

**Development Architecture:** Hybrid WSL2 (Repository/Editing in WSL; Native Windows SDK for Build/Execution)

---

## 1. Executive Summary & Goals

The objective of this project is to build a unified, modular desktop customization suite for Windows 11 that behaves like a standard consumer application:

1. **Admin / Control Panel on Startup:** The application starts into a modern Windows 11 Fluent Control Center where users can visually review, toggle, and configure all features before or while desktop overlays run.
2. **Independent Core Features:** Either the **Floating Dock** or the **Interactive Notch** can be toggled completely off, allowing the user to run only the dock, only the notch, or both simultaneously.
3. **Background-Only Opacity:** Opacity sliders strictly adjust the transparency of the background pill textures (`#18181B` / `#000000`). Application icons, running indicator dots, clock text, and system badges remain 100% solid, sharp, and fully opaque.
4. **Taskbar Integration & Floating Placement:** When the dock is active, the Windows taskbar is hidden, and the dock floats directly at the bottom edge of the display in the taskbar position.
5. **macOS-Style Active App Display with Native Windows Icons:** Running top-level applications are discovered and displayed on the dock with high-resolution Windows icons and glowing running dots. Pinned applications utilize robust system path resolution to guarantee uniform icon spacing without ghost/invisible item gaps.
6. **Dynamic Dock Width Resizing & Center-Anchored Symmetric Transitions:** The dock automatically recalculates its width based on active/opened applications. As applications launch or terminate, the dock smoothly expands outward from the center and contracts inward toward the center, maintaining perfect horizontal balance on the screen.
7. **Native Windows Taskbar Interaction Model, Multi-Window Picker & Icon Ordering:** Dock icons mirror native Windows taskbar ergonomics. Pinned shortcuts are discovered from the Windows taskbar and positioned first, followed by running unpinned apps in stable chronological launch order. When an app has multiple open windows, a dual indicator sign (`• •`) is displayed, and clicking opens an interactive Window Selection Picker popup. Left click toggles focus and minimization. Middle-click and Shift + Left-click spawn new application instances. Right-clicking an icon opens a modern Windows 11 Fluent context menu.
8. **Application Branding, Resilient Icon Loading & Compact Sizing:** Native embedded application icon (`app.ico`) across all standard resolutions, enabling CustomDesktop itself to appear in the dock. Resilient window icon initialization prevents BAML `TypeConverterMarkupExtension` startup exceptions. Ultra-compact icon sizing slider down to 16px.
9. **Standard App Lifecycle:** Background system tray presence with a context menu for quick toggle access, Control Panel reopening, and clean application exit.

---

## 2. Architecture & Tech Stack

| Component | Choice / Implementation | Reason / Constraint |
| --- | --- | --- |
| **Language** | C# | Native performance, deep Windows API interop. |
| **UI Framework** | WPF (XAML) | Hardware-accelerated graphics, robust styling/animation system. |
| **Target Framework** | `net8.0-windows10.0.22621.0` or `net10.0-windows10.0.22621.0` | Targets Windows 11 22H2+ SDK; direct WinRT access (GSMTC, Bluetooth). |
| **Desktop Visuals** | WPF DirectComposition Per-Pixel Alpha | Allows 100% transparent corners outside custom pill curvatures without rectangular DWM artifacts. |
| **Windowing Mode** | Multi-Window, `ShutdownMode="OnExplicitShutdown"` | Controlled lifecycle: windows are shown, hidden, or closed independently based on user toggles. |
| **App Management** | WinForms `NotifyIcon` + Control Panel Window | Native system tray icon and Control Center providing toggles (Enable/Disable Dock, Enable/Disable Notch, Settings, Exit). |
| **Configuration** | JSON via `System.Text.Json` | Stored under `%LOCALAPPDATA%\CustomDesktop\settings.json`. Zero external database overhead. |
| **Development Setup** | Hybrid WSL2 + Windows Host | Source files in WSL2; compilation, debugging, and execution run via Windows host .NET SDK. |

---

## 3. Modularity & Feature Toggle Architecture

The application runs as a single background process managing two independent desktop window controllers and an Admin / Control Panel.

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
       │  - Sizing, Gap & Opacity  │                 │
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
│  - Auto-Resizing │  │                  │
│  - Window Picker │  │                  │
│  - Context Menu  │  │                  │
└──────────────────┘  └──────────────────┘
```

---

## 4. Window Behavior & Low-Level Specifications

Both `DockWindow` and `NotchWindow` adhere to the following desktop window constraints:

1. **Non-Focus-Stealing (`WS_EX_NOACTIVATE = 0x08000000`):** Interacting with the dock or notch never steals keyboard focus from active games, editors, or fullscreen tools. `WM_MOUSEACTIVATE` returns `MA_NOACTIVATE`.
2. **Task Switcher Exclusion (`WS_EX_TOOLWINDOW = 0x00000080`):** Neither overlay window appears in `Alt + Tab` or Task View.
3. **Z-Ordering (`Topmost`):** Overlay windows stay pinned topmost on the desktop layer.
4. **DPI & Multi-Monitor Support:** Declared `PerMonitorV2` in `app.manifest`.
5. **Decoupled Background & Content Layers:** Background pill border opacity is decoupled from foreground content so transparency does not wash out icons or text.
6. **Center-Anchored Responsive Placement:** Windows calculate horizontal center coordinates dynamically relative to the primary monitor (`(PrimaryScreenWidth - CurrentWidth) / 2`).

---

## 5. Feature Specifications

### 5.1 Floating Dock

* **Master Toggle:** `Dock.Enabled` (true/false).
* **Positioning:** Centered horizontally along the bottom edge of the selected display (`PrimaryScreenHeight - Height - MarginFromScreen`).
* **Dimensions & Spacing:**
  * `IconSize`: 16px to 72px (default 48px). Ultra-compact mode supported down to 16px.
  * `ItemGap`: 2px to 24px (default 8px).
  * `MarginFromScreen`: 0px to 48px (default 10px).
  * `Opacity` (Background Only): 10% to 100% (default 85%).
  * `CornerRadius`: 0px to 36px (default 24px).
* **Dynamic Auto-Resizing & Center Anchoring:**
  * The dock width automatically adapts to the number of pinned and running items: `CalculatedWidth = (Items.Count * (IconSize + ItemGap)) + HorizontalPadding`.
  * When an unpinned application opens or closes, `DockWindow` animates both `Width` and `Left` simultaneously using smooth `CubicEase` transitions, symmetrically expanding and contracting outwards and inwards from the center.
* **Native Windows Taskbar Interaction Model & Icon Ordering:**
  * **Icon Order:**
    * Pinned applications appear first (indices `0 .. PinnedCount - 1`), discovering user shortcuts directly from Windows Taskbar (`%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`).
    * Unpinned running applications are appended strictly after pinned apps in stable chronological launch order. Their positions never reshuffle upon switching focus.
  * **Multi-Window Sign & Selection Picker:**
    * Single window running: Single blue indicator dot (`•`).
    * 2+ windows running: Dual blue indicator dots (`• •`).
    * Clicking an app with multiple windows restores both windows and opens a Windows 11 Fluent Window Selection Picker directly above the dock icon. Users see cards with live titles, icons, and individual close buttons `✕`, clicking any card focuses that exact window.
  * **Click-to-Focus / Click-to-Minimize Toggle:**
    * If single window is in background or minimized, clicking restores (`SW_RESTORE`) and brings it to foreground (`ForceSetForegroundWindow`).
    * If single window is already the active foreground window, clicking minimizes it (`SW_MINIMIZE`).
  * **Middle-Click New Instance:**
    * Middle-clicking (wheel click) an icon spawns a new process instance.
  * **Shift + Left-Click New Instance:**
    * Shift-clicking an icon spawns a new process instance.
  * **Right-Click Fluent Context Menu:**
    * App title header with high-resolution icon.
    * Window Controls (when running): Close Window (`WM_CLOSE`), Close All Windows (for multi-window apps), Minimize / Restore.
    * Launch Controls: New Window / Launch, Run as Administrator (`Verb = "runas"`).
    * Pinning Controls: Pin to Dock / Unpin from Dock (saved directly to `settings.json`).
* **Real-Time App Discovery:**
  * `RunningAppsService` continuously listens to Win32 window creation and destruction hooks (`SetWinEventHook`) alongside high-speed polling (500ms) to ensure instantaneous detection of newly opened and closed windows.
* **Taskbar Auto-Hide:** Hides `Shell_TrayWnd` and all `Shell_SecondaryTrayWnd` when dock is active.
* **Running Indicators:** Dot indicators beneath active apps; clicking an active app restores its window (`SetForegroundWindow`).
* **Path Resolution:** Resolves full paths for pinned apps via `System32`, `Windows`, `App Paths`, and `%PATH%` to guarantee valid icons and eliminate ghost gaps.

---

### 5.2 Interactive Notch

* **Master Toggle:** `Notch.Enabled` (true/false).
* **Compact Mode (Pill):** Displays current time, compact date, and minimal system badges.
* **Dimensions & Spacing:**
  * `WidthCompact`: 140px to 360px (default 220px).
  * `HeightCompact`: 28px to 56px (default 36px).
  * `MarginFromScreen`: 0px to 48px (default 0px / flush with screen top).
  * `Opacity` (Background Only): 10% to 100% (default 92%).
  * `CornerRadius`: 0px to 28px (default 16px).
* **Expanded Mode:** Triggered via click (or optional hover) to expose media controls, volume slider, and toggles.

---

## 6. Implementation Milestones

```
[Milestone 1: App Lifecycle, Control Panel & Customization] (CURRENT MILESTONE)
  ├── Admin / Control Panel Window on Startup (Spacious 2-Row Layout)
  ├── System Tray NotifyIcon Setup (Context Menu: Dock, Notch, Control Panel, Exit)
  ├── Dynamic Window Instantiation / Teardown Logic (Overlays mount/unmount reactively)
  ├── Background-Only Opacity & Pure Rounded Pill Geometries
  ├── Windows Taskbar Auto-Hide & Taskbar-Level Dock Placement
  ├── App Gap Setting & Path-Resolved Equal Spacing
  ├── Dynamic Dock Auto-Resizing & Center-Anchored Smooth Transitions
  ├── Native Windows Taskbar Click Model (Focus/Minimize Toggle, Multi-Window Cycle, Middle-Click New Instance)
  ├── Windows Taskbar Icon Order & Right-Click Context Menu (Close, Admin, Pin/Unpin)
  ├── Multi-Window Visual Sign (• •) & Interactive Window Selection Picker
  ├── Ultra-Compact Icon Sizing (down to 16px)
  ├── Application Icon (app.ico) & Dock Display for CustomDesktop
  ├── Resilient Window Icon Initialization & BAML Parser TypeConverter Guard
  └── Settings Engine (JSON serialization under %LOCALAPPDATA%)

[Milestone 2: Enhanced Floating Dock Mechanics]
  ├── Pinned Apps Model & Drag-and-Drop Reordering
  ├── Dynamic Running Dots & Process Scanning Optimization
  └── Auto-Hide Smooth Slide Animations

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
  ├── Auto-Start with Windows Registry Run key integration
  └── Multi-Monitor & Mixed DPI Scaling Verification
```

---

## 7. Data Contract: `AppSettings.cs` Reference

```json
{
  "Version": "1.9.0",
  "Dock": {
    "Enabled": true,
    "IconSize": 48,
    "ItemGap": 8,
    "MarginFromScreen": 10,
    "Opacity": 0.85,
    "CornerRadius": 24,
    "AutoHide": false,
    "AutoHideDelayMs": 300,
    "ShowRunningIndicators": true,
    "HideWindowsTaskbar": true,
    "TargetDisplayIndex": 0,
    "BackdropType": "Acrylic",
    "PinnedApps": [
      {
        "Name": "File Explorer",
        "ExecutablePath": "explorer.exe",
        "Arguments": ""
      },
      {
        "Name": "Google Chrome",
        "ExecutablePath": "chrome.exe",
        "Arguments": ""
      },
      {
        "Name": "OpenVPN Connect",
        "ExecutablePath": "OpenVPNConnect.exe",
        "Arguments": ""
      }
    ]
  },
  "Notch": {
    "Enabled": true,
    "WidthCompact": 220,
    "HeightCompact": 36,
    "MarginFromScreen": 0,
    "Opacity": 0.92,
    "CornerRadius": 16,
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
