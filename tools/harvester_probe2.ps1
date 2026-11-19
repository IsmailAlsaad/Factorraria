# ==============================================================================
# FACTORRARIA HARVESTER BOX PROBE 2 (read-only for the project, changes nothing)
# Run:  powershell -ExecutionPolicy Bypass -File .\tools\harvester_probe2.ps1
# Decompiles the vanilla types the Harvester Box depends on straight from tModLoader.dll (so it shows the REAL
# code that runs, tML hooks included) and writes tools\harvester_probe2_report.txt. Do not commit the report or _probe_decomp.
# Needs the .NET SDK (dotnet) and installs the ilspycmd tool if missing. Pass -Dll if the script cannot find tModLoader.dll.
# ==============================================================================
param(
    [string]$Root = 'C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\Factorraria',
    [string]$Dll = ''
)
$ErrorActionPreference = 'Continue'
$reportPath = Join-Path $Root 'tools\harvester_probe2_report.txt'
$outDir = Join-Path $Root 'tools\_probe_decomp'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s) }

# ---- find tModLoader.dll ----
if (-not $Dll) {
    $cands = New-Object System.Collections.ArrayList
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    $libs = @()
    if ($steam) {
        $libs += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) { foreach ($m in (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"')) { $libs += $m.Matches[0].Groups[1].Value.Replace('\\', '\') } }
    }
    $libs += 'C:\Program Files (x86)\Steam', 'D:\SteamLibrary', 'E:\SteamLibrary'
    foreach ($l in ($libs | Select-Object -Unique)) { [void]$cands.Add((Join-Path $l 'steamapps\common\tModLoader\tModLoader.dll')) }
    $Dll = $cands | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Dll -or -not (Test-Path -LiteralPath $Dll)) { Write-Host "tModLoader.dll not found. Re-run with -Dll 'full\path\to\tModLoader.dll'" -ForegroundColor Red; exit 1 }
W "Dll: $Dll"

# ---- ilspycmd ----
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    Write-Host 'Installing ilspycmd (dotnet tool)...' -ForegroundColor Yellow
    dotnet tool install -g ilspycmd | Out-Host
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
}
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) { Write-Host 'ilspycmd is not available. Install the .NET SDK, then run: dotnet tool install -g ilspycmd' -ForegroundColor Red; exit 1 }

$types = @('Terraria.WorldGen', 'Terraria.ObjectData.TileObjectData', 'Terraria.TileObject', 'Terraria.Item', 'Terraria.ID.ItemID', 'Terraria.Player')
$files = @{}
foreach ($t in $types) {
    $f = Join-Path $outDir ($t + '.cs')
    Write-Host "Decompiling $t ..." -ForegroundColor Cyan
    ilspycmd -t $t $Dll 2>$null | Out-File -FilePath $f -Encoding utf8
    $files[$t] = $f
    W ("{0}: {1} KB" -f $t, [Math]::Round((Get-Item $f).Length / 1024))
}

function Dump-Method([string]$file, [string]$pattern, [int]$maxLines) {
    $lines = Get-Content -LiteralPath $file
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $pattern) {
            W ("--- {0}:{1}" -f (Split-Path $file -Leaf), ($i + 1))
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
        W ("--- {0}:{1}" -f (Split-Path $file -Leaf), $h.LineNumber)
        W $h.Line
        foreach ($c in $h.Context.PostContext) { W $c }
        W ''
    }
}

$wg = $files['Terraria.WorldGen']
W '=================== WorldGen methods ==================='
foreach ($p in @('static .*\bCheckAlch\(', 'static .*\bPlaceAlch\(', 'static .*\bGrowAlch\(', 'static .*\bPlantAlch\(', 'static .*\bIsHarvestableHerbWithSeed\(',
                 'static .*\bCheckSapling', 'static .*\bGrowTree\(', 'static .*\bAttemptToGrowTreeFromSapling\(', 'static .*\bTryGrowingTreeByType\(',
                 'static .*\bGetTreeType\(', 'static .*\bGrowPalmTree\(', 'static .*\bGrowAshTree\(', 'static .*\bGrowGemTree', 'static .*\bCheck1x2\(', 'static .*\bPlace1x2\(')) {
    Dump-Method $wg $p 400
}
W '=================== WorldGen matches ==================='
foreach ($p in @('PlanterBox', '\b380\b', 'Gemcorn', 'case 82:', 'case 83:', 'case 84:')) {
    W "##### $p"
    Dump-Matches $wg $p 6 40
}
W '=================== TileObjectData / TileObject ==================='
foreach ($k in @('Terraria.ObjectData.TileObjectData', 'Terraria.TileObject')) {
    foreach ($p in @('PlanterBox', 'ImmatureHerbs', 'MatureHerbs', 'BloomingHerbs', 'Saplings')) {
        W "##### $k : $p"
        Dump-Matches $files[$k] $p 12 12
    }
}
W '=================== Item / ItemID ==================='
foreach ($p in @('PlanterBox', 'Gemcorn', 'Acorn')) {
    W "##### Item : $p"
    Dump-Matches $files['Terraria.Item'] $p 5 60
    W "##### ItemID : $p"
    Dump-Matches $files['Terraria.ID.ItemID'] $p 0 60
}

[IO.File]::WriteAllText($reportPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Report written: $reportPath ($([Math]::Round($sb.Length / 1024)) KB)" -ForegroundColor Green