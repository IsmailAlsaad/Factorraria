# System Patterns — Factorraria

## High-level architecture
Factorraria is organized as:
- `Common\` — shared frameworks (liquids, machines, networks, systems, UI, virtual items).
- `Content\` — actual game content (items, tiles/machines, liquids, projectiles, particles).

Content is mostly **data + thin subclasses** built on top of `Common\` base classes and registries.

## Machine framework
- `Common\Machines\BaseMachine.cs` — base for machines.
- `Common\Machines\BaseRecipeRegistery.cs` + `CustomRecipe.cs` — recipe definitions/registration; `Common\Systems\RecipeSystem.cs` drives them.
- `Common\Machines\ElectricMachine.cs` + `IElectric.cs` — electric machines (producers/consumers).
- `Common\Machines\MachineBiome.cs`, `MachineVisualOverride.cs`, `TileEntityHelper.cs` — helpers.
- `Common\Systems\MachineRegisterationSystem.cs` — autoload/registration of machines.
- Each machine folder contains: `<Name>TileEntity.cs`, `<Name>UIState.cs`, a recipe registry (where relevant),
  and sprites `_Off` / `_On` (plus `_On_FirstLiquid` / `_On_SecondLiquid` for fluid machines).

## UI system
- `Common\UI\MachineUIStateBase.cs` — base UI state per machine.
- `Common\UI\MachineUIRegistry.cs` + `Common\Systems\MachineUISystem.cs` — register & show machine UIs.
- `Common\UI\LiquidTankUIElement.cs` — tank rendering for fluid UIs.
- `Common\UI\UIItemSlotWrapper.cs`, `RecipeSelectHammerIcon.cs`, `MachineUIElementEntry.cs` — UI building blocks.
- Uses the **Luminance** library for UI/rendering.

## Liquids (custom, ModLiquidLib-based)
- `Common\Liquids\LiquidNetworkRegistries.cs`, `LiquidStack.cs`, `LiquidTextureCache.cs`.
- `Common\Liquids\PipeTileBase.cs`, `MotorTileBase.cs`/`MotorTileEntityBase.cs`, `PipeConnectionHelper.cs`.
- `Common\Systems\LiquidNetworkSystem.cs` + `Common\Networks\LiquidNetwork.cs` — network simulation.
- `LiquidNetworkDebugSystem.cs` — debug overlay.
- Content: `Content\Liquids\Oil\OilLiquid.cs` (+ Fall/Block/Slope sprites) — the current custom liquid.

## Power grid
- `Common\Networks\PowerNetwork.cs`, `Common\Systems\PowerGridSystem.cs`.
- `Common\Systems\PowerGridDebugSystem.cs` — debug overlay.
- Producers: `Content\Tiles\Machines\ElectricalProducers\GelBurner`.
- Consumers: `Content\Tiles\Machines\ElectricalConsumers\` (Autohammer, IceMachine, Solidifier).

## Logistics: conveyors & wiring
- `Common\Networks\PriorityConveyorNetwork.cs` + `Common\Systems\ConveyorPhysicsSystem.cs`.
- Conveyor content: `Content\Tiles\Conveyors\` (Clockwise / CounterClockwise priority conveyor tiles,
  shared `PriorityConveyorTileSpriteSheet.png`) and `Content\Items\Conveyors\` (+ `VanillaConveyorGlobalItem.cs`).
- Custom wiring: `Common\Systems\CustomWireSystem.cs`; wires in `Content\Items\Wires\` (CopperWire, TinWire,
  IronCutter, LeadCutter).

## Virtual items
- `Common\VirtualItems\VirtualItem.cs` + `VItemGlobalItem.cs`, driven by `Common\Systems\VirtualItemSystem.cs`.

## Other content
- Mounts: `Content\Items\Mounts\MechSuitItem.cs`, `Content\Projectiles\Mounts\MechSuitArmProjectile.cs`,
  `Common\Mounts\MechanicalArmIK.cs` (IK).
- Hooks/tools: `Content\Items\Hooks\MechanicalArmItem.cs` + `Content\Projectiles\Hooks\MechanicalArmProjectile.cs`.
- Weapons: `Content\Items\Weapons\IceBladeItem.cs` + `Content\Projectiles\Weapons\IceBladeSwingProjectile.cs`.
- Joke items: `Content\Items\JokeItems\` (C# logo, Terraria logo, tModLoader logo, Ismail's Cane).
- Particles: `Content\Particles\PuffExplosion.cs`; particle atlas in `Assets\Atlases\`.
- Config: `Content\Configs\FurnaceOffsetConfig.cs`.

## Key design decisions / conventions
- **One machine = one folder** with TileEntity + UIState (+ recipe registry) + state sprites. Keep this pattern.
- **Registries over hardcoding**: recipe registries, UI registry, liquid registries.
- **Systems live in `Common\Systems`** and are `ModSystem`s; content never contains grid logic.
- Localization via hjson: `Localization\en-US.hjson` (mod keys) + `en-US_Mods.Factorraria.hjson` (cross-mod keys).
- Use `..\ModAssemblies\` DLLs for ModLiquidLib/Luminance; their `.xml` files document APIs.