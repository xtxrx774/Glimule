# Glimule

## macOS-style keyboard layout and Caps Lock HUD for Windows

![Glimule](preview.gif)

On macOS Sonoma and Tahoe, switching the keyboard layout or Caps Lock shows a small capsule next to the text caret. Glimule brings that same mechanic to Windows.

The capsule appears only in real text fields, follows the caret across monitors, and does not replace the system caret or Windows input controls. Layouts are labeled the macOS way: English as **A**, Russian as **РУ**, and other languages in native script or ISO code. The app runs from the tray; settings stay in English regardless of the system language.

## Features

- Shows the current keyboard layout next to the caret
- Displays the Caps Lock state with a dedicated indicator
- Smooth animated transitions when switching layouts
- Supports text fields in desktop applications, browsers, Word, and Excel
- Per-monitor DPI support
- Adjustable indicator scale from 50% to 200%
- Optional Windows startup launch
- Runs quietly from the notification area

## Installation

Download `Glimule.exe` from [Releases](https://github.com/xtxrx774/Glimule/releases) and run it.

Glimule requires Windows 10 or later (64-bit).

## Build

The project uses WPF and WinForms on .NET 8:

```powershell
dotnet build -c Release
```

The executable is written to `bin\Release\net8.0-windows\Glimule.exe`.

## Usage

Launch `Glimule.exe`. The application runs in the notification area. Open the tray menu to change the scale, configure startup, open settings, or exit.
