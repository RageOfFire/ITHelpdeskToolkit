@echo off
setlocal

set VERSION=v3.1.1

echo ==========================================
echo IT Helpdesk Toolkit %VERSION% Build
echo ==========================================

python -m pip install -r requirements.txt
if errorlevel 1 goto :error

python -m pip install -U pyinstaller
if errorlevel 1 goto :error

pyinstaller --clean --noconfirm --onefile --windowed --hidden-import=openpyxl --hidden-import=xlrd --name ITHelpdeskToolkit_%VERSION% main.py
if errorlevel 1 goto :error

echo.
echo BUILD COMPLETE
echo EXE: dist\ITHelpdeskToolkit_%VERSION%.exe
echo.
pause
exit /b 0

:error
echo.
echo BUILD FAILED.
pause
exit /b 1
