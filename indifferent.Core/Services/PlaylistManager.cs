using Dapper;
using indifferent.Core.Models;
using Newtonsoft.Json;
using Serilog;

namespace indifferent.Core.Services;

// плейлист = папка в %APPDATA%\indifferent\play_lists\{name}\ + запись в бд
public class PlaylistManager
{
    readonly Db _db;

    public PlaylistManager(Db db) => _db = db;

    public List<Playlist> GetAll()
    {
        using var c = _db.Open();
        return c.Query<Playlist>("SELECT * FROM playlists ORDER BY created_at").ToList();
    }

    public void Create(string name)
    {
        var dir = DirOf(name);
        Directory.CreateDirectory(dir);

        using var c = _db.Open();
        c.Execute("INSERT OR IGNORE INTO playlists (id, name, created_at) VALUES (@id, @name, strftime('%s','now'))",
            new { id = Guid.NewGuid().ToString(), name });
    }

    public void Delete(string name)
    {
        using var c = _db.Open();
        var pl = c.QueryFirstOrDefault<Playlist>("SELECT * FROM playlists WHERE name = @name", new { name });
        if (pl == null) return;
        c.Execute("DELETE FROM playlist_tracks WHERE playlist_id = @id", new { id = pl.Id });
        c.Execute("DELETE FROM playlists WHERE id = @id", new { id = pl.Id });
        try { Directory.Delete(DirOf(name), true); } catch (Exception ex) { Log.Warning(ex, "папку плейлиста не убили"); }
    }

    public void AddTrack(string playlistName, string filePath)
    {
        if (!File.Exists(filePath)) return;

        var dir = DirOf(playlistName);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, Path.GetFileName(filePath));
        if (!string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            File.Copy(filePath, dest, true);

        var track = TrackFromTags(dest);
        using var c = _db.Open();
        c.Execute("""
            INSERT OR REPLACE INTO tracks (id, path, title, artist, album, duration)
            VALUES (@Id, @Path, @Title, @Artist, @Album, @Duration)
            """, track);

        var pl = c.QueryFirstOrDefault<Playlist>("SELECT * FROM playlists WHERE name = @playlistName", new { playlistName });
        if (pl == null) return;
        var pos = c.ExecuteScalar<int?>("SELECT MAX(position) FROM playlist_tracks WHERE playlist_id = @id", new { id = pl.Id }) ?? 0;
        c.Execute("INSERT OR IGNORE INTO playlist_tracks (playlist_id, track_id, position) VALUES (@pid, @tid, @pos)",
            new { pid = pl.Id, tid = track.Id, pos = pos + 1 });
    }

    public void RemoveTrack(string playlistName, string trackId)
    {
        using var c = _db.Open();
        c.Execute("DELETE FROM playlist_tracks WHERE playlist_id = (SELECT id FROM playlists WHERE name = @n) AND track_id = @t",
            new { n = playlistName, t = trackId });
    }

    public List<Track> GetTracks(string playlistName)
    {
        using var c = _db.Open();
        return c.Query<Track>("""
            SELECT t.* FROM tracks t
            JOIN playlist_tracks pt ON pt.track_id = t.id
            JOIN playlists p ON p.id = pt.playlist_id
            WHERE p.name = @n
            ORDER BY pt.position
            """, new { n = playlistName }).ToList();
    }

    string DirOf(string name) => Path.Combine(SettingsService.PlaylistsDir, Sanitize(name));

    static string Sanitize(string name)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return name;
    }

    public static Track TrackFromTags(string path)
    {
        var t = new Track { Id = LyricsCache.TrackId(path), Path = path };
        try
        {
            using var tf = TagLib.File.Create(path);
            t.Title = tf.Tag.Title ?? Path.GetFileNameWithoutExtension(path);
            t.Artist = tf.Tag.FirstPerformer ?? tf.Tag.FirstAlbumArtist ?? "";
            t.Album = tf.Tag.Album ?? "";
            t.Duration = tf.Properties?.Duration.TotalSeconds ?? 0;
        }
        catch (Exception ex)
        {
            // теги не читаются — ну и ладно, filename рулит
            Log.Warning(ex, "теги не читаются: {Path}", path);
            t.Title = Path.GetFileNameWithoutExtension(path);
        }
        return t;
    }
}
