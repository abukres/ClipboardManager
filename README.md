# Clipboard Manager

A Windows clipboard history manager inspired by
[Clipboard History Pro](https://www.outertech.com/en/clipboard-manager). It lives in the system tray,
remembers every text you copy and lets you paste it back into any application from a popup menu that
is opened with the Caps Lock key.

Written with C# and .NET 11 (Windows Forms). Clipboard history is stored in a local SQLite database.

## Features

- **Clipboard monitoring** for text content using `WM_CLIPBOARDUPDATE` (no polling).
- **Paste menu** at the mouse cursor, opened with **Caps Lock** (or an optional global hotkey such as `Alt+C`).
  - Entries are numbered `1`–`9`, stickies use letters `B`–`Z` and `A` opens the archive; press the key or click to paste.
  - Arrow keys, `PageUp`/`PageDown`, `Home`/`End`, `Enter` and `Esc` work as expected.
  - Choosing an entry copies it to the Clipboard and sends `Ctrl+V` to the previously active window.
  - Pasted text is always pure text: only Unicode text is put on the Clipboard, so a destination cannot
    restore fonts, colors or links, and invisible formatting characters (no-break spaces, zero width
    joiners, soft hyphens) are removed.
  - The popup width can be changed by dragging its right edge and is remembered for the next time.
  - Clicking anywhere outside the popup closes it.
  - The commands at the bottom open the archive window, open the configuration window, clear the
    Clipboard or add the current Clipboard content to the stickies.
- **Tray icon** with the recent entries, stickies, "Clear Clipboard content", archive, configuration and quit.
  The icon tooltip shows the most recent clipboard text. Double click opens the configuration window.
- **Archive window** with a SQLite backed search:
  - Keywords are combined (`foo bar`), excluded with a minus sign (`foo -bar`), phrases can be quoted.
  - Date filter: complete archive, 30 days, 6 months, 1 year, 3 years.
  - Configurable limit for stored items, archive size and maximum number of search results.
- **Stickies** (permanent text clips) that are listed in the paste menu and in a tray sub menu.
- **Configuration window** for monitoring, hotkeys, history depth, item size/length filters, startup entry
  and the archive.
- **More options**: notification sound, tray icon behaviour, initial cursor position in the paste menu,
  list double click action, delete confirmation, ignore-list (`;password;secret`), search result limit.
- **Link repair**: a browser link that is split over several lines is joined automatically.
- **Fullscreen protection**: the paste menu is suppressed while a fullscreen application is running.

Not implemented on purpose (as requested): encryption, RTF/HTML conversions, cloud synchronization and a
custom data format.

## Usage

| Action | Shortcut |
| --- | --- |
| Show paste menu | `Caps Lock` |
| Optional user hotkey | Configuration → Hotkey (for example `Alt+C`) |
| Paste selected entry | `1`…`9`, `B`…`Z`, `Enter` or click |
| Move through the menu | `↑` / `↓` / `PgUp` / `PgDn` / `Home` / `End` |
| Close the menu | `Esc` |
| Open the archive | `A` in the paste menu, `Archive...` in the paste menu or the tray menu |
| Open configuration | Double click the tray icon (configurable) or run `ClipboardManager.exe --config` |

The first number/letter of a history entry or sticky is its accelerator; the stickies start at `B`
because `A` is reserved for the archive. The second launch of the executable brings the configuration
window of the running instance to the front.

## Data

| File | Location |
| --- | --- |
| Database | `%LOCALAPPDATA%\ClipboardManager\clipboard.db` |
| Settings | `%LOCALAPPDATA%\ClipboardManager\settings.json` |

The database uses three tables: `History` (limited ring of recent entries), `Archive` (permanent
history with a lower-cased `SearchText` column for case-insensitive `LIKE` searches) and `Stickies`.

## Build and test

```powershell
dotnet build ClipboardManager.slnx
dotnet test ClipboardManager.slnx
dotnet run --project ClipboardManagerWinForm --config
```

The solution contains the WinForms application (`ClipboardManagerWinForm`) and an xUnit test project
(`ClipboardManagerWinForm.Tests`) covering the search query parser, text filters, the SQLite repository
and the capture pipeline.
