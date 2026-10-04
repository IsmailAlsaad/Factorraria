# ==============================================================================
# FACTORRARIA PATCH ENGINE - dot-sourced by every patch_*.ps1. Do not edit casually.
# Provides Edit / NewFile / Run-Patch. Expects $Root and $DryRun from the calling patch.
# All-or-nothing: every Find must match EXACTLY ONCE (whitespace included) in the
# current file, and every New file must not already exist. Otherwise nothing is written.
# ==============================================================================
# ----------------------------- ENGINE (do not edit) ---------------------------
$script:Ops = New-Object System.Collections.ArrayList
function Edit([string]$File, [string]$Label, [string]$Find, [string]$Replace) {
    [void]$script:Ops.Add(@{ Kind = 'Edit'; File = $File; Label = $Label; Find = $Find; Replace = $Replace })
}
function NewFile([string]$File, [string]$Label, [string]$Content) {
    [void]$script:Ops.Add(@{ Kind = 'New'; File = $File; Label = $Label; Content = $Content })
}
function Norm([string]$s) { return $s.Replace("`r`n", "`n").Replace("`r", "`n") }
function CountOf([string]$hay, [string]$needle) {
    $n = 0; $i = 0
    while (($i = $hay.IndexOf($needle, $i, [StringComparison]::Ordinal)) -ge 0) { $n++; $i += $needle.Length }
    return $n
}
function FullPath([string]$rel) {
    $full = [IO.Path]::GetFullPath((Join-Path $Root $rel))
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes project root: $rel" }
    return $full
}
function ReadSrc([string]$full) {
    $bytes = [IO.File]::ReadAllBytes($full)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $raw = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes, $(if ($bom) { 3 } else { 0 }), $bytes.Length - $(if ($bom) { 3 } else { 0 }))
    $eol = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
    return @{ Text = (Norm $raw); Eol = $eol; Bom = $bom }
}

function Run-Patch {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Project root not found: $Root" }
    $errors = New-Object System.Collections.ArrayList
    $state = @{}      # full path -> @{ Text; Eol; Bom; Dirty; IsNew }

    # ---- PHASE 1: verify everything against the files as they are on disk ----
    foreach ($op in $script:Ops) {
        $full = FullPath $op.File
        if ($op.Kind -eq 'New') {
            if (Test-Path -LiteralPath $full) { [void]$errors.Add("[$($op.Label)] NEW file already exists: $($op.File)") }
            continue
        }
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { [void]$errors.Add("[$($op.Label)] file not found: $($op.File)"); continue }
        if (-not $state.ContainsKey($full)) { $state[$full] = ReadSrc $full }
        $find = Norm $op.Find
        if ($find.Length -eq 0) { [void]$errors.Add("[$($op.Label)] empty Find text"); continue }
        $c = CountOf $state[$full].Text $find
        if ($c -eq 0) {
            $first = ($find -split "`n")[0].Trim()
            $hint = 'first line of Find not found either'
            if ($first.Length -gt 0) {
                $lines = $state[$full].Text -split "`n"
                for ($k = 0; $k -lt $lines.Count; $k++) { if ($lines[$k].Contains($first)) { $hint = "first line of Find appears at line $($k + 1), so the text after it differs"; break } }
            }
            [void]$errors.Add("[$($op.Label)] NO MATCH in $($op.File) ($hint)")
        } elseif ($c -gt 1) {
            [void]$errors.Add("[$($op.Label)] AMBIGUOUS: Find matches $c times in $($op.File)")
        }
    }
    if ($errors.Count -gt 0) {
        Write-Host ''
        Write-Host "PATCH HALTED - nothing was written. $($errors.Count) problem(s):" -ForegroundColor Red
        foreach ($e in $errors) { Write-Host "  - $e" -ForegroundColor Red }
        Write-Host 'Your local code differs from what the patch expected. Send me the current text of the files above.' -ForegroundColor Yellow
        $global:LASTEXITCODE = 1
        return
    }

    # ---- PHASE 2: apply in memory (re-check each Find so overlapping edits can't corrupt) ----
    foreach ($op in $script:Ops) {
        $full = FullPath $op.File
        if ($op.Kind -eq 'New') {
            $state[$full] = @{ Text = (Norm $op.Content); Eol = "`r`n"; Bom = $true; Dirty = $true; IsNew = $true }
            continue
        }
        $find = Norm $op.Find; $rep = Norm $op.Replace
        $s = $state[$full]
        if ((CountOf $s.Text $find) -ne 1) {
            Write-Host "PATCH HALTED - nothing was written. [$($op.Label)] overlaps an earlier edit in $($op.File)." -ForegroundColor Red
            $global:LASTEXITCODE = 1; return
        }
        $idx = $s.Text.IndexOf($find, [StringComparison]::Ordinal)
        $s.Text = $s.Text.Substring(0, $idx) + $rep + $s.Text.Substring($idx + $find.Length)
        $s.Dirty = $true
    }

    if ($DryRun) {
        Write-Host "DRY RUN OK - all $($script:Ops.Count) operation(s) match. Nothing written." -ForegroundColor Green
        return
    }

    # ---- PHASE 3: write temp files first, then swap them in ----
    $temps = @{}
    try {
        foreach ($full in @($state.Keys)) {
            $s = $state[$full]
            if (-not $s.Dirty) { continue }
            $dir = [IO.Path]::GetDirectoryName($full)
            if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir) }
            $text = if ($s.Eol -eq "`r`n") { $s.Text.Replace("`n", "`r`n") } else { $s.Text }
            $body = (New-Object System.Text.UTF8Encoding($false)).GetBytes($text)
            if ($s.Bom) { $body = [byte[]](0xEF, 0xBB, 0xBF) + $body }
            $tmp = "$full.patchtmp"
            [IO.File]::WriteAllBytes($tmp, $body)
            $temps[$full] = $tmp
        }
    } catch {
        foreach ($t in $temps.Values) { if (Test-Path -LiteralPath $t) { Remove-Item -LiteralPath $t -Force } }
        throw
    }
    foreach ($full in $temps.Keys) { Move-Item -LiteralPath $temps[$full] -Destination $full -Force }

    Write-Host ''
    Write-Host "PATCH APPLIED - $($temps.Count) file(s) written:" -ForegroundColor Green
    foreach ($full in $temps.Keys) { Write-Host "  $($full.Substring([IO.Path]::GetFullPath($Root).TrimEnd('\','/').Length + 1))" }
}
# ------------------------------- END ENGINE -----------------------------------
