param(
    [Parameter(Mandatory)] [string]$InputFile,
    [Parameter(Mandatory)] [string]$ProtectedFile,
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path $InputFile)) { throw "Input not found: $InputFile" }
if (-not (Test-Path $ProtectedFile)) { throw "Protected not found: $ProtectedFile" }

$inputSize = (Get-Item $InputFile).Length
$protectSize = (Get-Item $ProtectedFile).Length
$sizeRatio = [math]::Round($protectSize / [Math]::Max(1, $inputSize), 2)

$measurements = @{
    inputSize = $inputSize
    protectSize = $protectSize
    sizeRatio = $sizeRatio
    inputHash = (Get-FileHash $InputFile -Algorithm SHA256).Hash
    protectHash = (Get-FileHash $ProtectedFile -Algorithm SHA256).Hash
}

# Quick PE inspection
try {
    $pBytes = [IO.File]::ReadAllBytes($ProtectedFile)
    $peOff = [BitConverter]::ToInt32($pBytes, 60)
    $sections = [BitConverter]::ToUInt16($pBytes, $peOff + 6)
    $measurements.sectionCount = $sections

    # Count strings heuristically
    $text = [Text.Encoding]::Unicode.GetString($pBytes)
    $printableStrings = ([regex]::Matches($text, '[\x20-\x7E]{4,}')).Count
    $measurements.printableStringCount = $printableStrings
} catch { }

$measurements.timestamp = (Get-Date -Format "o")

if ($OutputFile) {
    $measurements | ConvertTo-Json -Depth 3 | Set-Content $OutputFile -Encoding UTF8
}

$measurements | ConvertTo-Json -Depth 3
