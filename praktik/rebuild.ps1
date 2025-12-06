# Скрипт для пересборки проекта praktik

Write-Host "Закрытие Visual Studio..." -ForegroundColor Yellow
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Очистка временных файлов..." -ForegroundColor Yellow
Remove-Item -Path ".\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\obj" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Пересборка проекта..." -ForegroundColor Green
dotnet build praktik.csproj --configuration Debug

Write-Host "`nГотово! Откройте Visual Studio и запустите проект." -ForegroundColor Green
Write-Host "Или нажмите F5 в Visual Studio для запуска." -ForegroundColor Cyan
