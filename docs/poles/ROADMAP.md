# Power Poles: Development Roadmap

Factorraria power poles: two electric pole machines (wooden and platinum) joined by hanging wires. Written against repo commit `7d955cc`. Where this document and the code disagree, the code wins.

## 1. Decisions

### Fixed by Ismail
- Two variants. **Wooden**: 1 swaying crossboard with 2 connection points, footprint 1x14, overhang canvas 3x14 (footprint in the middle column, `originX: 1, originY: 0`; the canvas only holds the pixels that poke out left and right). **Platinum**: 2 swaying crossboards (upper and lower) with 2 connection points each (4 total), footprint 1x16, drawn through the overhang system too for consistency (canvas 1x16, `originX: 0, originY: 0`, 16x256 art).
- Custom `ModTile` machines, non-solid (walk-through), anchored on a solid tile below. Electric consumers with a small constant draw that models transport loss. No machine recipes. The only UI element is the electric icon.
- Each pole has a single texture (no On/Off look).
- **Crossboards (changed after P2).** Each board is its own PNG, drawn manually by `PowerPoleCrossboardSystem` (client only, after tiles and after the overhang canvases), rotated around the board's centre by a wind sway (none at or below 20 mph, ramping up linearly to full at 60 mph; platinum sways at 50% of wooden; the swing phase comes from each pole's tile position), and also drawn on the placement hologram. A board is `PoleBoard(texturePath, size, poleHeightTiles, rowFromBottom, linkPoints)`: its centre sits on the middle of the pole column at that tile row, counted from the bottom (wooden row 12 of 14; platinum upper row 14 and lower row 12 of 16). Link points are pixels inside the board PNG. Links use the resting position (`GetLinkPointWorld`); only drawing uses the swayed one (`GetLinkPointWorldSwayed`).
- The footprint tiles accept ground wires (the existing `EnqueueMachineWires` already walks the footprint only). The overhang area accepts none.
- Gold wire becomes placeable like copper and tin. Copper and tin hanging wires have a shorter span than gold.
- Holding a wire (selected item or item on the cursor): click pole 1, a preview wire follows the cursor, click a different pole to connect. v1 draws a straight line. Later: Luminance pixelated trail, wind and player disturbance.

### Assumptions (change any of these before the patch that uses them)
- **A1. What a click means (decided 2026-10-26).** The footprint accepts ground wire with a **left** click, so linking is a **right** click while a wire item is held (selected item or item on the cursor). A right click on the pole's footprint, or on the hitbox of one of its crossboards, starts a link, and a right click on a second pole finishes it. Left clicks behave as today (ground wire on the footprint, nothing on the empty overhang area), so the wire items need no `CanUseItem` guard. The starting right click needs the same reach as placing a wire tile (checked on the client); after that the player may walk away and the pending wire follows the cursor as a hologram. The finishing right click is checked by the server (the tile clicked on pole B must be on that pole and in reach).
- **A2. Span is set by the wire item (decided).** Copper 20 tiles, tin 20, gold 40, measured between the two connection points at their resting positions. Public instance fields on `PoleLinkSystem` (`CopperSpan`, `TinSpan`, `GoldSpan`), not `const` or `static`, so hot reload can tweak them. A pending link (patch P6) turns red when it is longer than the span, cannot be committed, and is dropped at `BreakSpanMultiplier` (2) times the span: copper and tin 40 tiles, gold 80.
- **A3. Wire cost (decided).** One link costs `ceil(span / tilesPerItem)` wire items of its type: copper and tin 5 tiles per item, gold 8 (`CopperTilesPerItem`, `TinTilesPerItem`, `GoldTilesPerItem`). The items are taken when the link is committed (the second right click), never at the first click. The cost is stored in the link and refunded when it is cut or the pole is killed.
- **A4. Pole item recipes (decided).** Ismail registered the recipes himself. The machines have no `CustomRecipe`s and no pole patch touches the recipes.
- **A5. Draw.** Wooden 15 power, platinum 5 power (assembler 200, arm 100). Constant for v1. Optional length-scaled loss in P8.
- **A6. Vanilla wire toggling** (`MachineWireGlobalTile`) works on any `IElectricConsumer`, so a pole could be switched off by a vanilla wire. Left as is; it is a test item, and arguably a nice feature.

## 2. Codebase facts this plan relies on

| Area | What exists | Consequence for poles |
|---|---|---|
| Machine base | `ElectricConsumerMachine : BaseMachine, IElectricConsumer` with abstract `PowerDemand` (`Common/Machines/ElectricMachine.cs`) | Poles subclass it directly |
| Working flag | `isWorking` is only set by `ProcessRecipes()`, which returns at once when `Recipes == null`. `PowerNetwork.Tick` only counts demand of consumers with `isWorking` | A pole must set `isWorking = true` itself in an `Update()` override, or its loss is never counted. The setter is `protected`, so this fits in the subclass |
| Brownout | `PowerNetwork.Tick` is binary: supply below demand sets `isOn = false` for the whole grid | Pole visuals follow `isOn` for free. Connectivity is not affected by brownout |
| Footprint wires | `PowerGridSystem.EnqueueMachineWires` loops the `TileObjectData` width x height from the entity's top-left | Ground wires connect on all 14 or 16 footprint tiles, overhang excluded, with no code change |
| Flood fill | `BuildNetworkFromMachine` (`Common/Systems/PowerGridSystem.cs`): registers machines, floods wires of one type, machines bridge types | Pole links need one new bridge: a registered pole also pulls in its linked poles |
| Wires | `CustomWireType` is `[Flags] byte` (Copper `1<<0`, Tin `1<<1`). `AllWireTypes`, a `(byte Copper, byte Tin)` frame cache, `DrawWireTile`, `PlayerCanSeeWires`, `CustomWireNet.WireItemType` and `IsSingleKnownType`, both cutters | Gold is `1<<2`. Save format, join sync and edit packet already carry the raw byte. Existing worlds stay valid |
| Gold wire | `GoldWireItem` exists (`Content/Items/Materials/GoldWireItem.cs`, `internal`, no use code) and is already an ingredient/product in the Assembler and Autohammer registries | Edit it in place. The patch engine has no move or delete operation, so it stays in `Materials` |
| Overhang | `MachineVisualRegistry.RegisterOverhang<T>(tile, on, off, footprintW, footprintH, originX, originY, onFrames, offFrames, yOffset)`. Padding defaults to 0, so cells are 16px. Assembler registration is the template (`MachineRegisterationSystem.PostSetupContent`) | Wooden canvas 112x224 px, platinum 112x256 px. The tile's `Texture` points at the `_Off` sheet |
| Machine UI | `MachineUIStateBase` subclass + `MachineUIRegistry.Register`. `RoboticArmUIState` shows how to place an `ElectricityUIIcon(Entity.GetUseFraction, ShowElements, () => Entity.WireToggle, Entity)` | `PowerPoleUIState` is one icon, shared by both variants |
| Placement | `BaseMachine.Hook_AfterPlacement` runs `MachinePlacement.ServerPlace` (single-player, server) or sends tile square + entity request (client) | Reused as is by the pole tiles via `PlacementHook(... Hook_AfterPlacement ...)` like the Assembler |
| Kill | `MachineVisualOverride.KillTile` calls `MachineRemoval.OnTileKilled` (server/single-player drops and removes, client removes replica) | Link cleanup hooks in here (server side) |
| Net | `FactorrariaNet.Register/SendRequest/SendToClient/Broadcast/InReach/IsOwner/SyncInventoryItem`, `JoinSync.Register`, per-packet rate-limit cost. Last packet id is `CartMapSnapshot = 52` | New packets append at 53 and 54. Pattern to copy: `CustomWireNet` (request, full-state changed packet, join provider). Item consumption pattern: `BucketPour` (server decides, client acts on the result) |
| Wire drawing | `CustomWireSystem.PostDrawTiles` draws tile wires only while the player holds wire or a cutter | Hanging wires are physical lines: always visible |

## 3. Multiplayer design (rule 9)

**Authority.** The server owns the link graph. Clients never create or remove links directly.

| Thing | Who | How |
|---|---|---|
| Link graph (`PoleLinkSystem`) | Server and single-player | Saved in world data. Clients hold a replica |
| Create / cut request | Owner client asks | `PoleLinkRequest` (to server) |
| Validation | Server | Sender alive; both tiles are poles; different poles; both free points exist; span within the wire's max; the player is in reach of the clicked tile; the player holds enough wire |
| Wire consumption and refunds | Server decides, owning client takes | The server only checks that its mirror of the sender's inventory (cursor slot 58, then 0-57) holds the cost; the owning client takes the items when the `PoleLinkChanged` answer arrives (cursor first, then selected item, then the rest) and syncs each changed slot with `SyncEquipment` (slot 58 is the cursor), the same split as `BucketPour` and `CartInputNet`. Refunds are spawned by the server with `Item.NewItem`, once |
| Replica update | Server broadcasts | `PoleLinkChanged` carries the **complete slot list of each affected pole**, so a repeated or out-of-order packet cannot drift (same idea as `WireChanged`) |
| Join | Server | `JoinSync` provider `"pole-links"` sends every link |
| Power grid | Server (and single-player) | `gridNeedsRebuilding = true` on every link change. Clients receive `isOn` through the machine snapshot as today |
| Pending link, preview, hit-testing, hanging-wire drawing, later the rope simulation | Client only | Never saved, never sent |

**Single-player** runs the server handler directly (rule 10): `PoleLinkNet.RequestLink` and `RequestCut` call the handler when `netMode == SinglePlayer`.

**Rejected requests.** The server answers the sender alone with the current state of the two poles, so the client never stays out of sync. No client prediction: the link appears when the server confirms, which is one round trip.

**Entity vs link lifetime.** Links are keyed by the pole entity's top-left `Point16`, not by entity ID, so they survive the entity bootstrap scan (patch 4.5) and entity re-sharing. The flood fill skips a link whose pole entity does not exist yet. Links are removed only when a pole is **killed** (event-driven), never by a scan at world load, so a pole that has not been bootstrapped yet cannot lose its links.

## 4. Data model

```
PoleLink
  Point16 A, B          // entity top-left of each pole
  byte SlotA, SlotB     // connection point index on each pole
  CustomWireType Wire   // Copper, Tin or Gold
  ushort Cost           // wire items paid (refund amount)

PoleLinkSystem : ModSystem   (Common/Systems/PoleLinkSystem.cs)
  List<PoleLink> Links
  Dictionary<Point16, PoleLink[]> BySlot   // per pole, length = LinkPointCount
  public float CopperSpan, TinSpan, GoldSpan, WireTilesPerItem   // tunables, instance fields
  TryAdd / Remove / RemoveAllFor(Point16 pole) / GetLinkedPoles(Point16)
  SaveWorldData / LoadWorldData   // parallel lists, like CustomWireSystem
```

`PowerPoleTileEntityBase : ElectricConsumerMachine` (folder `Content/Tiles/Machines/ElectricalConsumers/PowerPoles/`) adds `abstract PoleBoard[] Boards`. Each `PoleBoard` holds its PNG path, size, attach point and link points (pixels inside the board PNG). `LinkPointCount` counts the points of all boards (platinum order: upper board's two points, then the lower board's). Connection point world position = `Position * 16 + board.AttachOffset + (point - board.Pivot)` (`GetLinkPointWorld`).

**Connection point coordinates come from the art.** Give the pixel position of each crossarm tip inside its crossboard PNG (x, y from that PNG's top-left) and each PNG's size. The pivot is the PNG's centre, so nothing else needs converting.

**Point choice (decided).** The player never picks a point from a list. The client starts the pending wire at the free point closest to the player and, when the cursor is over a second pole, ends it at that pole's free point closest to the start point; both slots travel in the request (`hintSlotA`, `hintSlotB`) so the preview and the final wire match. The server uses a hint when that point exists and is free, otherwise it refuses with `SlotTaken`. A request without hints (slot 255, what the temporary `/plink` sends) lets the server choose: for A the free point closest to B's footprint top, for B the free point closest to A's chosen point. A pole with no free point refuses the link; the preview still attaches to the closest point but is drawn red and cannot be committed.

## 5. Patch plan

One chat = one patch, same workflow as the multiplayer roadmap. Every patch ends with the usual changed/remember list and a test checklist, and marks its progress line in this file. Because poles add netcode, every patch from P3 also adds a line to the Progress section of `docs/multiplayer/ROADMAP.md` and a next-chat prompt (rule 10).

### P1. Gold wire (tile wire only)
No poles yet. Small and testable on its own. Patch file: `patch_pole_p1_gold_wire.ps1`.

- `Common/Systems/CustomWireSystem.cs`: add `Gold = 1 << 2`; add it to `AllWireTypes`; add a `GoldWireTileTexture` asset requested in `Load()` with `RequestIfExists` (a missing PNG logs a warning and gold wire is simply not drawn); the frame cache tuple becomes `(byte Copper, byte Tin, byte Gold)` (`RefreshFrame`, `DrawWireTile`); `DrawWireTile` draws the gold layer; `PlayerCanSeeWires` also shows wires while a `GoldWireItem` is held.
- `Common/Net/CustomWireNet.cs`: `WireItemType` becomes a switch with a Gold case.
- `Content/Items/Materials/GoldWireItem.cs`: same use behaviour as `CopperWireItem` (`CanUseItem` reach check, owner-only `UseItem`, `CustomWireNet.RequestEdit`, consumable, use time); keeps its file, namespace, `maxStack` and rarity.
- `Content/Items/Wires/IronCutterItem.cs` and `LeadCutterItem.cs`: Gold branch (checked before Tin and Copper).
- `docs/poles/ROADMAP.md` (new): this document.
- Art: `Content/Items/Wires/GoldWireTile.png`, same sheet layout and size as `CopperWireTile.png` (16 frames, 18px stride). The `GoldWireItem` sprite already exists.

Multiplayer: nothing new. The edit packet and join sync already carry the raw type byte.

Tests: place, cut and refund gold wire in single-player; gold wire powers a machine from a producer; mixed copper/tin/gold on one machine bridges; reload the world, wires and frames persist; in a dedicated-server session another client sees the gold wire and the cut refund drops once.

### P2. Pole tiles, entities, UI, visuals (no links)
- New folder `Content/Tiles/Machines/ElectricalConsumers/PowerPoles/` with `PowerPoleTileEntityBase.cs`, `WoodenPowerPoleTile.cs`, `WoodenPowerPoleTileEntity.cs`, `PlatinumPowerPoleTile.cs`, `PlatinumPowerPoleTileEntity.cs`, `PowerPoleUIState.cs`.
- Tile setup (both): `Main.tileFrameImportant`, not solid, `TileObjectData` width 1, height 14 or 16, `CoordinateHeights` all 16, `Origin` at the bottom tile, `AnchorBottom` on a solid tile, `HookPostPlaceMyPlayer` = `PlacementHook(ModContent.GetInstance<...TileEntity>().Hook_AfterPlacement, -1, 0, true)` exactly like `MechanicalAssemblerTile`, `Texture` => the `_Off` sheet, map entry, `RegisterItemDrop`.
- Entity: `ValidTileType`, `PowerDemand` (A5), `Update()` override calling `base.Update()` then `isWorking = true`, `LinkPointOffsets` (placeholder values until the real art coordinates are known).
- UI: one `PowerPoleUIState` with one `ElectricityUIIcon` entry (copy of the Robotic Arm entry), registered for both tile types with `MachineUIRegistry.Register`.
- `Common/Systems/MachineRegisterationSystem.cs`: UI registration in `Load()`, `RegisterOverhang<...>` in `PostSetupContent()` (wooden `footprintWidth: 1, footprintHeight: 14, originX: 3, originY: 0`; platinum `1, 16, 3, 0`; one frame each).
- Items: `WoodenPowerPoleItem`, `PlatinumPowerPoleItem` in `Content/Items/Machines/` (`createTile`). Recipes per Open item 1.
- Art (after P2b): `WoodenPowerPole.png` at 192x224 (4 styles side by side, each an overhang canvas 3x14, plain 16px cells, no padding; a random style 0 to 3 is rolled per placed pole) and `PlatinumPowerPole.png` at 16x256 (overhang canvas 1x16), one texture each (no On/Off look), plus the crossboard PNGs and both item icons. The wooden sprite keeps the footprint in the middle column and may poke out left and right; platinum is footprint only but still registered as an overhang.

Multiplayer: placement and kill go through the existing Phase 4 paths, so no new code. The snapshot already carries `isOn`/`isWorking`.

Tests: place on flat ground; refuse placement without a solid tile below or with blocked space above; walk through the pole; `_On`/`_Off` flips with power; right-click shows only the electric icon; break it and the item returns; ground wire on a footprint tile joins the pole to a grid; ground wire next to the overhang does not; overhang draws correctly near screen edges (see Verification V3); placement hologram looks right; dedicated server: place, join late, break.

### P3. Link data layer (server authority, no player input yet)
- `Common/Systems/PoleLinkSystem.cs` (data model above, save/load, tunables).
- `Common/Net/PoleLinkNet.cs`: `RequestLink`, `RequestCut`, `HandleRequest` (full validation from section 3), `SendChanged`, `HandleChanged`, `WriteAll`/`ReadAll`, registration in a `PoleLinkNetSystem : ModSystem.Load()` exactly like `CustomWireNetSystem`. Appends `PoleLinkRequest = 53` and `PoleLinkChanged = 54` to `FactorrariaPacket`, with a rate-limit cost.
- Kill cleanup: `MachineRemoval.OnTileKilled` (server and single-player branch) calls `PoleLinkSystem.RemoveAllFor(pole)`, refunds each link's wire to the player or drops it, and broadcasts `PoleLinkChanged` for the other end of each removed link.
- Every link change sets `PowerGridSystem.gridNeedsRebuilding = true`.
- Temporary test command `/plink x1 y1 x2 y2 [copper|tin|gold]` and `/plinks` (list), because the input does not exist yet. Added to the Phase 10 removal list.

Tests: link two poles with `/plink` in single-player, save and reload (links persist); dedicated server: link, a late joiner receives it, kill a pole, the other end and the joiner both update; refused cases (too long, same pole, no free point, not a pole) answer the sender and change nothing.

### P4. Power across links
- `Common/Systems/PowerGridSystem.cs`: add a `Queue<TileEntity>` of linked poles to `BuildNetworkFromMachine`, drained in the same loop. Two edit points: right after the start machine registers, and in the bridge branch where a found machine registers. A small private helper does "if machine is a pole, enqueue `GetLinkedPoles`". When a linked pole is still in `remainingMachines` it is registered and its footprint wires are enqueued (`EnqueueMachineWires`), so wire networks on both ends merge.
- The flood fill never touches a missing entity.
- `isWorking = true` from P2 now matters: a long chain of poles shows its total loss in the grid readout.

Multiplayer: server-side only. Clients get `isOn` through snapshots.

Tests: producer on a wire at pole 1, consumer on a wire at pole 2, linked: consumer runs; cut the link and it stops; copper-only grid at one end and tin-only at the other still join through a pole link; a chain of three poles; brownout turns every pole in the grid dark and recovers; the electric icon's demand readout includes the pole loss.

### P5. Hanging wire drawing (straight line)
- `Common/Systems/PoleWireDrawSystem.cs`: `PostDrawTiles` draws every link whose bounding box intersects the screen (zoom corrected, same visible-rect style as `CustomWireSystem.GetVisibleTileRect`). Straight line from point A to point B drawn with `TextureAssets.MagicPixel` using the source rectangle `new Rectangle(0, 0, 1, 1)`, 2px thick, tinted per wire type and multiplied by `Lighting.GetColor` at the midpoint.
- The drawing sits behind a one-method seam (two points, a wire type) so P9 replaces its body without touching callers.
- Make the line draw after the overhang canvases so wires end on top of the crossarm tips.
- Client only. No save, no packets.

Tests: links drawn at the right points in both directions; culling at screen edges and zoom out; performance with several hundred links off screen; light dims the wire in dark caves; draws on a dedicated-server client after join.

### P6. Player input: pending link, preview, request
- `Common/Systems/PoleLinkInputSystem.cs` plus a `PoleLinkPlayer : ModPlayer`. Owner client only (`FactorrariaNet.IsOwner`).
- The held wire is the item on the cursor (`Main.mouseItem`) when there is one, otherwise `player.HeldItem`. Both count.
- Click handling on the **right-click press edge** (not on hold): while a wire item is held (cursor item first, otherwise the selected item), a right click whose cursor is on a pole's footprint or on the hitbox of one of its crossboards, with the pole in the same reach as placing a wire tile, and not on a UI element, is consumed (the pole's UI does not open): the first click sets `Pending = (pole, start point = the free point closest to the player)`. While pending, the wire follows the cursor as a hologram even when the player walks away. Hovering a second pole snaps the end to that pole's free point closest to the start point; a second right click on it finishes the link and calls `PoleLinkNet.RequestLink` (with the hint slots and the clicked tile).
- No `CanUseItem` guard is needed (decided): left click still places ground wire and only the right click links (A1).
- Cancel the pending link on Escape, switching to a non-wire item, the first pole being killed (replica removed), the span reaching `BreakSpanMultiplier` times the wire's span (copper and tin 40 tiles, gold 80), or world unload. A right click that is not on a pole while pending does nothing.
- Preview: from the start point to the cursor (or to the hovered second pole's chosen point), drawn only while on screen. Normal colour when the span is within the held wire's range; **red** when it is longer (it does not break until twice the span), when the second pole has no free point (it still attaches to the closest point), or when the player does not hold enough wire; red wires cannot be committed. Show the span in tiles near the cursor. Nearby connection points are highlighted while wire is held. Finished links are drawn by P5 and, like the preview, are not drawn when off screen.
- Consumption (V5, resolved in P3): the server checks that its mirror of the sender holds `Cost` wire items and answers with `PoleLinkChanged` (Linked + item type + count); the owning client takes them (`PoleLinkNet.ConsumeWire`: cursor stack first, then the selected item, then the rest; `SyncEquipment` per changed slot, slot 58 for the cursor). P6 only adds the input; the packets already exist.
- Failure feedback: a `Main.NewText` or `CombatText` saying too long, no free point, or not enough wire, driven by the answer packet.

Multiplayer: this is the first patch where a player can create links in a multiplayer session; the full P3 validation applies. The reach of the starting click is checked on the client only (the same check as placing a wire tile). The server checks the finishing click: the tile in the request must be on pole B (footprint or crossboard, with `ClickSlackPixels`) and within `FactorrariaNet.InReach`. The first pole can be far away by then, which is fine because the request carries both pole tiles and the server verifies both are poles, not that the player is near the first.

Tests: link two poles with each wire type; span too long in red; pending cancelled by right-click/Esc/item swap; cursor-held wire works with the inventory open; hotbar-held wire works; clicking the footprint still places ground wire; autoReuse does not double-fire; not enough wire shows a message and consumes nothing; two players linking the same free point at once (second is refused and told); dedicated server and Host & Play.

### P7. Cutting links and refunds
- `IronCutterItem` and `LeadCutterItem` get a link branch before the tile-wire branch: if the cursor is within about 6px of a hanging line (segment distance test, later the sag curve) or within a connection point's hit zone, send `PoleLinkNet.RequestCut` for that link.
- Server removes the link, returns `Cost` wire items of the link's type with `Item.NewItem`, broadcasts `PoleLinkChanged` for both poles, and flags the grid.
- A cut beats a ground-wire cut only when the cursor is closer to the line than to a footprint wire tile.

Tests: cut by clicking the line and by clicking the point; refund count equals the stored cost; the other client sees the wire vanish; power drops on the far side; killing a pole refunds all its links once (not twice).

### P8. Polish
- `PowerDemand` scaled by the total length of the links the pole owns (each link counted half at each end), keeping the constant floor.
- Hover tip or UI readout of "links 1/2".
- Sounds for linking, cutting and a quiet hum while powered (optional).
- Balance pass on spans, cost per item and pole draw.

### P9. Living wire (separate plan, written when P8 is done)
Client-only visual upgrade, no netcode.
- Replace the straight line with the Luminance pixelated trail renderer. Before writing any code, read the Luminance API in the DLL's `.xml` docs or its source; do not guess signatures.
- A small Verlet rope per visible link: node count from span, slack and sag from span, wind from `Main.windSpeedCurrent`, local player and NPC velocity pushing nearby nodes, damping, simulation only for links on screen, node cap for performance.
- The connection point positions stay authoritative from the link data. Only the curve between them is simulated, and each client simulates its own.
- The span rule and the cut hit-test then use the curve or an upper-bound segment.

## 6. Open items needing a decision
1. ~~How do players get poles?~~ Decided: Ismail registered the recipes manually.
2. ~~A1 hit-zone rule~~ Decided (2026-10-26): a right click on the footprint or on a crossboard's hitbox while holding wire; left click still places ground wire (A1).
3. ~~Span numbers and cost per item~~ Decided (2026-10-26): copper and tin 20 tiles at 5 tiles per item, gold 40 tiles at 8 tiles per item (A2, A3). P8 may retune them.
4. ~~Reach rule~~ Decided (2026-10-26): the starting click needs the same reach as placing a wire tile (client side); the player may then walk away. The server checks the finishing click's tile on pole B for reach.

## 7. Verification list (checked in code or API before the patch that needs it)
- **V1.** The correct tModLoader hook name for "tile entity removed". Plan: use `MachineRemoval.OnTileKilled` instead, which already exists. **Resolved in P3:** `MachineVisualOverride.KillTile` (a `GlobalTile`) calls `MachineRemoval.OnTileKilled` for every registered machine tile, poles included (overhang registration puts them in `MachineVisualRegistry.Definitions`), once per process that sees the break, idempotent because the entity is gone after the first call. The link cleanup sits in `PoleLinkNet.OnMachineKilled`, called from both branches of `OnTileKilled`.
- **V2.** `TileEntityHelper.TryGetEntityFromTile` and `MachineWidth/MachineHeight` behave for a 1x14 and 1x16 footprint (only exercised on shapes up to 3x3 and the Robotic Arm so far). **Resolved in P3:** `TileEntityHelper.TryGetEntityFromTile` looks the clicked tile up in `TileEntity.ByPosition` first and otherwise derives the corner from the tile frame and `TileObjectData` (`CoordinateHeights`), so any of the 14 or 16 tiles works, and it was already exercised in-game by the P2 right-click and break tests. `MachineWidth/MachineHeight` come from `TileObjectData` in `InitializeGeometry` (lazy, may be unset on a fresh entity), so the pole code reads `TileObjectData.GetTileData(ValidTileType, 0).Height` itself (`PoleLinkSystem.FootprintHeight`).
- **V3.** `MachineOverhangSystem` culls by the **canvas** rectangle, not the footprint, so a pole whose footprint is off screen but whose overhang is visible still draws. Check at P2.
- **V4.** Tall `TileObjectData` placement: anchor row, `CoordinateHeights` array of 14 and 16 entries, `Origin`, and that vanilla placement preview is skipped for overhang machines (the Assembler's comment says it is).
- **V5.** Server-side removal of items from a cursor-held stack and `FactorrariaNet.SyncInventoryItem` usage; see how `BucketPour` swaps the bucket. **Resolved in P3:** the server's copy of the cursor item is `player.inventory[58]` (`BucketPour` and `CartInputNet` read it that way); the server only checks the count against its mirror, and the owning client takes the items and syncs each changed slot with `NetMessage.SendData(MessageID.SyncEquipment, ..., slot, prefix)`, using slot 58 for `Main.mouseItem` (`FactorrariaNet.SyncInventoryItem` finds slots by reference in `inventory`, so it does not cover a cursor item that is not that exact object). Still to confirm in-game on a dedicated server: a cursor-held stack is counted and taken correctly.
- **V6.** `MachineWireGlobalTile.PreHitWire` on a 14-tall footprint skips all footprint tiles correctly (A6).
- **V7.** Luminance's trail renderer API (P9 only).
- **V8.** `JoinSync` ordering: links arrive independently of entities, so a joiner may know links before it knows the pole entities. The drawing and the flood fill must tolerate missing entities. **Handled in P3:** links are keyed by tile position and never validated against entities at load or join; `GetLinkedPoles` returns positions only; `/plinks` prints `entity missing here` for an end the process has no entity for. Patch P4 (the flood fill) and P5 (drawing) must skip a missing entity.

## 8. Risks
- Wire item `autoReuse` plus a click that both links and places ground wire: handled by press-edge detection and the `CanUseItem` guard, but this is the most likely source of first-test bugs.
- Pole draw only matters if `isWorking` is set. Forgetting it makes the poles invisible to the grid.
- A 14 and 16 tall non-solid column interacts with vanilla placement checks and with NPC and cart pathing. Test near carts and tracks.
- Link data in world saves is new state. Keep the save keys stable from P3 on, and treat unknown future fields as optional.
- Rate limiter cost for `PoleLinkRequest` must allow normal play (a click every few seconds) while blocking floods.

## 9. Temporary test code to remove in Phase 10
`/plink`, `/plinks` (P3; both live in `Common/Net/PoleLinkCommands.cs`, delete the file), the `PoleLinkNet.Trace` helper and every `[PoleLinks]` log line (calls in `PoleLinkNet.cs` and `PoleLinkSystem.cs`), and from P4 the counters `PowerGridSystem.PoleBridgesLastBuild` and `PoleLinkEndsMissingLastBuild`, the `[PoleLinks] grid rebuilt` log line in `PowerGridSystem.RebuildNetworks`, and the grid parts of `/plinks` (they go away with the file, except the counters and the log line), plus anything the multiplayer roadmap already lists.

## 10. Progress
One line per patch. `[ ]` not started, `[~]` applied, untested, `[x]` tested and done. Each patch marks itself `[~]`; a small follow-up patch records test results.

- [x] P1 Gold wire (patch_pole_p1_gold_wire.ps1; tested, all checklist items passed as reported)
- [x] P2 Pole tiles, entities, UI, visuals (patch_pole_p2_tiles.ps1: tested, all checklist items passed as reported; the visuals part is superseded by P2b. Originally: applied, untested. Needs the 4 sheets + 2 item icons or the game will not load. LinkPointOffsets are placeholders. UI and overhang registration sit in PostSetupContent, not Load, because tile types only exist there. Recipes are placeholders in the item classes)
- [x] P2b Swaying crossboards, footprint-only art, draw 15 / 5 (patch_pole_p2b_crossboard.ps1: tested, works well as reported; sway tuned in patch_pole_p2c_sway_tweaks.ps1. Needs WoodenPowerPole.png 48x224 (3x14 canvas, replaces the old 112x224), PlatinumPowerPole.png 16x256 (replaces the old 112x256, overhang canvas 1x16), one texture each, WoodenPowerPole_Crossboard, PlatinumPowerPole_CrossboardUpper and _CrossboardLower, and 2 item icons. Board sizes and link points are the real art values)
- [x] P2c Wind sway tweaks (patch_pole_p2c_sway_tweaks.ps1: tested, works well as reported. No sway at or below 20 mph, linear ramp to full at 60 mph, platinum 50%, phase from each pole's tile position)
- [x] P2d Machine art styles (patch_art_styles.ps1: tested, all checklist items passed as reported, including the dedicated-server join and client-placement checks. `RegisterOverhang(styleCount)`, rolled + saved + synced `BaseMachine.VisualStyle`; the wooden pole has 4 styles)
- [~] P3 Link data layer (patch_pole_p3_link_data.ps1, applied, untested). BUILT: `Common/Systems/PoleLinkSystem.cs` (PoleLink, the graph, the tunables CopperSpan/TinSpan/GoldSpan 20/20/40, Copper/Tin/GoldTilesPerItem 5/5/8, BreakSpanMultiplier 2, ClickSlackPixels, save keys PoleLinkA/B/SlotA/SlotB/Wire/Cost, geometry and point-choice helpers, `Version` counter); `Common/Net/PoleLinkNet.cs` (PoleLinkRequest = 53 with Link and Cut, hint slots and a clicked tile; PoleLinkChanged = 54 with a result byte and the complete slot list of each affected pole; join provider `pole-links`; a client queue drained in PostUpdateEverything; wire consumption `ConsumeWire`); kill cleanup (`MachineRemoval.OnTileKilled` calls `PoleLinkNet.OnMachineKilled`); `Common/Net/PoleLinkCommands.cs` (/plink, /plinks, temporary); `CustomWireNet.IsSingleKnownType` and `WireItemType` made public. Every change sets `PowerGridSystem.gridNeedsRebuilding`. The server flood fill does not use the links until P4. DECISIONS recorded 2026-10-26 (A1 to A4, open items 1 to 4, point choice, reach). VERIFIED in code: V1, V2, V5 (in-game part still open), V8. KNOWN LIMITS: a pole tile removed without `MachineVisualOverride.KillTile` would leave orphan links (harmless to the flood fill, visible in `/plinks`); the server trusts its mirror of the cursor slot for the wire count; a link keeps the cost it was paid at even if the tunables change later. Test checklist: see the patch reply. Next chat prompt: see below.
- [~] P4 Power across links (patch_pole_p4_power_links.ps1, applied, untested; tested together with P3). BUILT in `Common/Systems/PowerGridSystem.cs`: `BuildNetworkFromMachine` has a `Queue<Point16> linkedPoleQueue` (positions, not entities, so a missing entity can be skipped). `EnqueueLinkedPoles(machine)` runs at the two edit points (right after the start machine registers, and in the bridge branch after a found machine registers); it only acts for `PowerPoleTileEntityBase`. The wire loop is now `while (wireQueue.Count > 0 || linkedPoleQueue.Count > 0)` and drains the queue first through `DrainLinkedPoles`: each queued position must hold a `PowerPoleTileEntityBase` (a missing entity, or some other machine built where a pole used to be, is skipped and counted, so a stale link can never bridge power into the wrong machine) and still be in `remainingMachines`; it then registers, enqueues its footprint wires (`EnqueueMachineWires`, so copper, tin and gold networks at both ends merge) and its own links (chains work). Server and single-player only (the rebuild never runs on a client); no packet; clients get `isOn` through the machine snapshot as before. Rebuilds are already flagged by every link change (P3) and by a pole being killed or created. TEMPORARY TEST AIDS (Phase 10): counters `PowerGridSystem.PoleBridgesLastBuild` and `PoleLinkEndsMissingLastBuild`, one `[PoleLinks] grid rebuilt: ...` log line per rebuild while links exist, and `/plinks` now prints the grid count plus `same grid` or `DIFFERENT grids` for each link. The existing debug overlay (config `EnableDebugs`) colours each grid, and the electric icon hover shows the grid's demand and supply. KNOWN LIMITS: a pole counts its 15 or 5 power as soon as it is in any grid, linked or not (unchanged since P2); a link between two poles that are both off every producer's grid just makes a consumer-only grid (reads Off, as a lone pole does). Test checklist: see the patch reply (P3 and P4 together). Next chat prompt: see below.
- [ ] P5 Hanging wire drawing (straight line)
- [ ] P6 Player input and preview
- [ ] P7 Cutting and refunds
- [ ] P8 Polish
- [ ] P9 Living wire (Luminance trail, wind, disturbance)

Starting a patch: "Implement pole patch P<n>." The patch reads this file from the fresh clone, checks that its prerequisites are marked done, states the authority and sync design first, and ends with a next-chat prompt for the following patch.

Next chat prompt (after P4 applied): "Continue Factorraria power poles. Clone the repo and read docs/poles/ROADMAP.md (sections 1, 3, 4, 5 P5, 7 and the Progress section) and, from docs/multiplayer/ROADMAP.md, section 3 (working rules) and its Progress section (the power pole link lines). State: P1 (gold wire), P2/P2b/P2c/P2d are tested and done. P3 (link data layer) and P4 (power across links) are applied; I will give my test results for both first and you record only what I report, you do not guess. Then implement P5 (hanging wire drawing, straight line): client only, no packets, no save. Draw every link of PoleLinkSystem.Instance.Links whose bounding box intersects the visible rect (zoom corrected, same style as CustomWireSystem.GetVisibleTileRect); wires that are off screen are not drawn at all. The end points are the SWAYED link points: PowerPoleTileEntityBase.GetLinkPointWorldSwayed(index, PowerPoleCrossboardSystem.GetSwayAngle(pole.Position, boardIndex, pole.SwayScale)) so the wire follows the swaying crossboards (links and span checks keep using the resting GetLinkPointWorld); skip a link whose pole entity is missing on this client (V8). Straight line with TextureAssets.MagicPixel using source Rectangle(0, 0, 1, 1), 2px thick, tinted per wire type, multiplied by Lighting.GetColor at the midpoint, drawn after the overhang canvases and the crossboards so wires end on top of the crossarm tips. Put the drawing behind a one-method seam (two points, a wire type) so P9 replaces its body without touching callers; build it so the P6 pending-link hologram can reuse the same seam (it draws from the start point to the cursor and can be red). Use PoleLinkSystem.Version to cache anything per-link. Check how PowerPoleCrossboardSystem orders its draw and where GetSwayAngle lives before writing code. State the authority/sync design first (rule 9: client only), check every Find against the fresh clone, and deliver a PowerShell patch with the usual changed/remember list and test checklist (section 5 P5 of the poles roadmap), the Progress edits in BOTH roadmaps, and the next-chat prompt for P6 (player input: right click with wire held starts a pending link, hologram follows the cursor, red above the span, dropped at twice the span, second right click commits). Settled design to keep: right click links, left click places ground wire; spans copper and tin 20 tiles, gold 40; the wire is only taken when the link is committed."

(Superseded by the prompt above.) Next chat prompt (after P3 applied): "Continue Factorraria power poles. Clone the repo and read docs/poles/ROADMAP.md (sections 1, 3, 4, 5 P4, 7 and the Progress section) and, from docs/multiplayer/ROADMAP.md, section 3 (working rules) and its Progress section (the power pole link data layer line). State: P1 (gold wire), P2/P2b/P2c/P2d (poles, swaying crossboards, wind tweaks, machine art styles) are tested and done. P3 (link data layer: PoleLinkSystem, PoleLinkNet, kill cleanup, temporary /plink and /plinks) is applied; I will give my test results first and you record only what I report, you do not guess. Then implement P4 (power across links): in PowerGridSystem.BuildNetworkFromMachine a registered pole also pulls in its linked poles through PoleLinkSystem.Instance.GetLinkedPoles(pole.Position), which returns positions only and can name a pole whose entity does not exist on this process, so skip those (V8); a linked pole that is still in remainingMachines is registered and its footprint wires are enqueued (EnqueueMachineWires) so the wire networks at both ends merge; chains of poles must work. Server and single-player only (clients get isOn through the machine snapshot). State the authority/sync design first (rule 9), check every Find against the fresh clone, and deliver a PowerShell patch with the usual changed/remember list and test checklist (section 5 P4 of the poles roadmap), the Progress edits in BOTH roadmaps (the poles P4 line and a multiplayer Progress line), and the next-chat prompt for P5 (hanging wire drawing, straight line, only links on screen are drawn). Settled design to keep: right click links, left click places ground wire; spans copper and tin 20 tiles, gold 40; the wire is only taken when the link is committed."

(Superseded by the prompt above.) Next chat prompt (after P2d): "Continue Factorraria power poles. Clone the repo and read docs/poles/ROADMAP.md (sections 1, 3, 4, 5 P3, 6, 7 and the Progress section) and, from docs/multiplayer/ROADMAP.md, section 3 (working rules) and its Progress section. State: P1 (gold wire), P2/P2b (poles, swaying crossboards), P2c (wind sway tweaks) and P2d (machine art styles, wooden pole has 4) are tested and done. Poles place, draw, sway, power draw is 15 wooden and 5 platinum, link points exist as pixels on the boards (GetLinkPointWorld / GetLinkPointWorldSwayed), but there are no links yet. Implement P3 (link data layer, server authority, no player input): PoleLinkSystem, PoleLinkNet with PoleLinkRequest and PoleLinkChanged, JoinSync provider, kill cleanup in MachineRemoval.OnTileKilled, gridNeedsRebuilding on every change, and the temporary /plink and /plinks commands. State the authority/sync design first (rule 9), check V1, V2, V5 and V8 in the code, copy the CustomWireNet / WireChanged pattern for the packets, and check that every Find matches the fresh clone. Deliver a PowerShell patch with the usual changed/remember list and test checklist, plus Progress edits in BOTH roadmaps (the poles P3 line and a multiplayer Progress line) and the next-chat prompt for P4. Add /plink, /plinks and the pole-link log lines to the Phase 10 removal list. My decisions on the open items: <paste>."