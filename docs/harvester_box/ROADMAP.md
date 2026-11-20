# Harvester Box roadmap and context handoff

Home: `docs/harvester_box/ROADMAP.md`. Written 2026-11-17 by the Phase 0 patch. The code wins wherever this file disagrees with it.

## 0. How to use this file in a new chat

1. Clone the repo, read this file, then read only what the requested phase names (use `CODEBASE_INDEX.md` to find files). Do not load whole files you do not need.
2. Check the Progress section (section 6). Do not start a phase whose predecessor is not `[x]`, or at least `[~]` with Ismail's test report pasted in.
3. Follow `PROJECT_CONTEXT.md` (patch scripts, no `const`/`static` unless forced, reuse helpers, multiplayer first, Ismail makes all art and you tell him exact paths and sizes).
4. One chat = one phase. Every patch edits the Progress section (`[~]` applied untested) and ends with the next-chat prompt (section 8) updated.
5. Multiplayer code goes in `Common/Net/`. Every phase that adds netcode also edits `docs/multiplayer/ROADMAP.md` and `SURVEY_APPENDIX.md` (rule 10).

## 1. What it is (Ismail's spec, final unless he changes it)

- New powered machine "Harvester Box": an upgrade to the planter boxes, one version per planter box type. No machine recipes (the item itself still needs a crafting recipe).
- 1x1, mimics planter box placement: merges horizontally, not vertically, with boxes of the same type.
- Harvests grown herbs on top of it automatically, and acorns that grow into trees.
- The tile sprite is a static sheet. A gear is drawn on top: one per box, adjacent boxes' gears mesh and rotate in opposite directions, direction from the x position (even/odd).
- When the plant above has matured, that box's gear becomes another sprite (Ismail draws it) and a staff of regrowth sprite (Ismail provides) grows from the gear's center and rotates with the gear. Grass particles come off it while moving.
- When the staff passes over the plant it is harvested and replanted. An acorn becomes the tree that matches the box (Shiverthorn box has snow in it, so its acorn becomes a boreal sapling, and so on).
- Harvested plants drop as regular items if a player is near. If not, everything is culled and the drops become vItems.

### Decisions added 2026-11-17

- **Deathweed trees (SUPERSEDED, see "Decisions 2026-11-17 (Phase 1 chat)" below):** Ismail first wanted the tree chosen at random, ebonwood (corruption) or crimson. He dropped the random rule: the Corrupt box always grows corruption trees and the Crimson box always grows crimson trees.
- **Herbs only grow on their own box.** This applies to the Harvester Boxes and to the vanilla planter boxes, which are reworked to match (a herb survives and grows only on the planter box of its own herb).
- **Gem acorns (gemcorns) work too.** Assumption: a gemcorn works in any Harvester Box and the item decides the tree (the matching gem tree). Phase 0 confirms the item and tree names.
- **Future note, not now:** custom growth rules such as a light level requirement. Keep the growth check in one function (`HarvesterBoxRules`) so rules can be added there later.

### Decisions 2026-11-17 (Phase 1 chat, Ismail's answers to the 7c open decisions)

1. **Deathweed trees:** no random rule. The Crimson Planter Box (item name `CrimsonPlanterBox`, 3218 today) always grows crimson trees and the Corrupt Planter Box (`CorruptPlanterBox`, 3217 today) always grows corruption trees. Code finds the planter items BY NAME (`ItemID.Search`), never by id.
2. **A lone box grows a tree and herbs.** It follows Terraria's rules for acorns: an acorn with one valid block under it grows. An acorn with an open Harvester Box on its left and right can grow into a tree with the three wide trunk base. Phase 5 must therefore relax vanilla's "at least one fit SIDE neighbour" rule in `GrowTree` for boxes (hook, see 7c), while the wide root flare still only appears where the neighbouring boxes exist.
3. **Gemcorns and everything else reuse vanilla growing rules for now** (no special box exception for the underground rule). Custom rules later, in `HarvesterBoxRules`.
4. **The Blinkroot box grows a forest tree.**
5. Vanilla growth conditions (Daybloom by day, Moonglow by night, Blinkroot underground, and so on) stay as they are (unchanged assumption, not objected to).

### Assumptions (change them if wrong)

- A herb is harvested at the Mature and Blooming stages, with the vanilla yield for the stage it was found at.
- Replanting is free.
- Gears mesh with any adjacent Harvester Box, whatever its type.
- "Nearby" means about one screen from any player.
- The box has no inventory: drops appear at the box (real items) or become vItems (conveyors can pick them up).
- A custom wire on ANY box of a row connects the whole row to the grid (Phase 1b, see 6c); rows of different kinds do not join. (Phase 1 needed one wire per box.)

## 2. Design decisions

1. **ModTile, not vanilla tile + TileEntity.** The box must be told apart from a normal planter box, so it is a ModTile like Motors (`MotorTileBase` is the template: 1x1, `HookPostPlaceMyPlayer`, `Hook_AfterPlacement`). Eight separate ModTiles from one generic base (the `MotorTileBase<TEntity>` pattern, one class per box), NOT one tile with styles: vanilla decides a tree's look and drops from the soil TILE TYPE under the trunk (see 7c), so each box needs its own tile id. Frame and merge logic copied from the vanilla planter box. `HarvesterBoxTileEntity : ElectricConsumerMachine`, no UI, no recipes.
2. **Plants must live on a ModTile.** Vanilla herbs and saplings check the tile below (tML has `AnchorType.PlanterBox`, `PlanterBox` only means vanilla tile 380). Phase 1 proves the hooks. Fallback if hooks are not enough: `On_` hooks on the herb and sapling checks.
3. **Acorns:** right-clicking the box with an acorn consumes it and plants a sapling of the box's tree type. Growth is driven by the box on a timer (vanilla's tree-growing function), not by random world ticks. The sapling survival check must accept the box.
4. **Drops use a capture scope.** `RoboticArmDropCaptureSystem` / `RoboticArmDropCapture` (in the arm folder) already catches the items a tile break or tree fell spawns (`EntitySource_TileBreak`, `EntitySource_ShakeTree`) during a scoped call and hands them to the arm, which turns them into vItems (`RouteDrop`, `VirtualItemSystem.SpawnVirtualItem`). Phase 3 generalises that into a shared capture (an interface the arm and the box both implement) instead of copying it. Vanilla yields stay exact.
5. **Near or culled:** `BaseMachine.IsAnyPlayerNear` and `IsOnScreen` decide. Near = real items. Culled = skip the animation and particles, run the cycle on a timer, drops become vItems.
6. **Server-authoritative cycle:** `Idle -> Ready -> Extending -> Sweeping -> Retracting -> Idle`.

## 3. Multiplayer design (rule 9)

Update 2026-11-18: a row of same-kind boxes has ONE entity (at its leftmost box) and the server reconciles it after every place or mine; the lifecycle and the client side are in 6c. Rows below that say "per box" mean per tile.

| Thing | Authority | Sync |
|---|---|---|
| Cycle state machine, harvest, replant, tree growth timer, drops | Server and single-player | One small TE sync per state change (state + start tick) |
| Acorn right-click | Server validates | Request packet in `Common/Net/`; single-player runs the handler directly |
| Gears, staff, particles, sprite swap | Client only, never saved | Derived from tiles the client already has, synced `isOn`, and the state sync |
| Gear meshing | Client | One shared server-synced gear clock: angle = direction x clock + parity offset. Unpowered gears freeze and ease back in. The staff strike is timed from the same clock so the tip crosses the plant when the server harvests. |

## 4. Phases (one patch each)

- **Phase 0, research and spike.** Log this roadmap. Read the repo patterns (done, section 5). Dump the vanilla source for herbs, saplings, trees, planter boxes and gemcorns with `tools\harvester_probe.ps1` and report discrepancies. Finalise the tree table.
- **Phase 1, tile, item, power.** 8 ModTiles (one per planter box), frame/merge, 8 items, crafting recipe (placeholder ingredients, Ismail decides), powered TileEntity, registration in `MachineRegisterationSystem` (UI states, visuals, `Unload`), herbs plant and grow on the box, herb-only-on-own-box rule for Harvester Boxes and vanilla planter boxes. Needs the 8 tile sheets from Ismail (paths and sizes given in that phase).
- **Phase 2, gear visuals.** `IMachineDynamicDraw`: gears, parity direction, meshing, spin-up and spin-down, shared gear clock. Needs the gear sprite (idle and active). Exact paths and sizes are given in that phase.
- **Phase 3, herb harvest.** Server state machine, harvest, replant, shared drop capture, near vs vItem split. Works before the staff art exists.
- **Phase 4, staff and particles.** Staff grow/rotate/retract, active-gear swap, grass dust (only on screen, `IsOnScreen`), the client state sync, packets plus ROADMAP/APPENDIX edits.
- **Phase 5, acorns and trees.** Right-click planting, sapling survival hook, growth timer, felling the grown tree, replanting the sapling, tree table (random ebonwood/crimson for Deathweed, gemcorns).
- **Phase 6, polish and MP verification.** Culling thresholds, dedicated-server test pass, future-note hooks, roadmap cleanup.

## 5. What the repo already gives us (checked 2026-11-17)

- `MotorTileBase<TEntity>` (`Common/Liquids/`) is the 1x1 ModTile + TileEntity template: `TileObjectData.Style1x1`, `HookPostPlaceMyPlayer = PlacementHook(entity.Hook_AfterPlacement, -1, 0, true)`, `StyleHorizontal`/`StyleWrapLimit` set to avoid a division by zero.
- `BaseMachine.Hook_AfterPlacement` places the entity on the server (`MachinePlacement.ServerPlace`), and needs `ValidTileType` plus an entry in the visual registry.
- Registration hub: `MachineRegisterationSystem` (`Load` UI, `PostSetupContent` visuals, `PostAddRecipes`, `Unload`).
- `IMachineDynamicDraw` + `MachineDynamicDrawSystem` for the gear and staff drawing (behind tiles or in front).
- `RoboticArmTileEntity.RouteDrop` plus `VirtualItemSystem.SpawnVirtualItem` for the vItem conversion.
- `TileOverdrawSystem` if the gear or staff must be drawn outside the 16px cell.
- Power: a machine joins a grid through custom wires on its footprint (`PowerGridSystem`). `ElectricConsumerMachine` gives `PowerDemand`.

## 6. Progress

One line per phase. `[ ]` not started, `[~]` applied untested, `[x]` tested and done.

- [x] Phase 0: research done. Probe 2 report received 2026-11-17 and logged in 7c. Probe 3 (`tools\harvester_probe3.ps1`) was written in the Phase 1 chat and run there against the probe 2 decompile cache: planter styles, gemcorn ids, `GrowTree` callers and the seed placement path are answered in 7d. Still open: the callers in `Main` and `TileDrawing` (they were not decompiled yet). Run `harvester_probe3.ps1 -Dll ...` once to fill that in; its report goes in the next chat.
- [x] Phase 1: tile, item, power, herb-on-own-box rules. Applied 2026-11-17 (patch_harvester_p1), TESTED 2026-11-18 (Ismail: everything passed). See 6b.
- [x] Phase 1b: row power sharing (one entity per row) and the right-click electricity panel. Applied 2026-11-18 (patch_harvester_rows), TESTED 2026-11-19 (Ismail: done and tested). See 6c.
- [x] Phase 2: gear visuals. Applied 2026-11-19 (patch_harvester_gears and patch_harvester_gears_fix), TESTED 2026-11-19 (Ismail: looks good). See 6d. The gear PNGs are Ismail's art.
- [~] Phase 3: herb harvest and drops. Applied 2026-11-19 (patch_harvester_harvest), UNTESTED. See 6e.
- [~] Phase 4: staff, particles, client sync. Applied 2026-11-19 (patch_harvester_staff), UNTESTED, tested together with Phase 3. See 6f. Needs the staff sprite from Ismail (6f).
- [~] Phase 5 (acorns and trees; palm, ash and gemcorns are not in yet): applied 2026-11-20 (patch_harvester_trees), UNTESTED, see 7h. Older notes on this line: acorns, trees, gemcorns
- [ ] Phase 6: polish, MP verification

## 6b. Phase 1 as built (patch_harvester_p1, 2026-11-17, applied UNTESTED)

What exists now:
- `Common/Harvester/`: `HarvesterBoxKinds` (enums `HerbStyle`, `HarvesterBoxKind`, planter item NAMES, kind to herb), `HarvesterBoxRegistry` (tile type to herb, vanilla planter style to herb, herb anchor list extension), `HarvesterBoxRules.HerbAllowedOnSoil` (THE place for growth rules), `HarvesterBoxHerbHooks` (ModSystem: `On_WorldGen.CheckAlch` and `On_WorldGen.PlaceAlch`), `HarvesterBoxTileBase<TEntity, TItem>`, `HarvesterBoxTileEntityBase<TTile>`, `HarvesterBoxItemBase<TTile>`.
- `Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/`: 8 tiles, 8 entities, 8 items (`DaybloomHarvesterBox...`, Moonglow, Corrupt, Crimson, Blinkroot, Waterleaf, Shiverthorn, Fireblossom) and `HarvesterBoxRegistration` (visual registration, placeholder recipes).
- Edits: `MachineVisualDefinition.DrawNormalTile` (the machine draw override steps aside, so the tile draws its own merge frames), `MachineRegisterationSystem` (visuals in PostSetupContent, `HarvesterBoxRegistry.Clear()` in Unload), `RecipeSystem` (calls `HarvesterBoxRegistration.AddRecipes`), hjson names.
- Tile: 1x1 ModTile, platform-like (solid top), unhammerable, placeable beside a tile or with a wall behind it (motor rule). Frames: `TileFrame` override sets frameX = 18 * cell from the same-type boxes on the left and right (0 lone, 1 left end, 2 middle, 3 right end), frameY 0. Different box types do not merge. Frames are tile data, so nothing is synced.
- Entity: `ElectricConsumerMachine`, `PowerDemand` 10 (placeholder), `isWorking = true` always. Joins a grid through a custom wire on its own tile. No UI, no recipes, no cycle yet.
- Herb rules: a herb survives and can be planted only on the box of its own herb. Vanilla planter boxes get the same rule (their style comes from the `placeStyle` of the planter box ITEM found by name, so no style number is hard-coded). Clay pot and normal soils are untouched. A Harvester Box is not in vanilla's hard-coded soil lists, so for the length of one vanilla call the hook makes the soil tile look like a vanilla planter box (the real type is always restored). That keeps vanilla's liquid and lava rules per herb exactly. The hooks read tile data only, so they behave the same on every client and the server and need no packet. No new netcode in Phase 1 (placement goes through the existing `MachinePlacement` route).
- Herb seeds placed by hand go through `WorldGen.PlaceTile` to `PlaceAlch` (probe 3: `Player.cs:39064` sends every tile except 82 and 227 down the `TileObject` path), so the same hook covers hand planting, natural planting (`PlantAlch`) and survival (`CheckAlch`, called from `TileFrame`). The herb `TileObjectData` anchors are not used for seeds and are left untouched.
- Existing wrong herbs on vanilla planter boxes are only removed when their tile is next framed (a neighbour change); nothing sweeps the world.

Art for Ismail (all optional: until a PNG exists the tile borrows the vanilla planter box sheet and the item borrows the planter box icon):
- Tiles: `Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/<Kind>HarvesterBoxTile.png`, 72x16, four 16x16 frames with 2 px gaps at x = 0, 18, 36, 54: lone box, left end, middle, right end. Kinds: Daybloom, Moonglow, Corrupt, Crimson, Blinkroot, Waterleaf, Shiverthorn, Fireblossom.
- Items: `.../HarvesterBox/<Kind>HarvesterBoxItem.png`, 32x32.

Phase 1 test checklist: see the patch reply (placing and merging, mining, herb planting by hand and by growth, wrong herb rejected on vanilla planters, power, multiplayer).

## 6c. Phase 1b: row power sharing and the right-click panel (patch_harvester_rows, 2026-11-18, applied UNTESTED)

Decisions (Ismail, 2026-11-18, after Phase 1 passed testing):
1. A row is Harvester Boxes of the SAME kind touching left and right (the same rule the tile merge frames use). Powering one box powers the whole row.
2. ONE merged tile entity per row. The alternative (one entity per box, with PowerGridSystem treating a touching row as one connection) was compared and recommended: it needs no save change and no entity create/kill code. Ismail chose the merged entity.
3. A vanilla wire switch toggles the WHOLE row (it is the one entity's `WireToggle`).
4. Power demand is per box: `PowerDemand` = 10 x row length (the 10 is still a placeholder). Each box will run its own gear and staff, so power should grow with work.
5. Right-click on a box opens a small panel that shows only the electricity icon, on the tile that was clicked (not on the row's first box).

How it works
- Invariant (server and single-player): every maximal run of same-kind box tiles has exactly ONE entity, at the run's leftmost box. Row length, `cornerPosition`, `MachineWidth`, `MachineCenter` and the demand are worked out from the tiles and never saved, so the save format did not change. `Common/Harvester/HarvesterBoxRows.cs` has the stateless helpers (run, entity of a run, geometry, the kill path); `Common/Harvester/HarvesterBoxRowSystem.cs` has the reconcile. `Common/Machines/IRowMachine.cs` is the small interface the entity implements.
- Lookup: `TileEntityHelper.TryGetEntityFromTile` is row-aware for box tiles only, so draw, light, wire, right-click, the bootstrap scan and the power grid resolve any box of a row to the row's entity (the corner it returns is the entity's position).
- Lifecycle: placing a box (`Hook_AfterPlacement`, or a client's request through `NetPlaceEntityAttempt`, which creates nothing when the row already has an entity), mining a box (`MachineRemoval.OnTileKilled` hands box tiles to `HarvesterBoxRows.OnTileKilled`) and world load only MARK a position dirty. `HarvesterBoxRowSystem.PostUpdateWorld` reconciles the marked runs one tick later (the kill hook runs before the tile is gone): it kills extra entities, creates a missing one at the left end through `MachinePlacement.ServerPlace` (which shares it), and carries the wire switch state over (off wins when rows join; a split-off part inherits it). Every 300 ticks a cheap check marks any entity that is not at its run's left end or that shares its run with another entity. World load marks every entity once, which folds the one-entity-per-box rows of Phase 1 worlds into one entity per row.
- Power: `PowerGridSystem.EnqueueMachineWires` reads the whole span of an `IRowMachine`, so a custom wire on any box connects the row. `PowerDemand` = 10 x `RowLength`.
- Panel: `MachineVisualOverride.RightClick` records the clicked tile in `BaseMachine.RightClickedTile`; the box's `OnRightClick` calls `MachineUISystem.OpenUI` at that tile with the row entity. `HarvesterBoxUIState` is the pole panel's twin: only `ElectricityUIIcon`, 40x40 at offset (-12, -60), art `Common/UI/CustomUIElements/Lightning_*.png`, nothing new. Registered for all 8 tiles in `HarvesterBoxRegistration`. Before this patch a right-click on a box did nothing: `MachineVisualOverride.RightClick` only checks the visual registry (`DrawNormalTile` is read only in `PreDraw`), reached `BaseMachine.OnRightClick`, found no entry in `MachineUIRegistry` and returned silently.

Multiplayer design (rule 9)
- Authority: the server and single-player decide when row entities are created or removed. A client never creates one; it gets them through message 86 (`MachinePlacement.Share`) or with the section data.
- Synced: nothing new, no new packet. `isOn`, `isWorking`, `WireToggle` and the power readout ride the existing per-entity snapshot and light sync. Tile frames are tile data.
- Removal: nothing broadcasts a server-side entity kill (multiplayer verification item 14 is still open), so a client (a) removes the replica on a tile it sees mined, as before, and (b) every 20 ticks removes any replica that has another replica to its left in the same run, and refreshes the geometry of the rest (a client never runs `Update`).
- The dirty queue is locked because `NetPlaceEntityAttempt` may run on the network thread.
- Single-player runs the same reconcile.

Consequences for later phases
- Phases 2 to 4 want PER-BOX state (gear parity, the harvest cycle, the plant above each box). The entity is now per row, so that state must be derived from the tiles (x parity for the gears) or kept in an array indexed by `x - Position.X` that is rebuilt from the tiles whenever the row changes. A merge, a split or a new left end kills and recreates the entity, so such state must not be saved only on the entity. Decide this before Phase 2.
- Extending a row at its left end moves the entity (kill, create at the new left end): an open panel closes and entity-only state resets.

Known limits
- Not compiled or run. The entity create/kill churn and the client cleanup are new code: test them in multiplayer.
- `MachineDetailSystem.WithinReach` measures to the nearest box of the row.
- The smart-cursor outline (`MachineSmartOutlineSystem`) uses the row length as the footprint width, so one outline covers the whole row; the "nearby" right-click fallback (cursor not over the row) opens the panel on the row's first box.
- A wireless row of two or more boxes now reads "No power" on hover (it forms a grid), a lone box still reads Off.

## 6d. Phase 2: gear visuals (patch_harvester_gears, 2026-11-19, applied UNTESTED)

Decisions (Ismail, 2026-11-19):
1. Gear direction comes ONLY from the parity of the world tile x (even one way, odd the other). Nothing is stored, so any two touching gears mesh, whatever the row or kind.
2. Per-box state for Phases 3 and 4 (harvest cycle, plant above): an array indexed by `x - Position.X`, rebuilt from the tiles whenever the row changes (a merge, a split or a new left end rebuilds the entity), never saved only on the entity.
3. The right-click electricity panel always opens in the MIDDLE of the row (supersedes 6c decision 5, "on the tile that was clicked"): middle box of an odd row, between the two middle boxes of an even row (`HarvesterBoxUIState.PanelShiftX` = 8 px), whichever box was clicked. Clicking any box of an open row closes it.
4. The gear draw size is read from the texture. The sprite has 4 teeth, on the corners. The mesh offset (degrees) and the speed are tunable properties.

As built (all in `HarvesterBoxTileEntityBase`, client only, never saved or sent)
- The row entity implements `IMachineDynamicDraw`. `DynamicUpdate` runs on clients and single-player only; `DynamicDraw` draws one gear per box in front of the tiles, so in front of the plant.
- Tunables, expression-bodied so hot reload works: `GearTexturePath`, `GearActiveTexturePath`, `GearSpeedDegreesPerTick` (3), `GearMeshOffsetDegrees` (90), `GearTeeth` (4), `GearSpinUpTicks` / `GearSpinDownTicks` (30), `GearSyncEase` (0 = off), `GearOffset` (0, from the middle of the box tile), `GearScale`.
- Angle: even world x = `phase`, odd world x = `-phase + GearMeshOffsetDegrees`.
- One shared clock: `GearClock` is a pure function of `Main.GameUpdateCount`, so every gear on a client agrees with no state. The clock is NOT synced between clients (cosmetic). Phase 4 times the staff strike from the server's cycle start tick, not from this clock.
- Spin-up and spin-down (changed 2026-11-19 after testing): each row eases a `gearSpeed` 0..1 with `isOn` (the wire switch is already folded into `isOn`). A gear stops wherever it was and speeds up from there, with nothing pulling it to a rest angle or onto the clock (the first version snapped, which made gears jump back on power cut and hiccup on startup). Gears of one row always mesh (same phase, direction by x parity). Rows that started or stopped at different times can be out of mesh with each other; set `GearSyncEase` above 0 (try 0.005) to let full-speed gears slowly slide onto the shared clock if that bothers you.
- A new entity (row change) starts at full speed if powered, so a merge shows no spin-up blip.
- `IsGearActive(boxIndex)` is the hook Phase 4 overrides for the active-gear sprite (false now; the idle sprite stands in while the active PNG does not exist).
- No gear PNG yet means nothing is drawn (`RequestIfExists`).
- Netcode: none, so no `docs/multiplayer` edit.

Art for Ismail: `Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/HarvesterBoxGear.png` (idle) and `HarvesterBoxGear_Active.png` (same size, same centre), each a single frame with the gear centred.

## 6e. Phase 3: herb harvest (patch_harvester_harvest, 2026-11-19, applied UNTESTED)

What exists now (all server / single-player; no new packet)
- `HarvesterBoxTileEntityBase.UpdateHarvest`, called from `Update` (a multiplayer client never runs `Update`). While the row is powered (`isOn`; a wire switch or a brownout freezes the cycle, and the delay only counts powered ticks) it looks every `HarvestScanTicks` (20) at the tile above each idle box. When that plant is ready the box goes `Idle -> Ready`; after `HarvestDelayTicks` (60) powered ticks it calls `HarvestHerb(x, y)`. The delay is a stand-in for the staff sweep: Phase 4 replaces it with Extending / Sweeping / Retracting and keeps `HarvestHerb` as the single place that harvests.
- Ready means (`HarvesterBoxRules.IsReadyToHarvest`, THE place to change it): tile 83 (Mature) or 84 (Blooming) AND the herb's style is the box's own herb (Ismail's assumption: harvest at Mature and Blooming, with the vanilla yield of the stage found: mature outside its condition gives the herb only, blooming gives herb and seeds). A plant that is removed or changed while waiting sends the box back to Idle.
- `HarvestHerb`: `WorldGen.KillTile(x, y)` inside the shared drop capture (vanilla yield and sounds), then `WorldGen.PlaceAlch(x, y, style)` (free replant, goes through the Phase 1 hooks), then on a server the tile kill (`MessageID.TileManipulation`) and a tile square are sent to clients. In single-player nothing is sent.
- Per-box state is `BoxState[]` indexed by `x - Position.X`, rebuilt when the row length changes (a merge, a split or a new left end rebuilds the entity and so resets it), never saved. A reset only restarts a waiting plant's delay.
- Shared drop capture: new `IDropCaptureTarget` (`Common/Machines/IDropCaptureTarget.cs`, one method `RouteDrop(Item)`), implemented by `RoboticArmTileEntity` and by the box entity. `RoboticArmDropCaptureSystem.Active` and `.Run` now take the interface (the classes keep their names and their file in the arm folder).
- Drop routing (`RouteDrop`, same rules as the arm, around the harvested plant's tile: the tile above, left and right):
  1. a conveyor beside the plant: the drop becomes a vItem on a free conveyor tile; a full belt PAUSES the harvest (the box waits, like the arm's back-pressure);
  2. no conveyor and no player near (`IsAnyPlayerNear`) and not on screen: a vItem on a free non-solid neighbouring tile (it falls and waits to be picked up);
  3. no conveyor and a player near: left alone, it drops into the world like vanilla.
- No saves, no new packet. Vanilla item and tile sync carry the result; the vItem system syncs its own objects.

Multiplayer design (rule 9)
- Authority: the server (and single-player) runs the whole cycle: the scan, the harvest, the replant, the drop routing. Clients never decide anything.
- Synced: only what vanilla and the vItem system already sync (the killed tile and replanted herb as tile messages, real drops as world items, vItems through VItemNet). The staff and the active gear are Phase 4 and add the one small per-state-change sync then.
- Single-player runs the same code path (no tile messages sent).

Known limits
- Not compiled or run.
- Phase 3 has no animation: the herb simply pops. The gears turn but do not yet react to a ripe plant (`IsGearActive` is still false).
- A row far from every player keeps running (like every machine) and its drops become vItems; this is the part most worth testing on a dedicated server.

## 6f. Phase 4: staff, particles and the strike packet (patch_harvester_staff, 2026-11-19, applied UNTESTED, tested together with Phase 3)

What exists now
- The per-box cycle is now `Idle -> Ready -> Strike -> Idle` (the roadmap's Extending, Sweeping and Retracting are the three parts of Strike, told apart by the tick count, so there is one tick counter, not three states). Server and single-player run it in `UpdateHarvest`; every count is in POWERED ticks, so a cut or a brownout freezes it.
  - Ready: the plant above is Mature or Blooming and the box's own herb. After `HarvestDelayTicks` (now 30) the strike starts, unless a conveyor beside the plant is full (then the box waits, as in Phase 3).
  - Strike (`StaffExtendTicks` 20, `StaffSweepTicks` 40, `StaffRetractTicks` 20): the staff grows out of the gear, turns at the gear's own speed (`GearSpeedDegreesPerTick`) and in the gear's direction (x parity), and its tip is exactly over the plant at the cross tick (extend + sweep / 2). At that tick the server calls `HarvestHerb` (kill with the shared drop capture, free replant, tile messages). If the plant vanishes before the cross tick the box goes back to Idle.
- Active gear: `IsGearActive(k)` is true while the box is Ready or in Strike. A client works Ready out from its own tiles (no packet). The gear CROSSFADES between its idle and active sprite over `GearActiveFadeTicks` (12): a per-box blend value (client visual only) fades the active sprite in over the opaque idle one, then the idle one out, so the gear never looks see-through.
- Staff drawing (client, in `DynamicDraw`, under the gears): the sprite points diagonally to the top right with its pivot at the bottom left corner (Ismail's art); it is turned by `StaffSpriteRotationDegrees` (-45) so it points up at angle 0, and it GROWS by scaling the whole sprite up from its pivot (uniform scale = growth), so it grows along the direction it faces. Tunables: `StaffTexturePath`, `StaffOffset`, `StaffPivotOffset` (texture pixels from the bottom left corner), `StaffSpriteRotationDegrees`, `StaffScale`, the four tick counts, `StaffAbortMarginTicks`.
- Particles: grass dust (`StaffDustType` 2) from random points along the whole length of the staff (pivot to tip) while it moves, `StaffDustChance` per tick, only when the tip is on screen. No dust and no draw on a dedicated server.
- Hot-reload tunables only; nothing is saved.

Multiplayer design (rule 9)
- Authority: the server (and single-player) runs the cycle, the harvest, the replant and the drops, exactly as in Phase 3.
- ONE new packet, `HarvesterBoxStrike` = 62, server to nearby clients (`Common/Net/HarvesterBoxNet.cs`, `NearbyTiles` 100): payload `int x, int y` of the box. It is sent once per strike, when Ready turns into Strike. It is purely the cosmetic start: a client queues it, and on the main thread starts the strike on the row entity it already has (`IHarvesterBoxRow.BeginClientStrike`), then replays the rest with the same tick counts as the server.
- Not synced on purpose: Ready (derived from the client's own tiles), the end of the strike, a cancel (a client sees the same plant vanish and aborts after `StaffAbortMarginTicks`), the gears, the dust. No join sync: a strike in progress at join time is simply not drawn.
- Single-player sends nothing; a host that plays does not need the packet either (its visuals read the entity's own state).
- Timing: the client starts its strike when the packet arrives and the server's tile messages travel the same way, so the staff crosses the plant at about the moment the herb pops, within the connection's jitter.

Art for Ismail
- `Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/HarvesterStaffOfRegrowth.png`: the staff of regrowth, one frame, pointing diagonally to the top right with its pivot at the bottom left corner (the gear centre is the pivot, the plant is about 16 px above it, so the staff must reach about 20 px along its axis). Until it exists nothing is drawn and no dust spawns. If the folder differs, change `StaffTexturePath`.
- `HarvesterBoxGear_Active.png` (6d) is used from now on while a plant is ripe.

Known limits
- Not compiled or run.
- A client keeps a strike alive for a few ticks after the plant vanishes (the abort margin). A box whose entity is rebuilt mid-strike (a row change) loses the strike on the server; clients stop theirs when it ends or the plant changes.

## 7. Verify (resolved by the probe, then recorded here)

- Vanilla herb check: the tile types and the planter style each herb requires (`CheckAlch`, `PlaceAlch`, herb growth). Planter box style order.
- How `TileObjectData` anchors herbs and saplings (`AnchorType.PlanterBox`).
- Sapling survival and tree growth (`CheckSapling`, `GrowTree`, `TryGrowingTreeByType`, `GetTreeType`), sapling styles per tree.
- Gemcorn item names, the sapling they place, the tree they grow.
- Herb drop rules by stage (and what `IsHarvestableHerbWithSeed` says).
- Callers of drops tolerate the capture approach for herbs (the arm only proved trees and blocks).

## 7b. Findings so far (probe 1 report plus tML source, 2026-11-17)

- Vanilla `CheckAlch`/`PlaceAlch`, planter box styles and gemcorn names are still UNVERIFIED (probe 1 saw only `.patch` files).
- tML plant API: `ModTree`/`ModPalmTree` are keyed by the SOIL tile type (`GrowsOnTileId`, `PlantLoader.Get<ModTree>(TileID.Trees, soilType)`), so one soil tile id gives exactly one tree type with its own textures. Not usable for per-style or random vanilla trees, so we do not use `ModTree`.
- `WorldGen.GetTreeType(int soilTileType)` asks `PlantLoader.GetModTreeType` first, then vanilla's switch on the soil tile. It has no position, so a Harvester Box soil would need a scoped override (set by the box right around its own growth call, same pattern as the drop capture) or an `On_WorldGen.GetTreeType` hook.
- Better sapling route: our own sapling ModTile with `TileID.Sets.TreeSapling` and `CommonSapling` set, and `ModTile.GrowSapling(i, j, underground, treeHeightAddon, ignoreWalls)` overridden, so the box fully controls growth (it can call `WorldGen.GrowTree` or `GrowPalmTree` and choose the type). `GrowTree` loops over `TreeSapling` tiles and uses the soil for the type.
- `TileID.Sets.IgnoredByGrowingSaplings` already lists the herb tiles (82, 83, 84), so a herb beside a growing sapling does not block it.

## 7c. Probe 2 findings (decompiled tModLoader.dll, 2026-11-17). Facts, not guesses.

### Herbs (WorldGen.CheckAlch / PlaceAlch / GrowAlch / PlantAlch / IsHarvestableHerbWithSeed)
- Herb tiles: 82 ImmatureHerbs, 83 MatureHerbs, 84 BloomingHerbs. The herb type is the tile's `frameX / 18` (style): 0 Daybloom, 1 Moonglow, 2 Blinkroot, 3 Deathweed, 4 Waterleaf, 5 Fireblossom, 6 Shiverthorn.
- Valid soil per style (checked in `CheckAlch` for survival and in `PlaceAlch` for planting; the lists are hard-coded `Main.tile[x, y + 1].type != ...` chains): Daybloom: 109, 2, 477, 492, 78 (clay pot), 380. Moonglow: 60, 78, 380. Blinkroot: 0, 59, 78, 380. Deathweed: 661, 662, 199, 203, 23, 25, 78, 380. Waterleaf: 53, 116, 78, 380. Fireblossom: 57, 633, 78, 380. Shiverthorn: 147, 161, 163, 164, 200, 78, 380.
- **Vanilla planter boxes (tile 380) accept EVERY herb.** There is no style check anywhere. The "herbs only grow on their own box" rework therefore has to be added: hook `CheckAlch` and `PlaceAlch` (`On_` hooks) so that on tile 380 the planter's style must match the herb style, and the same for our 8 boxes. The clay pot (78) stays as it is unless Ismail says otherwise.
- `GrowAlch` growth conditions are by herb style and ignore the soil: Daybloom only grows by day, Moonglow by night, Blinkroot underground (`y > worldSurface`), Waterleaf when raining, Fireblossom in the underworld (`y > maxTilesY - 200`), Shiverthorn has its own random chance, Deathweed none. Planter boxes obey the same conditions. Kept as is for now (future note: custom rules go in `HarvesterBoxRules`).
- Harvest stage: `IsHarvestableHerbWithSeed(type, style)`: tile 84 is always seed-harvestable. Tile 83 is when the herb's own condition holds (Daybloom day, Moonglow night, Deathweed night with blood moon or full moon, Waterleaf rain, Fireblossom late day). Vanilla `KillTile` drops herb item `313 + style` and seed `307 + style` (Shiverthorn special-cased). Breaking the herb tile with the normal kill call gives the vanilla yield, which is what we capture.
- Replant: `WorldGen.PlaceAlch(x, y, style)` sets tile 82 with `frameX = 18 * style`.

### Planter boxes
- Vanilla has EIGHT planter box items in this order (`ItemID`): DayBloomPlanterBox 3215, MoonglowPlanterBox 3216, CorruptPlanterBox 3217, CrimsonPlanterBox 3218, BlinkrootPlanterBox 3219, WaterleafPlanterBox 3220, ShiverthornPlanterBox 3221, FireBlossomPlanterBox 3222. Corrupt and Crimson are both Deathweed boxes. The tile-380 style order is probably the same, but NOT confirmed (probe 3).
- `TileObject.cs`: the anchor type `AnchorType.PlanterBox` only matches `tile.type == 380`. Banners and similar hang from `AnchorTop = ... | PlanterBox`, so a hanging thing will not attach under a Harvester Box unless that check is hooked. Low priority.
- Vanilla treats tile 380 like a platform in several places (`TileID.Sets.Platforms[type] || type == 380`, frame and conflict checks). Phase 1 must read the 380 branches of `TileFrame` before copying the merge logic.

### Trees (the part that decides the design)
- `AttemptToGrowTreeFromSapling(x, y, underground)` chooses the growth from the SAPLING tile and its style (`frameX / 54`), not from the soil: tile 20 with style 6 to 9 grows a palm (`GrowPalmTree`), style 10 an ash tree (`TryGrowingTreeByType(634)`), every other style `GrowTree`. Tile 595 grows tree 596 (Sakura), tile 615 tree 616 (Willow), and tile 590 is the GEM sapling: style 0 to 6 grows tree 583 (Topaz), 584 (Amethyst), 585 (Sapphire), 586 (Emerald), 587 (Ruby), 588 (Diamond), 589 (Amber). It returns false for gem saplings when not `underground`, and for forest-type saplings when `underground`. Server only (`netMode == 1` returns false).
- `GrowTree` and `GrowPalmTree` are the old forest-tile-5 and palm builders. A tree's LOOK, wood and acorn drops are decided later from the soil tile under the trunk through `GetTreeType(int soilTileType)`: 2 and 477 Forest, 23 Corrupt, 199 Crimson, 60 Jungle, 147 Snow, 109 and 492 Hallowed, 53 Palm, 112 PalmCorrupt, 234 PalmCrimson, 116 PalmHallowed, 633 Ash, 70 Mushroom. It asks `PlantLoader.GetModTreeType` first. It takes the soil TYPE only, with no position, so the tree type follows the soil tile id. That is why each box needs its own tile id, and why a per-planting random Deathweed tree cannot be done by soil alone (see the open decision).
- `GrowTree` demands `IsTileTypeFitForTree(soil)` for the soil and for at least one SIDE neighbour (`tile[i - 1, j]` or `tile[i + 1, j]`), plus an empty-space check and a wall check. A single box with air on both sides cannot grow a vanilla tree. A row of boxes can, if `IsTileTypeFitForTree` is true for them.
- `Check1x2` (the tile-frame check of a sapling) re-derives the sapling style from the soil on every update (soil 2 or 477 style 0, 147 style 1, 60 style 2, 23 or 661 style 3, 199 or 662 style 4, 109 or 492 style 5, 53 style 6, 116 style 7, 234 style 8, 112 style 9, 633 style 10) and DESTROYS the sapling when the soil is not in that list and `TileLoader.SaplingGrowthType` (the `ModTree` route) says no. So a sapling on a box dies unless `Check1x2` is hooked or the box is registered as a ModTree soil.
- `TileID.Sets.TreeSapling` and `CommonSapling` are the sets tML offers for custom saplings; `ModTile.GrowSapling` overrides growth of a custom sapling tile.
- `ModTree` is keyed by soil tile id (one tree per soil) and brings its own textures, so it fits one custom tree per tile, which is not what we want (we want vanilla looks).

### What this means for the design (proposal, to be confirmed in Phase 5)
- 8 box tiles, each mapped to a vanilla tree type through a static soil to `TreeTypes` registry consulted by an `On_WorldGen.GetTreeType` hook (the soil id is all that function gets, so a stateless lookup works). The same registry feeds hooks on `IsTileTypeFitForTree` and `Check1x2`.
- Growth is driven by the box itself: it plants a sapling of the right tile and style (20 with the style of its tree type, or 590 for a gemcorn) and calls `AttemptToGrowTreeFromSapling` on a timer, passing `underground = false` for normal saplings and `true` for gem saplings (gem trees only grow with that flag).
- Suggested box to tree mapping: Daybloom Forest, Moonglow Jungle, Blinkroot Forest (dirt and mud), Waterleaf Palm, Shiverthorn Snow (boreal), Fireblossom Ash, Corrupt Corruption (ebonwood), Crimson Crimson tree.

### Open decisions for Ismail (ANSWERED 2026-11-17, see "Decisions 2026-11-17 (Phase 1 chat)")
1. Two Deathweed boxes exist. Random tree for both, or fixed (Corrupt = ebonwood, Crimson = crimson tree)? Random per planting needs more than a soil lookup (the type would have to be stored per box and read by drawing and drop code).
2. A single box in open air cannot grow a vanilla tree (side-neighbour rule). Accept (rows of boxes work) or hook the rule so a lone box works?
3. Gem trees only grow with `underground = true` in vanilla. In a box they should grow anywhere (assumed yes).
4. Blinkroot box: Forest tree (assumed).
5. Keep vanilla growth conditions (Daybloom grows by day and so on) until the custom-rules future note (assumed yes).

### Still unknown (probe 3, small, needs `-Dll "C:\GOG Games\Terraria\tModLoader\tModLoader.dll"`) PROBE 3 SCRIPT WRITTEN 2026-11-17 (`tools\harvester_probe3.ps1`); ANSWERED in 7d except the callers in Main and TileDrawing.
- Tile-380 style order: `Item.SetDefaults` for items 3215 to 3222 (`createTile`, `placeStyle`), and the `TileFrame` / `TileObjectData` branch for tile 380 (frames, merging).
- Gemcorn item ids and the sapling style each places (items whose `createTile` is 590, 595 or 615), plus the vanilla acorn items.
- Callers of `GetTreeType(`, `IsTileTypeFitForTree(` and `TreeTypes` in `WorldGen`, `Main` and `TileDrawing` (to size the hook work), and the full `IsTileTypeFitForTree`.
- The vanilla 1x1 platform-like draw rules needed to mimic a planter box when drawn (frames).

## 7d. Probe 3 findings (2026-11-17, run on the probe 2 decompile cache). Facts, not guesses.

- **Planter box items and styles:** `DayBloomPlanterBox` 3215, `MoonglowPlanterBox` 3216, `CorruptPlanterBox` 3217, `CrimsonPlanterBox` 3218, `BlinkrootPlanterBox` 3219, `WaterleafPlanterBox` 3220, `ShiverthornPlanterBox` 3221, `FireBlossomPlanterBox` 3222. `Item.SetDefaults`: `createTile = 380`, `placeStyle = type - 3215` (`Item.cs:33728`). `WorldGen.PlaceTile` case 380 sets `frameY = 18 * style` (`WorldGen.cs:55234`), `KillTile` drops `3215 + frameY / 18` (`WorldGen.cs:59998`). Merging is a `if (num == 380)` branch in `WorldGen.TileFrame` (`WorldGen.cs:76777`). So the style order is the ItemID order above, and Phase 1 reads each style from the item's `placeStyle` by name.
- **Gemcorns** are the `GemTree<Gem>Seed` items: Topaz 4851, Amethyst 4852, Sapphire 4853, Emerald 4854, Ruby 4855, Diamond 4856, Amber 4857. Each is `DefaultToPlaceableTile(590, style)` with style 0 to 6 in that order (`Item.cs:44193` on): tile 590 is the gem sapling. The plain acorn is `Acorn` 27 with `createTile = 20`. Other saplings: `DefaultToPlaceableTile(595, 0)` (`Item.cs:44255`) and `(615, 0)` (`Item.cs:44402`); the Sakura and Yellow Willow seeds are `VanityTreeSakuraSeed` 4871 and `VanityTreeYellowWillowSeed` 4907 (not checked which of the two uses 595 and 615).
- **Tree soil rule:** `WorldGen.IsTileTypeFitForTree(ushort)` (`WorldGen.cs:23033`) is a switch over tiles 2, 23, 60, 70, 109, 147, 199, 477, 492, 633, 661, 662 and otherwise returns `TileLoader.CanGrowModTree(type)`, so a mod tile can be made fit through tML instead of a hook (check the exact tML API before Phase 5). Its callers in `WorldGen`: `GrowTree` (the soil under the sapling at 23065, and the left / right neighbour rule at 23350 and 23354) and `GrowTreeWithSettings` (23958 and 23962). `GetTreeType` is called only from `IsThisAMushroomTree` (58005) and `ShakeTree` (58033) in `WorldGen`. No hits in `Player`, `Item` or `TileObject`. `Main` and `TileDrawing` were not decompiled yet: probe 3's caller list for them is the one open item.
- **Seed placement:** `Player.cs:39064` sends every tile except 82 and 227 down the `TileObject` path, so a seed in the hand goes `WorldGen.PlaceTile` (default case, `Main.tileAlch[num]`, `WorldGen.cs:55187`) to `PlaceAlch`. The herb `TileObjectData` (`AnchorBottom = SolidTile | AlternateTile`, valid tiles per style, alternate 78) is not used for seeds. `CheckAlch` runs from `TileFrame` (`WorldGen.cs:77341`), `GrowAlch` from the overground and underground tile updates, `PlantAlch` is the natural spawn.

## 7e. Probe 3 report (run on Ismail's tModLoader.dll, 2026-11-18): reference for Phase 5 (trees)

Status: everything 7c listed under "Still unknown" is now answered. Nothing is left to run before Phase 5, except the two small dumps named at the end of this section. WorldGen line numbers below are from `Terraria.WorldGen` decompiled from `C:\GOG Games\Terraria\tModLoader\tModLoader.dll` (the same numbers as the probe 2 cache). Facts first, then what they mean for Phase 5.

Facts
- **Who calls `IsTileTypeFitForTree(ushort)`** (`WorldGen.cs:23033`; true for tiles 2, 23, 60, 70, 109, 147, 199, 477, 492, 633, 661, 662, otherwise `TileLoader.CanGrowModTree(type)`): `GrowTree` at 23065 (the soil under the sapling) and 23350 and 23354 (the left / right neighbour rule), and `GrowTreeWithSettings` at 23958 and 23962. Nothing else in `WorldGen`, and zero hits in `Main`, `TileDrawing`, `Player`, `Item` and `TileObject`.
- **Who calls `GetTreeType(int)`** (`WorldGen.cs:57962`): only `IsThisAMushroomTree` (58005) and `ShakeTree` (58033, which also holds all 32 other `TreeTypes` uses, the drops). Zero hits in `Main`, `TileDrawing`, `Player`, `Item` and `TileObject`. It first asks `PlantLoader.GetModTreeType(tileType)`; otherwise soil tile to `TreeTypes`: 2 and 477 Forest, 23 Corrupt, 199 Crimson, 70 Mushroom, 60 Jungle, 147 Snow, 109 and 492 Hallowed, 53 Palm, 112 PalmCorrupt, 234 PalmCrimson, 116 PalmHallowed, 633 Ash, anything else `None`.
- **Drawing does not use either function.** `TileDrawing` has its own `GetTreeBiome(tileX, tileY, tileFrameX, tileFrameY)` (`TileDrawing.cs:2147`, private static), used by `DrawTrees` (9557), `GetTileDrawData` (5667), `GetTileDrawTexture` (1555, 1606) and `PrepareForAreaDrawing` (293). `WorldGen.GetTreeFrame(Tile)` is at 57949 and `GetCommonTreeFoliageData` (57788 to 57841) uses it. The bodies of `GetTreeBiome` and `GetTreeFrame` were not dumped.
- **Planter box tile 380 in `Main`:** `tileFrameImportant[380]`, `tileTable[380]`, `tileSolid[380]` and `tileSolidTop[380]` are true (`Main.cs:7933, 8052, 9901, 9902`). `TileDrawing` has no special case for 380 (0 hits), so it is drawn by the normal tile draw from its frames. That is why the Harvester Box draws as a normal framed tile (`DrawNormalTile`, Phase 1).
- **Gemcorns:** `GemTreeTopazSeed` 4851, `GemTreeAmethystSeed` 4852, `GemTreeSapphireSeed` 4853, `GemTreeEmeraldSeed` 4854, `GemTreeRubySeed` 4855, `GemTreeDiamondSeed` 4856, `GemTreeAmberSeed` 4857. They use `DefaultToPlaceableTile((ushort)590, style)` with style 0 to 6 in that order, so a text search for `createTile = 590` finds nothing (B2 came back empty for 590, 595 and 615 for that reason). The plain acorn is `Acorn` 27, `createTile = 20` (`Item.cs:3924`). Other sapling items: `DefaultToPlaceableTile((ushort)595, 0)` and `(615, 0)`.
- **Herb tile data is locked after startup:** `TileObjectData` throws `FieldAccessException("Tile data is locked and only accessible during startup.")` on writes once `readOnlyData` is set. Phase 1 does not edit herb anchors, so this does not matter now.

What this means for Phase 5 (analysis, not yet decided)
1. A tree grows from a box only if `IsTileTypeFitForTree(boxTile)` is true, and the tree's drops depend on `GetTreeType(boxTile)` (shaking). Two ways to get there: register each box as a mod tree soil through tML (`TileLoader.CanGrowModTree` and `PlantLoader.GetModTreeType`; verify the API before relying on it), or add `On_WorldGen.IsTileTypeFitForTree` and `On_WorldGen.GetTreeType` hooks that map a box to its tree type (Forest for the Daybloom, Moonglow and Blinkroot boxes and the rest, Corrupt for the Corrupt box, Crimson for the Crimson box). Both only touch `WorldGen`, so there are very few places to patch.
2. Ismail's rule that a lone box grows a tree: the neighbour rule is the one at 23350 and 23354 inside `GrowTree`; it needs the hook design to relax it for boxes. Not yet read in detail.
3. Because drawing has its own biome function, check how `GetTreeBiome` finds the tree's look before assuming a hook on `GetTreeType` is enough to make a crimson tree look crimson on a Crimson box.

Open dumps before writing Phase 5 (small; the next chat can ask for them in one probe): the bodies of `TileDrawing.GetTreeBiome` (2147) and `WorldGen.GetTreeFrame` (57949), and `WorldGen.GrowTree` from 23350 to 23360.

## 7h. Phase 5 as built (patch_harvester_trees.ps1, 2026-11-20): applied, UNTESTED
Decision (Ismail, 2026-11-20): NO tree or sapling ModTile and NO ModTree. Vanilla trees just accept the Harvester Boxes as soil through conditions added to vanilla functions. This replaces the ModTree route proposed in 7f and 7g. No IL was needed. Planting is a normal LEFT click with an acorn on the tile above the box, like an acorn on a grass block (older plan text in section 5 that says right-click planting is out of date).

Authority and sync (rule 9): the server (or single-player) does everything that changes tiles: planting, growing, felling, replanting. The vanilla tile messages carry the result (sapling: `SendTileSquare` 3x3; a felled tree: one 7x25 tile square). The only new packet is `HarvesterBoxPlantAcorn` (63, client -> server, payload int x, int y of the box, in `Common/Net/HarvesterBoxTreeNet.cs`); in single-player `FactorrariaNet.SendRequest` runs the handler right away. A `GlobalItem` on the acorn (`HarvesterBoxAcornItem.UseItem`) sends it when the cursor is on a free tile above a tree box; vanilla's own placement fails there (its soil list does not know the box), so the two never both succeed. The server checks reach, that the acorn really is in the sender's held slot, that the box is a tree box and that the two tiles above are free, then writes the sapling, takes one acorn and syncs that slot. The hooks only read tile data, so they behave the same on every side.

How it works (all in `Common/Harvester/HarvesterBoxTrees.cs`)
- Each box kind names the vanilla soil it stands in for (`HarvesterBoxKinds.TreeSoil`): Daybloom and Blinkroot grass (2), Moonglow jungle grass (60), Corrupt corruption grass (23), Crimson crimson grass (199), Shiverthorn snow block (147). Waterleaf (palm) and Fireblossom (ash) return 0 and grow nothing yet. `HarvesterBoxRegistry.BoxTreeSoil` holds tile type -> soil.
- **Disguise** (`Begin` / `End`): for the length of one vanilla call the box tile's type is the vanilla soil (same trick as `HarvesterBoxHerbHooks.RunAsVanillaPlanter`); the type and the frame are put back and the three boxes of that row are framed again. Used by the sapling check, growing, felling and the two draw hooks.
- **Hooks** (`HarvesterBoxTreeHooks`): `WorldGen.IsTileTypeFitForTree` (a box counts, so trunks and saplings keep standing), `WorldGen.Check1x2` (sapling survival and style, disguised), `WorldGen.GetCommonTreeFoliageData` (tops and branches, disguised), `TileDrawing.GetTreeVariant` (trunk look, disguised).
- **Growing**: a box with a sapling above makes an attempt every `TreeGrowTicks` (600) powered ticks through `WorldGen.AttemptToGrowTreeFromSapling`, with the box and the boxes beside it disguised (a row grows a tree with the root flare over its neighbours). Lone box: vanilla `GrowTree` needs an active soil tile beside the soil, so when neither side has one an EMPTY side cell is made an actuated vanilla soil tile for the call (passes the gate, fails the root flare test, then restored). A box wedged between two foreign blocks never grows a tree.
- **Felling**: `IsReadyToHarvest` also says yes for a grown tree (the column above the box is trunk tiles up to a top, frameX 22 and frameY >= 198). The strike fells it with two `KillTile` calls (second trunk tile, then the stump) inside the drop capture with the box disguised, so wood, acorns and bonus wood are the vanilla ones for that soil and are routed like herb drops; a new sapling is planted for free.

Why Waterleaf and Fireblossom are not in: a palm and an ash tree are different tree tiles (323 and 634) with their own growth code (`GrowPalmTree`, `GrowTreeWithSettings` with the ash profile), their own look code and their own top frames. The probes could not dump `GetPalmTreeBiome` (no match) or `GrowTreeSettings` (empty), so the ash ground rule and the palm look hooks are unknown. A small probe 7 for exactly those is the next step (5b), together with gemcorns.

Not in this patch: Waterleaf palm, Fireblossom ash, gemcorns, shaking a box tree by hand (`ShakeTree` reads the real box type), chopping a box tree by hand (wood and acorns follow the real box type, so a corruption box gives plain wood when a player chops it).

Things to test: (1) left click an acorn on the tile above a Daybloom box: a sapling appears and one acorn is used; the panel still opens with a right click as before. (2) The sapling stays (does not pop) when you place or break blocks around it. (3) A powered box grows it into a vanilla forest tree within a few minutes, with no roots when alone; a box in a row grows a tree with roots over its neighbours. (4) Moonglow, Corrupt, Crimson and Shiverthorn boxes grow jungle, corruption, crimson and snow trees (trunk and top). (5) The grown tree is felled by the staff, wood and acorns of the right kind (ebonwood, shadewood, rich mahogany, boreal wood) reach a conveyor or the floor, and a sapling is planted again. (6) The box art and the box row merging are unchanged afterwards. (7) Multiplayer: planting from a client, the sapling and tree appear for everyone, the acorn count is right, the felled tree disappears for everyone.

Fix 5 (patch_harvester_trees_debug, DEBUG ONLY, changes no behaviour): growth still throws IndexOutOfRange in KillTile_GetTreeDrops after Fix 2 and Fix 4. This patch hooks `WorldGen.CheckTree` and `WorldGen.KillTile` (in `HarvesterBoxTreeHooks`, marked TEMPORARY) and, with EnableDebugs on, writes `[HarvesterTrees]` lines to the log: the first 8 tree-tile kills of each growth with the killing CheckTree call (its tile, frame and the four tiles around it, BOX! = a box that was not disguised, ACTUATED = actuated tile), plus one `result for` line per growth attempt (grew, kills, trunk height, top frame, ready). Next: read the log, fix against it, then remove the debug block. UNTESTED.

Fix 4 (patch_harvester_trees_crowded): crowded saplings errored while a tree grew. GrowTree calls RangeFrame(i - 2 .. i + 2) and CheckTree compares the raw type under each trunk base, so a grown tree two boxes away (box not disguised) was killed and its cascade hit GetTreeDrops. Growth and felling now also disguise the boxes at x - 2 and x + 2. UNTESTED.

Fix 3 (patch_harvester_trees_fellfix2, probe 4 findings): boxes are solid-top, so the side-column disguise skipped neighbour boxes; vanilla's acorn search under a branch (KillTile_GetTreeDrops, loops down the column while the tile is not solid) then walks past the box and out of the world. A column with no ground now gets a temporary soil tile in the empty cell beside the box. CheckTree also compares the tile BELOW a root or trunk piece by raw type (2 or a tree type, else it kills the piece), so every box under a piece must be disguised while it runs. UNTESTED.

Fix 2 (patch_harvester_trees_growfix): the second trace came from vanilla's random tick (UpdateWorld_OvergroundTile -> AttemptToGrowTreeFromSapling), which grew box saplings undisguised. It is now blocked for box saplings (hook in HarvesterBoxTreeHooks); only TryGrowSapling grows them. UNTESTED. Hook assumes AttemptToGrowTreeFromSapling(int, int, bool) returns bool.

Fix after first test (patch_harvester_trees_fellfix): trees grew and fell, but IndexOutOfRange in KillTile_GetTreeDrops fired during felling and left branches floating. Cause: only the middle box was disguised; branch and root pieces read the first solid tile under THEIR column (x-1, x+1), which is not tree soil. Felling now disguises the surface tiles of both side columns too (HarvesterBoxTrees.RunAsTreeSoilWithSides). UNTESTED.

Next-chat prompt (use this one; the older text in section 8 is out of date): "Continue the Harvester Box. Clone the repo, read docs/harvester_box/ROADMAP.md sections 7h and 6. Phase 5 (trees on the boxes) is applied; here are my test results: <paste>. Growth still throws IndexOutOfRange in KillTile_GetTreeDrops; patch_harvester_trees_debug adds `[HarvesterTrees]` log lines (EnableDebugs on): <paste the KILL and result lines from a lone box, a normal row and a crowded row>. Fix growth against that log first, then remove the debug block, make the harvester handle a tree with no top, and make a manual break use the box's real wood. Fix what failed, then plan 5b: Waterleaf palm, Fireblossom ash and gemcorns, which needs probe 7 first (GetPalmTreeBiome and the palm look code, GrowTreeSettings or whatever holds the ash ground rule, the top frames of palm and ash trees)."

## 8. Next-chat prompt

"Continue the Harvester Box with Phase 5 (acorns and trees: right-click planting, the sapling survival and tree growth hooks, felling the grown tree, replanting the sapling, the tree table, gemcorns). Read `docs/harvester_box/ROADMAP.md` from a fresh clone: section 6 (Progress), 7c, 7d and 7e (the tree findings), and the \"Decisions 2026-11-17 (Phase 1 chat)\" block. Phase 3 (herb harvest, patch_harvester_harvest) and Phase 4 (staff and particles, patch_harvester_staff) test results: <PASTE what worked and what failed, or say nothing was tested yet>. If a Phase 3 or 4 test failed, fix that first. Phase 5 needs the open probe-3 dumps (`TileDrawing.GetTreeBiome` at 2147, `WorldGen.GetTreeFrame` at 57949, `WorldGen.GrowTree` 23350 to 23360, plus the Main / TileDrawing callers): <PASTE the report, or say it has not been run>; if it has not been run, write that probe first and stop. State the multiplayer design first (the server grows and fells the tree; the sapling and tree are tiles the existing sync already carries). My tModLoader.dll is at `C:\GOG Games\Terraria\tModLoader\tModLoader.dll` (pass it with `-Dll` to any probe)."