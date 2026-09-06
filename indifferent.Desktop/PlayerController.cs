using indifferent.Core.Models;
using indifferent.Core.Services;

namespace indifferent.Desktop;

// очередь + состояние плеера. UI подписывается и перерисовывается
public class PlayerController
{
    public AudioEngine Audio { get; }
    readonly PlaylistManager _playlists;
    readonly LrcLibClient _lrc;

    List<Track> _queue = new();
    int _idx = -1;

    public event Action? StateChanged;
    public Track? Current => _idx >= 0 && _idx < _queue.Count ? _queue[_idx] : null;
    public bool IsPlaying => Audio.IsPlaying;

    public PlayerController(AudioEngine audio, PlaylistManager playlists, LrcLibClient lrc)
    {
        Audio = audio;
        _playlists = playlists;
        _lrc = lrc;
        Audio.PlaybackFinished += () => Next();
    }

    public List<Track> GetTracks(string playlist) => _playlists.GetTracks(playlist);

    public void PlayPlaylist(string name)
    {
        _queue = _playlists.GetTracks(name);
        _idx = 0;
        PlayCurrent();
    }

    void PlayCurrent()
    {
        var t = Current;
        if (t == null) return;
        Audio.Play(t.Path);
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (Current == null) return;
        if (IsPlaying) Audio.Pause(); else Audio.Resume();
        StateChanged?.Invoke();
    }

    public void Next()
    {
        if (_queue.Count == 0) return;
        _idx = (_idx + 1) % _queue.Count;
        PlayCurrent();
    }

    public void Prev()
    {
        if (_queue.Count == 0) return;
        if (Audio.CurrentTime > 3) { Audio.Seek(0); return; } // в начале трека = предыдущий
        _idx = (_idx - 1 + _queue.Count) % _queue.Count;
        PlayCurrent();
    }

    public void Seek(double sec) => Audio.Seek(sec);

    // текст: из кэша, если нет — сеть. вызывать из фона
    public async Task<List<LyricLine>> GetLyricsAsync(Track t)
    {
        try { return await _lrc.FetchLyricsAsync(t.Artist, t.Title, t.Album, t.Duration, t.Id); }
        catch { return new(); }
    }
}
