using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace CustomDesktop.Services;

public class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private bool _isDisposed;

    public event Action? MediaPropertiesChanged;
    public event Action? PlaybackStatusChanged;

    public string Title { get; private set; } = "No media playing";
    public string Artist { get; private set; } = "Custom Desktop";
    public ImageSource? Thumbnail { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool HasActiveSession => _currentSession != null;

    public bool IsSpotifyRunning
    {
        get
        {
            try
            {
                return Process.GetProcessesByName("Spotify").Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public MediaService()
    {
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                UpdateCurrentSession();
            }
        }
        catch
        {
            // GSMTC might not be accessible in non-interactive/restricted contexts
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession();
    }

    private void UpdateCurrentSession()
    {
        if (_sessionManager == null) return;

        try
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            // Prioritize Spotify session if available
            var sessions = _sessionManager.GetSessions();
            GlobalSystemMediaTransportControlsSession? selectedSession = null;

            foreach (var session in sessions)
            {
                if (session.SourceAppUserModelId != null &&
                    session.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                {
                    selectedSession = session;
                    break;
                }
            }

            _currentSession = selectedSession ?? _sessionManager.GetCurrentSession();

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _ = RefreshMediaPropertiesAsync();
                RefreshPlaybackInfo();
            }
            else
            {
                Title = IsSpotifyRunning ? "Spotify is idle" : "No media playing";
                Artist = IsSpotifyRunning ? "Spotify" : "Custom Desktop";
                Thumbnail = null;
                IsPlaying = false;
                MediaPropertiesChanged?.Invoke();
                PlaybackStatusChanged?.Invoke();
            }
        }
        catch
        {
            // Session access guard
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshMediaPropertiesAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        RefreshPlaybackInfo();
    }

    public async Task RefreshMediaPropertiesAsync()
    {
        if (_currentSession == null) return;

        try
        {
            var props = await _currentSession.TryGetMediaPropertiesAsync();
            if (props != null)
            {
                Title = !string.IsNullOrWhiteSpace(props.Title) ? props.Title : "Unknown Title";
                Artist = !string.IsNullOrWhiteSpace(props.Artist) ? props.Artist : (!string.IsNullOrWhiteSpace(props.AlbumArtist) ? props.AlbumArtist : "Unknown Artist");

                if (props.Thumbnail != null)
                {
                    Thumbnail = await LoadThumbnailAsync(props.Thumbnail);
                }
                else
                {
                    Thumbnail = null;
                }

                MediaPropertiesChanged?.Invoke();
            }
        }
        catch
        {
            // Ignore temporary WinRT RPC errors
        }
    }

    private void RefreshPlaybackInfo()
    {
        if (_currentSession == null) return;

        try
        {
            var playback = _currentSession.GetPlaybackInfo();
            if (playback != null)
            {
                IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                PlaybackStatusChanged?.Invoke();
            }
        }
        catch { }
    }

    private static async Task<ImageSource?> LoadThumbnailAsync(IRandomAccessStreamReference thumbnailRef)
    {
        try
        {
            using var stream = await thumbnailRef.OpenReadAsync();
            using var netStream = stream.AsStreamForRead();

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = netStream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TryTogglePlayPauseAsync();
            }
            catch { }
        }
    }

    public async Task NextAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TrySkipNextAsync();
            }
            catch { }
        }
    }

    public async Task PreviousAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TrySkipPreviousAsync();
            }
            catch { }
        }
    }

    public void LaunchSpotify()
    {
        try
        {
            // 1. Try launching via Spotify URI scheme
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                // 2. Try launching via AppData default executable path
                string appDataSpotify = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");
                if (File.Exists(appDataSpotify))
                {
                    Process.Start(new ProcessStartInfo(appDataSpotify) { UseShellExecute = true });
                    return;
                }

                // 3. Try WindowsApps Spotify
                string localAppSpotify = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "Spotify.exe");
                if (File.Exists(localAppSpotify))
                {
                    Process.Start(new ProcessStartInfo(localAppSpotify) { UseShellExecute = true });
                }
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            _sessionManager = null;
            _currentSession = null;
        }
        GC.SuppressFinalize(this);
    }
}
