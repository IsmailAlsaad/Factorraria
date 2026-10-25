# Liquid overhaul roadmap and context handoff

Suggested home in the repo: `docs/liquid_overhaul/ROADMAP.md` (the multiplayer plan already uses `docs/multiplayer/ROADMAP.md`, so keep them apart).
Written 2026-10-25 after step 2; updated by the step 3 patch (2026-10-25). The code wins wherever this file disagrees with it.

## 0. How to use this file in a new chat

1. Make sure the step 1 and step 2 files are on GitHub (`git log` in the clone should show them; see section 2 for the list). Ismail applies patches on his disk first, so ask him to commit and push if the clone does not have `Common/Networks/FlowNetwork.cs`.
2. Clone the repo, read this file, then read only what the requested step names. Do not load whole files you do not need.
3. Check the Progress section. Do not start a step whose predecessor is not marked `[x]` or at least `[~]` with Ismail's test report pasted in.
4. Follow `PROJECT_CONTEXT.md` (patch script workflow, no `const`/`static` unless forced, reuse existing helpers, multiplayer first, art is Ismail's). One chat = one step.
5. Each step's patch edits the Progress section below (`[~]` applied untested). When Ismail reports test results, a small follow-up patch records them.

## 1. Goal and locked decisions

Replace the per-tile "flow field" in the pipe system with a simple model that behaves the same in every edge case. These decisions are Ismail's and are final unless he changes them:

- **Pipes hold no liquid.** A network is the connected pipe graph. Liquid moves instantly between ports, limited by pump budgets.
- **Motors are one-way pumps.** A machine behind a pump (intake side) is a source, in front of it a sink. Role comes from which side of a pump it sits on, not from a flow magnitude.
- **One liquid type at a time per network.** It takes the first type it sees from its sources, silently ignores other types until that one runs out (or nothing can take it), then moves to the next. Mixed networks show no error. The active type is memory only: not saved, not reset on a power flip.
- **Global cap = the weakest pipe tier in the network.** The bottleneck is intentional. An OFF motor counts as a plain pipe at its `PipeEquivalentMaxFlowRate` (so that property stays, contrary to the first draft of the plan).
- **Climb penalty kept:** 0.5 units/tick per climbed tile. Physics fixed: only where liquid really rises (source below the pump pays on the intake side; the discharge side pays where it goes up). A source above the pump pays nothing.
- **Series pumps supported.** Route rate = the weakest pump's rate; pumps in series add their strength to the climb budget.
- **Empty tanks forget their liquid type** (remove `MinTankKeep`).
- **Debug is fully visual under the existing `EnableDebugs` config toggle**; `/fpipes` and `/fflow` print the same data as text.
- World liquid keeps moving in whole-tile chunks (255) with banking, infinite-source detection stays, `FindSinkSlot` and `ResolveIncomingLiquid` semantics stay.

## 2. Current state (what exists in the code)

### Pure core (no Terraria types), `Common/Networks/Flow/`
| File | What it does |
|---|---|
| `FlowTypes.cs` | `FlowDir` (same order as `Direction`: Up, Down, Left, Right), `FlowNode` (tile, motor on/off, facing, strength), `FlowPort` (attach point: node, mouth, owner), `FlowRoute` (src, dst, climbs, pumps, `RatePerMinute`), `FlowTransfer` (route, src, dst, type, amount). |
| `FlowRouteFinder.cs` | `Build(nodes, sources, sinks, decayPerClimb, cap)`: Dijkstra per source attach point over (node, passedAnOnPump) states. An ON motor passes only forward, an OFF motor passes both ways on its axis and only counts as a route if some ON pump is on the route. Climb = a step to a smaller Y. Rate = min(weakest pump, sum of pump strengths minus climbs x decay), then min with cap. Never routes a machine into itself (same `Owner`). |
| `FlowRouteMerger.cs` | Step 2. `FlowLinks` (structural link rule: pipes on every side, motors on their axis only) and `Merge(raw, srcPortsOfAttach, dstPortsOfAttach)`: turns attach-level routes into port-level routes, one per (source port, sink port), best rate then fewest climbs. |
| `FlowSolver.cs` | `Solve(routes, sourceOffer[], sourceType[], sinkCount, sinkSpace(sink, type), pumpBudget[], output)`. One active type (`ActiveType`), proportional sharing of sources, sinks and pumps, 3 rounds, never over-draws or over-fills. `pumpBudget` is indexed by node and consumed in place (units per tick). |

### Game layer (Step 2), `Common/Networks/`
- `FlowNetwork.cs`
  - `FlowSource`: one machine output tank (`Machine`, `Tank`, `TankIndex`) or one world cell (`WorldPos`). `FlowSink`: one machine (all its input tanks together) or one world cell. Both have `Owner`.
  - `FlowNetwork`: `Positions[]`, `Nodes[]` (`FlowNode`), `Motors[]` (null for pipes), `PassRate[]`, `Sources`, `Sinks`, attach arrays, `Routes` (port level), `Solver`, `Cap`, `DecayPerClimbPerMinute` (0.5 x 3600), `BuildMs`, `RefreshMs`. `RefreshRoutes()` re-reads each motor's `isOn`/`PumpStrength`, recomputes the cap, rebuilds routes (no topology rebuild needed).
  - `FlowNetworkBuilder.BuildAll(HashSet<Point> allPipes)`: flood fill from the registered pipes (a motor joins only on its axis), nodes sorted by (X, Y), attach points (machines: any pipe or motor-axis neighbour that is a `BaseMachine`; world: the same open-side rule as before via `PipeConnectionHelper.GetOpenSides`, never into solid tiles), ports collapsed per tank / machine / world cell.
- `FlowNetworkDiagnostics.cs`: `/fflow` and `/fflow all`.
- `LiquidNetworkSystem` (edited in step 2): `public static List<FlowNetwork> FlowNetworks`; `RebuildFlowNetworks()` after each rebuild; `RefreshFlowNetworks()` when `flowNeedsRecalculating`; both wrapped in try/catch; cleared on world load/unload. Nothing reads `FlowNetworks` except `/fflow`.

### Still the live system (until step 3)
`LiquidNetwork.Tick()` (flow field, `ResolvedFlow`, `LiquidType` lock, `MinTankKeep`, `IsPumpedOutBySibling`, world banks) still moves all liquid. Its users: `LiquidNetworkSystem`, `LiquidNetworkDebugSystem`, `Common/Net/LiquidPipeDiagnostics.cs`, `MotorTileEntityBase` (the `flowNeedsRecalculating` flag), plus rebuild-flag setters in `MotorTileBase`, `PipeTileBase`, `TileEntityHelper`, `MotorRotate`, `MachinePlacement`, `TilePlacedConsumers`, `LiquidTopologyGlobalTile`. `LiquidBroadcastSystem.Mark` (8.1) is called from the world-tile withdraw/deposit code and must keep being called.

### Behaviour differences already introduced in the topology (visible in `/fflow`, not yet in play)
- A pipe touching the SIDE of a motor is no longer pulled into the motor's network (the old flood fill did that).
- A machine touching the side of a motor is not attached (the old code treated it as a sink).
- A machine touched by several pipe tiles is one port, not several.
- Seeds are still only registered pipes, as before: a motor between two machines with no pipe at all forms no network.

### Sandbox tests (not compiled by tModLoader: `.txt`)
`tools/flow_tests/`: `Program.cs.txt` (core, 40,547 checks), `Stubs.cs.txt` + `TopologyTests.cs.txt` (the real builder against stand-in Terraria types, 38 checks, includes a 600-pipe timing), `README.txt`. Rerun them after any change to the core or the builder (section 7).

## 3. Progress

One line per step. `[ ]` not started, `[~]` applied untested, `[x]` tested and done.

- [x] 1 Pure core: route finder + solver (`patch_flow_core.ps1`; 40,547 sandbox checks; inert in game)
- [x] 2 Topology build + `/fflow` (`patch_flow_topology.ps1`; tested in game, everything works, builds 0.03-0.05 ms)
- [~] 3 New tick (`FlowNetwork.Tick`), `LiquidWorldBanks`, tanks forget type (`patch_flow_tick.ps1`; applied untested, no `UseNewFlow` switch by Ismail's decision)
- [~] 4 Motors: `flowNeedsRecalculating`, `wasOn` and the old reset deleted; `FlowNetwork.Tick` detects power flips (`patch_flow_motors.ps1`; applied untested)
- [~] 5 Visual debug overlay on `FlowNetworks`, `/fpipes` reads `FlowNetworks` (`patch_flow_debug.ps1`; applied untested)
- [ ] 6 Cleanup of the old system, docs

### Step 3 decisions (Ismail, 2026-10-25)
- World banking: the per-cell `withdrawBank` / `depositBank` design of step 3 below, shared by all networks in one `LiquidWorldBanks` (`LiquidNetworkSystem.WorldBanks`), `FlushAll()` before every flow-network rebuild.
- No `UseNewFlow` switch and no `/fflow old|new`: the new tick simply replaces the old one. The old `LiquidNetwork` objects are still built (the old debug overlay and `/fpipes` read them) but are never ticked, so their `LiquidType` / `ResolvedFlow` are stale until steps 5 and 6.
- `MinTankKeep` only existed inside the old `LiquidNetwork`; it is gone from the live path. Removing the old class is step 6.
- The Hellforge no longer refills its own lava (`HellforgeTileEntity` has no `Update()`); the old note in PROJECT_CONTEXT.md is stale.
- `FindSinkSlot` now lives in `Common/Liquids/LiquidTankRules.cs`; the old `LiquidNetwork.FindSinkSlot` delegates to it.
- World cells: `ClearTile()` is only called when the cell has no tile (the old code called it unconditionally, which could clear a non-solid tile that shared a cell with liquid).

## 4. The new data flow in one picture

```
tiles + machines --BuildAll--> FlowNetwork { nodes, ports, attach points }
                                   |  RefreshRoutes()  (topology change, power flip)
                                   v
                          Routes (port -> port, rate, climbs, pumps)      <- FlowRouteFinder + FlowRouteMerger
                                   |
 each server tick:  offers + types + sink space + pump budgets --FlowSolver.Solve--> FlowTransfer list
                                   |
                                   v  apply: withdraw from tanks / world cells, deposit into tanks / world cells
```

Multiplayer authority for ALL steps: the server (or single-player) is the only side that builds and ticks the networks (`PostUpdateWorld` returns on a client). Nothing here is saved or sent. Clients learn about changes through the existing 5.2 tank light updates (`MachineLightSync`) and the 8.1 world-liquid broadcast (`LiquidBroadcastSystem.Mark`). No new packets in any step. Single-player must keep working unchanged.

## 5. Remaining steps

### Step 3. New tick (the big one)

**Goal.** The new system moves the liquid. The old `LiquidNetwork.Tick()` stops being called.

**Design.**
- New `FlowNetwork.Tick()` (or a small `FlowNetworkTicker` in the same folder). Per network per tick:
  1. Detect motor changes cheaply (compare each motor's `isOn`/strength with its node; call `RefreshRoutes()` if different). This also catches `WireToggle`, which masks `isOn`.
  2. Build `sourceOffer[]` / `sourceType[]` per source port:
     - machine tank: `tank.IsEmpty ? 0 : tank.Amount` and `tank.LiquidType` (-1 if empty);
     - world cell: type from `LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType)`, offer from the cell (see the world helper).
  3. `sinkSpace(k, type)`:
     - machine: `slot = FindSinkSlot(machine.InputLiquids, machine.ResolveIncomingLiquid(type))` (move this helper out of `LiquidNetwork` into a shared static), space = `Capacity - Amount`, 0 if no slot;
     - world cell: 0 if solid; 0 if the cell holds a different liquid; else `255 - LiquidAmount` minus what is already banked.
  4. `pumpBudget[node] = min(Strength, Cap) / 3600` for each ON motor node (the same rule the sandbox tests use), 0 otherwise.
  5. `Solver.Solve(...)`, then apply every `FlowTransfer`.
- Apply rules:
  - Withdraw from a machine tank: `Amount -= x`; if the tank drops to about 0, set `Amount = 0` and `LiquidType = -1` (**forget type**). Remove `MinTankKeep`.
  - Deposit into a machine: `slot.LiquidType = resolved type; slot.Amount += x`, clamped to `Capacity`.
  - Never let a network pump a machine into itself (already guaranteed by `Owner` in the routes).
- Call site: in `LiquidNetworkSystem.PostUpdateWorld`, loop `FlowNetworks[i].Tick()` instead of `ActiveNetworks[i].Tick()`. Keep the old flow field and the old networks alive and unticked for now (removed in step 6).
- **Safety net:** declined by Ismail (no switch). A tick that throws is caught per network (`TickFailed`) and logged once.

**World liquid helper (the one tricky part, decide at the start of the chat).** Terraria liquid is an integer 0-255 per tile and moving it in tiny fractions flickers, so the old code banked the per-tick rate and moved whole 255 chunks. The new solver produces fractional transfers, so each world port needs a small bank:
- Recommended: per world cell keep `withdrawBank` and `depositBank` floats. Transfers accumulate into the bank; when the bank reaches a whole chunk (255, or the rest of the tile) apply it to the tile (`LiquidAmount`, `ClearTile()` at 0, `WorldGen.SquareTileFrame`, `LiquidBroadcastSystem.Mark(x, y)`), keep the remainder. The offer a world source gives the solver is `tile amount - withdrawBank`; the space a world sink offers is `255 - tile amount - depositBank`, so nothing is promised twice. Drop banks (flush whole units) when the network is rebuilt; losing under one chunk on a rebuild is acceptable.
- Infinite sources: keep `IsInfiniteSource(cell)` (flood fill through `WorldLiquidSources.ComputeIsInfinite`, per-network cache, re-check every 60 ticks) and keep the "sink machine treats world liquid as bottomless" flag (`BaseMachine.TreatsWorldLiquidAsInfinite(type)`, Magic Materializer + shimmer): when any sink with a route accepts the active type that way, world sources of that type offer a bottomless amount. Same over-approximation the old code made.
- Mismatch rules from the old code stay: a world sink holding a different liquid refuses; a solid tile refuses.

**Audit before removing `MinTankKeep`.** Places that read tank type or emptiness: `LiquidStack.IsEmpty` (type -1 or amount <= 0) is what most code uses, which is safe. Still check: `BaseMachine.cs` around lines 563, 621 and 671 (recipe input/output tanks), `CustomRecipe.cs` around 182 and 345-358 (the fuel tank already resets to -1 when drained), `LiquidTankUIElement.cs` lines 53 and 126 (what an empty tank draws), `MachineLightSync.cs` around 322-354 (already sends -1 for empty tanks), `BucketPour.cs` 150-157, the Hellforge refill in `HellforgeTileEntity.Update()` (refills its lava tank when empty, make sure it also restores the type), and the carts (`CartCargo`, `CartSystem`; they use their own tank, not pipes).

**Files.** `FlowNetwork.cs` (Tick + apply), a new small `LiquidWorldBanks` helper class, `LiquidNetworkSystem.cs` (call site, switch), move `FindSinkSlot` to a shared static. Do not touch machines, UI, packets or the broadcast code beyond calling `LiquidBroadcastSystem.Mark`.

**Sandbox tests to add.** Extend `TopologyTests.cs.txt` with a fake-tank tick harness: source tank drains and forgets its type at 0; two sources into one sink split fairly and never overfill; sink full means the type stops being active and another type flows; a pump off means no flow; series pumps; machine between two pumps passes liquid through; world source/sink banking over many ticks conserves units.

**Ismail's test checklist for step 3.**
- Water: pool or tank machine -> pipe -> motor -> machine, fills at the pump rate.
- Lava into a Hellforge, oil into whichever machines take it, fuel tanks.
- Two liquids in one network: it drains one type, then the other, with no error.
- Tank runs dry: its type clears, the UI shows it empty, the machine can take another liquid.
- Branch (T-junction): total delivered equals the pump rate, not doubled.
- Climb: lift 3 tiles with a MK1 pump (rate drops by 0.5/tick per tile), source above the pump pays nothing.
- Pump into open air/pool and drain a pool through a pipe; the infinite lake rule still works (a big lake is not drained).
- Magic Materializer + shimmer still treats world shimmer as bottomless.
- Multiplayer: dedicated server + client, pool pumping shows on the client (8.1 broadcast), machine tanks update on the client (5.2 light sync). Re-test R9: client builds pool + pipes + motor, liquid flows without a reload.
- Performance: `/fflow` header build/refresh ms on the biggest setup; no frame hitch when a motor flips.

### Step 4. Motors

**Goal.** Power flips and motor changes need no rebuild and no old-system reset.
- Delete the `flowNeedsRecalculating` flag, the `wasOn` tracking in `MotorTileEntityBase.Update()` and the `network.LiquidType = -1` reset (if step 3 already detects motor changes inside `Tick()`, this step is mostly deletion). `UpdateMotor()` stays as the extension point.
- Keep `PipeEquivalentMaxFlowRate` and `PumpStrength` (the cap rule needs them).
- Keep the rebuild triggers: `Facing` change (`OnRightClick`, `MotorRotate`), placing or breaking a motor (`MotorTileBase.KillTile`, `MotorPlacedConsumer`).
- Verify what a brownout does: the whole grid goes `isOn = false`, so pump budgets are 0 and progress freezes; liquid already in tanks stays.
- Tests: toggle power with a wire/lever (`WireToggle` masks `isOn`), brownout, series pumps with the second one off, MK1 off acting as a pipe (cap lowered, flow only if another pump is ON on the route).

### Step 5. Visual debug and diagnostics

Rewrite `LiquidNetworkDebugSystem.PostDrawTiles` (same `EnableDebugs` toggle, still single-player only on clients, roadmap 8.4 / P6) to draw `FlowNetworks`:
- pipe and motor tiles tinted per network, motor arrow in its facing direction plus ON/OFF;
- every source port and sink port marked at its attach point (different colours, world cells distinct), with the liquid / amount;
- per network label: active type (`Solver.ActiveType`), cap, route count, source/sink count;
- per route endpoint: climbs and the rate lost to climbing; optionally highlight the route under the mouse;
- use `Utils.DrawBorderString`, `TextureAssets.MagicPixel` with source `new Rectangle(0, 0, 1, 1)`, and the existing helpers (`DrawAttachmentOverlay`, `DrawWorldText`). Do not read `ResolvedFlow` any more.
Update `LiquidPipeDiagnostics` (`/fpipes`, `Summary()` used by the `[Pipes] networks rebuilt` log line and by `MultiplayerBootProbeSystem`) to read `FlowNetworks`; `/fflow` becomes the detailed view. `/fliquid` (broadcast counters) is unaffected.
Tests: overlay under `EnableDebugs`; `/fpipes` and `/fflow` agree; server log line still prints.

### Step 6. Cleanup

Only after steps 3-5 are `[x]`:
- Delete the old network: `LiquidNetwork.cs` (keep `LiquidEndpoint` only if still used), `ResolveFlow`, `PriorityQueueLite`, `BuildNetworkFromPipe`, `RecordAttachmentIfAny`, `NarrowCapacity`, `RestoreSavedNetworkTypes`, `ActiveNetworks` (or rename `FlowNetworks` to `ActiveNetworks`), `PipeAttachment`/`MotorAttachment` structs if unused.
- Saves: stop writing `NetworkTypePipes` / `NetworkTypeNames`; keep reading and ignoring them so old worlds load. `AllPipeTiles` is still saved.
- Fix stale comments (`PipeConnectionHelper`, `WorldLiquidSources` mention the old functions).
- Update `PROJECT_CONTEXT.md` (Liquids paragraph: networks, ports, routes, solver, one liquid at a time, cap rule) and regenerate `CODEBASE_INDEX.md` (the patch engine does this on apply).
- Leave the `TEMPORARY (8.1b)` diagnostics for roadmap Phase 10 as planned.

## 6. Risks and open items

- **Performance.** `FlowRouteFinder` runs one Dijkstra per source attach point (about 5 ms warm for 600 pipes / 60 machines in the sandbox). A world with thousands of pipes and hundreds of attachments may hitch on a rebuild or power flip. If `/fflow` shows slow builds, optimise in step 3 or 4: skip attach points that cannot reach any ON pump, or cache per-zone distances.
- **World banking** (step 3) is the most delicate piece; conserve units and keep the 8.1 broadcast calls.
- **Behaviour changes for old worlds:** branched networks stop over-delivering; the intake-side climb penalty now applies where liquid rises; a machine at the side of a motor no longer connects; a type nobody can take no longer stalls a network. Tell Ismail in every step's reply.
- **R9** (client-built pool + pipes + motor not delivering until reload) was never reproduced; the new design removes the hidden type lock that could cause it, but verify in step 3.
- **Pre-existing, not part of this work:** `PipeConnectionHelper.GetOpenSides` reads neighbours without a bounds check (pipes cannot be placed on the world edge in practice); motors with machines on both sides but no pipe form no network.
- Possible later features: a motor seeding networks by itself, full-footprint boxes in the overlay, ports per input tank if a machine ever needs two tanks of the same liquid filled separately.

## 7. Sandbox toolbox (for the assistant)

- Clone: `git clone --depth 1 https://github.com/IsmailAlsaad/Factorraria.git`.
- C# compiler: `apt-get update && apt-get install -y --no-install-recommends dotnet-sdk-8.0`. NuGet is blocked by the proxy, so do not use `dotnet build`/`run`; call Roslyn directly:
  ```
  REF=$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0)
  RFLAGS=""; for f in $REF/*.dll; do RFLAGS="$RFLAGS -r:$f"; done
  dotnet /usr/lib/dotnet/sdk/8.0.*/Roslyn/bincore/csc.dll -nologo -nullable- -out:test.dll -target:exe -optimize+ $RFLAGS <files.cs>
  echo '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' > test.runtimeconfig.json
  dotnet test.dll
  ```
- Core tests: copy `Program.cs.txt` to `.cs` and compile with `Common/Networks/Flow/*.cs`. Topology tests: `Stubs.cs.txt` + `TopologyTests.cs.txt` + `Common/Liquids/LiquidStack.cs` + `Common/Liquids/PipeConnectionHelper.cs` + `Common/Networks/Flow/*.cs` + `Common/Networks/FlowNetwork.cs` + `Common/Networks/FlowNetworkDiagnostics.cs` (separate program from the core tests; both have a `Program` class).
- PowerShell to test patch scripts: download `powershell-7.4.6-linux-x64.tar.gz` from the PowerShell GitHub releases, extract, run `pwsh -File patch_x.ps1 -Root <clone> -DryRun`, then without `-DryRun` on the clone.
- The game-layer files only compile for real inside tModLoader; the stubs catch type and logic errors but not API mismatches. Never claim the mod compiles.
- Patch delivery follows `PROJECT_CONTEXT.md`: copy `patch_template.ps1`, `Edit` / `NewFile` only, UTF-8 with BOM, reply with changed list, "things to remember", "things to test", dry-run and apply commands, and a reminder to commit and push.

## 8. Next chat (written by the step 5 patch)

Paste Ismail's test results for steps 3, 4 and 5 first (he tests them together). If something failed, fix it in that chat with a small patch before step 6. Then:

> Continue the liquid overhaul in Factorraria. Read `docs/liquid_overhaul/ROADMAP.md` from a fresh clone (sections 0, 1, 3 and Step 6). Results of steps 3-5: <paste>. Implement step 6 (cleanup: delete the old LiquidNetwork system, stop writing the saved network types, update PROJECT_CONTEXT.md's Liquids paragraph) as a patch script, following PROJECT_CONTEXT.md. Mark steps 3-5 `[x]` in Progress first if I reported them passing.

Step 5 changed: `LiquidNetworkDebugSystem` (draws `FlowNetworks`: tinted pipes and motors with facing arrow and ON/OFF, every source/sink port at its attach tile with its tank or world-cell liquid, one readout per network with active liquid, cap, moved/tick and port counts, and per route the rate, climbs and rate lost to climbing); `LiquidPipeDiagnostics.Summary()` / `Report()` read `FlowNetworks` (the `[Pipes] networks rebuilt` log line and `MultiplayerBootProbeSystem` still work). Nothing reads the old `ActiveNetworks` / `ResolvedFlow` / `LiquidType` any more except `LiquidNetworkSystem` itself (`RebuildNetworks`, `ResolveFlow`, `RestoreSavedNetworkTypes`, `SaveWorldData`), which step 6 removes. Not done (optional): highlighting the route under the mouse.