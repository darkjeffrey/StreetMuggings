@echo off
setlocal
rem Builds Street Muggings (Release) and installs it into the Bannerlord Modules folder.
rem Double-click it, or run it from any folder: it always builds the project next to this file.

cd /d "%~dp0"
echo Building Street Muggings...
echo.

dotnet build -c Release
set "RESULT=%ERRORLEVEL%"

echo.
if "%RESULT%"=="0" (
    echo BUILD SUCCEEDED - the mod was copied to your Bannerlord Modules\StreetMuggings folder.
) else (
    echo BUILD FAILED with exit code %RESULT%. Scroll up for the errors.
)
echo.
set /p DUMMY=Press Enter to close...
endlocal
