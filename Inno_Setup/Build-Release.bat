@echo off
echo ============================================
echo  Building FlowMy - Self-Contained
echo ============================================
echo.

set BUILD_CONFIG=Release
set RUNTIME=win-x64
set MAIN_APP=..

echo [1/3] Cleaning previous builds...
rmdir /s /q "%MAIN_APP%\bin\%BUILD_CONFIG%" 2>nul
echo [OK] Cleaned
echo.


echo [2/3] Building App (WPF - Executable)...
cd "%MAIN_APP%"
dotnet publish -c %BUILD_CONFIG% -r %RUNTIME% --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false
if %errorlevel% neq 0 (
    echo [ERROR] Failed to build FlowMy
    pause
    exit /b 1
)

echo [3/3] Checking output files...
if exist "%MAIN_APP%\bin\%BUILD_CONFIG%\net8.0-windows\%RUNTIME%\publish\FlowMy.exe" (
    echo [OK] FlowMy.exe found
) else (
    echo [ERROR] FlowMy.exe not found!
)


echo.
echo ============================================
echo  Build completed!
echo ============================================
echo.
echo Output files are located at:
echo   Main App:  %MAIN_APP%\bin\%BUILD_CONFIG%\net8.0-windows\%RUNTIME%\publish\
echo.
pause