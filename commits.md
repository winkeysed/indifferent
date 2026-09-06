# commits.md — журнал изменений

## [2026-09-06] Проверка окружения и зависимостей (без написания кода)

- Прочитана и проанализирована спецификация проекта "indifferent" (C# + WPF + WebView2, NAudio, SQLite, LRCLIB).
- Проверена среда:
  - .NET SDK: найдено 8.0.424 и 10.0.400 (актуальная: 10.0.400) — достаточно для WPF + WebView2.
  - WebView2 Runtime (Evergreen): установлен, версия 152.0.4191.62/66 (Program Files (x86)\Microsoft\EdgeWebView).
  - NuGet-пакеты из спеки: не проверялись установкой (пакеты ставятся при `dotnet restore` после создания проектов).
- Изменения кода: нет. Создан только этот журнал (commits.md).

## [2026-09-06] Решения по проекту (принято с заказчиком)

- Воспроизведение — строго через плейлисты, «в никуда» нельзя.
- Target framework: net8.0-windows.
- Настройки/кэш/БД — в %APPDATA%\indifferent\.
- Ассеты (ico.png, персонаж, gif) — заглушки, добавит позже.
- Горячие клавиши — отложены на потом.
- ID треков — хеш пути/GUID.
- Стиль кода: по persona.md (живые комментарии, без корпоративного слопа).

## [2026-09-06] Шаг 1–5: каркас проекта

- Создан solution `indifferent.slnx` (SDK 10, XML-формат) + `indifferent.Core` (net8.0), `indifferent.Desktop` (net8.0-windows, WPF), папка `indifferent.Web` (копируется в output через csproj Desktop).
- NAudio 3.0.1 несовместим с net8.0 → откат на NAudio 2.2.1 (+ NAudio.Wasapi 2.2.1). Flurl 4: класс `Url` в namespace `Flurl`, не `Flurl.Http`.
- Core: Models (Track, Playlist, LyricLine, Settings), SettingsService (%APPDATA%\indifferent), Db (SQLite-схема), LyricsCache (+парсер .lrc), LrcLibClient, PlaylistManager (папки + БД + TagLib), LibraryScanner, AudioEngine (NAudio WasapiOut, vorbis через VorbisWaveReader), WaveformRenderer (SkiaSharp, RMS-бары).
- Desktop: App.xaml.cs (DI + Serilog), MainWindow (WebView2), WebHost (виртуальный хост appassets.indifferent.local → web/, AddHostObjectToScript("backend")), BackendApi (Host Object: play/seek/lyrics/playlists/settings/dialogs/GetRandomGif). UseWindowsForms=true для FolderBrowserDialog.
- Web: index.html (экран 1: топ-бар, зона текста, персонаж, талисман, выдвижная панель с прогрессом и кнопками), styles.css (CSS-переменные тем, акцент #FF6B9D), app.js (мост к backend), player.js (play/pause, seek по прогрессу и по строкам, подсветка текущей строки).
- Сборка: `dotnet build indifferent.slnx` — OK, 0 ошибок.
- TODO: settings.html, playlists.html, playlist-detail.html, очередь воспроизведения, drag&drop, hotkeys.

## [2026-09-06] Шаг: welcome-экран + экраны плейлистов

- `welcome.html` — стартовая вкладка: логотип `indifferent.` (розовая точка), подпись-подсказка, кнопки «Поехали» (в плеер) и «Создать плейлист» (prompt → CreatePlaylist → сразу в detail). Талисман + тема подтягиваются.
- `playlists.html` + `js/playlists.js` — сетка карточек, пустое состояние, создание плейлиста.
- `playlist-detail.html` — список треков (№/название/исполнитель/длительность), «Играть все» (пока первый трек), «+ Добавить трек» (OpenFileDialog → копирование в папку плейлиста), правый клик по треку = удалить.
- CSS: карточки плейлистов, шапка списка треков, hover-строки.
- WebHost теперь открывает `welcome.html` первым. Сборка OK.

## [2026-09-06] Фикс: краш при запуске

- App.xaml содержал StartupUri → WPF пытался создать второе MainWindow через несуществующий пустой конструктор, приложение падало через 1-2 сек. StartupUri убран (окно создаётся вручную в OnStartup). Сборка OK.

## [2026-09-06] Очередь, события в JS, экран настроек

- BackendApi: очередь плейлиста (Next/Prev по кругу, Prev в начале трека = seek 0), автопереход на следующий трек по PlaybackFinished, GetNowPlaying.
- События C# -> JS работают: PositionChanged -> app.onTimeUpdate (прогресс живой), app.onState (что играет), текст грузится в фоне -> app.onLyrics.
- settings.html: вкладки Основные/Визуал/Текст/Горячие клавиши, живой предпросмотр темы, размер шрифта, чекбоксы, путь к библиотеке.
- index.html: плашка 'что играет'; player.js переписан под события.
- Сборка OK.

## [2026-09-07] WebView2 удалён -> нативный WPF, форма LRCLIB, настройки

- WebView2 и весь indifferent.Web выпилены (csproj: пакет, UseWindowsForms, web-контент; BackendApi.cs и WebHost.cs удалены).
- Новый нативный UI: MainWindow.xaml — шапка, welcome-экран, плеер (текст слева с подсветкой/кликом-перемоткой, персонаж справа), экраны плейлистов и треков, нижняя панель (слайдер прогресса, громкость, prev/play/next, время).
- PlayerController.cs — очередь плейлиста, Next/Prev (Prev при >3 сек = seek 0), автопереход по концу трека.
- Темы: кисти в ресурсах приложения, App.ApplyTheme; settings.json в %APPDATA%\indifferent сохраняется (SettingsService.Save), тема/шрифт/чекбоксы/библиотека — SettingsWindow.
- Форма LRCLIB (LyricsDialog): открывается после добавления трека и через правый клик по треку; ссылка-поиск на lrclib.net, поле «вставьте ссылку со страницы песни» (парс id -> /api/get/{id}), поиск по API (/api/search), выбор варианта -> текст в кэш.
- LrcLibClient: GetByIdAsync/SearchAsync/SaveManual.
- Сборка OK.

## [2026-09-07] Расширенные логи + фикс краша при старте

- App.xaml.cs: глобальные обработчики (DispatcherUnhandledException, AppDomain.UnhandledException), try/catch вокруг стартапа, пошаговые Debug-логи (DI, настройки, тема, окно).
- MainWindow ctor: логи по этапам, ошибки показываются в MessageBox и в логе.
- НАЙДЕНО И ПОЧИНЕНО: Volume_ValueChanged срабатывал из XAML (Value=0.7) до инициализации полей -> NullReferenceException при InitializeComponent, окно вообще не показывалось. Громкость теперь задаётся в коде, обработчик защищён null-гвардом.
- Проверено запуском: процесс жив, 'окно показано' в логе.

## [2026-09-07] README.md написан (честно помечен как очень сырой прототип)

## [2026-09-07] README обновлён: добавлены реальные баги (нет окна лирикса, нет shuffle/repeat). Продолжение завтра.

## [2026-09-07] Готово к GitHub: .gitignore, workflow release.yml (паблиш по тегу v*), проверен Release-паблиш (indifferent.exe, один файл + Assets)

## [2026-09-07] commits.md перезаписан целиком в UTF-8 (Add-Content писал в системной кодировке — кириллица превращалась в кракозябры)
