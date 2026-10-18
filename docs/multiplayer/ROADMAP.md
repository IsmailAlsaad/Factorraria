# Factorraria Multiplayer Roadmap (version 2)

Compiled from the ten survey steps plus a delta review of the later commits (step 11) (ModSystems, ModPlayers, Globals, BaseMachine + tile entities, UI, carts, liquids, content, remaining files, non-code surfaces). Every finding ID below (X1, G0, M1, U1, C1, P1, I1, R1, S1, A1 ...) refers to the matching sub-report; the full text is in the appendix file `FACTORRARIA_MULTIPLAYER_SURVEY_APPENDIX.md`.

Survey base: GitHub commit `25510ef` (183 `.cs` files). **Version 2** also covers every commit through `49f8da0` (2026-10-16, 192 `.cs` files). The code wins where it disagrees with this document.

---

## Revision history and what changed in version 2

- **Version 1** was written against commit `25510ef`.
- **Version 2** reviews the 29 commits from `25510ef` through `49f8da0` (28 new commits). They add the Robotic Arm machine (7 new files), the dynamic-draw framework (`IMachineDynamicDraw`, `MachineDynamicDrawSystem`), a new `BaseMachine.IntakeTiles()` hook, the arm's second mode on `MechanicalArmItem`, hardmode enemy lists for awakened statues, and recipe data changes. None of the original findings was invalidated; two statements were extended (the list of real-ModTile machines now includes the arm, and the machine count is one higher).

**Findings that changed the roadmap** (full text in appendix step 11)

| ID | Sev | What | Where it is applied |
|---|---|---|---|
| A1 | B | The arm worker only strikes after the client-only `RoboticArmVisual` reports it is in position; on a server the visual does not exist, so the arm never strikes | New patch 2.7 (prep) and Phase 7b.1 |
| A2 | B | Every world edit by the arm (pick, chop, wall, place, object placement) runs on the server with no network send | Phase 7b.2 |
| A3 | H | `RouteDrop` uses `IsOnScreen`, which is always false on a dedicated server, so every drop becomes a vItem even next to a player | Patch 2.7 (helper), Phase 7b.3 |
| A4 | H | The arm places tiles on the server and runs the placement hooks there; arm-placed machines are never broadcast, and tile-placed consumers must be callable from server code and idempotent | Phase 1.3, Phase 4.2, Phase 7b.6 |
| A5 | H | The area editor changes `AreaX/Y/W/H` on the client replica | Phase 5.1 (snapshot), 6.2 (accept filter), 7b.5 (request) |
| A5b | H | Clients need arm target and strike events to animate the visual | Phase 7b.4 |
| A6 | M | The arm item's place mode is a client-only item property switch; the server may reject the placement | Phase 7b.6 (verification item 15) |
| A7 | M | Dynamic-draw framework only works once machine entities exist on clients; detour is installed on servers too | Phase 0.5, works with Phases 4-5 |
| A8, A9 | L | `IntakeTiles()` default depends on the footprint geometry; `MachineUISystem.OpenMachine` must point at the live replica | Phase 4.1, Phase 6.1 |
| A11 | L | Statue entries hold asset references: included in the dedicated-server boot check (hardmode lists themselves are MP-safe) | Phase 0.5 |
| A12-A14 | L | Ghost `Player` `whoAmI`, drop capture `OnSpawn`, worker state: verify, no redesign | Phase 7b.3, 7b.7, verification items 17-18 |
| A10 | L | Recipe data changes | none |

**Recommendation:** land patch **2.7** before more arm development. It is a single-player-safe refactor, and every later arm feature then builds on logic that does not depend on the client.

**New default (change if you disagree):** the arm's animation on clients is driven by two small events from the server (target changed, strike started), not by re-simulating the worker on each client.

---

## Progress

One line per patch. `[ ]` not started, `[~]` applied, untested, `[x]` tested and done. Each patch marks itself `[~]`; a small follow-up patch records your test results.

- [x] 0 Dedicated-server boot and test rig (0.1-0.5; patches mp_0, mp_0b, mp_0c: server-safe detour guards + [MP-Boot] probe; tested in single-player, on a dedicated server via the .bat with one client, and with a Host & Play client; results in section 8 and the test rig notes)
- [x] 1.1 Net core (patch_mp_1_1_net_core.ps1: router, helpers, request helper, /fping; tested: single-player direct run, dedicated server + client round trip 16.8 ms, everything else unchanged)
- [x] 1.2 Player sync and join sync (patch_mp_1_2_join_sync.ps1: JoinSync provider registry, chunked join packets, FactorrariaPlayerSync, disconnect hooks, /fjoin; tested: single-player no-op, dedicated server + client full sync 2/2 providers 20012 bytes ~115 ms, repeated /fjoin works, everything else unchanged)
- [x] 1.3 Tile-placed request (G0) (patch_mp_1_3_tile_placed.ps1: TilePlacedGlobalTile hook, TilePlaced packet + validation, ITilePlacedConsumer registry, test log consumer; tested: single-player logs placements, dedicated server logs them with the right player, one line per placement)
- [x] 1.4 Server-side network config and rate limiter (patch_mp_1_4_net_config.ps1: NetworkConfig ServerSide ModConfig, per-player token-bucket NetRateLimiter applied in FactorrariaNet.Handle, /fflood; flood test passed on a dedicated server, join sync and tile-placed worked with a second client; config screen, limiter-off and JoinChunkSize checks not yet reported)
  - Notes: Phase 1 exit criteria (request, join sync, tile-placed all working in a server + 2 client session): the two-client run was skipped by decision on 2026-10-17 and is assumed to work (each piece was tested separately with one client). Net core, join sync, tile-placed and network config APIs are in the Phase 1 section. Temporary test code to remove in Phase 10: test-counter and test-blob (JoinSyncSystem), PlacementLogConsumer (TilePlacedSystem), /fflood.
- [x] 2.1 MechSuitPlayer owner gating (patch_mp_2_1_mech_suit.ps1: owner-only toggle, movement and arm launch, controlJump dismount, MechSuitStateRequest/MechSuitState packets, anchor tile and bend side in projectile ai[0..2]; applied, tested with a server + 2 clients on 2026-10-18: toggle, arm sync and anchor/bend were correct, but 3 problems found, fixed by 2.1b below)
- [x] 2.1b MechSuit follow-up (patch_mp_2_1b_mech_suit_followup.ps1: Space dismount on the controlJump press edge, gravity cancelled on remote suited copies in PostUpdateRunSpeeds, owner re-sends its arms every 3 s with netUpdate, [MechSuit] SyncPlayer log line; applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
- [x] 2.1c MechSuit remote movement (patch_mp_2_1c_mech_suit_sync.ps1: 2.1b test found the other client's copy keeps sliding after the owner stops and snaps back after a couple of seconds; remote copy now sets velocity from the synced control bits in PostUpdateRunSpeeds, owner sets netUpdate on input change and every 15 ticks while suited; applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
- [x] 2.1d MechSuit position every tick (patch_mp_2_1d_mech_suit_position.ps1: 2.1c test still desynced, worst after an enemy hit (remote copy took the knockback and kept moving while the owner stayed put); owner now sends its position every tick (PosResyncTicks = 1), and the remote copy sets its velocity from the synced controls in PreUpdateMovement, after any knockback; applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
  - 2.1 to 2.1d: the earlier "still buggy" report came from a stale build (the build was failing from 2.1c on, see 2.4b). After a clean build the suit's movement, the other client's view and enemy hits all worked (tested 2026-10-18). The rework idea (a dedicated owner-position packet) is dropped unless new problems show up.
  - Notes: before testing anything, confirm the build has NO compile errors (a failed build leaves tModLoader running the last good build, which gave false results for 2.1c to 2.4). 2.1 to 2.4b are tested and done; 2.5 and 2.6 are applied, untested (batch test pending). Interim notes: machine slot clicks only edit the client copy until 6.2 (patch_mp_slot_click_fix workaround in UIItemSlotWrapper); the Phase 1 two-client exit run was skipped by decision. Temporary test code to remove in Phase 10: test-counter, test-blob, PlacementLogConsumer, /fflood, the [MechSuit] SyncPlayer log line in MechSuitPlayer.
- [x] 2.2 CartEquipPlayer (patch_mp_2_2_cart_equip.ps1: trim runs on the owner only, then sends SyncEquipment for miscEquips[2] with the slot number computed as 58 + armor + dye + 2; applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
- [x] 2.3 Ice Blade facing / PuffExplosion (patch_mp_2_3_ice_blade.ps1: the owner decides the swing's facing and stores it in Projectile.ai[0] (set at spawn in Shoot, re-sent with netUpdate when the cursor crosses the player), everyone else reads it; ReleasedSwing returns early on a dedicated server; applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
- [x] 2.4 Mechanical Arm pull velocity (patch_mp_2_4_mechanical_arm.ps1: the push (velocity, speed cap, fallStart) and the Jump release run on the owner only; Jump sets ai[0] = 3 + netUpdate and every copy removes itself instead of relying on an owner-side Kill() reaching the others; SendExtraAI not needed because pushDirection is owner-only now; applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
- [x] 2.4b Compile fix + player sync (patch_mp_2_4b_player_sync.ps1: MechSuitItem used Player.netUpdate, which does not exist, since patch 2.1c, so the build FAILED and tModLoader kept the last good build: the results reported for 2.1c to 2.4 may come from a stale build and must be retested after a fresh build. New helper FactorrariaNet.SyncPlayerNow(player) sends vanilla message 13 (owner, multiplayer client only); the suit calls it from PostUpdate, the Mechanical Arm every tick while pushing and retracting plus once in OnKill (grounded push looked frozen for a second or two on the other client because vanilla sent nothing); applied, tested with a server + 2 clients on 2026-10-18 after a clean build: worked)
- [~] 2.5 ConveyorPhysicsSystem (patch_mp_2_5_conveyor_physics.ps1: players = the local player only (the dedicated server pushes none), plus FactorrariaNet.SyncPlayerNow while the belt moves the owner; NPCs = server or single-player only, flagged netUpdate every NpcSyncTicks (4) ticks while carried; items unchanged, same on both sides as vanilla; TryApplyConveyorForce now returns whether it moved the entity; applied, tested 2026-10-18 after a clean build: the local player on belts worked, but NPCs were not carried and dropped items jittered or stayed put on a server + clients, fixed by 2.5b below)
- [~] 2.5b Conveyor NPCs and items (patch_mp_2_5b_conveyor_server_sync.ps1: only the server (or single-player) pushes NPCs and dropped items, clients never do; the server sends a carried NPC or item with vanilla SyncNPC / SyncItem every 3 ticks (NpcSyncTicks, ItemSyncTicks) instead of relying on netUpdate; the server logs [MP-Net] [Conveyor] push and packet counts every 5 s while something is carried; applied, tested 2026-10-18 after a clean build: NPCs are STILL not carried, dropped items now move but jump sporadically; the [Conveyor] server log lines have not been looked at yet; OPEN, see "Open problem: conveyor NPCs and items" in Phase 2; not blocking Phase 3)
- [x] 2.6 VanillaConveyorGlobalItem slot sync (patch_mp_2_6_conveyor_item_sync.ps1: new FactorrariaNet.SyncInventoryItem(player, item) finds the item's inventory slot by reference and sends vanilla SyncEquipment (owner, multiplayer client only); called after the Shift+right-click swap in VanillaConveyorGlobalItem and, same bug, in ClockwisePriorityConveyor and CounterClockwisePriorityConveyor; applied, tested 2026-10-18 after a clean build: worked)
- [x] 2.7 Robotic Arm preparation (A1, A3) (patch_mp_2_7_arm_motion.ps1: RoboticArmMotion owned by the entity and ticked after the worker, worker and visual read it, BaseMachine.IsAnyPlayerNear used by RouteDrop; assumed working by decision on 2026-10-17, not yet tested on a dedicated server; 7b.1 still confirms it)
  - (Patch 2.1 was started from the next-chat prompt that stood here.) The arm now has no client-only dependency in its timing, so Phase 7b.1 only needs to confirm it on a dedicated server. Temporary test code to remove in Phase 10 is unchanged (test-counter, test-blob, PlacementLogConsumer, /fflood).
- [x] 3.1 Custom wires (patch_mp_3_1_custom_wires.ps1: wire and cutter items are owner-only and send WireEditRequest; the server validates (player, tile, reach, current state), edits WireGrid (power grid flagged for rebuild), spawns the returned wire item once and broadcasts WireChanged (the tile's full flags); a rejected edit gets WireChanged to the sender alone; join provider custom-wires; /fwires test command; packets in Common/Net/CustomWireNet.cs; applied, tested 2026-10-18 after a clean build: worked)
- [ ] 3.2 Recipe knowledge, world-shared
- [ ] 3.3 Tile-placed consumers
- [ ] 3.4 Replicas for chest conveyors and awakened statues
- [ ] 4.1 Server-safe geometry
- [ ] 4.2 Placement
- [ ] 4.3 Placement info
- [ ] 4.4 Kill path
- [ ] 4.5 Entity bootstrap
- [ ] 5.1 Snapshot
- [ ] 5.2 Light updates
- [ ] 5.3 Detail channel
- [ ] 5.4 Power flags
- [ ] 6.1 Machine request family and lifetime
- [ ] 6.2 Slot edits
- [ ] 6.3 Recipe selection
- [ ] 6.4 Toggles and guard
- [ ] 7.1 Server authority
- [ ] 7.2 Replication
- [ ] 7.3 Priority conveyors
- [ ] 7.4 Integration review
- [ ] 7b.1 Server worker
- [ ] 7b.2 World-edit broadcast
- [ ] 7b.3 Drop routing
- [ ] 7b.4 Visual events
- [ ] 7b.5 Area request
- [ ] 7b.6 Placement
- [ ] 7b.7 Ghost player
- [ ] 8.1 World liquid broadcast
- [ ] 8.2 Tank sync
- [ ] 8.3 Motor rotate request
- [ ] 8.4 Verification
- [ ] 9.1 Identity and guards
- [ ] 9.2 Replication
- [ ] 9.3 Input as requests
- [ ] 9.4 Cargo and world interaction
- [ ] 9.5 Chest panel
- [ ] 9.6 Events and verification
- [ ] 10 Hardening and release

---

## 1. Goal and success definition

Make Factorraria work in multiplayer on **dedicated servers and Host & Play**, with single-player behavior unchanged.

The mod is "done" for multiplayer when all of this holds with a server and at least two clients:

1. Every machine can be placed, opened, operated and broken by any player, and everyone sees it working (animation, lights, progress, tanks).
2. Conveyor items, chest conveyors, pipes, motors, wires and the power grid behave identically for everyone.
3. Carts can be placed, driven by the world, loaded, fueled and picked up by any player, and everyone sees them move smoothly.
4. Recipe knowledge (world-shared), parchments and the recipe book work for everyone.
5. A player who joins late sees the complete current state; leaving and rejoining loses nothing.
6. Saving, shutting down and reloading the server keeps all of it.
7. No item duplication or loss through UI races, double handling or disconnects.

## 2. Decisions and assumptions

**Decided by you**
- Recipe knowledge is **world-shared**.
- Carts are **server-authoritative with client interpolation**.
- Targets: **Host & Play and dedicated server**.

**Adopted as defaults (change any of them before the phase that uses it)**
- Machine/cart slot edits: **Option A** (client predicts, sends the edit with the old item it expected; server applies only if the slot still matches; otherwise rejects and the client rolls back). Used in Phase 6 and Phase 9.
- Machine and cart UIs close when the player is out of tile reach, dead, or the target is gone.
- Tunables (defaults): cart update rate 15/s, cart interest radius two screens, interpolation buffer 100 ms, machine light-update rate limited, world-liquid broadcasts capped per tick. All live in one server-side config (Phase 1).
- World-shared "seen items" means one player holding an ingredient reveals the recipe for everyone.

**Design assumption to confirm early:** whether Host & Play runs the server in the same process or a separate one is unconfirmed. The plan is written to be correct either way: server logic is gated by `netMode != MultiplayerClient`, client-only logic by `!Main.dedServ`, and every request helper must run the server handler directly in single-player.

## 3. Working rules for every patch

1. **Server owns simulation.** Anything that changes shared state runs where `netMode != MultiplayerClient`.
2. **Requests, not edits.** Clients send a small request; the server validates (target exists, player in reach, state allows it, values sane), applies, then broadcasts. Rejections must leave the client consistent.
3. **Single-player short-circuit.** The request helper calls the server handler directly when `netMode == SinglePlayer`, so single-player never depends on the network path.
4. **Owner gating.** Code that reads local input, `Main.MouseWorld`, `Player.tileTargetX/Y` or spawns something once must check `player.whoAmI == Main.myPlayer`.
5. **Stable identifiers on the wire.** Recipe keys, liquid registry ids, skin keys, cart ids and vItem ids; never list indices.
6. **Three state channels.** Snapshot (on join), delta (on change), detail (only to players who have a UI open).
7. **Reuse existing helpers** instead of re-implementing them.
8. **Patch workflow unchanged.** Each step ships as a patch script (`patch_mp_<phase>_<name>.ps1`) through the existing engine with dry run first. After every patch I will give you a short "what changed / what to remember" list and a "things to test" checklist. Commit and push (including the regenerated `CODEBASE_INDEX.md`) after each successful patch.
9. **New folder `Common/Net/`** holds all packet code so the indexer and the project layout stay clean. Add `docs\*` to `buildIgnore` if you commit these documents to the repo.
10. **Machine logic must not depend on anything client-only** (visuals, `Main.screenPosition`, `Main.LocalPlayer`, textures, UI state). Server logic needs its own timing and its own "is a player near" test. Any new Robotic Arm work should follow this rule from now on (A1, A3).
11. **Build before you test.** A compile error does not stop tModLoader from loading the previous good build, so a test after a failed build tests old code. Check that the build output shows no errors (and, for a fix, that the new behavior is really in game) before reporting test results. `Player` has no `netUpdate`; to send the owner's player state use `FactorrariaNet.SyncPlayerNow(player)`.

## 4. Phase overview

| Phase | Name | Depends on | Size | Risk | Ships on its own |
|---|---|---|---|---|---|
| 0 | Dedicated-server boot and test rig | none | S | low | yes |
| 1 | Net foundation | 0 | M | low | yes (no gameplay change) |
| 2 | Owner gating and projectile sync | 0 | M | low | yes, patch by patch |
| 3 | Shared world state: wires, knowledge, tile-placed consumers | 1 | L | medium | yes |
| 4 | Machine foundation | 1, 3 | L | **high** | no (needs 5 to be usable) |
| 5 | Machine sync | 4 | L | medium | with 4 |
| 6 | Machine interaction and UI | 5 | L | medium | yes |
| 7 | Conveyors and virtual items | 4, 5, 3 | XL | **high** | yes |
| 7b | Robotic Arm | 4, 5, 6, 7 (prep patch 2.7 has no dependency) | L | medium | yes |
| 8 | Liquids and pipes | 3, 5 | M | medium | yes |
| 9 | Carts | 1, 6, 7, 8 | XL | **high** | yes |
| 10 | Hardening and release | all | M | low | yes |

Order of dependency (an arrow means the left side must be done first): 0 → 1 → 3 → 4 → 5; 5 → 6, 7 and 8; 6 + 7 → 7b; 6 + 7 + 8 → 9; 0 → 2 (independent, can start any time after Phase 0); everything → 10.

Phases 4 and 5 are one working unit: after Phase 4 alone, machines exist on the server but clients cannot see their state. Do them back to back and test them together.

---

## Phase 0 - Dedicated-server boot and test rig

**Goal:** prove the mod loads and runs on a dedicated server with no players, and set up the way every later phase is tested. No gameplay change.

**Tasks**
- 0.1 Launch the existing **TerrariaServer** profile (already in `Properties/launchSettings.json`) with a test world; fix any load-time crash. Checklist from step 3/9 (S, R7): every `Load()`/`PostSetupContent()` and UI system must avoid client-only calls (`UserInterface`, textures, fonts, `LiquidTextureCache`, `MachineVisualRegistry` registration, ModLiquidLib registration).
- 0.2 Add log lines (temporary) for the verification items that cannot be read from code: does `TrackData.EnsureLoaded()` succeed on the server (C7); are `AllPipeTiles` and machine entities populated after load.
- 0.3 Work out how to run the server plus two clients locally (second tModLoader instance with its own save folder; flags unverified) and record the procedure in this roadmap.
- 0.4 Confirm Host & Play behavior (same or separate process) by checking `Main.netMode`/`Main.dedServ` in a log line on the host.
- 0.5 Extend the load-time check to the newer systems (A7, A11): `MachineDynamicDrawSystem` (installs an `On_Main.DrawNPCs` detour, add a `!Main.dedServ` guard) and `AwakenedStatueEntries`/`AwakenedStatueRegistry` (asset references must be requested, never loaded with `.Value`, on a server).

**Files:** small guards only if the boot fails; otherwise none.

**Test checklist:** server starts and loads world; simulation ticks with zero players and no exceptions; single-player world loads and behaves as before; log shows the answers to 0.2 and 0.4.

**Exit criteria:** clean server start; verification items 1, 4, 12 and 13 of the master list answered.

**Result:** met. Items 1, 4, 12 and 19 are answered; item 13 only has its first half (the server boots with ModLiquidLib loaded), and the custom-liquid sync half stays open for 8.4.

---

## Phase 1 - Net foundation

**Goal:** the shared plumbing every later phase uses. Covers X1, X2, X3 and the "missing plumbing" list from step 2.

**Patches**
- **1.1 Net core:** `Common/Net/` with a packet-type enum, a router registered from `Factorraria.HandlePacket`, helpers (`IsServerSide`, `HasLocalClient`, `IsOwner(player)`, reach check, "send to players near a point", item write/read via the item serializer so prefix and mod data survive), and the request helper with the single-player short-circuit. Include a ping packet for testing.
- **1.2 Player sync and join sync:** a ModPlayer with `OnEnterWorld` (request full state), `PlayerDisconnect` (cleanup hook list), `SyncPlayer`/`CopyClientState`/`SendClientChanges` stubs. A **join-provider registry**: each system registers a provider that can write and read its full state; the server sends providers one at a time (chunked if large). Note: `ModSystem.NetSend/NetReceive` is called on *every* world-data send (join, boss kill, new day) and must be overridden as a pair, so it is only suitable for tiny state; large state uses the join-request path.
- **1.3 Tile-placed request (G0):** one client-to-server packet (position + tile type) sent from a single shared hook, with a consumer registry (`ITilePlacedConsumer`) so each system declares what it does when the server is told a tile was placed. Server validates the tile at that position matches and the sender is in reach. This is the one mechanism that fixes chest conveyors, liquid topology, awakened statues, pipes, motors, priority conveyors and machine placement. The consumer API must also be callable **directly from server code** (the Robotic Arm already places tiles on the server and triggers these hooks there, A4), and every consumer must be **idempotent** so a double notification from two paths is harmless.
- **1.4 Server-side network config and rate limiter:** a `ServerSide` `ModConfig` for the tunables in section 2, plus a per-player request rate limiter used by all handlers.

**Test checklist:** ping round trip client-to-server-to-client; late-join test with a dummy provider that sends a counter; tile-placed log appears on the server when a client places a block; single-player ignores all packets and still works; kicking/leaving fires the disconnect hook.

**Exit criteria:** the three mechanisms (request, join sync, tile-placed) demonstrably work in a server + 2 client session.

**Net core API (added by patch 1.1, all in `Common/Net/`; later patches build on this)**
- `FactorrariaPacket` (explicit byte ids, append only), `PacketDirection` (`ToServer` / `ToClient`) and `PacketHandler(BinaryReader reader, int sender)`. For `ToServer` packets `sender` is the player index, or `-1` when server code made the request itself (for example the Robotic Arm worker). For `ToClient` packets it is always `-1`.
- `FactorrariaNet.Register(type, direction, handler)` from a `ModSystem.Load()` on every side. `Factorraria.HandlePacket` forwards to `FactorrariaNet.Handle`, which drops unknown ids and packets that arrive on the wrong side, and catches every exception so a bad packet cannot crash the server.
- `FactorrariaNet.SendRequest(type, write)`: on a client it sends the packet; in single-player and from server code it runs the server handler right there through the same write/read path.
- `SendToClient`, `Broadcast`, `SendToNearby(center, radiusTiles, ...)`: do nothing unless this is a dedicated server (in single-player the server state already is the player's state).
- Helpers: `IsServerSide`, `HasLocalClient`, `IsSinglePlayer`, `IsOwner(player)`, `InReach(player, tileX, tileY)` (vanilla tile range + block range + held item boost + `ReachSlackTiles`), `WriteItem` / `ReadItem` (vanilla item serializer: stack, prefix and mod data survive), `Log`.
- Every handler must validate its input (target exists, reach, state, value ranges) and must read exactly what the writer wrote. `/fping` is the test command (a Chat command, so it runs on the client that typed it).

**Join sync API (added by patch 1.2, all in `Common/Net/`)**
- `JoinSync.Register(key, serverWrite, clientRead)` from `ModSystem.Load()` on every side. `key` is a short stable string and is what identifies the provider on the wire (never a list index). `serverWrite` runs on the server and must write the system's complete state; `clientRead` runs on the client, must read exactly what was written and must replace local state (a second request, such as `/fjoin`, runs it again).
- Flow: `FactorrariaPlayerSync.OnEnterWorld` calls `JoinSync.RequestFullState()` -> `JoinRequest(token)` -> the server queues one job per player and, from `JoinSyncSystem.PostUpdateEverything`, serializes one provider at a time and sends one chunk per tick (`JoinSync.ChunkSize`, 6000 bytes) -> `JoinChunk` -> `JoinDone` (provider count, byte count). The client reassembles each provider, calls its reader, then sets `JoinSync.Completed` and raises `JoinSync.JoinCompleted` (UI that depends on synced state can rebuild there). The log line `[MP-Net] join sync #N: ...` lists any provider that did not arrive.
- Guards: one job per player at a time, `RequestCooldownTicks` (120) between requests from the same player, stale tokens dropped, `MaxProviderBytes` cap, a provider that throws is skipped and reported as missing, and a reader that does not consume exactly the bytes written is logged. The tunables now come from `NetworkConfig` (patch 1.4).
- Single-player: the request handler only logs, nothing is sent, `JoinSync.Completed` is true immediately and no provider runs.
- `JoinSync.RegisterDisconnectHook(Action<int> playerIndex)`: runs from `ModPlayer.PlayerDisconnect()` on the server and on remote clients (tModLoader's documented behavior). The disconnecting player's join job and cooldown are cleared first. The mech suit arms (2.1), cart panels (Phase 6/9) and pending requests (Phase 10) register their cleanup here.
- `FactorrariaPlayerSync` (ModPlayer) holds `OnEnterWorld`, `PlayerDisconnect` and empty `SyncPlayer` / `CopyClientState` / `SendClientChanges` stubs for per-player state that other clients need later.
- Temporary test providers `test-counter` (a counter that increases on every full-state generation) and `test-blob` (20000 bytes with a checksum, so it needs four chunks) live in `JoinSyncSystem`; remove them in Phase 10. `/fjoin` re-requests the state and prints the summary in chat.

**Tile-placed API (added by patch 1.3, all in `Common/Net/`)**
- `ITilePlacedConsumer { bool AppliesTo(int tileType); void OnTilePlaced(int x, int y, int tileType, int placer); }`, registered with `TilePlaced.Register(consumer)` from `ModSystem.Load()` on every side. Consumers run only where the simulation runs (single-player, the server), never on a multiplayer client, and must be **idempotent**: the same placement can reach a consumer twice. `placer` is the player index, or -1 for server code or single-player.
- The single shared hook is `TilePlacedGlobalTile.PlaceInWorld`. Systems must NOT add their own `PlaceInWorld` hooks for this; patch 3.3 moves the existing ones (chest conveyor, liquid topology, awakened statues, pipes, motors, priority conveyors, machine placement) into consumers.
- On a multiplayer client the hook sends `TilePlaced` (x, y, tile type). The server (`TilePlaced.HandleRequest`) checks the position is in the world, the type is valid, the tile at that position is that type, and the sender is in reach (`FactorrariaNet.InReach`), logs a rejection otherwise, then runs the consumers. In single-player and on a server the hook runs the consumers directly (after the same tile check).
- Server code that places tiles (the Robotic Arm) needs nothing extra: it already calls `TileLoader.PlaceInWorld`, which reaches the hook. Other server code can call `TilePlaced.RunConsumers(x, y, tileType)` directly.
- Temporary test consumer `PlacementLogConsumer` in `TilePlacedSystem` logs `[MP-Net] tile placed: ...` for every placement (`TilePlacedSystem.LogPlacements`); remove it in Phase 10. Requests are rate-limited per player by `FactorrariaNet.Handle` (patch 1.4).

**Network config and rate limiter (added by patch 1.4)**
- `NetworkConfig` (`Content/Configs/NetworkConfig.cs`, `ConfigScope.ServerSide`) holds every multiplayer tunable. Read it as `NetworkConfig.Current.<Field>` at the point of use (never cache it in a field or constant), so a change in the config UI applies at once. In use now: `ReachSlackTiles` (`FactorrariaNet.ReachSlackTiles` reads it), `RequestsPerSecond` (40, 0 = off), `RequestBurst` (80), and the three join sync values. Reserved for later phases: `CartUpdatesPerSecond` (15), `CartInterestRadiusTiles` (200), `CartInterpolationBufferMs` (100), `MachineStateUpdatesPerSecond` (4), `LiquidBroadcastsPerTick` (20). The last two numbers and the radius were chosen when the config was written (the roadmap only fixed the cart rate and the buffer), so revisit them in the phase that uses them.
- `NetRateLimiter` is a per-player token bucket. `FactorrariaNet.Handle` charges every ToServer packet before its handler runs and drops it when the bucket is empty, with one summary log line per second (`[MP-Net] rate limit: dropped ...`). `FactorrariaNet.Register(type, direction, handler, cost)` takes an optional token cost (default 1) for expensive packets. Server code that calls `SendRequest` itself, and single-player, bypass it. The bucket resets when the player disconnects (through `JoinSync.RegisterDisconnectHook`).
- A handler still has to validate its input; the limiter only bounds how often. `/fflood [count]` is the test command (sends many pings in one tick).
- Known harmless warning: a packet dropped by the limiter never has its body read, so tModLoader logs `[tML] Silently Caught Exception: Read underflow N of M bytes caused by Factorraria in HandlePacket` on the server (seen in the 1.4 flood test). tModLoader catches it and the next packets parse normally. Only appears during a flood. Fixing it needs the body length, which HandlePacket does not receive; leave it unless it becomes noisy.
- Flood test numbers (cost 1, burst 80, 40/s): the first 80 pings of one tick were handled, the rest dropped, and `/fping` worked again afterwards. The drop summary logs the first drop immediately, so its count ("dropped 1") under-reports the real number; later drops are only logged if the flood continues past one second.

---

## Phase 2 - Owner gating and projectile sync

**Goal:** remove behaviors that are wrong or duplicate in MP but are small and independent. Each item is its own patch and can ship any time after Phase 0.

| Patch | Findings | Change |
|---|---|---|
| 2.1 | Step 2 `MechSuitPlayer`, I7 | Owner-gate toggle/arm launch/free movement; use `controlJump`+release flags instead of `PlayerInput`; `NetSend/NetReceive` for `MechSuitArmProjectile` (anchor tile, bend sign); disconnect/death cleanup of arms |
| 2.1b | 2.1 test results | Fix the three problems the 2.1 test found (see the "Mech suit owner gating" block): reliable Space dismount for the owner, gravity cancelled on remote copies of a suited player, and the owner re-sending its arms so a late joiner sees them |
| 2.2 | Step 2 `CartEquipPlayer` | Owner-only trimming of the minecart slot; sync the equipment slot after a change (slot-index mapping to verify, item 11) |
| 2.3 | I5 | Ice Blade swing facing decided once by the owner and synced; guard `PuffExplosion.Spawn()` with `!Main.dedServ` |
| 2.4 | I6 | Mechanical Arm: owner-only pull velocity; instant-retract through synced state instead of owner-only `Kill()` (verify item 7) |
| 2.5 | Step 1 `ConveyorPhysicsSystem` | Players: local player only; NPCs: server (+`netUpdate`); items: same on both sides as vanilla |
| 2.6 | Step 3 `VanillaConveyorGlobalItem` | After the right-click type swap, sync the inventory slot (verify item 6) |
| 2.7 | A1, A3 | **Robotic Arm preparation, single-player safe, recommended before further arm work.** Move reach time, strike length and impact point into a motion model owned by `RoboticArmWorker`, so it no longer waits on `RoboticArmVisual`; add a server-side `IsAnyPlayerNear(worldPos, range)` helper and use it in `RouteDrop` instead of `IsOnScreen`. The visual keeps reading the same model so animation and world change stay in step |

**Robotic Arm preparation (added by patch 2.7)**
- `RoboticArmMotion` (arm folder) holds the hand's target, side, spring, strike timer and impact point, with no texture or screen dependency. The entity owns one (`arm.Motion`) and ticks it right after `worker.Tick()`, the same order the visual used to tick in. The worker calls `SetTarget`, `StartStrike`, `InPosition`, `Striking` and `ImpactPoint` on it; `RoboticArmVisual` only reads it (hand, side, bend, strike progress) to solve the elbow and draw.
- On a multiplayer client no logic tick runs, so `DynamicUpdate` ticks the motion itself there (idle until Phase 7b feeds it the server's target and strike events, A5b). Held-tool length: sprite diagonal everywhere except a dedicated server, which falls back to the item's width and height (so a server's reach timing for a tool can differ by a tick or two from a client's drawing; the world change always follows the server's timing).
- `BaseMachine.IsAnyPlayerNear(worldPos, margin)` is the server-safe "is anyone watching" test (any active player's screen-sized box, `NearPlayerWidth` x `NearPlayerHeight`, tunable). `RouteDrop` uses `IsAnyPlayerNear || IsOnScreen`, so single-player behaves as before and the host's own screen still counts.

**Mech suit owner gating (added by patch 2.1)**
- `MechSuitPlayer` is driven only on the owner's client (`FactorrariaNet.IsOwner`): `UseItem` toggles, `PreUpdateMovement` handles death, the Space dismount (`controlJump` with `releaseJump`, no `PlayerInput`), the arm search/launch and the free movement. The server and remote clients run none of it.
- The flag is mirrored by two packets in `Common/Net/MechSuitNet.cs`: `MechSuitStateRequest` (owner to server, on every toggle) and `MechSuitState` (server to the other clients). A late joiner gets it from `MechSuitPlayer.SyncPlayer` on the server. The mirror exists only so `MechSuitArmProjectile.AI` (which runs on every machine) does not kill the arms of a player who is in the suit. It also resets on `PlayerDisconnect`.
- `MechSuitArmProjectile` carries its anchor tile and bend side in `ai[0..2]`, which travel in the spawn packet, so no `NetSend/NetReceive` or `netUpdate` is needed and nobody ever sees an arm pointing at (0, 0). `Latched` is a local bool each machine works out itself. Remote copies end their own arms when the mirrored flag clears or the owner leaves, so the result does not depend on a kill packet from the owner (verification item 7 is no longer needed for correctness).
- Known limits (the first one is fixed by patch 2.1b, see below): a player who joins while someone's arms are already out sees the arms only when that player next launches one (vanilla does not send existing projectiles to a joiner). `arms[].ProjectileIndex` is now owner-only, which settles finding 4.

**2.1 test results (2026-10-18, dedicated server, 2 clients) and the 2.1b plan**
- Worked: toggling with the item (single-player and multiplayer), the other client sees the arms once the owner remounts, arm anchor tile and bend side are correct on the other client, no exceptions or rate-limit lines in either client log. Because the arms survive on the other client, the mirrored flag (`MechSuitState`) and the `ai[0..2]` spawn data are working.
- Problem 1, single-player: Space does not dismount; toggling the item again does. Cause not confirmed. Suspect: `Player.controlJump && Player.releaseJump` is read too late, so vanilla has already cleared `releaseJump` (or `controlJump` is not set yet) when `PreUpdateMovement` runs. Fix options, in order of preference: (a) track the previous frame's `controlJump` inside `MechSuitPlayer` and dismount on the press edge (no dependence on vanilla's field order); (b) read `PlayerInput.Triggers.JustPressed.Jump` again, still gated by `FactorrariaNet.IsOwner`, which is correct for the owner (the original bug was remote players reading the local keyboard, which the gate already removes). Verify in tModLoader's `Player.Update` where `releaseJump` is written relative to `PreUpdateMovement` before choosing.
- Problem 2, late joiner does not see existing arms; they appear only after the owner remounts. Expected from the "known limits" note but it is not acceptable. Fix: the owner periodically sets `Projectile.netUpdate = true` on its live arm projectiles (for example every 3 to 5 seconds, or on seeing a new active player) so vanilla sends a projectile sync that creates the arm on a client that does not have it. The suit flag for a late joiner should already arrive through `MechSuitPlayer.SyncPlayer`; check that in the server log while testing (not yet confirmed separately).
- Problem 3, the suited player looks like it is constantly falling on the other client, although the arms are in the right place. Cause not confirmed, strong suspect: before 2.1, the remote copy of a suited player also ran `ApplyFreeMovement`, whose `Player.gravity = 0` made the remote copy float between position packets. The owner gate removed that, so the remote copy now falls with normal gravity between corrections. Fix: on a non-owner copy with the mirrored flag set, set `Player.gravity = 0f` in `PreUpdateMovement` (and nothing else), so it extrapolates from the synced position and velocity. Do not override remote velocity. Verify what vanilla's player position packet carries (position, velocity, control bits) in tModLoader's source before relying on it. This also corrects the appendix wording that the whole `ApplyFreeMovement` could be owner-only.
- 2.1b test checklist: single-player Space dismounts and retracts the arms; second client sees a suited player hover steadily, move smoothly and stop where they stop; second client joining while the first is suited sees the arms within a few seconds without a remount; owner disconnecting still removes the arms; nobody else's suit is affected by Space on another client; no errors in either client log or the server.

**What patch 2.1b changed (checked against the tModLoader 1.4.4 patch files; vanilla Player.cs itself is not in the repo, so the order below comes from the order of the patch hunks)**
- Problem 1, cause confirmed: in `Player.Update`, vanilla's jump block (`if (controlJump) { ...; releaseJump = false; } else { releaseJump = true; }`) runs long before `PlayerLoader.PreUpdateMovement`, so `controlJump && releaseJump` could never be true inside the hook. Fix: `MechSuitPlayer` keeps `jumpWasDown` (last frame's `controlJump`, owner only) and dismounts on the press edge. `Activate()` seeds it with the current `controlJump`, so a Space still held when the item is used is not a dismount. Vanilla's jump code still runs on the dismount frame, so a grounded owner may hop slightly.
- Problem 3, cause corrected: vanilla adds gravity (`velocity.Y += gravity * gravDir`) before `PreUpdateMovement`, so `Player.gravity = 0f` set there has no effect (and the owner's own `gravity = 0` in `ApplyFreeMovement` is dead code; what keeps the owner hovering is its velocity overwrite, so that line was left alone). The remote copy now sets `Player.gravity = 0f` in `MechSuitPlayer.PostUpdateRunSpeeds` (after the run speeds, before gravity), only for a non-owner whose mirrored flag is set, and nothing else on it is touched. Vanilla's player packet (message 13) carries the control bits, the position and the velocity (velocity only when it is not zero), and the remote copy keeps extrapolating from those.
- Problem 2: while `netMode == MultiplayerClient` the owner sets `Projectile.netUpdate = true` on its live arms every `ArmResyncTicks` (180 ticks). Vanilla then sends the projectile (message 27) to the server, which relays it; a client without that arm creates it, a client that has it re-applies the same anchor and position. If the suit flag reaches the joiner after the arm, that copy ends on its first tick (flag still false) and the next re-send recreates it, so the worst case is about 3 seconds.
- `SyncPlayer` check: vanilla calls it from `SyncOnePlayer(plr, toWho, fromWho)` with `toWho` = the joining client, after the player info packets, so the flag should arrive before the first re-sent arm. The server logs `[MechSuit] sent suit flag of player X to joining client Y` when it sends it (temporary test line, remove in Phase 10).
- Hook order to remember for later owner-gating patches (2.2, 2.4): `ModPlayer.PreUpdateMovement` runs after vanilla's jump block and after gravity has been applied. To change gravity use a hook before it (`PostUpdateRunSpeeds`); to detect a key press from `controlJump`, compare with the previous frame yourself instead of reading `releaseJump`.

**Conveyor physics (added by patch 2.5)**
- Authority: players move themselves. Only the process that owns a player pushes it (`FactorrariaNet.HasLocalClient` and `Main.myPlayer`), so a dedicated server pushes no player and a client never pushes a remote copy. Vanilla only sends a player's position when a key changes, so while the belt moves the owner the system calls `FactorrariaNet.SyncPlayerNow(player)` (multiplayer client only; a no-op in single-player and on the server). NPCs: only `FactorrariaNet.IsServerSide` pushes them (single-player or server); on a server, a carried NPC gets `netUpdate = true` every `ConveyorPhysicsSystem.NpcSyncTicks` ticks (4, staggered by NPC index) so clients see a slide, not a jump. Clients never push NPCs. Items: unchanged, every side pushes them, the same as vanilla's own belts.
- `TryApplyConveyorForce` returns true only when the entity really moved (a wall blocked it, or it was not grounded, returns false), which is what triggers the sync, so a player standing on a blocked belt sends nothing extra.
- Known limits: the owner's push is 2.5 px per tick and its packet goes out every tick it moves (same cost as the Mech Suit); NPC sync is every 4th tick, so a fast belt may look slightly steppy on clients (raise or lower `NpcSyncTicks`). A boss or a town NPC with its own netUpdate pattern is unaffected.
- **2.5b correction (supersedes the NPC and item rules above):** the 2.5 test showed that NPCs were not carried and dropped items stayed put and jittered. Items were pushed by every side and corrected by server packets, and the NPC `netUpdate` flag did not get the carried NPC to the clients. New rule for both: only `FactorrariaNet.IsServerSide` pushes NPCs and dropped items; clients never do. The server sends the carried entity itself with `NetMessage.SendData(MessageID.SyncNPC / SyncItem, ..., index)` every `NpcSyncTicks` / `ItemSyncTicks` (3) ticks, staggered by index; instanced items are not sent (vanilla uses another message for them). The local player is unchanged. The server logs `[MP-Net] [Conveyor] last 5 s: NPC pushes N, item pushes M, sync packets sent K` while anything is carried (temporary, `ConveyorPhysicsSystem.LogStats`, remove in Phase 10). If NPCs or items still do not move, that log line says whether the server pushed anything (N and M above zero) and sent it (K above zero): zero pushes means the server's own check fails; pushes with packets means the clients are ignoring or overwriting them.
- **Open problem: conveyor NPCs and items (status after 2.5b, 2026-10-18).** Players on belts work (2.5). Not solved: (a) NPCs are not carried on a server + client session, (b) dropped items move but jump sporadically. Nothing is confirmed about the cause. What is known: the hook `PostUpdateNPCs` runs on every side (checked in tModLoader's Main patch); 2.5b made only the server push NPCs and items and send them with `SyncNPC` / `SyncItem` every 3 ticks, and the clients no longer push them; the old 2.5 variant (everyone pushes items) jittered and did not move them. Not yet known: (1) whether NPCs are carried in SINGLE-PLAYER on each belt type (never reported); (2) which belt was used: `TryApplyConveyorForce` only moves NPCs on the two priority conveyors (`if (!(entity is Item)) break;`), on vanilla belts NPCs are left to vanilla; (3) the server's `[MP-Net] [Conveyor] last 5 s: NPC pushes N, item pushes M, sync packets sent K` lines (temporary, `ConveyorPhysicsSystem.LogStats`).
  - Hypotheses, untested, in the order to check them: A) the server never pushes the NPC: its `velocity.Y` at rest can stay above the 0.2 grounded limit, or the NPC's own AI undoes the move; the log's N would be 0. B) the server pushes but the clients ignore or overwrite it: a client NPC runs its own AI and may snap back, or the packet does not carry what is needed (N and K above 0 but nothing moves). C) the items jump because one push is 2.5 px per tick and an item is sent every 3 ticks (7.5 px steps), because vanilla also sends the item on its own schedule, and because a client copy keeps its own velocity and friction between packets; vanilla belts may also push items by themselves on every side (a double push on vanilla belts). D) timing: our push happens in `PostUpdateNPCs`, after vanilla's own NPC and item update and its sync decision for that tick.
  - Ideas for a fix, not tried: send velocity so the client extrapolates; push NPCs by velocity.X instead of position; let clients run a cosmetic-only copy of the push and take only large corrections from the server; or leave carried dropped items as they are and let Phase 7 solve them (items on belts are meant to become virtual items there, which are simulated on the server and interpolated on clients, so the item jitter may be better fixed once, in 7.1 and 7.2).
  - Next step: read the server log lines and test single-player first; decide with that data. Do not write a fix before it.
- 2.5 test checklist: single-player belts (vanilla belts and both priority conveyors) move the player, an NPC and a dropped item exactly as before; server + 2 clients: a player on a belt moves smoothly on its own screen and on the other client's screen, with no stutter and no double speed; a player standing still beside the belt is not pushed; a zombie or other NPC on a belt is carried on both clients without jitter and at the same speed as in single-player; dropped items on a belt slide on both clients and end up in the same place; the dedicated server console shows no errors and the player does not drift on the server when idle off the belt.

**Conveyor item right-click sync (added by patch 2.6)**
- `RightClick` for an item runs only on the client that clicked, never on the server, and the swap is done with `SetDefaults` on that client's copy. Authority: the owner's inventory is the truth, so the owner sends the slot. New helper `FactorrariaNet.SyncInventoryItem(player, item)` (owner + multiplayer client only) finds the slot by reference in `player.inventory` (0-58) and sends vanilla's `SyncEquipment` (type, stack, prefix). It sends nothing for an item that is not in the inventory array (chest or bank item), and nothing in single-player or on a server. Called by `VanillaConveyorGlobalItem.RightClick` and, because they have the identical bug, by `ClockwisePriorityConveyor` and `CounterClockwisePriorityConveyor` (not in the original roadmap row; added on purpose).
- Not synced: `favorited` (the message does not carry it, and the server does not need it).
- 2.6 test checklist: server + 1 client: Shift+right-click a vanilla conveyor stack in the inventory, then check the server's copy (for example leave and rejoin, or use the second client to see the item held in the hand: it shows the new direction); the same for both priority conveyors; stack size and favorite star stay on your screen; a stack in a chest is not changed by this (vanilla behavior); single-player works as before; no errors in the server console or the client log.

**Test checklist (per patch):** two clients each use the item at the same time; neither sees duplicated arms/projectiles; pressing Space on one client does not affect the other client's mech suit or arm; ice blade swings face the owner's cursor on both screens; standing on conveyors moves only your own character on your screen and NPCs don't jitter; swapping a conveyor item's direction survives a relog; no errors on the dedicated server console. For 2.7: in single-player the arm behaves exactly as before (same strike timing, same drops), and with the visual temporarily disabled the worker still mines, chops, hammers and places.

---

## Phase 3 - Shared world state

**Goal:** the small world-level registries that every other phase depends on. Covers wires, recipe knowledge, and every system that needs "a tile was placed". Needs Phase 1.

**Patches**
- **3.1 Custom wires (I1, step 1 `CustomWireSystem`):** `WireAdd/WireRemove` requests from the wire and cutter items (owner-gated; read the target tile from the owner only); server validates, applies, sets `gridNeedsRebuilding`, broadcasts, drops returned wire items server-side; join provider for `WireGrid`; client refreshes its wire frame cache on receive.
- **3.2 Recipe knowledge, world-shared (R2, I2, I3):** join provider and delta packets for unlocked, crafted and seen sets; clients bump `Version`/`SeenVersion` on receive so the UI rebuilds; `MarkSeen` becomes a batched, rate-limited request of newly seen types; `MarkCrafted` from machines (server) broadcasts; parchment reading becomes a `ReadParchment` request: server rolls with its RNG and authoritative sets, unlocks, broadcasts, and replies with the unlocked key so the reader's book opens on it; the parchment is consumed only when the server confirms (keep-item logic moves into the reply handler); debug command made server-readable if wanted.
- **3.3 Tile-placed consumers (G0):** register consumers for chest conveyors (probe), liquid topology (rebuild flag), awakened statues (statue check), pipes and motors (`RegisterPipeTile`, rebuild flags), priority conveyors (`OnPriorityConveyorPlaced`). Add the pipe-list **self-heal on world load** (drop saved pipe positions whose tile is no longer a pipe and re-add pipe tiles found by a one-time scan) to repair worlds already affected by earlier tests (P1).
- **3.4 Replicas for chest conveyors and awakened statues (step 1, U5):** join providers and deltas for chest-conveyor entries and the awakened statue set so the client mode button and `AwakenedStatueDrawSystem` have data; `CycleMode` becomes a request; server broadcasts the new mode.

**Custom wires (added by patch 3.1)**
- Authority: the server owns `CustomWireSystem.WireGrid` (the power grid is built there). `CopperWireItem`, `TinWireItem` and the two cutters run only for the owner (`FactorrariaNet.IsOwner`), because `Player.tileTargetX/Y` is the local mouse tile; every other copy of the player returns `null` and does nothing. The owner calls `CustomWireNet.RequestEdit(add, x, y, type)`.
- Packets (`Common/Net/CustomWireNet.cs`): `WireEditRequest` (8, to server: add flag, x, y, one wire type) and `WireChanged` (9, to client: x, y and the tile's COMPLETE flags, not a delta). The server handler checks sender, alive, a single known wire type, tile in the world, `FactorrariaNet.InReach`, then whether the wire is there already (add) or still there (cut). On success it changes `WireGrid`, spawns the returned wire item with `Item.NewItem` (cut only), logs `[MP-Net] wire added/cut ...` and broadcasts `WireChanged` to everyone except the sender. On rejection it sends `WireChanged` to the sender only, which undoes the sender's prediction.
- Prediction: on a multiplayer client the owner applies its edit at once; in single-player the request runs the server handler directly (no prediction, no packet), same as every request helper.
- Join: provider `custom-wires` writes the count and then x, y, flags for every tile; the client replaces its grid and rebuilds all sprite frames.
- Known limits: the wire item is consumed on the owner's client (vanilla trust model), so a rejected add (out of reach, or two players placing the same tile in the same moment) loses that one wire item; no refund on purpose, because a refund on every rejection would let a modified client mint wire. The owner cuts based on its own copy of the grid, so a stale copy would cut nothing until the next sync. Power networks cannot be viewed on a client until Phase 5; use the server log lines and `/fwires` for now.
- 3.1 test checklist: single-player place and cut copper and tin wire (item consumed on place, returned once on cut, sounds, wire view while holding a wire or cutter); server + 2 clients: A places copper and tin wire and B sees it; B cuts it and A sees it vanish and exactly one wire item drops; both clients place and cut at the same time on neighbouring tiles; a third client joining later sees every wire (`/fwires` on each client matches the server log's `wire tiles now` and the join log line `join provider custom-wires: N wire tiles`); `/fjoin` keeps the wires; placing beyond reach (hack or edge of range) leaves no ghost wire after a second; leave and rejoin; save the world, restart the server, wires still there; holding a wire item shows the wires on the owner only, never on someone else's screen; no errors in the server console or client logs.

**Test checklist:** player A places copper and tin wire, player B (and a late joiner) sees it; cutting wire returns the item to the dropper's world once; power grids form on the server (check a powered machine later); two players read parchments at once, both get different unlocks, both books update and open on the right page; reading an exhausted-pool parchment keeps the item; place a chest next to a conveyor as a client and verify the chest conveyor registers on the server; placing and breaking pipes and motors keeps `AllPipeTiles` correct after save and reload; awakened statues placed by a client start spawning and show their draw effect to everyone; chest-conveyor mode change shows on the other client; world save/reload preserves all of it.

---

## Phase 4 - Machine foundation

**Goal:** machines exist on the server, are created and destroyed only on the server, and have valid geometry there. Covers M1, M2, M5, M6, step 3 `MachineVisualOverride`. Highest-risk phase because it touches the framework every machine uses.

**Patches**
- **4.1 Server-safe geometry (M2):** compute `cornerPosition`, width/height, center and `MachineInitialized` from `TileObjectData` in a method called from `OnPlaced`, `LoadData`, and `NetReceive`; remove the dependence on `PreDraw`.
- **4.2 Placement (M1):** for real ModTile machines (`MotorMK1`, `MechanicalAssembler`, `RoboticArm`) restore the multiplayer branch in `BaseMachine.Hook_AfterPlacement` (client sends the tile square and the entity-placement message, returns `-1`) and move `OnPlaced`, power/liquid registration and geometry into `OnNetPlace()` (server only). For vanilla-tile machines, a tile-placed consumer on the server creates the entity at the right position, runs `OnPlaced`, registers it, and broadcasts it through tModLoader's tile-entity sharing message. Draw and light code switch to lookup-only (`TryGet...`) and never create entities. One shared server-side helper "place machine entity and broadcast" is used by all routes: the tile-placed consumer, `OnNetPlace`, and placements made by server code such as the Robotic Arm's (A4); without it an arm-placed machine would exist only on the server.
- **4.3 Placement info (M5):** biome is currently computed from the local player. The placing client sends a small follow-up packet with its detected biome right after placement; the server applies it to the new entity (and keeps `SaveData`'s stored value).
- **4.4 Kill path (M6):** `KillTile` drops contents and removes the entity only on the server; clients just remove their replica; the machine-killed notification closes any open UI.
- **4.5 Entity bootstrap for existing and world-generated machine tiles (new, found while compiling):** the current code creates missing entities **lazily from draw code**, which also covers vanilla tiles generated by the world (Hellforges, furnaces in houses) and tiles placed before this feature. With draw-time creation removed, the server needs a replacement: a chunked scan after world load for the registered machine tile types that creates missing entities over several ticks, plus `EnsureEntity` on every "open machine" request and on pipe/conveyor adjacency checks.

**Test checklist:** place each machine type as a client on a dedicated server; the server log shows one entity per machine with correct size/center; machines placed via the ModTile path and via the vanilla-tile path both work; break a machine as a client and as the host: contents drop once, entity is gone on server and every client; save and reload keeps machines; world-generated Hellforge and furnaces get entities after load; conveyors can feed and pick up from machines on the dedicated server (geometry fix); biome-dependent recipe behaves according to the placer's biome.

---

## Phase 5 - Machine sync

**Goal:** every client has an accurate replica of each machine. Covers M3, M7, M8, M9 and the sync half of U3. Pairs with Phase 4.

**Patches**
- **5.1 Snapshot:** `NetSend/NetReceive` for `BaseMachine` and subclasses (slots, tanks, flags, geometry, selected recipe by stable key, Materializer `BackgroundActive`, motor `Facing`, fuel state, Robotic Arm `AreaX/Y/W/H`). tModLoader sends it with the section/entity data; recipe and liquid references use stable keys/ids.
- **5.2 Light updates:** a compact, rate-limited packet for `isOn`, `isWorking`, work progress, crafts counter, `useFraction`, fuel burn fraction, and quantized tank type/amount for overlays, sent only to players near the machine and only when changed. Receiving clients update the replica and animation/light follow automatically (animation callbacks are draw-driven and already run only on clients).
- **5.3 Detail channel:** server-side subscriber list per machine (add on open, remove on close, leave, death or out of reach; capped), sending full slot/tank contents on subscribe and after each accepted edit.
- **5.4 Power flags:** make sure the server writes `isOn`/`useFraction` from `PowerNetwork` and that those fields are part of 5.2; confirm wire sync (3.1) makes grids form on the server.

**Test checklist:** machine animation, lights and fire effects work for a second player standing nearby; progress bar and output counter are correct for the player with the UI open; a player far away costs no light-update traffic; a late joiner sees correct state without opening anything; powered machines turn on and off together on all screens when a generator is added or removed; Hellforge infinite lava and Materializer state are visible to all.

---

## Phase 6 - Machine interaction and UI

**Goal:** all machine UIs work for several players safely. Covers U1-U4, U7-U10, M4, M11.

**Patches**
- **6.1 Machine request family and lifetime:** Open/Close with `EnsureEntity`, validation helper (entity exists, reach, state), subscription start/stop; UI stores the machine's position/ID and re-resolves the live replica each frame (U2; this also keeps `MachineUISystem.OpenMachine`, which the arm's area editor reads, pointing at the live replica, A9); UI closes on reach, death or kill (U7).
- **6.2 Slot edits (U1, Option A):** compare-and-swap `SlotEdit` request, server validation (each machine's own accept filter, such as the arm's `IsAcceptedItem`; stack limits; reach), broadcast to subscribers, and rollback of both the slot and the cursor item on a reject. Verify chest-context slot behavior (U8, item 5). Output sound baseline fix (U10).
- **6.3 Recipe selection (U4):** `SelectRecipe` request using stable recipe-group keys; server validates against the machine's recipe list; synced back through the detail channel.
- **6.4 Toggles and guard (M4, U9):** Materializer `BackgroundActive` toggle request; redesign `MaterializerRightClickGuard` to block the vanilla monolith interaction on the Materializer tile instead of undoing it (verify item 8).

**Test checklist:** two players open the same machine and see each other's changes; both try to take the same output at once and only one gets it; a conveyor feeding the machine while a player edits a slot never loses or duplicates items; walking away closes the UI; breaking the machine while another player has it open closes their UI and drops items once; selecting a recipe on one client updates the other's UI; Materializer toggle and monolith right-click behave and nothing desyncs; try shift-click and right-click-hold on slots.

---

## Phase 7 - Conveyors and virtual items

**Goal:** conveyor items are simulated once, on the server, and shown to everyone. Covers the `VirtualItemSystem` blocker, U6/I4, and the conveyor parts of steps 1, 3, 5, 8. Needs machines (Phases 4 and 5) so that intake and output work.

**Patches**
- **7.1 Server authority:** guard `PostUpdateWorld` (`ConvertItemsToVItems`, `CheckPlayerPickups`, movement) to server/single-player; add stable vItem ids; when a world item is converted, also sync its removal to clients (set inactive and send the item sync message); keep `CheckPlayerPickups` as is on the server (it already loops all players using synced positions, so a pickup *request* packet is not needed — it drops a real item at the player, which vanilla then picks up). The Robotic Arm already calls `SpawnVirtualItem`, `IsConveyorTile` and `IsTileOccupied` from the server worker, so these must be safe to call from server code and must hand out ids (A13).
- **7.2 Replication:** spawn/remove/move delta packets for vItems within each player's interest area, item payload serialized with the shared item writer, client-side list with interpolation for drawing; join provider for vItems near the joining player.
- **7.3 Priority conveyors (U6, I4):** `SetConveyorFilter` request (position + item type), filters included in the join provider, priority-conveyor registration via the tile-placed consumer (3.3), make `OnPriorityConveyorKilled` idempotent (it is called from both `KillTile` and `CanDrop`).
- **7.4 Integration review:** chest-conveyor and cart paths that insert into chests must sync the slot after `VItemStacking` edits; Extractinator outputs; `VItemGlobalItem` immunity timer remains server-only.

**Test checklist:** a long conveyor line looks the same for host and clients and does not stutter; items on conveyors never duplicate when a client stands on, picks up or drops items; dropping items on a belt as a client works; priority filter set by a client works and survives save/reload; items reach machines and chests and get pulled out for every player; late joiner sees belts already carrying items; heavy test: hundreds of vItems, check bandwidth and server tick time; throw items at a belt next to a cart and next to a chest conveyor.

---

## Phase 7b - Robotic Arm

**Goal:** the arm works on a server and every client sees it. Covers A1-A5b and A12-A14. Needs machine entities and sync (Phases 4-5), slot edits (6.2) and server-authoritative vItems (7.1). Patch 2.7 (the single-player refactor) comes first.

**Patches**
- **7b.1 Server worker (A1):** confirm on a dedicated server that the worker reaches, strikes and finishes with no visual present (the motion model from 2.7 drives all timing).
- **7b.2 World-edit broadcast (A2):** after each strike (pick, axe, hammer, wall, plain and object placement) the server sends the affected area with `SendTileSquare`, sized to cover multi-tile footprints and neighbor frame changes. Drops from tile kills are server `Item.NewItem` (auto-synced) or vItems (7.1).
- **7b.3 Drop routing (A3, A13):** `IsAnyPlayerNear` decides world item versus vItem; verify that items inactivated inside `OnSpawn` do not appear as ghost items on clients.
- **7b.4 Visual events (A5b):** `TargetChanged` and `StrikeStarted` packets to players near the arm; the client `RoboticArmVisual` follows them, goes idle on power loss, and draws the held item from the replicated input slot.
- **7b.5 Area request (A5):** the area editor previews locally and sends one `SetArea` request on mouse release; the server clamps with the same `SetArea` and rebroadcasts; `AreaX/Y/W/H` are already in the snapshot (5.1).
- **7b.6 Placement (A4, A6):** a player placing the arm goes through the ModTile path (4.2); verify the server accepts placement made through the item's mode switch (item 15), otherwise add a placement request; arm placements of machine tiles use the shared broadcast helper and idempotent consumers.
- **7b.7 Ghost player (A12):** verify the vanilla calls driven by the hidden `Player` never use its `whoAmI` as an ignore-client argument (it would skip client 0).

**Test checklist:** a client places an arm and another client sees it; one client changes the area and the other sees the overlay data after reopening and the arm obeying it; the arm mines, chops, hammers walls and places blocks, furniture and torches while clients watch (tiles change for everyone, once); drops arrive once, as world items when a player is near and as vItems otherwise; output onto an adjacent conveyor works; arm animation shows target and strikes on every client and matches the world change; no power means idle on all screens; a late joiner sees correct pose and area; server restart keeps area and slot; two arms and a player working the same tile do not duplicate drops; the arm placing a machine, pipe, chest or statue creates its entity/registration on server and clients exactly once; breaking the arm drops its contents once.

**Exit criteria:** an arm can run unattended on a dedicated server with two clients watching, with no desync, duplication or exceptions.

---

## Phase 8 - Liquids and pipes

**Goal:** liquids move through pipes and tanks correctly for everyone. Covers P2-P7. Needs Phase 3 (pipe registration) and Phase 5 (tank channel).

**Patches**
- **8.1 World liquid broadcast (P2):** after any world liquid edit by the network the server sends the water update message; collect changed positions per tick, send each once, with a per-tick cap from the server config.
- **8.2 Tank sync (P4):** quantized tank type/amount in the light channel; full precision only through the detail channel.
- **8.3 Motor rotate request (P3):** right-click becomes a `RotateMotor` request; the server rotates, flags a network rebuild and broadcasts; clients reframe neighbor pipes on receipt.
- **8.4 Verification (P5-P7, S2):** confirm ModLiquidLib synchronizes custom liquid ids in tile data and water updates; decide the liquid debug overlay stays single-player only.

**Test checklist:** a pump line from a lake into a Solidifier works with players watching from afar and up close; draining and filling world liquid is visible to everyone and matches the server; rotating a motor as a client re-routes flow on the server; pipes placed by clients connect to motors and machines after a reload; custom oil and fuel liquid display correctly for clients; no flood of water messages in the log.

---

## Phase 9 - Carts

**Goal:** the cart system works in multiplayer. Covers C1-C10 and `CartInteractionPlayer`. The largest feature phase; depends on slot-edit infrastructure (6.2), vItem authority (7.1) and the liquid broadcast fix (8.1).

**Patches**
- **9.1 Identity and guards (C2, C9):** stable cart ids (saved, with a next-id counter; ids assigned to old saves on load); `PostUpdateWorld` simulation gated to server/single-player; clients get an interpolation-only update.
- **9.2 Replication (C1):** spawn/remove/move/state packets, interest radius, join provider for carts in range, skin sent as key, module state packets on change.
- **9.3 Input as requests (C3):** remove the single-player gates; client-side click detection and hologram stay local; place (server validates range/overlap/rail and the held item and acknowledges so the client consumes it), install module, shove, pick up, panel open/close, fuel add, shift-insert; finalize `CartInteractionPlayer` and the equipment-slot fix from 2.2.
- **9.4 Cargo and world interaction (C4, C6):** server removes picked-up world items with proper item sync; spills and fuel pickups server-only; liquid broadcast from the server (`sendWater` guard changed from "client" to "not single-player"); pressure plates take the existing direct-call branch on the server (remove the dead client branch).
- **9.5 Chest panel (C5):** subscription, compare-and-swap slot edits (reuse 6.2), cart located by id each frame, close on reach/removal.
- **9.6 Events and verification (C7, C8):** bump/pour event broadcast for sound and effects; `TrackData` verified on the server and `Main.NewText` replaced by logging; interpolation tuning.

**Test checklist:** a cart placed by client A appears for B and the host; carts run along tracks smoothly on remote screens; chains and carriages move together; loading a cart panel on two clients at once never dupes; fueling and module install work for any player; terrarium pouring changes liquid for everyone; pressure plates trigger on the server once; picking a cart up returns the right skin item once; a cart leaving a player's range and returning looks right; carts survive server restart and late join; test a long run on slow connection settings.

---

## Phase 10 - Hardening and release

**Goal:** make it safe and releasable. Covers step 10 and all remaining verification items.

**Tasks**
- Audit every request handler: target exists, reach, state, value ranges, rate limit; log rejections.
- Disconnect, death and leave cleanup: subscriptions, mech-suit arms, open cart panels, pending requests.
- Late-join soak test with every join provider active at once; size check of join traffic.
- Robotic Arm soak: several arms working while players walk through their areas.
- Bandwidth and tick-time profiling (many machines, many vItems, many carts); tune values in the server config.
- Localization strings for rejection and "too far" messages; server messages to one player via chat-to-client, not `Main.NewText`.
- Update `PROJECT_CONTEXT.md` with a multiplayer section (rules from section 3, packet module layout) and refresh `description.txt`.
- Re-run the whole master verification list below and close each item.

**Test checklist:** the full scenario matrix in section 7.

---

## 5. Traceability: every finding and where it lands

| Finding(s) | Phase / patch |
|---|---|
| X1, X2, X3 | 1.1, 1.2, 1.3 |
| X4 (unguarded `PostUpdateWorld`) | 7.1 (vItems), 9.1 (carts) |
| `VirtualItemSystem` | 7.1, 7.2 |
| `CartSystem`, `CartChestUISystem`, C1-C10, `CartInteractionPlayer` | Phase 9 (C3 also 2.2) |
| `CustomWireSystem`, I1 | 3.1 |
| `PowerGridSystem`, M7 | 5.4 |
| `LiquidNetworkSystem`, P1 | 3.3 (registration, self-heal) |
| P2, P4, P3, P5-P7 | 8.1, 8.2, 8.3, 8.4 |
| `ChestConveyorSystem`, U5, `ChestConveyorGlobalTile` | 3.3, 3.4 |
| `ConveyorPhysicsSystem` | 2.5 |
| `RecipeKnowledgeSystem`, R2, I2, I3 | 3.2 |
| `AwakenedStatueSystem`, `AwakenedStatueGlobalTile`, `AwakenedSpawn*` | 3.3, 3.4 |
| `MachineUISystem`, U1-U4, U7, U8, U10 | 6.1-6.3 |
| U9, M4 | 6.4 |
| U6, I4 | 7.3 (item swap in 2.6) |
| U11, U12 | covered by 5.x and 3.2 (no extra work) |
| G0 | 1.3 + consumers in 3.3 and 4.2 |
| `MachineVisualOverride`, M1, M2, M5, M6 | 4.1-4.4 (+ 4.5 new) |
| M3, M8, M9, M10 | 5.1, 5.2 (M10 in 3.2) |
| M11 | 6.1 |
| `LiquidTopologyGlobalTile` | 3.3 |
| `MaterializerWireGuard` | Phase 0 check (machine registry exists on the server), 6.4 test |
| `VItemGlobalItem` | 7.2 (item serialization), 7.4 |
| `VanillaConveyorGlobalItem` | 2.6 |
| `MechSuitPlayer`, I7 | 2.1 |
| `CartEquipPlayer` | 2.2 |
| I5, I6 | 2.3, 2.4 |
| `VanillaCartItemGlobal`, `CartModuleItemGlobal`, `CartMountBlocker`, `CartTrackHookBlocker` | Phase 9 (placement via 9.3; verify no hook flash in 9.6) |
| `LiquidInteractionEdits` | 8.4 (item 9) |
| `MachineOverhangSystem`, `AwakenedStatueDrawSystem`, debug overlays | Phase 0 `dedServ` check; overlays stay single-player (8.4) |
| `AwakenedSpawnGlobalNPC`, `AwakenedSpawnSystem` | 3.3/3.4 test (looked correct in survey) |
| R1, R3-R6, R9, I8-I10, `RecipeBookPlayer`, `ExtractinatorBlockPlayer`, `TimedCrafting*`, joke/material items, buckets (P8), custom liquids (P9), P10 | no change needed |
| R7, S2, S3 | Phase 0, 8.4 |
| S4, S5 | 1.4, Phase 10 |
| A1, A3 | 2.7 (prep), 7b.1, 7b.3 |
| A2, A5b, A12, A13, A14 | 7b.2, 7b.4, 7b.7, 7b.3 (A14 needs no change) |
| A4 | 1.3 (server-callable, idempotent consumers), 4.2 (shared helper), 7b.6 |
| A5 | 5.1 (snapshot), 6.2 (accept filter), 7b.5 |
| A6 | 7b.6 (verification item 15) |
| A7, A11 | Phase 0.5 check; visuals work once Phases 4-5 are in |
| A8, A9 | 4.1 (default geometry), 6.1 (`OpenMachine`) |
| A10 | no change needed |
| Join-state items (wires, knowledge, chest conveyors, statues, vItems, carts) | providers in 3.1, 3.2, 3.4, 7.2, 9.2 |

## 6. Corrections found while compiling

1. **Pickup request packet is not needed (step 1).** `CheckPlayerPickups` loops all players using synced positions and drops a real item at the player on the server; vanilla then picks it up. (Phase 7.1.)
2. **Entity bootstrap (new finding 4.5).** Removing draw-time entity creation requires a replacement for world-generated and pre-existing machine tiles.
3. **Join sync mechanism.** `ModSystem.NetSend/NetReceive` is called on every world-data send, not only on join, so large state uses a join request instead.
4. **Placement hook names.** tModLoader's helper is `Generic_HookPostPlaceMyPlayer` / `Generic_Hook_AfterPlacement`; the server entry point is `NetPlaceEntityAttempt`, which calls `OnNetPlace()` and then broadcasts the entity. `BaseMachine.Hook_AfterPlacement` currently replaces that flow and has its multiplayer branch commented out.
5. **Recipe parchment is read through `RightClick`**, not `UseItem` (step 2 wording fixed in the appendix).
6. **Pipe registration** was "verify" in step 1; step 7 confirmed it as a blocker.
7. **Survey base moved (version 2).** Version 1 was written against `25510ef`. The 28 later commits add findings A1-A14 (see the version 2 section and appendix step 11). Nothing in the original findings was invalidated; two statements were extended (real-ModTile machines now include the Robotic Arm; one more machine tile entity exists).

## 7. Test environment and scenario matrix

Set up in Phase 0, used in every phase.

| Scenario | What to check |
|---|---|
| Single-player | Behavior unchanged after every patch |
| Dedicated server + 1 client | Basic function of the phase |
| Dedicated server + 2 clients | Concurrency: same machine/cart/chest, simultaneous placement |
| Host & Play + 1 client | Host acts as both player and server |
| Late join | Join after machines, belts, wires, carts exist |
| Leave and rejoin | Open UI/arms/panels cleaned up; nothing lost |
| Save, stop, restart server | All state persists; ids and registries stable |
| Slow connection (simulated latency) | Interpolation, rollback and rejection behavior |
| Robotic Arm (Phase 7b) | Arm working with players near and far, area edits by two clients, arm placing machine tiles, power loss mid-strike |

### Phase 0 test rig (verified in the first session except where marked "to confirm")

The probe writes `[MP-Boot]` lines through `Mod.Logger.Info`: into `client.log` for any process that has a client (single-player, Host & Play host, joining client) and into `server.log` on a dedicated server. Both are in the tModLoader `Logs` folder (location to confirm).

1. **Dedicated server:** run `start-tModLoaderServer.bat` from the tModLoader install folder (Windows; the `TerrariaServer` Visual Studio profile starts the same server), choose a test world, and wait for it to load. The server needs Factorraria, ModLiquidLib and Luminance enabled. Its log is `server.log`.
2. **Join it:** Multiplayer, Join via IP, `127.0.0.1`. Start the server first. The client log (`client.log`, or `client2.log` for a second simultaneous instance) shows `netMode=MultiplayerClient`.
3. **Second local client:** a second tModLoader instance needs its own save folder. The `-savedirectory <path>` flag is the first thing to try (to confirm); record what actually worked here.
4. **Single-player regression:** load a normal world; behavior is unchanged and the log shows one `world loaded` and one `summary` line.
5. **Host & Play:** the host process is a client (`netMode=MultiplayerClient`), so the server runs in a separate process.

**Observed in Phase 0 (same test world: 589 pipe tiles, 79 machine entities in single-player and on the server)**

| What the probe saw | Single-player | Dedicated server | Joining client |
|---|---|---|---|
| Pipe tiles registered | 589 | 589 | 0 (Phase 3.3 must register them on clients) |
| Machine entities | 79 | 79 | 6 (how entities reach clients is for 4.5/5.1) |
| Machines with geometry initialised | 1 | 0 | 0 (confirms M2: geometry is only set up from draw code; Patch 4.1) |
| Visual textures loaded | 13 of 13 | 0 of 13 (requests only) | 13 of 13 |
| Awakened statue entries | 44 | 44 | 44 |

- The server's `summary` line arrived about 2 seconds after the client connected but 30 seconds after the world loaded. That is consistent with an empty dedicated server not running its update loop until a client connects, so machines would not run while nobody is online. Treat it as expected unless a later phase needs otherwise.
- The `[MP-Boot]` probe stays in for now (`MultiplayerBootProbeSystem`, `EnableProbe`); later phases reuse it to check state on each side. Remove it in Phase 10.

## 8. Master verification list (open items)

| # | Item | Resolved in |
|---|---|---|
| 1 | Host & Play: same or separate process | **Answered (Phase 0): separate process.** The Host & Play host process logged `netMode=MultiplayerClient`, not `Server`. A standalone server logged `pid=2292 netMode=Server dedServ=True`, a different pid from the client (3888) |
| 2 | `PlaceInWorld` is client-only on a dedicated server (call site confirmed in tModLoader source) | Phase 1.3 test |
| 3 | Pipe registration and self-heal on server | 3.3 (partly seen in Phase 0: a dedicated server registers all 589 pipe tiles at world load, same as single-player; a joining client registers 0; placement self-heal still to verify) |
| 4 | `TrackData.EnsureLoaded` works on a dedicated server | **Answered (Phase 0): loaded** on the dedicated server and on a joining client; 9.6 still adds the logger-only failure message |
| 5 | `ItemSlot.Handle` with chest context sends no chest sync with index -1 | **Answered (patch 2.7 follow-up): it does send one and throws `IndexOutOfRangeException` on a multiplayer client** (seen clicking the Robotic Arm slot). `UIItemSlotWrapper` now runs the click with netMode shown as single-player (interim). The edit stays client-side until 6.2 adds `SlotEdit`; 6.2 should replace this workaround |
| 6 | Right-click `ItemSlot` path syncs the changed slot | 2.6 applied, untested: not assumed, the item sends the slot itself (duplicate with a vanilla send, if any, is harmless). The 2.6 test settles whether vanilla already did |
| 7 | Owner-side `Projectile.Kill()` propagates to other clients | 2.1 and 2.4 no longer depend on it (remote suit arms end themselves from the mirrored flag; the Mechanical Arm's Jump release is the synced state ai[0] = 3); the question itself is still open |
| 8 | Monolith right-click frame flip sent before the guard undoes it | 6.4 |
| 9 | `LiquidMerge` stone result reaches clients | 8.4 |
| 10 | `UseItem` runs for remote players on every client (owner gate is correct either way) | 2.x tests |
| 11 | `SyncEquipment` slot index mapping for `miscEquips` | **Answered (patch 2.2, tested 2026-10-18):** 58 + armor.Length + dye.Length + index works; the other client sees the single cart in the slot |
| 12 | No client-only call in any `Load()`/`PostSetupContent()` | **Answered (Phase 0):** the dedicated server loads the mod and the world with no exception. Visual registry: 13 definitions registered, 0 On textures loaded on the server (requests only, as designed) |
| 13 | ModLiquidLib custom liquid sync | Phase 0 (server boots with ModLiquidLib loaded), 8.4 (the sync itself, still open) |
| 14 | Tile-entity removal is broadcast to clients when the server kills a machine (added while compiling; items 1-13 come from step 10) | 4.4 |
| 15 | Server accepts placement of the Robotic Arm made through the arm item's client-only mode switch (A6) | 7b.6 |
| 16 | Which `WorldGen` kill/place calls broadcast when run on a server (A2); the fix (explicit `SendTileSquare`) works regardless | 7b.2 |
| 17 | The arm's hidden `Player` `whoAmI` is never used as an ignore-client argument (A12) | 7b.7 |
| 18 | Items inactivated inside `OnSpawn` do not appear on clients (A13) | 7b.3 |
| 19 | `MachineDynamicDrawSystem` detour on a dedicated server is harmless; add the guard (A7) | **Answered (Phase 0.5):** guard added (also on the statue draw and overhang preview detours); the server boots and runs cleanly with them |
| 20 | NPCs and dropped items carried by belts in multiplayer: server push check, sync, client smoothing (see Phase 2, "Open problem: conveyor NPCs and items") | open; 2.5c or Phase 7.1/7.2 |

## 9. Biggest risks

- **Phase 4/5 (machine framework):** every machine depends on it; mitigate by shipping geometry (4.1) first, testing in single-player after each patch, and doing 4 and 5 as one test unit.
- **Phase 7 (virtual items):** performance and consistency under load; mitigate with interest-based sending, deltas, and the profiling pass.
- **Phase 9 (carts):** biggest new code; mitigate by building replication (9.2) before any interaction (9.3) and testing interpolation on its own.
- **Phase 7b (Robotic Arm):** the worker was written against a client-side visual. The motion-model refactor (2.7) is the main risk, but it is single-player testable, so land it first; keep new arm features free of client-only dependencies (rule 10).
- **Item duplication through UI races:** mitigated by compare-and-swap edits, server-only drops and consumption on acknowledgement; each phase's test checklist includes a race test.

## 10. What stays untouched

Recipe registries and fuel tables, the `BaseMachine` recipe/fuel hooks, the thin machine subclasses, worldgen and chest loot, buckets, custom liquid definitions, materials, ores, joke items, the local recipe-book UI, `TimedCrafting*`, `ExtractinatorBlockPlayer`, and the client-only configs. Also unchanged: the arm's visual animation math (`RoboticArmVisual`, `MechanicalArmIK`), the dynamic-draw passes, the arm's tile-selection logic and the recipe data changes.
