@echo off
setlocal

echo ==========================================
echo IT Helpdesk Toolkit v3.0 Build
echo ==========================================

python -m pip install -r requirements.txt
if errorlevel 1 goto :error

python -m pip install -U pyinstaller
if errorlevel 1 goto :error

pyinstaller --clean --noconfirm --onefile --windowed --name ITHelpdeskToolkit main.py
if errorlevel 1 goto :error

echo.
echo BUILD COMPLETE
echo EXE: dist\ITHelpdeskToolkit.exe
echo.
pause
exit /b 0

:error
echo.
echo BUILD FAILED.
pause
exit /b 1
