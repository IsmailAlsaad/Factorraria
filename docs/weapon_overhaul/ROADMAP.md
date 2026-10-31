# Weapon Overhauls: Roadmap

Factorraria weapon overhauls. Each weapon gets its own section; one chat = one patch (or a few small patches of the same weapon). Vanilla items are overhauled through a `GlobalItem` (recipe, drops and base stats stay vanilla). Vanilla dust and sounds only; Ismail provides all art (every sprite has a vanilla fallback until his file exists). Multiplayer first (project rule 9): the owner's client decides, everyone else mirrors.

## 1. Ice Boomerang (vanilla `ItemID.IceBoomerang`)

### 1.1 Design (decisions by Ismail, 2026-10-31)
- Left click throws it at the cursor. It does NOT return by itself.
- It stops at the cursor, on a tile, or on the first enemy it touches (crystal forms at the contact point on the enemy, no surface snapping). It spins down, then becomes an ice crystal there.
- Crystals: 30 s life, hard cap 5 per player (6th evicts the oldest). Crystals float where they formed.
- A throw that hits a crystal directly (no enemy first; enemy wins a same-frame tie) starts a ricochet chain through every alive crystal. No max distance between crystals. Chain ignores tiles. Order: nearest unvisited crystal first, each visited once; the target list is snapshotted at the start, dead crystals are skipped.
- Each crystal it bounces off explodes (area damage). Crystals SURVIVE their own explosion.
- Damage and speed accumulate only from crystal-to-crystal bounces. Infinite pierce along the chain (each enemy hit once per segment, immunity reset per bounce). After the chain it returns to the player at the accumulated speed and also hits enemies on the way back (final damage, no more accumulation). Catching it resets the accumulation.
- Only one boomerang in flight; it counts as in flight until it has become a crystal.
- Enemies get Chilled (plus a slight slow) from a crystal explosion or by touching a crystal.
- No player recoil, no HUD for the crystal count.
- Juice (all vanilla dust and sounds): trails, bursts, rings, light, pop-in, bob, flicker before expiry, shatter on eviction/expiry, chain lines, hit-stop, pitch rising per bounce, small screen shake.

### 1.2 Multiplayer design
- AUTHORITY: the throwing player's client. `Shoot` runs only there and puts the throw range in `ai[1]`. The boomerang's stop point, crystal spawn, eviction and chill application all run only when `Projectile.owner == Main.myPlayer`. Single-player is the same code.
- SYNC: no new packet so far. Boomerang state is `ai[0]` (netUpdate on the flip to settling); the crystal is a normal synced projectile; an eviction sets the crystal's `ai[1] = 1` + netUpdate and every machine removes its own copy when it sees the flag. Chill uses vanilla `NPC.AddBuff` (a client sends the add-buff message itself). Every machine ends its own settling boomerang on the same timeline.
- Known limits: a player who joins while crystals exist does not see them (vanilla does not resend projectiles); with 30 s lifetime this is accepted.
- Chain (W1.3) plan: boomerang `ai[0]` = state, `ai[1]` = current target crystal projectile identity, netUpdate per bounce; the blast is a separate short-lived projectile spawned by the owner so its damage uses the normal hit path.

### 1.3 Files
- `Content/Items/Weapons/IceBoomerang/IceBoomerangGlobalItem.cs` (override, one-in-flight rule, throw, tooltips)
- `Content/Projectiles/Weapons/IceBoomerang/IceBoomerangProjectile.cs`, `IceCrystalProjectile.cs`, `IceBoomerangTuning.cs` (all numbers + chill helper), `IceBoomerangFx.cs` (all dust and sound)

### 1.4 Art (optional, each has a vanilla fallback)
- `Content/Projectiles/Weapons/IceBoomerang/IceBoomerangProjectile.png`: single frame, drawn centered, about 28x28 (hitbox is 28x28).
- `Content/Projectiles/Weapons/IceBoomerang/IceCrystalProjectile.png`: single frame, drawn centered, about 28x44 (hitbox is 22x38).

### 1.5 Progress
- [~] W1.1 + W1.2: item override, throw at cursor, outbound flight, settle into a crystal, crystal life/cap/eviction, chill on touch, basic dust and sounds (patch_ice_boomerang_1_1.ps1, applied, untested)
- [ ] W1.3: chain (crystal hit detection, ordering, accumulation, blast projectile with chill, return and catch)
- [ ] W1.4: full juice pass (chain lines, hit-stop, screen shake, pitch per bounce, stronger trail per level)
- [ ] W1.5: multiplayer verification (server + 2 clients) and tuning

### 1.6 Tests for W1.1 + W1.2
- Equip the Ice Boomerang (`/give` or cheat): left click throws at the cursor; the tooltip shows the new lines.
- Throw into open air: it stops exactly at the cursor, spins down, becomes a crystal. A second click while it is still flying or settling does nothing.
- Throw at a wall: crystal forms against the wall. Throw at an enemy: it hurts it and the crystal forms on the enemy's body.
- Place 6 crystals: the oldest shatters when the 6th forms. Wait 30 s: a crystal shatters by itself and flickers in its last 3 s.
- Enemy touching a crystal gets Chilled (and keeps it while it stays inside).
- Multiplayer (server + 2 clients): the other client sees the throw, the settle, the crystal, the eviction and the expiry; the chill shows on the enemy for both; a player leaving makes their crystals vanish.

### 1.7 Next chat prompt
"Implement patch W1.3 of docs/weapon_overhaul/ROADMAP.md (Ice Boomerang chain). Read section 1 of the roadmap, clone, and check the code of W1.1 + W1.2 first."