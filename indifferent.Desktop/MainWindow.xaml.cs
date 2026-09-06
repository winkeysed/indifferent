using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using indifferent.Core.Models;
using indifferent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace indifferent.Desktop;

public partial class MainWindow : Window
{
    readonly IServiceProvider _sp;
    readonly PlayerController _player;
    readonly SettingsService _settings;
    readonly PlaylistManager _playlists;
    readonly LyricsCache _lyrics;

    string _currentPlaylist = "";
    DispatcherTimer _timer = new();
    bool _seeking;

    public MainWindow(IServiceProvider sp)
    {
        try
        {
            Log.Debug("MainWindow: InitializeComponent");
            InitializeComponent();
            _sp = sp;
            _player = sp.GetRequiredService<PlayerController>();
            _settings = sp.GetRequiredService<SettingsService>();
            _playlists = sp.GetRequiredService<PlaylistManager>();
            _lyrics = sp.GetRequiredService<LyricsCache>();
            Log.Debug("MainWindow: сервисы получены");

            _player.StateChanged += OnStateChanged;
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += (_, _) => UpdateProgress();
            _timer.Start();

            LoadCharacter();
            Log.Debug("MainWindow: персонаж загружен");
            Volume.Value = 0.7; // громкость задаём кодом, а не в XAML
            ShowWelcome();
            Log.Debug("MainWindow: welcome показан");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MainWindow ctor упал");
            MessageBox.Show(ex.ToString(), "Ошибка окна");
            throw;
        }
    }

    void ShowWelcome()
    {
        WelcomePanel.Visibility = Visibility.Visible;
        PlayerPanel.Visibility = Visibility.Collapsed;
        PlaylistsPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Collapsed;
        BottomPanel.Visibility = Visibility.Collapsed;
        WelcomeHint.Text = _playlists.GetAll().Count > 0
            ? $"плейлистов: {_playlists.GetAll().Count}"
            : "музыка слушает тебя";
    }

    void ShowPlayer()
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
        PlaylistsPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Collapsed;
        BottomPanel.Visibility = Visibility.Visible;
    }

    void ShowPlaylists()
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Collapsed;
        PlaylistsPanel.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
        BottomPanel.Visibility = Visibility.Collapsed;
        RenderPlaylists();
    }

    void ShowDetail(string name)
    {
        _currentPlaylist = name;
        WelcomePanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Collapsed;
        PlaylistsPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        BottomPanel.Visibility = Visibility.Collapsed;
        DetailTitle.Text = name;
        RenderTracks(name);
    }

    // ---------- события шапки/велкома ----------
    void Logo_Click(object s, RoutedEventArgs e) => ShowWelcome();
    void Playlists_Click(object s, RoutedEventArgs e) => ShowPlaylists();
    void Settings_Click(object s, RoutedEventArgs e) => new SettingsWindow(_sp) { Owner = this }.ShowDialog();

    void GoPlay_Click(object s, RoutedEventArgs e)
    {
        var pls = _playlists.GetAll();
        if (pls.Count == 0) { ShowPlaylists(); return; }
        ShowPlayer();
        if (_player.Current == null) _player.PlayPlaylist(pls[0].Name);
    }

    void NewPlaylist_Welcome_Click(object s, RoutedEventArgs e) { ShowPlaylists(); CreatePlaylistDialog(); }

    // ---------- плейлисты ----------
    void NewPlaylist_Click(object s, RoutedEventArgs e) => CreatePlaylistDialog();

    async void CreatePlaylistDialog()
    {
        var name = Prompt("Название плейлиста:", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        _playlists.Create(name.Trim());
        ShowDetail(name.Trim());
        await ScanAfterCreate(name.Trim());
    }

    // после создания сразу предлагаем набить плейлист
    async Task ScanAfterCreate(string name)
    {
        if (MessageBox.Show("Добавить треки сейчас?", "indifferent", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        AddTrack_Click(this, new RoutedEventArgs());
    }

    void RenderPlaylists()
    {
        PlaylistCards.Children.Clear();
        foreach (var p in _playlists.GetAll())
        {
            var cnt = _playlists.GetTracks(p.Name).Count;
            var card = MakeCard(p.Name, cnt == 0 ? "♪" : "♫", $"{cnt} треков");
            card.MouseLeftButtonUp += (_, _) => ShowDetail(p.Name);
            PlaylistCards.Children.Add(card);
        }
        var add = MakeCard("+", "＋", "Новый плейлист");
        add.MouseLeftButtonUp += (_, _) => CreatePlaylistDialog();
        PlaylistCards.Children.Add(add);
    }

    Border MakeCard(string title, string ph, string sub)
    {
        var tb = new TextBlock
        {
            Text = ph, FontSize = 40, Foreground = (Brush)FindResource("TextSecondary"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var cover = new Border
        {
            Width = 160, Height = 160, CornerRadius = new CornerRadius(12),
            Background = (Brush)FindResource("ProgressBg"), Child = tb
        };
        var stack = new StackPanel { Margin = new Thickness(4) };
        stack.Children.Add(cover);
        stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = (Brush)FindResource("TextPrimary"), TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = (Brush)FindResource("TextSecondary") });

        var b = new Border { Margin = new Thickness(6), Padding = new Thickness(4), Cursor = System.Windows.Input.Cursors.Hand, Child = stack };
        b.MouseEnter += (_, _) => { cover.Opacity = 0.8; };
        b.MouseLeave += (_, _) => { cover.Opacity = 1; };
        return b;
    }

    // ---------- треки ----------
    void RenderTracks(string name)
    {
        TrackList.Items.Clear();
        var tracks = _playlists.GetTracks(name);
        for (int i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i];
            var row = new TextBlock
            {
                Text = $"{i + 1}.  {t.Title}   —   {t.Artist}   [{TimeSpan.FromSeconds(t.Duration):m\\:ss}]",
                Padding = new Thickness(10, 14, 6, 14),
                Foreground = (Brush)FindResource("TextPrimary")
            };
            var item = new ListBoxItem { Content = row };
            item.MouseDoubleClick += (_, _) => { _player.PlayPlaylist(name); for (int k = 0; k < i; k++) _player.Next(); };
            var menu = new ContextMenu();
            var del = new MenuItem { Header = "Удалить из плейлиста" };
            var lrc = new MenuItem { Header = "Текст из LRCLIB" };
            del.Click += (_, _) => { _playlists.RemoveTrack(name, t.Id); RenderTracks(name); };
            lrc.Click += (_, _) => new LyricsDialog(_sp, t) { Owner = this }.ShowDialog();
            menu.Items.Add(del); menu.Items.Add(lrc);
            item.ContextMenu = menu;
            TrackList.Items.Add(item);
        }
        if (tracks.Count == 0)
            TrackList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "пусто. добавь треков", Foreground = (Brush)FindResource("TextSecondary"), Padding = new Thickness(10) }, IsEnabled = false });
    }

    void PlayAll_Click(object s, RoutedEventArgs e)
    {
        if (_currentPlaylist == "") return;
        ShowPlayer();
        _player.PlayPlaylist(_currentPlaylist);
    }

    async void AddTrack_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Аудио (*.mp3;*.flac;*.ogg)|*.mp3;*.flac;*.ogg" };
        if (dlg.ShowDialog() != true) return;

        Track? last = null;
        foreach (var f in dlg.FileNames)
        {
            _playlists.AddTrack(_currentPlaylist, f);
            last = _playlists.GetTracks(_currentPlaylist).FirstOrDefault(t => t.Path.Equals(f, StringComparison.OrdinalIgnoreCase));
        }
        RenderTracks(_currentPlaylist);

        if (last != null)
            new LyricsDialog(_sp, last) { Owner = this }.ShowDialog(); // сразу форма текста
    }

    // ---------- плеер ----------
    void OnStateChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var t = _player.Current;
            NpTitle.Text = t?.Title ?? "";
            NpArtist.Text = t?.Artist ?? "";
            PlayBtn.Content = _player.IsPlaying ? "⏸" : "▶";
            if (t != null) _ = LoadLyrics(t);
        });
    }

    async Task LoadLyrics(Track t)
    {
        LyricsList.Items.Clear();
        NoLyricsHint.Text = "ищу текст...";
        var lines = await _player.GetLyricsAsync(t);
        NoLyricsHint.Text = lines.Count == 0 ? "текст не найден" : "";

        double last = 0;
        foreach (var l in lines)
        {
            var tb = new TextBlock
            {
                Text = l.Text, FontSize = _settings.Current.FontSize,
                Foreground = (Brush)FindResource("TextSecondary"),
                TextAlignment = TextAlignment.Center, Padding = new Thickness(4, 3, 4, 3), Opacity = 0.6,
                Cursor = System.Windows.Input.Cursors.Hand, Tag = l.Time
            };
            tb.MouseDown += (_, _) => _player.Seek(l.Time);
            LyricsList.Items.Add(tb);
            last = l.Time;
        }
        _lyricLines = lines;
    }

    List<LyricLine> _lyricLines = new();
    int _curLine = -1;

    void UpdateProgress()
    {
        if (_seeking) return;
        double cur = _player.Audio.CurrentTime, total = _player.Audio.TotalTime;
        if (total > 0) Progress.Value = cur / total * 100;
        TimeLbl.Text = $"{TimeSpan.FromSeconds(cur):m\\:ss} / {TimeSpan.FromSeconds(total):m\\:ss}";

        // подсветка строки
        if (_lyricLines.Count == 0) return;
        int idx = -1;
        for (int i = 0; i < _lyricLines.Count; i++) { if (_lyricLines[i].Time <= cur) idx = i; else break; }
        if (idx == _curLine) return;
        _curLine = idx;
        for (int i = 0; i < LyricsList.Items.Count; i++)
        {
            if (LyricsList.Items[i] is TextBlock tb)
            {
                bool active = i == idx;
                tb.Foreground = (Brush)FindResource(active ? "Accent" : "TextSecondary");
                tb.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
                tb.Opacity = active ? 1 : 0.6;
                if (active) tb.BringIntoView();
            }
        }
    }

    void SeekStart(object s, System.Windows.Controls.Primitives.DragStartedEventArgs e) => _seeking = true;
    void SeekEnd(object s, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _seeking = false;
        _player.Seek(Progress.Value / 100 * _player.Audio.TotalTime);
    }

    void Play_Click(object s, RoutedEventArgs e) => _player.Toggle();
    void Next_Click(object s, RoutedEventArgs e) => _player.Next();
    void Prev_Click(object s, RoutedEventArgs e) => _player.Prev();

    void Volume_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        // XAML грузится раньше полей — Value="0.7" триггерит событие при нуле инициализации
        if (_player == null) return;
        _player.Audio.SetVolume((float)e.NewValue);
    }

    void LoadCharacter()
    {
        var p = _settings.Current.CharacterPath;
        if (string.IsNullOrEmpty(p)) return;
        if (!Path.IsPathRooted(p)) p = Path.Combine(AppContext.BaseDirectory, p);
        if (File.Exists(p)) Character.Source = new BitmapImage(new Uri(p));
    }

    string? Prompt(string caption, string def)
    {
        // простейший ввод без лишних окон
        var win = new Window
        {
            Width = 380, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            Title = caption, ResizeMode = ResizeMode.NoResize, Background = (Brush)FindResource("BgPrimary")
        };
        var box = new TextBox { Text = def, Margin = new Thickness(16) };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 16, 0) };
        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(box);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btns.Children.Add(ok);
        panel.Children.Add(btns);
        win.Content = panel;
        ok.Click += (_, _) => { win.DialogResult = true; };
        return win.ShowDialog() == true ? box.Text : null;
    }
}
