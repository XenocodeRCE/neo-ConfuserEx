param(
    [string]$Configuration = "Release",
    [string]$ConfuserRoot = "D:\dev\confuser-lab\src\ConfuserEx",
    [string]$KoiRoot = "D:\dev\confuser-lab\src\KoiVM",
    [string]$MsBuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    [string]$TargetFrameworkRoot = "D:\dev\confuser-lab\tooling\reference-assemblies"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Stage ConfuserEx dependencies for KoiVM ==="
Write-Host "Source: $ConfuserRoot"
Write-Host "Target: $KoiRoot\$Configuration\bin\"

# Build ConfuserEx from parent project
Write-Host ""
Write-Host "[1] Building ConfuserEx..."
Push-Location $ConfuserRoot
try {
    & $MsBuild ".\Confuser2.sln" `
        /m /t:Build /p:Configuration=$Configuration `
        "/p:TargetFrameworkRootPath=$TargetFrameworkRoot" `
        /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "ConfuserEx build failed" }
}
finally { Pop-Location }

# Copy assemblies to KoiVM dependency location
$targetDir = "$KoiRoot\$Configuration\bin"
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$assemblies = @(
    "Confuser.Core.dll",
    "Confuser.DynCipher.dll",
    "Confuser.Protections.dll",
    "Confuser.Renamer.dll",
    "Confuser.Runtime.dll",
    "dnlib.dll"
)

Write-Host ""
Write-Host "[2] Copying assemblies..."
foreach ($asm in $assemblies) {
    $src = "$ConfuserRoot\$Configuration\bin\$asm"
    if (Test-Path $src) {
        Copy-Item $src $targetDir -Force
        Write-Host "  $asm -> $targetDir"
    }
    else {
        Write-Warning "  NOT FOUND: $src"
    }
}

Write-Host ""
Write-Host "=== Staging complete ==="
Write-Host "KoiVM can now be built with: tools\build-koi.ps1 -Configuration $Configuration"
