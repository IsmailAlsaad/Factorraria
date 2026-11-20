# ==============================================================================
# FACTORRARIA HARVESTER BOX PROBE 10 (read-only for the project, changes nothing)
# Run:  powershell -ExecutionPolicy Bypass -File .\tools\harvester_probe10.ps1 -Dll "C:\GOG Games\Terraria\tModLoader\tModLoader.dll"
# Dumps vanilla tree shaking (shake loot when the staff fells a tree): ShakeTree whole, its once-per-tree bookkeeping, who calls it,
#   the item source it uses, and the shake related tile sets.
# Reads the decompiled WorldGen from tools\_probe_decomp when it exists (use -Fresh to decompile again). Writes tools\harvester_probe10_report.txt.
# Do not commit the report or _probe_decomp.
# ==============================================================================
param(
    [string]$Root = 'C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\Factorraria',
    [string]$Dll = '',
    [switch]$Fresh
)
$ErrorActionPreference = 'Continue'
$reportPath = Join-Path $Root 'tools\harvester_probe10_report.txt'
$outDir = Join-Path $Root 'tools\_probe_decomp'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s) }

# ---- find tModLoader.dll (Steam libraries, then the GOG folders) ----
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

    # GOG: the usual install folders on every drive
    foreach ($drive in @('C', 'D', 'E', 'F')) {
        foreach ($gog in @("${drive}:\GOG Games", "${drive}:\Program Files (x86)\GOG Galaxy\Games", "${drive}:\GOG Galaxy\Games")) {
            [void]$cands.Add((Join-Path $gog 'Terraria\tModLoader\tModLoader.dll'))
            [void]$cands.Add((Join-Path $gog 'tModLoader\tModLoader.dll'))
        }
    }
    $Dll = $cands | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

    # last resort: look inside the GOG folders a few levels deep
    if (-not $Dll) {
        foreach ($drive in @('C', 'D', 'E', 'F')) {
            $gogRoot = "${drive}:\GOG Games"
            if (Test-Path -LiteralPath $gogRoot) {
                $found = Get-ChildItem -LiteralPath $gogRoot -Filter 'tModLoader.dll' -Recurse -Depth 3 -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($found) { $Dll = $found.FullName; break }
            }
        }
    }
}
if (-not $Dll -or -not (Test-Path -LiteralPath $Dll)) { Write-Host "tModLoader.dll not found. Re-run with -Dll 'full\path\to\tModLoader.dll'" -ForegroundColor Red; exit 1 }
W "Dll: $Dll"

# ---- decompile what is missing (ilspycmd is only needed then) ----
$types = @('Terraria.WorldGen')
$files = @{}
$ilspyChecked = $false
foreach ($t in $types) {
    $f = Join-Path $outDir ($t + '.cs')
    $files[$t] = $f
    $have = (Test-Path -LiteralPath $f) -and ((Get-Item -LiteralPath $f).Length -gt 10000)
    if ($have -and -not $Fresh) {
        Write-Host "Using cached $t" -ForegroundColor DarkGray
    }
    else {
        if (-not $ilspyChecked) {
            $ilspyChecked = $true
            if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
                Write-Host 'Installing ilspycmd (dotnet tool)...' -ForegroundColor Yellow
                dotnet tool install -g ilspycmd | Out-Host
                $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
            }
            if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) { Write-Host 'ilspycmd is not available. Install the .NET SDK, then run: dotnet tool install -g ilspycmd' -ForegroundColor Red; exit 1 }
        }
        Write-Host "Decompiling $t ..." -ForegroundColor Cyan
        ilspycmd -t $t $Dll 2>$null | Out-File -FilePath $f -Encoding utf8
    }
    if (Test-Path -LiteralPath $f) { W ("{0}: {1} KB" -f $t, [Math]::Round((Get-Item -LiteralPath $f).Length / 1024)) } else { W "$t : MISSING (decompile failed)" }
}

# ---- helpers ----
$cache = @{}
function Lines([string]$file) {
    if (-not $cache.ContainsKey($file)) { $cache[$file] = @(Get-Content -LiteralPath $file) }
    return $cache[$file]
}
# The whole method that starts at the line matching $pattern (brace counting, like probe 2).
function Dump-Method([string]$file, [string]$pattern, [int]$maxLines) {
    $lines = Lines $file
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
# Every line matching $pattern with $before lines above and $after below (at most $limit hits).
function Dump-Around([string]$file, [string]$pattern, [int]$before, [int]$after, [int]$limit) {
    $hits = @(Select-String -LiteralPath $file -Pattern $pattern -Context $before, $after | Select-Object -First $limit)
    W ("# {0} hit(s) shown for '{1}' in {2}" -f $hits.Count, $pattern, (Split-Path $file -Leaf))
    foreach ($h in $hits) {
        W ("--- {0}:{1}" -f (Split-Path $file -Leaf), $h.LineNumber)
        foreach ($c in $h.Context.PreContext) { W $c }
        W ('>>> ' + $h.Line)
        foreach ($c in $h.Context.PostContext) { W $c }
        W ''
    }
}
# The matches of $pattern grouped by the method they sit in (the last method header above them): method, number of hits, file line numbers.
function List-Callers([string]$file, [string]$pattern) {
    $lines = Lines $file
    $leaf = Split-Path $file -Leaf
    $method = '(outside any method)'
    $order = New-Object System.Collections.ArrayList
    $nums = @{}
    $total = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\t(public|private|internal|protected)\s[^=;]*\(' -and $l -notmatch '^\t\t') { $method = $l.Trim() }
        if ($l -match $pattern) {
            $total++
            if (-not $nums.ContainsKey($method)) { $nums[$method] = New-Object System.Collections.ArrayList; [void]$order.Add($method) }
            [void]$nums[$method].Add($i + 1)
        }
    }
    W ("##### '{0}' in {1}: {2} line(s) in {3} method(s)" -f $pattern, $leaf, $total, $order.Count)
    foreach ($m in $order) {
        $shown = ($nums[$m] | Select-Object -First 12) -join ', '
        W ("  {0} hit(s) at line(s) {1}  in  {2}" -f $nums[$m].Count, $shown, $m)
    }
    W ''
}

$wg = $files['Terraria.WorldGen']
W '##### 1. ShakeTree (whole method)'
Dump-Method $wg 'static .*\bShakeTree\(' 700
W '##### 2. who calls it, and its bookkeeping (treeShake / numTreeShakes)'
List-Callers $wg 'ShakeTree\(|numTreeShakes|treeShakeX|maxTreeShakes'
Dump-Around $wg 'numTreeShakes|maxTreeShakes' 3 6 8
W '##### 3. IsTileALeafyTreeTop, GetTreeBottom users in the shake, GetItemSource_FromTreeShake'
Dump-Method $wg 'static .*\bIsTileALeafyTreeTop\(' 60
Dump-Method $wg 'static .*\bGetItemSource_FromTreeShake\(' 20
Dump-Around $wg 'IsShakeable' 3 6 6

[IO.File]::WriteAllText($reportPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Report written: $reportPath ($([Math]::Round($sb.Length / 1024)) KB)" -ForegroundColor Green
Write-Host 'Attach the report in the next chat.' -ForegroundColor Green
