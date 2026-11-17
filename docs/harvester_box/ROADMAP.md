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

- **Deathweed trees:** Ismail wants the tree chosen at random, ebonwood (corruption) or crimson. NOTE (probe 2): vanilla has TWO Deathweed planter boxes, the Corrupt and the Crimson Planter Box, so 8 boxes not 7. OPEN: is it random for both, or Corrupt = ebonwood and Crimson = crimson tree? Ask Ismail before Phase 5 (see 7c).
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

1. **ModTile, not vanilla tile + TileEntity.** The box must be told apart from a normal planter box, so it is a ModTile like Motors (`MotorTileBase` is the template: 1x1, `HookPostPlaceMyPlayer`, `Hook_AfterPlacement`). Eight separate ModTiles from one generic base (the `MotorTileBase<TEntity>` pattern, one class per box), NOT one tile with styles: vanilla decides a tree's look and drops from the soil TILE TYPE under the trunk (see 7c), so each box needs its own tile id. Frame and merge logic copied from the vanilla planter box. `HarvesterBoxTileEntity : ElectricConsumerMachine`, no UI, no recipes.
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

- [x] Phase 0: research done. Probe 2 report received 2026-11-17 and logged in 7c. A short probe 3 (`tools\harvester_probe3.ps1`, written by the next chat) still has to confirm the planter style order and the gemcorn ids before Phase 1 code that depends on them.
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

### Open decisions for Ismail
1. Two Deathweed boxes exist. Random tree for both, or fixed (Corrupt = ebonwood, Crimson = crimson tree)? Random per planting needs more than a soil lookup (the type would have to be stored per box and read by drawing and drop code).
2. A single box in open air cannot grow a vanilla tree (side-neighbour rule). Accept (rows of boxes work) or hook the rule so a lone box works?
3. Gem trees only grow with `underground = true` in vanilla. In a box they should grow anywhere (assumed yes).
4. Blinkroot box: Forest tree (assumed).
5. Keep vanilla growth conditions (Daybloom grows by day and so on) until the custom-rules future note (assumed yes).

### Still unknown (probe 3, small, needs `-Dll "C:\GOG Games\Terraria\tModLoader\tModLoader.dll"`)
- Tile-380 style order: `Item.SetDefaults` for items 3215 to 3222 (`createTile`, `placeStyle`), and the `TileFrame` / `TileObjectData` branch for tile 380 (frames, merging).
- Gemcorn item ids and the sapling style each places (items whose `createTile` is 590, 595 or 615), plus the vanilla acorn items.
- Callers of `GetTreeType(`, `IsTileTypeFitForTree(` and `TreeTypes` in `WorldGen`, `Main` and `TileDrawing` (to size the hook work), and the full `IsTileTypeFitForTree`.
- The vanilla 1x1 platform-like draw rules needed to mimic a planter box when drawn (frames).

## 8. Next-chat prompt

"Continue the Harvester Box. Read `docs/harvester_box/ROADMAP.md` from a fresh clone, especially 7c (probe 2 findings) and the open decisions. Phase 0 research is done. Do a small probe 3 first (section 7c, 'Still unknown'), then implement Phase 1 as a patch script. My tModLoader.dll is at `C:\GOG Games\Terraria\tModLoader\tModLoader.dll` (pass it with `-Dll`; the probe 2 script's auto-search only knows Steam folders, so add `C:\GOG Games` to its search list)."