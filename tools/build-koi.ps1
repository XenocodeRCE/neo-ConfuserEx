param(
    [string]$Configuration = "Debug",
    [string]$KoiRoot = "D:\dev\confuser-lab\src\KoiVM",
    [string]$MsBuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    [string]$TargetFrameworkRoot = "D:\dev\confuser-lab\tooling\reference-assemblies"
)

$ErrorActionPreference = "Stop"
Push-Location $KoiRoot

try {
    Write-Host "=== KoiVM Build ==="
    Write-Host "Configuration: $Configuration"
    Write-Host "Root:          $KoiRoot"

    # Ensure trailing slash for SolutionDir
    $solutionDir = $KoiRoot.TrimEnd('\') + '\'

    # Build dependencies first (ordered by dependency)
    $deps = @(
        @{Name="dnlib";              Path="dnlib\src\dnlib.csproj"},
        @{Name="Confuser.Core";      Path="Confuser.Core\Confuser.Core.csproj"},
        @{Name="Confuser.DynCipher"; Path="Confuser.DynCipher\Confuser.DynCipher.csproj"},
        @{Name="Confuser.Renamer";   Path="Confuser.Renamer\Confuser.Renamer.csproj"},
        @{Name="Confuser.Runtime";   Path="Confuser.Runtime\Confuser.Runtime.csproj"},
        @{Name="Confuser.Protections"; Path="Confuser.Protections\Confuser.Protections.csproj"}
    )

    Write-Host ""
    Write-Host "[1/2] Building ConfuserEx dependencies..."
    foreach ($dep in $deps) {
        Write-Host "  $($dep.Name)..."
        & $MsBuild $dep.Path `
            /t:Build /p:Configuration=$Configuration /p:Platform=AnyCPU `
            /p:SolutionDir=$solutionDir `
            "/p:TargetFrameworkRootPath=$TargetFrameworkRoot" `
            /v:minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed: $($dep.Name)"
        }
    }

    # Build KoiVM projects
    $koiProjects = @(
        @{Name="KoiVM.Runtime";  Path="KoiVM.Runtime\KoiVM.Runtime.csproj"},
        @{Name="KoiVM";          Path="KoiVM\KoiVM.csproj"},
        @{Name="KoiVM.Confuser"; Path="KoiVM.Confuser\KoiVM.Confuser.csproj"}
    )

    Write-Host ""
    Write-Host "[2/2] Building KoiVM projects..."
    foreach ($proj in $koiProjects) {
        Write-Host "  $($proj.Name)..."
        & $MsBuild $proj.Path `
            /t:Build /p:Configuration=$Configuration /p:Platform=AnyCPU `
            /p:SolutionDir=$solutionDir `
            "/p:TargetFrameworkRootPath=$TargetFrameworkRoot" `
            /v:minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed: $($proj.Name)"
        }
    }

    Write-Host ""
    Write-Host "=== KoiVM build complete ==="
    Write-Host "Output: $KoiRoot\$Configuration\bin\"
}
finally {
    Pop-Location
}
