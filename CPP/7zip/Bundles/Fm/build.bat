@echo off
setlocal

set VS_PATH=D:\Program Files\Microsoft Visual Studio\18\Insiders
set VCVARS=%VS_PATH%\VC\Auxiliary\Build\vcvarsall.bat

if not exist "%VCVARS%" (
    echo Error: VS 2026 not found at %VS_PATH%
    echo Please update VS_PATH in this script
    exit /b 1
)

call "%VCVARS%" x64

cd /d "%~dp0..\..\..\7zip\Bundles\Fm"

echo Building 7zFM...
nmake /f makefile NEW_COMPILER=1

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: x64\7zFM.exe
) else (
    echo.
    echo Build failed!
)

endlocal
