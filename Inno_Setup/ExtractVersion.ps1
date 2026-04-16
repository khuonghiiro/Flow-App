# ExtractVersion.ps1
$CsprojPath = "..\FlowMy.csproj"
$OutputFile = "version.iss"

Write-Host "Step 1: Checking file"
if (-not (Test-Path $CsprojPath)) {
    Write-Host "ERROR: File not found"
    exit 1
}

Write-Host "Step 2: Reading XML"
try {
    [xml]$csproj = Get-Content $CsprojPath -Encoding UTF8
    $fileVersion = $csproj.Project.PropertyGroup.FileVersion
    
    if ($fileVersion) {
        Write-Host "Found: $fileVersion"
        
        $ver = [string]$fileVersion
        $ver = $ver.Trim()
        $ver = $ver -replace '\.0$', ''
        
        Write-Host "Processed: $ver"
        
        Write-Host "Step 3: Creating version.iss"
        
        $output = @"
; Auto-generated version file
#define MyAppVersion "$ver"
"@
        
        Set-Content -Path $OutputFile -Value $output -Encoding UTF8
        
        Write-Host "SUCCESS!"
        Write-Host "Version: $ver"
        exit 0
    }
    else {
        Write-Host "ERROR: FileVersion not found"
        exit 1
    }
}
catch {
    Write-Host "ERROR: $PSItem"
    exit 1
}