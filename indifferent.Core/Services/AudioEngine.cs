using NAudio.Vorbis;
using NAudio.Wave;
using Serilog;

namespace indifferent.Core.Services;

public class AudioEngine : IDisposable
{
    WasapiOut? _output;
    WaveStream? _reader;
    Timer? _timer;
    string _currentPath = "";

    public event Action<double>? PositionChanged;
    public event Action? PlaybackFinished;

    public double CurrentTime => _reader?.CurrentTime.TotalSeconds ?? 0;
    public double TotalTime => _reader?.TotalTime.TotalSeconds ?? 0;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public string CurrentPath => _currentPath;

    public AudioEngine()
    {
        // 10 раз в сек шлём позицию в JS. достаточно для синхронизации текста
        _timer = new Timer(_ =>
        {
            if (IsPlaying)
            {
                try { PositionChanged?.Invoke(CurrentTime); } catch { /* гонки при dispose — пофиг */ }
            }
        }, null, 0, 100);
    }

    public void Play(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        Stop();
        try
        {
            // vorbis отдельно, всё остальное — стандартный reader
            _reader = Path.GetExtension(filePath).ToLowerInvariant() == ".ogg"
                ? new VorbisWaveReader(filePath)
                : new AudioFileReader(filePath);

            _output = new WasapiOut();
            _output.Init(_reader);
            _output.PlaybackStopped += (_, _) => PlaybackFinished?.Invoke();
            _output.Play();
            _currentPath = filePath;
        }
        catch (Exception ex)
        {
            // NAudio иногда сбоит на битых файлах. логируем и идём дальше
            Log.Error(ex, "не удалось воспроизвести {Path}", filePath);
            _reader = null;
        }
    }

    public void Pause() => _output?.Pause();

    public void Resume() => _output?.Play();

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _reader?.Dispose();
        _reader = null;
    }

    public void Seek(double seconds)
    {
        if (_reader == null) return;
        _reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, TotalTime));
    }

    public void SetVolume(float volume) // 0..1
    {
        if (_reader is AudioFileReader r) r.Volume = Math.Clamp(volume, 0f, 1f);
        // VorbisWaveReader не умеет громкость — ладно, потом доведём
    }

    public void Dispose()
    {
        _timer?.Dispose();
        Stop();
    }
}
