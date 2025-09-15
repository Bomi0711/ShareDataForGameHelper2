@echo off
echo Building ShareData2 Plugin...
echo.

dotnet build --configuration Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo DLL location: bin\Release\net8.0\ShareData2.dll
    echo.
    echo The DLL will be automatically copied to GameHelper2 plugins directory.
    echo.
) else (
    echo.
    echo Build failed!
    echo.
)

pause
