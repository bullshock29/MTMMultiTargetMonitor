# MTM — Multi Target Monitor

A small WPF app for creating and editing `.rdp` files with **per-monitor** selection.

The built-in Remote Desktop client only lets you pick *one* monitor or *all* monitors. The `.rdp`
file format actually supports an arbitrary subset via the `selectedmonitors` field — this tool gives
that a UI.

## Build & run

```
dotnet build -c Debug
dotnet run --project src/MultiTargetMonitor
```

Or publish a portable exe:

```
dotnet publish src/MultiTargetMonitor -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Requires the .NET 9 Desktop Runtime.

## Using it

- **General tab** – computer name and (optional) user name.
- **Display tab** – tick *Use all of my monitors*, or click monitors in the diagram / list to pick a
  specific set. The number drawn on each monitor is its `selectedmonitors` id.
- **Save** (header) writes the `.rdp`. **Connect** (footer) saves and launches `mstsc.exe`.
- **⋯ menu → Preview .rdp file** shows the exact file that will be written.
- Existing `.rdp` files are loaded with every key preserved — the tool only rewrites the
  monitor-related lines plus `full address` / `username`.

### What gets written

| Situation | Keys |
| --- | --- |
| Use all monitors | `use multimon:i:1` (no `selectedmonitors`) |
| Specific monitors | `use multimon:i:1` + `selectedmonitors:s:<ids>` |
| Nothing selected | `use multimon:i:0` |

`screen mode id:i:2` and `span monitors:i:0` are always set. No fixed resolution is written — each
monitor uses its native resolution. When the primary monitor is in the selection it is placed first
in the list (it becomes the session's primary).

For a **multi-monitor** selection the tool also forces `dynamic resolution:i:0` and
`smart sizing:i:0`. Both of those make the client render one resizable/scaled desktop surface, which
overrides `selectedmonitors` and appears as a single desktop stretched across the monitors. A
single-monitor selection keeps `dynamic resolution:i:1`.

### Validation

The Display tab warns when a selection can misbehave in mstsc:

- **Error** – the monitors are not edge-adjacent (RDP needs a contiguous block).
- **Warning** – the selection is L-shaped / has a gap in its bounding box.
- **Warning** – the Windows primary monitor is not included.

## Monitor id mapping

Ids come from `EnumDisplayMonitors` (0-based enumeration order). They should match the numbers shown
by `mstsc.exe /l`. To print the mapping for the current machine:

```
MultiTargetMonitor.exe --list-monitors
```

(run from a console — it attaches to the parent console for output).

## Appearance

Light and dark themes with a teal accent. The theme control (half-circle icon, top right) offers
**Light**, **Dark**, or **Match Windows** (follows the OS app theme live). The choice is saved to
`%LOCALAPPDATA%\MultiTargetMonitor\settings.json`. The OS title bar is themed to match on Windows 10
20H1+ / Windows 11.

Palettes live in `Themes/Palette.Light.xaml` / `Palette.Dark.xaml`; control styling is in
`Themes/Controls.xaml`. `ThemeManager` swaps the active palette dictionary at runtime.

## Icon

`src/MultiTargetMonitor/Assets/app.ico` (7 frames, 16–256 px) is the exe / taskbar / window icon and
also drives the Explorer "Open with" entry. To regenerate it from the source art:

```
pwsh tools/make-ico.ps1
```

That reads `tools/app-source.jpg`, autocrops the white margin, and rewrites `app.ico` +
`app-256.png` (the latter is the in-app header logo).

## Windows integration

**⋯ menu → Add to Windows "Open with" menu** registers the app under
`HKCU\Software\Classes\Applications\MultiTargetMonitor.exe` and adds a right-click verb for `.rdp` files
(on Windows 11 it appears under *Show more options*). Fully reversible from the same menu; no admin
rights, no installer.

**⋯ menu → Set as default for .rdp files…** opens the Windows "how do you want to open this?" dialog
for a file you pick — Windows does not allow an app to claim a default handler silently.

## Project layout

```
src/MultiTargetMonitor/
  Models/            RdpFile (round-trip parser), RdpMonitor
  Services/          MonitorEnumerator, MonitorWriter, SelectionValidator,
                     ShellIntegration, RdpLauncher, NativeConsole,
                     ThemeManager, AppSettings, WindowChrome
  ViewModels/        MainViewModel, MonitorRow
  Controls/          MonitorLayoutControl (the clickable diagram)
  Themes/            Palette.Light.xaml, Palette.Dark.xaml, Controls.xaml
  MainWindow.xaml    General + Display tabs
```
