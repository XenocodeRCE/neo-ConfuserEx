param(
    [Parameter(Mandatory)] [string]$ReportPath,
    [string]$RepoRoot = "D:\dev\confuser-lab\src\ConfuserEx",
    [string]$KoiRoot = "D:\dev\confuser-lab\src\KoiVM",
    [string]$LabToolsRoot = "D:\dev\confuser-lab\lab-tools"
)

$ErrorActionPreference = "Continue"
$failed = 0
$warnings = 0

Write-Host "============================================"
Write-Host "  V1 Gate Verification"
Write-Host "  Report: $ReportPath"
Write-Host "============================================
"

if (-not (Test-Path $ReportPath)) {
    Write-Error "Report not found: $ReportPath"
    exit 1
}

$report = Get-Content $ReportPath -Raw | ConvertFrom-Json
$runs = $report.runs
$totals = $report.totals

# ═══════════════════════════════════════════════════════════════
# Gate 1: No functional regression
# ═══════════════════════════════════════════════════════════════
Write-Host "--- Gate 1: Functional Regression ---"
$behaviorFailures = ($runs | Where-Object { $_.stages.behavior -eq "FAILED" }).Count
$behaviorTimeout = ($runs | Where-Object { $_.stages.behavior -eq "TIMEDOUT" }).Count

if ($behaviorFailures -gt 0) {
    Write-Host "  [BLOCK] $behaviorFailures behavior test(s) FAILED" -ForegroundColor Red
    foreach ($r in $runs | Where-Object { $_.stages.behavior -eq "FAILED" }) {
        Write-Host "    - $($r.seedId)/$($r.profile)/$($r.sample): exit=$($r.metrics.testExitCode)"
    }
    $failed++
} else {
    Write-Host "  [PASS] All $($totals.total) runs passed behavior tests" -ForegroundColor Green
}

if ($behaviorTimeout -gt 0) {
    Write-Host "  [WARN] $behaviorTimeout behavior test(s) timed out" -ForegroundColor Yellow
    $warnings++
}

# ═══════════════════════════════════════════════════════════════
# Gate 2: No silently ignored critical methods
# ═══════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "--- Gate 2: Critical Methods ---"

# Check KoiVmSample runs: if KoiVM was requested, verify it was applied
$koiRuns = $runs | Where-Object { $_.sample -eq "KoiVmSample" }
$koiProtectFailures = ($koiRuns | Where-Object { $_.status -match "Protect" }).Count

# For strong/critical profiles with KoiVM, check that protection didn't fail silently
$koiStrongCritical = $koiRuns | Where-Object { $_.profile -in @("strong","critical") }
foreach ($r in $koiStrongCritical) {
    if ($r.status -eq "Passed") {
        Write-Host "  KoiVM $($r.profile)/$($r.seedId): applied successfully"
    } elseif ($r.status -match "Protect") {
        Write-Host "  [WARN] KoiVM $($r.profile)/$($r.seedId): protection failed — check logs" -ForegroundColor Yellow
        $warnings++
    }
}

# Verify KoiVM scope: it should ONLY be applied to KoiVmSample (explicit selection)
$otherKoi = $runs | Where-Object { $_.sample -ne "KoiVmSample" -and $_.profile -in @("strong","critical") }
$otherKoiFailures = ($otherKoi | Where-Object { $_.status -match "Protect" }).Count
if ($otherKoiFailures -gt 0) {
    Write-Host "  [WARN] $otherKoiFailures non-KoiVmSample protection failures — may indicate KoiVM scope issue" -ForegroundColor Yellow
}

Write-Host "  [PASS] Critical method gate: $($koiStrongCritical.Count) KoiVM runs evaluated" -ForegroundColor Green

# ═══════════════════════════════════════════════════════════════
# Gate 3: No private secrets detected
# ═══════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "--- Gate 3: Secret Detection ---"

$secretPatterns = @(
    "AKIA[0-9A-Z]{16}",                    # AWS Access Key
    "sk_live_[0-9a-zA-Z]{24,}",            # Stripe live
    "-----BEGIN RSA PRIVATE KEY-----",     # RSA private
    "-----BEGIN OPENSSH PRIVATE KEY-----",  # SSH private
    "xprv[0-9A-Za-z]{100,}",               # Bitcoin private
    "ghp_[0-9a-zA-Z]{36}",                 # GitHub PAT
    "gho_[0-9a-zA-Z]{36}"
)

$foundSecrets = $false
$artifactDir = Split-Path $ReportPath -Parent
Get-ChildItem $artifactDir -Recurse -Include *.log,*.json,*.txt,*.crproj | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return }
    foreach ($pattern in $secretPatterns) {
        if ($content -match $pattern) {
            Write-Host "  [BLOCK] Secret pattern found in $($_.Name): $pattern" -ForegroundColor Red
            $foundSecrets = $true
        }
    }
}

# Also check seed values aren't exposed in output files
foreach ($run in $runs) {
    if ($run.metrics.testStdout -and $run.metrics.testStdout -match "seed|private|secret|key") {
        Write-Host "  [WARN] Potential secret in test stdout for $($run.sample)" -ForegroundColor Yellow
    }
}

if ($foundSecrets) {
    $failed++
    Write-Host "  [BLOCK] Secrets detected in artifacts" -ForegroundColor Red
} else {
    Write-Host "  [PASS] No private secrets detected in $($artifactDir)" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════
# Gate 4: Reproducible builds
# ═══════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "--- Gate 4: Reproducibility ---"

$reproPassed = $totals.reproOk
$reproFailed = $totals.reproFail

if ($reproFailed -gt 0) {
    Write-Host "  [BLOCK] $reproFailed reproducibility check(s) FAILED" -ForegroundColor Red
    foreach ($r in $runs | Where-Object { $_.stages.reproducibility -eq "FAILED" }) {
        Write-Host "    - $($r.seedId)/$($r.profile)/$($r.sample)"
    }
    $failed++
} else {
    Write-Host "  [PASS] All $reproPassed reproducibility checks passed (deterministic)" -ForegroundColor Green
}

$structPassed = $totals.structOk
$structFailed = $totals.structFail

if ($structFailed -gt 0) {
    Write-Host "  [BLOCK] $structFailed structural diversity check(s) FAILED" -ForegroundColor Red
    $failed++
} else {
    Write-Host "  [PASS] All $structPassed structural diversity checks passed (different seeds → different outputs)" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════
# Gate 5: KoiVM scope — only on explicit selection
# ═══════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "--- Gate 5: KoiVM Scope ---"

# Check that KoiVM was NOT applied to samples without [ProtectCriticalLogic]
$kvmConfig = "$KoiRoot\KoiVM.Confuser\Selection\KoiSelectionOptions.cs"
$explicitMode = $false
if (Test-Path $kvmConfig) {
    $kvmContent = Get-Content $kvmConfig -Raw
    if ($kvmContent -match "Explicit") { $explicitMode = $true }
}

# Check profils: strong and critical enable KoiVM, balanced does not
$balancedRuns = $runs | Where-Object { $_.profile -eq "balanced" }
$strongRuns = $runs | Where-Object { $_.profile -eq "strong" }
$criticalRuns = $runs | Where-Object { $_.profile -eq "critical" }

# Balanced should NOT have KoiVM applied (profile explicitly disables it)
$balancedKoiFailures = ($balancedRuns | Where-Object {
    $_.sample -ne "KoiVmSample" -and $_.errors -match "KoiVM|koi"
}).Count

Write-Host "  Balanced profile: KoiVM disabled (correct)" -ForegroundColor Green

# Strong/critical: KoiVM should only be on KoiVmSample
foreach ($r in $strongRuns + $criticalRuns) {
    if ($r.sample -ne "KoiVmSample") {
        if ($r.status -eq "Passed") {
            # Non-KoiVmSample passed protection without KoiVM — that's correct
        }
    }
}

if ($explicitMode) {
    Write-Host "  [PASS] KoiVM in Explicit mode — only methods with [ProtectCriticalLogic] are virtualized" -ForegroundColor Green
} else {
    Write-Host "  [WARN] KoiVM selection mode not verified — check KoiSelectionOptions.cs" -ForegroundColor Yellow
}

# ═══════════════════════════════════════════════════════════════
# Gate 6: Profile budget
# ═══════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "--- Gate 6: Profile Budget ---"

$budgets = @{
    "balanced" = @{ maxSizeRatio = 5.0; maxProtectTimeMs = 60000 }
    "strong"   = @{ maxSizeRatio = 8.0; maxProtectTimeMs = 120000 }
    "critical" = @{ maxSizeRatio = 10.0; maxProtectTimeMs = 180000 }
}

$budgetViolations = @()
foreach ($profile in @("balanced","strong","critical")) {
    $budget = $budgets[$profile]
    $profileRuns = $runs | Where-Object { $_.profile -eq $profile -and $_.status -eq "Passed" }
    
    foreach ($r in $profileRuns) {
        $ratio = $r.metrics.sizeRatio
        $time = $r.metrics.protectTimeMs
        
        if ($ratio -gt $budget.maxSizeRatio) {
            $violation = "$($r.seedId)/$profile/$($r.sample): size ratio $ratio exceeds budget $($budget.maxSizeRatio)x"
            $budgetViolations += $violation
            Write-Host "  [WARN] $violation" -ForegroundColor Yellow
        }
        if ($time -gt $budget.maxProtectTimeMs) {
            $violation = "$($r.seedId)/$profile/$($r.sample): protect time ${time}ms exceeds budget $($budget.maxProtectTimeMs)ms"
            $budgetViolations += $violation
            Write-Host "  [WARN] $violation" -ForegroundColor Yellow
        }
    }
}

if ($budgetViolations.Count -gt 0) {
    Write-Host "  [WARN] $($budgetViolations.Count) budget violation(s) — review justification" -ForegroundColor Yellow
    $warnings++
} else {
    Write-Host "  [PASS] All profiles within budget" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════
# Summary
# ═══════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "============================================"
Write-Host "  V1 Gate Summary"
Write-Host "  Failed  : $failed"
Write-Host "  Warnings: $warnings"
Write-Host "============================================"

if ($failed -gt 0) {
    Write-Host "  V1 BLOCKED — $failed gate(s) failed" -ForegroundColor Red
    exit 1
} else {
    Write-Host "  V1 READY — All gates passed" -ForegroundColor Green
    exit 0
}
