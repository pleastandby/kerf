# =====================================================================
#  Kerf Studio — Automated Installer Script for Windows
#  Installs KerfStudio.exe, creates shortcuts, and associates .kerf files
# =====================================================================

$ErrorActionPreference = "Stop"

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "  Installing Kerf Vector Studio (DirectX GPU Accelerated)" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$localApp = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$targetDir = Join-Path $localApp "Programs\KerfStudio"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find KerfStudio.exe
$srcExe = Join-Path $scriptDir "..\KerfStudio.exe"
if (-not (Test-Path $srcExe)) {
    $srcExe = Join-Path $scriptDir "KerfStudio.exe"
}

if (-not (Test-Path $srcExe)) {
    Write-Host "[ERROR] KerfStudio.exe not found at: $srcExe" -ForegroundColor Red
    exit 1
}

# 1. Create Target Directory
Write-Host "[*] Creating target directory: $targetDir" -ForegroundColor Yellow
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# 2. Copy Executable
$destExe = Join-Path $targetDir "KerfStudio.exe"
Write-Host "[*] Copying binary to: $destExe" -ForegroundColor Yellow
Copy-Item -Path $srcExe -Destination $destExe -Force

# 3. Initialize Local Storage
$storageDir = Join-Path $localApp "KerfStudio\Projects"
if (-not (Test-Path $storageDir)) {
    New-Item -ItemType Directory -Path $storageDir -Force | Out-Null
}
Write-Host "[*] Local storage initialized at: $storageDir" -ForegroundColor Green

# 4. Create Desktop Shortcut
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$desktopShortcut = Join-Path $desktop "Kerf Studio.lnk"
Write-Host "[*] Creating Desktop shortcut: $desktopShortcut" -ForegroundColor Yellow

$wshell = New-Object -ComObject WScript.Shell
$sc = $wshell.CreateShortcut($desktopShortcut)
$sc.TargetPath = $destExe
$sc.WorkingDirectory = $targetDir
$sc.Description = "Kerf Studio - Native DirectX Vector Design"
$sc.Save()

# 5. Create Start Menu Shortcut
$startMenu = [Environment]::GetFolderPath([Environment+SpecialFolder]::StartMenu)
$startProgDir = Join-Path $startMenu "Programs\Kerf Studio"
if (-not (Test-Path $startProgDir)) {
    New-Item -ItemType Directory -Path $startProgDir -Force | Out-Null
}
$smShortcut = Join-Path $startProgDir "Kerf Studio.lnk"
Write-Host "[*] Creating Start Menu shortcut: $smShortcut" -ForegroundColor Yellow
$sc2 = $wshell.CreateShortcut($smShortcut)
$sc2.TargetPath = $destExe
$sc2.WorkingDirectory = $targetDir
$sc2.Description = "Kerf Studio - Native DirectX Vector Design"
$sc2.Save()

# 6. Register .kerf File Association in Registry
Write-Host "[*] Registering .kerf file extension association in Windows Registry..." -ForegroundColor Yellow
New-Item -Path "HKCU:\Software\Classes\.kerf" -Force -Value "Kerf.DesignFile" | Out-Null
New-Item -Path "HKCU:\Software\Classes\Kerf.DesignFile" -Force -Value "Kerf Vector Design Document" | Out-Null
New-Item -Path "HKCU:\Software\Classes\Kerf.DesignFile\DefaultIcon" -Force -Value "$destExe,0" | Out-Null
New-Item -Path "HKCU:\Software\Classes\Kerf.DesignFile\shell\open\command" -Force -Value "`"$destExe`" `"%1`"" | Out-Null

Write-Host ""
Write-Host "[SUCCESS] Kerf Studio installed successfully!" -ForegroundColor Green
Write-Host "  Application: $destExe" -ForegroundColor Gray
Write-Host "  Desktop:     $desktopShortcut" -ForegroundColor Gray
Write-Host "  File Assoc:  .kerf files now open automatically with Kerf Studio!" -ForegroundColor Gray
Write-Host "=====================================================================" -ForegroundColor Cyan

# Launch
Start-Process $destExe
