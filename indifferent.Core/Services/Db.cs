using Microsoft.Data.Sqlite;
using Dapper;
using Serilog;

namespace indifferent.Core.Services;

public class Db
{
    readonly string _connStr;

    public Db(SettingsService settings)
    {
        _connStr = $"Data Source={SettingsService.DbPath}";
        Init();
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_connStr);
        c.Open();
        return c;
    }

    void Init()
    {
        using var c = Open();
        c.Execute("""
            CREATE TABLE IF NOT EXISTS tracks (
                id TEXT PRIMARY KEY,
                path TEXT NOT NULL,
                title TEXT, artist TEXT, album TEXT,
                duration REAL, cover_path TEXT, waveform_path TEXT, lrc_path TEXT
            );
            CREATE TABLE IF NOT EXISTS playlists (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                cover_path TEXT,
                created_at INTEGER
            );
            CREATE TABLE IF NOT EXISTS playlist_tracks (
                playlist_id TEXT NOT NULL,
                track_id TEXT NOT NULL,
                position INTEGER,
                FOREIGN KEY (playlist_id) REFERENCES playlists(id),
                FOREIGN KEY (track_id) REFERENCES tracks(id)
            );
            CREATE TABLE IF NOT EXISTS lyrics_cache (
                track_id TEXT PRIMARY KEY,
                source TEXT, synced_text TEXT, plain_text TEXT, fetched_at INTEGER
            );
            """);
        Log.Information("база готова: {Db}", SettingsService.DbPath);
    }
}
