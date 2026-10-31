# Weapon Overhauls: Roadmap

Factorraria weapon overhauls. Each weapon gets its own section; one chat = one patch (or a few small patches of the same weapon). Vanilla items are overhauled through a `GlobalItem` (recipe, drops and base stats stay vanilla). Vanilla dust and sounds only; Ismail provides all art (every sprite has a vanilla fallback until his file exists). Multiplayer first (project rule 9): the owner's client decides, everyone else mirrors.

## 1. Ice Boomerang (vanilla `ItemID.IceBoomerang`)

### 1.1 Design (decisions by Ismail, 2026-10-31)
- Left click throws it at the cursor. It does NOT return by itself.
- It stops at the cursor, on a tile, or on the first enemy it touches (crystal forms at the contact point on the enemy, no surface snapping). It spins down, then becomes an ice crystal there.
- Reaching the end of the throw is a gradual stop (it brakes over the last `BrakeDistance` px down to a slow crawl, then settles) instead of an instant halt. Hitting a tile or an enemy turns it into a crystal at once (no spin-down; Ismail's change after W1.1, kept).
- Ismail's local changes after W1.1, kept by W1.3: the throw range is fixed (`MaxThrowRange` = `MinThrowRange` = 20 tiles, not the cursor distance), `autoReuse` is on (the one-in-flight rule still blocks the next throw), the item tooltips are commented out, the crystal uses the vanilla North Pole snowflake texture and the boomerang/crystal custom-art requests are disabled.
- Crystals: 30 s life, hard cap 5 per player (6th evicts the oldest). Crystals float where they formed.
- Crystal look (decided 2026-10-31): each crystal shows one random frame of the 3-frame snowflake sheet; behind it, see-through blue-white copies of the same frame expand and fade out, a new one every 36 ticks. Bouncing the boomerang off a crystal costs it 15 s of life (a crystal with less than 15 s left shatters on the bounce).
- A throw that hits a crystal directly (no enemy first; enemy wins a same-frame tie) starts a ricochet chain through every alive crystal. No max distance between crystals. Chain ignores tiles. Order: nearest unvisited crystal first, each visited once; the target list is snapshotted at the start, dead crystals are skipped.
- Each crystal it bounces off explodes (area damage). Crystals SURVIVE their own explosion, but lose 15 s of life per bounce.
- Damage and speed accumulate only from crystal-to-crystal bounces. Infinite pierce along the chain (each enemy hit once per segment, immunity reset per bounce). After the chain it returns to the player at the accumulated speed and also hits enemies on the way back (final damage, no more accumulation). Catching it resets the accumulation.
- Only one boomerang in flight; it counts as in flight until it has become a crystal.
- Enemies get Chilled (plus a slight slow) from a crystal explosion or by touching a crystal.
- No player recoil, no HUD for the crystal count.
- Juice (all vanilla dust and sounds): trails, bursts, rings, light, pop-in, bob, flicker before expiry, shatter on eviction/expiry, chain lines, hit-stop, pitch rising per bounce, small screen shake.

### 1.2 Multiplayer design
- AUTHORITY: the throwing player's client. `Shoot` runs only there and puts the throw range in `ai[1]`. The boomerang's stop point, crystal spawn, eviction and chill application all run only when `Projectile.owner == Main.myPlayer`. Single-player is the same code.
- SYNC: no new packet so far. Boomerang state is `ai[0]` (netUpdate on the flip to settling); the crystal is a normal synced projectile; an eviction sets the crystal's `ai[1] = 1` + netUpdate and every machine removes its own copy when it sees the flag. Chill uses vanilla `NPC.AddBuff` (a client sends the add-buff message itself). Every machine ends its own settling boomerang on the same timeline.
- Known limits: a player who joins while crystals exist does not see them (vanilla does not resend projectiles); with 30 s lifetime this is accepted.
- Crystal frame and bounce life (W1.4): the owner picks the frame and passes it as `ai0` of the crystal's spawn packet. A bounce adds 1 to the crystal's `ai[2]` + netUpdate; every machine subtracts 15 s per new bounce from its own `timeLeft` (not synced by vanilla), so all copies expire together. The halo is a pure function of the crystal's age: nothing stored or sent.
- Chain (W1.3, built): boomerang `ai[0]` = phase (0 outbound, 1 settling, 2 chain, 3 returning), `ai[1]` = target crystal projectile identity (chain), `ai[2]` = bounce level (chain/returning). The owner's client detects the crystal touch, picks every next target (nearest crystal not yet bounced off, own crystals only), scales `Projectile.damage` from `originalDamage`, clears the local NPC immunity per segment and sets netUpdate. Every machine steers its own copy toward the crystal named in `ai[1]` (and toward the player when returning), so remote copies move smoothly and only wait a moment for the next target. `tileCollide` is set from the phase on every machine. A swept `Colliding` test stops fast boomerangs tunnelling through enemies. The blast (`IceCrystalBlastProjectile`) is a separate 6-tick projectile spawned by the owner at the crystal; it hits each enemy once in a circle, applies Chilled in `OnHitNPC`, and makes the crystal under it flash on every machine.

### 1.3 Files
- `Content/Items/Weapons/IceBoomerang/IceBoomerangGlobalItem.cs` (override, one-in-flight rule, throw, tooltips)
- `Content/Projectiles/Weapons/IceBoomerang/IceBoomerangProjectile.cs`, `IceCrystalProjectile.cs`, `IceBoomerangTuning.cs` (all numbers + chill helper), `IceBoomerangFx.cs` (all dust and sound)

- W1.3 adds `IceCrystalBlastProjectile.cs` (the area explosion at each bounced crystal)

### 1.4 Art (optional, each has a vanilla fallback)
- `Content/Projectiles/Weapons/IceBoomerang/IceBoomerangProjectile.png`: single frame, drawn centered, about 28x28 (hitbox is 28x28).
- `Content/Projectiles/Weapons/IceBoomerang/IceCrystalProjectile.png`: single frame, drawn centered, about 28x44 (hitbox is 22x38).

### 1.5 Progress
- [x] W1.1 + W1.2: item override, throw at cursor, outbound flight, settle into a crystal, crystal life/cap/eviction, chill on touch, basic dust and sounds (patch_ice_boomerang_1_1.ps1, tested by Ismail 2026-10-31: works; he asked for the gradual stop, done in patch 1_3)
- [x] W1.3: chain (crystal hit detection, nearest-first order, accumulation, blast projectile with chill, return and catch) + gradual stop at the cursor (patch_ice_boomerang_1_3.ps1, tested by Ismail 2026-10-31: everything works)
- [-] W1.4 (original juice pass: chain lines, hit-stop, screen shake, stronger trail): skipped, Ismail did not like it and reverted it (2026-10-31)
- [~] W1.4b: random crystal frame, expanding halo behind each crystal, a bounce costs a crystal 15 s of life (patch_ice_crystal_halo.ps1, applied, untested)
- [ ] W1.5: multiplayer verification (server + 2 clients) and tuning

### 1.6 Tests for W1.1 + W1.2
- Equip the Ice Boomerang (`/give` or cheat): left click throws at the cursor; the tooltip shows the new lines.
- Throw into open air: it stops exactly at the cursor, spins down, becomes a crystal. A second click while it is still flying or settling does nothing.
- Throw at a wall: crystal forms against the wall. Throw at an enemy: it hurts it and the crystal forms on the enemy's body.
- Place 6 crystals: the oldest shatters when the 6th forms. Wait 30 s: a crystal shatters by itself and flickers in its last 3 s.
- Enemy touching a crystal gets Chilled (and keeps it while it stays inside).
- Multiplayer (server + 2 clients): the other client sees the throw, the settle, the crystal, the eviction and the expiry; the chill shows on the enemy for both; a player leaving makes their crystals vanish.

### 1.6b Tests for W1.3 (+ gradual stop)
- Throw into open air: it slows down smoothly over the last few tiles and settles at the cursor (no sudden stop). Hitting a wall or an enemy still stops it at once.
- With 1 crystal placed: throw so the boomerang touches the crystal. It flies to its center, a blast goes off (burst, rings, sound, the crystal flashes), it returns to you and is caught. The crystal is still there afterwards.
- With 3-5 crystals: hit one directly. It visits the others nearest-first, each exactly once, getting faster and stronger (check damage numbers rise per bounce), then returns. Enemies standing between crystals are hit once per segment; on the way back they are hit again.
- Enemies inside a blast get damage and Chilled. A throw that touches an enemy before a crystal does NOT chain.
- Cannot throw again until it has been caught (or has become a crystal).
- Let a crystal expire mid-chain (or have only a few seconds left): the chain skips it and carries on.
- Multiplayer (server + 2 clients): the other client sees the boomerang fly along the same path between crystals, the blasts and flashes, and the return; damage numbers and Chilled show for both; leaving mid-chain removes the boomerang.

### 1.6c Tests for W1.4b
- Place 5 crystals: they show a mix of the 3 snowflake frames (not all the same). In multiplayer the other client sees the same frame on each crystal. If a frame looks clipped or shows a sliver of its neighbour, the 2 px padding assumption in `IceCrystalProjectile.PreDraw` needs adjusting.
- Each crystal has see-through blue-white copies of itself growing and fading behind it, a new one every ~0.6 s, several overlapping. They look right on all 3 frames and flicker with the crystal in its last 3 s.
- Chain through 3 crystals: each crystal that is bounced off loses 15 s (check by timing it: a fresh crystal bounced once lasts about 15 s more, bounced twice shatters at once). A crystal with under 15 s left shatters on the bounce (shatter sound and dust) but the chain still goes on to the other crystals.
- The crystal count cap and eviction still work (6th evicts the oldest, using the shortened life as 'oldest').
- Multiplayer (server + 2 clients): the other client sees the same frames, halos, and the crystals expiring at the same time as for the owner after a bounce.

### 1.7 Next chat prompt
"Implement patch W1.5 of docs/weapon_overhaul/ROADMAP.md (Ice Boomerang multiplayer verification and tuning). Read section 1 of the roadmap, clone, and check the code of W1.1-W1.3 and W1.4b first. W1.4b test results: <paste them here>. Tuning numbers to change (all in IceBoomerangTuning.cs): <list them here>."