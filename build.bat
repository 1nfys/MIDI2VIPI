@echo off
setlocal enabledelayedexpansion

echo ==============================================
echo       MIDI2VIPI Build Script (C# 5 / .NET 4.8)
echo ==============================================
echo.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at:
    echo %CSC%
    echo Make sure .NET Framework 4.8 is installed.
    pause
    exit /b 1
)

echo [INFO] Compiling MIDI2VIPI.exe using native .NET Framework compiler...

"%CSC%" /nologo /target:winexe /out:MIDI2VIPI.exe /win32icon:app_icon.ico /optimize+ *.cs

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Compilation failed!
    pause
    exit /b %errorlevel%
)

echo [SUCCESS] Successfully compiled MIDI2VIPI.exe (Standalone)!
pause
