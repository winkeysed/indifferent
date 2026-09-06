using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using indifferent.Core.Services;
using Serilog;
using Application = System.Windows.Application;

namespace indifferent.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ловим всё, что не поймали иначе — иначе процесс молча умирает
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Fatal(args.Exception, "UI-поток упал");
            MessageBox.Show(args.Exception.ToString(), "Ошибка");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "фатальный краш домена");

        Log.Information("=== старт indifferent ===");

        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(SettingsService.DataDir, "log.txt"), rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();
            Log.Information("папка данных: {Dir}", SettingsService.DataDir);

            var services = new ServiceCollection();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<Db>();
            services.AddSingleton<AudioEngine>();
            services.AddSingleton<LyricsCache>();
            services.AddSingleton<LrcLibClient>();
            services.AddSingleton<PlaylistManager>();
            services.AddSingleton<LibraryScanner>();
            services.AddSingleton<WaveformRenderer>();
            services.AddSingleton<PlayerController>();
            Services = services.BuildServiceProvider();
            Log.Debug("DI собран");

            var s = Services.GetRequiredService<SettingsService>();
            Log.Debug("настройки: {Json}", s.Current.ToJson());
            ApplyTheme(s.Current.Theme);
            Log.Debug("тема {Theme} применена", s.Current.Theme);

            var win = new MainWindow(Services);
            MainWindow = win;
            win.Show();
            Log.Information("окно показано");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "стартуп умер");
            MessageBox.Show(ex.ToString(), "Ошибка запуска");
            Shutdown();
        }
    }

    // темы — просто перекидываем кисти в ресурсы
    public static void ApplyTheme(string theme)
    {
        var res = Current.Resources;
        bool dark = theme == "dark";
        res["BgPrimary"] = new SolidColorBrush(dark ? Color.FromRgb(0x0A, 0x0A, 0x0F) : Colors.White);
        res["TextPrimary"] = new SolidColorBrush(dark ? Color.FromRgb(0xF0, 0xF0, 0xF0) : Color.FromRgb(0x1A, 0x1A, 0x1A));
        res["TextSecondary"] = new SolidColorBrush(dark ? Color.FromRgb(0x88, 0x88, 0x88) : Color.FromRgb(0x66, 0x66, 0x66));
        res["Accent"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x9D));
        res["BorderSubtle"] = new SolidColorBrush(dark ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x14, 0x00, 0x00, 0x00));
        res["ProgressBg"] = new SolidColorBrush(dark ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
        res["ProgressFill"] = res["TextPrimary"];
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.GetRequiredService<AudioEngine>().Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
