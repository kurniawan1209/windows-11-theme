using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CustomDesktop.Services;

public class BatteryService : IDisposable
{
    private bool _isDisposed;

    public event Action? BatteryStateChanged;

    public bool HasBattery { get; private set; }
    public int BatteryPercent { get; private set; }
    public bool IsCharging { get; private set; }
    public bool IsFullyCharged { get; private set; }

    public BatteryService()
    {
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch { }

        Refresh();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        Refresh();
    }

    public void Refresh()
    {
        try
        {
            var power = SystemInformation.PowerStatus;

            // Check if system has a real battery
            bool noBattery = power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) ||
                             power.BatteryLifePercent > 1.0f;

            HasBattery = !noBattery;

            if (HasBattery)
            {
                BatteryPercent = (int)Math.Round(Math.Clamp(power.BatteryLifePercent, 0f, 1f) * 100);
                IsCharging = power.PowerLineStatus == PowerLineStatus.Online ||
                             power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging);
                IsFullyCharged = IsCharging && BatteryPercent >= 99;
            }
            else
            {
                BatteryPercent = 100;
                IsCharging = power.PowerLineStatus == PowerLineStatus.Online;
                IsFullyCharged = true;
            }

            BatteryStateChanged?.Invoke();
        }
        catch
        {
            HasBattery = false;
            BatteryPercent = 100;
            IsCharging = true;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            try
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
            catch { }
        }
        GC.SuppressFinalize(this);
    }
}
