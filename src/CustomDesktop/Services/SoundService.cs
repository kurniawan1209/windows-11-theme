using System;
using System.Runtime.InteropServices;

namespace CustomDesktop.Services;

public class SoundService : IDisposable
{
    private IAudioEndpointVolume? _audioEndpointVolume;
    private AudioEndpointVolumeCallback? _callback;
    private bool _isDisposed;

    public event Action<float, bool>? VolumeChanged;

    public float MasterVolume
    {
        get
        {
            if (_audioEndpointVolume == null) return 0.5f;
            try
            {
                _audioEndpointVolume.GetMasterVolumeLevelScalar(out float volume);
                return volume;
            }
            catch
            {
                return 0.5f;
            }
        }
        set
        {
            if (_audioEndpointVolume == null) return;
            try
            {
                float clamped = Math.Clamp(value, 0f, 1f);
                _audioEndpointVolume.SetMasterVolumeLevelScalar(clamped, Guid.Empty);
            }
            catch
            {
                // Ignore volume set failures
            }
        }
    }

    public bool IsMuted
    {
        get
        {
            if (_audioEndpointVolume == null) return false;
            try
            {
                _audioEndpointVolume.GetMute(out bool isMuted);
                return isMuted;
            }
            catch
            {
                return false;
            }
        }
        set
        {
            if (_audioEndpointVolume == null) return;
            try
            {
                _audioEndpointVolume.SetMute(value, Guid.Empty);
            }
            catch
            {
                // Ignore mute set failures
            }
        }
    }

    public SoundService()
    {
        InitializeAudio();
    }

    private void InitializeAudio()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            if (device != null)
            {
                Guid iidIAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iidIAudioEndpointVolume, CLSCTX.CLSCTX_ALL, IntPtr.Zero, out var endpoint);
                _audioEndpointVolume = endpoint as IAudioEndpointVolume;

                if (_audioEndpointVolume != null)
                {
                    _callback = new AudioEndpointVolumeCallback(this);
                    _audioEndpointVolume.RegisterControlChangeNotify(_callback);
                }
            }
        }
        catch
        {
            // CoreAudio might fail in headless or containerized environments
            _audioEndpointVolume = null;
        }
    }

    internal void OnNotification(float newVolume, bool isMuted)
    {
        VolumeChanged?.Invoke(newVolume, isMuted);
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_audioEndpointVolume != null && _callback != null)
            {
                try
                {
                    _audioEndpointVolume.UnregisterControlChangeNotify(_callback);
                }
                catch { }
            }
            _audioEndpointVolume = null;
            _callback = null;
        }
        GC.SuppressFinalize(this);
    }

    #region COM Interfaces

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    private enum EDataFlow { eRender, eCapture, eAll }
    private enum ERole { eConsole, eMultimedia, eCommunications }

    [Flags]
    private enum CLSCTX
    {
        CLSCTX_INPROC_SERVER = 0x1,
        CLSCTX_INPROC_HANDLER = 0x2,
        CLSCTX_LOCAL_SERVER = 0x4,
        CLSCTX_ALL = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("657804FA-D6AD-4496-8560-E5D522304CD9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig]
        int OnNotify(IntPtr pNotify);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig]
        int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig]
        int GetChannelCount(out uint pnChannelCount);
        [PreserveSig]
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
        [PreserveSig]
        int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig]
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig]
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig]
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig]
        int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig]
        int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig]
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig]
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        public bool bMuted;
        public float fMasterVolume;
        public uint nChannels;
        public float fChannelVolume;
    }

    private class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly SoundService _parent;

        public AudioEndpointVolumeCallback(SoundService parent)
        {
            _parent = parent;
        }

        public int OnNotify(IntPtr pNotify)
        {
            if (pNotify != IntPtr.Zero)
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotify);
                _parent.OnNotification(data.fMasterVolume, data.bMuted);
            }
            return 0;
        }
    }

    #endregion
}
