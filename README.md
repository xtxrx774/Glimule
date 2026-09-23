# Vexqlyn

Индикатор Caps Lock и раскладки у текстового курсора в Windows — капсула в стиле macOS, прямо в поле ввода.

## Что делает

- Показывает Caps Lock и переключение языка (A / РУ) у каретки
- Прячет системный переключатель раскладки Windows в текстовых полях
- Работает в обычных полях ввода, браузерах, Word и Excel
- Не появляется в деревьях, списках и панелях вроде NVIDIA Control Panel
- Масштаб капсулы меняется на лету, без перезапуска

## Запуск

Нужны Windows 10/11 и [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build -c Release
Start-Process .\bin\Release\net8.0-windows\CursorUI.exe
```

Или скопируйте сборку в `%LOCALAPPDATA%\CursorUI` и запускайте оттуда. В трее: **Настройки** и **Выход**.
