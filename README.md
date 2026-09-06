# indifferent

Локальный музыкальный плеер. Минимализм, синхронизированный текст песен из LRCLIB, персонаж справа, тёмная/светлая тема. Нативный WPF, .NET 8.

> **ВНИМАНИЕ: проект очень-очень сырой.** Работает база: плейлисты, воспроизведение, текст из LRCLIB, настройки. Много недоделано, баги почти гарантированы. Это ранний прототип, а не релиз.

## Что уже работает

- **Welcome-экран** — логотип, «Поехали» и «Создать плейлист».
- **Плейлисты** — создаются как папки в `%APPDATA%\indifferent\play_lists\{имя}\`, файлы копируются внутрь.
- **Воспроизведение** — NAudio (WasapiOut), mp3/flac/ogg. Очередь плейлиста, next/prev по кругу, prev в начале трека = перемотка на ноль, автопереход на следующий трек.
- **Текст песни** — попытка автозагрузки из LRCLIB, синхронизация по строкам (подсветка), клик по строке = перемотка. Автопоиск часто не находит/не срабатывает, ручная форма пока не открывается (см. ниже).
- **Настройки** (`settings.json` в `%APPDATA%\indifferent\`) — тема (светлая/тёмная, применяется сразу), размер шрифта текста, чекбоксы, путь к библиотеке.
- **Логи** — `%APPDATA%\indifferent\logYYYYMMDD.txt`, все исключения падают в лог и в MessageBox.

## Чего нет / сломано (честно)

Проверено ~минутой теста, список наверняка не полный:

- **Окно текста песни (LyricsDialog) не открывается** — форма LRCLIB сейчас не показывается.
- **Перемешка (shuffle) и повтор (repeat) не работают** — кнопки есть, логики нет.
- Gapless, кроссфейд, ReplayGain — нет.
- Drag & drop, горячие клавиши — нет.
- Waveform рендер (SkiaSharp) есть, но в UI не подключён.
- Обложки плейлистов — нет.
- Персонаж и gif-талисман — заглушки, ассетов пока нет (`Assets/characters/`, `Assets/pl_dans/` — добавь свои png/gif).
- Громкость не влияет на vorbis (только mp3/flac).
- Ливбрери-сканер есть, кнопки «Сканировать» в UI нет.
- UI местами спартанский, это WPF-минимализм почти без стилей.

## Сборка и запуск

Нужен .NET SDK 8+ (есть 8 и 10 — ок).

```
dotnet build indifferent.slnx
```

Запуск:

```
indifferent.Desktop\bin\Debug\net8.0-windows\indifferent.exe
```

## Структура

```
indifferent.Core/     — сервисы: AudioEngine (NAudio), PlaylistManager (папки + SQLite),
                        LrcLibClient, LyricsCache (парсер .lrc + кэш), LibraryScanner,
                        WaveformRenderer, SettingsService, Db
indifferent.Desktop/  — WPF: MainWindow (welcome/плеер/плейлисты/треки), SettingsWindow,
                        LyricsDialog (форма LRCLIB), PlayerController (очередь), App (DI + темы)
play_lists/           — нет, плейлисты в %APPDATA%\indifferent\play_lists\
```

## Стек

.NET 8 (WPF), NAudio 2.2.1 + NAudio.Vorbis, TagLibSharp, Flurl.Http, Newtonsoft.Json,
Microsoft.Data.Sqlite + Dapper, SkiaSharp, Serilog, Microsoft.Extensions.DependencyInjection.

WebView2 был удалён — UI полностью нативный.

## Хочешь помочь?

Первым делом: очереди нет как таковой (только текущий плейлист), вижуал без полировки,
иконки — юникод-глифы. Планы: drag&drop, hotkeys, waveform, обложки, gapless.
