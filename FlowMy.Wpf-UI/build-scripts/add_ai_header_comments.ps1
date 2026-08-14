# =========================================================================================
# Script: add_ai_header_comments.ps1
# Mục đích: Tự động chèn khối comment AI Notice vào đầu tất cả các file .cs trong solution.
# =========================================================================================

$ErrorActionPreference = "Stop"

# Xác định thư mục root của repository
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Get-Item "$scriptDir\..\..").FullName

Write-Host "=== FLOWMY AI HEADER COMMENT INSERTER ===" -ForegroundColor Cyan
Write-Host "Root Directory: $rootDir" -ForegroundColor DarkGray

$headerComment = @"
// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================

"@

$targetDirs = @(
    "$rootDir\FlowMy.Core",
    "$rootDir\FlowMy.Wpf-UI"
)

$excludedSubstrings = @(
    "\bin\",
    "\obj\",
    "\.vs\",
    "\.git\",
    ".Designer.cs",
    ".g.cs",
    ".i.cs",
    "AssemblyInfo.cs"
)

$processedCount = 0
$updatedCount = 0
$skippedCount = 0

foreach ($dir in $targetDirs) {
    if (-not (Test-Path $dir)) { continue }

    $csFiles = Get-ChildItem -Path $dir -Filter "*.cs" -Recurse -File

    foreach ($file in $csFiles) {
        $path = $file.FullName

        # Bỏ qua các file sinh tự động / compiler output
        $shouldSkip = $false
        foreach ($exclude in $excludedSubstrings) {
            if ($path.Contains($exclude)) {
                $shouldSkip = $true
                break
            }
        }
        if ($shouldSkip) { continue }

        $processedCount++
        $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

        # Kiểm tra nếu file đã có header AI comment
        if ($content -match "AI NOTICE" -or $content -match "AI CODING STANDARDS") {
            $skippedCount++
            continue
        }

        # Chèn header vào đầu file
        $newContent = $headerComment + $content
        [System.IO.File]::WriteAllText($path, $newContent, [System.Text.Encoding]::UTF8)
        $updatedCount++
        Write-Host " [UPDATED] $($file.Name)" -ForegroundColor Green
    }
}

Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
Write-Host " Tổng số file .cs quét: $processedCount" -ForegroundColor White
Write-Host " Đã cập nhật header:     $updatedCount" -ForegroundColor Green
Write-Host " Đã có sẵn (bỏ qua):     $skippedCount" -ForegroundColor DarkGray
Write-Host " Hoàn tất!`n" -ForegroundColor Yellow
