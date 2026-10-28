using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CustomDesktop.Core.Models;

public class AppSettings
{
    [JsonPropertyName("Version")]
    public string Version { get; set; } = "1.10.0";

    [JsonPropertyName("Dock")]
    public DockSettings Dock { get; set; } = new();

    [JsonPropertyName("Notch")]
    public NotchSettings Notch { get; set; } = new();

    [JsonPropertyName("System")]
    public SystemSettings System { get; set; } = new();
}

public class DockSettings
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("IconSize")]
    public int IconSize { get; set; } = 48;

    [JsonPropertyName("ItemGap")]
    public int ItemGap { get; set; } = 8;

    [JsonPropertyName("MarginFromScreen")]
    public int MarginFromScreen { get; set; } = 10;

    [JsonPropertyName("Opacity")]
    public double Opacity { get; set; } = 0.85;

    [JsonPropertyName("CornerRadius")]
    public double CornerRadius { get; set; } = 24.0;

    [JsonPropertyName("AutoHide")]
    public bool AutoHide { get; set; } = false;

    [JsonPropertyName("AutoHideDelayMs")]
    public int AutoHideDelayMs { get; set; } = 300;

    [JsonPropertyName("ShowRunningIndicators")]
    public bool ShowRunningIndicators { get; set; } = true;

    [JsonPropertyName("HideWindowsTaskbar")]
    public bool HideWindowsTaskbar { get; set; } = true;

    [JsonPropertyName("TargetDisplayIndex")]
    public int TargetDisplayIndex { get; set; } = 0;

    [JsonPropertyName("BackdropType")]
    public string BackdropType { get; set; } = "Acrylic";

    [JsonPropertyName("PinnedApps")]
    public List<PinnedAppConfig> PinnedApps { get; set; } = new();
}

public class PinnedAppConfig
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ExecutablePath")]
    public string ExecutablePath { get; set; } = string.Empty;

    [JsonPropertyName("Arguments")]
    public string Arguments { get; set; } = string.Empty;
}

public class NotchSettings
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("WidthCompact")]
    public int WidthCompact { get; set; } = 220;

    [JsonPropertyName("HeightCompact")]
    public int HeightCompact { get; set; } = 36;

    [JsonPropertyName("MarginFromScreen")]
    public int MarginFromScreen { get; set; } = 0;

    [JsonPropertyName("Opacity")]
    public double Opacity { get; set; } = 0.92;

    [JsonPropertyName("CornerRadius")]
    public double CornerRadius { get; set; } = 16.0;

    [JsonPropertyName("WidthExpanded")]
    public int WidthExpanded { get; set; } = 420;

    [JsonPropertyName("HeightExpanded")]
    public int HeightExpanded { get; set; } = 240;

    [JsonPropertyName("ShowClock")]
    public bool ShowClock { get; set; } = true;

    [JsonPropertyName("ShowBattery")]
    public bool ShowBattery { get; set; } = true;

    [JsonPropertyName("ShowSound")]
    public bool ShowSound { get; set; } = true;

    [JsonPropertyName("ShowNetwork")]
    public bool ShowNetwork { get; set; } = true;

    [JsonPropertyName("ShowBluetooth")]
    public bool ShowBluetooth { get; set; } = true;

    [JsonPropertyName("ShowMedia")]
    public bool ShowMedia { get; set; } = true;

    [JsonPropertyName("ExpandOnHover")]
    public bool ExpandOnHover { get; set; } = false;

    [JsonPropertyName("BackdropType")]
    public string BackdropType { get; set; } = "Mica";
}

public class SystemSettings
{
    [JsonPropertyName("EnableAnimations")]
    public bool EnableAnimations { get; set; } = true;

    [JsonPropertyName("LaunchOnStartup")]
    public bool LaunchOnStartup { get; set; } = false;

    [JsonPropertyName("CloseToTray")]
    public bool CloseToTray { get; set; } = true;
}
