$ErrorActionPreference = "Stop"

Set-Location -LiteralPath $PSScriptRoot

$pythonExe = Join-Path $PSScriptRoot "..\.venv\Scripts\python.exe"

if (Test-Path -LiteralPath $pythonExe) {
    & $pythonExe -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
} else {
    & python -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
}
