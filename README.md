# Glimule

A tiny Windows tray HUD that shows **Caps Lock** and the **keyboard layout** next to the text caret.

![Glimule](preview.gif)

When you type, a capsule appears at the insertion point: an outlined Caps Lock glyph while Caps is on, and a sliding **A | РУ** (or your other layouts) picker when you switch languages. The indicator follows the focused text field and stays out of the way otherwise.

Works in ordinary edit boxes, browsers, Word, and Excel. Built with WPF on .NET 8.

## Features

- Capsule overlay anchored to the caret, not the screen corner
- Caps Lock shown only when you stop typing
- Language-switch animation with a sliding selection pill
- Optional hide of the stock Windows language overlay while a text field is focused
- Live scale (50–200%) from the tray, no restart
- Run at startup, from the tray menu

## Run

Requires Windows 10/11 and [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build -c Release
Start-Process .\bin\Release\net8.0-windows\CursorUI.exe
```

Quit from the tray icon (**Выход**). Settings are under **Настройки**.

## По-русски

Утилита в трее: капсула у текстового курсора показывает Caps Lock и текущую раскладку (A / РУ и другие). Появляется только в полях ввода. Выход и настройки — в меню иконки в трее.
