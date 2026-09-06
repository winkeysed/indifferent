using System.Reflection;
using indifferent.Core.Models;
using Serilog;

namespace indifferent.Core.Services;

public class SettingsService
{
    // настройки живут в %APPDATA%\indifferent — туда писать можно всегда
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "indifferent");

    public static readonly string SettingsPath = Path.Combine(DataDir, "settings.json");
    public static readonly string DbPath = Path.Combine(DataDir, "indifferent.db");
    public static readonly string PlaylistsDir = Path.Combine(DataDir, "play_lists");
    public static readonly string CacheDir = Path.Combine(DataDir, "cache");

    public Settings Current { get; set; } = new();

    public SettingsService()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(PlaylistsDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(Path.Combine(CacheDir, "waveforms"));
        Load();
    }

    void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = Settings.FromJson(File.ReadAllText(SettingsPath));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "settings.json битый, берём дефолт");
        }
    }

    public void Save()
    {
        File.WriteAllText(SettingsPath, Current.ToJson());
    }
}
