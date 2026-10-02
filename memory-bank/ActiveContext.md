# Active Context — Factorraria

> This file changes most often. Update it after meaningful work.

## Current focus
- **Project setup / tooling.** Establishing persistent context (Cline rules + this Memory Bank) and mapping the
  codebase. No gameplay feature work is in flight yet.

## Recent changes
- Verified the workspace and code layout.
- Noted tModLoader source location: `...\tModLoader-2026.08.3.0\patches\tModLoader\Terraria\` (no `src\` folder).
- Created global rule `...\Documents\Cline\Rules\global-preferences.md`.
- Created workspace rules: `.clinerules\project.md` and `.clinerules\memory-bank.md`.
- Created the `memory-bank\` folder with all six core files.

## Next steps (candidates — confirm with Ismail)
- Fill in `description.txt`.
- Decide the next feature/bug to tackle (pick a machine, the power grid, liquids, or conveyors).
- Optionally expand localization keys.

## Active decisions & considerations
- Keep the "one machine = one folder" convention when adding machines.
- Verify API signatures against the tModLoader source before writing code.
- Do not claim a successful build unless it actually ran through tModLoader/VS.

## Patterns & preferences
- Windows PowerShell; redirect long command output to a temp file and read it back.
- Prefer the editor tool for file edits.