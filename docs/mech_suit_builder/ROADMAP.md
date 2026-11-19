# Mech Suit build tool roadmap and context handoff

Home: `docs/mech_suit_builder/ROADMAP.md`. Written 2026-11-19 by the Phase 0 patch. The code wins wherever this file disagrees with it.

## 0. How to use this file in a new chat

1. Clone the repo, read this file, then read only what the requested phase names (use `CODEBASE_INDEX.md`). Do not load whole files you do not need.
2. Check the Progress section (section 9). Do not start a phase whose predecessor is not `[x]`, or at least `[~]` with Ismail's test report pasted in.
3. Follow `PROJECT_CONTEXT.md` (patch scripts, no `const`/`static` unless forced, reuse helpers, multiplayer first, Ismail makes all art and you tell him exact paths and sizes).
4. One chat = one phase. Every patch edits the Progress section (`[~]` applied untested) and ends with the next-chat prompt (section 10) updated.
5. Multiplayer code goes in `Common/Net/`. A phase that adds netcode also edits `docs/multiplayer/ROADMAP.md` and `SURVEY_APPENDIX.md` (rule 10).

## 1. What it is (Ismail's spec, final unless he changes it)

The Mech Suit becomes a build tool: select a region, deploy it elsewhere as a hologram, and the suit's arms build it from the player's inventory.

- 2 arms stay anchored to blocks. The other 2 fetch blocks from the player's inventory and place them (same style as the Robotic Arm placing blocks). When switching block type, the arms retract to the player to "grab" the next block, then continue.
- Shift + right-click drag selects an area and saves it to be copied. The selection area UI stays showing.
- Holding Shift shows a placement hologram of the selected area plus the box to be placed in that area.
- Shift + left click places that hologram and the suit starts building from materials in the player inventory.
- When a material runs out, its hologram turns red.
- A hologram block that collides with an already placed block also appears red and is ignored until the underlying block is cleared.

### Decisions 2026-11-19

1. **No build order.** The builder arms just place the closest valid tile. A tile whose placement condition is not valid yet (it needs support) is not placed until the condition becomes valid, because the arms placed other tiles around it.
2. **Art:** the selection area, the hologram box and the valid/red states use `RoboticArmArea.png` (valid) and `RoboticArmAreaInvalid.png` (red), both in `Content/Tiles/Machines/ElectricalConsumers/RoboticArm/`, same 4x4 cell layout as the arm's working area (see `RoboticArmAreaEditorSystem`). No new art for the selection.
3. **Chests, machines and container objects are NOT skipped.** They are copied and placed like any other object. What happens to their contents and settings is an open item (section 8).
4. **Arm count:** `MaxArms` stays 3 while the suit is not building. While a build job runs it is 4, and only 2 of them anchor the player; the other 2 are the builder arms.
5. Confirmed assumptions: the cursor is the centre of the hologram; no rotation or flipping; everything only works while the player is in the suit; Esc cancels a running job; Shift + right-click without a drag clears the selection.

### Assumptions (change them if wrong)

- Tiles with no placeable item (for example grass) are skipped and not drawn in the hologram.
- Selection size cap is 64 x 64 tiles (tunable fields `MaxSelectionWidth` / `MaxSelectionHeight` in `MechSuitBuilderSystem`).
- A builder arm reaches about 7 tiles from the player (the Robotic Arm's `ReachTiles`). Tiles farther away wait until the player flies closer.
- Phase 1 to 4 copy tiles, walls, slopes and paint. Objects (including containers and machines) arrive in Phase 5.

## 2. Existing code this builds on

- `MechSuitPlayer` (`Content/Items/Mounts/MechSuitItem.cs`): `IsActive`, `MaxArms` (3), the anchored arm slots, `MechSuitArmProjectile`. The suit state is already synced (`Common/Net/MechSuitNet.cs`, packets `MechSuitStateRequest` / `MechSuitState`).
- `RoboticArmMotion` / `RoboticArmVisual`: plain classes (no tile entity needed) that drive and draw one arm: `SetTarget`, `StartStrike`, `InPosition`, IK. The builder arms reuse them with the player's shoulder as the pivot.
- `RoboticArmWorker`: tile, wall and object placement (`TryPlace`, `PlaceObject`, `PlaceWall`, `CanPlaceAt`, `SendTileSquare`). Phase 3 extracts what it needs into a shared helper instead of copying it.
- `RoboticArmNet`: nearby-player broadcast of arm state, tile actions and place sounds. The builder arms follow the same pattern.
- `RoboticArmAreaEditorSystem`: drag-select input pattern and the area sheet. `Slot(index, count)` is now public and shared.

## 3. Multiplayer design (rule 9)

| Thing | Authority | Sync |
|---|---|---|
| Selection rectangle | local client only | none (never saved) |
| Hologram, red states | local client only | none; computed from the local inventory and local tiles |
| Build job | server | started by one request packet (source rect, destination origin); the server reads the source region from the world itself, so no blueprint travels |
| Material use | server | the server checks and consumes the owner's inventory |
| Tile placement | server | `SendTileSquare`, same as the Robotic Arm |
| Builder arm visuals | server decides, clients draw | nearby-player state like `RoboticArmNet` (target tile, held item) |
| Cancel | client requests, server ends the job | one packet |

Single-player runs the request handler directly. The job belongs to one player and ends if that player leaves the suit, dies or disconnects.

## 4. Build rules

- Each build tick the server picks the closest tile of the job that is within a builder arm's reach, has the material in the owner's inventory, is not blocked by an existing block, and passes the placement check (`CanPlaceAt`). A tile that fails only the support check stays pending and is retried later.
- The job ends when no tile is placeable and everything left is blocked, unsupported, out of material or out of reach. The suit reports what is left.
- Switching the material the arm is carrying means the arm retracts to the player (`SetTarget` on the player, wait for `InPosition`), takes the next item, then goes back out.

## 5. Patches

| Patch | What | Netcode |
|---|---|---|
| M1 | Selection: Shift + right-click drag, size cap, saved region, area sheet overlay, no size text (`MechSuitBuilderSystem`) | none |
| M2 | Hologram of the saved region at the cursor, the placement box, red states (collision, missing material), Shift + left click sends nothing yet | none |
| M3 | Shared placement helper extracted from `RoboticArmWorker`; server build job; two builder arms (visuals, retract and grab); `MaxArms` 3 / 4 and 2 anchors while building | start, cancel, arm state |
| M4 | Esc cancel, job end report, progress and remaining-material readout, multiplayer pass | cancel packet |
| M5 | Objects, containers and machines, paint and slopes polish | none new |

## 6. Test checklists

M1: not in the suit, Shift + right-click does nothing special. In the suit, hold Shift and right-click drag: the area sheet appears, a drag past 64 stops growing. Release: the selection stays. A new drag replaces it. Shift + right-click without moving clears it. Releasing Shift mid-drag keeps the old selection. Holding Shift does not swing or place the held item. Leaving the world clears it. Taking the suit off hides it, getting back in shows it again.

## 7. Art

None needed for M1 and M2 (they reuse `RoboticArmArea.png` and `RoboticArmAreaInvalid.png`). The builder arms (M3) reuse the existing suit arm art unless Ismail draws new ones.

## 8. Open items (defaults used until Ismail decides)

- DECIDED 2026-11-19: chests, machines and containers are placed EMPTY. Their contents and settings (recipe, area, filters) are not copied, so items are never duplicated.
- DECIDED 2026-11-19: the selection shows no size text.
- What the readout looks like during a build (M4).

## 9. Progress

- [x] M1 Selection (patch_mech_builder_m1_selection.ps1, tested 2026-11-19, all checks passed; patch_mech_builder_m1b_no_readout.ps1 then removed the "W x H" text because Ismail did not want it)
- [ ] M2 Hologram
- [ ] M3 Server build job and builder arms
- [ ] M4 Cancel, report, multiplayer pass
- [ ] M5 Objects, containers, machines

## 10. Next-chat prompt

"Implement Mech Suit build tool patch M2 (hologram). Clone the repo, read docs/mech_suit_builder/ROADMAP.md (sections 1, 3, 5, 9) and PROJECT_CONTEXT.md. M1 is tested and done (2026-11-19)."