@echo off
setlocal

cd /d "%~dp0"

echo Running unit tests...
echo.

dotnet test ".\praktik.Tests\praktik.Tests.csproj"

echo.
if errorlevel 1 (
    echo Tests finished with errors.
) else (
    echo Tests finished successfully.
)

pause
