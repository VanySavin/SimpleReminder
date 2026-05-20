# SimpleReminder (WPF, .NET 8)

Portable-приложение для Windows: локальные напоминания с вводом естественным текстом на русском языке.

## Для пользователей

Сборка для запуска без установки — см. [`README_PORTABLE.txt`](README_PORTABLE.txt) и папку `publish/SimpleReminder-win-x64-portable/`.

Данные хранятся рядом с exe в `data/` (`reminders.json`, `settings.json`).

## Возможности

- Ввод напоминаний текстом: `через 5 минут`, `завтра в 9`, `в понедельник`, `суп через 5 минут`, составное время и др.
- Список активных напоминаний без ограничения по количеству.
- Всплывающие уведомления с очередью, отложением и автозапуском.
- Настройки: тема, позиции окон, кнопки отложения, автозапуск с Windows.
- Работа в системном трее.

## Структура проекта

- `src/ReminderApp/` — основное приложение
- `tests/ReminderApp.Tests/` — unit-тесты парсера
- `publish-portable-win-x64.ps1` — сборка portable-версии

## Сборка portable (для переноса на другой ПК)

**Не путать с Build в Visual Studio** — `bin\Release\` (~170 КБ exe) требует установленный .NET.

Portable-сборка (~70 МБ, .NET внутри):

```bat
build-portable.bat
```

Подробнее: [`КАК_СОБРАТЬ.txt`](КАК_СОБРАТЬ.txt)

Результат: `publish\SimpleReminder-win-x64-portable\`

## Сборка из исходников (разработка)

```powershell
dotnet build src/ReminderApp/ReminderApp.csproj -c Release
dotnet test tests/ReminderApp.Tests -c Release
.\publish-portable-win-x64.ps1
```

## Разработка

```powershell
dotnet run --project src/ReminderApp/ReminderApp.csproj
```
