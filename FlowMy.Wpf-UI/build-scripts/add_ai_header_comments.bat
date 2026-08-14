@echo off
chcp 65001 >nul
title FlowMy - Add AI Header Comments to .cs files
echo ========================================================
echo   FLOWMY - CHÈN COMMENT HƯỚNG DẪN AI VÀO FILE .CS
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%add_ai_header_comments.ps1"

echo.
echo Nhấn phím bất kỳ để thoát...
pause >nul
