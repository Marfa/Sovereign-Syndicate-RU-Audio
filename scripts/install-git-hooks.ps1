# Install tracked git hooks from .githooks/ into .git/hooks/
$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$srcDir = Join-Path $RepoRoot ".githooks"
$dstDir = Join-Path $RepoRoot ".git\hooks"

if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
    throw "Not a git repository: $RepoRoot"
}
if (-not (Test-Path $srcDir)) {
    throw "Missing .githooks directory: $srcDir"
}

New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
Get-ChildItem -Path $srcDir -File | ForEach-Object {
    $dest = Join-Path $dstDir $_.Name
    Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
    Write-Host "Installed hook: $($_.Name) -> $dest"
}
