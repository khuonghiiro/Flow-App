@echo off
chcp 65001 >nul 2>&1
echo ====================================================
echo  📁 FLOWMY - TỰ ĐỘNG KHỞI TẠO TOÀN BỘ CÂY THƯ MỤC ASSETS
echo ====================================================
echo.
echo  Đang tạo các thư mục Tiếng Việt chuẩn theo asset_structure.json...
echo.

node "%~dp0scripts\create_asset_folders.js"

echo.
echo  ✅ Hoàn tất! Tất cả các thư mục và tệp .gitkeep đã sẵn sàng.
echo.
pause
