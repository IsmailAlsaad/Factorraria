# Project Brief — Factorraria

## What it is
**Factorraria** is a Terraria mod for tModLoader that adds **industrial automation** gameplay
(Factorio-inspired): machines, power, fluids, and item transport that the player builds and wires together.

## Core goals
- Build a cohesive automation loop: harvest/generate resources -> process in machines -> transport via
  conveyors/pipes -> consume power/liquids.
- Provide a modular **machine framework** so new machines are cheap to add (base class + tile entity + UI + recipe registry).
- Ship a **custom liquid system** (currently Oil) integrated with pipes, tanks, and fluid motors.
- Ship a **power grid** with producers (e.g. Gel Burner) and consumers (e.g. Autohammer, Ice Machine, Solidifier).
- Add **logistics**: conveyors with priority routing, custom wiring, and a Mechanical Arm item.

## Scope (current, v0.1)
- In scope: machines (Furnace, Motors, Pipes, Autohammer, IceMachine, Solidifier, GelBurner), Oil liquid,
  conveyors (clockwise / counter-clockwise priority), wires (Copper/Tin + cutters), Mounts (Mech Suit),
  Weapons (Ice Blade), Automation hook (Mechanical Arm).
- Out of scope (for now): full tech tree, boss content, biomes (there is a `MachineBiome` helper).

## Identity
- Mod name (display): Factorraria
- Author: Ismail
- Version: 0.1
- Root namespace: `Factorraria`
- Entry point: `Factorraria.cs` -> `public class Factorraria : Mod`

## Success criteria
- Machines can be placed, UI-configured, and correctly consume/emit power and liquids.
- Liquids flow through networks with correct per-tile rendering (liquid + tiles + slopes).
- A player can build a working production chain end-to-end in-game.