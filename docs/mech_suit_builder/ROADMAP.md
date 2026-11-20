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
- Shift + right-click drag selects an area and saves it to be copied. The drag rectangle shows while dragging; once the drag ends the saved selection is no longer drawn (it stays saved). DESIGN CHANGE 2026-11-20 (Ismail): only the placement hologram stays on screen.
- Holding Shift shows a placement hologram of the selected area plus the box to be placed in that area. With a saved selection this hologram and its box are the only thing drawn: the source area is not shown, and the hologram is hidden while a new selection is being dragged.
- Shift + left click places that hologram and the suit starts building from materials in the player inventory.
- When a material runs out, its hologram turns red.
- A hologram block that collides with an already placed block also appears red and is ignored until the underlying block is cleared.

### Decisions 2026-11-19

1. **No build order.** The builder arms just place the closest valid tile. A tile whose placement condition is not valid yet (it needs support) is not placed until the condition becomes valid, because the arms placed other tiles around it.
2. **Art:** the selection area, the hologram box and the valid/red states use `RoboticArmArea.png` (valid) and `RoboticArmAreaInvalid.png` (red), both in `Content/Tiles/Machines/ElectricalConsumers/RoboticArm/`, same 4x4 cell layout as the arm's working area (see `RoboticArmAreaEditorSystem`). No new art for the selection.
3. **Chests, machines and container objects are NOT skipped.** They are copied and placed like any other object. What happens to their contents and settings is an open item (section 8).
4. **Arm count:** `MaxArms` stays 3 while the suit is not building. While a build job runs it is 4, and only 2 of them anchor the player; the other 2 are the builder arms.
6. **Design change 2026-11-20 (Ismail, applied in patch M3):** the area you copied from no longer stays on screen. The drag rectangle still shows while dragging, the selection stays saved, and while Shift is held in the suit only the placement hologram and its box show (what M2 draws). While dragging a new selection the old source area is not shown either, and the hologram is hidden (open question for Ismail: should the hologram stay visible while dragging? Default: hidden).
5. Confirmed assumptions: the cursor is the centre of the hologram; no rotation or flipping; everything only works while the player is in the suit; Esc cancels a running job; Shift + right-click without a drag clears the selection.

### Assumptions (change them if wrong)

- Tiles with no placeable item (for example grass) are skipped and not drawn in the hologram.
- Selection size cap is 64 x 64 tiles (tunable fields `MaxSelectionWidth` / `MaxSelectionHeight` in `MechSuitBuilderSystem`).
- A builder arm reaches about 7 tiles from the player (the Robotic Arm's `ReachTiles`). Tiles farther away wait until the player flies closer.
- Phase 1 to 4 copy tiles, walls and half blocks. Patch M3 builds plain blocks (tiles with no TileObjectData) and walls only; EVERY object, including one-tile ones (platforms, torches, doors, furniture), chests and machines, is skipped by the job until patch M5 (the hologram already shows them). Slopes and paint are not copied yet (M5 polish).

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
| Material use | server | the server checks and consumes the owner's inventory (its copy); because vanilla ignores a server's writes to a player's own inventory, the owner's client removes the same items after a `MechBuildConsumed` packet and re-syncs those slots |
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
| M3 (applied 2026-11-20) | Shared helpers extracted (`MechSuitBlueprint` scan + item lookup, `WorldPlacement` tile / wall rules, `RoboticArmWorker` now delegates to it); server build job (`MechSuitBuildSystem`); two builder arms (`MechSuitBuilderArms`: motion model, replica, drawing; retract and grab); `MechSuitPlayer.ArmCount` 3 / 4 and 2 anchors while building (`MaxArms` stays the slot count 3); design change: selection no longer drawn | `MechBuildRequest` 63, `MechBuildArmState` 64, `MechBuildConsumed` 65 (cancel packet is M4) |
| M4 | Esc cancel, job end report, progress and remaining-material readout, multiplayer pass | cancel packet |
| M5 | Objects, containers and machines, paint and slopes polish | none new |

## 6. Test checklists

M1: not in the suit, Shift + right-click does nothing special. In the suit, hold Shift and right-click drag: the area sheet appears, a drag past 64 stops growing. Release: the rectangle disappears (design change 2026-11-20) but the selection stays saved: hold Shift and the hologram shows. A new drag replaces it. Shift + right-click without moving clears it. Releasing Shift mid-drag keeps the old selection. Holding Shift does not swing or place the held item. Leaving the world clears it. Taking the suit off hides the hologram, getting back in shows it again. While dragging only the drag rectangle shows (the old saved area is not drawn).

M2: with a selection saved, in the suit, hold Shift: the hologram of the region follows the cursor (cursor tile = centre) with the box around it. Tiles and walls show at about 50% alpha with the right frames; grass shows nothing. Chests, machines and other multi-tile objects show as whole objects (only if the whole object was inside the selection). Place the hologram over empty air with enough material: all blocks and the box are normal. Drop below the needed amount of one block: the blocks farthest from the cursor turn red first, the nearest stay normal, and the box cells under red blocks switch to the red sheet. Put the hologram over solid ground: blocks over a different block turn red; blocks over the same block disappear (already built). Over its own source it all disappears. Releasing Shift hides it. Not in the suit: nothing shows. Shift + left click does not swing or place the held item (since M3 it starts the build, see M3); a new selection clears the stored origin. A selection partly outside the world shows red box cells there. With a saved selection nothing but the hologram and its box is ever drawn, and only while Shift is held.

M3: build a small region of two block types plus a background wall (for example 5 x 5 of dirt and stone) and select it. In the suit, fly near an empty spot, hold Shift, move the hologram to a place within about 6 tiles of you and Shift + left click: (1) one `build requested` line in client.log and, in single-player or on the server, a `[MechBuild] ... started a build` line with the block count; (2) the suit now holds only 2 anchor arms (a third one reels in) and two builder arms (the claws) appear at your sides; (3) each builder arm folds back to you, takes a block (the claw shows the block), reaches out and places the closest valid block, nearest first, no fixed order; (4) your inventory loses exactly one item per placed block (check the stack counts, also on a dedicated server with a client); (5) switching the block type makes that arm fold back to you before the next block; (6) a tile whose support is missing is not placed until a neighbour exists, then it is; (7) a tile of the destination that already holds the same block is skipped, one that holds a different block is ignored and not placed; (8) when a material runs out the job ends with a log line `ended (nothing left can be placed)` that lists blocked / out of material / unsupported / out of reach, and the arms fold back; walls only go next to a tile or wall; (9) leaving the suit (Space / Mount key), dying or leaving the world ends the job and the arms fold away; a second Shift + left click replaces a running job; (10) tiles that are more than about 6.5 tiles away wait (the job does not end while some are only out of reach: fly closer; it times out after 30 s without a placement); (11) objects (chests, platforms, torches, furniture) are NOT built (their count shows in the start line) and the hologram still shows them; (12) a second client near you sees the arms move and the blocks appear; the placement sound plays; (13) Robotic Arm still places blocks and walls as before (the shared helper); (14) the saved selection is not drawn after the drag ends, the drag rectangle still shows while dragging, and Shift shows only the hologram. Report any `[MechBuild]` line that looks wrong.

## 7. Art

None needed for M1 and M2 (they reuse `RoboticArmArea.png` and `RoboticArmAreaInvalid.png`). The builder arms (M3) reuse the existing suit arm art unless Ismail draws new ones.

## 8. Open items (defaults used until Ismail decides)

- DECIDED 2026-11-19: chests, machines and containers are placed EMPTY. Their contents and settings (recipe, area, filters) are not copied, so items are never duplicated.
- DECIDED 2026-11-19: the selection shows no size text.
- What the readout looks like during a build (M4).

## 9. Progress

- [x] M1 Selection (patch_mech_builder_m1_selection.ps1, tested 2026-11-19, all checks passed; patch_mech_builder_m1b_no_readout.ps1 then removed the "W x H" text because Ismail did not want it)
- [x] M2 Hologram (patch_mech_builder_m2_hologram.ps1, tested 2026-11-20, all checks passed)
- [~] M3 Server build job and builder arms (patch_mech_builder_m3_build_job.ps1, applied 2026-11-20, UNTESTED; includes the 2026-11-20 design change: the saved selection is no longer drawn. Tiles and walls only, objects wait for M5; consumed items are mirrored to the owner's client by MechBuildConsumed, which relies on vanilla ignoring a server's writes to the player's own inventory (UNVERIFIED against the tModLoader source). Temporary: the `[MechBuild]` log lines (`MechSuitBuildSystem.LogJobs`).)
- [~] M3 fixes 2026-11-20 (patch_mech_builder_m3_fixes.ps1, UNTESTED): FindTarget no longer throws ArgumentOutOfRange when part of the destination is already built (it keeps the best block, not its index); a new Shift + left click while a job runs now ADDS that copy to the job instead of replacing it (job blocks hold absolute world tiles, cap MaxPendingBlocks 30000, no new packet); the hologram on-screen test uses a bigger margin and never a view smaller than the screen (fix for the cut-off hologram, cause not confirmed in game).
- [~] M4 Cancel, report, placed holograms (patch_mech_builder_m4.ps1, UNTESTED; NEW PACKET MechBuildCancel = 67 client -> server, empty payload: the server ends the job of the SENDER only (MechSuitBuildNet.HandleCancel), every client learns from the normal arm state packet. Esc (MechSuitBuilderSystem.CancelKey) sends it while the suit is building. The server tells the owner what happened when a job ends: one chat line (placed, left, why) via ChatHelper, Main.NewText in single-player. Every copy already sent stays on screen as a hologram until the job ends, each block vanishes when built and turns red where something else stands (client only, MechSuitHologramSystem.AddPlaced / DrawPlaced), with a "N blocks left" readout above the player. Also: the anchors keep all 3 slots while building (AnchorArmsWhileBuilding = 3, the 2 builder arms are extra), and the builder arms always hold an item (first pending block) even when nothing is in reach. NOT done: a missing-material list in the readout (only the end report counts it), objects (M5), the multiplayer verification items)
- [ ] M5 Objects, containers, machines

## 10. Next-chat prompt

Newest (after patch M4 was applied): "Continue the Mech Suit build tool. Clone the repo, read docs/mech_suit_builder/ROADMAP.md (sections 1 to 5, 9) and PROJECT_CONTEXT.md, and docs/multiplayer/ROADMAP.md section 3. State: M1 and M2 tested; M3 fixes and M4 (cancel packet MechBuildCancel = 67, end report, placed-copy holograms, 3 anchors while building, builder arms always hold an item) are applied and UNTESTED, test results below if I give them (record them with a small patch). Remaining: the multiplayer verification items, a missing-material readout, then M5 (objects, containers, machines)."

Older prompt (superseded by the one above):

"Implement Mech Suit build tool patch M4 (cancel, job report, progress readout, multiplayer pass). Clone the repo, read docs/mech_suit_builder/ROADMAP.md (sections 1 to 5, 9) and PROJECT_CONTEXT.md, and docs/multiplayer/ROADMAP.md section 3. State: M1 and M2 tested and done; M3 (server build job, two builder arms, shared helpers) is applied, test results below if I give them (record them with a small docs patch first, do not guess results I did not give; if a test fails, fix M3 first). Code: `MechSuitBuildSystem` (Common/Mounts) holds the server job (`StartJob`, `EndJob(owner, reason)`, per-arm phases Idle / Retracting / Reaching / Striking, `FindTarget` scan, `ApplyPlacement`), `MechSuitBuilderArms` the arm model / replica / drawing, `MechSuitBlueprint` the shared scan and item lookup, `WorldPlacement` (Common/Systems) the tile / wall rules shared with `RoboticArmWorker`, `MechSuitBuildNet` (Common/Net) the packets MechBuildRequest 63, MechBuildArmState 64, MechBuildConsumed 65, and `MechSuitPlayer` has `IsBuilding`, `SetBuilding`, `AnchorArmLimit`, `ArmCount`. Design change already applied: the saved selection is no longer drawn, only the drag rectangle (while dragging) and the Shift hologram. M4 scope: Esc cancels a running job (one cancel packet, server ends the job through EndJob), a job end report and a progress / remaining-material readout for the owner (what the readout looks like is still open: ask me), and the multiplayer pass (second client, disconnect, death, suit off, join while a job runs). Known M3 limits: objects (including platforms, torches, doors, chests, machines) are skipped until M5; slopes and paint are not copied; consumed items are mirrored to the owner by MechBuildConsumed (check the inventories match on a dedicated server); a job times out after IdleTimeoutTicks without a placement. Temporary test code to remove in Phase 10: the [MechBuild] log lines (MechSuitBuildSystem.LogJobs). M3 test results: <paste>."