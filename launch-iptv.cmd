@echo off
setlocal EnableExtensions

set "REPO_ROOT=%~dp0"
set "APP_PROJECT=%REPO_ROOT%src\Iptv.App\Iptv.App.csproj"

if /I "%~1"=="--help" goto :usage
if /I "%~1"=="-h" goto :usage
if /I "%~1"=="/?" goto :usage

if not exist "%APP_PROJECT%" (
    echo ERROR: Could not find the IPTV app project:
    echo   %APP_PROJECT%
    exit /b 2
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet was not found on PATH. Install the .NET 10 SDK/runtime, then try again.
    exit /b 2
)

pushd "%REPO_ROOT%" >nul
if errorlevel 1 (
    echo ERROR: Could not switch to repository root: %REPO_ROOT%
    exit /b 2
)

if "%~1"=="" (
    dotnet run --project "%APP_PROJECT%"
    goto :done
)

set "FIRST_ARG=%~1"
if "%~2"=="" if /I "%FIRST_ARG:~0,7%"=="http://" (
    dotnet run --project "%APP_PROJECT%" -- --playlist-url "%~1"
    goto :done
)
if "%~2"=="" if /I "%FIRST_ARG:~0,8%"=="https://" (
    dotnet run --project "%APP_PROJECT%" -- --playlist-url "%~1"
    goto :done
)
if "%~2"=="" if exist "%~1" (
    dotnet run --project "%APP_PROJECT%" -- --playlist-file "%~f1"
    goto :done
)

dotnet run --project "%APP_PROJECT%" -- %*

:done
set "EXIT_CODE=%ERRORLEVEL%"
popd >nul
if not "%EXIT_CODE%"=="0" (
    echo IPTV Viewer exited with code %EXIT_CODE%.
)
exit /b %EXIT_CODE%

:usage
echo IPTV Viewer launcher
echo.
echo Usage:
echo   launch-iptv.cmd
echo   launch-iptv.cmd https://www.apsattv.com/xumo.m3u
echo   launch-iptv.cmd assets\sample-playlists\duplicate-channels.m3u
echo   launch-iptv.cmd --playlist-url https://www.apsattv.com/xumo.m3u
echo.
echo A single http/https URL maps to --playlist-url. A single existing file maps to --playlist-file.
echo Other arguments are forwarded to the app unchanged.
exit /b 0
