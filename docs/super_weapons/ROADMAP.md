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
- States: `Incomplete` -> `Assembled` -> `Igniting` -> `Launched`. A weapon is *ready* when `Assembled` and the tank holds a full 5000 Fuel (`IsReadyToFire`); there is no `Primed` state (S3.2 dropped).

**Trigger** (server only, a cheap check every `TriggerCheckTicks`):
- A Wall of Flesh with `ShieldUp` exists, a ready weapon (Assembled + full tank) exists whose facing points toward the wall (`sign(wall.x - weapon.x) == Facing`), and no other weapon is `Igniting`. Pick by D2.
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

- **Server (or single-player) owns everything:** shield state, assembly detection, the ready check, trigger, ignition, missile position, hit check, explosion, tile removal.
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

**S3.2 (dropped by Ismail on 2026-10-30, not implemented).** No tank status text and no `Primed` state. A weapon counts as ready to fire when it is `Assembled` and its tank is full (`MissileTankTileEntity.IsReadyToFire`, checked by the server at trigger time).

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

- V1 (S4.1) RESOLVED in S4.1 from the decompiled 1.3.5.3 NPC.SpawnWOF (the 1.4 / tModLoader version was NOT re-read, so treat it as very likely, not certain): it spawns only from a Guide Voodoo Doll burning in the Underworld; its x starts at the doll's x and is pushed toward the NEARER world edge (left if the doll is in the left half, right if in the right half) 16 px at a time until no player is within 1200 px of it, or it reaches 20 tiles from the edge; its y is the doll's y moved to open air and no higher than 180 tiles above the world's bottom. So the wall always starts at the world-edge side of the players and walks toward them, and any weapon between it and the world centre of the fight faces it. Chosen: the trigger rule needs no spawn logic, only sign(wall.x - weapon.x) == Facing (SuperWeaponTrigger). Original question: Where and at what height the Wall of Flesh spawns relative to the player (`Main.wofNPCIndex`, the spawn code in `NPC.SpawnWOF`) and how it moves; what "the correct side" means in practice.
- V2 (S1.1) RESOLVED in S1.1: ModifyIncomingHit runs only where a hit is calculated (items, projectiles, SimpleStrikeNPC). The server applies a client's hit packet through StrikeNPC(HitInfo, fromNet: true) without it, and damage over time goes through UpdateLifeRegen, not StrikeNPC. Whether HitModifiers can reduce damage below 1 was not verified, so zeroing there is best effort. Chosen: four layers in WallShieldGlobalNPC (ModifyIncomingHit zero, UpdateLifeRegen zero, CheckDead returns false, PostAI restores the mouth's life on every side). CanBeHitByItem / CanBeHitByProjectile are deliberately NOT used: returning false makes projectiles fly through the wall instead of stopping on it. The eyes forward damage to the mouth (realLife), so the mouth's life lock covers them. Original question: Which damage paths a GlobalNPC must cover: `dontTakeDamage` versus `ModifyIncomingHit`, damage over time through `lifeRegen` / `UpdateLifeRegen`, direct `StrikeNPC` calls, `realLife` forwarding from the eyes, and `dontTakeDamageFromHostiles`.
- V3 (S1.2) RESOLVED in S1.2 (from the tModLoader diff; vanilla Main.cs is not public): GlobalNPC.PreDraw / PostDraw wrap the whole vanilla draw of one NPC, but Main.DrawNPCs walks the NPC indices from 199 down to 0, so the eyes (higher index) are normally drawn before the mouth and a per-NPC overlay could not be ordered. Chosen: a detour on On_Main.DrawNPCs that draws after the vanilla pass (once per pass, for NPCs with behindTiles equal to that pass): body strip, mouth, eyes. Body span from Main.wofDrawAreaTop / Bottom (unit checked at runtime), body side opposite the mouth's direction (`/fshield flip` if wrong). NOT verified from source: the exact layout of the vanilla NPC_113 / NPC_114 sheets and that vanilla draws them from npc.frame at npc.Center; the overlay assumes so and the in-game test decides. Original question: How the wall, its eyes and its tiled body are drawn (the special draw in `Main.DrawNPCs`, `Main.wofDrawAreaTop` and `Bottom`), and which hook can add the overlay.
- V4 (S1.1) RESOLVED in S1.1: SendExtraAI / ReceiveExtraAI (bit writer / bit reader, must be overridden together) run with every SyncNPC (creation, join, netUpdate). A change is pushed with npc.netUpdate = true plus NetMessage.SendData(SyncNPC) on the server. ShieldUp is set in SetDefaults, not OnSpawn, because OnSpawn runs at the end of NPC.NewNPC, after the vanilla sync send. Original question: `GlobalNPC.SendExtraAI` / `ReceiveExtraAI` for the flag and forcing a net update when it changes.
- V5 (S2.1) RESOLVED in S2.1: ModTile.PostDraw only runs for tiles whose own cell is on screen and runs inside the tile pass (later tiles paint over it, a tip pops in at the screen edge). IMachineDynamicDraw walks TileEntity.ByPosition and the parts have no TileEntity. Chosen: the part's own draw is off (PreDraw false) and OverhangDrawSystem.PostDrawTiles scans the visible area plus ScanPaddingTiles (8) for registered part tiles and draws the art from the top-left cell. Original question: Drawing outside a ModTile footprint: dynamic draw versus `ModTile.PostDraw`; culling at the screen edge.
- V6 (S2.1) RESOLVED in S2.1 (pattern from ExampleMod's ExampleChair and the repo's Meteor Head Statue): StyleHorizontal, StyleMultiplier 1, newTile.Direction PlaceRight (style 0), an alternate with PlaceLeft added with addAlternate(1) (style 1); vanilla placement and TileObject.CanPlace pick the alternate from Player.direction. A placed tile's facing is TileFrameX / (footprint width * 18): 0 = right, 1 = left (OverhangDrawSystem.GetFacing). Original question: Left/right placement styles (`StyleHorizontal`, alternates) and how to read a placed tile's facing.
- V7 (S2.2) RESOLVED in S2.2: a BaseMachine with no overrides but `AcceptedLiquidFuels` (a LiquidFuelTable that only knows Fuel) has no slots, no recipes (ProcessRecipes returns at once when Recipes is null) and exactly one input tank, InputLiquids[0], whose filter is the table; the pipe sink ports and BucketPour.TryFindMachineTank both work on InputLiquids. The tank size was fixed at 1000, so BaseMachine got `InputTankCapacity(int tankIndex)` applied in CreateInputLiquids. The placement and kill paths only run for tile types in MachineVisualRegistry.Definitions, so the tank registers a visual (one sheet in both the On and Off slot) and a UI in MachineRegisterationSystem. Original question: How a ModTile machine without slots or power declares a liquid input tank, and the sink port wiring (`MechanicalAssemblerTileEntity`, `BaseMachine`).
- V8 (S4.2). Projectile draw override for a large composite sprite, culling, and sync of velocity.
- V9 (S1.1). What other mods' hooks might touch the Wall of Flesh (informational only).

## 10. Risks

- The wall has special drawing, so the shield overlay is the least certain visual step (V3).
- A missile that flies until the wall is gone means a stuck wall could keep a missile alive; the range cap and the fizzle rule cover it.
- A multi-part launch that removes tiles must be atomic on the server, or a client could see half a weapon.
- If damage can reach the wall through a path the GlobalNPC misses, the shield leaks; S1.1 tests every source.

## 11. Temporary test code to remove in S6
- `/fshield` command (kept only while `EnableDebugs` is on, as with `/fcosmic`).
- `/fweapon` command and the `[Missile]` chat lines of `SuperWeaponSystem.Say` (S4.1).

## 12. Progress

One line per patch: `[ ]` not started, `[~]` applied untested, `[x]` tested done. Each patch edits its own line and adds the next-chat prompt.

- [x] S1.1 Shield state, damage block, break, sync (patch_sw_s1_1_shield_state.ps1, applied, tested OK by Ismail 2026-10-29; Common/SuperWeapons/WallShieldGlobalNPC.cs + WallShieldCommand.cs. AUTHORITY: server / single-player owns ShieldUp, only BreakShield / RaiseShield write it. SYNC: the flag rides the NPC sync (SendExtraAI / ReceiveExtraAI) plus an immediate SyncNPC send, no packet. BLOCK: four layers, see the V2 line in section 9. Tests: every damage source does nothing to the mouth and the eyes, the Hungry and leeches still take damage, `/fshield break` makes the wall killable, a second client shows the same `/fshield status`, a new wall after death is shielded again)
- [x] S1.2 Shield visual and break effect (patch_sw_s1_2_shield_visual.ps1, applied, tested OK by Ismail 2026-10-29; Common/SuperWeapons/WallShieldDrawSystem.cs, Common/Net/SuperWeaponNet.cs, packet SuperWeaponEffect = 57. CLIENT ONLY drawing: a detour on On_Main.DrawNPCs draws the stone body strip, the mouth and the eyes after the vanilla pass while ShieldUp; a missing PNG draws a grey block so alignment can be tested before the art exists. BREAK EFFECT: BreakShield calls SuperWeaponNet.SendEffect, the server broadcasts one packet to every client, single-player plays it directly: stone dust along the wall, boom + crack (placeholder sounds), camera punch. Art by Ismail: Content/Tiles/SuperWeapons/WallShield_Body.png (190x420, replaces the vanilla body texture), WallShield_Mouth.png, WallShield_Eye.png; /fshield status prints the sheet sizes. Tests: the grey blocks cover the mouth, both eyes and the body strip out to the screen edge; /fshield flip and /fshield offset fix the body side if it is wrong; /fshield break plays the effect on every client; /fshield raise brings the overlay back)
- [x] S2.1 Overhang draw helper plus the drill (patch_sw_s2_1_drill.ps1, applied, tested OK by Ismail 2026-10-29; Common/SuperWeapons/OverhangDrawSystem.cs, Content/Tiles/SuperWeapons/MissileDrill/MissileDrillTile.cs + MissileDrillItem.cs. NO NETCODE: the drill is a plain tile with no TileEntity, placed, mined and synced by vanilla tile code; its facing is read from the tile frame on every client, the drawing is client only. Item: Missile Drill, placeholder recipe 12 iron bars at an Anvil. Art by Ismail: Content/Tiles/SuperWeapons/MissileDrill/MissileDrill.png (48x48, facing right, tip on the right, the left 32 px are the footprint) and MissileDrillItem.png; until they exist the part draws as a grey footprint block with a lighter tip block and the item borrows the Cobalt Drill icon. Tests: the tip draws over empty space and over other tiles; placing while facing left and while facing right gives the mirrored part and the hologram matches; mining returns the item; walking the part to the very edge of the screen, and zooming out, does not pop the tip; saving and reloading keeps the facing; a second client sees the same facing)
- [x] S2.2 Tank (Fuel tank, pipes, bucket pour) (patch_sw_s2_2_tank.ps1, applied, tested OK by Ismail 2026-10-29; Content/Tiles/SuperWeapons/MissileTank/ (MissileTankTile, MissileTankTileEntity, MissileTankRegistry, MissileTankUIState, MissileTankItem), BaseMachine gets the virtual `InputTankCapacity(int)` (default 1000). The tank is a slotless, powerless BaseMachine with ONE input tank (InputLiquids[0], Fuel only, capacity 5000 = `FuelCapacity` on the entity); the existing machine placement, kill path, snapshot, light updates, pipe sink and bucket pour all apply, no new netcode. Item: Missile Fuel Tank, placeholder recipe 20 iron bars at an Anvil; right click opens a UI with just the Fuel gauge. Art by Ismail: Content/Tiles/SuperWeapons/MissileTank/MissileTank.png (72x54: 4x3 cells of 16 px with 2 px gaps) and MissileTankItem.png; until they exist the tile borrows the vanilla statue sheet and the item the Barrel icon. Tests: pipes fill it and only with Fuel; a Fuel bucket pours in and any other bucket is refused; the gauge hover shows the amount out of 5000; save and reload keeps the amount; mining returns the item and removes the entity; a second client sees the amount (open the UI, and the gauge follows)
- [x] S2.3 Thruster (patch_sw_s2_3_thruster.ps1, applied, tested OK by Ismail 2026-10-29; Content/Tiles/SuperWeapons/MissileThruster/MissileThrusterTile.cs + MissileThrusterItem.cs, registered with the S2.1 OverhangDrawSystem: footprint 1x1, art 32x16 facing right, rear plate 16 px behind the footprint (ArtOffsetX -16). NO NETCODE, no entity, same facing rule as the drill (style 0 right, style 1 left, read from TileFrameX / 18). Item: Missile Thruster, placeholder recipe 6 iron bars at an Anvil. Art by Ismail: Content/Tiles/SuperWeapons/MissileThruster/MissileThruster.png (32x16, facing right, the RIGHT 16 px are the footprint and the LEFT 16 px stick out behind it) and MissileThrusterItem.png; until they exist the part draws as a grey footprint block with a lighter rear block and the item borrows the Cog icon. Tests: three in a column; placing facing left and facing right; the rear plate draws behind the nozzle side over empty space and over other tiles, including over a tank cell; mining returns the item; save and reload keeps the facing; a second client sees the same facing)
- [~] S3.1 Assembly detection (patch_sw_s3_1_assembly.ps1, applied, untested; Common/SuperWeapons/SuperWeaponAssembly.cs (pure geometry, no Terraria types; 52 checks in tools/sw_tests), Common/SuperWeapons/SuperWeaponDebugSystem.cs, MissileTankTileEntity gets WeaponState / WeaponFacing / WeaponIssue* and an Update override. AUTHORITY: the server / single-player re-reads the tiles around the tank every AssemblyCheckTicks (10) and owns the result; a client never computes it. Nothing is saved: it is recomputed from the tiles, so a reload always agrees with the world. SYNC: six small bytes in the snapshot extra (join) and the light extra (changes, only re-sent when the bytes change), no new packet. RULES: the drill's top-left tile sits at tank x + 4 (weapon faces right) or tank x - 2 (faces left), in the tank's top row; its facing must match that side; the three thrusters stand in the column touching the opposite end, one per tank row, same facing; parts elsewhere are ignored; the first problem is reported (drill, drill facing, then thrusters top to bottom). Overlay (needs EnableDebugs): white box on the tank, green box around the 7x3 weapon when assembled, else a red box where the first missing or wrong piece belongs plus one text line. NOTE FOR S3.2: BaseMachine.ApplyLightState returns early while the detail channel is fresh (the tank UI is open), so the light extra is NOT applied then, and BuildDetail carries no extras; S3.2 must deliver the assembly bytes another way before the UI text can follow the world while it is open. Tests: valid weapon facing right and facing left; remove each thruster in turn; thrusters one row up or down; drill one and two rows off; drill or one thruster facing the wrong way; mine a part and place it back; stray parts around the weapon do not matter; save and reload keeps it valid; a second client sees the same overlay)
- [~] S4.1 Trigger and ignition (patch_sw_s4_1_trigger.ps1, applied, untested; Common/SuperWeapons/SuperWeaponTrigger.cs (pure, tests in tools/sw_tests), SuperWeaponSystem.cs, SuperWeaponIgnitionFxSystem.cs, SuperWeaponCommand.cs, SuperWeaponEffectKind.Ignition = 1 in SuperWeaponNet.cs, MissileTankTileEntity gets IsFull / IsReadyToFire / BeginIgnition / CompleteIgnition / ClearLaunch / IgnitionTicksLeft and RefreshAssembly leaves Igniting and Launched alone while the geometry stays valid. S3.2 was DROPPED by Ismail on 2026-10-30: there is no Primed state and no tank status text; ready = Assembled + full tank, checked by the server at trigger time. AUTHORITY: the server / single-player runs the trigger and the countdown (SuperWeaponSystem.PostUpdateEverything); a client does nothing there. TRIGGER: every TriggerCheckTicks (30), if a Wall of Flesh with ShieldUp exists, the ready tanks (Assembled + full tank, MissileTankTileEntity.IsReadyToFire) whose facing points at it (sign(wall.x - tank centre x) == WeaponFacing) are candidates and the nearest in x starts (D2, D3); nothing starts while a tank is Igniting or Launched (one weapon per wall). IGNITION: IgniteTicks (120) of Igniting, then Launched, which in S4.1 only logs and says "LAUNCH (S4.1 stub)". Igniting / Launched are cleared when the shielded wall is gone, a part is removed, the tank is mined, or /fweapon reset. SYNC: no request packet; WeaponState (Igniting = 3, Launched = 4) already travels in the snapshot, light and detail bytes; one cosmetic packet SuperWeaponEffect kind Ignition (sound + camera shake at the tank, sent once at the start; single-player plays it directly); each client draws fire and smoke dust at the three nozzles every tick while its replica says Igniting (SuperWeaponIgnitionFxSystem, placeholder dust). DEBUG: /fweapon status (everywhere), /fweapon fire (starts the nearest ready tank without a wall, needs EnableDebugs), /fweapon reset; single-player also prints the ignition lines in chat. Tunables: SuperWeaponSystem.TriggerCheckTicks and IgniteTicks (instance fields). Tests: a ready weapon facing the wall ignites and the flames come out of the thruster side; the same weapon facing away does not; a weapon that became ready after the wall spawned still fires; two ready weapons: only the nearest one ignites; removing a part during the countdown aborts it; with the wall dead or the shield broken the tank goes back to Assembled; /fweapon fire works without a wall; a second client sees the flames and hears the start)
- [ ] S4.2 Missile flight
- [ ] S4.3 Impact and edge cases
- [ ] S5 Automation gating (design chat, then recipes)
- [ ] S6 Hardening and balance

Next chat prompt after S4.1: "S4.1 is applied and tested; results: <paste>. Implement super weapon patch S4.2." Read the S4.1 Progress line first (S3.2 was dropped; there is no Primed state, a weapon is ready when Assembled and its tank is full): SuperWeaponSystem owns the trigger and the countdown on the server, CompleteIgnition() is where S4.2 replaces the stub (spawn the missile projectile at the weapon's centre row moving along WeaponFacing, remove the part tiles and empty the tank in one server step, then clear the launch state), TankCenter(tank) gives the weapon's centre. Resolve V8 (composite projectile draw and sync) before building; the missile needs its own sprite (7x3 tiles, composite or one texture, art by Ismail). S1.1 to S2.3 are tested; S3.1 was applied untested.

Starting a patch: "Implement super weapon patch S<n>." The patch reads this file from the fresh clone (sections 1, 2, 4, the patch, 7, 9 and Progress) and `docs/multiplayer/ROADMAP.md` section 3, checks that its prerequisites are marked done, states the authority and sync design first, and ends with a next-chat prompt.

First prompt: "Implement super weapon patch S1.1 for Factorraria (Wall of Flesh stone shield: state, damage block, break, sync, /fshield debug command). Clone the repo, read docs/super_weapons/ROADMAP.md (sections 1, 2, 4, 6 S1.1, 7, 9 and Progress) and docs/multiplayer/ROADMAP.md section 3. Resolve V2 and V4 in the tModLoader source first, state the authority and sync design, then deliver the patch script. Open items in section 8 use their defaults."