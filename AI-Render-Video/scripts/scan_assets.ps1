# ============================================================
# scan_assets.ps1 - Scan assets/ -> Generate ASSET_CATALOG.md (EN for AI) & ASSET_CATALOG_VI.md (VI for User) + asset_manifest.json
# ============================================================

param(
    [string]$RootDir = (Join-Path $PSScriptRoot "..\assets")
)

$RootDir = (Resolve-Path $RootDir).Path
$OutputMdEn = Join-Path $RootDir "ASSET_CATALOG.md"
$OutputMdVi = Join-Path $RootDir "ASSET_CATALOG_VI.md"
$OutputJson = Join-Path $RootDir "asset_manifest.json"

# Supported formats
$ModelExts = @(".vrm", ".glb", ".gltf")
$AudioExts = @(".mp3", ".wav", ".ogg")
$AnimExts  = @(".glb", ".bvh", ".fbx")
$ImageExts = @(".png", ".jpg", ".jpeg", ".webp")

function Get-AssetFiles {
    param([string]$Folder, [string[]]$Extensions)
    if (-not (Test-Path $Folder)) { return @() }
    $files = Get-ChildItem -Path $Folder -File -Recurse |
        Where-Object { $Extensions -contains $_.Extension.ToLower() }
    return $files
}

function Get-SizeMB {
    param([long]$Bytes)
    return [math]::Round($Bytes / 1048576, 2)
}

function Get-AssetId {
    param([string]$FileName)
    return [System.IO.Path]::GetFileNameWithoutExtension($FileName)
}

function Get-RelPath {
    param([string]$FullPath, [string]$Root)
    return $FullPath.Substring($Root.Length + 1).Replace("\", "/")
}

Write-Host "  [1/6] Scanning characters..." -ForegroundColor Cyan
$maleChars   = @(Get-AssetFiles (Join-Path $RootDir "characters\male") $ModelExts) + @(Get-AssetFiles (Join-Path $RootDir "characters\man") $ModelExts)
$femaleChars = @(Get-AssetFiles (Join-Path $RootDir "characters\female") $ModelExts) + @(Get-AssetFiles (Join-Path $RootDir "characters\woman") $ModelExts)
$baseBodies  = Get-AssetFiles (Join-Path $RootDir "characters\base_bodies") $ModelExts
$faces       = Get-AssetFiles (Join-Path $RootDir "characters\faces") $ModelExts
$hairstyles  = Get-AssetFiles (Join-Path $RootDir "characters\hairstyles") $ModelExts
$beards      = Get-AssetFiles (Join-Path $RootDir "characters\beards") $ModelExts
$costumes    = Get-AssetFiles (Join-Path $RootDir "characters\costumes") $ModelExts
$accessories = Get-AssetFiles (Join-Path $RootDir "characters\accessories") $ModelExts
$legacyChars = Get-AssetFiles (Join-Path $RootDir "characters") $ModelExts |
    Where-Object { $_.DirectoryName -eq (Join-Path $RootDir "characters") }

Write-Host "  [2/6] Scanning props..." -ForegroundColor Cyan
$weapons     = Get-AssetFiles (Join-Path $RootDir "props\weapons") $ModelExts
$tools       = Get-AssetFiles (Join-Path $RootDir "props\tools") $ModelExts
$consumables = Get-AssetFiles (Join-Path $RootDir "props\consumables") $ModelExts
$furniture   = Get-AssetFiles (Join-Path $RootDir "props\furniture") $ModelExts
$buildings   = Get-AssetFiles (Join-Path $RootDir "props\buildings") $ModelExts
$nature      = Get-AssetFiles (Join-Path $RootDir "props\nature") $ModelExts
$vehicles    = Get-AssetFiles (Join-Path $RootDir "props\vehicles") $ModelExts
$legacyProps = Get-AssetFiles (Join-Path $RootDir "props") $ModelExts |
    Where-Object { $_.DirectoryName -eq (Join-Path $RootDir "props") }

Write-Host "  [3/6] Scanning maps & skyboxes..." -ForegroundColor Cyan
$maps     = Get-AssetFiles (Join-Path $RootDir "maps") $ModelExts
$skyboxes = Get-AssetFiles (Join-Path $RootDir "SkyBoxs") $ImageExts

Write-Host "  [4/6] Scanning audio..." -ForegroundColor Cyan
$bgm        = Get-AssetFiles (Join-Path $RootDir "audio\bgm") $AudioExts
$sfxCombat  = Get-AssetFiles (Join-Path $RootDir "audio\sfx\combat") $AudioExts
$sfxInteract= Get-AssetFiles (Join-Path $RootDir "audio\sfx\interaction") $AudioExts
$sfxAmbient = Get-AssetFiles (Join-Path $RootDir "audio\sfx\ambient") $AudioExts

Write-Host "  [5/6] Scanning animations..." -ForegroundColor Cyan
$animCombat     = Get-AssetFiles (Join-Path $RootDir "animations\combat") $AnimExts
$animInteract   = Get-AssetFiles (Join-Path $RootDir "animations\interaction") $AnimExts
$animXianxia    = Get-AssetFiles (Join-Path $RootDir "animations\xianxia") $AnimExts
$animLocomotion = Get-AssetFiles (Join-Path $RootDir "animations\locomotion") $AnimExts

Write-Host "  [6/6] Scanning VFX..." -ForegroundColor Cyan
$vfxAssets = Get-AssetFiles (Join-Path $RootDir "vfx") $ImageExts

$allFiles = @($maleChars) + @($femaleChars) + @($baseBodies) + @($faces) + @($hairstyles) + @($beards) +
    @($costumes) + @($accessories) + @($legacyChars) +
    @($weapons) + @($tools) + @($consumables) + @($furniture) +
    @($buildings) + @($nature) + @($vehicles) + @($legacyProps) +
    @($maps) + @($skyboxes) + @($bgm) + @($sfxCombat) + @($sfxInteract) + @($sfxAmbient) +
    @($animCombat) + @($animInteract) + @($animXianxia) + @($animLocomotion) +
    @($vfxAssets)

$totalFiles = $allFiles.Count
$totalSizeBytes = ($allFiles | Measure-Object -Property Length -Sum).Sum
if ($null -eq $totalSizeBytes) { $totalSizeBytes = 0 }
$totalSizeMB = [math]::Round($totalSizeBytes / 1048576, 1)
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

function Get-PreviewPath {
    param([System.IO.FileInfo]$File, [string]$Root)
    $dir = $File.DirectoryName
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($File.Name)
    foreach ($ext in @(".png", ".jpg", ".jpeg", ".webp", ".svg")) {
        $p1 = Join-Path $dir "$baseName$ext"
        $p2 = Join-Path $dir "$baseName.preview$ext"
        if (Test-Path $p1) { return "assets/" + (Get-RelPath $p1 $Root) }
        if (Test-Path $p2) { return "assets/" + (Get-RelPath $p2 $Root) }
    }
    return ""
}

function Format-TableRow {
    param([System.IO.FileInfo]$File, [string]$Root)
    $id   = Get-AssetId $File.Name
    $rel  = Get-RelPath $File.FullName $Root
    $ext  = $File.Extension.ToUpper().TrimStart(".")
    $size = "$(Get-SizeMB $File.Length) MB"
    $prev = Get-PreviewPath $File $Root
    $prevCell = if ($prev) { "``$prev``" } else { "—" }
    return "| ``$id`` | ``$rel`` | $ext | $size | $prevCell |"
}

$tableHeader = "| ID | Path | Format | Size | Ref Image |"
$tableSep    = "|:---|:---|:---|---:|:---|"

# ============================================================
# 1. Generate ASSET_CATALOG.md (English for AI)
# ============================================================
Write-Host ""
Write-Host "  Generating ASSET_CATALOG.md (English for AI)..." -ForegroundColor Green

$mdEn = @()
$mdEn += "# ASSET CATALOG - AI 3D Animation Studio"
$mdEn += ""
$mdEn += "> **FOR AI AGENTS:** Read this catalog to generate scene JSON (`MasterSceneConfig`)."
$mdEn += "> **Auto-generated:** $timestamp"
$mdEn += "> **Total assets:** $totalFiles files, $totalSizeMB MB"
$mdEn += "> **Scan root:** ``$RootDir``"
$mdEn += ""
$mdEn += "---"
$mdEn += ""
$mdEn += "## AI INSTRUCTIONS - HOW TO GENERATE JSON SCENE CONFIG"
$mdEn += ""
$mdEn += "### Step 1: Select Map & Environment"
$mdEn += "Set ``environment.map`` to an available map file from ``maps/``."
$mdEn += ""
$mdEn += "### Step 2: Modular Character Assembly"
$mdEn += "Assemble characters using the ``assembly`` field:"
$mdEn += '```json'
$mdEn += '{'
$mdEn += '  "id": "actor_01",'
$mdEn += '  "name": "Li Qingyun",'
$mdEn += '  "model": "characters/base_bodies/male_warrior.vrm",'
$mdEn += '  "assembly": {'
$mdEn += '    "base_body": "characters/base_bodies/male_warrior.vrm",'
$mdEn += '    "face": "characters/faces/face_male_young.glb",'
$mdEn += '    "hairstyle": "characters/hairstyles/hair_topknot.glb",'
$mdEn += '    "costume": "characters/costumes/costume_xianxia_white.glb",'
$mdEn += '    "accessories": ["characters/accessories/acc_headband.glb"],'
$mdEn += '    "skin_color": "#ffd1b3",'
$mdEn += '    "hair_color": "#1a1a2e"'
$mdEn += '  },'
$mdEn += '  "spawn_point": [0, 0, 0]'
$mdEn += '}'
$mdEn += '```'
$mdEn += "If modular parts are not available, use the legacy ``model`` field."
$mdEn += ""
$mdEn += "### Step 3: Configure Timeline Tracks"
$mdEn += "- ``movement``: idle, walk, run, fly_to, arms_crossed, hands_behind_back, meditate, etc."
$mdEn += "- ``speech``: line_ref + expressions (angry, cold, arrogant, meditative, etc.)"
$mdEn += "- ``combat_actions`` or ``combat_master``: melee combos, ranged projectiles, aerial battles"
$mdEn += "- ``object_interactions``: pickup, drink, carry, pour, dig, water, plant_seed, harvest"
$mdEn += "- ``transformations``: costume swap & body morphing with VFX"
$mdEn += "- ``inventory_actions``: equip, unequip, use, drop, give items"
$mdEn += ""
$mdEn += "### Step 4: Camera, Audio & World Events"
$mdEn += "- ``camera_tracks``: cinematic_dolly, face_close_up, orbit"
$mdEn += "- ``dynamic_world_events``: building upgrades, crop growth, spawn/destroy"
$mdEn += ""
$mdEn += "---"
$mdEn += ""
$mdEn += "## AVAILABLE ASSETS LIST"
$mdEn += ""

$charCategoriesEn = @(
    @{ Title="Characters - Base Bodies"; Files=(@($baseBodies) + @($legacyChars)); Folder="characters/base_bodies/" },
    @{ Title="Characters - Faces"; Files=$faces; Folder="characters/faces/" },
    @{ Title="Characters - Hairstyles"; Files=$hairstyles; Folder="characters/hairstyles/" },
    @{ Title="Characters - Beards"; Files=$beards; Folder="characters/beards/" },
    @{ Title="Characters - Costumes"; Files=$costumes; Folder="characters/costumes/" },
    @{ Title="Characters - Accessories"; Files=$accessories; Folder="characters/accessories/" }
)

foreach ($cat in $charCategoriesEn) {
    $mdEn += "### $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdEn += $tableHeader; $mdEn += $tableSep
        foreach ($f in $cat.Files) { $mdEn += Format-TableRow $f $RootDir }
    } else {
        $mdEn += "*No assets yet. Drop .glb/.vrm files into ``$($cat.Folder)``*"
    }
    $mdEn += ""
}

$propCategoriesEn = @(
    @{ Title="Props - Weapons"; Files=$weapons; Folder="props/weapons/" },
    @{ Title="Props - Tools"; Files=$tools; Folder="props/tools/" },
    @{ Title="Props - Consumables"; Files=$consumables; Folder="props/consumables/" },
    @{ Title="Props - Furniture"; Files=$furniture; Folder="props/furniture/" },
    @{ Title="Props - Buildings"; Files=$buildings; Folder="props/buildings/" },
    @{ Title="Props - Nature"; Files=$nature; Folder="props/nature/" },
    @{ Title="Props - Vehicles"; Files=$vehicles; Folder="props/vehicles/" }
)

foreach ($cat in $propCategoriesEn) {
    $mdEn += "### $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdEn += $tableHeader; $mdEn += $tableSep
        foreach ($f in $cat.Files) { $mdEn += Format-TableRow $f $RootDir }
    } else {
        $mdEn += "*No assets yet. Drop .glb files into ``$($cat.Folder)``*"
    }
    $mdEn += ""
}

if ($legacyProps.Count -gt 0) {
    $mdEn += "### Props - Legacy (root level)"
    $mdEn += $tableHeader; $mdEn += $tableSep
    foreach ($f in $legacyProps) { $mdEn += Format-TableRow $f $RootDir }
    $mdEn += ""
}

$mdEn += "### Maps"
if ($maps.Count -gt 0) {
    $mdEn += $tableHeader; $mdEn += $tableSep
    foreach ($f in $maps) { $mdEn += Format-TableRow $f $RootDir }
} else { $mdEn += "*No maps found.*" }
$mdEn += ""

$audioCategoriesEn = @(
    @{ Title="Audio - BGM"; Files=$bgm },
    @{ Title="Audio - SFX Combat"; Files=$sfxCombat },
    @{ Title="Audio - SFX Interaction"; Files=$sfxInteract },
    @{ Title="Audio - SFX Ambient"; Files=$sfxAmbient }
)

foreach ($cat in $audioCategoriesEn) {
    $mdEn += "### $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdEn += $tableHeader; $mdEn += $tableSep
        foreach ($f in $cat.Files) { $mdEn += Format-TableRow $f $RootDir }
    } else { $mdEn += "*No audio files found.*" }
    $mdEn += ""
}

$animCategoriesEn = @(
    @{ Title="Animations - Combat"; Files=$animCombat },
    @{ Title="Animations - Interaction"; Files=$animInteract },
    @{ Title="Animations - Xianxia"; Files=$animXianxia },
    @{ Title="Animations - Locomotion"; Files=$animLocomotion }
)

foreach ($cat in $animCategoriesEn) {
    $mdEn += "### $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdEn += $tableHeader; $mdEn += $tableSep
        foreach ($f in $cat.Files) { $mdEn += Format-TableRow $f $RootDir }
    } else { $mdEn += "*No animation clips found.*" }
    $mdEn += ""
}

$mdEn += "### VFX Textures"
if ($vfxAssets.Count -gt 0) {
    $mdEn += $tableHeader; $mdEn += $tableSep
    foreach ($f in $vfxAssets) { $mdEn += Format-TableRow $f $RootDir }
} else { $mdEn += "*No VFX textures found.*" }
$mdEn += ""

$mdEn += "---"
$mdEn += ""
$mdEn += "## AVAILABLE ACTIONS (40 actions)"
$mdEn += ""
$mdEn += "### Basic Movement"
$mdEn += "``idle``, ``walk``, ``run``, ``sit``, ``climb``"
$mdEn += ""
$mdEn += "### Advanced Movement"
$mdEn += "``fly_to``, ``dash_to``, ``teleport``, ``kneel``, ``bow``, ``meditate``"
$mdEn += ""
$mdEn += "### Combat"
$mdEn += "``heavy_slash_combo``, ``fast_slash``, ``magic_blast``, ``punch_kick``, ``fly_back_knockdown``, ``stagger_back``, ``block_defend``, ``dodge``"
$mdEn += ""
$mdEn += "### Xianxia Poses"
$mdEn += "``arms_crossed``, ``hands_behind_back``, ``fist_salute``, ``finger_spell``, ``power_charge``, ``flying_stance``"
$mdEn += ""
$mdEn += "### Object Interaction"
$mdEn += "``pickup_right``, ``carry_two_hands``, ``drink``, ``pour``, ``dig``, ``water_plants``, ``plant_seed``, ``harvest``, ``wave``, ``dance``, ``throw``"
$mdEn += ""
$mdEn += "## AVAILABLE EXPRESSIONS (21 expressions)"
$mdEn += ""
$mdEn += "### Basic Expressions"
$mdEn += "``neutral``, ``angry``, ``pain``, ``smile``, ``smirk``, ``sad``, ``serious``, ``surprised``, ``shock``"
$mdEn += ""
$mdEn += "### Xianxia Expressions"
$mdEn += "``cold``, ``arrogant``, ``contempt``, ``wise``, ``fierce``, ``meditative``, ``menacing``, ``compassionate``, ``determined``"
$mdEn += ""

$mdEn -join "`r`n" | Set-Content -Path $OutputMdEn -Encoding UTF8
Write-Host "  [OK] ASSET_CATALOG.md ($($mdEn.Count) lines)" -ForegroundColor Green

# ============================================================
# 2. Generate ASSET_CATALOG_VI.md (Tiếng Việt có dấu cho User)
# ============================================================
Write-Host "  Generating ASSET_CATALOG_VI.md (Tiếng Việt có dấu cho User)..." -ForegroundColor Green

$mdVi = @()
$mdVi += "# 📦 DANH MỤC TÀI NGUYÊN (ASSET CATALOG) — AI 3D Animation Studio"
$mdVi += ""
$mdVi += "> **DÀNH CHO NGƯỜI DÙNG:** File này cung cấp tổng quan tài nguyên studio bằng Tiếng Việt có dấu."
$mdVi += "> **AI Agents:** Đọc file ``ASSET_CATALOG.md`` (bản tiếng Anh)."
$mdVi += "> **Thời gian quét:** $timestamp"
$mdVi += "> **Tổng tài nguyên:** $totalFiles tệp tin, $totalSizeMB MB"
$mdVi += "> **Thư mục gốc:** ``$RootDir``"
$mdVi += ""
$mdVi += "---"
$mdVi += ""
$mdVi += "## 🤖 HƯỚNG DẪN DỰNG KỊCH BẢN SCENE JSON"
$mdVi += ""
$mdVi += "### Bước 1: Chọn Bản Đồ (Map)"
$mdVi += "Khai báo trường ``environment.map`` trỏ tới bản đồ trong ``maps/``."
$mdVi += ""
$mdVi += "### Bước 2: Lắp Ráp Nhân Vật Modular (Assembly)"
$mdVi += "Sử dụng trường ``assembly`` để tự do ghép mặt, tóc, râu, trang phục và phụ kiện:"
$mdVi += '```json'
$mdVi += '{'
$mdVi += '  "id": "actor_01",'
$mdVi += '  "name": "Lý Thanh Vân",'
$mdVi += '  "model": "characters/base_bodies/male_warrior.vrm",'
$mdVi += '  "assembly": {'
$mdVi += '    "base_body": "characters/base_bodies/male_warrior.vrm",'
$mdVi += '    "face": "characters/faces/face_male_young.glb",'
$mdVi += '    "hairstyle": "characters/hairstyles/hair_topknot.glb",'
$mdVi += '    "costume": "characters/costumes/costume_xianxia_white.glb",'
$mdVi += '    "accessories": ["characters/accessories/acc_headband.glb"],'
$mdVi += '    "skin_color": "#ffd1b3",'
$mdVi += '    "hair_color": "#1a1a2e"'
$mdVi += '  },'
$mdVi += '  "spawn_point": [0, 0, 0]'
$mdVi += '}'
$mdVi += '```'
$mdVi += "Nếu chưa có model riêng lẻ, có thể dùng trường ``model`` truyền thống để tương thích ngược."
$mdVi += ""
$mdVi += "### Bước 3: Lập Trình Timeline Chuyển Động"
$mdVi += "- ``movement``: idle (đứng thở), walk (đi bộ), run (chạy), fly_to (bay), arms_crossed (khoanh tay), hands_behind_back (tay sau lưng), meditate (ngồi thiền)..."
$mdVi += "- ``speech``: line_ref kết hợp biểu cảm (angry, cold, arrogant, meditative...)"
$mdVi += "- ``combat_actions`` hoặc ``combat_master``: chuỗi combo võ thuật, đấu phép tầm xa, không chiến"
$mdVi += "- ``object_interactions``: bưng bê, uống nước, rót nước, đào đất, tưới cây, gieo hạt, thu hoạch"
$mdVi += "- ``transformations``: biến thân đổi trang phục, phóng to thu nhỏ kèm hiệu ứng VFX"
$mdVi += "- ``inventory_actions``: rút đồ, cất đồ, trang bị vũ khí/công cụ"
$mdVi += ""
$mdVi += "### Bước 4: Camera, Âm Thanh & Sự Kiện Thế Giới"
$mdVi += "- ``camera_tracks``: góc máy dolly điện ảnh, cận cảnh mặt (close up), xoay vòng (orbit)"
$mdVi += "- ``dynamic_world_events``: nâng cấp nhà cửa, cây trồng lớn lên theo thời gian"
$mdVi += ""
$mdVi += "---"
$mdVi += ""
$mdVi += "## 📋 DANH SÁCH TÀI NGUYÊN HIỆN CÓ"
$mdVi += ""

$charCategoriesVi = @(
    @{ Title="Nhân Vật — Thân Hình Cơ Bản (Base Bodies)"; Files=(@($baseBodies) + @($legacyChars)); Folder="characters/base_bodies/" },
    @{ Title="Nhân Vật — Khuôn Mặt (Faces)"; Files=$faces; Folder="characters/faces/" },
    @{ Title="Nhân Vật — Kiểu Tóc (Hairstyles)"; Files=$hairstyles; Folder="characters/hairstyles/" },
    @{ Title="Nhân Vật — Râu (Beards)"; Files=$beards; Folder="characters/beards/" },
    @{ Title="Nhân Vật — Trang Phục (Costumes)"; Files=$costumes; Folder="characters/costumes/" },
    @{ Title="Nhân Vật — Phụ Kiện (Accessories)"; Files=$accessories; Folder="characters/accessories/" }
)

foreach ($cat in $charCategoriesVi) {
    $mdVi += "### 👤 $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdVi += $tableHeader; $mdVi += $tableSep
        foreach ($f in $cat.Files) { $mdVi += Format-TableRow $f $RootDir }
    } else {
        $mdVi += "*Chưa có tài nguyên. Thả tệp .glb/.vrm vào ``$($cat.Folder)``*"
    }
    $mdVi += ""
}

$propCategoriesVi = @(
    @{ Title="Đạo Cụ — Vũ Khí (Weapons)"; Files=$weapons; Folder="props/weapons/" },
    @{ Title="Đạo Cụ — Dụng Cụ (Tools)"; Files=$tools; Folder="props/tools/" },
    @{ Title="Đạo Cụ — Đồ Tiêu Hao (Consumables)"; Files=$consumables; Folder="props/consumables/" },
    @{ Title="Đạo Cụ — Nội Thất (Furniture)"; Files=$furniture; Folder="props/furniture/" },
    @{ Title="Đạo Cụ — Công Trình (Buildings)"; Files=$buildings; Folder="props/buildings/" },
    @{ Title="Đạo Cụ — Thiên Nhiên (Nature)"; Files=$nature; Folder="props/nature/" },
    @{ Title="Đạo Cụ — Phương Tiện (Vehicles)"; Files=$vehicles; Folder="props/vehicles/" }
)

foreach ($cat in $propCategoriesVi) {
    $mdVi += "### ⚔️ $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdVi += $tableHeader; $mdVi += $tableSep
        foreach ($f in $cat.Files) { $mdVi += Format-TableRow $f $RootDir }
    } else {
        $mdVi += "*Chưa có tài nguyên. Thả tệp .glb vào ``$($cat.Folder)``*"
    }
    $mdVi += ""
}

if ($legacyProps.Count -gt 0) {
    $mdVi += "### 🪑 Đạo Cụ — Thư Mục Gốc (Legacy Props)"
    $mdVi += $tableHeader; $mdVi += $tableSep
    foreach ($f in $legacyProps) { $mdVi += Format-TableRow $f $RootDir }
    $mdVi += ""
}

$mdVi += "### 🗺️ Bản Đồ & Môi Trường (Maps)"
if ($maps.Count -gt 0) {
    $mdVi += $tableHeader; $mdVi += $tableSep
    foreach ($f in $maps) { $mdVi += Format-TableRow $f $RootDir }
} else { $mdVi += "*Chưa có bản đồ.*" }
$mdVi += ""

$audioCategoriesVi = @(
    @{ Title="Âm Thanh — Nhạc Nền (BGM)"; Files=$bgm },
    @{ Title="Âm Thanh — Hiệu Ứng Chiến Đấu (Combat SFX)"; Files=$sfxCombat },
    @{ Title="Âm Thanh — Hiệu Ứng Tương Tác (Interaction SFX)"; Files=$sfxInteract },
    @{ Title="Âm Thanh — Hiệu Ứng Môi Trường (Ambient SFX)"; Files=$sfxAmbient }
)

foreach ($cat in $audioCategoriesVi) {
    $mdVi += "### 🎵 $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdVi += $tableHeader; $mdVi += $tableSep
        foreach ($f in $cat.Files) { $mdVi += Format-TableRow $f $RootDir }
    } else { $mdVi += "*Chưa có âm thanh.*" }
    $mdVi += ""
}

$animCategoriesVi = @(
    @{ Title="Hoạt Ảnh — Chiến Đấu (Combat Animations)"; Files=$animCombat },
    @{ Title="Hoạt Ảnh — Tương Tác (Interaction Animations)"; Files=$animInteract },
    @{ Title="Hoạt Ảnh — Tiên Hiệp (Xianxia Poses)"; Files=$animXianxia },
    @{ Title="Hoạt Ảnh — Di Chuyển (Locomotion)"; Files=$animLocomotion }
)

foreach ($cat in $animCategoriesVi) {
    $mdVi += "### 🎬 $($cat.Title)"
    if ($cat.Files.Count -gt 0) {
        $mdVi += $tableHeader; $mdVi += $tableSep
        foreach ($f in $cat.Files) { $mdVi += Format-TableRow $f $RootDir }
    } else { $mdVi += "*Chưa có hoạt ảnh.*" }
    $mdVi += ""
}

$mdVi += "### ✨ Hiệu Ứng Hình Ảnh (VFX Textures)"
if ($vfxAssets.Count -gt 0) {
    $mdVi += $tableHeader; $mdVi += $tableSep
    foreach ($f in $vfxAssets) { $mdVi += Format-TableRow $f $RootDir }
} else { $mdVi += "*Chưa có hiệu ứng VFX.*" }
$mdVi += ""

$mdVi += "---"
$mdVi += ""
$mdVi += "## 🏃 DANH SÁCH HÀNH ĐỘNG HỖ TRỢ (40 actions)"
$mdVi += ""
$mdVi += "### Di Chuyển Cơ Bản"
$mdVi += "``idle`` (đứng thở), ``walk`` (đi bộ), ``run`` (chạy nhanh), ``sit`` (ngồi), ``climb`` (trèo)"
$mdVi += ""
$mdVi += "### Di Chuyển Nâng Cao"
$mdVi += "``fly_to`` (bay lượn), ``dash_to`` (lướt nhanh), ``teleport`` (dịch chuyển), ``kneel`` (quỳ), ``bow`` (cúi chào), ``meditate`` (thiền định)"
$mdVi += ""
$mdVi += "### Chiến Đấu & Võ Thuật"
$mdVi += "``heavy_slash_combo`` (chém combo), ``fast_slash`` (chém nhanh), ``magic_blast`` (phóng chưởng), ``punch_kick`` (đấm đá), ``fly_back_knockdown`` (bị hất văng), ``stagger_back`` (loạng choạng), ``block_defend`` (đỡ đòn), ``dodge`` (né tránh)"
$mdVi += ""
$mdVi += "### Tư Thế Tiên Hiệp (Xianxia Poses)"
$mdVi += "``arms_crossed`` (khoanh tay), ``hands_behind_back`` (tay chắp sau lưng), ``fist_salute`` (bao quyền bái lễ), ``finger_spell`` (bắt ấn quyết), ``power_charge`` (vận công tụ khí), ``flying_stance`` (tư thế ngự không bay)"
$mdVi += ""
$mdVi += "### Tương Tác Đồ Vật & Đời Sống"
$mdVi += "``pickup_right`` (nhặt đồ), ``carry_two_hands`` (bưng bê 2 tay), ``drink`` (uống nước/rượu), ``pour`` (rót nước), ``dig`` (cuốc đất), ``water_plants`` (tưới cây), ``plant_seed`` (gieo hạt), ``harvest`` (thu hoạch), ``wave`` (vẫy tay), ``dance`` (nhảy múa), ``throw`` (ném đồ)"
$mdVi += ""
$mdVi += "## 🎭 DANH SÁCH BIỂU CẢM KHUÔN MẶT (21 expressions)"
$mdVi += ""
$mdVi += "### Biểu Cảm Cơ Bản"
$mdVi += "``neutral`` (bình thường), ``angry`` (tức giận), ``pain`` (đau đớn), ``smile`` (cười vui), ``smirk`` (cười nhếch mép), ``sad`` (buồn rầu), ``serious`` (nghiêm nghị), ``surprised`` (ngạc nhiên), ``shock`` (sửng sốt)"
$mdVi += ""
$mdVi += "### Biểu Cảm Tiên Hiệp & Phim Truyền Kỳ"
$mdVi += "``cold`` (lạnh lùng), ``arrogant`` (kiêu ngạo), ``contempt`` (khinh thường), ``wise`` (uyên bác), ``fierce`` (hung dữ/sát khí), ``meditative`` (thiền tịnh), ``menacing`` (nham hiểm/đe dọa), ``compassionate`` (từ bi), ``determined`` (quyết tâm/kiên định)"
$mdVi += ""

$mdVi -join "`r`n" | Set-Content -Path $OutputMdVi -Encoding UTF8
Write-Host "  [OK] ASSET_CATALOG_VI.md ($($mdVi.Count) lines)" -ForegroundColor Green

# ============================================================
# 3. Generate asset_manifest.json
# ============================================================
Write-Host "  Generating asset_manifest.json..." -ForegroundColor Green

function ConvertTo-ManifestEntry {
    param([System.IO.FileInfo]$File, [string]$Root)
    return @{
        id      = Get-AssetId $File.Name
        path    = Get-RelPath $File.FullName $Root
        format  = $File.Extension.TrimStart(".").ToLower()
        size_mb = Get-SizeMB $File.Length
    }
}

function ConvertTo-ManifestList {
    param([System.IO.FileInfo[]]$Files, [string]$Root)
    $list = @()
    foreach ($f in $Files) { $list += ConvertTo-ManifestEntry $f $Root }
    return ,$list
}

$manifest = @{
    generated_at = $timestamp
    total_files  = $totalFiles
    total_size_mb = $totalSizeMB
    characters = @{
        base_bodies = ConvertTo-ManifestList $(@($baseBodies) + @($legacyChars)) $RootDir
        faces       = ConvertTo-ManifestList $faces $RootDir
        hairstyles  = ConvertTo-ManifestList $hairstyles $RootDir
        beards      = ConvertTo-ManifestList $beards $RootDir
        costumes    = ConvertTo-ManifestList $costumes $RootDir
        accessories = ConvertTo-ManifestList $accessories $RootDir
    }
    props = @{
        weapons     = ConvertTo-ManifestList $weapons $RootDir
        tools       = ConvertTo-ManifestList $tools $RootDir
        consumables = ConvertTo-ManifestList $consumables $RootDir
        furniture   = ConvertTo-ManifestList $furniture $RootDir
        buildings   = ConvertTo-ManifestList $buildings $RootDir
        nature      = ConvertTo-ManifestList $nature $RootDir
        vehicles    = ConvertTo-ManifestList $vehicles $RootDir
        legacy      = ConvertTo-ManifestList $legacyProps $RootDir
    }
    maps  = ConvertTo-ManifestList $maps $RootDir
    audio = @{
        bgm             = ConvertTo-ManifestList $bgm $RootDir
        sfx_combat      = ConvertTo-ManifestList $sfxCombat $RootDir
        sfx_interaction = ConvertTo-ManifestList $sfxInteract $RootDir
        sfx_ambient     = ConvertTo-ManifestList $sfxAmbient $RootDir
    }
    animations = @{
        combat      = ConvertTo-ManifestList $animCombat $RootDir
        interaction = ConvertTo-ManifestList $animInteract $RootDir
        xianxia     = ConvertTo-ManifestList $animXianxia $RootDir
        locomotion  = ConvertTo-ManifestList $animLocomotion $RootDir
    }
    vfx = ConvertTo-ManifestList $vfxAssets $RootDir
    available_actions = @(
        "idle","walk","run","sit","climb",
        "fly_to","dash_to","teleport","kneel","bow","meditate",
        "heavy_slash_combo","fast_slash","magic_blast","punch_kick",
        "fly_back_knockdown","stagger_back","block_defend","dodge",
        "arms_crossed","hands_behind_back","fist_salute","finger_spell",
        "power_charge","flying_stance",
        "pickup_right","carry_two_hands","drink","pour","dig",
        "water_plants","plant_seed","harvest","wave","dance","throw"
    )
    available_expressions = @(
        "neutral","angry","pain","smile","smirk","sad","serious","surprised","shock",
        "cold","arrogant","contempt","wise","fierce",
        "meditative","menacing","compassionate","determined"
    )
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "  [OK] asset_manifest.json" -ForegroundColor Green

Write-Host ""
Write-Host "  Scan completed! $totalFiles assets ($totalSizeMB MB)" -ForegroundColor Yellow
