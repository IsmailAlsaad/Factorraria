# Tech Context — Factorraria

## Stack
- **Language:** C# (tModLoader mod).
- **Engine/framework:** Terraria + tModLoader.
- **tModLoader version referenced for lookups:** 2026.08.3.0.
- **SDK-style project:** `Factorraria.csproj` (Microsoft.NET.Sdk) importing `..\tModLoader.targets`.

## Dependencies
- `ModLiquidLib` — `..\ModAssemblies\ModLiquidLib_v2025.12.202601.dll` (+ `.xml` docs). Custom liquid system.
- `Luminance` — `..\ModAssemblies\Luminance_v1.0.14.dll` (+ `.xml` docs). UI/rendering helpers.
- Declared in `build.txt` as `modReferences = ModLiquidLib, Luminance`.

## Build / run setup
- Mod sources root: `C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\`
- This mod: `...\ModSources\Factorraria\`
- Reference DLLs: `...\ModSources\ModAssemblies\`
- Launch profiles: `Properties\launchSettings.json` (Terraria / TerrariaServer) using `$(tMLPath)` / `$(tMLSteamPath)`.
- Building normally happens through tModLoader/VS (needs Steam install path from `tMLMod.targets`). Do not assume a
  standalone `dotnet build` will work from a plain shell; verify before claiming a build succeeded.

## tModLoader API source for exact signature lookups
- Repo root: `C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader-2026.08.3.0`
- **`src\` does NOT exist in this checkout.** The real API source is at:
  `...\tModLoader-2026.08.3.0\patches\tModLoader\Terraria\`
  (e.g. `...\patches\tModLoader\Terraria\ModLoader\ModItem.cs`, `ModTile.cs`, `ModTileEntity.cs`,
  `ModSystem.cs`, `ModContent.cs`, `GlobalItem.cs`, `GlobalTile.cs`, `ModProjectile.cs`, `ModMount.cs`, etc.)
- Vanilla decompiled sources: `...\tModLoader-2026.08.3.0\patches\Terraria\` and `...\patches\TerrariaNetCore\`.
- When unsure about any hook/property, read the exact source there first.

## Environment constraints
- OS: Windows; shell: PowerShell.
- Terminal output capture via shell integration is unreliable. When a command's output matters, redirect it to a
  temp file (`Set-Content`) and read that file back.
- `description.txt` is still the default placeholder.

## Localization
- hjson files under `Localization\`. Keys for this mod in `en-US.hjson`; cross-referencing other mods in
  `en-US_Mods.Factorraria.hjson`.

## Assets
- `Assets\Atlases\Particles_Atlas.json` + `.png` for particle sprites.
- Machine sprites live next to their code (`_Off` / `_On` variants).