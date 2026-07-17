# Local security checks: pip-audit + gitleaks.
# Usage: .\scripts\check_security.ps1 [-SkipAudit] [-SkipGitleaks]
param(
    [switch]$SkipAudit,
    [switch]$SkipGitleaks
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not (Test-Path (Join-Path $RepoRoot "requirements-voice.txt"))) {
    throw "requirements-voice.txt not found. Run from Sovereign-Syndicate-RU-Audio repo."
}

$failed = $false

if (-not $SkipAudit) {
    Write-Host "== pip-audit ==" -ForegroundColor Cyan
    $req = Join-Path $RepoRoot "requirements-voice.txt"
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    python -m pip install -q pip-audit *>$null
    $ErrorActionPreference = $prevEap
    pip-audit -r $req
    if ($LASTEXITCODE -ne 0) {
        Write-Host "pip-audit: vulnerabilities found (see above)." -ForegroundColor Yellow
        $failed = $true
    } else {
        Write-Host "pip-audit: OK" -ForegroundColor Green
    }
}

if (-not $SkipGitleaks) {
    Write-Host "`n== gitleaks ==" -ForegroundColor Cyan
    $gitleaks = Join-Path $env:LOCALAPPDATA "gitleaks\gitleaks.exe"
    if (-not (Test-Path $gitleaks)) {
        Write-Host "gitleaks not found at $gitleaks" -ForegroundColor Yellow
        Write-Host "Install: winget install gitleaks  OR download from https://github.com/gitleaks/gitleaks/releases"
        $failed = $true
    } else {
        & $gitleaks detect --source $RepoRoot --verbose
        if ($LASTEXITCODE -ne 0) {
            Write-Host "gitleaks: leaks detected." -ForegroundColor Red
            $failed = $true
        } else {
            Write-Host "gitleaks: no leaks" -ForegroundColor Green
        }
    }
}

if ($failed) { exit 1 }
Write-Host "`nAll security checks passed." -ForegroundColor Green
exit 0
