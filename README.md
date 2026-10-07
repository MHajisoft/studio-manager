# Studio Manager

WPF desktop studio console (.NET 10) that replaces the Delphi **HCP_64** IP CCTV desk and embeds the **sound-recorder** workflow as inline multi-instance rows.

## Features

- **Single studio desk** — live RTSP preview (LibVLC), video record/snapshot (FFmpeg), ONVIF PTZ, presets, USB export of today’s media
- **Compact sound recorders** — strip under transport (source title, timer, icon, EQ under title); hover tooltip; click opens modal metadata; stop saves and removes the row
- **Secondary monitors** — optional fullscreen preview on monitor 2 / 3
- **Localization** — `fa-IR` (RTL, default) and `en-US` (LTR), switchable from the status bar; IRANSans(FaNum) font from sound-recorder

## Solution layout

```
StudioManager.slnx
src/
  StudioManager.App/     # WPF UI, localization, LibVLC/NAudio hosts
  StudioManager.Core/    # Camera/audio/media contracts + services
```

## Requirements

- .NET 10 SDK
- `ffmpeg` on `PATH` (record / snapshot)
- Network access to the camera (RTSP + ONVIF)
- Windows (WPF)

## Run

```bash
dotnet run --project src/StudioManager.App
```

## Configuration

Edit `src/StudioManager.App/appsettings.json`:

| Section | Purpose |
|---------|---------|
| `Studio:Ui:Culture` | `fa-IR` or `en-US` |
| `Studio:Camera` | Host, RTSP/ONVIF ports, credentials, video/snapshot folders |
| `Studio:Audio` | Save path, sample rate, MP3 bitrate |
| `Studio:MediaFolders` | Folders scanned for “Export today → USB” |

## Localization

- Resources: `Localization/Strings.resx` (English), `Localization/Strings.fa-IR.resx` (Persian)
- Runtime manager: `Services/LocalizationManager` — updates strings and `FlowDirection` (RTL/LTR)
- Add new UI text as a resx key in **both** files; bind with `{Binding [Key]}` or `_loc["Key"]` / `_loc.Format(...)`

## Font

`Assets/Fonts/IRANSans(FaNum).ttf` is embedded and applied app-wide (same pack URI pattern as sound-recorder).

## Visual theme

Cool mid-contrast dark desk (slate neutrals, deep teal Live/CTA, deep red Record — white labels).
No bright cream/mustard fills; ComboBox popups are fully themed dark; preview stays dark until Live.
Tokens: `App.xaml` (`Shell*` brushes).

## Agent skills

WPF UI work in this repo should follow the personal Cursor skill **`wpf-app-design`**
(`~/.cursor/skills/wpf-app-design`): tokenized themes, RTL/LTR + resx, studio-desk layout,
and code-first geometry when needed. Vendor packs (Syncfusion/DevExpress skills) only if
those NuGets are added later.

## Related sources

- `HCP_64/` — original Delphi CCTV/PTZ reference (not part of the build)
- `_ref/sound-recorder/` — local clone of the previous WPF recorder (gitignored)
