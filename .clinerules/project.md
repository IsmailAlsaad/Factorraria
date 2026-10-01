# Factorraria — Project Context (persistent memory)

## What this is
**Factorraria** is a Terraria tModLoader automation/industry mod (Factorio-inspired).
- Author: Ismail, version 0.1
- Entry point: `Factorraria.cs` -> `public class Factorraria : Mod`
- Root namespace: `Factorraria`
- `description.txt` is still the default placeholder (needs writing).

## Build metadata (`build.txt`)
- displayName = Factorraria
- modReferences = `ModLiquidLib`, `Luminance`

## External references
- Mod DLLs live at `..\ModAssemblies\` (i.e. `...\tModLoader\ModSources\ModAssemblies\`).
- `ModLiquidLib_v2025.12.202601.dll` (+ `.xml` docs) — custom liquid library.
- `Luminance_v1.0.14.dll` (+ `.xml` docs) — UI/rendering library.
- When API is unclear, read the `.xml` docs next to these DLLs, and the tModLoader source below.

## tModLoader API source for lookups
- Repo root: `C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader-2026.08.3.0`
- The `src\` folder does NOT exist here. The real API source is at:
  `C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader-2026.08.3.0\patches\tModLoader\Terraria\`
- Vanilla sources: `...\patches\Terraria\` and `...\patches\TerrariaNetCore\`.
- Always verify exact signatures there before writing/fixing mod code.

## Code layout
- `Common\Liquids` — liquid network registries, `LiquidStack`, texture cache, `PipeTileBase`, `MotorTileBase`/`MotorTileEntityBase`, `PipeConnectionHelper`.
- `Common\Machines` — `BaseMachine`, `BaseRecipeRegistery`, `CustomRecipe`, `ElectricMachine`, `IElectric`, `MachineBiome`, machine UI/visual helpers.
- `Common\Networks` — `LiquidNetwork`, `PowerNetwork`, `PriorityConveyorNetwork`.
- `Common\Systems` — `ConveyorPhysicsSystem`, `CustomWireSystem`, `LiquidNetworkSystem`, `PowerGridSystem`, `RecipeSystem`, `VirtualItemSystem`, `MachineUISystem`, debug systems.
- `Common\UI` — liquid tank UI elements, `MachineUIRegistry`, `MachineUIStateBase`, recipe select hammer icon, item slot wrapper.
- `Common\VirtualItems` — `VirtualItem`, `VItemGlobalItem`.
- `Content\Items` — Conveyors, Hooks (MechanicalArm), JokeItems, Liquids (Buckets/Motors/Pipes), Mounts (MechSuit), Weapons (IceBlade), Wires (CopperWire/TinWire/IronCutter/LeadCutter).
- `Content\Liquids\Oil` — custom Oil liquid (Liquid/Fall/Block/Slope sprites).
- `Content\Projectiles` — MechanicalArm, MechSuit arm, IceBlade swing.
- `Content\Tiles\Machines` — Furnace, Motors, Pipes (GeneralMachines); Autohammer, IceMachine, Solidifier (ElectricalConsumers); GelBurner (ElectricalProducer).
- `Content\Tiles\Conveyors` — priority conveyor tiles (share `PriorityConveyorTileSpriteSheet.png`).
- `Content\Configs\FurnaceOffsetConfig.cs`, `Content\Particles\PuffExplosion.cs`.
- `Localization\en-US.hjson`, `en-US_Mods.Factorraria.hjson`.

## Architecture summary (keep in mind when adding features)
- Custom liquid system (Oil) with liquid networks, tanks, pipes, fluid motors (ModLiquidLib-based).
- Power/electricity grid: producers vs consumers, `IElectric` interface, `ElectricMachine` base.
- Machine framework: `BaseMachine` + per-machine `TileEntity` + `UIState` + recipe registry (`CustomRecipe`).
- Custom wiring + conveyor physics with priority routing.
- A Mechanical Arm (IK) item/projectile and a Mech Suit mount.

## Conventions
- One machine = folder with `<Name>TileEntity.cs`, `<Name>UIState.cs`, `<Name>RecipeRegistry.cs`, and `_Off`/`_On` (+ optional liquid) PNGs.
- Localization uses hjson (`Localization\en-US.hjson` for the mod, `en-US_Mods.Factorraria.hjson` for cross-mod keys).

## Environment notes
- Shell is Windows PowerShell. Terminal output capture is unreliable; write command output to a temp file and read it unless output is trivially short.
- Prefer using the editor tool for file edits.

## How to resume work
When a new chat starts, read this file plus the files you're about to touch, then continue. If work spans many files, keep `activeContext`/`progress`-style notes in the project `memory-bank\` if present.