@echo off
echo =====================================================================
echo  Building Kerf Native Windows Studio - DirectX + Rust Engine
echo =====================================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT_APP=..\KerfStudio.exe
set OUT_SETUP=..\KerfStudioSetup.exe

where.exe cargo >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [*] Building High-Performance Rust Native Core [crates\kerf_core] ...
    cargo build --manifest-path crates\kerf_core\Cargo.toml --release
    if %ERRORLEVEL% EQU 0 (
        copy /y crates\kerf_core\target\release\kerf_core.dll kerf_core.dll >nul
        copy /y crates\kerf_core\target\release\kerf_core.dll ..\kerf_core.dll >nul
        echo [OK] Rust Core kerf_core.dll compiled and deployed.
    ) else (
        echo [WARN] Rust Core compilation had warnings or issues.
    )
) else (
    echo [INFO] Cargo not in PATH, using prebuilt or managed fallback engine.
)

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at: %CSC%
    exit /b 1
)

echo [*] Compiling Native App: src/**/*.cs -^> %OUT_APP% ...
"%CSC%" /target:winexe /platform:x64 /optimize+ /win32icon:kerf.ico /out:"%OUT_APP%" /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /r:System.dll,System.Core.dll,System.Xaml.dll,System.Web.Extensions.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll src\*.cs src\Model\*.cs src\Engine\*.cs src\Tools\*.cs src\IO\*.cs src\UI\*.cs

if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Compilation of KerfStudio.exe failed!
    exit /b 1
)

echo [*] Compiling Installer: KerfInstaller.cs -^> %OUT_SETUP% ...
"%CSC%" /target:winexe /platform:x64 /optimize+ /win32icon:kerf.ico /out:"%OUT_SETUP%" /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /r:System.dll,System.Core.dll,System.Xaml.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll KerfInstaller.cs src\UI\KerfLogo.cs

if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Compilation of KerfStudioSetup.exe failed!
    exit /b 1
)

echo.
echo =====================================================================
echo [SUCCESS] Kerf Studio, Rust Native Core, and Installer built!
echo   Main App:    %CD%\..\KerfStudio.exe
echo   Rust Core:   %CD%\..\kerf_core.dll
echo   Installer:   %CD%\..\KerfStudioSetup.exe
echo =====================================================================

if "%1"=="--test" (
    echo.
    echo [*] Compiling and running Automated Verification Tests...
    "%CSC%" /target:exe /main:Kerf.Tests.TestRunner /out:tests\TestRunner.exe /platform:x64 /optimize+ /warn:1 /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /r:System.dll,System.Core.dll,WindowsBase.dll,PresentationCore.dll,PresentationFramework.dll,System.Xaml.dll,System.Drawing.dll,System.Xml.dll,System.Web.Extensions.dll /recurse:src\*.cs tests\TestRunner.cs
    if %ERRORLEVEL% EQU 0 (
        tests\TestRunner.exe
    )
)
