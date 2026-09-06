using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using indifferent.Core.Models;
using indifferent.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace indifferent.Desktop;

// форма: ищем текст на lrclib, либо забираем по ссылке с сайта
public partial class LyricsDialog : Window
{
    readonly LrcLibClient _lrc;
    readonly Track _track;

    List<SearchHit> _found = new();
    SearchHit? _picked;

    public LyricsDialog(IServiceProvider sp, Track track)
    {
        InitializeComponent();
        _lrc = sp.GetRequiredService<LrcLibClient>();
        _track = track;
        TrackLbl.Text = $"{track.Artist} — {track.Title}".Trim(" —".ToCharArray());

        // сгенерированная ссылка на поиск на сайте
        var q = Uri.EscapeDataString($"{track.Artist} {track.Title}".Trim());
        OpenSite.NavigateUri = new Uri($"https://lrclib.net/search?q={q}");
        OpenSite.RequestNavigate += (_, e) =>
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        };

        SearchBtn.Click += (_, _) => _ = Search();
        FromUrlBtn.Click += (_, _) => _ = FromUrl();
        ApplyBtn.Click += Apply;
        CloseBtn.Click += (_, _) => Close();
        _ = Search(); // сразу ищем по тегам
    }

    async Task Search()
    {
        Results.Items.Clear();
        Results.Items.Add(new ListBoxItem { Content = "ищу...", IsEnabled = false });
        var resp = await _lrc.SearchAsync(_track.Artist, _track.Title);
        Results.Items.Clear();
        _found = new();

        // чтобы типы не путать, держим свой класс
        foreach (var r in resp.Take(15))
        {
            var hit = new SearchHit { Id = r.Id, Title = r.TrackName ?? "", Artist = r.ArtistName ?? "", Synced = r.SyncedLyrics, Plain = r.PlainLyrics };
            _found.Add(hit);
            var item = new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = $"{hit.Artist} — {hit.Title}  ({TimeSpan.FromSeconds(r.Duration):m\\:ss}){(hit.Synced == null ? "  [без синхро]" : "")}",
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
            item.Selected += (_, _) => _picked = hit;
            Results.Items.Add(item);
        }
        if (_found.Count == 0) Results.Items.Add(new ListBoxItem { Content = "ничего не нашлось", IsEnabled = false });
    }

    async Task FromUrl()
    {
        var url = UrlBox.Text.Trim();
        var m = Regex.Match(url, @"(?:/api)?/get/(\d+)");
        if (!m.Success)
        {
            MessageBox.Show("В ссылке нет id. Пример: https://lrclib.net/get/12345");
            return;
        }
        var resp = await _lrc.GetByIdAsync(int.Parse(m.Groups[1].Value));
        if (resp == null) { MessageBox.Show("Сайт не отдал текст (нет сети или нет такой песни)."); return; }

        _picked = new SearchHit { Id = resp.Id, Title = resp.TrackName ?? "", Artist = resp.ArtistName ?? "", Synced = resp.SyncedLyrics, Plain = resp.PlainLyrics };
        Results.Items.Clear();
        Results.Items.Add(new ListBoxItem
        {
            Content = $"✓ {_picked.Artist} — {_picked.Title} — готов к сохранению", IsSelected = true
        });
    }

    void Apply(object s, RoutedEventArgs e)
    {
        if (_picked == null) { MessageBox.Show("Сначала выбери вариант из списка."); return; }
        if (_picked.Synced == null && _picked.Plain == null) { MessageBox.Show("У этого варианта вообще нет текста."); return; }
        _lrc.SaveManual(_track.Id, new LrcLibClient.LrcApiResponse { SyncedLyrics = _picked.Synced, PlainLyrics = _picked.Plain });
        Close();
    }

    class SearchHit
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string? Synced { get; set; }
        public string? Plain { get; set; }
    }
}
