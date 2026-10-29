# Super Weapons: Development Roadmap

Factorraria super weapons, the main motivator for automation. The first one breaks the Wall of Flesh's stone shield: a horizontal multitile missile (drill head, fuel tank, three thrusters) that the player assembles from factory-made parts, fuels, and that flies at the wall when it spawns. Written against repo commit `b009a12` (2026-10-29). Where this document and the code disagree, the code wins.

## 1. Decisions

### Fixed by Ismail
- **Shield.** The Wall of Flesh takes no damage from weapons or anything else while its stone shield is up. The shield needs its own texture drawn on the wall.
- **Shield covers** the Wall of Flesh itself: its mouth, its eyes and its other textures. It does NOT cover the Hungry or the leeches.
- **Breaking it.** Only the super weapon breaks the shield. After that the wall is killed with normal weapons.
- **Retries.** If the wall kills the player or despawns, the next wall spawns with the shield again. The shield belongs to one wall instance.
- **Parts.** The player must automate the production of the parts. (Their recipes are a later design pass, patch S5.)
- **Structure, horizontal.** Drill head: art 3x3, tile footprint 2 wide x 3 tall. Tank: 4 wide x 3 tall. Thrusters: three separate 1x1 tiles stacked on the tank's face opposite the drill. Total footprint 7 wide x 3 tall.
- **Overhang.** Every part has an overhang texture: its tile footprint and hitbox are smaller, extra pixels are drawn outside the footprint.
- **Fuel.** The Fuel liquid (distilled from oil), tank capacity 5000.
- **Priming.** Assemble correctly and fill the tank = primed.
- **Launch.** When the Wall of Flesh is spawned and a primed weapon points in the right direction, its boosters start, it flies at the wall and explodes, breaking the shield.
- **Aiming.** Only left or right. The weapon faces the side where the wall is.
- **Flight.** The missile flies over tiles and never affects them.
- **Hit.** No real hitbox: a simple line check, the missile hits when its x is close to the wall's x.
- **Only one** primed weapon fires per wall.

### Defaults chosen by Claude (change any of them; they are listed again in section 8)
- D1. The hit check ignores y. The missile flies horizontally along its own row and hits when `abs(missile.x - wall.x) < HitDistancePx`. A weapon built far above or below the wall still hits.
- D2. Which weapon fires: the primed, correctly facing weapon whose x is nearest the wall's x.
- D3. The check runs continuously while a shielded wall exists, not only at the spawn tick, so a weapon primed after the wall spawned also fires.
- D4. Ignition takes 120 ticks (flames, shake, sound) between "trigger" and "launch".
- D5. At launch the part tiles are removed and the tank is emptied. The weapon is consumed. If the missile flies out of range or the wall is gone, it fizzles and nothing is refunded.
- D6. Parts can be placed and assembled anywhere in the world, not only in the Underworld.
- D7. Only the missile (and the debug command) breaks the shield. No other event does.
- D8. A broken shield stays broken for that wall instance only. Shield state is not saved (NPCs are not saved).
- D9. Art is drawn facing right only; a left-facing weapon is the same art flipped with `SpriteEffects`.

## 2. The model (exact rules the code must follow)

**Shield.**
- A `GlobalNPC` gives each Wall of Flesh and Wall of Flesh eye `ShieldUp = true` when it spawns. Hungry, Hungry II and leeches never get it.
- While `ShieldUp`, no damage reaches the wall from any source. Which hooks cover everything (melee, projectiles, DoT and debuffs, explosions, traps, direct `StrikeNPC` calls, `realLife` forwarding from the eyes) is verification item V2.
- `BreakShield()` is server/single-player only, sets `ShieldUp = false` on the mouth and every eye, plays the break effect through the broadcast, and cannot be undone for that wall.

**Assembly** (anchored on the tank, recomputed when part tiles change):
- Valid when: the tank exists; a drill exists whose 2x3 footprint touches one end of the tank's 4x3 footprint, rows aligned; exactly three thruster tiles stand on the opposite end, one per tank row; and all three parts have the same facing.
- `Facing` is +1 (drill on the right) or -1 (drill on the left).
- States: `Incomplete` -> `Assembled` -> `Primed` (assembled and the tank holds 5000 Fuel) -> `Igniting` -> `Launched`.

**Trigger** (server only, a cheap check every `TriggerCheckTicks`):
- A Wall of Flesh with `ShieldUp` exists, a `Primed` weapon exists whose facing points toward the wall (`sign(wall.x - weapon.x) == Facing`), and no other weapon is `Igniting`. Pick by D2.
- Ignition runs `IgniteTicks`, then Launch.

**Flight and impact:**
- Launch removes the part tiles, empties the tank, and spawns a missile projectile at the weapon's center row moving along `Facing`, accelerating up to `MissileMaxSpeedPx`.
- It ignores tile collision and damages nothing on the way.
- Each tick the server checks the wall: when `abs(missile.x - wall.x) < HitDistancePx`, the missile explodes and calls `BreakShield()`. If the wall is gone (despawned, killed, shield already down) or the missile has flown `MissileMaxRangeTiles`, it fizzles with a smaller effect.

## 3. Codebase facts this plan relies on

Verified in the repo clone at `b009a12`:
- The liquid already exists: `LiquidTypeRegistry.Fuel` (registered in `LiquidTypeRegistrationSystem.PostSetupContent`, `Common/Liquids/LiquidNetworkRegistries.cs`). Liquids are append-only.
- One bucket is 255 units (`BucketPour.BucketUnits`), the default `LiquidStack.Capacity` is 1000. So 5000 units is about 19.6 buckets; the tank needs its own capacity of 5000.
- Left-click pouring a bucket into any machine with input tanks exists (`Common/Net/BucketPour.cs`). The super-weapon tank should work with it for free.
- Machine tiles are either vanilla tiles with a custom TileEntity or real ModTiles (`MechanicalAssemblerTile` uses `TileObjectData.Style3x3` plus `HookPostPlaceMyPlayer`). The three parts are real ModTiles with their own `TileObjectData`.
- `IMachineDynamicDraw` and `MachineDynamicDrawSystem` already draw things outside the tile grid (before tiles, after tiles). The overhang draw should reuse them.
- Liquid networks live on the server; a machine's input tanks become sink ports. The tank must expose its Fuel tank the same way.
- `Common/Globals` holds the existing GlobalNPC-style classes (`BossLootGlobal`). No GlobalNPC for Wall of Flesh exists yet.
- No Wall of Flesh code exists in the repo. Packet code goes in `Common/Net/` (multiplayer working rule 10).

## 4. Multiplayer design (rule 9)

- **Server (or single-player) owns everything:** shield state, assembly detection, primed state, trigger, ignition, missile position, hit check, explosion, tile removal.
- **Synced to clients:**
  - Shield state of each wall NPC: a flag sent with the NPC (`SendExtraAI` / `ReceiveExtraAI`, with a forced NPC net update on change; verify V4).
  - Tank fuel and assembly state: on the tank's TileEntity through the existing machine snapshot.
  - The missile: a real projectile, synced by vanilla.
  - One small packet from the server for the break and ignition effects (position, kind) so clients can draw them; put it in `Common/Net/SuperWeaponNet.cs`.
- **Client only:** the stone overlay draw, overhang draws, the composite missile sprite, flames, shake and sounds.
- **Client requests:** none needed. The player's only actions are placing tiles and pouring fuel, which already go through the existing paths.
- **Single-player:** the same server code runs directly.
- **Join:** a client joining mid-fight gets the wall NPC with its flag (NPC sync); an ongoing missile is a projectile and syncs normally.

## 5. Data model and files

New folder `Common/SuperWeapons/`:
- `WallShieldGlobalNPC.cs` - `ShieldUp`, damage block, `BreakShield`, extra AI sync, the overlay draw hook.
- `SuperWeaponAssembly.cs` - pure geometry check (no Terraria types if possible, tested in `tools/`), `Facing`, state enum.
- `SuperWeaponSystem.cs` - ModSystem: the registry of tanks, the trigger loop, ignition, missile spawning, tunables (`[SuperWeaponParam]`-style fields like the cosmic ones).
- `SuperWeaponMissile.cs` - the ModProjectile and its composite draw.
- `Common/Net/SuperWeaponNet.cs` - effect packets.

New folder `Content/Tiles/SuperWeapons/` (one subfolder per part):
- `MissileDrill/` (`MissileDrillTile`, `MissileDrillItem`), `MissileTank/` (`MissileTankTile`, `MissileTankTileEntity`, `MissileTankUIState`, `MissileTankItem`), `MissileThruster/` (`MissileThrusterTile`, `MissileThrusterItem`). Names are proposals.
- Overhang helper in `Common/SuperWeapons/OverhangDraw.cs`, built on the dynamic draw system.

Art Ismail makes (proposed sizes, final paths are given before each patch):
- Shield overlay for the wall body, eyes and mouth (S1.2).
- Drill 48x48 (footprint 32x48, the extra 16 px is the tip), tank 64x48, thruster footprint 16x16 plus a rear overhang (for example 32x16), all drawn facing right. A flame strip for the thrusters later.

## 6. Patch plan

Each patch is one chat and one patch script (`patch_sw_<id>_<name>.ps1`). Every patch follows the project working rules: clone, verify every Find, state the authority and sync design first, mark Progress "applied, untested", give the changed list, the things-to-test list, and the next-chat prompt. Patches that add packets also add a line to `docs/multiplayer/ROADMAP.md` Progress.

### Phase S1 - The shield (no weapon needed, testable alone)

**S1.1 Shield state, damage block, break, sync**
- Resolve V1 to V4 first.
- `WallShieldGlobalNPC` with `ShieldUp`, damage block, `BreakShield`, sync.
- `/fshield` debug command: `status`, `break`, `raise` (raise puts the shield back on the live wall).
- Tests: every damage type does nothing (sword, arrow, explosive, lava and fire debuffs, minion, trap); the Hungry and leeches still take damage; `/fshield break` makes the wall killable; a second client sees the same state; a new wall after death has the shield again.

**S1.2 Shield visual and break effect**
- Resolve V3 (how the wall and its eyes are drawn) first.
- Stone overlay on the body, eyes and mouth while `ShieldUp`; break effect (debris dust, sound, screen shake) sent to clients.
- Tests: overlay lines up on the mouth, the eyes and the body strip at the edges of the screen; the break effect plays for every client.

### Phase S2 - Part tiles

**S2.1 Overhang draw helper plus the drill**
- Resolve V5 (drawing outside the tile footprint) and V6 (facing styles).
- Drill tile with a 2x3 footprint, 3x3 art, facing left/right placement, item, placeholder recipe, overhang drawn through the dynamic draw system with an off-screen padding check.
- Tests: tip draws over empty space and over other tiles; left and right placement; mining returns the item; edge-of-screen culling does not pop.

**S2.2 Tank**
- Resolve V7 (a tank-only machine and the input tank API).
- 4x3 ModTile with a TileEntity: one Fuel-only tank of 5000, no slots, connects to pipes as a sink, bucket pour works, saved and synced like other machines.
- Tests: pipes fill it; bucket pour fills it and refuses other liquids; save and reload keeps the amount; second client sees the amount.

**S2.3 Thruster**
- 1x1 tile with a rear overhang, facing placement, item.
- Tests: three in a column, placement left and right, overhang draws.

### Phase S3 - Assembly

**S3.1 Assembly detection**
- Pure geometry check with tests in `tools/`, wired into the tank entity; state and `Facing`; recomputed on part tile changes; debug overlay (`EnableDebugs`) showing the result and the first missing piece.
- Tests: the valid weapon in both directions; a missing thruster; a thruster one row off; drill on the wrong end; mixed facings; breaking a part invalidates; reload keeps it valid.

**S3.2 Tank UI and primed state**
- Fuel gauge and status text in the tank UI (assembled, facing, missing part), `Primed` when assembled and full.
- Tests: the UI matches the world state; priming flips at exactly 5000.

### Phase S4 - Launch

**S4.1 Trigger and ignition**
- Resolve V1 (where the wall spawns) first.
- `SuperWeaponSystem` trigger loop, nearest-weapon pick (D2), facing check, ignition countdown with flame and shake effects. It ends with a log line for now, no missile yet.
- Tests: the wrong facing does not fire; a weapon primed after the wall spawns fires; two weapons: only the nearest ignites.

**S4.2 Missile flight**
- Resolve V8 (composite projectile draw and sync).
- Launch removes the tiles and empties the tank; missile projectile with acceleration, composite 7x3 sprite, thruster flames.
- Tests: it flies over terrain without touching it; second client sees it; it flies the same left or right.

**S4.3 Impact and edge cases**
- Hit check on x, explosion, `BreakShield`, cleanup, fizzle cases.
- Tests: hit breaks the shield and the wall is killable; wall killed or despawned mid-flight -> fizzle; leaving the world mid-flight loses the weapon (accepted); a new wall has the shield again.

### Phase S5 - Automation gating (design chat first)
- Decide the part recipes and the intermediate products that force the player to build a factory (which machines, what quantities, any new materials). Then add the recipes, the machine recipe registry entries and the unlock flow. Code is small; the design pass is the real work.

### Phase S6 - Hardening and balance
- Dedicated server plus two clients run through the full flow; tunables config (`HitDistancePx`, `IgniteTicks`, speeds, range); tooltips and item descriptions; remove the debug commands that are not needed.
- Later: generalize into a reusable framework (boss shield registry, assembly definitions) when super weapon number two is designed.

## 7. Tunables (first pass; exact names are set in the patches)

| Name | Default | What it does |
|---|---|---|
| `FuelCapacity` | 5000 | Tank size, in liquid units (a bucket is 255) |
| `HitDistancePx` | 32 | Missile x within this of the wall x = hit |
| `IgniteTicks` | 120 | Time between trigger and launch |
| `MissileAccel` | to tune | Px per tick squared |
| `MissileMaxSpeedPx` | to tune | Must exceed the wall's speed with margin |
| `MissileMaxRangeTiles` | to tune | Fizzle distance |
| `TriggerCheckTicks` | 30 | How often the server looks for a launch |

## 8. Open items (every default below is used unless Ismail says otherwise)

- O1 (D1). Hit ignores y. Alternative: also require the wall's vertical span to cover the missile's row.
- O2 (D2). Nearest-by-x weapon fires. Alternative: first primed, or the one nearest the player.
- O3 (D3). Continuous check, so late priming still fires.
- O4 (D5). Weapon consumed at launch, nothing refunded on a fizzle.
- O5 (D6). Parts can be built anywhere, not only in the Underworld.
- O6. Tank capacity is 5000 units (about 19.6 buckets). Say if you meant 5000 as a different unit.
- O7. Whether the Hungry should also be shielded is decided: no.

## 9. Verification list (checked in the tModLoader or vanilla source before the patch that needs it)

- V1 (S4.1). Where and at what height the Wall of Flesh spawns relative to the player (`Main.wofNPCIndex`, the spawn code in `NPC.SpawnWOF`) and how it moves; what "the correct side" means in practice.
- V2 (S1.1). Which damage paths a GlobalNPC must cover: `dontTakeDamage` versus `ModifyIncomingHit`, damage over time through `lifeRegen` / `UpdateLifeRegen`, direct `StrikeNPC` calls, `realLife` forwarding from the eyes, and `dontTakeDamageFromHostiles`.
- V3 (S1.2). How the wall, its eyes and its tiled body are drawn (the special draw in `Main.DrawNPCs`, `Main.wofDrawAreaTop` and `Bottom`), and which hook can add the overlay.
- V4 (S1.1). `GlobalNPC.SendExtraAI` / `ReceiveExtraAI` for the flag and forcing a net update when it changes.
- V5 (S2.1). Drawing outside a ModTile footprint: dynamic draw versus `ModTile.PostDraw`; culling at the screen edge.
- V6 (S2.1). Left/right placement styles (`StyleHorizontal`, alternates) and how to read a placed tile's facing.
- V7 (S2.2). How a ModTile machine without slots or power declares a liquid input tank, and the sink port wiring (`MechanicalAssemblerTileEntity`, `BaseMachine`).
- V8 (S4.2). Projectile draw override for a large composite sprite, culling, and sync of velocity.
- V9 (S1.1). What other mods' hooks might touch the Wall of Flesh (informational only).

## 10. Risks

- The wall has special drawing, so the shield overlay is the least certain visual step (V3).
- A missile that flies until the wall is gone means a stuck wall could keep a missile alive; the range cap and the fizzle rule cover it.
- A multi-part launch that removes tiles must be atomic on the server, or a client could see half a weapon.
- If damage can reach the wall through a path the GlobalNPC misses, the shield leaks; S1.1 tests every source.

## 11. Temporary test code to remove in S6
- `/fshield` command (kept only while `EnableDebugs` is on, as with `/fcosmic`).

## 12. Progress

One line per patch: `[ ]` not started, `[~]` applied untested, `[x]` tested done. Each patch edits its own line and adds the next-chat prompt.

- [ ] S1.1 Shield state, damage block, break, sync
- [ ] S1.2 Shield visual and break effect
- [ ] S2.1 Overhang draw helper plus the drill
- [ ] S2.2 Tank (Fuel tank, pipes, bucket pour)
- [ ] S2.3 Thruster
- [ ] S3.1 Assembly detection
- [ ] S3.2 Tank UI and primed state
- [ ] S4.1 Trigger and ignition
- [ ] S4.2 Missile flight
- [ ] S4.3 Impact and edge cases
- [ ] S5 Automation gating (design chat, then recipes)
- [ ] S6 Hardening and balance

Starting a patch: "Implement super weapon patch S<n>." The patch reads this file from the fresh clone (sections 1, 2, 4, the patch, 7, 9 and Progress) and `docs/multiplayer/ROADMAP.md` section 3, checks that its prerequisites are marked done, states the authority and sync design first, and ends with a next-chat prompt.

First prompt: "Implement super weapon patch S1.1 for Factorraria (Wall of Flesh stone shield: state, damage block, break, sync, /fshield debug command). Clone the repo, read docs/super_weapons/ROADMAP.md (sections 1, 2, 4, 6 S1.1, 7, 9 and Progress) and docs/multiplayer/ROADMAP.md section 3. Resolve V2 and V4 in the tModLoader source first, state the authority and sync design, then deliver the patch script. Open items in section 8 use their defaults."