using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CustomDesktop.Services;

public class WifiNetworkItem
{
    public string Ssid { get; set; } = string.Empty;
    public int SignalQuality { get; set; }
    public string Security { get; set; } = "Secured";
    public bool IsConnected { get; set; }

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

    public List<WifiNetworkItem> ScanAvailableNetworks()
    {
        var result = new List<WifiNetworkItem>();
        if (_clientHandle == IntPtr.Zero) return result;

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
                                        IsConnected = isConnected
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
