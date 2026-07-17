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
    # coqui-tts requires transformers<5; 4.x Hub/Trainer CVEs are out of scope for local XTTS
    $ignore = @(
        "PYSEC-2025-217","PYSEC-2026-2290","PYSEC-2026-2288","PYSEC-2026-2289",
        "CVE-2025-14929","CVE-2026-5241","CVE-2026-1839","CVE-2026-4372"
    )
    $auditArgs = @("-r", $req) + ($ignore | ForEach-Object { @("--ignore-vuln", $_) })
    & pip-audit @auditArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "pip-audit: vulnerabilities found (see above)." -ForegroundColor Yellow
        $failed = $true
    } else {
        Write-Host "pip-audit: OK (transformers 4.x Hub/Trainer CVEs ignored — see requirements-voice.txt)" -ForegroundColor Green
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
