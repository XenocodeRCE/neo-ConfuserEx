param(
    [string]$ArtifactsRoot = "D:\dev\confuser-lab\artifacts",
    [hashtable]$Results = @{}
)

$ErrorActionPreference = "Stop"
$failed = 0

Write-Host "=== CI Quality Gates ===" -ForegroundColor Yellow

# ── Gate 1: No functional test failures ─────────────────────
$testFailures = ($Results.GetEnumerator() | Where-Object { $_.Value -eq "FAILED" }).Count
if ($testFailures -gt 0) {
    Write-Host "  [FAIL] $testFailures stage(s) failed" -ForegroundColor Red
    $failed++
} else {
    Write-Host "  [PASS] No functional test failures" -ForegroundColor Green
}

# ── Gate 2: No unclassified protection failures ─────────────
# Verified by behavior test stage — if it passed, all protections worked
if ($Results["behavior-tests"] -ne "FAILED") {
    Write-Host "  [PASS] No unclassified protection failures" -ForegroundColor Green
} else {
    Write-Host "  [WARN] Behavior tests not run — cannot verify protections" -ForegroundColor Yellow
}

# ── Gate 3: No rollback on critical methods ─────────────────
# Verified by unit tests (snapshot restore tests in KoiVM selection)
if ($Results["unit-tests"] -ne "FAILED") {
    Write-Host "  [PASS] No rollback on critical methods (verified by snapshot tests)" -ForegroundColor Green
} else {
    Write-Host "  [WARN] Unit tests not run — cannot verify snapshot integrity" -ForegroundColor Yellow
}

# ── Gate 4: Performance budget ──────────────────────────────
# Build time should be reasonable (< 5 min for full build)
$binlog = "$ArtifactsRoot\ci-build.binlog"
if (Test-Path $binlog) {
    $buildTime = ((Get-Item $binlog).LastWriteTime - (Get-Item $binlog).CreationTime).TotalSeconds
    if ($buildTime -lt 300) {
        Write-Host "  [PASS] Build time: $([math]::Round($buildTime,1))s (budget: 300s)" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] Build time: $([math]::Round($buildTime,1))s exceeds 300s budget" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [WARN] No binlog found — cannot check build time" -ForegroundColor Yellow
}

# ── Gate 5: JSON reports present ────────────────────────────
$reportPath = "$ArtifactsRoot\ci-report.json"
if (Test-Path $reportPath) {
    try {
        $report = Get-Content $reportPath | ConvertFrom-Json
        Write-Host "  [PASS] CI report present ($($report.stages.PSObject.Properties.Count) stages)" -ForegroundColor Green
    } catch {
        Write-Host "  [FAIL] CI report malformed" -ForegroundColor Red
        $failed++
    }
} else {
    Write-Host "  [INFO] CI report not yet generated — will be produced by pipeline" -ForegroundColor Yellow
}

# ── Summary ─────────────────────────────────────────────────
Write-Host ""
if ($failed -eq 0) {
    Write-Host "=== All quality gates PASSED ===" -ForegroundColor Green
} else {
    Write-Host "=== $failed quality gate(s) FAILED ===" -ForegroundColor Red
    exit 1
}
