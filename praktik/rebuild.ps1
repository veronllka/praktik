# Rebuild Script for 'praktik'

Write-Host "Closing Visual Studio..." -ForegroundColor Yellow
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Cleaning temporary files..." -ForegroundColor Yellow
if (Test-Path ".\bin") { Remove-Item -Path ".\bin" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path ".\obj") { Remove-Item -Path ".\obj" -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "Rebuilding project..." -ForegroundColor Green
# Attempting to build with explicit x64 architecture to avoid MSB4216 x86 error
dotnet build praktik.csproj --configuration Debug --arch x64 /p:GenerateResourceUsePreserializedResources=true

Write-Host "`nDone! Please open Visual Studio and run the project (F5)." -ForegroundColor Green
