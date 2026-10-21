@echo off
echo =====================================================================
echo  Building Kerf Native Windows Studio (DirectX GPU Accelerated)
echo =====================================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=..\KerfStudio.exe

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at: %CSC%
    exit /b 1
)

echo [*] Compiling native-windows\KerfStudio.cs -^> %OUT% ...
"%CSC%" /target:winexe /platform:x64 /optimize+ /out:"%OUT%" /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /r:System.dll,System.Core.dll,System.Xaml.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll KerfStudio.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] KerfStudio.exe built successfully!
    echo Location: %CD%\..\KerfStudio.exe
) else (
    echo.
    echo [FAILED] Compilation failed.
)
