# Factorraria Multiplayer Survey - Appendix (version 2)

The eleven sub-reports behind `FACTORRARIA_MULTIPLAYER_ROADMAP.md`, in survey order. Steps 1-10 were written against commit `25510ef`; step 11 reviews the commits through `49f8da0` (2026-10-16).

Note: where a sub-report and the roadmap differ, the roadmap's section 6 ("Corrections found while compiling") and the version 2 section apply.


---

# Factorraria multiplayer survey - Step 1: ModSystems

Model assumed: **server-authoritative**. The server (or the host in single-player) runs every simulation. Clients only draw, send requests, and receive state. Every `static` registry in a ModSystem exists separately in each process, so each one needs either "server only" or "synced to clients".

Severity: **B** blocker (breaks or dupes in MP) / **H** high / **M** medium / **L** low.

## Cross-cutting findings
- **X1 (B)** The mod has no `HandlePacket`, no packet types, no `OnPlayerJoined`/world-join sync. Needed once, in `Factorraria.cs`, as a packet router + `ModPacket` helper. **Status:** router and helpers added in patch 1.1 (tested: ping round trip client-server-client works).
- **X2 (B)** Client-only hooks (`ModTile.PlaceInWorld`, `ModItem.UseItem`, `GlobalTile.PlaceInWorld`, UI clicks) mutate registries in the client process only. The server never learns. Every one needs "client sends request, server applies, server broadcasts".
- **X3 (H)** World data (`Save/LoadWorldData`) loads on the server only. Clients start with empty registries, so all registries that UI/draw code reads need a full sync on join plus deltas. **Status:** join-provider registry (`JoinSync`) added in patch 1.2 (applied, untested); the real providers arrive with 3.1, 3.2, 3.4, 7.2 and 9.2.
- **X4 (M)** Several ModSystems run `PostUpdateWorld` on clients too with no guard (VirtualItem, Cart). Each needs `if (Main.netMode == NetmodeID.MultiplayerClient) return;` or a client-side interpolation path.

## Per-system findings

| System | Status today | Problem in MP | Sev |
|---|---|---|---|
| `VirtualItemSystem` | No guard, static `virtualItems`, no IDs, no packets | Every client simulates its own conveyor items. `ConvertItemsToVItems` eats world items per-process, `CheckPlayerPickups` spawns items locally, so dupes/ghosts/desync. Needs server-only sim, stable vItem IDs, snapshot/delta packets, item removal via `NetMessage` item sync. | B |
| `CartSystem` | Hard-gated to `NetmodeID.SinglePlayer` (placement, input, `PostUpdatePlayers`); sim in `PostUpdateWorld` unguarded | Carts are not usable in MP at all. Needs server sim, cart IDs, placement/mount/interact packets, client interpolation, join sync. Biggest single item. | B |
| `CartChestUISystem` | Client UI only | Edits cart cargo/fuel locally; needs request packets and server validation. | H |
| `CustomWireSystem` | `WireGrid` static, mutated by `AddWire`/`RemoveWire` from item `UseItem` | Server never gets wires, so power grids never form on a dedicated server. Needs request packet, broadcast, join sync, `gridNeedsRebuilding` on server. | B |
| `PowerGridSystem` | Client skip present; sim server-side | Correct split already. Clients have no networks, so machine on/off state must come from machine sync (TileEntity step). `PowerGridDebugSystem` shows nothing on clients. | M |
| `LiquidNetworkSystem` | Client skip present | Same split. Need to verify how pipes reach `AllPipeTiles` on the server (`RegisterPipeTile` is called from `PipeTileBase.PlaceInWorld`, a client-side hook). Tank contents and network liquid type need sync for UI. | B (confirmed in step 7, P1) |
| `ChestConveyorSystem` | Server-only tick, `SyncChestItem` sent from server | `QueueProbe` is client-skipped, but its callers (`ChestConveyorGlobalTile`) run on the placing client, so the server never probes. `CycleMode` from the mode UI is local only. Entries need join sync + mode-change packet. | H |
| `ConveyorPhysicsSystem` | Runs on all sides | Pushes remote players, NPCs and items on every client, causing jitter/double push. Restrict: players = local only, NPCs = server (+`netUpdate`), items = follow vanilla (both sides). | M | **Patch 2.5 applied:** players = local player only plus `SyncPlayerNow` (tested, worked). NPCs and items failed in the test (not carried, jitter); **patch 2.5b** makes the server push both and send them with SyncNPC / SyncItem every 3 ticks, clients never push them (untested). |
| `RecipeKnowledgeSystem` | Static `unlocked`/`crafted`/`seen` sets | Server-held; clients' recipe book reads empty sets. Needs delta + join sync, and `MarkSeen`/parchment unlock become client-to-server requests. **Design decision needed:** world-shared (current) vs per-player knowledge. | H |
| `RecipeBookSystem` / `RecipeBookPlayer` UI | Client UI | Fine once knowledge syncs. | L |
| `MachineUISystem` | Client UI | Opens machine UI from local TileEntity data; needs open/close + slot-edit packets (UI step). | H |
| `TimedCraftingSystem` | Client, local player | Vanilla crafting is client-side, so likely fine. Verify result creation uses normal recipe path (ModPlayer step). | L |
| `AwakenedStatueSystem` | Client skip present | `QueueStatueCheck` is called from a client-side GlobalTile hook, so the server never queues. Awakened set must be synced for `AwakenedStatueDrawSystem` (draws nothing on clients today). **Status:** replica, join provider `awakened-statues` and `AwakenedStatuesChanged` deltas added in patch 3.4b (applied, untested). | H |
| `AwakenedSpawnSystem` / `AwakenedSpawnGlobalNPC` | Client skip present | Server spawning via `NPC.NewNPC` syncs itself. Looks correct; re-check in the NPC step. | L |
| `WorldLiquidSources` | Static cache | Reads world liquid; fine server-side, but cart code using it must run on the server. | L |
| `MachineRegisterationSystem`, `LiquidTypeRegistrationSystem`, `RecipeSystem`, `AwakenedStatueRegistry` | Load/PostSetupContent | Registration must be identical on server and client. Append-only liquid registry already helps. Needs a mod-version check; `LiquidTypeRegistry` indices travel in packets. | M |
| `MachineOverhangSystem`, `AwakenedStatueDrawSystem`, `LiquidNetworkDebugSystem`, `PowerGridDebugSystem` | Draw/debug, `dedServ` guards on some | Client-only; check `dedServ` guard on each. | L |
| `ChestLootSystem`, `SiliconOreWorldGen`, `PostWorldGenEdits` | Worldgen | Worldgen runs on the host only. No change expected. | L |
| `CartMountBlocker`, `MaterializerRightClickGuard` | Detours | Detours run on both sides; check they only act for the owning client (`Main.myPlayer`). Re-check in Globals/Content steps. | M |

## Existing partial net code (keep / extend)
`NetMessage.sendWater` (BucketBase, CartCargo), `SyncChestItem` (ChestConveyor), `HitSwitch` (Cart pressure plates, currently sent from client), `SendTileSquare` (MagicMaterializer), `Projectile.netUpdate` (MechanicalArmProjectile), `NetSend/NetReceive` on the two scroll items.

## Decisions (answered by Ismail)
1. Recipe knowledge: **world-shared** (server-held sets, synced to all clients).
2. Carts: **server-authoritative with client interpolation**.
3. Targets: **host-and-play and dedicated server** (so every `dedServ` and `netMode` guard must work for both). **To verify:** whether Host & Play runs the server in the same process or as a separate one. Plan the code to be correct either way: server logic gated by `netMode != MultiplayerClient`, client-only logic gated by `!Main.dedServ`.

## Remaining survey steps
2 ModPlayers, 3 GlobalItems/GlobalTiles/GlobalNPCs/other Globals, 4 BaseMachine + TileEntities, 5 Machine UIs and custom UI elements, 6 Carts (Common/Carts), 7 Liquids/Networks/Pipes/Motors, 8 Items/Projectiles/Mounts/Hooks/Weapons, 9 Statues, Knowledge, Crafting, Chest loot, 10 Config/commands/misc.


---

# Factorraria multiplayer survey - Step 2: ModPlayers

Decisions carried over: recipe knowledge is world-shared; carts are server-authoritative with client interpolation; targets are host-and-play and dedicated server.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## Background (how ModPlayer behaves in MP)
Every `Player` exists in every process (server and all clients). `ModPlayer` hooks like `PostUpdate`, `PreUpdateMovement` and `UseItem`-driven code run for **all** players, not just the local one. Anything that must happen once (spawning items/projectiles, reading input, editing inventory) has to be gated with `Player.whoAmI == Main.myPlayer`. State other clients need must go through `SyncPlayer` / `SendClientChanges` / `CopyClientState`, or projectile/item NetSend.

The mod has 6 ModPlayers and none of them uses `SyncPlayer`, `CopyClientState`, `SendClientChanges` or `OnEnterWorld` (except one Restore call).

## Per-class findings

| ModPlayer | What it does | Problem in MP | Fix | Sev |
|---|---|---|---|---|
| `MechSuitPlayer` (in `MechSuitItem.cs`) | Toggle via item `UseItem`; PreUpdateMovement spawns up to 3 arm projectiles, overrides velocity, drops gravity | (1) `UseItem` and `PreUpdateMovement` run for remote players too, so each client spawns duplicate arm projectiles for everyone. (2) Reads `PlayerInput.Triggers.JustPressed.Jump`, which is the **local keyboard**: pressing Space on your machine dismounts every active mech player you can see. (3) Arm `AnchorTile` and `ArmBendSign` live in `ModProjectile` fields that are never synced, so other clients draw arms pointing nowhere. (4) `arms[].ProjectileIndex` is a per-process index. (5) Remote velocity override fights the position sync. | Gate Activate/UpdateArms/launch/free-movement to the owner. Use `Player.controlJump` + `releaseJump` instead of `PlayerInput`. Add `NetSend/NetReceive` to `MechSuitArmProjectile` (anchor tile, bend sign) and `Projectile.netUpdate = true` after setup. Sync `IsActive` with `SyncPlayer`/`SendClientChanges` only if remote clients need it for drawing. Verify owner-side `Projectile.Kill` propagates. | H |
| `CartEquipPlayer` (patch 2.2, applied, untested: owner-only, then SyncEquipment with slot 58 + armor + dye + 2) | `PostUpdate` trims the minecart equip slot (`miscEquips[2]`) to stack 1 and hands the excess back | Runs for **every** player in every process. On the server and on other clients it edits a replica inventory and calls `GetItem`/`QuickSpawnItem` (world item spawn), so excess items get duplicated or spawned twice. Owner-side edits also need an equipment sync packet. | Gate to owner. After changing the slot call `NetMessage.SendData(MessageID.SyncEquipment, ...)` with the right slot index (verify index mapping against `Player.cs`/`NetMessage`). | H |
| `CartInteractionPlayer` | `CanUseItem` blocks item use when the cursor is over a cart; `ShiftClickSlot` pushes items into the cart UI | Both are deliberately disabled outside `NetmodeID.SinglePlayer` (so in MP clicking a cart swings your sword). `FindCartAtCursor` reads `CartSystem.Carts`, which will be a client replica after the cart phase. `TryShiftInsert` edits cargo locally. | Remove the single-player gate once carts replicate. `FindCartAtCursor` must read the replica. Insert becomes a request packet (server validates, moves the item, syncs). | H (cart phase) |
| `ExtractinatorBlockPlayer` | Temporarily flips `ItemID.Sets.ExtractinatorMode` for the held item while the local cursor is over an Extractinator | Owner-only and `dedServ`-guarded already. Static set edit is process-local, so it is safe. Relies on the vanilla right-click flow; the machine UI open must then go through the server (UI step). | None in the player itself. | L |
| `RecipeBookPlayer` | Saves the recipe book button offset per character | Client-side UI preference saved in the character file, which is correct. Nothing to sync. | None. | L |
| `TimedCraftingPlayer` | Local timed crafting; calls `Main.CraftItem` then `GetItem` | Owner-only and `dedServ`-guarded. Vanilla crafting is client-authoritative, so ingredient use and item grant sync through normal inventory sync. `deliveringCraft` flag lives on a ModSystem, per process, so it is safe. | Verify after the Globals step that no OnCraft hook writes mod state on the client only. | L |

## Related findings from grepping the callers
These belong to the **recipe knowledge** sync designed in the roadmap (world-shared):
- `RecipeParchmentItem` (line 58) calls `RecipeKnowledgeSystem.Unlock(key)` from the client's `RightClick`. Becomes: client sends request, server unlocks and broadcasts.
- `RecipeVisibility` (line 110) calls `MarkSeen(item.type)` from client inventory scanning. Becomes: client batches newly seen types and sends them; server adds and broadcasts.
- `BaseMachine` (line 294) calls `MarkCrafted` on the server side; needs a broadcast to clients.
- `RecipeDebugCommand` reads the sets locally; on a client it would show empty unless synced (or made server-side via `CommandType.World`).

## Missing player-level plumbing (to add in the roadmap)
1. A `FactorrariaPlayerSync` ModPlayer: `SyncPlayer`, `CopyClientState`, `SendClientChanges` for any per-player state other clients need (currently just the mech suit flag, optionally). **Status:** `FactorrariaPlayerSync` added with empty stubs in patch 1.2 (applied, untested).
2. `OnEnterWorld` hook: request the full world-state sync (knowledge sets, wires, chest-conveyor entries, awakened statues, carts, vItems) from the server. Pairs with the X3 finding in step 1. **Status:** `FactorrariaPlayerSync.OnEnterWorld` requests the state through `JoinSync` (patch 1.2, applied, untested).
3. An `OnLeave`/death reset for the mech suit so a disconnecting player does not leave arm projectiles behind. **Status:** `JoinSync.RegisterDisconnectHook` added in patch 1.2 (applied, untested); the mech suit cleanup registers in patch 2.1.

## Roadmap inputs from this step
- Phase "Player safety": owner-gating fixes for `MechSuitPlayer` and `CartEquipPlayer` (small, independent, good first PR).
- Phase "Join sync": `OnEnterWorld` request/response plumbing shared by every registry.
- Cart phase absorbs `CartInteractionPlayer`.


---

# Factorraria multiplayer survey - Step 3: Globals

Covers every GlobalItem, GlobalTile, GlobalNPC, GlobalProjectile, GlobalLiquid and GlobalWall (15 classes), plus the two detour hooks in `CartMountBlocker`.

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## Key engine facts used in this step
- `ModTile/GlobalTile.PlaceInWorld` is called from `Player.PlaceThing_Tiles` (confirmed in tModLoader's `Player.cs` patch: `TileLoader.PlaceInWorld(tileTargetX, tileTargetY, inventory[selectedItem])`). That is the placing player's own item-use path, so it fires on the **placing client** (and in single-player) and not on the server. Treat it as client-only.
- `KillTile` hooks run on the server and on every client that processes the tile kill, so side effects inside them (drops, TileEntity removal) run once per process unless guarded.
- Wiring (`PreHitWire`) is processed on the server, so server data is what these hooks see.
- Static data (e.g. `ItemID.Sets`, `SetDefaults` edits, loot rules) is built per process from the same mod code, so deterministic edits are safe on both sides.

## New cross-cutting finding
**G0 (B) - "tile placed" notification is missing.** Four systems depend on knowing when a player placed a tile (chest conveyor, liquid topology, awakened statues, machine/pipe placement), and all four only hear it on the placing client. One shared mechanism fixes all of them: a small client-to-server packet sent from a single `PlaceInWorld` hook (position + tile type), validated on the server (tile at that position matches, player is in reach), which then runs the same logic the hook runs today. Build it once in the net-plumbing phase. **Status:** `TilePlaced` packet, `TilePlacedGlobalTile` hook and the `ITilePlacedConsumer` registry added in patch 1.3 (applied, untested); the real consumers move over in patch 3.3.

## Per-class findings

| Class | What it does | Problem in MP | Fix | Sev |
|---|---|---|---|---|
| `MachineVisualOverride` (GlobalTile, `Common/Machines`) | Draw override, light, and machine lifecycle hooks | `PlaceInWorld` creates the machine's TileEntity **on the placing client only** (`def.GetEntity` -> `GetOrCreateEntity`), so the server never has the machine. `KillTile` has no netMode guard: it drops the machine's slot contents with `Item.NewItem` in every process that sees the kill (duplicates), and `GetEntity` can create an entity just to kill it. `RightClick` opens the UI locally from client-side TileEntity data. | Server-side entity creation (placement packet or the vanilla TileEntity placement message) and server-only drops (`netMode != MultiplayerClient`). Machine UI interactions become requests (UI step). Rework together with `BaseMachine` (step 4). | B |
| `ChestConveyorGlobalTile` | `PlaceInWorld` queues chest-conveyor probes | Returns immediately on the client, and `PlaceInWorld` never fires on the server, so the server never probes. Chest conveyors never register in MP. | G0 packet; server runs the existing probe logic. | H |
| `LiquidTopologyGlobalTile` | Marks pipe networks dirty on place/kill next to a pipe | `KillTile` marks the flag on the server (fine). `PlaceInWorld` marks it only on the placing client. Flag is a static, so the server's network goes stale after placements. | G0 packet. | H |
| `AwakenedStatueGlobalTile` | Awakened-statue wire guard + queues statue checks | `PreHitWire` runs on the server with server data (fine). `PlaceInWorld` is client-only, and `QueueStatueCheck` also returns early on clients, so a placed statue is never noticed. `KillTile` works on the server. | G0 packet. | H |
| `AwakenedSpawnGlobalNPC` | Tags statue-spawned enemies, holds them from despawning, rolls statue loot | `CheckActive` and `OnKill` run on the server where the tag lives; `OnKill` already skips clients; `Item.NewItem` on the server syncs. Tag and entry are per-process and not saved (same as today). Looks correct. | Verify in-game. | L |
| `MaterializerWireGuard` | Blocks wire toggling of the Shimmer Monolith when overridden | Runs on the server (where wiring runs) and depends on `MachineVisualRegistry.Definitions`, which is filled in `MachineRegisterationSystem.PostSetupContent`. That method has no `dedServ` guard, so the registry should exist on the server too. | Confirm the registration path touches no textures on the server (grep found none outside `LiquidTextureCache`, which is client-only). | M |
| `VItemGlobalItem` | `ConveyorImmunityTimer` on world items; Coal `maxStack = 9999` | The timer is per-process and not synced, which is fine because only the server will convert world items once `VirtualItemSystem` is fixed. Items a player drops start at 0 on the server, so they convert straight away (probably intended). Coal stack change is deterministic on both sides. vItem packets will need full item serialization (`ItemIO`/`NetMessage` item write) so prefix and mod data survive. | Note for the vItem phase. | M |
| `VanillaConveyorGlobalItem` | Shift+right-click swaps conveyor item direction by `SetDefaults` | Edits the inventory item locally; the server's copy of that slot keeps the old type unless the slot is synced. | After the swap, owner-only `NetMessage.SendData(SyncEquipment, ... slot)` (verify whether the vanilla right-click path already syncs the slot). | M | **Patch 2.6 applied (untested):** `FactorrariaNet.SyncInventoryItem`, also used by the two priority conveyor items. |
| `VanillaCartItemGlobal` | Cart items stack to 99, `CanUseItem` false, tooltips | Deterministic on both sides; real placement lives in `CartSystem`. | Placement moves to a packet in the cart phase. | L (cart phase) |
| `CartModuleItemGlobal` | Tooltips on chests/terrarium | Client display only. | None. | L |
| `IceBladeItem` (GlobalItem) | Turns Ice Blade into a projectile swing | `SetDefaults` is deterministic; `Shoot` runs on the owner; the projectile syncs normally. Check the swing projectile's own sync in step 8. | None here. | L |
| `BossLootGlobal` | Edits boss drop tables | Loot rules are built identically on every process and rolled on the server. | None. | L |
| `LiquidInteractionEdits` (GlobalLiquid) | Water + lava merge makes stone | Liquid simulation runs on the server; clients do not simulate it. Deterministic hook. Verify that the placed stone tile reaches clients (tile sync). | Verify in-game. | L |
| `GlobalWallOverride` | No-op override | Nothing. | None (could be deleted). | L |
| `CartMountBlocker` (ModSystem detours) | Cancels vanilla track right-click mounting | Both detours are gated to `Main.myPlayer`, so remote players are unaffected. | None. | L |
| `CartTrackHookBlocker` (GlobalProjectile) | Kills the grappling hook created by a track right-click | Gated to the owner. The hook may already be in the network spawn message when it is killed; verify other clients do not see a flash of it. | Verify in-game. | L |

## Dedicated-server startup check (found along the way)
No texture or `GraphicsDevice` access was found in registration code outside `LiquidTextureCache`. Still, the first dedicated-server run should be done early in the roadmap, because any missed client-only call at load time crashes the server before anything else can be tested.

## Roadmap inputs from this step
- Net-plumbing phase: packet router + `TilePlaced` request packet (G0) + one shared validation helper.
- Machine framework phase: `MachineVisualOverride` placement/kill rewrite with `BaseMachine` (step 4).
- Small fixes: conveyor item swap sync; verify `LiquidMerge` stone sync.


---

# Factorraria multiplayer survey - Step 4: BaseMachine and the machine TileEntities

Scope: `Common/Machines/*` (BaseMachine, ElectricMachine, IElectric, TileEntityHelper, MachineVisualOverride, MachineOverhangSystem) and all 12 machine TileEntities (Autohammer, Extractinator, MechanicalAssembler, Solidifier, GelBurner, DecayChamber, Furnace, Hellforge, IceMachine, LiquidDistillator, MagicMaterializer, MotorMK1).

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## Engine facts verified in the tModLoader source
- `TileEntity.Update()` is **not called for multiplayer clients** (documented on the hook). Machine simulation therefore already runs only on the server (and in single-player). That part of the design is right.
- `TileEntity.ID` is consistent between server and clients, and `ModTileEntity` has overridable `NetSend`/`NetReceive` (used for the join snapshot and for the `TileEntitySharing` message).
- tModLoader has a ready-made placement flow for ModTileEntity: `Generic_Hook_AfterPlacement` (client sends `SendTileSquare` + `TileEntityPlacement`), the server runs `NetPlaceEntityAttempt` -> `Place` -> `OnNetPlace()` (server-only hook) and broadcasts the entity.
- `ModTileEntity.IsTileValidForEntity` is only called on load and server placement.

## Findings

### M1 (B) Machine entities are created on the wrong side
- Vanilla-tile machines (Furnace, Hellforge, Autohammer, Extractinator, Solidifier, GelBurner, DecayChamber, IceMachine, LiquidDistillator, MagicMaterializer) get their TileEntity from `MachineVisualOverride.PlaceInWorld` -> `def.GetEntity` -> `TileEntityHelper.GetOrCreateEntity<T>`. That runs on the **placing client only**, so the entity gets a client-local ID and the server has no machine at all.
- `MachineVisualOverride.PreDraw` also calls `def.GetEntity(i, j)` on every draw, so a client that sees a machine tile with no entity silently **creates a phantom entity** from draw code.
- `BaseMachine.Hook_AfterPlacement` (used by the real ModTile machines: MotorMK1, MechanicalAssembler) has its multiplayer branch commented out, so the client calls `Place` locally with a client-only ID.
- **Fix:** server-authoritative placement. ModTile machines use tModLoader's own flow (`Generic_HookPostPlaceMyPlayer`, with the registration/`OnPlaced` work moved into `OnNetPlace`). Vanilla-tile machines use the shared "tile placed" request packet (step 3, G0): the server validates, calls `Place`, runs `OnPlaced`, registers with the power grid and liquid network, and broadcasts. Draw and light code may only *look up* an entity (`TryGetEntity`), never create one. **Status:** done in patches 4.2 (placement routes, client lookup-only) and 4.5 (bootstrap scan and `EnsureEntity`, lookup-only draw everywhere).

### M2 (B) Machine geometry is initialised from draw code
`InitializeMachine` (`MachineVisualOverride.PreDraw`) is the only place that sets `cornerPosition`, `MachineWidth/Height`, `MachineCenter` and `MachineInitialized`. `LoadData` restores the numbers but not the flag. On a dedicated server nothing is ever drawn, so `ScanVItems`, conveyor output release and anything gated by `MachineInitialized` (BaseMachine lines ~657 and ~683) never work there. **Fix:** a server-safe `InitializeGeometry()` (data from `TileObjectData`, which exists on the server) called from `OnPlaced`, `LoadData`/world load and `NetReceive`.

### M3 (B) No machine state reaches clients
Clients draw and show UI from fields that exist only on the server: `isOn`, `isWorking`, `WorkProgress`/`WorkFraction`, `CraftsFinished`, `SelectedRecipe`/`ManualGroup`, fuel burn fraction, slot contents, liquid tanks (type + amount, used by liquid overlays and tank UI), `InsideBiome`, Materializer `BackgroundActive`, motor facing. Without it: machines never animate, lights stay off, and UIs show empty machines.
**Proposed design (three channels):**
1. **Join snapshot:** full state in `NetSend`/`NetReceive` (slots, tanks, flags, geometry). tModLoader sends it with the world.
2. **Light updates:** a compact packet (isOn, isWorking, progress, crafts counter, tank type/amount for overlays) sent when changed, rate limited, only to clients near the machine.
3. **Detail updates:** full slot/tank contents sent only to players who have that machine's UI open (subscribe on open, unsubscribe on close/leave), plus after each accepted UI edit.
**Status:** channel 1 (the snapshot in `NetSend`/`NetReceive`) was implemented in patch 5.1 (see the Phase 5 block of the roadmap); channel 2 (light updates) was implemented in patch 5.2 (packet `MachineLightUpdate`, see the Phase 5 block of the roadmap); channel 3 (detail updates) was implemented in patch 5.3 (packets `MachineSubscribeRequest`, `MachineDetail`, `MachineDetailClosed`, server-side subscriber list in `MachineDetailSystem`, see the Phase 5 block of the roadmap).

### M4 (H) Interaction is local
`BaseMachine.OnRightClick` opens the UI from client-side data. `MagicMaterializerTileEntity.OnRightClick` flips `BackgroundActive` locally, but the server's `Update` is what reads it, so the toggle does nothing in MP. `HasShimmer` also reads a tank the client does not have. **Fix:** right-click becomes "request open" (server subscribes the player and sends detail state); Materializer toggle becomes a request packet validated on the server. UI edits are step 5.
**Status (patch 6.4, applied, untested):** the toggle half is done with `MaterializerToggleRequest` / `MaterializerBackgroundChanged` (`Common/Net/MaterializerToggle.cs`): the client sends the wanted value (no local flip), the server validates reach and shimmer in the tank (`HasShimmer` is evaluated on the server, where the real tank is), sets `BackgroundActive` and broadcasts it to all clients; the snapshot already carries the value for replicas that arrive later. The open half was done in 6.1.

### M5 (H) `InsideBiome` uses the local player
`DetermineBiomeAt` reads `Main.LocalPlayer` and returns Unknown on a server, so biome water (Snow Water etc. in `ResolveIncomingLiquid`) cannot work. **Fix:** the placing client computes the biome and includes it in the placement request (simple, and the value is already saved in `SaveData`), or a server-side tile scan. **Status:** the placing-client option was implemented in patch 4.3 (packet `MachineBiomeReport`, server state in `MachinePlacementInfo`, see the Phase 4 block of the roadmap). Still open: machines placed by server code (Robotic Arm) and bootstrapped machines keep Unknown. `InsideBiome` is in the entity snapshot since patch 5.1, and the server re-shares the machine once the reported biome is applied.

### M6 (H) `KillTile` / `OnKill` side effects
`MachineVisualOverride.KillTile` drops slot contents and kills the entity in every process that sees the kill (details in step 3). `BaseMachine.OnKill` only flips static flags (fine on the server) and calls `MachineUISystem.NotifyMachineKilled`, which must also close the UI for subscribed clients. **Status:** the kill-path half was implemented in patch 4.4 (`MachineRemoval.OnTileKilled`, server-only drops, replica-only removal on clients, `fail`/`effectOnly` guard, UI liveness check). Closing the UI for subscribed clients from the server was implemented in patch 5.3 (`BaseMachine.OnKill` calls `MachineDetailSystem.NotifyMachineKilled`, which sends `MachineDetailClosed` to every subscriber; the client closes the UI in `MachineUISystem.UpdateUI`). `ReleaseHiddenIngredientSlots` uses `Item.NewItem` from `Update`, which is server-only and therefore correct.

### M7 (M) Power state sync
`isOn` is written by `PowerNetwork` (server). `useFraction`, `isOn` and `isWorking` feed the electricity icon/UI and animation, so they belong in the light update (M3). `PowerGridSystem.RegisterMachineToMasterList` is currently only called from `Hook_AfterPlacement`; the server placement path must call it, and after load `InitializeMachinesList` already rebuilds from `TileEntity.ByPosition`. **Status:** `isOn`, `isWorking` and `useFraction` travel in the light update since patch 5.2; grid formation on the server was checked in patch 5.4: wire edits (3.1), client placement (4.2, `OnNetPlace`), the 4.5 bootstrap and kills (`OnKill`) all set `gridNeedsRebuilding` or register the machine. One gap was fixed: the one-time index of saved machines ran only when the machine list was empty, so a machine registered earlier (bootstrap scan, early placement) made the server skip the saved ones; it is now a once-per-world flag (`machinesIndexed`). The server logs a `[Power] rebuilt` line after each rebuild.

### M8 (M) Visual effects are draw-driven (this is fine)
`OnAnimationFrameChanged` (dust, gore, sounds in Furnace, GelBurner, MechanicalAssembler, etc.) is triggered from `PreDraw`/`NotifyAnimationFrame`, so it runs only on clients and uses `isOn && isWorking`. Once M3 syncs those two flags the effects work with no change. `Main.netMode == Server` early-outs already present are harmless. **Status:** the two flags reach clients in the light update since patch 5.2, so no effect code changed.

### M9 (M) Machine-specific notes
- **Extractinator:** server `Update` spawns vItems directly; correct once `VirtualItemSystem` is server-authoritative (step 1). `ExtractinatorBlockPlayer` (step 2) stays local.
- **Hellforge:** refills its lava tank in `Update` (server only, fine); tank state needs M3.
- **GelBurner / Autohammer:** `Update` runs on the server; no extra work beyond M3/M7.
- **MagicMaterializer:** `Update` changes tiles and already calls `SendTileSquare` when `netMode == Server` (good). `OnKill` calls `AwakenedStatueSystem.RescanFor` (server only, fine). Needs `BackgroundActive` in snapshots and a toggle packet (M4).
- **Motors:** pump/network behavior is in the liquid step (7); `Facing` goes into the snapshot.
- **Furnace, DecayChamber, IceMachine, LiquidDistillator, Solidifier, MechanicalAssembler:** thin subclasses, no MP-specific code of their own; they inherit everything from the `BaseMachine` fixes.
**Status (patch 5.1):** the snapshot covers every machine subclass: the `BaseMachine` fields for all of them, plus extras for the motor (`Facing`), the Materializer (`BackgroundActive`) and the Robotic Arm (`AreaX/Y/W/H`). The Extractinator, Hellforge, Gel Burner and Autohammer logic stays server-side and needs nothing more than the 5.2 light updates.

### M10 (M) Recipe knowledge hook
`FinishRecipe` calls `RecipeKnowledgeSystem.MarkCrafted` on the server; the comment there says netcode was deferred. It needs the knowledge broadcast from step 1.

### M11 (L) Validation
Every machine request (open, edit slot, toggle, select recipe) must be checked on the server: the entity exists, the player is within reach, and slot indexes/stack sizes are valid. A single `MachineRequest` helper can do this for all machines.

## What does not need to change
The `BaseMachine` framework hooks (`Recipes`, fuel tables, `WorkDuration`, `CanStartCraft`, `OnRecipeFinished`) and the recipe registries are pure data/logic that run identically wherever the server runs them. The thin-subclass pattern means almost all MP work lands in `BaseMachine`, `MachineVisualOverride` and one packet module, not in the 12 machines.

## Roadmap inputs from this step
- Phase "Machine foundation": M1 + M2 together (server-side creation and geometry, remove draw-time creation).
- Phase "Machine sync": M3 snapshot + light + detail channels, M7.
- Phase "Machine interaction": M4, M5, M6, M11 (with the UI step).
- A new `Common/Net/` folder: packet router, `MachineSync`, `MachineRequest`, `TilePlaced`.


---

# Factorraria multiplayer survey - Step 5: UI (machine UIs, custom elements, recipe book, chest-conveyor UI)

Scope: `MachineUISystem`, `MachineUIStateBase`, `MachineUIRegistry`, the 10 `<Machine>UIState` classes, `UIItemSlotWrapper`, `Common/UI/CustomUIElements/*` (ElectricityUIIcon, FireUIElement, LiquidTankUIElement, OutputSlotAnimation, RecipeSelectHammerIcon / RecipeBrowserPanel), `Common/UI/RecipeBookUI/*`, `ChestConveyorModeUI`, plus UI-adjacent input code found along the way (priority conveyor right-click, `MaterializerRightClickGuard`).

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## How the UI works today (the part that matters for MP)
- One shared `MachineUIStateBase` instance per tile type; `CurrentEntity` points to the machine being viewed.
- Slots are `UIItemSlotWrapper`s bound by getter/setter lambdas **directly to the live `Item` objects** in `CurrentEntity.InputSlots/OutputSlots`. A click runs vanilla `ItemSlot.Handle(ref item, context)` against the machine's own item, so the client edits the machine's data in place.
- Tanks, fuel and progress elements read live machine fields every frame (`InputLiquids`, `Fuels[i].Remaining`, `WorkFraction`, `CraftsFinished`, `useFraction`).
- The recipe browser calls `CurrentEntity.SetManualGroup(group)` straight from a click.
- Closing happens only when the inventory closes or the machine is killed. There is no reach check.

## Findings

### U1 (B) Slot edits are local only, and a naive fix would dupe items
Slot clicks change the client's replica; the server never hears about them, and the server's own tick (conveyor intake, output release, crafting consuming ingredients, producing outputs) changes the same slots at the same time. Two ways to fix it; this needs a decision (see "Decisions needed"):
- **Option A (recommended): vanilla-chest style.** The client predicts with `ItemSlot.Handle` as today, then sends a `SlotEdit` packet (machine id, slot kind, index, **expected old item** and **new item**). The server applies it only if the slot still matches the expected old item (compare-and-swap), validates the machine's accept filter and stack limits and the player's reach, then broadcasts to subscribers. On a mismatch it rejects, resends the authoritative slot, and the client rolls back (including the item on the cursor, via an equipment sync to that player). Keeps all current click behavior (shift, right-click hold, favorites) because vanilla code still does the clicking.
- **Option B: server transactions.** Client sends only "clicked slot X with button Y"; the server computes the swap on the player's synced cursor item. Cleaner for anti-dupe, but `ItemSlot.Handle` behaviors (split, hold-to-repeat, shift, favorites) would have to be re-implemented on the server and the UI feels laggy.
Either way the slot edit must also be re-sent to the other players who have the same machine open.
**Status (patch 6.2, tested 2026-10-19, working):** Option A is implemented. `UIItemSlotWrapper` keeps vanilla doing the clicking (predicted), then `MachineSlotEdit.SendEdit` sends machine corner, slot kind and index, the expected old item and the new item. The server (`MachineSlotEdit.ApplyOnServer`) validates with `MachineRequest.Validate`, requires a subscription (`MachineDetailSystem.IsSubscribed`), compares the slot with the expected old item (type, stack, prefix), checks the new item (valid type, stack within `maxStack`) and `BaseMachine.AcceptsSlotItem`, writes the slot, calls `MachineDetailSystem.Push` and answers. A reject answers with the authoritative slot and the client rolls the slot and the cursor item back. Shift-click is sent as a quick take and the client receives the items only on accept. The server's own conveyor intake, output release and crafting run on its tick; an edit is applied between ticks, so a race ends as a reject, never as created or lost items. Known limit: the cursor is only rolled back when it still holds what the click left there.

### U2 (B) The UI binds to objects that go stale
A replicated TileEntity can be replaced when a sync arrives (a new instance is read and stored in `ByID`), so `CurrentEntity`, and `MachineUISystem.NotifyMachineKilled`'s reference comparison, would point at a dead copy. **Fix:** the UI stores the machine's tile position/ID and re-resolves the live instance from `TileEntity.ByPosition` each frame; if it no longer exists, it closes.
**Status:** done in patch 6.1 on a multiplayer client: `MachineUISystem` keeps the open machine's position, rebinds `openState.CurrentEntity` to the live replica every frame (so `OpenMachine`, A9, is live too) and closes only when the replica is gone.

### U3 (B) No server subscription / detail data
The UI reads full machine state (slots, tanks, fuel, selected recipe) that clients only have in coarse form (step 4, M3). Opening the UI must send an "open machine" request; the server subscribes that player and sends the detail snapshot, then updates while open; closing (or disconnecting, or moving out of reach) unsubscribes.
**Status:** the snapshot half of U3 is patch 5.1 (the replica has the slots, tanks, fuel and recipe pick whenever the entity is delivered); the server-side subscription and the detail channel are patch 5.3 (a thin `MachineSubscribeRequest` sent by `MachineUISystem` on open and close, the subscriber list, `MachineDetail` packets with the exact state); the rest of the open request (EnsureEntity, one validation helper, the UI re-resolving the live replica) is 6.1. **Status (6.1):** done: `MachineRequest.Validate` (Common/Net) is the shared validation and creates a missing entity through `EnsureEntity`; the subscribe request uses it; a client without a replica asks the server and opens the UI when the replica arrives.

### U4 (H) Recipe selection is a direct call
The recipe browser's `SetManualGroup` becomes a `SelectRecipe` request (machine id + stable recipe-group key). The server checks the group exists in that machine's `Recipes`, applies it, and syncs `ManualGroup` to subscribers. Recipes must be identified by **stable keys** (`RecipeCatalog.KeyFor`, liquid name, output item name), never by list index, because the registries are built separately on server and clients in `PostAddRecipes` and could differ in order. The same applies to syncing `SelectedRecipe` (needed on clients to compute the progress bar duration via `RecipeDuration`).
**Status (patch 6.3, applied, untested):** done with `MachineSelectRecipeRequest` / `MachineSelectRecipeResult` (`Common/Net/MachineRecipeSelect.cs`). The wire key is `RecipeCatalog.KeyFor(machine name, group)`, resolved on the server against the machine's own recipe book; `ManualGroup` and `SelectedRecipe` already reached clients through the 5.3 detail (`MachineSnapshot.WriteRecipes` / `ReadRecipes`), so the pick follows to every subscriber through `MachineDetailSystem.Push`. A reject answers with the real key and the client restores it.

### U5 (H) Chest-conveyor mode button
`ChestConveyorModeUI` reads `Main.chest[player.chest]` (chest index and contents are already replicated by vanilla) and calls `ChestConveyorSystem.CycleMode(entry)` locally. It needs (1) a replica of the entries (step 1), and (2) `CycleMode` turned into a request packet; the server changes the mode and broadcasts it. The UI should then draw from the replica instead of mutating it.

### U6 (H) Priority conveyor filter
`ClockwisePriorityConveyorTile.RightClick` (and the counter-clockwise twin) call `VirtualItemSystem.SetConveyorFilter` from the client, with the held item as the filter. The server never learns the filter. Also `PlaceInWorld` (client only) calls `OnPriorityConveyorPlaced`, so the server never builds networks for newly placed priority conveyors. **Fix:** request packet for the filter (item type), the shared "tile placed" packet (step 3, G0) for placement, and filter data included in the conveyor join sync. Fully covered with the tiles in step 8.
**Status (patch 7.3 and 7.3b, tested):** the filter is set through `ConveyorFilterRequest` (server validates reach, tile and item type); the server broadcasts and joins send every filtered network (filter + tiles), and clients draw the filter icons from that replica.

### U7 (M) Reach and lifetime
There is no distance check, so a UI stays open while the player walks away or dies, and in MP a server-side subscription would leak. Add: close when the player is out of tile reach, dead, or the machine is gone; server-side unsubscribe on leave/death/reach and cap the number of subscribers per machine.

### U8 (M) `ItemSlot.Handle` with `ChestItem` context outside a chest
Slots use `ItemSlot.Context.ChestItem`. Vanilla chest slots send chest sync messages using `Player.chest`, which is -1 here. Verify (in tModLoader's `ItemSlot`/`Player` source, since `ItemSlot.cs.patch` only shows diffs) that no `SyncChestItem` is sent with chest index -1; if it is, either use a different context or block it, because the server could log errors or process a bogus chest index.
**Status (confirmed in testing, interim fix applied):** on a dedicated server with one client, clicking a machine slot (the Robotic Arm input) threw `IndexOutOfRangeException` at `ItemSlot.Handle` in `UIItemSlotWrapper`. The wrapper now temporarily reports netMode as single-player around the call. Edits stay on the client copy until 6.2 (`SlotEdit`) replaces the workaround.
**Status (6.2, tested 2026-10-19, working):** the shield stays, because the chest-slot sync for `Player.chest` = -1 sits inside `ItemSlot.Handle` and cannot be switched off; it is now one helper, `UIItemSlotWrapper.RunVanillaClick`, wrapped around that one call only. What changed is that machine slot edits are no longer silent client-side edits: they are sent as `SlotEdit` requests. Cart slots keep the shield alone until Phase 9.

### U9 (M) `MaterializerRightClickGuard`
It restores the monolith's tile frames after vanilla toggles them on right-click. If vanilla already sent the toggled frame to the server before the guard undoes it locally, the server and other clients would see the monolith flip while the local client shows it unchanged. **Fix:** prevent the vanilla interaction for the Materializer tile (skip `orig`) instead of undoing it, and keep the existing `PreHitWire` guard on the server (step 3).
**Status (patch 6.4, applied, untested):** `MaterializerRightClickGuard` now skips `orig` for the Materializer's tile (tile type plus the visual registry, no entity needed), calls `TileLoader.RightClick(x, y)` itself (what vanilla calls at the end of `TileInteractionsUse`, so `MachineVisualOverride.RightClick` and the machine's `OnRightClick` still run) and sets `Player.tileInteractionHappened`. The server's `SyncMonolithFrames` and `MaterializerWireGuard` stay as the backstop.

### U10 (M) Output slot animation
`OutputSlotAnimation` plays a finish sound when `CraftsFinished` changes. With replicated state the first snapshot after opening must not count as a change (initialize the baseline from the first sync), otherwise every open plays the sound.
**Status (6.2, tested 2026-10-19, working):** `OutputSlotAnimation` waits for the first detail packet of a UI session on a multiplayer client (`LastDetailTick >= session start`, or two seconds) before it trusts the craft counter, so opening a UI no longer plays the finish sound.

### U11 (L) Read-only elements
`ElectricityUIIcon`, `FireUIElement`, `LiquidTankUIElement` and the tooltip parts of the recipe browser only read machine data and set `mouseInterface`; they work once U3 provides the data.

### U12 (L) Recipe book
`RecipeBookSystem` and the book panel are client-only. They depend on world-shared knowledge being replicated (step 1) and on `RecipeVisibility.MarkSeen` becoming a request (step 2). `RecipeBookPlayer` stores the button offset in the character file, which is correct. `RecipeBookPlayer`'s settings need no sync.

## Decisions needed
1. **Slot edit model:** Option A (optimistic + compare-and-swap) or Option B (server transactions)? Recommendation: A.
2. **Subscription radius:** is "close UI when farther than normal tile reach" acceptable? Recommendation: yes.

## Roadmap inputs from this step
- Phase "Machine interaction": U1, U2, U3, U4, U7, U8, U10 with the machine requests from step 4 (one `MachineRequest` packet family: Open, Close, SlotEdit, SelectRecipe, Toggle).
- Phase "Conveyors": U5 and U6 with VirtualItem and chest-conveyor sync.
- Small: U9 (Materializer right-click) and U12.


---

# Factorraria multiplayer survey - Step 6: Carts

Scope: `Common/Carts/*` (CartSystem, Cart, CartChain, CartCargo, CartChestUI, CartGeometry, CartSkinTable, CartSkins, TrackData, CartMountBlocker, CartEquipPlayer, CartInteractionPlayer) and `Content/Items/Carts/*`. Items from steps 1-3 that touch carts are cross-referenced, not repeated.

Decisions carried over: world-shared recipe knowledge; **carts server-authoritative with client interpolation**; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## Why the approach works for this code
`Cart` is documented as "pure data: not an NPC, projectile or mount". It follows the rail with wheel probes, collides with tiles, and does not carry players or depend on player movement. Physics needs only `Main.tile` and the vanilla minecart tables, so the whole simulation can stay on the server unchanged, and clients only need to draw interpolated positions. No client-side prediction is needed.

## Current state
- Every player-facing entry point is hard-gated to `NetmodeID.SinglePlayer`: `CartSystem.CheckPlacement`, `PostUpdatePlayers` (all click handling), and `CartInteractionPlayer`.
- `PostUpdateWorld` simulates `CartSystem.Carts` on whichever process has carts. Carts load only on the server, so the simulation effectively runs only there, but clients never learn about any cart (empty list), so nothing is drawn or clickable.
- Carts have no stable ID (they are found by list position or object reference); chains use a save-time `SaveId`.
- Code written with MP in mind already exists in two places: the pressure-plate hit (`Cart.CheckPressurePlate`) and `CartCargo.SendWater`. Both assume a **client** simulation, which is the wrong side for this design (see C6).

## Findings

### C1 (B) Cart replication layer is missing entirely
**Status (patch 9.2, applied, untested):** implemented in Common/Net/CartNet.cs as three packets instead of four: `CartSubscribeRequest` (sent after the join sync; the first delta after it replaces the join provider), `CartDelta` (full state of carts that entered the radius or changed module, module item, tank, burning flag or chain place, plus removals) and `CartMove` (motion, at `CartUpdatesPerSecond`, only for carts whose motion changed). Chain membership travels as the motor's cart id plus slot. Chest contents, fuel slots and the bump / pour events are not part of 9.2 (9.4, 9.5, 9.6). The client tick runs from `PostUpdateEverything` because `PostUpdateWorld` does not run on a client.

Add a stable `Id` to every cart (monotonic counter, saved and restored), plus these server-to-client messages:
- `CartSpawn` (full state: id, skin key, module, module item, position, velocity/speed, rotation, onTrack, facing, chain id + slot, tank type/amount, fuel burn fraction, filters) and `CartRemove(id)`.
- `CartMove` (id, position, speed, rotation, onTrack, facing, anim frame), sent at a fixed rate (about 15/s) **only for carts near each player** (interest radius around two screens); the client buffers about 100 ms and interpolates. A cart entering a player's radius gets a `CartSpawn`; leaving it gets a `CartRemove` for that client only.
- `CartModuleState` on change (module, tank, fuel burning, filters, motor state).
- Join sync: the full list in range via the shared `OnEnterWorld` request (step 2).
Skin is sent as the stable **skin key string** (not the item type), module item as item type.

### C2 (B) Simulation guard
**Status (patch 9.1, applied, untested):** `CartSystem.PostUpdateWorld` runs `ClientInterpolate()` on a multiplayer client (only `Cart.ClientStep()` on each replica) and `ServerSimulate()` everywhere else; `Cart.Update`, `CartCargo.Update` and `CartChain.UpdateCarriages` also return at once on a client. Replicas are fed through `Cart.ApplyNetState` (patch 9.2 sends the states). Chain id and slot for the coupling visuals are part of 9.2.

`PostUpdateWorld`, `CartChain` updates and `CartCargo.Update` must run only when `Main.netMode != MultiplayerClient`. Clients get a separate light `PostUpdateWorld` that only advances interpolation. Carriage positions are computed by the server (`CartChain.UpdateCarriages`), so clients draw them from synced positions; the client only needs chain id and slot for coupling visuals.

### C3 (B) Input becomes requests
**Status (patch 9.3, applied, untested):** place, install module, shove and pick up are implemented in Common/Net/CartInputNet.cs as one request packet with a kind byte (`CartActionRequest`) and one answer (`CartActionResult`); the client consumes the item when a place or install is accepted. The single-player gates in `CheckPlacement`, `PostUpdatePlayers`, `TryGetHologram` and `CartInteractionPlayer.CanUseItem` are gone (the hologram uses `TrackData` on the client). Panel open/close, fuel add, shift-insert and filter changes move to 9.5 because they need the synced contents; the pickup reward is spawned by the server (`QuickSpawnItem`).

All click handling stays on the owning client (it needs `Main.mouse*` and the held item) but now sends requests; the single-player gate is removed and `FindCartAtCursor` reads the replica list:
- `CartPlace` (skin item type, tile x/y): the server re-runs `CheckPlacement` (range, overlap, rail), verifies the player's held item, spawns the cart, and acknowledges so the client consumes the item. A reject must leave the item untouched.
- `CartInstallModule`, `CartShove`, `CartPickUp`, `CartPanelOpen/Close`, fuel add, shift-insert, filter changes: each with server-side validation (cart exists, player in reach, item/slot sanity).
- Pickup returns items via server `Item.NewItem` at the player (auto-synced), or `QuickSpawnItem` on the server.
- The placement hologram (`DrawPlacementHologram`) is a client preview and needs `TrackData` loaded on the client too.

### C4 (H) Cart cargo touches shared world state
**Status (patch 9.4, applied, tested 2026-10-23: works):** cart code only runs on the server or in single-player, so the cargo changes are made on the authoritative side and 9.4 only checks that they reach the clients. Dropped items a cart takes from go through the one helper `CartCargo.FinishWorldItemPickup` (item switched off when empty, then `VirtualItemSystem.SyncWorldItem`, which sends vanilla's SyncItem; this sync existed since 7.4). `CartCargo` has no `Item.NewItem` or other `Main.item` write: spills are vItems (7.2 replication) and cart pickup items spawn through `QuickSpawnItem` on the server (9.3). `PickUpConveyorItems` and the fuel vItem loop edit the server's real vItems and `VItemNet.ServerTick` sends the new stack or removal. `OnBump` and `PourTank` have explicit "not on a client" guards. Liquid: see C6. R2 and R3 are re-tested with carts by the 9.4 checklist.
- `PickUpConveyorItems` takes virtual conveyor items; correct once `VirtualItemSystem` is server-authoritative (step 1).
- `GetWorldItemCandidates` scans `Main.ActiveItems`. On the server, removing a picked-up world item must also broadcast the item change (set inactive and sync the item), otherwise clients keep a ghost item.
- `SpillChest` and fuel pickup create or remove world items; they must run only on the server (`Item.NewItem` there auto-syncs).
- `DrainWorldLiquid` and `PourTank` edit tile liquid and call `WorldGen.SquareTileFrame`; the change must reach clients (C6).

### C5 (H) Chest panel edits (cart UI)
`CartChestUIState` edits `Cart.ChestItems`, `Tank` and fuel locally through the same `UIItemSlotWrapper` approach as the machine UIs. Use the same model decided in step 5 (Option A: optimistic edit + compare-and-swap `SlotEdit` request, server validates `SlotAccepts`/filters/stack, broadcasts to subscribers), with a subscription so panel contents are streamed only to the player who has it open. UI must re-resolve the cart by **id** each frame (not by reference), and close when the cart is gone or out of reach.

### C6 (H) Two existing net branches point the wrong way
**Status (patch 9.4, applied, tested 2026-10-23: works except the plate click, see 9.4b):** `CartCargo.SendWater` now returns in single-player and otherwise calls `LiquidBroadcastSystem.Mark(x, y)` (8.1) instead of the client-only `NetMessage.sendWater`; the server sends the liquid change (vanilla water message, or a 1x1 tile square for a custom liquid) with the per-tick cap. `Cart.CheckPressurePlate` calls `Wiring.HitSwitch` directly and the dead client branch is removed. Test 2026-10-23: the plate track made no click for a client (the server has no audio); patch 9.4b sends it through `CartSound` (`CartSoundKind.PressurePlate`). Whether a wired lever needs vanilla's HitSwitch message for a client-side effect was not reported separately and stays open.
- `CartCargo.SendWater` only sends when `netMode == MultiplayerClient`. With a server simulation the server must broadcast liquid changes, so the guard becomes "not single-player" (`NetMessage.sendWater` handles both directions).
- `Cart.CheckPressurePlate` sends `HitSwitch` from a client and calls `Wiring.HitSwitch` directly otherwise. With the server simulating carts, the server takes the direct branch, which is correct; the client branch simply becomes dead code and can be removed to avoid confusion.

### C7 (M) `TrackData` on a dedicated server
`TrackData.EnsureLoaded()` reads private vanilla minecart tables by reflection (`_tileHeight`, `_trackType`, `_boostLeft`) and retries silently when they are null. It must be verified that vanilla initialises these on a dedicated server (if they are filled by a client-only load path, carts silently stay disabled there). Also `ReportFailureOnce` uses `Main.NewText`, which should become a logger-only message on a server.

### C8 (M) Chains and bumps
**Status (patch 9.6a, applied, tested 2026-10-22: everything works):** the bump, cart-vs-cart, chain coupling and terrarium pour sounds all go through `CartSound.Play` (Common/Net/CartSound.cs). Single-player plays them locally; a dedicated server sends packet `CartSound` (45) to the players within 80 tiles, rate limited per kind; a client plays it in `CartSoundSystem.PostUpdateEverything`. Chain id and slot for the link drawing were already done in 9.2 (CartDelta). Not done: bump spill and pour visual effects, only the sounds.

`CartChain` (motor + carriages, `AutoAttach`, `RebuildFromSave`) and `Cart.TryCollide`/`Bumped` events run purely in the server simulation. The client needs only chain id/slot and the bump event for visuals/sound. Bump sounds (`SoundEngine.PlaySound` in `CartCargo`) are no-ops on a server, so they need a small `CartEvent` broadcast (bump, pour) for nearby clients.

### C9 (M) Save/load and IDs
**Status (patch 9.1, applied, untested):** every cart has `Cart.Id` (saved as `id`), the counter `nextCartId` is saved as `nextCartId` (also with no carts left) and only goes up; on load it is raised above every saved id, carts without an id (older saves) and duplicate ids get fresh ones in list order, and the load logs `[Carts] loaded N cart(s), next id X, Y id(s) assigned on load`. `CartSystem.FindById(id)` is the lookup. `Cart.SaveVersion` was not bumped: a missing `id` reads as 0.

`SaveWorldData`/`LoadWorldData` already work on the server. Add `Id` (and the next-id counter) to save data; old saves assign ids on load.

### C10 (L) Items and tooltips
`CartModuleItemGlobal`, `VanillaCartItemGlobal` (step 3) and `CartMountBlocker` (owner-gated detours) are fine as they are. `CartEquipPlayer` and `CartInteractionPlayer` are covered in step 2.

## Roadmap inputs from this step
- Phase "Carts": C1-C3 form the core (identity + replication + requests), then C4-C6 (cargo, panel, water/plates), then C7-C9 (verification and polish).
- Depends on: net-plumbing phase (packet router, join sync), the VirtualItem phase (C4), and the shared slot-edit model (C5).
- Tunables to set at implementation time: update rate (default 15/s), interest radius (default two screens), interpolation buffer (default 100 ms).


---

# Factorraria multiplayer survey - Step 7: Liquids, pipes, motors, networks

Scope: `Common/Liquids/*` (PipeTileBase, PipeConnectionHelper, MotorTileBase, MotorTileEntityBase, LiquidStack, LiquidNetworkRegistries, LiquidTextureCache, WorldLiquidSources, LiquidInteractionEdits), `Common/Networks/*` (LiquidNetwork, PowerNetwork, PriorityConveyorNetwork), `Common/Systems/LiquidNetworkSystem`, the custom liquids (`FuelLiquid`, `OilLiquid`), the buckets, `PipeMK1Tile/Item`, `MotorMK1Tile/TileEntity/Item`.

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## What is already right
- `LiquidNetworkSystem.PostUpdateWorld` skips clients (step 1), so the simulation is already server-only.
- Network topology is decided by `PipeConnectionHelper.CanConnect` (tile adjacency + entity lookup), **not** by the pipe's stored `TileFrameX`. Pipe sprite frames are purely visual (`ReframeSelf` runs in `NearbyEffects` on each client), so stale frames on the server do not matter.
- Liquid type ids come from `LiquidTypeRegistry` (append-only) and tModLoader's own `ModLiquid` ids; both are identical on server and clients for the same mod version.
- Buckets follow vanilla's pattern (client edits the tile, then `NetMessage.sendWater`), so they already sync.

## Findings

### P1 (B) The server never learns about new pipes (and loses them on save)
`PipeTileBase.RegisterPipeTile` is called from `PlaceInWorld`, which fires only on the placing client (step 3). `UnregisterPipeTile` is in `KillTile`, which runs on both. Result in MP: pipes placed by players never enter the server's `AllPipeTiles`, networks never include them, and since `SaveWorldData` writes `AllPipeTiles` from the server, **they are also missing after a reload**. `MotorTileBase.PlaceInWorld` only sets rebuild flags on the client for the same reason.
**Fix:** the shared "tile placed" request packet (step 3, G0) makes the server call `RegisterPipeTile` / set the rebuild flags. As a safety net, `LoadWorldData` should drop saved positions whose tile is no longer a pipe and re-add pipe tiles found by a one-time scan, so worlds already damaged by earlier tests heal themselves.

### P2 (B) World liquid changes made by pipes are never sent to clients
`LiquidNetwork.WithdrawFromWorldTile` and `DepositToWorldTile` edit `tile.LiquidAmount`/`LiquidType` and call `WorldGen.SquareTileFrame`, but nothing broadcasts the change (the file has no network calls). Clients would see pipe-pumped liquid frozen or wrong until a section resync. **Fix:** after each edit the server calls `NetMessage.sendWater(x, y)`. Because the network changes many tiles per tick, collect changed positions in a per-tick set and send each once, with a cap per tick so it cannot flood (leftovers go next tick).
**Test result (Ismail, 2026-10-21):** the broadcast works (the client sees the pool change). Separate open issue R9: a pool and pipe network built on a client did not feed the machine until the world was saved, loaded in single-player and then joined again on the dedicated server; tracked as 8.1b (verification item 25), cause not known, hypotheses in the Progress entry.
**Status (patch 8.1b, applied, diagnostics only):** the code was read for every route by which the server learns about pipes, motors, machines and world liquid (list in the 8.1b Progress entry) and none of the three hypotheses holds up; no fix was written. `/fpipes [scan]` (Common/Net/LiquidPipeDiagnostics.cs, temporary) and a richer `[Pipes] networks rebuilt` log line show what the server's networks contain so the next occurrence can be named. The one real unsynced input found is the motor `Facing` (P3, patch 8.3).
**Status (patch 8.1, applied):** `LiquidBroadcastSystem` (Common/Net/LiquidBroadcast.cs) is marked from `LiquidNetwork.WithdrawFromWorldTile` and `DepositToWorldTile` (the only two world liquid edits of the networks), server only, so single-player and clients are unchanged. One queued position per tile, up to `NetworkConfig.LiquidBroadcastsPerTick` sent per tick, the rest next tick. Vanilla liquids and emptied tiles go through `NetMessage.sendWater`; custom liquids go through a 1x1 `SendTileSquare(-1, ...)` because ModLiquidLib carries the liquid type in the tile block codec (read in its source: `IL_NetMessage.CompressTileBlock_Inner` / `DecompressTileBlock_Inner`; it does not patch the water message), which the in-game test of the Fuel / Oil case confirms (verification item 13). `CartCargo.SendWater` (client-only) is left to Phase 9.

### P3 (H) Motor rotation is a local edit
`MotorTileEntityBase.OnRightClick` rotates `Facing`, sets `networkNeedsRebuilding` and reframes neighbor pipes, all on whichever process runs the click (the client replica). The server's network still uses the old `Facing`. **Fix:** right-click becomes a `RotateMotor` request (position); the server validates reach, rotates, rebuilds, and broadcasts the new facing. Clients run `PipeTileBase.ReframeNeighbors` when they receive the new facing. `Facing` also belongs in the motor's join snapshot (step 4). Motors are placed through `Hook_AfterPlacement` (step 4 M1), so entity creation follows the machine-foundation fix.
**Status (patch 8.3, applied, untested):** `MotorRotate` (Common/Net/MotorRotate.cs): `OnRightClick` on a multiplayer client sends `MotorRotateRequest` (38) with the wanted facing and changes nothing locally; the server validates with `MachineRequest.Validate`, sets `Facing`, flags `networkNeedsRebuilding` and broadcasts `MotorFacingChanged` (39) to every client; clients queue it and, on the main thread, set `Facing` and run `PipeTileBase.ReframeNeighbors`. `Facing` was already in the 5.1 snapshot. Single-player rotates directly. Also the one unsynced network input found while hunting R9 (8.1b).

### P4 (H) Tank contents for tanks and overlays
The network changes `LiquidStack.Amount` every tick for machines and motors on the server. Clients need the tank type and amount for the liquid overlays and UI tanks (step 4 M3, step 5 U3). To avoid sending every tick, send **quantized** values (for example 1/32 of capacity, or when the liquid type changes) and only when the quantized value changes; full precision goes only to players who have that machine's UI open.
**Status (8.2, docs only, applied, untested):** already implemented by 5.2 (light channel: type and amount in 1/32 of capacity for both tank arrays, sent only on change to near players) and 5.3 (exact tanks in the detail channel); the overlay reads the replica tank and the on/working flags travel in the same light payload. Nothing was missing; see the 8.2 Progress entry for the checklist.

### P5 (M) Motor on/off reaches network via the power grid only
`MotorTileEntityBase.Update` detects `isOn` changes and sets `flowNeedsRecalculating`. This is server-side and correct once the power grid is correct (wires are synced to the server, step 1). Clients only need `isOn` for animation (step 4 light update).
**Status (8.4, read, untested):** confirmed by reading: isOn and isWorking are in the 5.2 light flags; nothing else is needed.

### P6 (M) Debug overlays
`LiquidNetworkDebugSystem` draws `LiquidNetworkSystem` data, which is empty on clients. Either keep it as a host-only/single-player tool or add a debug-only request that streams the network summary. Low value; recommended: leave single-player only.
**Status (8.4, read):** the liquid overlay draws a notice on a multiplayer client; the power grid overlay has no notice and is left as is (debug config only).

### P7 (M) Infinite-source flood fill cache
`LiquidNetwork.IsInfiniteSource` caches by `Main.GameUpdateCount` and `WorldLiquidSources.ComputeIsInfinite` flood-fills `Main.tile` liquid. It runs only on the server (and the cart code, step 6, also on the server), so nothing changes, but the **server must have accurate liquid tile data**: after P2 the server's tiles are the truth, and bucket placements by clients reach the server through the vanilla `sendWater` handler.
**Status (8.4, read):** nothing to change; 8.1 made the server's own edits visible to clients.

### P8 (L) Buckets
`BucketBase.HoldItem` is owner-gated (`whoAmI == Main.myPlayer`), uses the same flow as vanilla buckets and sends `sendWater` from the client. No change needed. Optional hardening (server validation of the placed liquid) is outside this scope because vanilla trusts the client too.

### P9 (L) Custom liquids
`FuelLiquid` and `OilLiquid` (ModLiquid) override merge rules and splash effects; they run on both sides with identical results (sounds and dust are no-ops on a server). `LiquidTextureCache` is client-only (it loads textures; confirm it is not touched during server load, step 3's dedicated-server check).
**Status (8.4, read, in-game test open):** `LiquidTextureCache.GetOrCreate` is only called from draw code and `Clear()` returns early on a server (empty cache). Whether ModLiquidLib carries Fuel and Oil to clients (item 13) and whether the water + lava stone reaches them (item 9) need the 8.4 in-game checklist; a client's bucket of a custom liquid (vanilla water message) is part of that checklist.

### P10 (L) Placement checks
`PipeTileBase.CanPlace` / `MotorTileBase.CanPlace` run on the client. Vanilla has the same trust model for tile placement, so no server re-check is planned.

## Roadmap inputs from this step
- Net-plumbing phase: tile-placed packet registers pipes and motors (P1) and heals saved data.
- Liquid phase: P2 (broadcast world liquid changes), P3 (motor rotate request), P4 (quantized tank sync), then P5-P7 verification.
- Depends on: machine foundation (step 4) for motor entity creation and for tank sync channels.


---

# Factorraria multiplayer survey - Step 8: Items, projectiles, mounts, conveyor tiles

Scope: everything in `Content/` not yet covered: `Items/Wires/*`, `Items/Discovery/*` (scrolls, parchments), `Items/Conveyors/*` and `Tiles/Conveyors/*`, `Items/Hooks/MechanicalArmItem` + `Projectiles/Hooks/MechanicalArmProjectile`, `Items/Weapons/IceBladeItem` (GlobalItem, step 3) + `IceBladeSwingProjectile`, `Items/Mounts/MechSuitItem` + `MechSuitArmProjectile` (player side covered in step 2), joke items, materials, ores, `Particles/PuffExplosion`, `Configs/FurnaceOffsetConfig`, `AwakenedStatueEntries`, `MagicMaterializerRegistry`.

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## Rule used in this step
`ModItem.UseItem` is called for every player on every client, and `Player.tileTargetX/Y` are **statics driven by the local mouse**. Item code that reads them, mutates mod registries, or spawns items must be gated by `player.whoAmI == Main.myPlayer`, and anything that changes shared mod state must become a request to the server.

## Findings

### I1 (B) Wire items change state on the wrong side
- `CopperWireItem` and `TinWireItem`: `UseItem` calls `CustomWireSystem.AddWire(Player.tileTargetX, tileTargetY, ...)` with no owner check. Each client runs it for every player it sees using the **local** mouse tile, so wires appear where *you* are pointing when *someone else* swings a wire item, and the server (which needs the wires for the power grid) never runs it.
- `IronCutterItem` / `LeadCutterItem`: same, plus `Item.NewItem` for the returned wire runs in every process that executes it.
- **Fix:** owner-only; send `WireAdd(x, y, type)` / `WireRemove(x, y, type)` to the server. The server validates reach, whether the wire is already there, and (for add) trusts the client's consumed item (vanilla trust model) or refunds on reject. The server changes `WireGrid`, sets `gridNeedsRebuilding`, broadcasts, and the server spawns the dropped wire item at the tile. Join sync for the whole wire grid is listed in step 1. **Patch 3.1 applied (untested):** owner-only items, `WireEditRequest` / `WireChanged` (full tile flags), server drops the returned wire, join provider `custom-wires`; a rejected add is not refunded (see the roadmap block).

### I2 (H) Parchment reading rolls on the client
`RecipeParchmentItem.RightClick` rolls `ScrollPools.TryRoll(PoolName, Main.rand, ...)` on the clicking client, then calls `RecipeKnowledgeSystem.Unlock` locally. The code already carries `TODO (Phase 6): the roll and the unlock must run on the server`. **Fix:** client sends `ReadParchment(poolName)`; the server rolls with its own RNG against its authoritative knowledge, unlocks, broadcasts the delta to all clients (world-shared decision), and replies to the reader with the unlocked key so `RecipeBookSystem.OpenAt(key)` opens the right page. Consumption must follow the result: keep the item when the pool is exhausted (the existing `keepOnThisClick` logic moves to the reply handler, or the client keeps the item until the server confirms).

### I3 (M) Scrolls
`ClosedScrollItem` already has `NetSend`/`NetReceive` for `PoolName`, so the pool survives item sync and drops. Its right-click spawns a parchment with `QuickSpawnItem` on the clicking client, which is fine in vanilla's trust model (client-side item use). The per-pool `CanStack` is deterministic. No change needed beyond verifying that the parchment's own `PoolName` also has `NetSend/NetReceive` (it does, lines 94-96).

### I4 (H) Priority conveyors
- Items (`ClockwisePriorityConveyor`, counter-clockwise twin): the right-click rotation swap is a local `SetDefaults` (step 3: needs an owner-side slot sync).
- Tiles (`ClockwisePriorityConveyorTile` and twin): `RightClick` sets the conveyor filter on the client (`VirtualItemSystem.SetConveyorFilter`), `PlaceInWorld` registers the conveyor on the client only, and `KillTile`/`CanDrop` call `OnPriorityConveyorKilled` on both sides (harmless on clients if the vItem list is a replica, but `CanDrop` also calls it again, so make sure it is idempotent).
- **Fix:** `SetConveyorFilter` request (position + item type, server validates), the shared "tile placed" request (step 3, G0) for `OnPriorityConveyorPlaced`, and filter data included in the join/conveyor snapshot. See step 5 U6.
**Status (patch 7.3 and 7.3b, tested):** `OnPriorityConveyorKilled` is idempotent (a tile not in the map returns at once); placement registration is server-side since 3.3; the right-click rotation swap of the priority conveyor items is still local and stays open.

### I5 (M) Ice Blade swing projectile
`IceBladeSwingProjectile` computes its facing from `Main.MouseWorld` on **every** client (`isFacingRight`), so remote players' swings are mirrored according to each viewer's mouse. `Owner.itemAnimation/itemTime`, `Owner.releaseUseItem` and the swing timers are deterministic per client and fine. **Fix:** the owner decides the facing once (store it in `Projectile.ai`/`spriteDirection` and call `netUpdate`), everyone else reads it. Also `PuffExplosion(...).Spawn()` is called inside `AI()`, which also runs on a dedicated server: guard with `!Main.dedServ` (the particle systems may not exist on a server).

**Status (patch 2.3, applied, untested):** `IceBladeItem.Shoot` passes the facing as ai0 and `IceBladeSwingProjectile` keeps it in `ai[0]`; only the owner rewrites it (with `netUpdate`) when the cursor crosses the player, so viewers read it instead of their own mouse. `ReleasedSwing` returns early when `Main.dedServ`. Side effect to watch: each `netUpdate` also re-sends `ai[1]` (the swing timer), so a remote copy's swing can jump back a few ticks when the facing flips.

### I6 (M) Mechanical Arm hook
`MechanicalArmItem.Shoot` is owner-only (fine) and the projectile already uses `Projectile.netUpdate` when latching/retracting, and keeps jump/grapple input behind `Main.myPlayer == Projectile.owner`. Remaining issues:
- `player.velocity +=` and the speed cap (pull toward the hook) run on every client for the owner's `Player` object; only the owner's client should apply it (others receive the synced position/velocity).
- Instant kill on jump uses `Projectile.Kill()` from the owner only; other clients will keep the projectile until it times out. Use a synced retract state (`ai[0] = 2` + `netUpdate`) or send the kill message.
- `SendExtraAI/ReceiveExtraAI` are commented out; confirm all needed state is in `ai[0]`/`ai[1]` (the file says the bend sign is synced through `ai[1]`).

**Status (patch 2.4, applied, untested):** the push (`player.velocity +=`, speed cap, `fallStart`) and the Jump check now run only where `FactorrariaNet.IsOwner(player)`. Jump no longer calls `Projectile.Kill()` on the owner alone: it sets `ai[0] = 3` (released) and `netUpdate`; the arm is hidden at once, remote copies and the server kill it when they see the state, and the owner kills it 4 AI updates later. `SendExtraAI/ReceiveExtraAI` stay commented out: `pushDirection` is only read by the owner, and the bend sign already travels in `ai[1]` (each machine also recomputes it from the spawn velocity on the first tick).

### I7 (M) Mech suit arm projectiles
Same two issues as step 2 (anchor tile and bend sign need `NetSend/NetReceive`; owner-only spawn). Added here: `MechSuitArmProjectile` also kills itself locally (`Projectile.Kill()` at line 73), so retract/kill needs to be driven by synced state.
**Status (patch 2.1, applied, untested):** the suit flag is synced with `MechSuitStateRequest`/`MechSuitState` (`Common/Net/MechSuitNet.cs`) and the anchor tile and bend side now travel in `ai[0..2]`, so `NetSend/NetReceive` were not needed. Every machine kills its own copy of an arm when the owner leaves or the mirrored flag clears. The step 2 findings for `MechSuitPlayer` (duplicate spawns, local keyboard Space, per-process arm indices, remote velocity override) are all handled by the owner gate.
**Correction (test on 2026-10-18):** "remote velocity override" needs to be split in two. The velocity override itself stays owner-only, but the `Player.gravity = 0` part must also run on the remote copy of a suited player, otherwise it falls between position packets (seen on the second client). A late joiner also does not receive projectiles that already exist, so the owner has to re-send its arms (patch 2.1b).
**Status (patch 2.1b, applied, untested):** the Space dismount now uses the `controlJump` press edge (`releaseJump` is already cleared by vanilla's jump block before `PreUpdateMovement`); the gravity cancel for the remote copy of a suited player is in `PostUpdateRunSpeeds`, not `PreUpdateMovement` (vanilla adds gravity before `PreUpdateMovement` runs, so a change there does nothing); the owner marks its arm projectiles `netUpdate` every 180 ticks so a late joiner receives them. Details are in the roadmap's "What patch 2.1b changed".
**Status (patch 2.1c, applied, untested):** 2.1b test: velocity was synced but the other client kept sliding after the owner stopped, then snapped back. Cause (not verified against vanilla source, which is not in the repo): vanilla sends no velocity in the player packet when it is zero and only sends at all on a change, so the remote copy kept extrapolating the last non-zero velocity until a later position packet. Fix: remote copies of a suited player compute velocity from the control bits in the packet (same `ReadMoveInput` rule as the owner), and the owner forces `netUpdate` when its input changes and every `PosResyncTicks` (15) while suited.
**Status (patch 2.4b, applied, untested):** `Player.netUpdate` does not exist (it broke the build from 2.1c on). The suit's position sync now calls `FactorrariaNet.SyncPlayerNow` (message 13) from `MechSuitPlayer.PostUpdate`. Earlier mech suit results (2.1b to 2.1d) were probably produced by a stale build and are worth one more look after this patch.

### I8 (L) Items and tiles that need nothing
Materials (`ByriteAlloy*`, `Coke`, `*Sheet`, `GoldPlate`, `GoldWire`, `MeteoritePanel`, `SimpleCircuit`, `Steel*`), `SiliconOre*` and `PureSilicon*` (item + ore tile), `MechanicalAssemblerItem`, and the joke items (draw hooks only; `IsmailsCaneItem` hold style is cosmetic). They are deterministic data and run the same on every side.

### I9 (L) Static data
`AwakenedStatueEntries` and `MagicMaterializerRegistry` are static data tables built identically from mod code on every process; ordering matters only if indices go on the wire, so use the stable keys (as in step 5, U4).

### I10 (L) Config
`FurnaceOffsetConfig` is `ConfigScope.ClientSide` (visual offsets, `VItemOffset`, debug toggles, `RevealAllRecipes`). Client-side scope is correct for visuals. `RevealAllRecipes` is a client-only reading of the knowledge (it does not change the world), but check in step 10 that it never writes to the shared sets.

## Roadmap inputs from this step
- Net-plumbing phase: `WireAdd/WireRemove`, `SetConveyorFilter`, `ReadParchment` request packets; owner-gating helper (`if (player.whoAmI != Main.myPlayer) return`).
- Small fixes (independent, safe first patches): I5 (swing facing + server particle guard), I6 (owner-only velocity + synced retract), I7.
- Depends on: wire grid and conveyor/vItem replication (step 1), knowledge replication (step 1).


---

# Factorraria multiplayer survey - Step 9: Remaining files and coverage check

Method: after steps 1-8 I compared every `.cs` file in the repo (183) against the sub-reports. Files whose name appears in no report were read here. The per-machine folders (`*TileEntity`, `*UIState`, `*RecipeRegistry` for Autohammer, Extractinator, MechanicalAssembler, Solidifier, GelBurner, DecayChamber, Furnace, Hellforge, IceMachine, LiquidDistillator, Motor) are covered as a group in steps 4/5, and the item/ore/material files as a group in step 8.

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

Severity: **B** blocker / **H** high / **M** medium / **L** low.

## Findings

### R1 (L) Chest loot and worldgen
`ChestLootSystem.PostWorldGen` applies `ChestLootRegistry.ApplyAll(WorldGen.genRand, ...)` with the seeded generator, and `SiliconOreWorldGen` / `PostWorldGenEdits` are standard worldgen passes. World generation runs on the host or server only and is seed-deterministic, so no MP work is needed. (Existing worlds are not re-patched; that is a content decision, not an MP one.)

### R2 (M) Recipe knowledge internals (`RecipeKey`, `ScrollPools`, `ScrollPoolDefinitions`, `RecipeCatalog`, `RecipeVisibility`, `RecipeDebugCommand`)
- All are pure logic over `RecipeKnowledgeSystem`. `ScrollPools.TryRoll` and `RecipeCatalog` read the unlocked/crafted sets, so the **server** must be the one rolling (step 8, I2) against its authoritative sets.
- `RecipeKnowledgeSystem` has `Version` and `SeenVersion` counters that the UI uses to rebuild lists (`RecipeSelectHammerIcon`, `RecipeVisibility.Poll`). When a delta or full sync arrives on a client, it must bump these counters or the UI will not refresh.
- `RecipeVisibility` is UI-only (state, faded/hidden display). Its `Poll` scans the local inventory and calls `MarkSeen` constantly; in MP that must send only **newly seen** item types, batched and rate-limited. With world-shared knowledge, one player holding an ingredient reveals it for everyone (a direct consequence of your decision, worth knowing).
- `RevealAllRecipes` (client config) only changes display and never writes to the sets, so it can stay client-side.
- `RecipeDebugCommand` is `CommandType.Chat`, so it runs on the sending client (replica data). Fine once knowledge is replicated; if you want it to inspect the real data it should become `CommandType.World`/`Server`.

### R3 (L) Recipe data (`BaseRecipeRegistery`, `CustomRecipe`, `RecipeOutputKey`, `MachineBiome`)
Registries are built in `PostAddRecipes` by importing vanilla recipes and adding manual ones; the result is deterministic for the same mod list (tModLoader enforces matching mods on join). Rule for the wire format: always identify recipes by their stable key (`RecipeCatalog.KeyFor`, output item/liquid name), never by list index. `MachineBiome` is a plain enum (the biome detection itself is in `BaseMachine`, step 4 M5).

### R4 (L) `VItemStacking`
A pure stack-merging helper over `Item[]`. After the server inserts into a chest through it, the chest slot must be synced (`ChestConveyorSystem` does this with `SyncChestItem`; the same must be done in the cart cargo path, step 6 C4). Concurrent edits by a player and a conveyor on the same chest are last-writer-wins, the same behavior vanilla has for chests.

### R5 (L) `MechanicalArmIK`
Pure math for drawing the arm; no state, no MP issue.

### R6 (L) Statue support types (`IStatueAwakener`, `AwakenedStatueEntries`)
Data and interface only; behavior and sync needs are in steps 1, 3 and 8.

### R7 (M) UI files with no `dedServ` guard of their own
`BookScrollbar`, `RecipeBookButtonState`, `RecipeBookDragHandle` and `RecipeSelectHammerIcon` create or use `UIElement`s/`UserInterface` but contain no guard. They are safe only if nothing constructs them on a server. The owning systems have guards (`RecipeBookSystem` 5, `MachineUISystem` 1, `CartChestUI` 1), but each should be re-verified on the **first dedicated-server run** (a single unguarded `new UserInterface()` or font/texture request in `Load()` crashes the server at startup). Also `UIItemSlotWrapper` and `LiquidTextureCache` (steps 3, 5, 7) belong on that checklist.

### R8 (L) `Factorraria.cs`
Empty `Mod` class: this is where the packet router (`HandlePacket`) and the `ModPacket` helper will live.

### R9 (L) Joke and cosmetic items, particles
Covered in step 8. Nothing further.

## Coverage statement
Every `.cs` file in the repo is now accounted for in steps 1-9 (system, player, global, machine, UI, cart, liquid, content, remaining). Step 10 will cover the non-code surfaces (build.txt/mod references, configs, localization, commands, tools) and the final cross-check.

## Roadmap inputs from this step
- Knowledge phase: version counters on receive, batching of `MarkSeen`, optional server-side debug command.
- Verification phase: first dedicated-server run checklist (R7 plus the earlier items from steps 3, 6 and 7).
- Net-plumbing phase: `Factorraria.cs` hosts the router.


---

# Factorraria multiplayer survey - Step 10: Non-code surfaces and final cross-check

Scope: `build.txt`, `Factorraria.csproj`, `Properties/launchSettings.json`, `Localization/*`, configs, commands, `tools/`, repo housekeeping, plus the master verification list and cross-cutting checklist for the roadmap.

Decisions carried over: world-shared recipe knowledge; server-authoritative carts; host-and-play and dedicated server both supported.

## Findings

### S1 (L) `build.txt` and dependencies
`build.txt` sets no `side`, so the default (`Both`) applies: server and every client must have the mod, which is what this design needs. `modReferences = ModLiquidLib, Luminance` means both must also be present on a dedicated server. The `.csproj` reference paths (`..\ModAssemblies\...`) are local build paths and are not a runtime concern. `buildIgnore` already excludes `tools\*`, the indexer, the index and patch scripts from the `.tmod`.

### S2 (M) Third-party mod compatibility in MP
Two things to confirm in the roadmap's verification phase: (1) ModLiquidLib synchronizes custom liquid types in tile/section data and in `NetMessage.sendWater`, which pipe and cart liquid broadcasts (steps 6, 7) depend on; (2) Luminance is only used as a library (no netcode of the mod relies on it). If ModLiquidLib needs extra calls for custom liquid sync, they belong in the liquid phase.

### S3 (L) Testing setup already exists
`Properties/launchSettings.json` already has a **TerrariaServer** profile next to the normal Terraria one, so a dedicated server can be launched straight from Visual Studio for testing. A realistic test needs a server plus two clients; I could not verify the exact flags for running a second local tModLoader instance (a separate save directory is needed), so that is a "verify" item for the first test session.

### S4 (L) Localization
`en-US.hjson` and `en-US_Mods.Factorraria.hjson` are loaded identically on server and clients. New packet-related messages (rejections, "too far", "pool exhausted") should be added there. Server-originated messages to one player must use `ChatHelper.SendChatMessageToClient` (or be sent as a packet and shown on the client), not `Main.NewText`, which on a server only writes to the console. Current server-reachable `Main.NewText` calls: `CartSystem.ReportFailureOnce` (step 6); the parchment messages run on the client today but move into the reply handler (step 8).

### S5 (L) Configs
`FurnaceOffsetConfig` is `ConfigScope.ClientSide` (visuals and debug), which is correct. Optional addition: a small `ServerSide` config for network tuning (cart update rate, interest radius, per-tick liquid broadcast cap, machine light-update rate) so you can tune without recompiling.

### S6 (L) Commands and mod calls
Only `RecipeDebugCommand` (`CommandType.Chat`); no `Mod.Call`. Nothing to change.

### S7 (L) Tools and housekeeping
`tools\patch_engine.ps1` and the indexer are build-time only. The packet module should live in a new folder (`Common/Net/`) so the indexer picks it up. `description.txt` is still the placeholder; it is a release item, not an MP item.

## Master verification list (everything marked "verify" in steps 1-9)
1. Whether Host & Play runs the server in-process or as a separate process (steps 1, 3). Code is written to be correct either way.
2. `ModTile/GlobalTile.PlaceInWorld` is client-only; call site confirmed in tModLoader source, behavior on a dedicated server to be confirmed with one log line (step 3).
3. Pipe registration on the server after the tile-placed packet; self-heal of old saves (step 7).
4. `TrackData.EnsureLoaded` reflection works on a dedicated server (step 6).
5. `ItemSlot.Handle` with `ChestItem` context does not send chest sync with chest index -1 (step 5).
6. Right-click `ItemSlot` path for `VanillaConveyorGlobalItem` syncs the changed slot, or needs an explicit `SyncEquipment` (step 3).
7. `Projectile.Kill()` from the owner propagates to other clients, or must be replaced by synced state (steps 2, 8).
8. Vanilla Monolith right-click frame flip sent to the server before `MaterializerRightClickGuard` undoes it (step 5, U9).
9. `LiquidMerge` stone result reaches clients (step 3).
10. `UseItem` runs for remote players on every client (assumed; the owner gate is correct either way) (steps 2, 8).
11. Equipment slot index mapping for `SyncEquipment` of `miscEquips` (step 2).
12. First dedicated-server startup: no client-only call in any `Load()`/`PostSetupContent()` (steps 3, 9).
13. ModLiquidLib custom liquid sync (S2).

## Cross-cutting building blocks the roadmap must create first
- **Net module** (`Common/Net/`): packet type enum, router in `Factorraria.HandlePacket`, read/write helpers, `IsServerSide`/`IsOwner(player)` helpers, a reach/validation helper, "send to players near a point" helper.
- **Join sync** (`OnEnterWorld` request/response) with a registry of syncable states (wires, conveyor filters, chest-conveyor entries, awakened statues, knowledge sets, carts in range).
- **Tile-placed request** (G0) used by chest conveyors, liquid topology, awakened statues, pipes, motors, priority conveyors, and machine placement.
- **Machine request family** (Open, Close, SlotEdit, SelectRecipe, Toggle, Rotate) with a shared slot compare-and-swap helper (also used by cart panels).

## Coverage statement
All 183 code files and all non-code surfaces are now accounted for. The survey is complete; the next deliverable is the compiled roadmap.


---

# Factorraria multiplayer survey - Step 11: Delta review (commits after the survey base)

Reviewed range: `25510ef` (survey base, "awakened hardmode statues fix") through `49f8da0` (HEAD, 2026-10-16): **29 commits**, 192 `.cs` files (the survey saw 183; nine are new).

What changed in code (non-art):
- **New:** `RoboticArm` machine (`RoboticArmTile`, `RoboticArmTileEntity`, `RoboticArmWorker` incl. `RoboticArmGhostSystem`, `RoboticArmVisual`, `RoboticArmAreaEditorSystem`, `RoboticArmDropCapture`, `RoboticArmUIState`), `IMachineDynamicDraw`, `MachineDynamicDrawSystem`.
- **Modified framework:** `BaseMachine` (new virtual `IntakeTiles()`), `MachineUISystem` (new `OpenMachine` property), `MachineUIStateBase` (`OutputSlotEntry` visibility predicate), `MachineRegisterationSystem` (registers the arm), `ElectricityUIIcon`, `MechanicalArmIK`, `MechanicalArmItem` (second mode: place the arm).
- **Data only:** `RecipeSystem` (112 lines removed), four recipe registries, `AwakenedStatueRegistry`/`AwakenedStatueEntries`/`AwakenedStatueDrawSystem` (hardmode enemy lists), localization.
- **Added and removed again:** an `ArmSpike` debug command (`8f37e00`); it is not in HEAD.

Severity: **B** blocker / **H** high / **M** medium / **L** low. IDs use prefix **A** (arm / post-survey).

## Findings

### A1 (B) The arm worker's timing depends on the client-only visual
`RoboticArmWorker.TickReach` only moves on to the strike when `arm.Visual` exists and reports `InPosition`; `TickStrike` takes its impact moment from `arm.Visual?.ImpactPoint`. `RoboticArmVisual` is created in `DynamicUpdate`, which runs from `MachineDynamicDrawSystem` only when `!Main.dedServ`. In multiplayer, `TileEntity.Update()` (and so the worker) runs only on the server, where `Visual` is always null. Result: the arm waits in Reach until `ReachTimeoutTicks` (120), blacklists the tile for `BlockedCooldownTicks` (180) and tries the next one. **It never strikes on a server.**
**Fix:** move the reach time, strike length and impact point into a small deterministic motion model that the worker owns (the same easing the visual uses). The worker keeps working with no visual. Clients animate their own visual from replicated events (A5b). In single-player the worker may still call the visual directly, as it does today.
**Status (patch 2.7, applied, untested):** done as `RoboticArmMotion`, owned by the entity (`arm.Motion`) and ticked once per logic tick right after the worker. The worker no longer touches `arm.Visual`; the visual reads the motion to draw. A server falls back to the item hitbox for the held-tool length (no textures there). Phase 7b.1 still has to confirm it on a dedicated server.
**Test result (7b.1, 2026-10-20): passed.** On a dedicated server the arms mine and place blocks with no visual present and the client sees the tiles change; the hand does not animate on the client (7b.4). **Status (patch 7b.1, applied):** code review of the worker, `RoboticArmMotion` and the entity tick found nothing left that needs a client: no visual, `Main.LocalPlayer`, screen or texture is read, `ItemLength` has the `Main.dedServ` fallback, and `Motion` is ticked once per logic tick (server, single-player) or by `DynamicUpdate` only on a `MultiplayerClient`. No logic change. Temporary `[Arm]` log lines, worker counters and a worker/motion/visual part of the `/fsnap` line were added so a dedicated-server test shows reach, strike and finish with `visual=none`; remove them in Phase 10.

### A2 (B) World edits made by the arm are never sent to clients
Every strike changes the world on the server: `ghost.PickTile`, the copied axe branch (`WorldGen.KillTile`), `ghost.PickWall`, `WorldGen.PlaceWall`, `TileObject.Place` + `SquareTileFrame` + post-placement hook, `WorldGen.PlaceTile`. `RoboticArmWorker` contains no network call. In vanilla the *player's client* reports tile changes to the server; a server-run change is not automatically broadcast (some `WorldGen` methods broadcast when run on a server and some do not; to verify, item 16).
**Status (patch 7b.2, applied, untested):** `RoboticArmWorker` now calls `NetMessage.SendTileSquare` after every strike that changed the target and after every placement (3x3 for plain tiles and walls, the object size on every side for multi-tile objects, a 9 x 43 column for trees and cacti), server only. The 7b.1 test showed clients already saw single tile changes without it, so part of vanilla broadcasts by itself; `/farmsend off` measures how much. Entity furniture placed by the arm (chests, signs) is not covered and is reported by the test for 7b.6. A temporary ghost-item diagnostic (`/fghost`, `[Ghost]` log lines) was added for finding R3 (see A13).
**Fix:** after every strike the server sends the affected area with `NetMessage.SendTileSquare` (size covers multi-tile objects and neighbor frame changes). Cheap and uniform, so do it for all jobs rather than rely on per-method behavior.

### A3 (H) Drop routing uses the local screen
`RoboticArmTileEntity.RouteDrop` decides between "drop into the world" and "make a vItem" with `IsOnScreen(Shoulder)`. `BaseMachine.IsOnScreen` returns `false` when `Main.dedServ`, so on a dedicated server every drop becomes a vItem even with a player standing next to the arm; in Host & Play it would use the host's screen only.
**Fix:** a server-side helper `IsAnyPlayerNear(worldPos, range)` (using the active players' synced positions, range of one screen) replaces the call; client-only `IsOnScreen` stays for drawing. The rule becomes "no player near -> vItem".
**Status (patch 2.7, applied, untested):** `BaseMachine.IsAnyPlayerNear(worldPos, margin)` added; `RouteDrop` now uses `IsAnyPlayerNear || IsOnScreen` (the local screen still counts, so single-player and Host & Play are unchanged). Phase 7b.3 still has to verify the world-item versus vItem split on a dedicated server.

### A4 (H) Server-side placements need entity broadcast and server-direct tile-placed handling
The worker calls `TileObjectData.CallPostPlacementPlayerHook` and `TileLoader.PlaceInWorld` **on the server**. Consequences:
- If the arm places a machine tile, `BaseMachine.Hook_AfterPlacement` runs on the server and `Place`s the entity there, but nothing broadcasts it (tModLoader only broadcasts in its own `NetPlaceEntityAttempt` path), so clients never get the entity.
- Placement hooks such as the chest-conveyor probe, pipe registration and statue checks already fire on the server for arm placements (a side effect: arms are "better" than players today because player placements never reach the server).
**Fix:** one server-side helper "place machine entity and broadcast" used by the tile-placed consumer (Phase 4.2), by `OnNetPlace`, and by arm placements. The tile-placed consumer API (Phase 1.3) must be **callable directly from server code** as well as from the packet handler, and every consumer must be **idempotent** so a double notification (arm path + packet path) is harmless.
**Status (patch 7b.6, applied, untested):** read again for the arm: the worker's object route calls `CallPostPlacementPlayerHook` (ModTile machines reach `Hook_AfterPlacement`'s server branch, `MachinePlacement.ServerPlace`, which shares the entity) and then `TileLoader.PlaceInWorld` (`TilePlaced.RunConsumers`: `MachinePlacedConsumer` for vanilla-tile machines, which skips ModTiles, plus the pipe, motor, statue and conveyor consumers). All of it is idempotent, so no change was needed. A refused entity request (`NetPlaceEntityAttempt`) leaves a tile without an entity, which the open request (6.1) or the bootstrap scan (4.5) creates later; the temporary `/farmsend placereject` tests it.

### A5 (H) Area editor and arm UI edit the replica
`RoboticArmAreaEditorSystem` calls `arm.SetArea` on the client replica while the player drags; `RoboticArmTileEntity.SaveData/LoadData` store `AreaX/Y/W/H`.
**Fix:** (a) `AreaX/Y/W/H` go into the machine snapshot (Phase 5.1); (b) the drag previews locally and sends one `SetArea` request on mouse release (the server runs the same clamp in `SetArea` and rebroadcasts), not one per frame; (c) the input slot uses the machine's own `IsAcceptedItem` as the compare-and-swap accept filter (Phase 6.2).
**Status (patch 7b.5, tested 2026-10-21: works):** (a) was done in 5.1 and (c) in 6.2. (b): `RoboticArmAreaEditorSystem` still previews on the replica and now calls `RoboticArmAreaNet.Commit` on mouse release, which sends one `ArmAreaRequest` (36). The server validates (arm exists, player in reach, arm UI open, sane size, minimum spacing), runs the same `SetArea` clamp, answers the sender with `ArmAreaChanged` (37, always the arm's real area, so a rejection restores the overlay) and sends the new area to the other players within 75 tiles. Neither the detail channel nor the light update carries the area, so opening the arm UI also sends a query request to refresh a replica that missed a broadcast. Test aid: `/farmsend areareject` in the server console.

### A5b (H) Visual events channel for the arm
**Status (patch 7b.4b, tested 2026-10-21: dust, the hit, break and placing sounds all work; only the tile damage cracks do not show on the client, left as is by decision):** the 7b.4 test showed the animation but no dust, damage or sound on clients (the server runs the strike and vanilla makes those on a screen only). The arm now sends vanilla's tile-manipulation message (id 17: kill tile / kill wall, fail flag for a hit that changed nothing) to the players near it before the tile square, so each client replays the hit as it does for another player's mining, plus an `ArmEffect` packet (id 35) for the placement sound. Damage cracks are checked by the test.
**Status (patch 7b.4, applied, untested):** `RoboticArmNet` sends one `ArmState` packet (id 34) to the players within 75 tiles when the hand's target, a strike start or the held item type changes, plus a refresh; each client replays it in its own `RoboticArmMotion` (already ticked by `DynamicUpdate` on a client) and `RoboticArmVisual` draws it, with the held item type taken from the packet (`HeldForVisual`) because arm slots are not replicated for arms nobody opened.
Clients draw the arm through `RoboticArmVisual` driven by `SetTarget`, `StartStrike(ticks)` and `HasTarget/InPosition`. In MP those calls happen on the server worker, so clients need a small broadcast to nearby players: `TargetChanged(arm, tile or none)` and `StrikeStarted(arm, ticks)` (plus "idle" when power drops, which the worker already expresses as `SetTarget(null)`). The held item shown on the claw comes from the replicated input slot (Phase 5). Visual drawing stays client-only.

### A6 (M) Mechanical Arm item mode switch is client-local
`MechanicalArmItem` switches `Item.createTile/consumable/shoot` through `ApplyMode(PlacingMode(player))` where `PlacingMode` requires `player.whoAmI == Main.myPlayer && Main.keyState.PressingShift()`. The server's and other clients' copies of the item stay in hook mode. If the server validates tile placement against the held item's `createTile`, placement of the Robotic Arm may be rejected (tile reverts). To verify (item 15) in a real session; if it is rejected, the placement must go through a request instead of the mode switch. The hook shot itself is unaffected (`Shoot` is owner-only, covered in step 8).
**Test result (Ismail, 2026-10-21):** reported as working ("everything works"), so the server accepts placement made through the mode switch; item 15 is closed and no placement request is needed.
**Status (patch 7b.6, applied):** by reading the code the placement does not depend on the item's mode on the server: only the owner's own `PlaceThing` reads `createTile`, and the server only receives the tile square, the tile-entity request (87) and the biome report from `Hook_AfterPlacement`, the same traffic as the Motor MK1 that passed on a dedicated server in 4.2 and 4.3. No mod code on the server compares a placement with the held item; vanilla's handlers for messages 17 and 79 are not in the tModLoader patches and could not be read, so item 15 is closed by the in-game test only. No placement request was added. One related inconsistency was fixed: `RoboticArmTileEntity.IsPlaceable` depended on the mode-dependent `createTile` of whichever copy of the arm item it saw, so it now refuses `MechanicalArmItem` by type (an arm never places arms).

### A7 (M) Dynamic draw framework
`MachineDynamicDrawSystem` iterates `TileEntity.ByPosition` for `IMachineDynamicDraw` entities in `PostUpdateEverything`, an `On_Main.DrawNPCs` detour and `PostDrawTiles`. All three are guarded by `!Main.dedServ`; on clients they will iterate the **replicated** entities, so visuals only work once Phases 4-5 deliver machine entities to clients. Minor: the detour is installed on a server too (harmless but unnecessary; add a guard and include it in the Phase 0 server-boot check). **Status:** guard added in patch mp_0 (tested: the dedicated server boots and runs cleanly), and the same guard added to `AwakenedStatueDrawSystem` and `MachineOverhangSystem`.

### A8 (L) `BaseMachine.IntakeTiles()`
New virtual, default is the machine's footprint (`cornerPosition`, `MachineWidth/Height`), so Phase 4.1 (server-safe geometry) still applies to every default machine. `RoboticArmTileEntity` overrides it with the 3x3 around its anchor `Position`, so it does not depend on `MachineInitialized`, and works on a server once vItems are server-authoritative (Phase 7.1).

### A9 (L) UI framework changes
`MachineUISystem.OpenMachine` (read by `RoboticArmAreaEditorSystem`) and `OutputSlotEntry(..., isVisible)`. No MP effect beyond Phase 6.1: when the UI re-resolves the machine by position each frame, `OpenMachine` must return that live replica so the editor never edits a stale object.

### A10 (L) Recipe changes
`RecipeSystem` lost 112 lines and registries changed. Data only; the "identify recipes by stable key" rule is unchanged.

### A11 (L) Awakened statue hardmode lists (25510ef)
`AwakenedStatueEntry.PickNpcType` now uses `Main.hardMode` and `Main.rand`; it runs on the server where spawns happen, and `Main.hardMode` is a synced world flag. No MP change. The registration code holds `Asset<Texture2D>` references; add it to the Phase 0 dedicated-server check (requests are fine, loading `.Value` is not).

### A12 (L) Arm-specific object lifetimes
`RoboticArmGhostSystem` creates a hidden `Player` on first use (`new Player()`, `whoAmI` 0) on whichever process runs the worker. The worker passes `plr: -1` to `PlaceTile` already. To verify (item 17): none of the vanilla calls the ghost makes uses its `whoAmI` as an "ignore this client" argument in a network send, which would silently skip player 0.
**Status (patch 7b.7, applied as docs only, untested):** the ghost is used in `RoboticArmWorker` only (`ghost.PickTile`, `ghost.PickWall`, and `ghost.hitTile` through `ChopTile`); `PlaceWall`, `PlaceObject` and `PlacePlain` never take it (`PlacePlain` passes `plr: -1`). No mod code reads its `whoAmI` or `Main.myPlayer` in the arm folder. Every send the arm makes is ours and ignores nobody: `NetMessage.SendTileSquare(-1, ...)`, `NetMessage.SendData(MessageID.TileManipulation, i, -1, ...)` to each near player, and `FactorrariaNet` packets to explicit players. In the tModLoader patches (Player.cs.patch around `PickTile`, WorldGen.cs.patch) every visible send uses -1 as ignore-client. NOT read: the bodies of vanilla `Player.PickTile`, `PickWall`, `WorldGen.KillTile` and `Player.GetOtherPlayersPickTile` (not in the patches, no decompile available); from memory the sends in `PickTile` / `PickWall` sit in `Main.netMode == 1` branches, unverified, and `GetOtherPlayersPickTile` skips `whoAmI` in a damage tally over the other players (worst case: a client 0's damage on the same tile is ignored, cosmetic, not a send). No fix was written for a cause nobody has seen; the two-client test (client 0 must see everything client 1 sees) closes verification item 17.

### A13 (L) Drop capture
`RoboticArmDropCaptureSystem`/`GlobalItem.OnSpawn` runs while the server executes `Item.NewItem`, then sets the item inactive when a drop becomes a vItem. Verify (item 18) that this does not leave a ghost world item on clients (the spawn broadcast must see an inactive item). `VirtualItemSystem.SpawnVirtualItem`, `IsConveyorTile` and `IsTileOccupied` are server-only calls after Phase 7.1, which is where the arm runs.
**Status (patch 7.1, tested):** the vItem simulation, world-item conversion and player pickup run only on the server and in single-player; `SpawnVirtualItem` hands out stable ids (`VirtualItem.Id`), so the arm worker and every other server caller get ids. Whether `OnSpawn`-inactivated items leave ghosts on clients is still verification item 18.
**Status (patch 7.2, tested):** clients now receive vItems through `VItemDelta` (see the roadmap 7.2 note) and draw them from replicas; the `VItemGlobalItem` immunity timer stays server-only as planned, and the arm worker, machines and the extractinator keep spawning vItems on the server only.
**Test finding (7.4 and 7b.1 test, 2026-10-20, R3):** ghost world items and vItems stayed on the client, could not be picked up, and were normal after a single-player reload. Cause not confirmed (suspects: this A13 path in `RouteDrop`, `SyncWorldItem` after cart pickups, vItem replica removal, server-only reservation/grab timers). Diagnostic in 7b.2, fix in 7b.3. Carts invisible to clients (R1) and the idle arm hand on clients (R4) are the expected Phase 9 and 7b.4 gaps.
**Status (patch 7.4, applied, untested):** integration review of the vItem users. Chest conveyors already sync each changed chest slot (`SyncChestItem`, server). The cart pickups of dropped world items (`CartCargo.PickUpWorldItems`, `PickUpFuelItems`) edited `Main.item` on the server with no `SyncItem`; they now call `VirtualItemSystem.SyncWorldItem`. Cart chest and fuel contents are cart data and stay unreplicated until Phase 9. The Extractinator needs no change (its `Update` runs on the server and `SpawnVirtualItem` output is replicated by 7.2). `VItemGlobalItem.ConveyorImmunityTimer` is server-only by construction (set and read only in the server-side conversion code, never sent).

### A14 (L) Worker state
`RoboticArmWorker` is created lazily in `Update()` and not saved; cooldowns use `Main.GameUpdateCount`. All server-side and fine after a restart (it just re-scans). Tunables are instance fields.

## Master-verification additions
15. Server accepts placement of the Robotic Arm made through the item's mode switch (A6). **7b.6 applied, untested:** code reading says no placement request is needed; the 7b.6 test (a client places an arm with Shift held) closes it.
16. Which `WorldGen` kill/place calls broadcast when run on a server (A2); the fix (explicit `SendTileSquare`) works regardless.
17. Ghost `Player` `whoAmI` is never used as an ignore-client argument (A12).
18. `OnSpawn`-inactivated items do not appear on clients (A13).
19. `MachineDynamicDrawSystem` detour on a dedicated server is harmless; guard it (A7).

## Roadmap impact
New **Phase 7b - Robotic Arm** (A1-A5b, A12-A14), a SP-safe preparation patch **2.7** (A1 motion model, A3 helper), plus edits to Phases 0, 1.3, 4.2, 5.1, 6.1, 6.2, 7.1, 10, the traceability table, the test matrix and the verification list. Details in the roadmap v2.
