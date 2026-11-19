# ==============================================================================
# FACTORRARIA HARVESTER BOX PROBE (read-only, changes nothing)
# Run:  powershell -ExecutionPolicy Bypass -File .\tools\harvester_probe.ps1
# Writes tools\harvester_probe_report.txt with the vanilla code the Harvester Box depends on.
# Do not commit the report.
# ==============================================================================
param(
    [string]$Root = 'C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\Factorraria',
    [string]$TmlRoot = 'C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader-2026.08.3.0'
)
$ErrorActionPreference = 'Continue'
$reportPath = Join-Path $Root 'tools\harvester_probe_report.txt'
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s) }

function Find-Sources([string[]]$names) {
    Get-ChildItem -Path $TmlRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $names -contains $_.Name } | Select-Object -ExpandProperty FullName
}

# Prints a whole method (brace matched, capped) for every line that matches $pattern.
function Dump-Method([string]$file, [string]$pattern, [int]$maxLines) {
    $lines = Get-Content -LiteralPath $file
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $pattern) {
            W ("--- {0}:{1}" -f $file, ($i + 1))
            $depth = 0; $started = $false
            $end = [Math]::Min($lines.Count, $i + $maxLines)
            for ($j = $i; $j -lt $end; $j++) {
                W $lines[$j]
                $o = ([regex]::Matches($lines[$j], '\{')).Count
                $c = ([regex]::Matches($lines[$j], '\}')).Count
                $depth += $o - $c
                if ($o -gt 0) { $started = $true }
                if ($started -and $depth -le 0) { break }
            }
            W ''
        }
    }
}

function Dump-Matches([string]$file, [string]$pattern, [int]$after, [int]$limit) {
    $hits = Select-String -LiteralPath $file -Pattern $pattern -Context 0, $after | Select-Object -First $limit
    foreach ($h in $hits) {
        W ("--- {0}:{1}" -f $file, $h.LineNumber)
        W $h.Line
        foreach ($c in $h.Context.PostContext) { W $c }
        W ''
    }
}

W "Harvester Box probe $(Get-Date -Format s)"
W "TmlRoot: $TmlRoot"

$worldGen = Find-Sources @('WorldGen.cs', 'WorldGen.cs.patch')
$tileObj  = Find-Sources @('TileObject.cs', 'TileObject.cs.patch', 'TileObjectData.cs', 'TileObjectData.cs.patch')
$itemSrc  = Find-Sources @('Item.cs', 'Item.cs.patch', 'ItemID.cs', 'ItemID.cs.patch')
W "WorldGen sources: $($worldGen -join ' | ')"
W "TileObject sources: $($tileObj -join ' | ')"
W "Item sources: $($itemSrc -join ' | ')"
W ''

foreach ($f in $worldGen) {
    W "=================== $f ==================="
    foreach ($p in @('static .*\bCheckAlch\(', 'static .*\bPlaceAlch\(', 'static .*\bGrowAlch\(', 'static .*\bGrowAlch', 'static .*\bPlantAlch\(',
                     'static .*\bIsHarvestableHerbWithSeed\(', 'static .*\bCheckSapling', 'static .*\bGrowTree\(', 'static .*\bTryGrowingTreeByType\(',
                     'static .*\bGetTreeType\(', 'static .*\bGrowTreeWithSettings\(', 'static .*\bGrowPalmTree\(', 'static .*\bGrowAshTree\(', 'static .*\bGrowGemTree')) {
        Dump-Method $f $p 260
    }
    foreach ($p in @('ImmatureHerbs', 'MatureHerbs', 'BloomingHerbs', 'PlanterBox', 'TileID\.Saplings', 'Gemcorn', 'case 380')) {
        W "##### matches for $p"
        Dump-Matches $f $p 5 40
    }
}

foreach ($f in $tileObj) {
    W "=================== $f ==================="
    foreach ($p in @('PlanterBox', 'ImmatureHerbs', 'MatureHerbs', 'BloomingHerbs', 'Saplings')) {
        W "##### matches for $p"
        Dump-Matches $f $p 8 20
    }
}

foreach ($f in $itemSrc) {
    W "=================== $f ==================="
    foreach ($p in @('PlanterBox', 'Gemcorn', 'Acorn')) {
        W "##### matches for $p"
        Dump-Matches $f $p 4 40
    }
}

[IO.File]::WriteAllText($reportPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Report written: $reportPath ($([Math]::Round($sb.Length / 1024)) KB)" -ForegroundColor Green