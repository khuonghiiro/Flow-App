@echo off
chcp 65001 >nul 2>&1
echo ====================================================
echo  🎨 FLOWMY - TỰ ĐỘNG KHỞI TẠO CÂY THƯ MỤC ASSET_2DS
echo ====================================================
echo.
echo  Đang tạo cây thư mục 2D chuyên dụng cho linh kiện & map...
echo.

node "%~dp0scripts\create_asset_2ds_folders.js"

echo.
echo  ✅ Hoàn tất! Cây thư mục asset_2ds đã sẵn sàng.
echo.
pause
