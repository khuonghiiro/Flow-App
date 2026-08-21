@echo off
chcp 65001 >nul 2>&1
echo ====================================================
echo  📁 FLOWMY - TỰ ĐỘNG KHỞI TẠO CÂY THƯ MỤC ASSETS CHUẨN
echo ====================================================
echo.
echo  Đang tạo 1 thư mục chuẩn duy nhất cho mỗi danh mục...
echo.

if "%~1"=="" (
    node "%~dp0scripts\create_asset_folders.js" vi --clean
) else (
    node "%~dp0scripts\create_asset_folders.js" %*
)

echo.
echo  ✅ Hoàn tất! Cây thư mục chuẩn đã sẵn sàng.
echo.
pause
