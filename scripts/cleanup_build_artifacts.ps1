# Remove generated build artifacts older than N days (default: 7).
# Usage: .\scripts\cleanup_build_artifacts.ps1 [-OlderThanDays 7] [-DryRun]
param(
    [int]$OlderThanDays = 7,
    [switch]$DryRun
)

$ErrorActionPreference = "Continue"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$cutoff = (Get-Date).AddDays(-$OlderThanDays)

function Test-BuildArtifactPath {
    param([string]$RelativePath)
    $rp = $RelativePath -replace '/', '\'
    if ($rp -match '\\(bin|obj)\\') { return $true }
    if ($rp -match '\\__pycache__\\') { return $true }
    if ($rp -match '\\\.vs\\') { return $true }
    if ($rp -match '\.pyc$') { return $true }
    if ($rp -match '(^|[\\/])analysis_[^\\/]+\.(json|txt|md)$') { return $true }
    if ($rp -match '^Sovereign-Syndicate-RU-Audio-v.+\.zip$') { return $true }
    if ($rp -match '^mods\\[^\\]+\.zip$') { return $true }
    return $false
}

$removedFiles = 0
Get-ChildItem -Path $RepoRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '\\\.git\\' -and
        $_.LastWriteTime -lt $cutoff
    } |
    ForEach-Object {
        $rel = $_.FullName.Substring($RepoRoot.Length).TrimStart('\')
        if (-not (Test-BuildArtifactPath $rel)) { return }
        if ($DryRun) {
            Write-Host "Would remove: $($_.FullName)"
        } else {
            Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
            Write-Host "Removed: $($_.FullName)"
        }
        $script:removedFiles++
    }

if (-not $DryRun) {
    @('bin', 'obj', '__pycache__', '.vs') | ForEach-Object {
        $dirName = $_
        Get-ChildItem -Path $RepoRoot -Recurse -Directory -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $dirName -and $_.FullName -notmatch '\\\.git\\' } |
            ForEach-Object {
                if ((Get-ChildItem -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) {
                    Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
                    Write-Host "Removed empty dir: $($_.FullName)"
                }
            }
    }
}

$action = if ($DryRun) { 'matched' } else { 'removed' }
Write-Host "Cleanup done. Files ${action}: $removedFiles (older than $OlderThanDays days, before $($cutoff.ToString('yyyy-MM-dd')))"
