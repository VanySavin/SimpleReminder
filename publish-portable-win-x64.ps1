# Portable self-contained build: SimpleReminder win-x64 (single folder to zip).
# Run from repo root: .\publish-portable-win-x64.ps1

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "src\ReminderApp\ReminderApp.csproj"
$OutDir = Join-Path $Root "publish\SimpleReminder-win-x64-portable"
$ReadmeSrc = Join-Path $Root "README_PORTABLE.txt"

if (-not (Test-Path $Project)) {
    Write-Error "Project not found: $Project"
}

if (-not (Test-Path $ReadmeSrc)) {
    Write-Error "README_PORTABLE.txt not found: $ReadmeSrc"
}

if (Test-Path $OutDir) {
    Remove-Item -Recurse -Force $OutDir
}

dotnet publish $Project `
    -c Release `
    -p:PublishProfile=Portable-win-x64 `
    -o $OutDir

$exe = Join-Path $OutDir "SimpleReminder.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Publish failed: SimpleReminder.exe not found"
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
if ($sizeMb -lt 50) {
    Write-Error "SimpleReminder.exe is only ${sizeMb} MB - expected ~70+ MB self-contained build. .NET runtime is NOT bundled."
}

Copy-Item -Path $ReadmeSrc -Destination (Join-Path $OutDir "README.txt") -Force

Write-Host ("Done: " + $OutDir)
Write-Host ("Run: " + $exe)
Write-Host ("Size: ${sizeMb} MB - portable, no .NET install required")
