using System.Net;
using indifferent.Core.Models;
using Flurl.Http;
using Newtonsoft.Json;
using Serilog;

namespace indifferent.Core.Services;

public class LrcLibClient
{
    readonly LyricsCache _cache;

    public LrcLibClient(LyricsCache cache) => _cache = cache;

    public class LrcApiResponse
    {
        public int Id { get; set; }
        public bool Instrumental { get; set; }
        public string? PlainLyrics { get; set; }
        public string? SyncedLyrics { get; set; }
        public string? TrackName { get; set; }
        public string? ArtistName { get; set; }
        public double Duration { get; set; }
    }

    // getById по числовому id с сайта. вернёт null если 404/нет сети
    public async Task<LrcApiResponse?> GetByIdAsync(int id)
    {
        try { return await new Flurl.Url($"https://lrclib.net/api/get/{id}").GetJsonAsync<LrcApiResponse>(); }
        catch (FlurlHttpException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound) { return null; }
        catch { return null; }
    }

    public async Task<List<LrcApiResponse>> SearchAsync(string artist, string title)
    {
        try
        {
            return await new Flurl.Url("https://lrclib.net/api/search")
                .SetQueryParam("artist_name", artist)
                .SetQueryParam("track_name", title)
                .GetJsonAsync<List<LrcApiResponse>>() ?? new();
        }
        catch { return new(); }
    }

    public void SaveManual(string trackId, LrcApiResponse resp) =>
        _cache.Put(trackId, "lrclib", resp.SyncedLyrics, resp.PlainLyrics);

    public async Task<List<LyricLine>> FetchLyricsAsync(string artist, string title, string album, double? duration, string trackId)
    {
        var cached = _cache.Get(trackId);
        if (cached != null)
            return cached.Value.synced != null ? LyricsCache.ParseSynced(cached.Value.synced) : new();

        var resp = await new Flurl.Url("https://lrclib.net/api/get")
            .SetQueryParam("artist_name", artist)
            .SetQueryParam("track_name", title)
            .SetQueryParam("album_name", album)
            .SetQueryParam("duration", duration)
            .GetJsonAsync<LrcApiResponse>();

        if (resp == null || resp.Instrumental) return new();

        _cache.Put(trackId, "lrclib", resp.SyncedLyrics, resp.PlainLyrics);
        return resp.SyncedLyrics != null ? LyricsCache.ParseSynced(resp.SyncedLyrics) : new();
    }
}
