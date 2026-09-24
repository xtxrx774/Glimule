# Glimule

## Keyboard layout and Caps Lock indicator for Windows

![Glimule](preview.gif)

Glimule is a lightweight tray utility that displays the active keyboard layout and Caps Lock state beside the text caret. It provides a compact visual cue while you work in text fields without replacing the standard Windows input controls.

The indicator follows the focused caret and adapts to the current monitor, making the active layout visible where it matters: next to the text you are editing.

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

## По-русски

Glimule — небольшая утилита в системном трее для Windows. Она показывает текущую раскладку клавиатуры и состояние Caps Lock рядом с текстовым курсором. Поддерживаются обычные поля ввода, браузеры, Word и Excel; масштаб индикатора можно настроить в пределах 50–200%.
