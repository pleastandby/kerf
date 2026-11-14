# =====================================================================
#  Kerf 3.1 Beta — Local Install Helper
#  Installs Kerf.exe, creates shortcuts, and associates .kerf files
# =====================================================================

$ErrorActionPreference = "Stop"

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "  Installing Kerf 3.1 Beta" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$localApp = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$targetDir = Join-Path $localApp "Programs\KerfStudio"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find the release executable
$srcExe = Join-Path $scriptDir "..\build-output\release\Kerf.exe"
if (-not (Test-Path $srcExe)) {
    $srcExe = Join-Path $scriptDir "Kerf.exe"
}

if (-not (Test-Path $srcExe)) {
    Write-Host "[ERROR] Kerf.exe not found at: $srcExe" -ForegroundColor Red
    exit 1
}

# 1. Create Target Directory
Write-Host "[*] Creating target directory: $targetDir" -ForegroundColor Yellow
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# 2. Copy Executable
$destExe = Join-Path $targetDir "Kerf.exe"
Write-Host "[*] Copying binary to: $destExe" -ForegroundColor Yellow
Copy-Item -Path $srcExe -Destination $destExe -Force
$srcDll = Join-Path (Split-Path -Parent $srcExe) "kerf_core.dll"
if (Test-Path $srcDll) {
    Copy-Item -Path $srcDll -Destination (Join-Path $targetDir "kerf_core.dll") -Force
}

# 3. Initialize Local Storage
$storageDir = Join-Path $localApp "KerfStudio\Projects"
if (-not (Test-Path $storageDir)) {
    New-Item -ItemType Directory -Path $storageDir -Force | Out-Null
}
Write-Host "[*] Local storage initialized at: $storageDir" -ForegroundColor Green

# 4. Create Desktop Shortcut
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$desktopShortcut = Join-Path $desktop "Kerf.lnk"
Write-Host "[*] Creating Desktop shortcut: $desktopShortcut" -ForegroundColor Yellow

$wshell = New-Object -ComObject WScript.Shell
$sc = $wshell.CreateShortcut($desktopShortcut)
$sc.TargetPath = $destExe
$sc.WorkingDirectory = $targetDir
$sc.Description = "Kerf 3.1 Beta — Vector Design and Animation"
$sc.Save()

# 5. Create Start Menu Shortcut
$startMenu = [Environment]::GetFolderPath([Environment+SpecialFolder]::StartMenu)
$startProgDir = Join-Path $startMenu "Programs\Kerf"
if (-not (Test-Path $startProgDir)) {
    New-Item -ItemType Directory -Path $startProgDir -Force | Out-Null
}
$smShortcut = Join-Path $startProgDir "Kerf.lnk"
Write-Host "[*] Creating Start Menu shortcut: $smShortcut" -ForegroundColor Yellow
$sc2 = $wshell.CreateShortcut($smShortcut)
$sc2.TargetPath = $destExe
$sc2.WorkingDirectory = $targetDir
$sc2.Description = "Kerf 3.1 Beta — Vector Design and Animation"
$sc2.Save()

# 6. Register .kerf File Association in Registry
Write-Host "[*] Registering .kerf file extension association in Windows Registry..." -ForegroundColor Yellow
New-Item -Path "HKCU:\Software\Classes\.kerf" -Force -Value "Kerf.DesignFile" | Out-Null
New-Item -Path "HKCU:\Software\Classes\Kerf.DesignFile" -Force -Value "Kerf Vector Design Document" | Out-Null
New-Item -Path "HKCU:\Software\Classes\Kerf.DesignFile\DefaultIcon" -Force -Value "$destExe,0" | Out-Null
New-Item -Path "HKCU:\Software\Classes\Kerf.DesignFile\shell\open\command" -Force -Value "`"$destExe`" `"%1`"" | Out-Null

Write-Host ""
Write-Host "[SUCCESS] Kerf 3.1 Beta installed successfully!" -ForegroundColor Green
Write-Host "  Application: $destExe" -ForegroundColor Gray
Write-Host "  Desktop:     $desktopShortcut" -ForegroundColor Gray
Write-Host "  File Assoc:  .kerf files now open automatically with Kerf!" -ForegroundColor Gray
Write-Host "=====================================================================" -ForegroundColor Cyan

# Launch
Start-Process $destExe
