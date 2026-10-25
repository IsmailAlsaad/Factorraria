# Power Poles: Development Roadmap

Factorraria power poles: two electric pole machines (wooden and platinum) joined by hanging wires. Written against repo commit `7d955cc`. Where this document and the code disagree, the code wins.

## 1. Decisions

### Fixed by Ismail
- Two variants. **Wooden**: 2 connection points, footprint 1x14, overhang canvas 7x14. **Platinum**: 4 connection points, footprint 1x16, overhang canvas 7x16. The footprint sits in the middle column of the canvas (`originX: 3, originY: 0`).
- Custom `ModTile` machines, non-solid (walk-through), anchored on a solid tile below. Electric consumers with a small constant draw that models transport loss. No machine recipes. The only UI element is the electric icon.
- Drawn through the overhang system. `_On` / `_Off` follow `isOn`, so a pole looks dead when its grid is underpowered.
- The footprint tiles accept ground wires (the existing `EnqueueMachineWires` already walks the footprint only). The overhang area accepts none.
- Gold wire becomes placeable like copper and tin. Copper and tin hanging wires have a shorter span than gold.
- Holding a wire (selected item or item on the cursor): click pole 1, a preview wire follows the cursor, click a different pole to connect. v1 draws a straight line. Later: Luminance pixelated trail, wind and player disturbance.

### Assumptions (change any of these before the patch that uses them)
- **A1. What a click means.** The footprint accepts ground wire, so clicking a pole with a wire is ambiguous. Rule: a click whose cursor is within about 1.5 tiles of one of the pole's **connection points** (crossarm tips, on the overhang) starts or finishes a link. Every other click behaves as today (ground wire on the footprint, nothing on the empty overhang area). While wire is held, nearby connection points are highlighted so the zone is visible.
- **A2. Span is set by the wire item.** The wire type decides the maximum span, measured between the two connection points in tiles. Placeholder tunables: copper 12, tin 14, gold 40. These are plain public fields on `PoleLinkSystem` (not `const` or `static`) so hot reload can tweak them.
- **A3. Wire cost.** One link costs `ceil(span / WireTilesPerItem)` wire items of the held type (placeholder 4 tiles per item). The cost is stored in the link and refunded when it is cut.
- **A4. Pole item recipes** are not decided yet (see Open items). The machines have no `CustomRecipe`s, but the pole *items* need some way to be obtained.
- **A5. Draw.** Wooden 1 power, platinum 2 power (assembler 200, arm 100). Constant for v1. Optional length-scaled loss in P8.
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
| Wire consumption and refunds | Server | Removes items and calls `SyncInventoryItem`; refunds with `Item.NewItem` once, server side (same as the cut-wire path) |
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

`PowerPoleTileEntityBase : ElectricConsumerMachine` (folder `Content/Tiles/Machines/ElectricalConsumers/PowerPoles/`) adds `abstract Vector2[] LinkPointOffsets` (pixels, from the footprint's top-left) and `LinkPointCount`. Wooden and platinum subclasses fill them in. Connection point world position = `Position * 16 + offset`.

**Connection point coordinates come from the art.** When the sprites exist, give the pixel position of each crossarm tip on the sprite (x, y from the canvas top-left). The conversion is `offset = (px - 48, py)` for both variants (3 tiles of padding on the left).

**Point choice.** The player does not choose a point. For each pole the server takes the free point closest to the other pole's footprint top. If a pole has no free point the link is refused.

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
- Art: `WoodenPowerPole_On.png` / `_Off.png` at 112x224, `PlatinumPowerPole_On.png` / `_Off.png` at 112x256 (plain 16px cells, no padding, single frame), plus both item icons. The sprite must keep the footprint column clear and put the crossarms inside the canvas.

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
- Click handling on the **left-click press edge** (not on hold, so `autoReuse` cannot link and finish in the same second): if the cursor world position is within the hit radius of a connection point of a pole the local player can reach, and the click is not on a UI element, the click is consumed: first click sets `Pending = (pole, point)`; a click on a different pole finishes it and calls `PoleLinkNet.RequestLink`.
- `CopperWireItem`, `TinWireItem` and `GoldWireItem` get a `CanUseItem` guard: return false when the cursor is inside a connection-point hit zone, so the same click does not also place ground wire.
- Cancel the pending link on right-click, Escape, switching to a non-wire item, the first pole being killed (replica removed), or world unload.
- Preview: from the first point to the cursor, green when the span is within the held wire's range, red when it is too long, with the span in tiles shown near the cursor. Nearby connection points are highlighted while wire is held. A pole with no free point shows a different highlight.
- Consumption: the server removes `Cost` wire items from the sender and calls `SyncInventoryItem`. For a cursor-held stack, the inventory sync path must be checked in the API at patch time (Verification V5).
- Failure feedback: a `Main.NewText` or `CombatText` saying too long, no free point, or not enough wire, driven by the answer packet.

Multiplayer: this is the first patch where a player can create links in a multiplayer session; the full P3 validation applies. Reach is checked against the second pole's clicked position only. The first pole can be far away by then, which is fine because the request carries both pole positions and the server verifies both are poles, not that the player is near the first.

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
1. **How do players get poles?** Machines have no `CustomRecipe`s, but the items need a source. Suggestion: wooden = wood + copper/tin wire, platinum = platinum bars + gold wire, as vanilla crafting recipes in `Common/Systems/RecipeSystem.cs`, or as Mechanical Assembler recipes. A simple placeholder is used in P2 unless decided.
2. **A1 hit-zone rule** (click near the connection points). If a different gesture is wanted (for example holding Shift), decide before P6.
3. **Span numbers and cost per item** (A2, A3), tuned in P8.
4. **Whether a pole should refuse links when the player is out of the first pole's reach**: currently only the clicked pole's reach is checked.

## 7. Verification list (checked in code or API before the patch that needs it)
- **V1.** The correct tModLoader hook name for "tile entity removed". Plan: use `MachineRemoval.OnTileKilled` instead, which already exists.
- **V2.** `TileEntityHelper.TryGetEntityFromTile` and `MachineWidth/MachineHeight` behave for a 1x14 and 1x16 footprint (only exercised on shapes up to 3x3 and the Robotic Arm so far).
- **V3.** `MachineOverhangSystem` culls by the **canvas** rectangle, not the footprint, so a pole whose footprint is off screen but whose overhang is visible still draws. Check at P2.
- **V4.** Tall `TileObjectData` placement: anchor row, `CoordinateHeights` array of 14 and 16 entries, `Origin`, and that vanilla placement preview is skipped for overhang machines (the Assembler's comment says it is).
- **V5.** Server-side removal of items from a cursor-held stack and `FactorrariaNet.SyncInventoryItem` usage; see how `BucketPour` swaps the bucket.
- **V6.** `MachineWireGlobalTile.PreHitWire` on a 14-tall footprint skips all footprint tiles correctly (A6).
- **V7.** Luminance's trail renderer API (P9 only).
- **V8.** `JoinSync` ordering: links arrive independently of entities, so a joiner may know links before it knows the pole entities. The drawing and the flood fill must tolerate missing entities.

## 8. Risks
- Wire item `autoReuse` plus a click that both links and places ground wire: handled by press-edge detection and the `CanUseItem` guard, but this is the most likely source of first-test bugs.
- Pole draw only matters if `isWorking` is set. Forgetting it makes the poles invisible to the grid.
- A 14 and 16 tall non-solid column interacts with vanilla placement checks and with NPC and cart pathing. Test near carts and tracks.
- Link data in world saves is new state. Keep the save keys stable from P3 on, and treat unknown future fields as optional.
- Rate limiter cost for `PoleLinkRequest` must allow normal play (a click every few seconds) while blocking floods.

## 9. Temporary test code to remove in Phase 10
`/plink`, `/plinks` (P3), plus anything the multiplayer roadmap already lists.

## 10. Progress
One line per patch. `[ ]` not started, `[~]` applied, untested, `[x]` tested and done. Each patch marks itself `[~]`; a small follow-up patch records test results.

- [~] P1 Gold wire (patch_pole_p1_gold_wire.ps1: P1 patch written; applied, untested)
- [ ] P2 Pole tiles, entities, UI, visuals
- [ ] P3 Link data layer
- [ ] P4 Power across links
- [ ] P5 Hanging wire drawing (straight line)
- [ ] P6 Player input and preview
- [ ] P7 Cutting and refunds
- [ ] P8 Polish
- [ ] P9 Living wire (Luminance trail, wind, disturbance)

Starting a patch: "Implement pole patch P<n>." The patch reads this file from the fresh clone, checks that its prerequisites are marked done, states the authority and sync design first, and ends with a next-chat prompt for the following patch.

Next chat prompt (after P1): "Continue Factorraria power poles. Clone the repo and read docs/poles/ROADMAP.md (sections 1, 2, 5 P2, 6, 7 and the Progress section). State: P1 (gold wire) is applied, test results below if I give them. Record the P1 results with a small patch first (do not guess results I did not give), then implement P2 (pole tiles, entities, UI, overhang visuals, no links). State the authority/sync design first and check dependencies. Build must show zero compile errors before any test. I will tell you my decisions on open items 1 to 4 and give the connection point pixel coordinates once the pole art exists."