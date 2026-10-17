# FlyVideos

A tiny, keyboard-first video viewer for Windows, inspired by [Fly Photos](https://github.com/riyasy/FlyPhotos) by [riyasy](https://github.com/riyasy): instant open, minimal chrome, everything one keypress away.

Built with C# / WPF on .NET.

## Download

Grab the latest **`FlyVideos-win-x64.zip`** from the [Releases](https://github.com/Evilironman/FlyVideos/releases) page — no .NET runtime required. Unzip and run `FlyVideos.exe`. Windows 10/11 (64-bit).

## Features

### Playback
- Instant open and auto-play — double-click a video and it's on screen
- Plays the whole folder as a playlist, in natural filename order
- Auto-advances to the next video when one ends (wraps around)
- Repeat modes — **All / One / Off** — press `R` to cycle
- Playback speed **0.25x – 4x** — press `S` for the +/− panel, `[` and `]` to nudge by 0.1x, `0` to reset
- Volume and mute with on-screen feedback
- Seek with `J` / `L` (∓10 s), `Home` / `End`, or by scrubbing the timeline
- Fullscreen with `F`; exiting restores your exact previous window size and position

### Browsing
- `A` / `D` for previous / next video (mouse wheel does the same)
- Thumbnail strip along the bottom: every video in the folder shown as a real frame, extracted from Windows' own thumbnail engine
  - The current video is highlighted and kept auto-centered
  - Click any thumbnail to jump straight to that video
- Timeline lives in its own strip below the video, so it never covers the picture

### Interface
- Borderless, dark, minimal — the window stays at the size you set it, never resizes per video
- Drag anywhere on the window to move it
- Single click toggles play/pause, double-click toggles fullscreen
- Volume OSD, status toasts and the speed panel all fade out on their own
- Drag & drop a video onto the window to open it

## Keyboard shortcuts

| Key | Action |
|---|---|
| `A` / `D` | Previous / next video |
| `PgUp` / `PgDn` | Previous / next video |
| Mouse wheel | Previous / next video |
| `Space` / `K` | Play / pause |
| `J` / `L` | Seek −10 s / +10 s |
| `Home` / `End` | Jump to start / end |
| `Up` / `Down` | Volume |
| `M` | Mute |
| `F` / double-click | Fullscreen (`Esc` or `F` to exit) |
| `R` | Repeat mode: All → One → Off |
| `S` | Show speed controls (auto-hides) |
| `[` / `]` | Speed −0.1x / +0.1x |
| `0` | Reset speed to 1x |
| `O` | Open a video (the folder becomes the playlist) |
| `Esc` | Exit fullscreen, or close the app |

## Supported formats

Anything Windows Media Foundation can decode out of the box — H.264 MP4/MKV, WMV, AVI and friends. HEVC/H.265 requires Microsoft's free "HEVC Video Extensions" from the Store.

## Build & run

Requires Windows 10/11 and the [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) (or the SDK to build).

```
dotnet publish -c Release
```

Run the generated `FlyVideos.exe` from `bin\Release\net10.0-windows\publish\`.

### Install (optional)

No installer needed. To wire it into Explorer's right-click menu:

1. Right-click any video → **Open with** → **Choose another app** → **FlyVideos** → *Always*

Or press `O` / drag & drop anything onto the window and browse from there.

## Credits

- Inspired by [Fly Photos](https://github.com/riyasy/FlyPhotos) by [riyasy](https://github.com/riyasy) — a blazing-fast, ultra-light photo viewer for Windows. FlyVideos is an independent, video-focused take on the same idea.

## License

Apache-2.0 — see [LICENSE](LICENSE).
