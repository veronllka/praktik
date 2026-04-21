@echo off
setlocal

cd /d "%~dp0"

set "PYTHON_EXE=%~dp0..\.venv\Scripts\python.exe"

if exist "%PYTHON_EXE%" (
    "%PYTHON_EXE%" -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
) else (
    python -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
)

endlocal
