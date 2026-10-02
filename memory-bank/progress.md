# Progress — Factorraria

## Status
Version 0.1, in active development. Broad feature set is already implemented in source; not yet verified by a
build in this session.

## What exists (works in source — unverified by build here)
- **Machines:** Furnace (GeneralMachines), Motors, Pipes; Autohammer, IceMachine, Solidifier (consumers);
  GelBurner (producer). Each with TileEntity + UIState (+ recipe registry where relevant) and state sprites.
- **Machine framework:** BaseMachine, recipe system (CustomRecipe + registries), ElectricMachine/IElectric,
  machine UI system (MachineUIRegistry, MachineUIStateBase), machine registration system.
- **Power grid:** PowerNetwork + PowerGridSystem (+ debug system).
- **Custom liquid system:** Oil liquid; liquid network (LiquidNetwork + LiquidNetworkSystem), pipes, motors,
  tanks/UI, LiquidStack, texture cache.
- **Logistics:** priority conveyors (CW/CCW) with ConveyorPhysicsSystem; custom wire system; Copper/Tin wires + cutters.
- **Virtual items:** VirtualItem + VItemGlobalItem + VirtualItemSystem.
- **Gear:** Mech Suit mount (+ arm projectile, MechanicalArmIK); Mechanical Arm hook; Ice Blade weapon.
- **Misc:** joke items, PuffExplosion particle + atlas, FurnaceOffsetConfig.

## What's left / to verify
- Build the mod successfully (via tModLoader/VS) and confirm no compile/runtime errors.
- Write `description.txt` (still default placeholder).
- Confirm progression/tuning and multiplayer sync behavior (unconfirmed).
- Any not-yet-created content (tech tree, additional liquids, etc.) — TBD with Ismail.

## Known issues

## Decision log
- 2026: Adopted Cline Rules + Memory Bank to persist project context across chats.
- Confirmed tModLoader API source path for lookups (see techContext.md).

## How to resume
1. Read all `memory-bank\` files.
2. Read `.clinerules\project.md`.
3. Ask Ismail what to work on next, or pick a "What's left" item.