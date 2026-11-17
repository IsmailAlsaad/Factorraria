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

- **Deathweed box:** the tree is chosen at random on each planting, ebonwood (corruption) or crimson, 50/50.
- **Herbs only grow on their own box.** This applies to the Harvester Boxes and to the vanilla planter boxes, which are reworked to match (a herb survives and grows only on the planter box of its own herb).
- **Gem acorns (gemcorns) work too.** Assumption: a gemcorn works in any Harvester Box and the item decides the tree (the matching gem tree). Phase 0 confirms the item and tree names.
- **Future note, not now:** custom growth rules such as a light level requirement. Keep the growth check in one function (`HarvesterBoxRules`) so rules can be added there later.

### Assumptions (change them if wrong)

- A herb is harvested at the Mature and Blooming stages, with the vanilla yield for the stage it was found at.
- Replanting is free.
- Gears mesh with any adjacent Harvester Box, whatever its type.
- "Nearby" means about one screen from any player.
- The box has no inventory: drops appear at the box (real items) or become vItems (conveyors can pick them up).
- Each box needs its own custom wire on its tile to join a power grid (machines bridge wire types, adjacent boxes do not conduct to each other). Bridging along a row can be added later.

## 2. Design decisions

1. **ModTile, not vanilla tile + TileEntity.** The box must be told apart from a normal planter box, so it is a ModTile like Motors (`MotorTileBase` is the template: 1x1, `HookPostPlaceMyPlayer`, `Hook_AfterPlacement`). One tile with 7 styles, frame and merge logic copied from the vanilla planter box. `HarvesterBoxTileEntity : ElectricConsumerMachine`, no UI, no recipes.
2. **Plants must live on a ModTile.** Vanilla herbs and saplings check the tile below (tML has `AnchorType.PlanterBox`, `PlanterBox` only means vanilla tile 380). Phase 1 proves the hooks. Fallback if hooks are not enough: `On_` hooks on the herb and sapling checks.
3. **Acorns:** right-clicking the box with an acorn consumes it and plants a sapling of the box's tree type. Growth is driven by the box on a timer (vanilla's tree-growing function), not by random world ticks. The sapling survival check must accept the box.
4. **Drops use a capture scope.** `RoboticArmDropCaptureSystem` / `RoboticArmDropCapture` (in the arm folder) already catches the items a tile break or tree fell spawns (`EntitySource_TileBreak`, `EntitySource_ShakeTree`) during a scoped call and hands them to the arm, which turns them into vItems (`RouteDrop`, `VirtualItemSystem.SpawnVirtualItem`). Phase 3 generalises that into a shared capture (an interface the arm and the box both implement) instead of copying it. Vanilla yields stay exact.
5. **Near or culled:** `BaseMachine.IsAnyPlayerNear` and `IsOnScreen` decide. Near = real items. Culled = skip the animation and particles, run the cycle on a timer, drops become vItems.
6. **Server-authoritative cycle:** `Idle -> Ready -> Extending -> Sweeping -> Retracting -> Idle`.

## 3. Multiplayer design (rule 9)

| Thing | Authority | Sync |
|---|---|---|
| Cycle state machine, harvest, replant, tree growth timer, drops | Server and single-player | One small TE sync per state change (state + start tick) |
| Acorn right-click | Server validates | Request packet in `Common/Net/`; single-player runs the handler directly |
| Gears, staff, particles, sprite swap | Client only, never saved | Derived from tiles the client already has, synced `isOn`, and the state sync |
| Gear meshing | Client | One shared server-synced gear clock: angle = direction x clock + parity offset. Unpowered gears freeze and ease back in. The staff strike is timed from the same clock so the tip crosses the plant when the server harvests. |

## 4. Phases (one patch each)

- **Phase 0, research and spike.** Log this roadmap. Read the repo patterns (done, section 5). Dump the vanilla source for herbs, saplings, trees, planter boxes and gemcorns with `tools\harvester_probe.ps1` and report discrepancies. Finalise the tree table.
- **Phase 1, tile, item, power.** ModTile with 7 styles, frame/merge, item, crafting recipe (placeholder ingredients, Ismail decides), powered TileEntity, registration in `MachineRegisterationSystem` (UI states, visuals, `Unload`), herbs plant and grow on the box, herb-only-on-own-box rule for Harvester Boxes and vanilla planter boxes. Needs the 7 tile sheets from Ismail.
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

- [~] Phase 0: roadmap logged, probe script delivered. Waiting for Ismail to run `tools\harvester_probe.ps1` and share `tools\harvester_probe_report.txt` (delete both outputs from commits afterwards).
- [ ] Phase 1: tile, item, power, herb-on-own-box rules
- [ ] Phase 2: gear visuals
- [ ] Phase 3: herb harvest and drops
- [ ] Phase 4: staff, particles, client sync
- [ ] Phase 5: acorns, trees, gemcorns
- [ ] Phase 6: polish, MP verification

## 7. Verify (resolved by the probe, then recorded here)

- Vanilla herb check: the tile types and the planter style each herb requires (`CheckAlch`, `PlaceAlch`, herb growth). Planter box style order.
- How `TileObjectData` anchors herbs and saplings (`AnchorType.PlanterBox`).
- Sapling survival and tree growth (`CheckSapling`, `GrowTree`, `TryGrowingTreeByType`, `GetTreeType`), sapling styles per tree.
- Gemcorn item names, the sapling they place, the tree they grow.
- Herb drop rules by stage (and what `IsHarvestableHerbWithSeed` says).
- Callers of drops tolerate the capture approach for herbs (the arm only proved trees and blocks).

## 8. Next-chat prompt

"Continue the Harvester Box. Read `docs/harvester_box/ROADMAP.md` from a fresh clone. Phase 0 is applied; here is the probe report: <paste or upload `tools\harvester_probe_report.txt`>. Finalise section 7 and implement Phase 1 as a patch script."