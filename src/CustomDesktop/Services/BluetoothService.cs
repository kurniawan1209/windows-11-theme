using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace CustomDesktop.Services;

public class BluetoothDeviceItem
{
    public string Name { get; set; } = "Unknown Device";
    public string Id { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "Peripheral";
    public bool IsConnected { get; set; }
    public bool IsPaired { get; set; }

    public string DeviceIcon => DeviceType switch
    {
        "Audio" => "🎧",
        "Mouse" => "🖱️",
        "Keyboard" => "⌨️",
        "Phone" => "📱",
        _ => "󰂯"
    };

    public string StatusText => IsConnected ? "Connected" : (IsPaired ? "Paired" : "Available");
}

public class BluetoothService : IDisposable
{
    private bool _isDisposed;

    public async Task<List<BluetoothDeviceItem>> GetDevicesAsync()
    {
        var devices = new List<BluetoothDeviceItem>();

        try
        {
            // Query paired Bluetooth devices
            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devInfoCollection = await DeviceInformation.FindAllAsync(selector);

            foreach (var devInfo in devInfoCollection)
            {
                if (string.IsNullOrWhiteSpace(devInfo.Name)) continue;

                string type = "Peripheral";
                string nameLower = devInfo.Name.ToLowerInvariant();
                if (nameLower.Contains("headset") || nameLower.Contains("headphone") || nameLower.Contains("earbud") || nameLower.Contains("speaker") || nameLower.Contains("buds") || nameLower.Contains("airpods"))
                {
                    type = "Audio";
                }
                else if (nameLower.Contains("mouse") || nameLower.Contains("trackpad"))
                {
                    type = "Mouse";
                }
                else if (nameLower.Contains("keyboard"))
                {
                    type = "Keyboard";
                }
                else if (nameLower.Contains("phone") || nameLower.Contains("iphone") || nameLower.Contains("galaxy"))
                {
                    type = "Phone";
                }

                bool isConnected = false;
                if (devInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connectedVal) && connectedVal is bool c)
                {
                    isConnected = c;
                }

                devices.Add(new BluetoothDeviceItem
                {
                    Id = devInfo.Id,
                    Name = devInfo.Name,
                    DeviceType = type,
                    IsConnected = isConnected,
                    IsPaired = devInfo.Pairing?.IsPaired ?? true
                });
            }
        }
        catch
        {
            // Bluetooth might be disabled or unavailable
        }

        // Sort connected devices first
        devices.Sort((a, b) =>
        {
            if (a.IsConnected && !b.IsConnected) return -1;
            if (!a.IsConnected && b.IsConnected) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return devices;
    }

    public void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
