# Planter box rework - roadmap

Ismail's spec (2026-11-22). Pair with `docs/harvester_box/ROADMAP.md` (the Harvester Boxes) and `PROJECT_CONTEXT.md`.

## 0. How to use this file in a new chat

1. Clone the repo, read this file's Progress section and the section of the phase you are asked to do. Read `CODEBASE_INDEX.md` only for the files you touch.
2. Verify every Find against the clone; the code wins over this file.
3. Deliver a PowerShell patch script (see `patch_template.ps1`) that includes an Edit to Progress marking the phase `[~]` (applied, untested).
4. When Ismail reports test results, record them with a small patch first, then continue.

## 1. The spec (final unless Ismail changes it)

- **Empty Planter Box**: a new item, placeable like any planter box, but nothing can be planted on it (no herb, no sapling).
- **Dryad** sells only Empty Planter Boxes. Every other planter box is removed from her shop.
- **Crafting**: every other planter box is crafted from the empty one, 5 empty + extras = 5 boxes, at a Work Bench (the Mechanical Assembler inherits Work Bench recipes). Harvester Box recipes stay at anvils (the Autohammer).

| Box | Extras (besides 5 empty) |
|---|---|
| Fireblossom | 2 Ash Block |
| Shiverthorn | 2 Snow Block |
| Waterleaf | 2 Sand Block |
| Blinkroot | 2 Dirt Block |
| Crimson (Deathweed) | 2 Crimsand, OR 2 Dirt + 1 Crimson Grass Seeds |
| Corruption (Deathweed) | 2 Ebonsand, OR 2 Dirt + 1 Corrupt Seeds |
| Moonglow | 2 Mud + 1 Jungle Grass Seeds |
| Daybloom | 2 Dirt + 1 Grass Seeds |
| **Mushroom** (new) | 2 Mud + 1 Glowing Mushroom (Mushroom Grass) Seeds. Grows glowing mushrooms occasionally and giant mushroom trees from time to time. No acorn or seed needed. |
| **Hallowed** (new) | 2 Dirt + 1 Hallowed Seeds. Acorns planted on it give pearlwood. Supports no herb. |
| **Stone** (new) | 2 Stone Block. Supports only gemcorns (all the gem trees). |

- Each of the three new boxes also gets a **Harvester Box** variant.
- **Break dust and sound** (Ismail, 2026-11-22): every plain planter box breaks with the dust of its own soil: Daybloom grass, Moonglow jungle grass, Blinkroot dirt, Waterleaf sand, Shiverthorn snow, Fireblossom ash, Corrupt and Crimson their grass, Mushroom mushroom grass, Hallowed hallowed grass, Stone stone, Empty wood. The Stone box makes the Tink sound instead of Dig. The Harvester Boxes are unchanged (silver dust, Tink).
- **Spreading**: the Corrupt, Crimson and Hallowed boxes (planter and Harvester alike) are infection SOURCES. They spread to nearby blocks AND to nearby planter boxes touching them (planter and Harvester Boxes both), converting them into the source's box.
  - Corrupt / Crimson convert: Daybloom, Waterleaf, Blinkroot, Moonglow, Shiverthorn, Mushroom, Stone.
  - Hallowed converts: Daybloom, Waterleaf, Blinkroot, Shiverthorn, Stone (and other nearby spreadable blocks).
  - Everything not listed is immune (Fireblossom, the Empty box, and between the evil boxes and the Hallowed box).

### Assumptions (change them if wrong)

- Recipes are hand-made at a Work Bench; vanilla recipes for the eight herb planter boxes are switched off so the empty box is the only way.
- The eight existing kinds are crafted into the VANILLA planter box items, so the Harvester recipes and tile rules keep working.
- A herb on a box that converts breaks like a herb that lost its soil (the existing "herb only on its own box" rule).
- Spreading rides vanilla's own tick and spread gates (`AllowedToSpreadInfections`, the Plantera rule).
- Evil boxes do not convert Hallowed boxes and the reverse. The Empty box never converts.
- The Empty box sells for 5 silver (`EmptyPlanterBoxItem.SetDefaults`, tunable).

## 2. Multiplayer design (rule 9)

| Thing | Authority | Sync |
|---|---|---|
| Dryad shop | Client | None. ModifyShop runs at load and no condition reads state. |
| Recipes | Both sides build the same list | None. |
| Herb refusal on the Empty box | Tile data every side already has (hook on `WorldGen.PlaceAlch` / `CheckAlch`) | None. |
| Mushroom growth, tree growth, harvests | Server and single-player | Tile changes via `NetMessage.SendTileSquare`; Harvester entities as today. |
| Spreading | Server and single-player (vanilla `hardUpdateWorld` only runs there) | `SendTileSquare`; a converted Harvester Box swaps tile and entity, then `HarvesterBoxRowSystem.MarkDirty` reconciles the row. |
| Sapling by right-click | Server validates | The existing acorn request packet in `Common/Net/`. |

## 3. Phases (one patch each)

- **Phase 1, empty box, shop, recipes.** `PlanterBoxTileBase`, `EmptyPlanterBoxItem` + `EmptyPlanterBoxTile`, `DryadShopGlobalNPC`, `PlanterBoxRecipes` (10 recipes), `HarvesterBoxRegistry.NoHerbBoxes` + the rule in `HarvesterBoxRules.HerbAllowedOnSoil`.
- **Phase 2, three new planter boxes.** A tile, an item and a recipe each (Mushroom, Hallowed, Stone), on `PlanterBoxTileBase` and `PlanterBoxItemBase`. Every plain custom box registers in `NoHerbBoxes` from the tile base (none of them grows a herb). `HarvesterBoxKind` is NOT touched here: its additions moved to Phase 3.
- **Phase 3, three Harvester variants.** Tile, item, entity, registration (`HarvesterBoxRegistration`), Autohammer recipes (`AutohammerRecipeRegistry.AddHarvesterRecipes`), row and power systems as they are. Add `Mushroom`, `Hallowed`, `Stone` to `HarvesterBoxKind` here. Careful: `PlanterItemName` only knows vanilla item names and falls back to Fireblossom for unknown kinds, `Herb(kind)` also falls back to Fireblossom (make it nullable), and `TreeSoil` falls back to 0.
- **Phase 4, Mushroom growth.** Glowing mushrooms (tile 71) grow occasionally, giant mushroom trees from time to time. Rules in `HerbGrowthRules` style, harvest by the Harvester variant.
- **Phase 5, trees.** Hallowed box: acorns give pearlwood (soil tile 109 in the tree table). Stone box: gemcorns only. Also make saplings work on the PLAIN custom planter boxes (today only the Harvester Box tree code handles a ModTile soil).
- **Phase 6, spreading.** Probe first (`tools\planter_probe.ps1`): decompile `WorldGen.hardUpdateWorld` (callers and gating), `SpreadInfectionToNearbyTile`, tile 380 frame logic. Then: hook `hardUpdateWorld` so the three source boxes spread to nearby blocks (`SpreadInfectionToNearbyTile`), a conversion table for touching boxes, vanilla tile 380 styles converted through `TileLoader.RegisterConversion` delegates (style = `frameY / 18`), Harvester Box conversion (tile swap + entity swap + `HarvesterBoxRowSystem.MarkDirty`), the herb above breaks.
- **Phase 7, polish and multiplayer test.** Dedicated-server pass, roadmap cleanup.

The probe (formerly Phase 0) moved to just before Phase 6, because Phases 1 to 5 need no new vanilla source.

## 4. Art needed from Ismail (14 files)

Tile sheets are 72x16: four 16x16 frames with 2 px gaps at x = 0, 18, 36, 54 (lone, left end, middle, right end). Until a PNG exists the code borrows a vanilla texture so the mod loads.

| Phase | Path | Size |
|---|---|---|
| 1 | `Content/Tiles/Farming/PlanterBoxes/EmptyPlanterBoxItem.png` | 32x32 |
| 1 | `Content/Tiles/Farming/PlanterBoxes/EmptyPlanterBoxTile.png` | 72x16 |
| 2 | `Content/Tiles/Farming/PlanterBoxes/MushroomPlanterBoxTile.png` | 72x16 |
| 2 | `Content/Tiles/Farming/PlanterBoxes/MushroomPlanterBoxItem.png` | 32x32 |
| 2 | `Content/Tiles/Farming/PlanterBoxes/HallowedPlanterBoxTile.png` | 72x16 |
| 2 | `Content/Tiles/Farming/PlanterBoxes/HallowedPlanterBoxItem.png` | 32x32 |
| 2 | `Content/Tiles/Farming/PlanterBoxes/StonePlanterBoxTile.png` | 72x16 |
| 2 | `Content/Tiles/Farming/PlanterBoxes/StonePlanterBoxItem.png` | 32x32 |
| 3 | `Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/MushroomHarvesterBoxTile.png` | 72x16 |
| 3 | same folder, `MushroomHarvesterBoxItem.png` | 32x32 |
| 3 | same folder, `HallowedHarvesterBoxTile.png` | 72x16 |
| 3 | same folder, `HallowedHarvesterBoxItem.png` | 32x32 |
| 3 | same folder, `StoneHarvesterBoxTile.png` | 72x16 |
| 3 | same folder, `StoneHarvesterBoxItem.png` | 32x32 |

Spreading needs no extra art (converted boxes reuse the Corrupt, Crimson and Hallowed sheets). Gears and staff are shared by every box.

## 5. Progress

One line per phase. `[ ]` not started, `[~]` applied untested, `[x]` tested and done.

- [x] Phase 1: empty box, Dryad shop, recipes (patch_planter_p1, 2026-11-22, applied, TESTED 2026-11-22: Ismail, everything works)
- [x] Phase 2: three new planter boxes (patch_planter_p2, 2026-11-22, applied, TESTED 2026-11-22: Ismail, all passed). New `PlanterBoxItemBase`, `CustomPlanterBoxes.cs` (Mushroom, Hallowed, Stone item and tile), 3 more Work Bench recipes (5 empty + 2 mud + 1 mushroom grass seeds; 5 empty + 2 dirt + 1 hallowed seeds; 5 empty + 2 stone). Growth on them is not in yet: mushrooms Phase 4, trees Phase 5.
- [x] Phase 2 tweaks (patch_planter_tweaks, 2026-11-22, applied, TESTED 2026-11-22: Ismail, all passed): per-box break dust (`PlanterBoxDust`, `PlanterBoxGlobalTile` for the vanilla boxes by style, `PlanterBoxTileBase.DustTile`), Stone box Tink sound (`BreakSound`), and the Dryad shop puts the Empty box in the Daybloom Planter Box slot (`InsertBefore`).
- [x] Phase 3: three Harvester variants (patch_planter_p3, 2026-11-22, applied, TESTED 2026-11-22: Ismail, all passed). `HarvesterBoxKind` gained Mushroom, Hallowed, Stone. `PlanterItemName` is now explicit per kind and answers null for the three (no Fireblossom fallback); `Herb(kind)` is `HerbStyle?` (null for the three); `HarvesterBoxRegistry.Register` puts herbless kinds in `NoHerbBoxes` and every box in the new `BoxTypes` set, which replaced `BoxHerbs` as the "is a Harvester Box" test in `HarvesterBoxRows.IsBoxTile`, `HarvesterBoxItemFall` and `HarvesterBoxTrees.ReframeBoxes`. `ResolvePlanterStyles` skips kinds without a vanilla box. `TreeSoil` stays 0 for them (Mushroom Phase 4, Hallowed and Stone Phase 5) and `PlanterDustTile` gives them their soil dust. New tile, item, entity and `RegisterVisual` each, plus an Autohammer recipe each (30 of the matching custom planter box + Staff of Regrowth + 12 Cog + 5 Simple Circuit). Harvester Boxes keep silver dust and Tink. They plant nothing and harvest nothing yet.
- [x] Phase 4: Mushroom growth (patch_planter_p4, 2026-11-22, applied, TESTED 2026-11-22: Ismail, everything passed). Probes 1 and 5 done (findings in sections 5b and 5c). Glowing mushrooms and giant mushroom trees grow on the plain Mushroom box and the Mushroom Harvester Box from vanilla's random update (`MushroomGrowthRules`, `MushroomBoxGlobalTile`); `PlantCheck` is hooked so a mushroom survives on a box; the Mushroom box is now in `BoxTreeSoil` (soil 70), so the tree disguise code covers it; no sapling or acorn on mushroom soil (`CanPlantSapling`); the Harvester harvests mushrooms (`IsReadyToHarvest`, `HarvestMushroom`) and fells mushroom trees without replanting. Design and test list in section 5c.
- [x] Phase 5a: Hallowed trees (patch_planter_p5a, 2026-11-22, applied, TESTED 2026-11-22: Ismail, all passed). `HarvesterBoxKind.Hallowed` and `HallowedPlanterBoxTile` now name soil 109 (`TreeSoil`), and `SaplingStyle(109)` is 5, so the plain Hallowed box and the Hallowed Harvester Box take acorns, grow hallowed trees (pearlwood) and the Harvester fells and replants them, all through the existing `HarvesterBoxTrees` code. No new hooks. Design and test list in section 5e.
- [x] Phase 5b: Stone box gemcorns (patch_planter_p5b, 2026-11-22, applied, TESTED 2026-11-22: Ismail, all passed, including the gem sapling surviving on the box through the TileFrame hook). Probe 6 done (findings in section 5g). The Stone box and the Stone Harvester Box stand in for stone (soil 1, passes `GemTreeGroundTest`), take ONLY gemcorns (never acorns), grow gem trees with vanilla's rules (underground only, Ismail's decision 2026-11-22) and the Harvester fells them and replants the same gem. New: gemcorn planting (`HarvesterBoxAcornItem`, `HarvesterBoxPlantAcorn` handler, `PlaceSapling(..., 590)`), `IsTreeTileType` knows tiles 583 to 589, `SideFits` for stone, `GetGemTreeFoliageData` and `TileFrame` hooks. Design and test list in section 5g.
- [ ] Phase 6: spreading (probe first)
- [ ] Phase 7: polish and multiplayer test

## 5b. Phase 4 probe 1 findings (tools\planter_probe4_report.txt, tML 2026.08.3.0, decompiled WorldGen uses raw tile ids)

Ids: 70 = mushroom grass (the soil), 71 = glowing mushroom plant, 72 = giant mushroom tree stem. Items: 183 = Glowing Mushroom, 194 = Mushroom Grass Seeds. The report found NO text "MushroomPlants" or "Mushroom" in WorldGen/TileDrawing source, so greps must use the numbers.

- **`GrowShroom(i, y)`** (the whole giant mushroom tree, `y` = the SOIL row): refuses if the tiles at (i-1, y-1), (i+1, y-1) are lava. Needs tile (i, y) solid and type 70 with no half brick or slope, `wall == 0` at (i, y-1), and ACTIVE type-70 tiles at (i-1, y) and (i+1, y) (so a lone box can never grow one, it needs a box on each side), plus `EmptyTileCheck(i-2, i+2, y-13, y-3, 71)` and `EmptyTileCheck(i-1, i+1, y-3, y-1, 71)` (existing tile 71 plants do not block). It then writes stem tiles (type 72) from y-n to y-1 with n = `genRand.Next(4, 11)` (4 to 10), stem frameX 0 and frameY random of 0/18/36, top tile frameX 36 and frameY random of 0/18/36, `frameNumber` random 0..2, calls `RangeFrame(i-2, y-n-1, i+2, y+1)` and on a server `NetMessage.SendTileSquare(-1, i-1, y-n, 3, n)`. It returns true when it grew. NOTE: it contains no growth chance and no caller inside WorldGen; its caller is somewhere the probe did not look.
- **Tree type**: `GetTreeType(70)` = `TreeTypes.Mushroom`, so any soil disguised as tile 70 reads as a mushroom tree; `IsThisAMushroomTree(i, j)` goes through `GetTreeBottom`. `ShakeTree` has a mushroom branch (see probe 10 report, `genRand.Next(8) == 0 && treeType == TreeTypes.Mushroom`).
- **Drops**: tiles 71 AND 72 share one rule (WorldGen.cs ~60676): 1 in 40 gives item 194 (Mushroom Grass Seeds), else 1 in 2 gives item 183 (Glowing Mushroom), else nothing. Per tile broken, so each stem tile rolls separately.
- **Survival of the plant**: `PlantCheck(i, j)` is called for tile 71 (same case list as 3, 24, 61, 73, 74, 110, 113, 201, 637, WorldGen.cs ~76714). Its body, so which soil it accepts for 71, is NOT in the report yet.
- **Look**: TileDrawing has no "Mushroom" text, so the stem looks come from the tile frames, not from the soil: no disguise is needed for drawing.
- **Not found yet** (probe 5): who calls `GrowShroom`, what spawns tile 71 on mushroom grass in the random update, the `PlantCheck` body for 71, how tile 72 survives (TileFrame / `CheckTree`-like code for 72, whether breaking a stem tile drops the ones above), and whether the harvest of a stem should fell it as one.

## 5c. Phase 4 probe 5 findings and design (tools\planter_probe5_report.txt, 2026-11-22; whole assembly decompiled, 1907 files)

Findings (they correct section 5b, which assumed the giant mushroom is tile 72):
- `GrowShroom` has NO caller in any decompiled type. It is dead code, and tile 72 is never grown by vanilla's update. Giant mushroom trees are ordinary tile 5 trees of tree type Mushroom (`GetTreeType(70)`; the tree drawing asks `IsThisAMushroomTree`), grown by `GrowTree`. Tile 72 and `GrowShroom` are NOT used by this mod.
- The random update, `UpdateWorld_GrassGrowth` `case 70` (mushroom grass): if the block is not actuated off, and the tile above (j - 1) is not active, 1 in 10 per random update places tile 71 there (`PlaceTile(i, j - 1, 71, mute: true)`, copies paint, `SendTileSquare` on a server). Then 1 in 300 calls `GrowTree(i, j)` (`j` = the SOIL row) and, if a player has line of sight, `TreeGrowFXCheck(i, j - 1)`. The same case also spreads mushroom grass over mud (not copied: the box does not spread).
- `TileLoader.RandomUpdate` runs at the start of both `UpdateWorld_OvergroundTile` and `UpdateWorld_UndergroundTile`, for every tile the random update picks, so `ModTile.RandomUpdate` and `GlobalTile.RandomUpdate(i, j, type)` give a box exactly the sampling of a natural mushroom grass block.
- `PlantCheck(x, y)` lets a tile 71 stand only on soil 70 (`num2 != 71 || num == 70`), otherwise it ends in `KillTile`. `TileFrame` calls it for 71.
- `TileFrame` case 72 (stem) kills the stem unless the tile under it is 72 or 70. Irrelevant now.
- `GrowTree` needs the soil to pass `IsTileTypeFitForTree`, then for soil 70 `EmptyTileCheck(i - 2, i + 2, j - n, j - 3, 20)` and `EmptyTileCheck(i - 1, i + 1, j - 2, j - 1, 20)` (n = 4 + a random 5 to 16). A glowing mushroom in that footprint blocks a tree.
- NOT in the report (inferred, to be confirmed in game): the part of `GrowTree` that builds the tree for soil 70. Everything points at a tile 5 tree, and the Harvester tree code already handles tile 5 trees on a disguised soil.

Design (what patch_planter_p4 does):
- Clock: `MushroomBoxGlobalTile.RandomUpdate` -> `MushroomGrowthRules.Step(x, y)` for the plain Mushroom box and the Mushroom Harvester Box (both are in `BoxTreeSoil` with soil 70).
- Glowing mushroom: `MushroomChance` (0.5 per random update) when the tile above is empty; `PlaceTile` runs through `HarvesterBoxTrees.RunAsTreeSoil` so vanilla sees mushroom grass.
- Tree: `TreeChance` (0.025 per random update) -> `HarvesterBoxTrees.TryGrowMushroomTree` = `TryGrowSapling(..., mushroom: true)`: the same disguise, neighbour and lone-box fake side as a sapling, but `WorldGen.GrowTree(x, soilY)` and no sapling needed. If it fails and glowing mushrooms stand within 2 columns, they are pushed aside (no drops) and it is tried once more.
- Survival: `On_WorldGen.PlantCheck` hook disguises the box for one call when a tile 71 stands on it. Trees already survive through the existing `CheckTree`/`IsTileTypeFitForTree` hooks.
- No acorn: `CanPlantSapling` refuses soil 70 (so acorns, and the replant after a felling, do nothing on a mushroom box).
- Harvest: `IsReadyToHarvest` says a tile 71 above a Mushroom Harvester Box is ripe at once; `HarvestMushroom` breaks it through the drop capture (vanilla yield) and plants nothing; a ripe tree is felled by the existing `FellTree` (no sapling afterwards).
- Multiplayer: server and single-player only (the random update never runs on a client). A new mushroom goes out with `SendTileSquare`; `GrowTree` and the felling send their own tiles. The `PlantCheck` hook reads tile data, so clients agree. No new packet.
- Left out on purpose: the paint copy of a new mushroom, and `TreeGrowFXCheck` (the growth sparkle), because their signatures were not verified.

Test results for Phase 4 (Ismail, 2026-11-22): everything passed.

## 5e. Phase 5a design (Hallowed trees) and what is known for 5b (gemcorns)

Facts, from the vanilla dumps in `tools\harvester_probe2_report.txt`, `harvester_probe3_report.txt` and `harvester_probe4_report.txt`:
- Hallowed soil is tile 109 (and 492, the second hallowed grass). `IsTileTypeFitForTree` says yes for both. `Check1x2` (sapling survival and style) gives 109 and 492 style 5. `AttemptToGrowTreeFromSapling` case 20 with style 5 falls into its default branch, `GrowTree` (not underground). `GetTreeType` gives Hallowed for 109 and 492, and `KillTile_GetTreeDrops` drops item 621 (Pearlwood) for them.
- So a hallowed tree is an ordinary tile 5 tree, exactly like the forest, jungle, corruption, crimson and snow trees the Harvester tree code already handles. The only per-soil data this mod keeps is `HarvesterBoxKinds.TreeSoil` and `HarvesterBoxKinds.SaplingStyle`.

What patch_planter_p5a does:
- `HarvesterBoxKinds.TreeSoil(Hallowed)` = hallowed grass (109); `HarvesterBoxKinds.SaplingStyle(109)` = 5; `HallowedPlanterBoxTile.TreeSoil` = hallowed grass. `HarvesterBoxRegistry.Register` and `PlanterBoxTileBase.SetStaticDefaults` then put both boxes into `BoxTreeSoil`, which is all the disguise, the hooks, the acorn packet (`HarvesterBoxTreeNet`), the harvest and the felling need. Saplings on the plain custom boxes were already generic since Phase 4 (`BoxTreeSoil` is filled by the tile base), so no extra code was needed for them.
- No new hooks, no new packet. Multiplayer as in Phase 5 of the Harvester Box (section 7h of docs/harvester_box/ROADMAP.md).

Known for 5b (Stone box, gemcorns), NOT yet verified in code:
- Gemcorns are items 4851 to 4857 (Topaz, Amethyst, Sapphire, Emerald, Ruby, Diamond, Amber), each `DefaultToPlaceableTile(590, style 0 to 6)`. Tile 590 is the gem sapling; `AttemptToGrowTreeFromSapling` case 590 returns false unless `underground` is true, then grows tile 583 to 589 (style 0 to 6) through `GrowTreeWithSettings` with the gem profile (`GroundTest = GemTreeGroundTest`, `WallTest = GemTreeWallTest`, height 7 to 12, top padding 4). The bodies of the two tests are not in any report: probe 6 dumps them.
- `GrowTreeWithSettings` needs the soil to pass the ground test, the same test on a tile beside it, and an empty 5-wide, (height + 4)-tall column; it does not read `IsTileTypeFitForTree`.
- Open design question for Ismail (5b): vanilla gem saplings only grow underground (below the surface). Decision 3 of the Harvester Box says to reuse vanilla rules, so a Stone box on the surface would never grow a gem tree. Keep that, or let the Stone box grow gem trees anywhere?
- Planned shape of 5b (after the probe): the Stone box stands in for stone, the acorn item hook also takes the seven gemcorns (the Stone box takes ONLY gemcorns, every other tree box only acorns), `PlaceSapling` writes tile 590 with the gemcorn's style, `IsTreeTileType` learns tiles 583 to 589, and the felling and the drops are checked against the probe report.

Test results for Phase 5a (Ismail, 2026-11-22): all passed.

## 5g. Phase 5b probe 6 findings and design (tools\planter_probe6_report.txt, 2026-11-22)

Findings:
- `GemTreeGroundTest(tileType)`: true for `TileID.Sets.Conversion.Stone` and `Conversion.Moss` tiles. So a Stone box standing in for stone (1) passes, and so does a neighbouring Stone box. `AshTreeGroundTest` is ash grass (633) only.
- `GemTreeWallTest(wall)`: `DefaultTreeWallTest` (`WallID.Sets.AllowsPlantsToGrow`) or one of the natural walls 2, 54 to 59, 61, 185, 196 to 199, 208 to 215. The wall at the tile above the soil must pass. Vanilla rule, kept.
- Underground only: `AttemptToGrowTreeFromSapling` case 590 returns false unless `underground`; `UpdateWorld_UndergroundTile` calls it for tile 590 (1 in 5 per random update, `underground: true`) and `UpdateWorld_OvergroundTile` never does. Ismail decided (2026-11-22): the Stone box follows the vanilla rule, so it grows gem trees only underground.
- Gem profiles (`GrowTreeWithSettings`): height 7 to 12, top padding 4, tree tiles 583 (Topaz), 584 Amethyst, 585 Sapphire, 586 Emerald, 587 Ruby, 588 Diamond, 589 Amber, sapling tile 590; the sapling's style (frameX / 54, 0 to 6) picks the tree. `GrowTreeWithSettings` also needs a tile beside the soil that passes the ground test.
- Gem tree tiles are drawn through `WorldGen.GetGemTreeFoliageData` (tops and branches), and TileDrawing adds a tree special point for frameY >= 198 and frameX >= 22 like tile 5, so a top frame is the same test as for the other trees.
- NOT in the report: which function keeps a gem sapling (590) standing. `TileFrame` skips 590 in its object switch, and `Check1x2`'s soil switch (which handles tile 20) has no stone case. Also not shown: what `KillTile_GetItemDrops` gives for tiles 583 to 589 (it has 11 hits) and the body of `GetGemTreeFoliageData`. The patch is written so that these do not matter (below), to be confirmed in game.

Design (what patch_planter_p5b does):
- `HarvesterBoxKinds.TreeSoil(Stone)` and `StonePlanterBoxTile.TreeSoil` = stone (1), so both Stone boxes enter `BoxTreeSoil`. Everything else (disguise, hooks, harvest, felling) is the existing tree code.
- Items: the seven gemcorns (4851 to 4857, `IsGemcorn`) are handled by `HarvesterBoxAcornItem` exactly like acorns. `CanPlantSapling(x, y, gemcorn)`: the Stone box takes ONLY gemcorns, every other tree box only acorns, the Mushroom box nothing. The server writes tile 590 with style = gemcorn index (`PlaceSapling(..., 590)`). Same packet, no new one.
- Growth: `TryGrowSapling` accepts a gem sapling (`IsSapling`), `SideFits` uses `GemTreeGroundTest` for stone, and vanilla's random update passes `underground` through the existing `AttemptToGrowTreeFromSapling` hook. A lone box gets the same fake side cell as for ash.
- Survival: a new `On_WorldGen.TileFrame` hook disguises the box as stone for the whole `TileFrame` call of a gem sapling tile, so whatever support check runs reads stone. `CheckTreeWithSettings` (already hooked for ash) covers the grown trunk, and the new `GetGemTreeFoliageData` hook covers tops and branches.
- Harvest: `IsTreeTileType` knows 583 to 589, so `IsTreeReady`, the harvest and the felling (`FellTree`, with the shake and drop capture) treat a gem tree like any other; the replant uses the felled tree's own gem style (`treeTile - 583`).
- Multiplayer as in Phase 5: server and single-player grow and fell, tile squares carry the result, the existing `HarvesterBoxPlantAcorn` packet carries the planting (the server reads the held item, so a client cannot plant what it does not hold).

Test results for Phase 5b (Ismail, 2026-11-22): all passed. The `TileFrame` hook for gem saplings was the main risk (the probe did not show which function checks a gem sapling's soil); it works, so it stays.

## 5h. Next-chat prompt (current, after Phase 5b)

"Continue the planter box rework (Factorraria). Clone the repo and read docs/planter_boxes/ROADMAP.md (spec, Progress, sections 5e to 5h, section 3 Phase 6). Phases 4, 5a (Hallowed trees) and 5b (Stone box gemcorns) are tested and done (all passed, recorded in Progress). Start Phase 6 (spreading) straight away: write the probe first (tools\planter_probe7.ps1: `WorldGen.hardUpdateWorld` callers and gating, `SpreadInfectionToNearbyTile`, the infection and purity conversion code, tile 380 frame logic, `TileLoader.RegisterConversion`), and ask me to run it."

## 5f. Next-chat prompt (OUTDATED: written after Phase 5a, use section 5h)

"Continue the planter box rework (Factorraria). Clone the repo and read docs/planter_boxes/ROADMAP.md (spec, Progress, sections 5c to 5f, section 3 Phase 5). Phase 4 (mushroom growth) is tested and done. Phase 5a (patch_planter_p5a, Hallowed trees) is applied; its test results are: <paste>. Record them first with a small patch. I ran tools\planter_probe6.ps1 and attach tools\planter_probe6_report.txt. Then do Phase 5b (the Stone box and gemcorns): read section 5e for what is known, reuse `HarvesterBoxTrees` (`BoxTreeSoil`, the disguise, `TryGrowSapling`), and my answer to the open question about growing gem trees on the surface: <answer>."

## 5d. Next-chat prompt (OUTDATED: written after Phase 4, use section 5f)

"Continue the planter box rework (Factorraria). Clone the repo and read docs/planter_boxes/ROADMAP.md (spec, Progress, sections 5b to 5d, section 3 Phase 5). Phase 4 (patch_planter_p4, mushroom growth) is applied; its test results are: <paste>. Record them first with a small patch, then do Phase 5 (Hallowed and Stone trees, saplings on the plain custom boxes): reuse `HarvesterBoxTrees` (`BoxTreeSoil`, the disguise, `TryGrowSapling`) the way Phase 4 did for the Mushroom box, and note `PlanterBoxTileBase.TreeSoil` is the hook for a plain box.\"

## 6. Next-chat prompt (OUTDATED: written before probe 5, use section 5d)

"Continue the planter box rework. Clone the repo, read docs/planter_boxes/ROADMAP.md (spec, Progress) and docs/harvester_box/ROADMAP.md only where needed. Phase 3 is tested. Phase 4 probe 1 is done and its findings are in section 5b of the roadmap. Write tools\planter_probe5.ps1 (a patch with NewFile, in the style of tools\planter_probe4.ps1) for the gaps listed in 5b, search the decompile by NUMBERS (70, 71, 72) because the decompile has no TileID names, and search every decompiled type that can hold the random update, not only WorldGen. Then, once I attach the report, do the Phase 4 design and patch. Note that the Mushroom planter box and Harvester Box have no growth code yet, `HarvesterBoxKinds.TreeSoil` is 0 for Mushroom, and `HarvesterBoxRules.IsReadyToHarvest` knows only herbs and `BoxTreeSoil` trees, so the harvest side needs a mushroom case too."