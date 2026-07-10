# Copy generated wav files to installed game Mods folder.
$src = 'C:\Temp\SovereignSyndicateVoice\voice'
$dst = 'C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate\Mods\SovereignSyndicateVoice\voice'
if (-not (Test-Path $src)) { Write-Error "Missing $src"; exit 1 }
New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($ch in @('atticus','clara','otto')) {
    $from = Join-Path $src $ch
    $to = Join-Path $dst $ch
    if (-not (Test-Path $from)) { continue }
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    Copy-Item (Join-Path $from '*.wav') $to -Force
    $count = (Get-ChildItem $to -Filter '*.wav').Count
    Write-Host "$ch : $count wav"
}
Write-Host "Done -> $dst"
