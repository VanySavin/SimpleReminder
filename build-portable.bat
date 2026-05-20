@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

echo.
echo  SimpleReminder - portable-сборка (без установки .NET на другом ПК)
echo  ================================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ОШИБКА] Команда dotnet не найдена. Установите .NET 8 SDK.
    echo          https://dotnet.microsoft.com/download
    goto :fail
)

set "PROJECT=src\ReminderApp\ReminderApp.csproj"
set "OUT=publish\SimpleReminder-win-x64-portable"

if not exist "%PROJECT%" (
    echo [ОШИБКА] Не найден проект: %PROJECT%
    goto :fail
)

if exist "%OUT%" (
    echo Удаляю старую папку publish...
    rmdir /s /q "%OUT%"
)

echo Публикация (это может занять 20-40 секунд)...
dotnet publish "%PROJECT%" -c Release -p:PublishProfile=Portable-win-x64 -o "%OUT%"
if errorlevel 1 (
    echo.
    echo [ОШИБКА] dotnet publish завершился с ошибкой.
    goto :fail
)

if not exist "%OUT%\SimpleReminder.exe" (
    echo [ОШИБКА] SimpleReminder.exe не создан.
    goto :fail
)

copy /y "README_PORTABLE.txt" "%OUT%\README.txt" >nul

echo.
echo  ГОТОВО
echo  ------
echo  Папка:  %CD%\%OUT%
echo  Запуск: %OUT%\SimpleReminder.exe
echo.

powershell -NoProfile -Command ^
  "$p='%OUT%\SimpleReminder.exe'; $mb=[math]::Round((Get-Item $p).Length/1MB,1); Write-Host ('  Размер exe: {0} MB' -f $mb); if($mb -lt 50){Write-Host '  ВНИМАНИЕ: файл слишком маленький (~170 KB) - это НЕ portable!' -ForegroundColor Red; exit 1} else {Write-Host '  Portable OK - .NET на другом ПК ставить не нужно.' -ForegroundColor Green}"
if errorlevel 1 goto :fail

echo.
echo  Скопируйте на флешку/другой ПК всю папку:
echo  SimpleReminder-win-x64-portable
echo.
pause
exit /b 0

:fail
echo.
pause
exit /b 1
