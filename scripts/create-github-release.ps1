# Создаёт GitHub Release v1.0.0 и прикрепляет portable zip.
# Требуется: GitHub CLI (gh) и авторизация: gh auth login

param(
    [string]$Tag = "v1.0.0",
    [string]$Zip = "publish\SimpleReminder-v1.0.0-win-x64-portable.zip",
    [string]$NotesFile = "docs\release-notes\v1.0.0.md"
)

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "Установите GitHub CLI: winget install GitHub.cli  затем: gh auth login"
}

if (-not (Test-Path $Zip)) {
    Write-Error "Не найден архив: $Zip. Сначала соберите: .\build-portable.bat и создайте zip."
}

$notes = Get-Content $NotesFile -Raw -Encoding UTF8
gh release create $Tag $Zip --title "SimpleReminder $Tag" --notes $notes
Write-Host "Готово: https://github.com/VanySavin/SimpleReminder/releases/tag/$Tag"
