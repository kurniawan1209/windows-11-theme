using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace CustomDesktop.Services;

public class BluetoothDeviceItem : INotifyPropertyChanged
{
    private string _name = "Unknown Device";
    private string _id = string.Empty;
    private string _deviceType = "Peripheral";
    private bool _isConnected;
    private bool _isPaired;
    private ulong _bluetoothAddress;
    private bool _isBusy;
    private string? _errorMessage;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string DeviceType
    {
        get => _deviceType;
        set { _deviceType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeviceIcon)); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
        }
    }

    public bool IsPaired
    {
        get => _isPaired;
        set { _isPaired = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public ulong BluetoothAddress
    {
        get => _bluetoothAddress;
        set { _bluetoothAddress = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public bool CanConnect => !IsConnected && !IsBusy;

    public bool CanDisconnect => IsConnected && !IsBusy;

    public string DeviceIcon => DeviceType switch
    {
        "Audio" => "🎧",
        "Mouse" => "🖱️",
        "Keyboard" => "⌨️",
        "Phone" => "📱",
        _ => "󰂯"
    };

    public string StatusText
    {
        get
        {
            if (IsBusy) return "Working...";
            if (IsConnected) return "Connected";
            if (IsPaired) return "Paired";
            return "Available";
        }
    }
}

public class BluetoothService : IDisposable
{
    private bool _isDisposed;

    private static readonly Regex MacRegex = new Regex(@"(?:DEV_|DeviceAddress_|Bluetooth.*?)([0-9a-fA-F]{12})", RegexOptions.Compiled);

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
                if (nameLower.Contains("headset") || nameLower.Contains("headphone") || nameLower.Contains("earbud") ||
                    nameLower.Contains("speaker") || nameLower.Contains("buds") || nameLower.Contains("airpods") ||
                    nameLower.Contains("bass") || nameLower.Contains("clip") || nameLower.Contains("studio") || nameLower.Contains("vs12"))
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

                ulong address = 0;
                var match = MacRegex.Match(devInfo.Id);
                if (match.Success && ulong.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsedMac))
                {
                    address = parsedMac;
                }

                devices.Add(new BluetoothDeviceItem
                {
                    Id = devInfo.Id,
                    Name = devInfo.Name,
                    DeviceType = type,
                    IsConnected = isConnected,
                    IsPaired = devInfo.Pairing?.IsPaired ?? true,
                    BluetoothAddress = address
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

    public async Task<(bool Success, string Message)> ConnectDeviceAsync(BluetoothDeviceItem item)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var devInfo = await DeviceInformation.CreateFromIdAsync(item.Id);
                if (devInfo == null)
                {
                    return (false, "Device not found.");
                }

                // If device has custom pairing, trigger confirmation reconnect
                if (devInfo.Pairing != null && devInfo.Pairing.CanPair)
                {
                    var customPairing = devInfo.Pairing.Custom;
                    if (customPairing != null)
                    {
                        void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
                        {
                            args.Accept();
                        }

                        customPairing.PairingRequested += OnPairingRequested;
                        try
                        {
                            var pairResult = await customPairing.PairAsync(DevicePairingKinds.ConfirmOnly, DevicePairingProtectionLevel.None);
                        }
                        finally
                        {
                            customPairing.PairingRequested -= OnPairingRequested;
                        }
                    }
                }

                // Also instantiate BluetoothDevice to wake up classic / RFCOMM / Audio connection
                try
                {
                    using var bt = await BluetoothDevice.FromIdAsync(item.Id);
                    if (bt != null)
                    {
                        var rfcomm = await bt.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
                    }
                }
                catch { }

                // Wait briefly for connection state to propagate
                for (int i = 0; i < 4; i++)
                {
                    await Task.Delay(500);
                    var updated = await DeviceInformation.CreateFromIdAsync(item.Id);
                    if (updated != null && updated.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var cVal) && cVal is bool c && c)
                    {
                        return (true, "Connected successfully.");
                    }
                }

                return (true, "Connection initiated.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });
    }

    public async Task<(bool Success, string Message)> DisconnectDeviceAsync(BluetoothDeviceItem item)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (item.BluetoothAddress != 0)
                {
                    long mac = (long)item.BluetoothAddress;
                    bool disconnected = DisconnectClassicDevice(mac);
                    if (disconnected)
                    {
                        return (true, "Disconnected successfully.");
                    }
                }

                // Fallback attempt: disable / enable PnP device node for this item
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"Get-PnpDevice -Class Bluetooth | Where-Object {{ $_.FriendlyName -like '*{item.Name}*' }} | Disable-PnpDevice -Confirm:$false; Start-Sleep -Milliseconds 500; Get-PnpDevice -Class Bluetooth | Where-Object {{ $_.FriendlyName -like '*{item.Name}*' }} | Enable-PnpDevice -Confirm:$false\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                }
                catch { }

                return (true, "Disconnected successfully.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });
    }

    private static bool DisconnectClassicDevice(long macAddress)
    {
        try
        {
            var findParams = new BLUETOOTH_FIND_RADIO_PARAMS
            {
                dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS))
            };

            IntPtr radioFindHandle = BluetoothFindFirstRadio(ref findParams, out IntPtr radioHandle);
            if (radioFindHandle == IntPtr.Zero || radioHandle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                int bytesReturned = 0;
                bool success = DeviceIoControl(
                    radioHandle,
                    IOCTL_BTH_DISCONNECT_DEVICE,
                    ref macAddress,
                    sizeof(long),
                    IntPtr.Zero,
                    0,
                    out bytesReturned,
                    IntPtr.Zero);

                return success;
            }
            finally
            {
                CloseHandle(radioHandle);
                BluetoothFindRadioClose(radioFindHandle);
            }
        }
        catch
        {
            return false;
        }
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

    #region Win32 Radio Control P/Invoke

    private const uint IOCTL_BTH_DISCONNECT_DEVICE = 0x41000c;

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public uint dwSize;
    }

    [DllImport("irprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(
        ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp,
        out IntPtr phRadio);

    [DllImport("irprops.cpl", SetLastError = true)]
    private static extern bool BluetoothFindRadioClose(IntPtr hFind);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        ref long InBuffer,
        int nInBufferSize,
        IntPtr OutBuffer,
        int nOutBufferSize,
        out int pBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    #endregion
}
