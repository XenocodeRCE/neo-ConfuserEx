param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",
    [string] $OutputDirectory = (Join-Path $PSScriptRoot "artifacts")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$cliOutput = Join-Path $OutputDirectory "cli"

dotnet publish (Join-Path $repoRoot "Confuser.CLI/Confuser.CLI.csproj") `
    --configuration $Configuration `
    --output $cliOutput

if ($IsWindows) {
    $guiOutput = Join-Path $OutputDirectory "gui"
    dotnet publish (Join-Path $repoRoot "ConfuserEx/ConfuserEx.csproj") `
        --configuration $Configuration `
        --output $guiOutput
}

$archive = Join-Path $OutputDirectory "Neo-ConfuserEx-$Configuration.zip"
Compress-Archive -Path (Join-Path $cliOutput "*") -DestinationPath $archive -Force
Write-Host "Created $archive"
