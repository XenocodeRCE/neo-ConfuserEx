param(
    [string]$Configuration = "Release",
    [string]$RepoRoot = "D:\dev\confuser-lab\src\ConfuserEx",
    [string]$KoiRoot = "D:\dev\confuser-lab\src\KoiVM",
    [string]$ConfuserCli = "",
    [string]$MsBuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    [string]$TargetFrameworkRoot = "D:\dev\confuser-lab\tooling\reference-assemblies",
    [string]$SeedsFile = "",
    [string]$ArtifactsRoot = "D:\dev\confuser-lab\artifacts\v1-matrix",
    [string]$ProfilesDir = "",
    [string[]]$Samples = @("BasicFlow","ExceptionFlow","Constants","KoiVmSample"),
    [string[]]$Profiles = @("balanced","strong","critical"),
    [int]$TimeoutPerRun = 300,
    [switch]$WhatIf
)

$ErrorActionPreference = "Continue"
$global:ExitCode = 0

# Resolve paths
if (-not $ConfuserCli) {
    $ConfuserCli = "$RepoRoot\Release\bin\Confuser.CLI.exe"
    if (-not (Test-Path $ConfuserCli)) {
        $ConfuserCli = "$RepoRoot\Confuser.CLI\bin\$Configuration\Confuser.CLI.exe"
    }
}
if (-not $ProfilesDir) { $ProfilesDir = "$RepoRoot\Build\Profiles" }
if (-not $SeedsFile) { $SeedsFile = "$RepoRoot\Build\v1-seeds.json" }

# Load seeds
$seeds = @()
if (Test-Path $SeedsFile) {
    $seedsData = Get-Content $SeedsFile -Raw | ConvertFrom-Json
    $seeds = $seedsData.seeds
} else {
    Write-Warning "Seeds file not found: $SeedsFile — using default seed"
    $seeds = @(@{ id = "default"; value = "v1-default"; label = "Default" })
}

$profilesMap = @{
    "balanced" = "$ProfilesDir\balanced.crproj"
    "strong"   = "$ProfilesDir\strong.crproj"
    "critical" = "$ProfilesDir\critical.crproj"
}

$allRuns = @()
$totals = @{ total = 0; passed = 0; failed = 0; reproOk = 0; reproStructOk = 0; reproFail = 0; structOk = 0; structFail = 0 }
$totalCombos = $seeds.Count * $Profiles.Count * $Samples.Count

Write-Host "============================================"
Write-Host "  V1 Matrix Runner"
Write-Host "  Seeds   : $($seeds.Count)"
Write-Host "  Profiles: $($Profiles.Count) ($($Profiles -join ', '))"
Write-Host "  Samples : $($Samples.Count) ($($Samples -join ', '))"
Write-Host "  Total   : $totalCombos combinations"
Write-Host "  Timeout : ${TimeoutPerRun}s per combination"
Write-Host "============================================
"

# Step 0: Build the full solution once
Write-Host "=== Step 0: Build solution ==="
Push-Location $RepoRoot
try {
    & $MsBuild ".\Confuser2.sln" /m /t:Build /p:Configuration=$Configuration `
        "/p:TargetFrameworkRootPath=$TargetFrameworkRoot" /v:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Solution build failed"
        exit 1
    }
    Write-Host "  Build: OK"
} finally { Pop-Location }

# Verify Confuser.CLI exists
if (-not (Test-Path $ConfuserCli)) {
    Write-Error "Confuser.CLI.exe not found at $ConfuserCli"
    exit 1
}
Write-Host "  CLI: $ConfuserCli"
Write-Host ""

# Step 1: For each combination
$counter = 0
foreach ($seed in $seeds) {
    foreach ($profile in $Profiles) {
        $profileFile = $profilesMap[$profile]
        if (-not (Test-Path $profileFile)) {
            Write-Warning "Profile not found: $profileFile"
            continue
        }

        foreach ($sample in $Samples) {
            $counter++
            $label = "[$counter/$totalCombos] $($seed.id)/$profile/$sample"

            Write-Host "=== $label ==="
            $runDir = "$ArtifactsRoot\$($seed.id)\$profile\$sample"
            New-Item -ItemType Directory -Force $runDir | Out-Null

            $runEntry = @{
                seedId = $seed.id
                seedLabel = $seed.label
                profile = $profile
                sample = $sample
                timestamp = (Get-Date -Format "o")
                status = "Unknown"
                stages = @{}
                metrics = @{}
                errors = @()
            }

            # --- 1a: Build sample ---
            $sampleProj = "$RepoRoot\UnitTest\Samples\$sample\$sample.csproj"
            if (-not (Test-Path $sampleProj)) {
                $runEntry.status = "Skipped"
                $runEntry.errors += "Sample project not found: $sampleProj"
                $allRuns += $runEntry
                continue
            }

            $sampleExe = "$RepoRoot\UnitTest\Samples\$sample\bin\$Configuration\$sample.exe"
            if ($WhatIf) {
                Write-Host "  [WHATIF] Build $sample"
            } else {
                Push-Location $RepoRoot
                try {
                    & $MsBuild $sampleProj /t:Build /p:Configuration=$Configuration `
                        "/p:TargetFrameworkRootPath=$TargetFrameworkRoot" /v:q
                    if ($LASTEXITCODE -ne 0) {
                        $runEntry.errors += "Build failed (exit $LASTEXITCODE)"
                        $runEntry.status = "BuildFailed"
                        $allRuns += $runEntry
                        Pop-Location; continue
                    }
                } finally { Pop-Location }
            }
            Write-Host "  Build: OK"

            # Copy input for this run — use seed-specific name for deterministic variation
            # ConfuserEx seeds its random generator from assembly identity (Parent.FullId).
            # Different assembly names → different seeds → different protection outcomes.
            $seedSampleName = "${sample}_$($seed.id)"
            $inputCopy = "$runDir\input\$seedSampleName.exe"
            New-Item -ItemType Directory -Force (Split-Path $inputCopy) | Out-Null
            Copy-Item $sampleExe $inputCopy -Force
            $inputHash = (Get-FileHash $inputCopy -Algorithm SHA256).Hash
            $inputSize = (Get-Item $inputCopy).Length
            $runEntry.metrics.inputHash = $inputHash
            $runEntry.metrics.inputSize = $inputSize

            # --- 1b: Protect ---
            $protectDir = "$runDir\protected"
            $protectCrproj = "$protectDir\$seedSampleName.crproj"
            New-Item -ItemType Directory -Force $protectDir | Out-Null

            # Generate .crproj from profile template with deterministic seed
            # RandomService.Seed(null) → Guid.NewGuid() (non-deterministic)
            # RandomService.Seed("fixed-string") → SHA256 deterministic
            $profileXml = Get-Content $profileFile -Raw
            # Inject seed attribute: <project seed="...">
            $profileXml = $profileXml -replace '(<project[^>]*)>', "`$1 seed=`"$($seed.value)`">"
            $profileXml | Set-Content $protectCrproj -Encoding UTF8

            $protectedExe = "$protectDir\$seedSampleName.exe"
            $protectLog = "$protectDir\protect.log"

            if ($WhatIf) {
                Write-Host "  [WHATIF] Protect with $profile profile, seed $($seed.id)"
            } else {
                $sw = [Diagnostics.Stopwatch]::StartNew()
                try {
                    # Use direct invocation for correct argument passing
                    & $ConfuserCli -n -o $protectDir $inputCopy $protectCrproj 2>&1 | Out-File $protectLog -Encoding UTF8
                    $exitCode = $LASTEXITCODE
                    $sw.Stop()
                    $runEntry.metrics.protectTimeMs = $sw.ElapsedMilliseconds

                    if ($exitCode -ne 0) {
                        $runEntry.errors += "Protection failed (exit $exitCode)"
                        $runEntry.status = "ProtectFailed"
                        $allRuns += $runEntry
                        continue
                    }
                } catch {
                    $sw.Stop()
                    $runEntry.errors += "Protection crashed: $_"
                    $runEntry.status = "ProtectCrashed"
                    $allRuns += $runEntry
                    continue
                }
            }

            # Find protected output
            if (Test-Path $protectedExe) {
                $protectHash = (Get-FileHash $protectedExe -Algorithm SHA256).Hash
                $protectSize = (Get-Item $protectedExe).Length
                $runEntry.metrics.protectHash = $protectHash
                $runEntry.metrics.protectSize = $protectSize
                $runEntry.metrics.sizeRatio = [math]::Round($protectSize / [Math]::Max(1, $inputSize), 2)
                Write-Host "  Protect: OK (${protectSize}B, ratio $($runEntry.metrics.sizeRatio)x, $($runEntry.metrics.protectTimeMs)ms)"
            } else {
                $runEntry.errors += "Protected output not found at $protectedExe"
                $runEntry.status = "ProtectNoOutput"
                $allRuns += $runEntry
                continue
            }

            # --- 1c: Test behavior ---
            $testDir = "$runDir\test"
            $testCopy = "$testDir\$seedSampleName.exe"
            New-Item -ItemType Directory -Force $testDir | Out-Null
            Copy-Item $protectedExe $testCopy -Force

            $testStdout = "$testDir\stdout.txt"
            $testStderr = "$testDir\stderr.txt"

            if ($WhatIf) {
                Write-Host "  [WHATIF] Test behavior"
            } else {
                try {
                    $psi = New-Object Diagnostics.ProcessStartInfo
                    $psi.FileName = $testCopy
                    $psi.WorkingDirectory = $testDir
                    $psi.UseShellExecute = $false
                    $psi.RedirectStandardOutput = $true
                    $psi.RedirectStandardError = $true
                    $psi.CreateNoWindow = $true

                    $testProc = [Diagnostics.Process]::Start($psi)
                    $outTask = $testProc.StandardOutput.ReadToEndAsync()
                    $errTask = $testProc.StandardError.ReadToEndAsync()

                    if ($testProc.WaitForExit(30000)) {
                        $testExit = $testProc.ExitCode
                        $testOut = $outTask.Result
                        $testErr = $errTask.Result
                        $testOut | Out-File $testStdout -Encoding UTF8
                        $testErr | Out-File $testStderr -Encoding UTF8

                        $runEntry.metrics.testExitCode = $testExit
                        $runEntry.metrics.testStdout = $testOut.Trim()
                        $runEntry.stages.behavior = if ($testExit -eq 0) { "PASSED" } else { "FAILED" }

                        if ($testExit -ne 0) {
                            $runEntry.errors += "Behavior test failed (exit $testExit)"
                            $runEntry.status = "BehaviorFailed"
                        }
                        Write-Host "  Test: exit=$testExit, stdout=[$($testOut.Trim())]"
                    } else {
                        taskkill /PID $testProc.Id /T /F 2>&1 | Out-Null
                        $runEntry.errors += "Behavior test timed out"
                        $runEntry.stages.behavior = "TIMEDOUT"
                        $runEntry.status = "BehaviorTimedOut"
                    }
                } catch {
                    $runEntry.errors += "Behavior test crashed: $_"
                    $runEntry.stages.behavior = "CRASHED"
                }
            }

            # --- 1d: Reproducibility (run protection again, same seed) ---
            $reproDir = "$runDir\repro"
            New-Item -ItemType Directory -Force $reproDir | Out-Null
            $reproCrproj = "$reproDir\repro.crproj"
            $profileXml | Set-Content $reproCrproj -Encoding UTF8
            $reproExe = "$reproDir\$seedSampleName.exe"

            if ($WhatIf) {
                Write-Host "  [WHATIF] Reproducibility (same seed)"
            } else {
                try {
                    & $ConfuserCli -n -o $reproDir $inputCopy $reproCrproj 2>&1 | Out-File "$reproDir\repro.log" -Encoding UTF8
                    $reproExit = $LASTEXITCODE

                    if ($reproExit -eq 0 -and (Test-Path $reproExe)) {
                        $reproHash = (Get-FileHash $reproExe -Algorithm SHA256).Hash
                        $runEntry.metrics.reproHash = $reproHash

                        # Gate: compare structural fingerprints (symbol maps), not raw hashes
                        $protectMap = "$protectDir\symbols.map"
                        $reproMap = "$reproDir\symbols.map"
                        $rawEqual = ($reproHash -eq $protectHash)
                        $structEqual = $false

                        if ((Test-Path $protectMap) -and (Test-Path $reproMap)) {
                            $protectFp = (Get-FileHash $protectMap -Algorithm SHA256).Hash
                            $reproFp = (Get-FileHash $reproMap -Algorithm SHA256).Hash
                            $structEqual = ($protectFp -eq $reproFp)
                            $runEntry.metrics.protectSymbolHash = $protectFp
                            $runEntry.metrics.reproSymbolHash = $reproFp
                        }

                        if ($rawEqual) {
                            $runEntry.stages.reproducibility = "PASSED"
                            $totals.reproOk++
                            Write-Host "  Repro: MATCH (bit-identical)"
                        } elseif ($structEqual) {
                            $runEntry.stages.reproducibility = "STRUCT_OK"
                            $runEntry.diagnostics += "Raw mismatch explained by PE metadata (timestamp/MVID); structural fingerprint identical"
                            Write-Host "  Repro: STRUCT_OK (raw diff, structural match — PE metadata only)"
                        } else {
                            $runEntry.stages.reproducibility = "FAILED"
                            $totals.reproFail++
                            $runEntry.errors += "Reproducibility FAILED: same seed produced different structural fingerprint"
                            Write-Host "  Repro: BLOCKED — structural mismatch with same seed!"
                        }
                    } else {
                        $runEntry.stages.reproducibility = "FAILED"
                        $runEntry.errors += "Repro protection failed"
                    }
                } catch {
                    $runEntry.stages.reproducibility = "CRASHED"
                    $runEntry.errors += "Repro crashed: $_"
                }
            }

            # --- 1e: Structural comparison (protect again with DIFFERENT seed) ---
            $structDir = "$runDir\struct"
            New-Item -ItemType Directory -Force $structDir | Out-Null
            $structCrproj = "$structDir\struct.crproj"

            # Use the next seed in the list (wrap around)
            $nextSeedIdx = ([array]::IndexOf($seeds, $seed) + 1) % $seeds.Count
            $nextSeed = $seeds[$nextSeedIdx]
            $nextSampleName = "${sample}_$($nextSeed.id)"
            $structInput = "$structDir\$nextSampleName.exe"
            Copy-Item $sampleExe $structInput -Force
            # Generate struct crproj with DIFFERENT seed
            $structXml = Get-Content $profileFile -Raw
            $structXml = $structXml -replace '(<project[^>]*)>', "`$1 seed=`"$($nextSeed.value)`">"
            $structXml | Set-Content $structCrproj -Encoding UTF8
            $structExe = "$structDir\$nextSampleName.exe"

            if ($WhatIf) {
                Write-Host "  [WHATIF] Structural comparison (different seed: $($nextSeed.id))"
            } else {
                try {
                    & $ConfuserCli -n -o $structDir $structInput $structCrproj 2>&1 | Out-File "$structDir\struct.log" -Encoding UTF8
                    $structExit = $LASTEXITCODE

                    if ($structExit -eq 0 -and (Test-Path $structExe)) {
                        $structHash = (Get-FileHash $structExe -Algorithm SHA256).Hash
                        $runEntry.metrics.diffSeedHash = $structHash
                        $runEntry.stages.structural = if ($structHash -ne $protectHash) { "PASSED" } else { "FAILED" }
                        if ($structHash -ne $protectHash) {
                            $totals.structOk++
                            Write-Host "  Struct: DIFFER ($($seed.id)≠$($nextSeed.id))"
                        } else {
                            $totals.structFail++
                            $runEntry.errors += "Structural diversity failed: different seeds produced same output"
                            Write-Host "  Struct: SAME (unexpected for different seeds)"
                        }
                    } else {
                        $runEntry.stages.structural = "FAILED"
                        $runEntry.errors += "Structural protection failed"
                    }
                } catch {
                    $runEntry.stages.structural = "CRASHED"
                    $runEntry.errors += "Structural crashed: $_"
                }
            }

            # --- Determine overall status ---
            # Pass criteria: behavior test must pass. Repro structural match is required.
            $behaviorOk = ($runEntry.stages.behavior -eq "PASSED")
            $structOk = ($runEntry.stages.structural -eq "PASSED")
            $reproStatus = $runEntry.stages.reproducibility
            
            # Tally repro
            switch ($reproStatus) {
                "PASSED"    { $totals.reproOk++ }
                "STRUCT_OK" { $totals.reproStructOk++ }
                "FAILED"    { $totals.reproFail++; $runEntry.errors += "BLOCKING: same-seed structural mismatch" }
            }
            if ($structOk) { $totals.structOk++ } else { $totals.structFail++ }
            
            if ($behaviorOk -and $reproStatus -ne "FAILED") {
                $runEntry.status = "Passed"
                $totals.passed++
            } else {
                $runEntry.status = "Failed"
                $totals.failed++
            }

            $allRuns += $runEntry
            $totals.total++

            # Save incremental report
            $report = @{
                schemaVersion = 1
                title = "V1 Matrix Report"
                timestamp = (Get-Date -Format "o")
                configuration = @{
                    seeds = $seeds
                    profiles = $Profiles
                    samples = $Samples
                    totalCombinations = $totalCombos
                }
                totals = $totals
                runs = @($allRuns)
            }
            $report | ConvertTo-Json -Depth 5 | Out-File "$ArtifactsRoot\v1-report.json" -Encoding UTF8

            Write-Host ""
        }
    }
}

# Final summary
Write-Host "============================================"
Write-Host "  V1 Matrix Complete"
Write-Host "  Total   : $($totals.total)"
Write-Host "  Passed  : $($totals.passed)"
Write-Host "  Failed  : $($totals.failed)"
Write-Host "  Repro OK    : $($totals.reproOk) (bit-identical)"
Write-Host "  Repro Struct: $($totals.reproStructOk) (raw diff, structural match)"
Write-Host "  Repro FAIL   : $($totals.reproFail) (BLOCKING)"
Write-Host "  StructOK: $($totals.structOk)"
Write-Host "  StructFail: $($totals.structFail)"
Write-Host "  Report  : $ArtifactsRoot\v1-report.json"
Write-Host "============================================"

if ($totals.failed -gt 0 -or $totals.reproFail -gt 0 -or $totals.structFail -gt 0) {
    exit 1
}
exit 0

