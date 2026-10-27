# Cosmic Radiation: Development Roadmap

Factorraria cosmic radiation power source: energetic particles fall from space, gemspark blocks split them into lower-frequency particles, cloud blocks ionize and thunder, and the lightning strikes machines with a power spike. Written against repo commit `6372941` (2026-10-27). Where this document and the code disagree, the code wins.

## 1. Decisions

### Fixed by Ismail
- **Source.** Cosmic particles come from space. The higher you go, the more frequent they are. They are custom simulated, drawn only on screen, and must be very cheap because they exist all over the world's space layer.
- **Seven colors**, one per vanilla gemspark block: amethyst = violet, topaz = yellow, sapphire = blue, emerald = green, ruby = red, amber = orange, diamond = white. White stands for UV-like frequencies we cannot see.
- **Cascade.** A gemspark block absorbs particles of its own color and re-emits 2 particles of the next lower frequency: white > violet > blue > green > yellow > orange > red. A full cascade turns 1 white into 64 red.
- **Re-emission angle.** A particle arriving along a path line is absorbed and 2 are released, one to each side of that line, each 5 to 10 degrees off it (10 to 20 degrees between the two).
- **Base energy halves with each step down in frequency**, so the cascade conserves base energy: white 64, violet 32, blue 16, green 8, yellow 4, orange 2, red 1. This resolves the "cascade is a huge amplifier" problem (decided 2026-10-28).
- **Ionization.** Every color has the same chance of being absorbed by a cloud block. Higher frequency ionizes fewer blocks with a higher multiplier: white ionizes 1 block (multiplier 3), red ionizes a cluster of about 10 to 12 adjacent cloud blocks (multiplier 1.1 each). The cluster is a flood fill from the hit tile until N tiles are included (N random per color).
- **Thunder.** An ionized block lives about 1 second, then the whole connected ionized cluster discharges at once, exactly once (no duplicate energy). Energy multiplies the ionization multipliers of every block in the cluster, so touching clusters merge and multiply. The explosive growth is intentional: no cap, no diminishing curve.
- **Strike.** If an electric consumer or producer machine is within 30 tiles, the lightning strikes it and the power grid gets a big supply spike scaled by the cluster energy. Otherwise the thunder goes off in a random direction. A battery that stores the spike comes later (not in this roadmap's phases).
- **Heatmap.** Baked per world. The world is split into 7 equal sections (large world 8400 / 7 = 1200 tiles each, small 600), each a hot spot for one randomly chosen color. Each color's chance is a Gaussian-like bump over its section that bleeds into the neighbors, with a constant low floor so every color appears everywhere. Section order is a random shuffle of the 7 colors.
- **Spawn is purely random** (no extra weight toward red or against white beyond the heatmap).
- **Off-screen logic is statistical** and gives the same result as simulating the particles while the player watches. Visuals are cosmetic.
- **Particle visual for now:** a small colored circle with a straight trail that fades out gradually. Custom absorb and re-emit audio comes later (Phase C7 only leaves hooks).
- **Multiplayer:** server authoritative, clients only draw (agreed).

### Assumptions (change any of these before the patch that uses them)
- **A1. Thunder energy formula.** `Energy = (sum of BaseEnergy of every particle absorbed into the cluster) x (product of the multipliers of every ionized block in the cluster)`. Checks: one white hit = 64 x 3 = 192; one red hit on 11 blocks = 1 x 1.1^11 = 2.85; a white cluster touching that red cluster = (64 + 1) x 3 x 1.1^11 = 556. Base energy adds, multipliers multiply.
- **A2. Per-color tables** (geometric steps between your two endpoints, all tunable in one table): multiplier white 3.0, violet 2.54, blue 2.15, green 1.82, yellow 1.54, orange 1.30, red 1.10. Cluster size N: white 1, violet 1 to 2, blue 2 to 3, green 3 to 4, yellow 5, orange 7 to 8, red 10 to 12.
- **A3. Cloud tiles that count:** `Cloud`, `RainCloud` and `SnowCloud` (all three). Placed or natural, no difference.
- **A4. Cloud absorption.** A particle passing through a cloud tile is absorbed with probability `CloudAbsorbChance` (same for every color, default 0.5), otherwise it keeps flying through. A particle that hits an already ionized block is absorbed and adds its base energy to that cluster (no new blocks).
- **A5. Other tiles.** A gemspark of a different color is transparent to the particle (it passes through). Any other solid tile stops the particle (lost). Non-solid tiles do not stop it.
- **A6. Adjacency** is the 4 neighbors (up, down, left, right), for both the flood fill and for merging ionized clusters.
- **A7. Strike target.** The nearest machine that is an `IElectricConsumer` or `IElectricProducer`, measured from the cluster's center, within `StrikeRadiusTiles` (30). Power poles count (they are consumers), so a pole is a natural lightning rod. Players and NPCs are never hurt in v1.
- **A8. Grid delivery.** A strike puts a surge on the struck machine: `SurgePower = Energy x SurgePowerPerEnergy` added to its grid's supply for `SurgeDurationTicks` ticks (default 30). The grid stays binary, so machines run only while supply covers demand. This is the stand-in until batteries exist.
- **A9. Cloud blocks survive a discharge** (they de-ionize and can be ionized again). Optional `RefractoryTicks` after a discharge, default 0 (off).
- **A10. Spread.** Particles fall mostly straight down: direction is vertical plus a random tilt up to +-5 degrees (`SpawnSpreadDeg`).
- **A11. Flight.** Speed `ParticleSpeed` tiles per tick, range `ParticleRange` tiles, then the particle dies.

## 2. The model (exact rules the code must follow)

**Colors** (`CosmicColor : byte`, ordered by frequency): White 0, Violet 1, Blue 2, Green 3, Yellow 4, Orange 5, Red 6. `Next(color)` is color + 1 (Red has none). One table holds, per color: base energy (64 down to 1), ionization multiplier, cluster size range, gemspark `TileID`, display color.

**Space and spawning.**
- Particles exist in a spawn band from the top of the world down to `SpaceBottomY` (the vanilla space/sky boundary, see V2). Spawn density per tile^2 is `RateDensity x heat_color(x) x altitude(y)`, where `altitude` is 1 at the top of the world and 0 at `SpaceBottomY` (linear).
- Direction: downward, vertical plus a random tilt (A10). A particle flies up to `ParticleRange` tiles at `ParticleSpeed`.

**Heatmap.** Stored: the shuffled color order (7 entries) and the format version. `sectionWidth = maxTilesX / 7`, `center_k = (k + 0.5) x sectionWidth`, `sigma = HeatSigmaFraction x sectionWidth` (0.35), `weight_c(x) = HeatFloor + (1 - HeatFloor) x exp(-(x - center_c)^2 / (2 sigma^2))` (floor 0.08). The color of a spawned particle is picked by the normalized weights at its x. A per-16-tile lookup table is built on load.

**Ray resolution.** A particle is a ray: start, direction, color, start tick. The server resolves it in one pass with a tile walk (Amanatides-Woo DDA), then schedules the outcome at `arrivalTick = startTick + distance / ParticleSpeed`. Per tile on the way:
- empty or non-solid: continue.
- gemspark of the same color: absorbed, outcome `Absorb(tile)`: at arrival two child rays of `Next(color)` start from that tile, directions = incoming direction rotated by +a and -b degrees, a and b random in [5, 10]. (Red has no next color: absorbed and lost.)
- gemspark of another color: continue (A5).
- cloud tile: roll `CloudAbsorbChance`; on success outcome `Ionize(tile)`, else continue (A4).
- any other solid tile, or range exhausted: the ray dies. No outcome.

**Statistical equivalence (the key performance rule).** Nothing is simulated per tick. The server keeps an event heap ordered by arrival tick and pops what is due each tick. Cost is proportional to the number of rays that can matter, not to the size of the sky:
1. A `ReceiverIndex` tracks every gemspark and cloud tile in the space/sky rows (built once on world load by scanning those rows, then kept up to date by a `GlobalTile` place/kill hook).
2. Receivers are grouped into rigs (spatial hash). A ray can only matter if it reaches a receiver, and every such ray starts inside that rig's sampling area: the part of the spawn band within `ParticleRange` of the rig and within `ParticleRange x tan(SpawnSpreadDeg)` sideways of its bounding box. Rigs whose sampling areas overlap are merged and sampled once, so no ray is counted twice.
3. Every `SampleStepTicks` (10), for each rig group the server draws `Poisson(sum of density x area x step)` rays with random start points inside the sampling area (color and altitude weights from the heatmap and `altitude`), resolves each as above and queues the outcomes. Rays that hit nothing are dropped.
This has the same expected arrivals at every receiver as simulating all the world's particles, and costs nothing where the player built nothing.

**Ionization and discharge.**
- `Ionize(tile)` at arrival: if the tile is already ionized, add the particle's base energy to that block (A4). Otherwise flood fill (4-neighbors, A6) over non-ionized cloud tiles starting at the hit tile until N tiles (random in the color's range) are ionized or no more exist. Each new block stores: color, multiplier, `BaseShare = baseEnergy / blocksIonizedByThisHit`, `ExpireTick = now + IonizedLifetimeTicks` (60).
- Every tick the server pops expired blocks from an expiry heap. When one expires, the whole connected ionized component (A6) discharges once: `Energy` by A1, center = mean tile, all its blocks de-ionize (A9), then the strike logic runs.

**Strike.** Find the target (A7). Found: add a surge (A8) and emit a `Strike(from, to, energy)` event. Not found: emit a `Spit(from, randomDirection, energy)` event, no power.

**Surge in the grid.** `BaseMachine` gets `SurgePower` and `SurgeTicksLeft` (server only, never saved). `PowerNetwork.Tick` adds the surge of every member to `totalSupply` and counts the ticks down. Existing binary rule is unchanged.

## 3. Codebase facts this plan relies on

| Area | What exists | Consequence |
|---|---|---|
| Power tick | `PowerNetwork.Tick()` (`Common/Networks/PowerNetwork.cs`) sums `PowerDemand` of working consumers and `PowerSupply` of working producers, binary stable check. `PowerGridSystem.PostUpdateWorld` runs it and returns on `MultiplayerClient` | Surge is one extra term in `totalSupply`; it runs server and single-player only already |
| Machine list | `PowerGridSystem.AllMachines` (`HashSet<TileEntity>`) | Strike target search walks it, filtered to `IElectricConsumer` / `IElectricProducer` |
| Producers | `ElectricProducerMachine` with abstract `PowerSupply` (`ElectricMachine.cs`) | The surge does not need a new machine: it rides on any machine |
| Net | `FactorrariaNet.Register(type, direction, handler, cost)`, `SendToClient`, `Broadcast`, `JoinSync.Register(key, write, read)`. Last used packet id is 54 (`PoleLinkChanged`) | New ids 55 and 56. Copy the pole link net pattern |
| Join | `JoinSync` providers are keyed by string and sent in chunks | Provider `"cosmic-heatmap"` |
| Tile hooks | `LiquidTopologyGlobalTile.KillTile` marks dirty on kill; placement goes through `TilePlaced` consumers (`Common/Net/TilePlacedConsumers.cs`) on the server | Receiver index updates on kill via a GlobalTile and on place via a `ITilePlacedConsumer` |
| World data | Systems use `SaveWorldData` / `LoadWorldData` / `OnWorldLoad` / `OnWorldUnload` | Heatmap saved the same way |
| Configs | `NetworkConfig`, `CartMapConfig` (`Content/Configs`) | Tunables live as public fields on `CosmicRadiationSystem` (hot reload friendly); only net limits go in a config |
| Helpers | `IsOnScreen(center, range)` exists on machines; `TextureAssets.MagicPixel` must be drawn with source `new Rectangle(0, 0, 1, 1)`; `ModContent.RequestIfExists` for art that may be missing | Reuse them in the client draw |
| Tests | `tools/flow_tests` tests pure code with stubs | Ray walk, flood fill and heatmap are written as pure code (no Terraria types, a small `ITileProbe` interface) and tested in `tools/cosmic_tests` |
| Art | Ismail makes all art | Phase C6 draws without art (pixel quads); optional circle texture path given below |

## 4. Multiplayer design (rule 9)

**Authority.** The server (and single-player) owns everything that changes the game: heatmap, receiver index, ray sampling, event heap, ionized blocks, discharges, surges. Clients own only drawing.

| Thing | Who | How |
|---|---|---|
| Heatmap (color order) | Server, saved in world data | JoinSync provider `"cosmic-heatmap"`; clients need it only for decor |
| Receiver index, rays, event heap, ionized blocks, surges | Server and single-player | Never saved, rebuilt on world load. Ionized blocks and surges are short-lived and dropped on unload |
| What clients see | Server pushes small events | `CosmicRays` (id 55): cosmetic path of each resolved ray (color, start, direction, length, start delay). `CosmicEvent` (id 56): `Ionize` (color, tiles), `Discharge` (tiles, energy tier), `Strike` (from, to), `Spit` (from, direction), `Absorb` (tile, color) |
| Who gets them | Server filters per player | Only players within `NetInterestRadiusTiles` of the event, capped per tick, rate-limit cost set per packet |
| Power spike | Server | `PowerNetwork` tick as today; clients get `isOn` through the machine snapshot as today |
| Client particles, trails, glow, bolts, sound | Client only | Pooled, never saved, never sent |

**Single-player** runs the same server code and a local event sink that feeds the client draw directly (rule 10). Server logic never calls draw code: it emits events through a `ICosmicEventSink`, so the sink is the only place that differs (local in single-player, network in multiplayer). This is why the packets can be written in Phase C6 without reworking C2 to C5.

**Decor.** Ambient streaks that cannot interact (Phase C7) are client-only and never touch the logic.

## 5. Data model and files

| File | Role |
|---|---|
| `Common/Cosmic/CosmicColor.cs` | enum, per-color table (base energy, multiplier, cluster range, gemspark tile, draw color), `Next()` |
| `Common/Cosmic/CosmicHeatmap.cs` | color order, weights, lookup table, save/load, join provider |
| `Common/Cosmic/CosmicRayTracer.cs` | pure code: DDA walk against `ITileProbe`, returns first outcome and distance |
| `Common/Cosmic/CosmicClusterMath.cs` | pure code: flood fill, A1 energy |
| `Common/Cosmic/CosmicReceiverIndex.cs` + `CosmicReceiverGlobalTile.cs` | gemspark and cloud tiles, rig grouping, sampling areas |
| `Common/Cosmic/CosmicRadiationSystem.cs` | the ModSystem: tunables, sampling, event heap, ionized blocks, discharge, strike, event sink |
| `Common/Cosmic/ICosmicEventSink.cs` | event records and the sink interface |
| `Common/Net/CosmicNet.cs` | packets 55 and 56, join provider, interest filtering |
| `Common/Cosmic/CosmicParticleSystem.cs` | client pool, draw, bolts, ionized glow |
| `Common/Cosmic/CosmicDebug.cs` | `/fcosmic` command |
| `tools/cosmic_tests/` | tests for the pure code |

## 6. Patch plan

Each phase is one chat and one patch script (`patch_cosmic_c<n>_<name>.ps1`). Every patch starts from the pole/multiplayer working rules: clone, verify every Find, state the authority and sync design first, mark Progress "applied, untested", end with the next-chat prompt, and give the changed list plus the test checklist. Phases that add packets also add a line to `docs/multiplayer/ROADMAP.md` Progress (project working rule 10).

### C1. Types, tables, heatmap
Authority: server saves and generates; clients receive on join.
- `CosmicColor` and the table (A2 values). `CosmicRadiationSystem` shell with the public tunables listed in section 7.
- `CosmicHeatmap`: shuffle on first load of a world that has none (and after world generation), `SaveWorldData` / `LoadWorldData`, the lookup table, `JoinSync` provider `"cosmic-heatmap"`.
- `/fcosmic heat` prints the color order, section width and the 7 weights at the player's column.
Tests: the order is stable after save and reload; weights add up and show a peak in each section with a floor of about 0.08 elsewhere; a second client sees the same order.

### C2. Receiver index, ray tracer, event heap (no gameplay)
Authority: server only. Single-player runs the same.
- `ITileProbe`, `CosmicRayTracer` (pure code) and `tools/cosmic_tests` for it: straight and angled rays, same-color gemspark, other-color gemspark passthrough, solid stop, range end.
- `CosmicReceiverIndex` (+ GlobalTile kill hook and place consumer), rig grouping and merging, sampling areas.
- The sampler (Poisson, density, `altitude`, heatmap color) and the event heap. Outcomes are only counted for now.
- `/fcosmic stats` (rays per second, queue size, receivers, rigs), `/fcosmic spawn <color> [tx ty]` (fires one ray at the cursor).
Tests: build a gemspark pad under the sky and watch the hit counters climb; a pad in the middle of nowhere gets rays only above it; breaking and placing receivers updates the counts; an empty world costs about nothing in `/fcosmic stats`.

### C3. Gemspark absorption and cascade
Authority: server only.
- `Absorb` outcome: schedule the two child rays at +a and -b degrees (a and b random in 5 to 10), recursion through the heap, no recursion in the call stack. Safety caps: `MaxRaysPerTick`, `MaxQueuedEvents` (drop and count the overflow).
Tests: a diamond gemspark under a spawned white ray produces counters 2 violet, then 4 blue after violet gemsparks below it, and so on to 64 red; a tower with no next-color layer ends the chain; the child angles in `/fcosmic stats` are within 5 to 10 degrees of the parent.

### C4. Cloud ionization and discharge (energy is only logged)
Authority: server only.
- `CosmicClusterMath` (pure code, tested): flood fill and A1 energy.
- `Ionize` outcome, ionized block table, expiry heap, merged-cluster discharge (once), refractory option.
- The discharge only logs `energy` and block count with `/fcosmic stats` plus a chat line when debug is on.
Tests: a white hit ionizes 1 block and logs 192 after 1 second; a red hit ionizes 10 to 12 and logs about 2.85 (+- the cluster size); two touching clusters discharge together, once; a particle hitting an ionized block adds its base energy; cloud blocks can be hit again afterwards.

### C5. Strike and grid surge
Authority: server only. Clients see only the existing machine `isOn` snapshot.
- Target search (A7) over `PowerGridSystem.AllMachines`, `BaseMachine.SurgePower` / `SurgeTicksLeft`, the `PowerNetwork.Tick` term (A8), the `Strike` and `Spit` events into the sink (still not drawn).
- `/fcosmic strike <energy>` strikes the nearest machine directly, so the grid can be tested without a sky farm.
Tests: a lone machine in range lights a connected grid for about 30 ticks and then goes off again; a surge below the grid's demand does nothing; no machine within 30 tiles produces a `Spit`; a pole as the nearest target powers the grid it is linked to; two strikes in the same window stack; save and reload leaves no leftover surge.

### C6. Client particle system and network events
Authority: server emits, clients draw. Single-player uses the local sink.
- `CosmicNet`: packets 55 and 56 appended to `FactorrariaPacket`, handlers, interest filter, per-tick caps, rate-limit costs, the local sink for single-player.
- `CosmicParticleSystem` (client): a fixed pool (default 1500, no allocation per tick), each particle a small circle plus a straight trail of 8 fading segments drawn with `MagicPixel` source `new Rectangle(0, 0, 1, 1)` in additive blend, culled with the same on-screen test as machines. Absorb and re-emit spawn the child particles at the right moment from the `Absorb` event and the `CosmicRays` paths.
- Optional art, requested from Ismail only if he wants it (`RequestIfExists`, with a plain 3x3 pixel quad as the fallback): `Content/Particles/CosmicParticle.png`, 8x8, a white filled circle that is tinted by color.
- Ionized cloud tint and the discharge and strike bolt are simple lines here; art polish is C7.
Tests (multiplayer needs two instances): both clients see the same particles and strikes near them; a client far away receives nothing; joining mid-farm shows the heatmap and the next events; particle count stays under the pool cap with a big cascade; no per-frame allocation spikes (watch the GC counter).

### C7. Ambient decor, polish, audio hooks
Authority: client only, plus the sink.
- Client-only ambient streaks in the space layer following the heatmap, killed at the first solid tile, low density, no logic.
- Light and a short flash at the strike point, ionized glow pulse before the discharge, absorb and re-emit sparkles at gemsparks.
- Empty sound hooks (`OnAbsorbed`, `OnReemitted`, `OnIonized`, `OnThunder`) so audio can be dropped in later without touching the logic.
- `EnableDebugs` overlay: draw the heatmap curve, rig boxes and the counters.
Tests: space looks alive but a decor particle never changes any counter; sparkles only where an event happened.

### C8. Hardening and balance
- Profile with a big sky farm (budget: average server cost under 0.2 ms per tick for 5 rigs, client under 1500 draws), tune the section 7 numbers with Ismail, check join and leave mid-cascade, world reload, hot reload of the tunables, remove the temporary commands that should not ship (section 10).
- Update this file and `docs/multiplayer/ROADMAP.md`.

### Not scheduled (after the phases above)
Battery (stores a surge), custom audio, lightning damage to players, a lightning rod tile, a surge readout in the machine UI, per-color gemspark visual glow while absorbing.

## 7. Tunables (public instance fields on `CosmicRadiationSystem`, starting values)

| Field | Start | Meaning |
|---|---|---|
| `RateDensity` | 0.0004 | particles per tile^2 per second at the top of the world, before heat and altitude |
| `SpawnSpreadDeg` | 5 | random tilt from vertical |
| `ParticleSpeed` | 0.8 | tiles per tick |
| `ParticleRange` | 160 | tiles before a ray dies |
| `CloudAbsorbChance` | 0.5 | per cloud tile passed |
| `ReemitAngleMinDeg` / `ReemitAngleMaxDeg` | 5 / 10 | child angle to each side |
| `IonizedLifetimeTicks` | 60 | before the cluster discharges |
| `RefractoryTicks` | 0 | cooldown after a discharge (off) |
| `StrikeRadiusTiles` | 30 | |
| `SurgePowerPerEnergy` / `SurgeDurationTicks` | 1 / 30 | A8 |
| `SampleStepTicks` | 10 | sampling period |
| `HeatSigmaFraction` / `HeatFloor` | 0.35 / 0.08 | heatmap shape |
| `MaxRaysPerTick` / `MaxQueuedEvents` | 200 / 20000 | safety caps |
| `ClientParticlePool` / `NetInterestRadiusTiles` | 1500 / 120 | client and net limits |

These are starting guesses, tuned in C8.

## 8. Open items needing a decision (defaults are used if you say nothing)
1. Which tiles are clouds (A3: all three types).
2. Other-color gemsparks transparent or blocking (A5: transparent).
3. Refractory period after a discharge (A9: off).
4. Can lightning hurt players or break tiles (A7: no).
5. Do poles attract strikes (A7: yes).
6. The energy formula (A1).
7. Spread of the incoming particles (A10: +-5 degrees).
8. Surge delivery shape until batteries exist (A8: power for 30 ticks).

## 9. Verification list (checked in code or API before the patch that needs it)
- **V1.** `TileID` names of the 7 gemsparks, `Cloud`, `RainCloud`, `SnowCloud` (tModLoader 2026.08.3.0 sources). Before C1.
- **V2.** Vanilla boundary of the space and sky layers (`Main.worldSurface x 0.35` is used by `ZoneSkyHeight`) and where floating islands sit relative to it, so particles can reach natural cloud islands with `ParticleRange`. Before C1.
- **V3.** Placing a gemspark or cloud on a client reaches the server through `TilePlaced`; confirm the consumer pattern before C2.
- **V4.** `Main.tileSolid` for gemspark blocks and clouds, and platforms, for the solid-stop rule. Before C2.
- **V5.** Additive blend `SpriteBatch.Begin` pattern copied from an existing `PostDrawTiles` system. Before C6.
- **V6.** That `PowerGridSystem.AllMachines` holds poles too. Before C5.

## 10. Risks
- **Merged-cluster explosion.** By design there is no cap. A large placed cloud pad with many red hits can produce astronomically large energy numbers. Check float overflow in C4 and clamp only the number sent to the grid (`float.MaxValue` guard), never the physics.
- **Sampling area size.** Wide sky farms make a wide sampling area; rays per step grows with area. Watch `/fcosmic stats` in C8 and raise `SampleStepTicks` if needed.
- **Visual and logic mismatch.** Decor streaks (C7) are separate from real rays; if they look like they pass through gemsparks without effect it will read as a bug. Tune density low or fade them near receivers.
- **Net volume.** One white cascade is up to 127 rays. Interest filter plus per-tick cap plus batching are required in C6, or the rate limiter will drop packets.
- **Receiver index drift** if a tile changes without a hook (world edits by other mods, explosions). Keep a periodic slow resync in C8.

## 11. Temporary test code to remove in Phase C8
`/fcosmic spawn`, `/fcosmic strike` and the verbose counters (keep `/fcosmic heat` and `/fcosmic stats`).

## 12. Progress

One line per patch: `[ ]` not started, `[~]` applied untested, `[x]` tested done.

- [ ] C1 Types, tables, heatmap
- [ ] C2 Receiver index, ray tracer, event heap
- [ ] C3 Gemspark absorption and cascade
- [ ] C4 Cloud ionization and discharge
- [ ] C5 Strike and grid surge
- [ ] C6 Client particles and network events
- [ ] C7 Ambient decor, polish, audio hooks
- [ ] C8 Hardening and balance

Starting a patch: "Implement cosmic patch C<n>." The patch reads this file from the fresh clone (sections 1, 2, 4, the phase, 7, 9 and Progress) and `docs/multiplayer/ROADMAP.md` section 3, checks that its prerequisites are marked done, states the authority and sync design first, and ends with a next-chat prompt.

Next chat prompt (after the roadmap is committed): "Implement cosmic patch C1 for Factorraria. Clone the repo, read docs/cosmic_radiation/ROADMAP.md (sections 1, 2, 4, 6 C1, 7, 9 and Progress) and docs/multiplayer/ROADMAP.md section 3. Resolve V1 and V2 first, then deliver the patch script. My answers to the open items in section 8: <your answers, or 'use defaults'>."