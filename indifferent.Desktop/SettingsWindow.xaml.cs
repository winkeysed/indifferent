using System.Windows;
using indifferent.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace indifferent.Desktop;

public partial class SettingsWindow : Window
{
    readonly SettingsService _settings;
    readonly LibraryScanner _scanner;

    public SettingsWindow(IServiceProvider sp)
    {
        InitializeComponent();
        _settings = sp.GetRequiredService<SettingsService>();
        _scanner = sp.GetRequiredService<LibraryScanner>();

        var s = _settings.Current;
        (s.Theme == "dark" ? ThDark : ThLight).IsChecked = true;
        FsRange.Value = s.FontSize;
        FsVal.Text = s.FontSize.ToString();
        ShiftChar.IsChecked = s.ShiftCharacterWhenNoLyrics;
        GifOn.IsChecked = s.GifEnabled;
        AnimOn.IsChecked = s.AnimationsEnabled;
        LibPath.Text = s.LibraryPath;

        FsRange.ValueChanged += (_, e) => FsVal.Text = ((int)e.NewValue).ToString();
        Browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            if (dlg.ShowDialog() == true) LibPath.Text = dlg.FolderName;
        };
        SaveBtn.Click += Save;
    }

    void Save(object s, RoutedEventArgs e)
    {
        var st = _settings.Current;
        st.Theme = ThDark.IsChecked == true ? "dark" : "light";
        st.FontSize = (int)FsRange.Value;
        st.ShiftCharacterWhenNoLyrics = ShiftChar.IsChecked == true;
        st.GifEnabled = GifOn.IsChecked == true;
        st.AnimationsEnabled = AnimOn.IsChecked == true;
        st.LibraryPath = LibPath.Text;
        _settings.Save(); // settings.json в %APPDATA%
        App.ApplyTheme(st.Theme);
        Close();
    }
}
