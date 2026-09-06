using NAudio.Wave;
using SkiaSharp;

namespace indifferent.Core.Services;

public class WaveformRenderer
{
    readonly Db _db;

    public WaveformRenderer(Db db) => _db = db;

    public string GetWaveformPath(string trackId, string trackPath)
    {
        var png = Path.Combine(SettingsService.CacheDir, "waveforms", $"{trackId}.png");
        if (File.Exists(png)) return png;
        if (!File.Exists(trackPath)) return "";

        try { Render(trackPath, png); return png; }
        catch { return ""; } // не получилось — не получилось
    }

    // 800x100, RMS по кусочкам. долго думал над красивостью — передумал
    void Render(string audioPath, string outPath)
    {
        using var reader = new AudioFileReader(audioPath);
        int sampleRate = reader.WaveFormat.SampleRate;
        int bars = 200;
        long total = reader.Length / 4; // stereo float
        long per = Math.Max(1, total / bars);
        var peaks = new float[bars];

        var buf = new float[4096];
        long read = 0;
        int bar = 0;
        float acc = 0;
        long inBar = 0;

        int n;
        while ((n = reader.Read(buf, 0, buf.Length)) > 0 && bar < bars)
        {
            for (int i = 0; i < n; i++)
            {
                acc += buf[i] * buf[i];
                inBar++;
                if (++read % per == 0 && bar < bars)
                {
                    peaks[bar++] = inBar > 0 ? (float)Math.Sqrt(acc / inBar) : 0;
                    acc = 0; inBar = 0;
                }
            }
        }

        float max = Math.Max(0.001f, peaks.Max());
        using var bmp = new SKBitmap(800, 100);
        using var cv = new SKCanvas(bmp);
        cv.Clear(SKColor.FromHsl(0, 0, 100));
        var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        float bw = 800f / bars;
        for (int i = 0; i < bars; i++)
        {
            float h = peaks[i] / max * 90f;
            cv.DrawRect(i * bw, (100 - h) / 2, bw * 0.7f, Math.Max(1, h), paint);
        }

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 80);
        using var fs = File.OpenWrite(outPath);
        data.SaveTo(fs);
    }
}
