using Dapper;
using indifferent.Core.Models;
using Serilog;

namespace indifferent.Core.Services;

public class LibraryScanner
{
    readonly Db _db;
    static readonly string[] AudioExt = { ".mp3", ".flac", ".ogg" };

    public LibraryScanner(Db db) => _db = db;

    // пробегает папку, суёт треки в бд. рекурсивно, чего мелочиться
    public List<Track> Scan(string folder)
    {
        var found = new List<Track>();
        if (!Directory.Exists(folder)) return found;

        foreach (var path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
        {
            if (!AudioExt.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
            var t = PlaylistManager.TrackFromTags(path);
            using var c = _db.Open();
            c.Execute("""
                INSERT OR REPLACE INTO tracks (id, path, title, artist, album, duration)
                VALUES (@Id, @Path, @Title, @Artist, @Album, @Duration)
                """, t);
            found.Add(t);
        }

        Log.Information("отсканировано {Count} треков в {Folder}", found.Count, folder);
        return found;
    }
}
