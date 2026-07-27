param(
    [string]$Configuration = "Release",
    [string]$RepoRoot = "D:\dev\confuser-lab\src\ConfuserEx",
    [string]$MsBuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    [string]$TargetFrameworkRoot = "D:\dev\confuser-lab\tooling\reference-assemblies"
)

$ErrorActionPreference = "Stop"

Write-Host "=== KoiVM Selection Tests ==="
Write-Host "Configuration: $Configuration"

# Build the KoiVmSample
Write-Host ""
Write-Host "[1] Building KoiVmSample..."
Push-Location $RepoRoot
try {
    & $MsBuild ".\UnitTest\Samples\KoiVmSample\KoiVmSample.csproj" `
        /t:Build /p:Configuration=$Configuration `
        "/p:TargetFrameworkRootPath=$TargetFrameworkRoot" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "KoiVmSample build failed" }

    # Run baseline
    Write-Host "[2] Running baseline (no protection)..."
    $sampleExe = ".\UnitTest\Samples\KoiVmSample\bin\$Configuration\KoiVmSample.exe"
    & $sampleExe
    if ($LASTEXITCODE -ne 0) { throw "Baseline run failed" }

    Write-Host ""
    Write-Host "=== KoiVM selection tests complete ==="
    Write-Host "KoiVmSample baseline: PASSED"
    Write-Host ""
    Write-Host "Full KoiVM integration tests require:"
    Write-Host "  1. KoiVM.Confuser.exe built from feat/selective-registration"
    Write-Host "  2. A .crproj with <protection id=`"koi`"> configuration"
    Write-Host "  3. Run: Confuser.CLI.exe -n <project>.crproj"
}
finally { Pop-Location }
