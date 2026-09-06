using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using indifferent.Core.Models;
using Newtonsoft.Json;
using Serilog;

namespace indifferent.Core.Services;

public class LyricsCache
{
    readonly Db _db;

    public LyricsCache(Db db) => _db = db;

    // id = md5 полного пути. коллизии — не наш вопрос
    public static string TrackId(string path)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLower();
    }

    public (string? synced, string? plain)? Get(string trackId)
    {
        using var c = _db.Open();
        var row = c.QueryFirstOrDefault<(string Source, string Synced, string Plain)>(
            "SELECT source, synced_text, plain_text FROM lyrics_cache WHERE track_id = @trackId", new { trackId });
        return row.Source == null ? null : (row.Synced, row.Plain);
    }

    public void Put(string trackId, string source, string? synced, string? plain)
    {
        using var c = _db.Open();
        c.Execute("""
            INSERT OR REPLACE INTO lyrics_cache (track_id, source, synced_text, plain_text, fetched_at)
            VALUES (@trackId, @source, @synced, @plain, strftime('%s','now'))
            """, new { trackId, source, synced, plain });
    }

    // [mm:ss.xx] текст → список строк
    static readonly Regex LrcLine = new(@"^\[(\d{1,2}):(\d{2})\.(\d{2,3})\]\s*(.*)$", RegexOptions.Compiled);

    public static List<LyricLine> ParseSynced(string synced)
    {
        var lines = new List<LyricLine>();
        foreach (var raw in synced.Split('\n'))
        {
            var m = LrcLine.Match(raw.Trim());
            if (!m.Success) continue;
            int msLen = m.Groups[3].Value.Length;
            double ms = double.Parse(m.Groups[3].Value) / (msLen == 3 ? 1000.0 : 100.0);
            lines.Add(new LyricLine
            {
                Time = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value) + ms,
                Text = m.Groups[4].Value
            });
        }
        return lines.OrderBy(l => l.Time).ToList();
    }

    public List<LyricLine> GetSyncedForJs(string trackId)
    {
        var cached = Get(trackId);
        if (cached == null || cached.Value.synced == null) return new();
        return ParseSynced(cached.Value.synced);
    }
}
