# Glimule

Tray app for Windows. Puts the current keyboard layout and Caps Lock next to the text caret, the same place macOS uses for that HUD (Sonoma / Tahoe).

![Glimule](preview.gif)

Regular Windows indicators sit in a corner or come up as a separate popup. This one follows the caret and only shows up in real edit fields (editors, browsers, Word, Excel, etc).

English is **A**, Russian is **РУ**. Other layouts get a native glyph or the ISO code. Caps has its own blue pill and waits until you stop typing.

Settings stay in English. Scale slider is 50–200%. Optional autostart.

## Inspired by macOS Tahoe and Sonoma

The idea comes from the small input HUD in macOS Tahoe and Sonoma. When the keyboard layout changes, macOS briefly shows a rounded capsule beside the caret instead of moving your attention to a corner of the screen.

Glimule recreates that interaction on Windows: the capsule follows the caret, the layout switch uses a sliding accent, and Caps Lock gets the same compact treatment. It is an independent Windows app and does not replace the native input system.

## Install

Get `Glimule.exe` from [Releases](https://github.com/xtxrx774/Glimule/releases). 64-bit Windows 10+.

## Build

.NET 8 + WPF:

```powershell
dotnet build -c Release
```

Binary lands in `bin\Release\net8.0-windows\Glimule.exe`.

## Usage

Start the exe. Icon is in the notification area — right-click for settings or exit.
