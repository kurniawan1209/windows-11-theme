using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomDesktop.Services;

public class WifiNetworkItem : INotifyPropertyChanged
{
    private string _ssid = string.Empty;
    private int _signalQuality;
    private string _security = "Secured";
    private bool _isConnected;
    private bool _hasSavedProfile;
    private bool _isPasswordPromptOpen;
    private bool _isConnecting;
    private string? _errorMessage;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    public string Ssid
    {
        get => _ssid;
        set { _ssid = value; OnPropertyChanged(); }
    }

    public int SignalQuality
    {
        get => _signalQuality;
        set { _signalQuality = value; OnPropertyChanged(); OnPropertyChanged(nameof(SignalGlyph)); }
    }

    public string Security
    {
        get => _security;
        set
        {
            _security = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SecurityGlyph));
            OnPropertyChanged(nameof(NeedsPassword));
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConnect)); OnPropertyChanged(nameof(CanDisconnect)); }
    }

    public bool HasSavedProfile
    {
        get => _hasSavedProfile;
        set { _hasSavedProfile = value; OnPropertyChanged(); OnPropertyChanged(nameof(NeedsPassword)); }
    }

    public bool IsPasswordPromptOpen
    {
        get => _isPasswordPromptOpen;
        set { _isPasswordPromptOpen = value; OnPropertyChanged(); }
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        set { _isConnecting = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public bool NeedsPassword => !HasSavedProfile && Security != "Open";

    public bool CanConnect => !IsConnected && !IsConnecting;

    public bool CanDisconnect => IsConnected && !IsConnecting;

    public string SignalGlyph => SignalQuality switch
    {
        >= 75 => "\uE701",
        >= 50 => "\uE872",
        >= 25 => "\uE873",
        >= 5 => "\uE874",
        _ => "\uEB55"
    };

    public string SecurityGlyph => Security == "Open" ? "\uE785" : "\uE72E";
}

public class WifiService : IDisposable
{
    private IntPtr _clientHandle = IntPtr.Zero;
    private bool _isDisposed;

    public WifiService()
    {
        try
        {
            uint negotiatedVersion;
            WlanOpenHandle(2, IntPtr.Zero, out negotiatedVersion, out _clientHandle);
        }
        catch
        {
            _clientHandle = IntPtr.Zero;
        }
    }

    public HashSet<string> GetSavedProfiles()
    {
        var profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show profiles",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                foreach (var line in output.Split('\n'))
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx >= 0 && line.Contains("Profile"))
                    {
                        string name = line[(colonIdx + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            profiles.Add(name);
                        }
                    }
                }
            }
        }
        catch { }
        return profiles;
    }

    public List<WifiNetworkItem> ScanAvailableNetworks()
    {
        var result = new List<WifiNetworkItem>();
        if (_clientHandle == IntPtr.Zero) return result;

        var savedProfiles = GetSavedProfiles();

        try
        {
            IntPtr pInterfaceList = IntPtr.Zero;
            if (WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out pInterfaceList) != 0 || pInterfaceList == IntPtr.Zero)
            {
                return result;
            }

            try
            {
                var interfaceList = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(pInterfaceList);
                long currentPtr = pInterfaceList.ToInt64() + Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo").ToInt64();

                var seenSsids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < interfaceList.dwNumberOfItems; i++)
                {
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(new IntPtr(currentPtr));
                    currentPtr += Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                    IntPtr pAvailableNetworkList = IntPtr.Zero;
                    if (WlanGetAvailableNetworkList(_clientHandle, ref info.InterfaceGuid, 0x00000002, IntPtr.Zero, out pAvailableNetworkList) == 0 &&
                        pAvailableNetworkList != IntPtr.Zero)
                    {
                        try
                        {
                            var networkList = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK_LIST>(pAvailableNetworkList);
                            long netPtr = pAvailableNetworkList.ToInt64() + Marshal.OffsetOf<WLAN_AVAILABLE_NETWORK_LIST>("Network").ToInt64();

                            for (int j = 0; j < networkList.dwNumberOfItems; j++)
                            {
                                var network = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(new IntPtr(netPtr));
                                netPtr += Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();

                                string ssid = network.dot11Ssid.ToString();
                                if (!string.IsNullOrWhiteSpace(ssid) && !seenSsids.Contains(ssid))
                                {
                                    seenSsids.Add(ssid);

                                    bool isConnected = (network.dwFlags & 0x00000001) != 0; // WLAN_AVAILABLE_NETWORK_CONNECTED
                                    string sec = network.dot11DefaultAuthAlgorithm switch
                                    {
                                        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN => "Open",
                                        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA_PSK => "WPA2-PSK",
                                        _ => "Secured"
                                    };

                                    result.Add(new WifiNetworkItem
                                    {
                                        Ssid = ssid,
                                        SignalQuality = (int)network.wlanSignalQuality,
                                        Security = sec,
                                        IsConnected = isConnected,
                                        HasSavedProfile = savedProfiles.Contains(ssid)
                                    });
                                }
                            }
                        }
                        finally
                        {
                            WlanFreeMemory(pAvailableNetworkList);
                        }
                    }
                }
            }
            finally
            {
                WlanFreeMemory(pInterfaceList);
            }
        }
        catch
        {
            // Fallback gracefully
        }

        // Sort connected first, then by signal quality
        result.Sort((a, b) =>
        {
            if (a.IsConnected && !b.IsConnected) return -1;
            if (!a.IsConnected && b.IsConnected) return 1;
            return b.SignalQuality.CompareTo(a.SignalQuality);
        });

        return result;
    }

    public async Task<bool> DisconnectAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan disconnect",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<(bool Success, string Message)> ConnectAsync(string ssid)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"wlan connect name=\"{ssid}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit(5000);

                if (output.Contains("completed successfully", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Connection request was completed", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, "Connected successfully.");
                }

                return (false, string.IsNullOrWhiteSpace(output) ? "Could not connect to network." : output.Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });
    }

    public async Task<(bool Success, string Message)> ConnectWithPasswordAsync(string ssid, string password)
    {
        return await Task.Run(() =>
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"wifi_{Guid.NewGuid():N}.xml");
            try
            {
                string hexSsid = Convert.ToHexString(Encoding.UTF8.GetBytes(ssid));
                string xml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{SecurityElement.Escape(ssid)}</name>
    <SSIDConfig>
        <SSID>
            <hex>{hexSsid}</hex>
            <name>{SecurityElement.Escape(ssid)}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{SecurityElement.Escape(password)}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";

                File.WriteAllText(tempFile, xml, Encoding.UTF8);

                // Register profile
                var addPsi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"wlan add profile filename=\"{tempFile}\" user=all",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var addProc = Process.Start(addPsi))
                {
                    addProc?.WaitForExit(4000);
                }

                // Connect using new profile
                var connPsi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"wlan connect name=\"{ssid}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var connProc = Process.Start(connPsi);
                string connOut = connProc?.StandardOutput.ReadToEnd() ?? "";
                connProc?.WaitForExit(5000);

                // Poll for connection confirmation
                for (int i = 0; i < 7; i++)
                {
                    Thread.Sleep(1000);
                    if (IsCurrentlyConnectedTo(ssid))
                    {
                        return (true, "Connected successfully.");
                    }
                }

                return (true, "Connection request completed.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
                catch { }
            }
        });
    }

    private bool IsCurrentlyConnectedTo(string ssid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            bool hasSsid = false;
            bool isConnected = false;

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("SSID") && !line.Contains("BSSID") && line.Contains(ssid, StringComparison.OrdinalIgnoreCase))
                {
                    hasSsid = true;
                }
                if (line.Contains("State") && line.Contains("connected", StringComparison.OrdinalIgnoreCase))
                {
                    isConnected = true;
                }
            }

            return hasSsid && isConnected;
        }
        catch
        {
            return false;
        }
    }

    public void OpenNetworkFlyout()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:network") { UseShellExecute = true });
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_clientHandle != IntPtr.Zero)
            {
                try
                {
                    WlanCloseHandle(_clientHandle, IntPtr.Zero);
                }
                catch { }
                _clientHandle = IntPtr.Zero;
            }
        }
        GC.SuppressFinalize(this);
    }

    #region Native WLAN Interop

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        uint dwFlags,
        IntPtr pReserved,
        out IntPtr ppAvailableNetworkList);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public uint isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_INTERFACE_INFO_LIST
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        public WLAN_INTERFACE_INFO InterfaceInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;

        public override string ToString()
        {
            if (uSSIDLength == 0 || ucSSID == null) return string.Empty;
            int len = (int)Math.Min(uSSIDLength, 32);
            return Encoding.UTF8.GetString(ucSSID, 0, len);
        }
    }

    private enum DOT11_AUTH_ALGORITHM
    {
        DOT11_AUTH_ALGO_80211_OPEN = 1,
        DOT11_AUTH_ALGO_80211_SHARED_KEY = 2,
        DOT11_AUTH_ALGO_WPA = 3,
        DOT11_AUTH_ALGO_WPA_PSK = 4,
        DOT11_AUTH_ALGO_WPA_NONE = 5,
        DOT11_AUTH_ALGO_RSNA = 6,
        DOT11_AUTH_ALGO_RSNA_PSK = 7
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;
        public uint uNumberOfBssids;
        public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] dot11PhyTypes;
        public bool bMorePhyTypes;
        public uint wlanSignalQuality;
        public bool bSecurityEnabled;
        public DOT11_AUTH_ALGORITHM dot11DefaultAuthAlgorithm;
        public uint dot11DefaultCipherAlgorithm;
        public uint dwFlags;
        public uint dwReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_AVAILABLE_NETWORK_LIST
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        public WLAN_AVAILABLE_NETWORK Network;
    }

    #endregion
}
