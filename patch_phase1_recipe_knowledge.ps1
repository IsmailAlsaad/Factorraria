# ==============================================================================
# FACTORRARIA PATCH: Phase 1 - Recipe Discovery (world knowledge + UI visibility)
# ==============================================================================
# Adds a world-level "which recipes has this world learned" system and makes the
# machine recipe browser show only learned recipes (hidden by default).
#
# NEW FILES
#   Common\Knowledge\RecipeKey.cs           stable save-safe id for one recipe group
#   Common\Knowledge\RecipeKnowledgeSystem.cs  world-level discovered set (saved with world)
#   Common\Machines\RecipeVisibility.cs     the one place that answers "is this recipe visible?"
# EDITS
#   Common\Systems\MachineRegisterationSystem.cs  learn each machine's book (+ using)
#   Common\Machines\BaseMachine.cs                finishing a craft teaches the world
#   Common\UI\CustomUIElements\RecipeSelectHammerIcon.cs  browser hides undiscovered recipes
#
# USAGE (run from the project root, PowerShell):
#   powershell -ExecutionPolicy Bypass -File .\patch_phase1_recipe_knowledge.ps1 -DryRun
#   powershell -ExecutionPolicy Bypass -File .\patch_phase1_recipe_knowledge.ps1
# ==============================================================================

[CmdletBinding()]
param(
    [string]$Root,
    [switch]$DryRun
)
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path } }
$ErrorActionPreference = 'Stop'

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

# ============================== PATCH CONTENT =================================
# --- New file: stable, save-safe id for one recipe's output group ------------
NewFile 'Common\Knowledge\RecipeKey.cs' 'RecipeKey' @'
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// A stable, save-safe identifier for one recipe group (one output type).
    /// Uses the item's full internal name (e.g. "Terraria/IronBar" or "Factorraria/SteelBarItem"),
    /// never its runtime id, so learned recipes survive mod load-order changes and localization.
    /// </summary>
    public static class RecipeKey
    {
        public static string For(RecipeOutputGroup group) => For(group.Key);

        public static string For(RecipeOutputKey key) =>
            key.IsLiquid
                ? "l|" + key.Id                       // liquid ids come from the append-only LiquidTypeRegistry
                : "i|" + Terraria.ID.ItemID.Search.GetName(key.Id);
    }
}
'@

# --- New file: the world-level set of learned recipes ------------------------
NewFile 'Common\Knowledge\RecipeKnowledgeSystem.cs' 'RecipeKnowledgeSystem' @'
using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Which recipes this WORLD has learned. Discovery is world-level (shared by every player who
    /// joins the world) and does not depend on mods the host happens to have enabled.
    ///
    /// A recipe is a KEY = one recipe group (one output type), e.g. "i|Terraria/IronBar".
    /// Learn() it when a machine finishes that recipe; the UI then stops hiding it.
    /// Hidden-by-default: a fresh world knows nothing until something is crafted.
    /// </summary>
    public class RecipeKnowledgeSystem : ModSystem
    {
        static readonly HashSet<string> known = new HashSet<string>();

        /// <summary>True if the world has learned this output group. UI uses this to filter the browser.</summary>
        public static bool IsKnown(RecipeOutputGroup group) => group != null && known.Contains(RecipeKey.For(group));

        public static bool IsKnown(RecipeOutputKey key) => known.Contains(RecipeKey.For(key));

        /// <summary>Learns a single output group (the world "discovers" that recipe).</summary>
        public static void Learn(RecipeOutputGroup group) => Learn(group.Key);

        /// <summary>Learns one output key. Called when a machine finishes a craft.</summary>
        public static void Learn(RecipeOutputKey key)
        {
            if (!known.Add(RecipeKey.For(key))) return;
            if (!Main.dedServ) Main.NewText($"Discovered a new recipe: {key.DisplayName}", 110, 220, 110);
            // TODO (later phase): send a packet so the host can tell clients too.
        }

        /// <summary>Learns every group in a book. Safe to call once per machine book after recipes are built.</summary>
        public static void LearnBook(RecipeBook book)
        {
            if (book == null) return;
            foreach (RecipeOutputGroup group in book.Groups) Learn(group);
        }

        public static void Forget(RecipeOutputKey key) => known.Remove(RecipeKey.For(key));

        // Persistence: just the list of learned keys. Absent = everything hidden.
        public override void SaveWorldData(TagCompound tag)
        {
            tag["KnownRecipeKeys"] = new List<string>(known);
        }

        public override void LoadWorldData(TagCompound tag)
        {
            known.Clear();
            if (!tag.ContainsKey("KnownRecipeKeys")) return;
            foreach (string key in tag.Get<List<string>>("KnownRecipeKeys")) known.Add(key);
        }

        public override void OnWorldUnload()
        {
            known.Clear();
        }

        public override void Unload()
        {
            known.Clear();
        }
    }
}
'@

# --- New file: the single source of truth for "is this recipe visible?" ------
NewFile 'Common\Machines\RecipeVisibility.cs' 'RecipeVisibility' @'
using Factorraria.Common.Knowledge;
using Factorraria.Content.Configs;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    /// <summary>
    /// One place that answers "should the player see this recipe in a machine browser?".
    /// Today: only recipes the world has learned, plus a debug reveal-all. Kept as its own class so a
    /// later phase can add more rules (creative mode, knowledge items, ...) without touching the UI.
    /// </summary>
    public static class RecipeVisibility
    {
        public static bool IsVisible(RecipeOutputGroup group) =>
            RecipeKnowledgeSystem.IsKnown(group) ||
            ModContent.GetInstance<FurnaceOffsetConfig>().RevealAllRecipes;   // debug: show everything
    }
}
'@

# --- Edit 1: machine hub learns every book as soon as recipes are built ------
Edit 'Common\Systems\MachineRegisterationSystem.cs' 'hub-learns-books' @'
using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Motors;
'@ @'
using Factorraria.Common.Knowledge;
using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Motors;
'@
Edit 'Common\Systems\MachineRegisterationSystem.cs' 'learn-after-build' @'
            AutohammerRecipeRegistry.BuildRecipes();
            // etc...
        }
'@ @'
            AutohammerRecipeRegistry.BuildRecipes();

            // Recipe discovery: the world starts out knowing every recipe that already existed
            // before this system was added, so nothing the player could already make disappears.
            RecipeKnowledgeSystem.LearnBook(FurnaceRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(HellforgeRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(SolidifierRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(IceMachineRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(LiquidDistillatorRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(AutohammerRecipeRegistry.Book);
            // etc...
        }
'@

# --- Edit 2: finishing a craft teaches the world that recipe -----------------
Edit 'Common\Machines\BaseMachine.cs' 'learn-on-finish' @'
using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
'@ @'
using Factorraria.Common.Knowledge;
using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
'@
Edit 'Common\Machines\BaseMachine.cs' 'learn-in-finishrecipe' @'
            WorkProgress = 0;
            OnRecipeFinished(recipe);
        }
'@ @'
            WorkProgress = 0;

            // Discovery: finishing a recipe teaches the world that it exists.
            if (recipe.TryGetPrimaryOutput(out RecipeOutputKey discoveredKey))
                RecipeKnowledgeSystem.Learn(discoveredKey);

            OnRecipeFinished(recipe);
        }
'@

# --- Edit 3: the recipe browser only shows learned recipes -------------------
Edit 'Common\UI\CustomUIElements\RecipeSelectHammerIcon.cs' 'using-machines' @'
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
'@ @'
using Factorraria.Common.Knowledge;
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
'@
Edit 'Common\UI\CustomUIElements\RecipeSelectHammerIcon.cs' 'filter-known' @'
            for (int i = 0; i < machineGroups.Count; i++)
            {
                if (!MatchesFilter(machineGroups[i])) continue;
'@ @'
            for (int i = 0; i < machineGroups.Count; i++)
            {
                if (!RecipeVisibility.IsVisible(machineGroups[i])) continue;   // hide undiscovered recipes
                if (!MatchesFilter(machineGroups[i])) continue;
'@

# --- Edit 4: debug opt-in - reveals every recipe ----------------------------
Edit 'Content\Configs\FurnaceOffsetConfig.cs' 'debug-flag' @'
        public bool EnableDebugs;
'@ @'
        public bool EnableDebugs;

        [Tooltip("Recipe Discovery debug: show EVERY recipe in machine browsers instead of only learned ones.")]
        public bool RevealAllRecipes;
'@
# ------------------------------ PATCH CONTENT END -----------------------------

Run-Patch