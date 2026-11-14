@echo off
setlocal
echo =====================================================================
echo  Building Kerf 3.1 Beta for Windows x64
echo =====================================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT_DIR=..\build-output\release
set OUT_APP=%OUT_DIR%\Kerf.exe
set OUT_SETUP=%OUT_DIR%\KerfSetup.exe
set CORE_DLL=%OUT_DIR%\kerf_core.dll
set WPF_LIB=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at: %CSC%
    exit /b 1
)
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"
if not exist "..\packages\kerf-react\dist\kerf-runtime.iife.js" (
    echo [ERROR] Standalone web runtime bundle is missing. Run npm run build in packages\kerf-react.
    exit /b 1
)

echo [*] Generating multi-resolution icon from assets\Kerf-Logo.svg ...
"%CSC%" /target:exe /platform:x64 /optimize+ /out:"%OUT_DIR%\GenerateKerfIcon.exe" /lib:"%WPF_LIB%" /r:System.dll,System.Core.dll,System.Xml.dll,WindowsBase.dll,PresentationCore.dll,PresentationFramework.dll,System.Xaml.dll tools\GenerateIcon.cs
if errorlevel 1 exit /b 1
"%OUT_DIR%\GenerateKerfIcon.exe" "..\assets\Kerf-Logo.svg" "..\assets\kerf.ico"
if errorlevel 1 exit /b 1
copy /y "..\assets\kerf.ico" "kerf.ico" >nul
copy /y "..\assets\kerf.ico" "..\kerf.ico" >nul
del /q "%OUT_DIR%\GenerateKerfIcon.exe" >nul 2>&1

where.exe cargo >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [*] Building Rust geometry core ...
    cargo build --manifest-path crates\kerf_core\Cargo.toml --release
    if errorlevel 1 (
        echo [WARN] Rust build failed; using the checked-in core if available.
    ) else (
        copy /y crates\kerf_core\target\release\kerf_core.dll "%CORE_DLL%" >nul
        copy /y crates\kerf_core\target\release\kerf_core.dll "kerf_core.dll" >nul
    )
)
if not exist "%CORE_DLL%" if exist "kerf_core.dll" copy /y "kerf_core.dll" "%CORE_DLL%" >nul
if not exist "%CORE_DLL%" (
    echo [ERROR] Native core was not found. Build crates\kerf_core or restore native-windows\kerf_core.dll.
    exit /b 1
)

echo [*] Compiling Kerf.exe with the latest icon and embedded splash image ...
"%CSC%" /target:winexe /platform:x64 /optimize+ /win32icon:"..\assets\kerf.ico" /out:"%OUT_APP%" /resource:"..\assets\kerf.ico",Kerf.Icon /resource:"..\assets\Kerf-loading-image.png",Kerf.LoadingImage /resource:"..\assets\KerfBanner.kfx",Kerf.KerfBanner /resource:"..\packages\kerf-react\dist\kerf-runtime.iife.js",Kerf.ReactRuntime /lib:"%WPF_LIB%" /r:System.dll,System.Core.dll,System.Xaml.dll,System.Web.Extensions.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll /recurse:src\*.cs
if errorlevel 1 (
    echo [FAILED] Compilation of Kerf.exe failed.
    exit /b 1
)

echo [*] Compiling standalone KerfSetup.exe with embedded application payload ...
"%CSC%" /target:winexe /platform:x64 /optimize+ /win32icon:"..\assets\kerf.ico" /out:"%OUT_SETUP%" /resource:"%OUT_APP%",KerfAppExecutable /resource:"%CORE_DLL%",KerfNativeCore /resource:"..\assets\kerf.ico",Kerf.Icon /lib:"%WPF_LIB%" /r:System.dll,System.Core.dll,System.Xaml.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll KerfInstaller.cs InstallerAssemblyInfo.cs src\ProductInfo.cs src\UI\KerfLogo.cs src\UI\KerfBrand.cs
if errorlevel 1 (
    echo [FAILED] Compilation of KerfSetup.exe failed.
    exit /b 1
)

echo.
echo =====================================================================
echo [SUCCESS] Kerf 3.1 Beta, standalone installer, and native core built.
echo   App:       %CD%\%OUT_APP%
echo   Installer: %CD%\%OUT_SETUP%
echo   Core:      %CD%\%CORE_DLL%
echo =====================================================================

if /i "%1"=="--test" (
    echo.
    echo [*] Compiling and running Automated Verification Tests...
    "%CSC%" /target:exe /main:Kerf.Tests.TestRunner /out:"%OUT_DIR%\TestRunner.exe" /platform:x64 /optimize+ /warn:1 /resource:"..\packages\kerf-react\dist\kerf-runtime.iife.js",Kerf.ReactRuntime /lib:"%WPF_LIB%" /r:System.dll,System.Core.dll,WindowsBase.dll,PresentationCore.dll,PresentationFramework.dll,System.Xaml.dll,System.Drawing.dll,System.Xml.dll,System.Web.Extensions.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll /recurse:src\*.cs tests\TestRunner.cs
    if errorlevel 1 (
        echo [FAILED] Compilation of automated verification tests failed.
        exit /b 1
    )
    "%OUT_DIR%\TestRunner.exe"
    set TEST_EXIT=%ERRORLEVEL%
    del /q "%OUT_DIR%\TestRunner.exe" >nul 2>&1
    if not "%TEST_EXIT%"=="0" exit /b %TEST_EXIT%
)

endlocal
