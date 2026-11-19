# ==============================================================================
# FACTORRARIA HARVESTER BOX PROBE 3 (read-only for the project, changes nothing)
# Run:  powershell -ExecutionPolicy Bypass -File .\tools\harvester_probe3.ps1 -Dll "C:\GOG Games\Terraria\tModLoader\tModLoader.dll"
# Answers the "Still unknown" list of docs\harvester_box\ROADMAP.md 7c, plus how a herb seed is placed by hand (Phase 1 check):
#   A. planter box (tile 380) item ids and styles, and its frame / merge / draw rules
#   B. gemcorn and acorn items (ids, createTile, placeStyle)
#   C. every caller of GetTreeType( / IsTileTypeFitForTree( / TreeTypes in WorldGen, Main, TileDrawing, Player
#   D. herb seed placement path (PlaceAlch callers, herb TileObjectData anchors, can the data be edited)
# Reads the decompiled files of probe 2 from tools\_probe_decomp when they exist (use -Fresh to decompile again) and decompiles only what is
# missing. Writes tools\harvester_probe3_report.txt. Do not commit the report or _probe_decomp.
# Needs the .NET SDK (dotnet) only when something has to be decompiled; installs the ilspycmd tool if missing.
# ==============================================================================
param(
    [string]$Root = 'C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\Factorraria',
    [string]$Dll = '',
    [switch]$Fresh
)
$ErrorActionPreference = 'Continue'
$reportPath = Join-Path $Root 'tools\harvester_probe3_report.txt'
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
$types = @('Terraria.WorldGen', 'Terraria.ObjectData.TileObjectData', 'Terraria.TileObject', 'Terraria.Item', 'Terraria.ID.ItemID', 'Terraria.Player',
           'Terraria.Main', 'Terraria.GameContent.Drawing.TileDrawing')
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
$item = $files['Terraria.Item']
$itemId = $files['Terraria.ID.ItemID']

# ==================== A. planter box (tile 380) ====================
W '=================== A. PLANTER BOX (tile 380) ==================='
W '##### A1. ItemID names and ids of the planter boxes (the id order is the style order: style = id - first id, to be confirmed by A2)'
Dump-Around $itemId 'PlanterBox' 0 0 40
W '##### A2. Item.SetDefaults: every item that places tile 380 (createTile, placeStyle)'
Dump-Around $item 'createTile = 380\b' 6 6 20
W '##### A3. WorldGen.PlaceTile / KillTile / TileFrame: how tile 380 is placed, which item it drops, how its frames are set'
Dump-Around $wg 'case 380:|if \(num == 380\)' 3 22 6
W '##### A4. other lines that mention 380 in WorldGen (short)'
Dump-Around $wg '\b380\b' 0 0 60
W '##### A5. TileObjectData: the tile 380 entry, if there is one'
Dump-Around $files['Terraria.ObjectData.TileObjectData'] '\b380\b|PlanterBox' 6 14 12
W '##### A6. TileDrawing (how a planter box is drawn: frames, offsets, platform-like top)'
Dump-Around $files['Terraria.GameContent.Drawing.TileDrawing'] '\b380\b|PlanterBox' 4 10 20
W '##### A7. Main: tile flag arrays that mention the planter box'
Dump-Around $files['Terraria.Main'] '\b380\b|PlanterBox' 1 2 30

# ==================== B. gemcorns and acorns ====================
W '=================== B. GEMCORNS AND ACORNS ==================='
W '##### B1. ItemID names (acorn / gemcorn / sapling / seed-like names)'
Dump-Around $itemId 'Acorn|Gemcorn|GemTree|Sapling' 0 0 80
W '##### B2. Item.SetDefaults: items with createTile 590 (gem sapling?), 595, 615 and 20 (plain sapling), with their placeStyle'
foreach ($ct in @('590', '595', '615', '20')) {
    W "##### createTile = $ct"
    Dump-Around $item ('createTile = ' + $ct + '\b') 8 6 40
}
W '##### B3. Item.SetDefaults: the item ids that GetTreeType / gem tree growth read (placeStyle on saplings)'
Dump-Around $item 'Gemcorn|Acorn' 6 6 40
W '##### B4. WorldGen: how the sapling tile style picks the tree (gem trees)'
Dump-Around $wg 'GrowGemTree|GemTree|\b(590)\b' 4 14 30

# ==================== C. callers of the tree type functions ====================
W '=================== C. CALLERS OF THE TREE TYPE FUNCTIONS ==================='
W '##### C1. full IsTileTypeFitForTree and GetTreeType (and DefaultTreeWallTest)'
Dump-Method $wg 'static .*\bIsTileTypeFitForTree\(' 120
Dump-Method $wg 'static .*\bGetTreeType\(' 120
Dump-Method $wg 'static .*\bDefaultTreeWallTest\(' 60
W '##### C2. every caller, with the method it sits in'
foreach ($k in @('Terraria.WorldGen', 'Terraria.Main', 'Terraria.GameContent.Drawing.TileDrawing', 'Terraria.Player', 'Terraria.Item', 'Terraria.TileObject')) {
    foreach ($p in @('GetTreeType\(', 'IsTileTypeFitForTree\(', 'TreeTypes', 'GetTreeFrame|GetTreeBiome|GetTreeStyle')) {
        List-Callers $files[$k] $p
    }
}

# ==================== D. herb seed placement ====================
W '=================== D. HERB SEED PLACEMENT (Phase 1 check) ==================='
W '##### D1. who calls PlaceAlch / CheckAlch / GrowAlch'
foreach ($k in @('Terraria.WorldGen', 'Terraria.Player', 'Terraria.Main', 'Terraria.TileObject', 'Terraria.Item')) {
    List-Callers $files[$k] 'PlaceAlch\(|CheckAlch\(|GrowAlch\('
}
W '##### D2. herb seed items: createTile 82 (immature herb) and its placeStyle'
Dump-Around $item 'createTile = 82\b' 6 6 30
W '##### D3. TileObjectData: the herb tiles 82 / 83 / 84 (anchors)'
Dump-Around $files['Terraria.ObjectData.TileObjectData'] 'addTile\((82|83|84)\)|ImmatureHerbs|MatureHerbs|BloomingHerbs|Main\.tileAlch' 30 3 12
W '##### D4. TileObjectData: can the anchor lists still be edited after loading? (write checks / read only flag)'
Dump-Around $files['Terraria.ObjectData.TileObjectData'] 'LockWrite|readOnlyData|_anchorValidTiles|_anchorAlternateTiles' 1 4 40
W '##### D5. TileObject.CanPlace: how the anchors are tested'
Dump-Around $files['Terraria.TileObject'] 'AnchorAlternateTiles|AnchorValidTiles|AnchorInvalidTiles' 3 4 20
W '##### D6. WorldGen.PlaceTile: the case that handles herbs / tileAlch'
Dump-Around $wg 'tileAlch|case 82:' 2 10 40

[IO.File]::WriteAllText($reportPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Report written: $reportPath ($([Math]::Round($sb.Length / 1024)) KB)" -ForegroundColor Green
Write-Host 'Paste the report (or attach it) in the next chat.' -ForegroundColor Green
