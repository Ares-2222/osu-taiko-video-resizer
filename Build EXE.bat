@echo off
setlocal
title Building osu!taiko Video Resizer 2 - made by Ares
cd /d "%~dp0"

echo osu!taiko Video Resizer 2 - made by Ares
echo.
echo Looking for the C# compiler that ships with Windows...
set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not defined CSC (
    echo.
    echo Could not find csc.exe.
    echo It normally lives in:
    echo   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
    echo If it is missing, install the .NET Framework 4.8 runtime, or just
    echo use the "Resize video (drag and drop).bat" script instead - it does
    echo exactly the same conversion.
    echo.
    pause
    exit /b 1
)

echo Found: %CSC%
echo.
echo Compiling...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"osu!taiko Video Resizer.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "src\Resizer.cs"

if errorlevel 1 (
    echo.
    echo Build FAILED. Copy the errors above and send them over.
    echo In the meantime the .bat scripts work fine on their own.
    echo.
    pause
    exit /b 1
)

echo.
echo Built "osu!taiko Video Resizer.exe" in this folder.
echo You can delete the src folder and this build script afterwards if you like.
echo.
pause
exit /b
