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
- [ ] Phase 4: Mushroom growth. Probe 1 done (report read 2026-11-22, findings in section 5b). Gaps remain (who calls GrowShroom, the random-update spawn of tile 71, PlantCheck for 71, survival of tile 72), so the next step is probe 5 (`tools\planter_probe5.ps1`), then the design and the patch.
- [ ] Phase 5: Hallowed and Stone trees
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

## 6. Next-chat prompt

"Continue the planter box rework. Clone the repo, read docs/planter_boxes/ROADMAP.md (spec, Progress) and docs/harvester_box/ROADMAP.md only where needed. Phase 3 is tested. Phase 4 probe 1 is done and its findings are in section 5b of the roadmap. Write tools\planter_probe5.ps1 (a patch with NewFile, in the style of tools\planter_probe4.ps1) for the gaps listed in 5b, search the decompile by NUMBERS (70, 71, 72) because the decompile has no TileID names, and search every decompiled type that can hold the random update, not only WorldGen. Then, once I attach the report, do the Phase 4 design and patch. Note that the Mushroom planter box and Harvester Box have no growth code yet, `HarvesterBoxKinds.TreeSoil` is 0 for Mushroom, and `HarvesterBoxRules.IsReadyToHarvest` knows only herbs and `BoxTreeSoil` trees, so the harvest side needs a mushroom case too."