# Script to extract used icons and copy from Icons_All to Icons
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"

Write-Host "Extracting used icons from codebase and copying to Assets/Icons..." -ForegroundColor Cyan

# Set of used icon keys
$usedIconKeys = New-Object System.Collections.Generic.HashSet[string]

# 1. Extract from XAML files: ConverterParameter='...'
Write-Host "Scanning XAML files..." -ForegroundColor Yellow
Get-ChildItem -Path $ProjectRoot -Filter "*.xaml" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    # Find ConverterParameter='icon-name' or ConverterParameter="icon-name"
    $matches = [regex]::Matches($content, "ConverterParameter=['""]([^'""]+)['""]")
    foreach ($match in $matches) {
        $iconKey = $match.Groups[1].Value
        # Skip values that are not icon keys
        if ($iconKey -and $iconKey -notmatch "^(Hover|TextOn|Pressed)$") {
            # Only add if it has format "icon-name subfolder" (contains space)
            if ($iconKey -match '\s') {
                $usedIconKeys.Add($iconKey) | Out-Null
                Write-Host "  Found in XAML: $iconKey" -ForegroundColor Gray
            } else {
                Write-Host "  Skipped (no subfolder): $iconKey" -ForegroundColor DarkGray
            }
        }
    }
}

# 2. Extract from C# files: iconConverter.Convert(..., "icon-name", ...)
Write-Host "Scanning C# files..." -ForegroundColor Yellow
Get-ChildItem -Path $ProjectRoot -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    # Find Convert(null, typeof(Uri), "icon-name", ...) or Convert(..., "icon-name", ...)
    $matches = [regex]::Matches($content, 'Convert\([^)]*["'']([^"'']+)["''][^)]*\)')
    foreach ($match in $matches) {
        $iconKey = $match.Groups[1].Value
        if ($iconKey -and $iconKey -notmatch "^(Hover|TextOn|Pressed)$") {
            # Only add if it has format "icon-name subfolder" (contains space)
            if ($iconKey -match '\s') {
                $usedIconKeys.Add($iconKey) | Out-Null
                Write-Host "  Found in C#: $iconKey" -ForegroundColor Gray
            } else {
                Write-Host "  Skipped (no subfolder): $iconKey" -ForegroundColor DarkGray
            }
        }
    }
    
    # Find GetIconPath("icon-name")
    $matches2 = [regex]::Matches($content, 'GetIconPath\(["'']([^"'']+)["'']\)')
    foreach ($match in $matches2) {
        $iconKey = $match.Groups[1].Value
        if ($iconKey -and $iconKey -match '\s') {
            $usedIconKeys.Add($iconKey) | Out-Null
            Write-Host "  Found GetIconPath: $iconKey" -ForegroundColor Gray
        }
    }
    
    # Skip fallback paths scanning as they don't have subfolder info
}

# 3. Load additional icons from icons.txt file (if exists)
$iconsTextFile = Join-Path $PSScriptRoot "icons.txt"
if (Test-Path $iconsTextFile) {
    Write-Host "`n================================================" -ForegroundColor Cyan
    Write-Host "Loading icons from icons.txt" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "Reading icons from: $iconsTextFile" -ForegroundColor Green
    
    $fileContent = Get-Content $iconsTextFile
    $addedFromFile = 0
    
    foreach ($line in $fileContent) {
        $line = $line.Trim()
        # Skip empty lines and comments
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
            continue
        }
        
        # Check if line has format "icon-name subfolder"
        if ($line -match '\s') {
            if ($usedIconKeys.Add($line)) {
                Write-Host "  Added from file: $line" -ForegroundColor Cyan
                $addedFromFile++
            } else {
                Write-Host "  Already exists: $line" -ForegroundColor DarkGray
            }
        } else {
            Write-Host "  Skipped (no subfolder): $line" -ForegroundColor DarkGray
        }
    }
    
    Write-Host "Added $addedFromFile new icons from icons.txt" -ForegroundColor Green
} else {
    Write-Host "`nicons.txt not found in script directory, skipping additional icons..." -ForegroundColor Yellow
}

# 4. Create Assets/Icons folder if not exists
$targetIconsFolder = Join-Path $ProjectRoot "Assets\Icons"
if (-not (Test-Path $targetIconsFolder)) {
    Write-Host "Creating folder: $targetIconsFolder" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetIconsFolder -Force | Out-Null
}

# 5. Read source icons folder from env_icon.txt
$envIconFile = Join-Path $PSScriptRoot "env_icon.txt"
$defaultSourceFolder = Join-Path $ProjectRoot "Assets\Icons_All"

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Source Icons Folder Selection" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$sourceIconsFolder = $null

if (Test-Path $envIconFile) {
    Write-Host "Reading source folder paths from: $envIconFile" -ForegroundColor Green
    $lines = Get-Content $envIconFile
    
    if ($lines.Count -eq 0) {
        Write-Host "env_icon.txt is empty, using default folder" -ForegroundColor Yellow
        $sourceIconsFolder = $defaultSourceFolder
    } else {
        # Doc tung dong va kiem tra ton tai
        $lineNumber = 0
        foreach ($line in $lines) {
            $lineNumber++
            $line = $line.Trim()
            
            # Bo qua dong trong hoac comment
            if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
                Write-Host "  Line $lineNumber : Skipped (empty or comment)" -ForegroundColor DarkGray
                continue
            }
            
            # Xu ly duong dan tuyet doi hoac tuong doi
            if ([System.IO.Path]::IsPathRooted($line)) {
                $testPath = $line
            } else {
                $testPath = Join-Path $ProjectRoot $line
            }
            
            Write-Host "  Line $lineNumber : Testing path: $testPath" -ForegroundColor Cyan
            
            # Kiem tra neu thu muc ton tai
            if (Test-Path $testPath) {
                $sourceIconsFolder = $testPath
                Write-Host "  Line $lineNumber : Found valid folder!" -ForegroundColor Green
                Write-Host "Using source folder: $sourceIconsFolder" -ForegroundColor Green
                break
            } else {
                Write-Host "  Line $lineNumber : Folder not found, trying next line..." -ForegroundColor Yellow
            }
        }
        
        # Neu khong tim thay thu muc nao ton tai
        if (-not $sourceIconsFolder) {
            Write-Host "No valid folder found in env_icon.txt, using default folder" -ForegroundColor Yellow
            $sourceIconsFolder = $defaultSourceFolder
        }
    }
} else {
    Write-Host "env_icon.txt not found, using default folder" -ForegroundColor Yellow
    $sourceIconsFolder = $defaultSourceFolder
}

Write-Host "`nFinal source folder: $sourceIconsFolder" -ForegroundColor Cyan

# Verify source folder exists. Neu khong co nhung Assets/Icons da co san thi van cho
# phep script tiep tuc de ghi manifest tu cac icon da co.
if (-not (Test-Path $sourceIconsFolder)) {
    Write-Warning "Source folder not found: $sourceIconsFolder"
    Write-Warning "Will not copy new icons, but will still regenerate manifest from existing Assets/Icons."
    $sourceIconsFolder = $null
}

$copiedCount = 0
$skippedCount = 0
$notFoundCount = 0
$usedIconPaths = New-Object System.Collections.Generic.HashSet[string]

$iconKeysToCopy = @()
if ($sourceIconsFolder) {
    Write-Host "`nCopying icons from $sourceIconsFolder to Assets/Icons..." -ForegroundColor Yellow
    $iconKeysToCopy = $usedIconKeys
} else {
    Write-Host "`nSkipping copy step - no valid source folder. Will just build manifest from existing icons." -ForegroundColor Yellow
}

foreach ($iconKey in $iconKeysToCopy) {
    # Parse icon key: "icon-name subfolder" (must have space)
    $parts = $iconKey -split '\s+', 2
    
    if ($parts.Count -ne 2) {
        # Skip if doesn't have exactly 2 parts (icon-name and subfolder)
        Write-Host "  Skipped (invalid format): $iconKey" -ForegroundColor DarkGray
        continue
    }
    
    # Format: "icon-name subfolder"
    $iconName = $parts[0]
    $subfolder = $parts[1]
    $fileName = "$iconName.svg"
    
    # Source: Assets/Icons_All/subfolder/icon-name.svg
    $sourcePath = Join-Path $sourceIconsFolder "$subfolder\$fileName"
    
    # Target: Assets/Icons/subfolder/icon-name.svg (keep subfolder structure)
    $targetSubfolder = Join-Path $targetIconsFolder $subfolder
    $targetPath = Join-Path $targetSubfolder $fileName
    
    # Create subfolder if not exists
    if (-not (Test-Path $targetSubfolder)) {
        New-Item -ItemType Directory -Path $targetSubfolder -Force | Out-Null
        Write-Host "  Created subfolder: $subfolder" -ForegroundColor Cyan
    }
    
    # Check if target file already exists
    if (Test-Path $targetPath) {
        $relativePath = "$subfolder\$fileName"
        Write-Host "  Skipped (already exists): $relativePath" -ForegroundColor Yellow
        $skippedCount++
        $usedIconPaths.Add("Assets\Icons\$relativePath") | Out-Null
        continue
    }
    
    # Check if source file exists and copy
    if (Test-Path $sourcePath) {
        Copy-Item -Path $sourcePath -Destination $targetPath -Force
        $relativePath = "$subfolder\$fileName"
        Write-Host "  Copied: $relativePath" -ForegroundColor Green
        $copiedCount++
        $usedIconPaths.Add("Assets\Icons\$relativePath") | Out-Null
    } else {
        Write-Warning "  Not found: $iconKey"
        Write-Host "    Searched: $sourcePath" -ForegroundColor DarkGray
        $notFoundCount++
    }
}

# 6. Tao/ghi de file manifest available_icons.txt trong Assets/Icons
# File nay duoc IconSelectorUserControl / IconResources doc khi runtime de biet chinh xac
# danh sach icon da duoc extract (thay vi dung toan bo AvailableIcons dictionary).
# Format moi dong: <icon-key>=<relative-path-from-project-root>
#   vd: "thumbtack duotone=Assets/Icons/duotone/thumbtack.svg"
$manifestPath = Join-Path $targetIconsFolder "available_icons.txt"
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Writing manifest: available_icons.txt" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$manifestLines = New-Object System.Collections.Generic.List[string]
$manifestLines.Add("# Auto-generated by ExtractIcons.ps1 - DO NOT EDIT MANUALLY")
$manifestLines.Add("# Format: <icon-key>=<relative-path-from-project-root>")
$manifestLines.Add("# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$manifestLines.Add("")

# Quet thuc te thu muc Assets/Icons de liet ke toan bo SVG da co (dung khi build/run).
# Duong dan luu tuong doi de link dong theo project.
$allSvgFiles = Get-ChildItem -Path $targetIconsFolder -Filter "*.svg" -Recurse -File |
    Sort-Object FullName

$manifestCount = 0
foreach ($svgFile in $allSvgFiles) {
    # Tinh duong dan tuong doi tu project root, dung forward slash cho cross-platform
    $relPath = $svgFile.FullName.Substring($ProjectRoot.Length).TrimStart('\', '/').Replace('\', '/')

    # Icon key = "<filename-without-ext> <subfolder>"
    $iconFileName = [System.IO.Path]::GetFileNameWithoutExtension($svgFile.Name)
    $subfolderPath = $svgFile.Directory.FullName.Substring($targetIconsFolder.Length).TrimStart('\', '/').Replace('\', '/')

    if ([string]::IsNullOrEmpty($subfolderPath)) {
        # Icon nam truc tiep trong Assets/Icons (khong co subfolder) -> khong tao key
        continue
    }

    $iconKey = "$iconFileName $subfolderPath"
    $manifestLines.Add("$iconKey=$relPath")
    $manifestCount++
}

# Ghi de (hoac tao moi) file manifest
[System.IO.File]::WriteAllLines($manifestPath, $manifestLines, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "  Wrote $manifestCount entries to: $manifestPath" -ForegroundColor Green

# 7. Display summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Found $($usedIconKeys.Count) unique icon keys with subfolders" -ForegroundColor Green
Write-Host "  Copied: $copiedCount icons" -ForegroundColor Green
Write-Host "  Skipped (already exist): $skippedCount icons" -ForegroundColor Yellow
Write-Host "  Not found: $notFoundCount icons" -ForegroundColor Red
Write-Host "  Total icons in Assets/Icons: $($usedIconPaths.Count)" -ForegroundColor Green
Write-Host "  Manifest entries: $manifestCount" -ForegroundColor Green
Write-Host "  Manifest file: $manifestPath" -ForegroundColor Green

# Display icon list
if ($usedIconPaths.Count -gt 0) {
    Write-Host "`nCopied icon files:" -ForegroundColor Cyan
    $usedIconPaths | Sort-Object | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Gray
    }
}

# Pause before exit (bo qua khi chay non-interactive)
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Script completed!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
if (-not $NoPause) {
    try {
        Write-Host "Press any key to exit..." -ForegroundColor Cyan
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    } catch {
        # Host khong ho tro ReadKey (non-interactive) -> bo qua
    }
}