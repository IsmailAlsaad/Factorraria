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
- [~] Phase 2: three new planter boxes (patch_planter_p2, 2026-11-22, applied UNTESTED). New `PlanterBoxItemBase`, `CustomPlanterBoxes.cs` (Mushroom, Hallowed, Stone item and tile), 3 more Work Bench recipes (5 empty + 2 mud + 1 mushroom grass seeds; 5 empty + 2 dirt + 1 hallowed seeds; 5 empty + 2 stone). Growth on them is not in yet: mushrooms Phase 4, trees Phase 5.
- [~] Phase 2 tweaks (patch_planter_tweaks, 2026-11-22, applied UNTESTED): per-box break dust (`PlanterBoxDust`, `PlanterBoxGlobalTile` for the vanilla boxes by style, `PlanterBoxTileBase.DustTile`), Stone box Tink sound (`BreakSound`), and the Dryad shop puts the Empty box in the Daybloom Planter Box slot (`InsertBefore`).
- [ ] Phase 3: three Harvester variants
- [ ] Phase 4: Mushroom growth
- [ ] Phase 5: Hallowed and Stone trees
- [ ] Phase 6: spreading (probe first)
- [ ] Phase 7: polish and multiplayer test

## 6. Next-chat prompt

"Continue the planter box rework. Clone the repo, read docs/planter_boxes/ROADMAP.md (spec, Progress) and docs/harvester_box/ROADMAP.md only where needed. Phase 2 is applied; my test results are below if I give them (record them with a small patch first, do not guess). Then do Phase 3: the three Harvester Box variants (Mushroom, Hallowed, Stone)."