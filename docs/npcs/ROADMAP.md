# NPC Story and Interactions: Development Roadmap

The NPC layer that makes the mod playable: the Dryad arrives from the Eye of Cthulhu, the Mechanic is hidden underground, her backpack waits in the dungeon, an "Important dialogue" system (exclamation mark) guides the player, and the Mechanic hands out the first super weapon scrolls. Written against repo commit `75653b2` (2026-10-30). Where this document and the code disagree, the code wins. Nothing in this document is built yet.

## 1. Decisions

### Fixed by Ismail
- **Dryad.** She drops from the Eye of Cthulhu on its first defeat, but only if she has not arrived yet.
- **Mechanic.** Her tied (bound) NPC spawns at a random underground spot, the same idea as the Goblin Tinkerer, instead of in the dungeon. Rescuing her works like any bound NPC.
- **Her Backpack.** A new entity takes her place in the dungeon. It spawns where the vanilla tied Mechanic would. Right click: the player receives an item called **The Mechanic's Bag**.
- **The Bag.** The player carries it in the inventory and talks to the Mechanic. The bag is consumed and the rest of her shop unlocks.
- **Shop (temporary).** Everything is removed from her shop for now. The vanilla Clockwise and Counter Clockwise conveyor belts are her default stock, even without the bag.
- **Important dialogue.** NPCs with important dialogue show an exclamation mark. Entries are registered dynamically with a condition (when it starts) and an exit condition (when it ends). While the condition is met and the exit is not, the NPC gives the same dialogue every time. Examples of exits: killing another boss, finding another NPC.
- **Dryad dialogue 1.** Placeholder: "oh {Player name}... thank you for saving me, I was trying to rescue someone proficient in machines, she was lost underground... can you help rescue her?" Exit: the player finds the Mechanic underground and she becomes an NPC with a house.
- **Dryad dialogue 2.** After the rescue she says they talked and the plan is with the Mechanic.
- **Mechanic dialogue.** When the player talks to her she gives 3 scrolls, one per component of super weapon #1 (the missile: drill, tank, thruster), and explains the super weapon: "this would require a lot of parts so I would recommend automating them". All text is placeholder; Ismail rewrites it later.
- **Skeletron first.** If the Eye of Cthulhu was never killed and Skeletron is killed, the Mechanic spawns fully in the dungeon, normally (tied, rescuable). Rescuing her in that state unlocks her whole shop.

### Defaults chosen by Claude (change any; all are listed again in section 9)
- D1. "Drop" means the Dryad appears as a town NPC at the Eye of Cthulhu's death position (chat line "The Dryad has arrived"). No item, no cell.
- D2. "Has not arrived yet" = no Dryad exists right now AND no Dryad has ever existed in this world (a latched flag). A Dryad who died later is not re-dropped.
- D3. "First defeated" uses our own saved flag, not `NPC.downedBoss1`, so the decision does not depend on vanilla's update order (V4). Old worlds are ignored (Ismail, 2026-10-30).
- D4. All story state is WORLD state (saved, synced), not per player, like recipe knowledge.
- D5. Her Backpack opens a normal chat window with one button, "Take it". No instant pickup.
- D6. SUPERSEDED by Ismail (2026-10-30), see "Confirmed by Ismail": the Mechanic shows "!" while the player carries the Bag and asks "Have you found my bag?" with the buttons "Give the bag" and "Close". The button sends the `TurnInBag` request; the server checks the bag, the client removes it after the answer (the same pattern as `RecipeKnowledgeNet` parchments).
- D7. The shop unlock is a world flag. After it, the vanilla shop list comes back behind that flag (entries are gated, not deleted). The first-bag player unlocks it for everyone.
- D8. Dryad dialogue 1 exits when the Mechanic is rescued AND housed (literal reading of the plan). One tunable switch turns the house requirement off.
- D9. The Mechanic's scroll gift is once per world, to the player who talks to her first. It is handed over as three Sealed Scrolls (`ClosedScrollItem`), each with its own one-recipe pool.
- D10. The scroll pools point at placeholder recipe art until Ismail decides what the part scrolls teach (see Q9).
- D11. Skeletron-first fork: the underground Mechanic is removed (only one Mechanic can exist), the dungeon Backpack is replaced by the tied Mechanic, the Dryad story is switched off, the Mechanic's own scroll dialogue still works after her rescue.
- D12. Underground placement: random spots between the rock layer and 100 tiles above the Underworld, open 3x4 space on a solid floor, no liquid, away from the world spawn, the dungeon and the other bound NPCs.
- D13. SUPERSEDED: old worlds are ignored (Ismail, 2026-10-30). No migration and no load-time placement; only new worlds get the story, placement runs in `PostWorldGen`.
- D14. The exclamation mark is drawn above any NPC whose entry condition is met for the LOCAL player, only while the NPC is on screen and its chat is not open.

### Confirmed by Ismail (2026-10-30)
- D1 (the Dryad appears at the Eye's death spot), D2 (what "not arrived yet" means), D7 (the vanilla shop list returns after the bag) and D8 (the Dryad's exit needs the Mechanic housed) are confirmed.
- **Her Backpack chat:** text "Looks dirty and full of wires", buttons "Take it" and "Close".
- **Bag turn-in (replaces D6):** while the player carries the Bag, the Mechanic gets an important dialogue with the "!". She says "Have you found my bag?" with the buttons "Give the bag" and "Close". So the important dialogue framework (N2.1) supports two chat buttons per entry (labels and a click callback), not only text.
- **Bag loss (replaces Q4):** the Bag respawns in the dungeon (Her Backpack comes back) if the player loses it. It stops respawning when a player holds it in the inventory, or after it was given to the Mechanic (`MechanicShopUnlocked`). Rule: a server watcher respawns the Backpack at its saved position when `BackpackTaken` is true, `MechanicShopUnlocked` is false, no online player holds a Bag in the inventory (main slots, banks and the cursor item) and no Bag lies on the ground, for `BackpackRespawnDelayTicks` in a row (so a Bag that was just dropped or is being picked up does not trigger it). Known limits: a Bag in a chest counts as lost, and with nobody online the watcher does not run; a second Bag from a respawn is harmless because only one is consumed.
- **Old worlds (replaces D13 and Q12):** ignored.
- Not answered yet: scrolls lost (Q8). The other open items stay on their defaults.

## 2. The story flow

Route A (normal):
1. World generation: the vanilla dungeon tied Mechanic becomes Her Backpack. A new tied Mechanic is hidden underground.
2. Eye of Cthulhu first defeat: the Dryad appears if she never arrived.
3. Dryad "!" (dialogue 1) repeats until the Mechanic is rescued and housed.
4. The player finds and frees the Mechanic. She becomes a town NPC (vanilla rescue).
5. Mechanic "!" (scroll dialogue): she explains the missile and gives the 3 scrolls (once). Dryad "!" (dialogue 2) shows from the Dryad's exit until the scrolls are given.
6. Later: Skeletron falls, the dungeon opens, the player takes the Bag from Her Backpack.
7. Mechanic "!" while the player carries the Bag: the Bag is consumed, the full shop unlocks.

Route B (Skeletron before the Eye of Cthulhu):
1. First Skeletron kill with the Eye never killed: the fork runs (D11).
2. The tied Mechanic is in the dungeon. Rescuing her sets the shop unlock at once. The Backpack and Bag are no longer needed.

## 3. The model (exact rules the code must follow)

**World flags** (`StorySystem`, string keys, saved in the world file, synced on join and on change):
`EyeKillSeen`, `DryadSeen`, `MechanicPlaced`, `BackpackPlaced`, `BackpackTaken`, `MechanicRescued`, `MechanicShopUnlocked`, `ScrollsGiven`, `DungeonFork`, plus one `Done.<DialogueId>` flag per important dialogue. The saved data also holds the Backpack and Mechanic tile positions (server only, used by the fork and the debug commands).

**Rules**
- S1. Only the server (or single-player) writes a flag. Clients read.
- S2. `MechanicRescued` is latched by a server watcher the first tick `NPC.savedMech` is true. If `DungeonFork` is set at that moment, `MechanicShopUnlocked` is set too.
- S3. `DryadSeen` is latched by a server watcher the first tick any Dryad NPC exists.
- S4. Eye of Cthulhu death (server, `OnKill`): if `EyeKillSeen` is false, set it; then if `DryadSeen` is false and no Dryad exists, spawn the Dryad at the death position, announce her arrival, set `DryadSeen`.
- S5. Skeletron death (server, `OnKill`): if `DungeonFork` is false and `EyeKillSeen` is false and `MechanicRescued` is false, run the fork (D11) and set `DungeonFork`.

**Important dialogue entry** (registered in `ImportantDialogueDefinitions`, the file Ismail edits, like `ScrollPoolDefinitions`):
- `Id` (unique string), `NpcType`, `Priority` (higher shows first when several are active on one NPC),
- `Condition(Player)`: when the entry starts. Evaluated on the local client, may look at the local inventory.
- `Exit()`: when it ends. World level only, evaluated on the server every `DialogueExitCheckTicks`. When true, the server sets `Done.<Id>` and the entry is retired for good (latched, so an exit that could flip back, like "has a house", cannot revive it).
- `Text(Player)`: a localization key `Mods.Factorraria.Dialogue.<Id>`, `{PlayerName}` replaced.
- `OnTalkStart(Player, NPC)` (optional): runs once on the local client when the player starts a conversation with that NPC while the entry is active. Used for the bag turn-in and the scroll gift (they send a request, see section 5).
- Active entry of an NPC = the highest `Priority` entry with `Condition` true and `Done` false. The NPC shows "!" while any entry is active; the chat text is the active entry's text; when none is active the NPC chats normally.

**Shop.** The Mechanic's vanilla shop entries get an extra condition (`MechanicShopUnlocked`). Two entries are added with no condition: `ItemID.ConveyorBeltLeft` (Clockwise) and `ItemID.ConveyorBeltRight` (Counter Clockwise) (verified from `Localization/en-US.hjson`).

**Placement (rule M1 to M6).**
- M1. After world generation (`PostWorldGen`), scan `Main.npc` for the vanilla tied Mechanic (`NPCID.BoundMechanic`). Remember her position, deactivate her, spawn Her Backpack there. (Assumes V1: vanilla spawns her as a real NPC during generation.)
- M2. Pick an underground cell by D12 and spawn a tied Mechanic there with the same fields vanilla sets for the tied Goblin Tinkerer (V1).
- M3. Her Backpack is a `ModNPC`: friendly, never moves, cannot be hurt, saved with the world (`NeedSaving`), never despawns (`CheckActive` false).
- M4. Taking the Bag: the server verifies the NPC, the distance and `BackpackTaken`, gives one Bag to the player, removes the Backpack, sets `BackpackTaken`.
- M5. The Bag is a quest-rarity item. It should survive lava (V9).
- M6. Old worlds are ignored (Ismail, 2026-10-30): no migration and no placement at load. Placement runs only in `PostWorldGen`.

## 4. Codebase and API facts this plan relies on

Confirmed in the repo clone at `75653b2`:
- `ScrollPools` / `ScrollPoolDefinitions` / `ClosedScrollItem` exist. A scroll carries a pool NAME; reading the parchment rolls one recipe or art entry from that pool, then falls back to the whole book when the pool is used up. Pool entries are catalog recipes (`.Add`, `.AddLiquid`) or art pages (`.AddArt`). The three super weapon parts have no machine recipe in the catalog today (placeholder Anvil recipes in `RecipeSystem`).
- `RecipeKnowledgeNet.RequestReadParchment` is the pattern for "client asks, server checks the inventory, client consumes after the answer". `FactorrariaNet` has `Register`, `SendRequest`, `SendToClient`, `Broadcast`, `SendToNearby`, `InReach`, `SyncInventoryItem`; `JoinSync.Register(key, serverWrite, clientRead)` adds a join provider.
- Last packet id is `SuperWeaponEffect = 57`. New ids append after the highest one in the enum when the patch is written.
- `BossLootGlobal` (in `Common/Globals`, namespace `Factorraria.Common.NPCs`) already hooks the Eye of Cthulhu loot. The new Eye and Skeletron hooks go in a separate GlobalNPC.
- Server-side item granting patterns: `player.QuickSpawnItem(source, item)` (`MachineSlotEdit`, `CartPanelEdit`) and `Item.NewItem` (broadcasts by itself).
- `ConveyorBeltLeft` is shown as "Conveyor Belt (Clockwise)", `ConveyorBeltRight` as "Conveyor Belt (Counter Clockwise)" (`Localization/en-US.hjson`).
- Existing worldgen systems use `ModifyWorldGenTasks` and `PostWorldGen` (`PostWorldGenEdits`). An on-screen helper `IsOnScreen(center, margin)` exists on `BaseMachine`; for NPC drawing look for a shared helper first (grep) before writing one.

Confirmed in the public tModLoader 1.4.4 source (Ismail's local build is 2026.08.3.0; recheck on it in the patch that uses each):
- `GlobalNPC.CanChat`, `GetChat(NPC, ref string)` and `OnChatButtonClicked` run on the local client only. `GlobalNPC.ModifyShop(NPCShop)` and `ModifyActiveShop` exist. `GlobalNPC.NeedSaving` exists (a saved NPC does not despawn naturally).
- `ModNPC` has `CanChat`, `GetChat`, `SetChatButtons`, `OnChatButtonClicked(bool, ref string)`, `NeedSaving`, `SaveData`, `LoadData`, `CheckActive`.
- `NPCShop.Entries` is a readable list and `ActiveEntries` skips disabled entries. Not yet confirmed: `Entry.Disable()` and `Entry.AddCondition()` (V5).
- `NPCID.Sets.SavesAndLoads` is internal in tModLoader, so a ModNPC must use `NeedSaving`.

NOT verified (the vanilla source is not public from here, see section 10): how vanilla spawns the tied NPCs at world generation, the exact rescue code, the Dryad arrival rules, and the order of events in vanilla NPC death.

## 5. Multiplayer design (rule 9)

- **Server (or single-player) owns:** every flag, the placement, the Dryad spawn, the fork, the Backpack removal, the bag check and removal of the result, the scroll hand-out, the done-latching of dialogue exits.
- **Clients own (local only):** the exclamation mark draw, choosing the chat text, the shop contents (computed from the synced flags), the talk-start detection (`Player.talkNPC` changes from none to an NPC), requests.
- **Synced state:** the flags. Join: a `story-flags` join provider sends them all. Change: `StoryFlagChanged` (server to all clients).
- **Packets (all code in `Common/Net/StoryNet.cs`):**
  - `StoryFlagChanged` (ToClient): flag key, value.
  - `StoryActionRequest` (ToServer): kind byte (`TakeBag`, `TurnInBag`, `ClaimScrolls`) plus the NPC index.
  - `StoryActionResult` (ToClient): kind, ok, reason. On `TurnInBag` ok the client removes one Bag from its inventory and calls `FactorrariaNet.SyncInventoryItem`.
- **Server validation for every request:** the player is alive and in game, the NPC exists with the right type, the player is within `InteractReachTiles` of it, the flag state allows it (not taken, not given, not unlocked), the bag exists in the server's copy of the inventory. A refused request leaves the client unchanged.
- **Single-player:** the request helper calls the server handler directly (working rule 3); the result is applied at once.
- **Rate limit:** register the requests with a cost like the other ToServer packets.
- **Join mid-story:** the flags arrive with the join sync; the Backpack and the tied Mechanic are normal saved NPCs and sync with the vanilla NPC sync.

## 6. Data model and files

New:
- `Common/Story/StorySystem.cs`: flags, save and load, the `savedMech` and Dryad watchers, the Skeletron and Eye handlers (as a `GlobalNPC` in `StoryEventsGlobalNPC.cs`).
- `Common/Story/StoryFlags.cs`: the flag keys.
- `Common/Net/StoryNet.cs`: the three packets, the join provider, the request helpers and handlers.
- `Common/Story/ImportantDialogue/ImportantDialogueRegistry.cs`, `ImportantDialogueDefinitions.cs` (the file Ismail edits), `ImportantDialogueGlobalNPC.cs` (chat text, exclamation mark), `ImportantDialoguePlayer.cs` (talk-start edge).
- `Common/Story/MechanicShopGlobalNPC.cs`: `ModifyShop`.
- `Common/Story/MechanicPlacement.cs`: `PostWorldGen`, migration, the underground search, the fork helper.
- `Common/Story/StoryCommand.cs`: `/fstory` (status, flag, dump, place, backpack, bag, scrolls, fork). Temporary, removed in N9.
- `Content/NPCs/HerBackpack/HerBackpackNPC.cs` + `HerBackpack.png`.
- `Content/Items/Story/MechanicsBagItem.cs` + `MechanicsBagItem.png`.
- `Content/UI/ImportantDialogue/Exclamation.png` (art, optional).
Edits: `ScrollPoolDefinitions` (three part pools), `RecipeArtDefinitions` (when the part art exists), `FactorrariaPacket` (3 ids), `Localization/en-US_Mods.Factorraria.hjson` (names, dialogue), `docs/multiplayer/ROADMAP.md` (a Progress line per patch).

Art Ismail makes (paths and sizes; until a PNG exists the code uses `RequestIfExists` and a text fallback, no placeholder art is generated):
- `HerBackpack.png`: NPC sheet, size set in N4.2 (a single frame is enough; the code reads the size).
- `MechanicsBagItem.png`: item icon, about 28x32.
- `Exclamation.png`: any size up to about 16x28; drawn centered above the head with a small bob. Missing = a yellow bordered "!" text.

## 7. Patch plan

One chat = one patch (or a few small ones from one phase). Each ships as a patch script, marks itself `[~]` in section 12 and adds a Progress line to `docs/multiplayer/ROADMAP.md`.

### Phase N0 - Verification (before N4)
Run the checks in section 10 (V1, V2, V3, V4, V9, V11) on the local vanilla source and report the findings. N1 to N3 do not depend on them.

### Phase N1 - Story foundation
- N1.1 `StorySystem`: flags, save and load (TagCompound), the join provider, `StoryFlagChanged`, `/fstory status` and `/fstory flag`. No gameplay yet.
- Tests: set a flag with the command, save and reload the world, the flag is still set; a second client joining sees the flag; a client's own `/fstory flag` is refused (server only).

### Phase N2 - Important dialogue framework
- N2.1 Registry, definitions file with ONE test entry on the Guide, `ImportantDialogueGlobalNPC` (chat text, exclamation mark), `ImportantDialoguePlayer` (talk-start), the server exit check and `Done.<Id>` latching.
- Tests: the Guide shows "!" only while the test condition is true, the chat text is the entry's text and repeats on every click, setting the exit flag removes the "!" and brings the normal chat back for good (even after the exit flips back), the "!" hides while chatting and off screen, a second client sees the same. Two entries on one NPC show by priority.

### Phase N3 - Mechanic shop
- N3.1 `MechanicShopGlobalNPC`: gate the vanilla entries behind `MechanicShopUnlocked`, add the two conveyor belts.
- Tests: the Mechanic's shop (spawn her with a vanilla method or the debug command) shows only the two belts; `/fstory flag MechanicShopUnlocked on` brings the vanilla list back; the belts' prices and names look right; a second client sees the same list.

### Phase N4 - Placement and the Backpack
- N4.1 `MechanicPlacement` (M1, M2, M6 plus the underground search and tunables), the `savedMech` watcher (S2), the `/fstory dump` and `/fstory place` tools, a minimal `HerBackpackNPC` (stands, saved, no interaction yet).
- Tests (new world on small, medium and large): there is a Backpack in the dungeon where the tied Mechanic used to be and a tied Mechanic underground; the log prints both positions; saving and reloading keeps both; the underground Mechanic can be rescued and becomes the town NPC; `MechanicRescued` turns on.
- N4.2 Backpack interaction and `MechanicsBagItem`: chat window "Looks dirty and full of wires" with the buttons "Take it" and "Close", the `TakeBag` request and result, and the respawn watcher (see "Confirmed by Ismail", `BackpackRespawnDelayTicks`).
- Tests: the Bag arrives once, the Backpack disappears for every client, a second player cannot take a second Bag, quest tooltip, lava does not destroy the Bag (V9), reload keeps `BackpackTaken`.

### Phase N5 - Bag turn-in
- N5.1 Mechanic dialogue entry `Mechanic.Bag` (condition: the local player carries the Bag and the shop is locked): "Have you found my bag?" with the buttons "Give the bag" and "Close"; the button sends the `TurnInBag` request, the Bag is consumed after the result, `MechanicShopUnlocked` is set.
- Tests: with the Bag the Mechanic shows "!", pressing "Give the bag" consumes exactly one Bag and unlocks the full shop for everyone, "Close" keeps the Bag, a second Bag does nothing, talking without the Bag shows no "!", a client that drops the Bag mid-talk is refused cleanly.

### Phase N6 - The Dryad
- N6.1 Eye of Cthulhu handler (S3, S4), the Dryad spawn and announcement, Dryad dialogues 1 and 2 (placeholder text).
- Tests: first Eye kill with no Dryad spawns her and she says dialogue 1; with a Dryad already there nothing spawns but dialogue 1 still shows; a second Eye kill does nothing; dialogue 1 stops once the Mechanic is rescued and housed; dialogue 2 shows until the scrolls are given; on a dedicated server with two clients.

### Phase N7 - The super weapon intro and the scrolls
- N7.1 Three scroll pools (`ScrollPoolDefinitions`), the `Mechanic.SuperWeaponIntro` entry, the `ClaimScrolls` request, `ScrollsGiven`.
- Tests: right after her rescue the Mechanic shows "!", talking gives exactly three Sealed Scrolls once, each opens into a parchment, reading each unlocks its page, the "!" goes away on the Mechanic and on the Dryad, a second player gets nothing.

### Phase N8 - Skeletron first
- N8.1 The Skeletron handler (S5) and the fork helper.
- Tests: kill Skeletron with `EyeKillSeen` false: the underground Mechanic is gone, the dungeon has a tied Mechanic, rescuing her unlocks the full shop at once and the Dryad story stays off; killing the Eye first and then Skeletron changes nothing; Skeletron killed twice does nothing the second time; the fork when the Bag is already taken.

### Phase N9 - Hardening
- Tunables moved to a config, localization pass (Ismail's text), remove `/fstory` and the test entry, dedicated server plus two clients full run of both routes, join sync with a world in the middle of the story, update this document.

## 8. Tunables (names are final in the patches)
`UndergroundMinDepthTiles`, `UndergroundBottomMarginTiles` (100), `PlacementTries`, `MinDistanceFromSpawnTiles`, `MinDistanceFromBoundNpcsTiles`, `OpenCellWidthTiles` (3), `OpenCellHeightTiles` (4), `InteractReachTiles`, `DialogueExitCheckTicks` (30), `ConditionCacheTicks` (10), `ExclamationBobPixels`, `ExclamationOffsetY`, `RequireHouseForDryadExit` (true), `ScrollsPerPart` (1), `BackpackRespawnDelayTicks` (600).
Plain static fields, not `const`, so hot reload can change them.

## 9. Open items (each default below is used unless Ismail says otherwise)
- Q1. Dryad "drop": D1 (appears as an NPC). Alternatives: a tied Dryad he must free, or an item.
- Q2. "Not arrived yet": D2. Alternative: only "no Dryad right now".
- Q3. Backpack: D5 (chat window, Take button). Alternative: a right click picks it up at once.
- Q4. RESOLVED by Ismail: the Backpack respawns while the Bag is lost (see "Confirmed by Ismail").
- Q5. RESOLVED by Ismail: a chat button "Give the bag" (see D6).
- Q6. What "temporarily remove everything" means for the shop after the bag: D7 (the vanilla list returns). Alternative: it stays empty after the bag too, until he designs the stock.
- Q7. Dryad exit needs a house: D8 (yes). Alternative: only the rescue.
- Q8. Scrolls: D9 (once per world, to the first player). If they are lost: `/fstory scrolls` for testing. Alternatives: the Mechanic gives a repeat set for a price, or the scroll gift is automatic recipe unlocks without items.
- Q9. What the three scrolls teach: today the part recipes are Anvil recipes, which are not in the recipe book, so the pools use placeholder art pages. Ismail either draws recipe pages and adds `AddArt`, or adds Mechanical Assembler recipes for the parts and uses `.Add("MechanicalAssembler", <part item>)`. A pool with one entry falls back to a random recipe if that page is already unlocked (the existing scroll rule); that is harmless.
- Q10. The fork (D11) details: removing the underground Mechanic, switching off the Dryad story, keeping her scroll dialogue.
- Q11. Underground placement (D12): layer, distances, biomes to avoid (Jungle, Corruption or Crimson, Marble, Granite, hive, Temple are avoided by default; Dungeon walls too).
- Q12. RESOLVED by Ismail: old worlds are ignored.
- Q13. Should the Dryad's dialogue 1 also start when she arrived by the vanilla route (Eater of Worlds or Brain of Cthulhu first) and the Eye was never killed? Default: no, the story starts at the Eye (so skipping the Eye means the dungeon route only).

## 10. Verification list (checked in the local vanilla source before the patch that needs it)
Search the local decompiled vanilla source (Ismail's tModLoader source folder; the 1.3 decompile can mislead, prefer 1.4). Suggested searches: `BoundMechanic`, `BoundGoblin`, `savedMech`, `NPCID.Dryad`, `downedBoss1`.
- V1 (N4.1, BLOCKER) How vanilla places the tied Mechanic, Goblin Tinkerer and Wizard during generation: which pass, the spawn call, the fields it sets (`homeless`, `direction`, `ai`, home tile). Confirm the tied Mechanic exists as a real NPC at `PostWorldGen`. Fallback if not: find the dungeon room from `GenVars` and place the Backpack with our own search.
- V2 (N4.1) The rescue code: does it set `NPC.savedMech`, does it call `Transform`, and is `savedMech` synced to clients.
- V3 (N6.1) Vanilla Dryad arrival rule (which boss flags), the arrival announcement call, and that a homeless spawned town NPC persists.
- V4 (N6.1, N8.1) When `OnKill` runs relative to the vanilla boss-defeated flags. Our own flags make this safe either way; this only confirms the hook fires on the server in single-player and multiplayer.
- V5 (N3.1) `NPCShop.Entry` members (`Disable`, `AddCondition`) and the Mechanic's shop name (expected `"Shop"`).
- V6 (N2.1) `GlobalNPC.PostDraw` for the mark and where `Player.talkNPC` is set, including after the chat closes.
- V7 (N2.1) Whether `GetChat` can run more than once per conversation (it must stay free of side effects; the plan already keeps side effects in the talk-start edge).
- V8 (N4.1) A saved `ModNPC` created during generation survives save and reload and does not despawn.
- V9 (N4.2) Lava immunity for a quest-rarity item (`ItemID.Sets.IsLavaImmuneRegardlessOfRarity` or rarity rules).
- V10 (N3.1) Which belt item is Clockwise: resolved, `ConveyorBeltLeft`.
- V11 (N4.1) `GenVars` dungeon location fields, for the placement fallback and the exclusion zone.

## 11. Risks
- If vanilla does not spawn the tied Mechanic as an NPC during generation (V1), the Backpack placement needs our own dungeon search. The plan allows it, but N4.1 grows.
- Gating the vanilla shop entries can clash with other mods that edit the Mechanic's shop.
- A talk-start edge detection that misses (chat opened and closed within a tick) would skip a turn-in. The player can simply talk again.
- Losing the Bag or the scrolls can block progress (Q4, Q8). The debug commands cover testing only.
- Worlds with the Mechanic already rescued must not be touched (rule M6).
- Dryad spawning without a house: she wanders to the world spawn like any homeless town NPC; the player has to house her.
- The Skeletron fork removes an NPC the player may already have found; it only runs when the Mechanic was never rescued.

## 12. Progress

One line per patch: `[ ]` not started, `[~]` applied, untested, `[x]` tested and done.

- [ ] N0 Verification of the vanilla facts (V1 to V11, Ismail reads the local source)
- [~] N1.1 Story foundation (patch_npc_n1_1_story_foundation.ps1, applied, untested; Common/Story/StoryFlags.cs, StorySystem.cs, StoryCommand.cs, Common/Net/StoryNet.cs, packet StoryFlagChanged = 58. AUTHORITY: the server / single-player writes flags (`StorySystem.ServerSet`), clients only read. SYNC: join provider "story-flags" sends the whole set, StoryFlagChanged broadcasts each change, single-player applies directly with no packet. SAVE: world data key "Flags". `/fstory status | known | flag <name> [on|off]` is a World command, so in multiplayer it runs on the server. Tests: see the N1.1 reply)
- [ ] N2.1 Important dialogue framework (registry, "!" mark, chat text, exit latching)
- [ ] N3.1 Mechanic shop (gate vanilla entries, two belts)
- [ ] N4.1 Placement (underground Mechanic, Backpack in the dungeon, migration, rescue watcher)
- [ ] N4.2 Her Backpack interaction and The Mechanic's Bag
- [ ] N5.1 Bag turn-in and full shop unlock
- [ ] N6.1 The Dryad (Eye of Cthulhu arrival, dialogues 1 and 2)
- [ ] N7.1 Super weapon intro and the three scrolls
- [ ] N8.1 Skeletron first fork
- [ ] N9 Hardening, remove debug, dedicated server test

Next chat prompt (after N1.1 applied): "N1.1 is applied (check its Progress line); results: <paste>. Implement patch N2.1 (important dialogue framework) of the NPC story roadmap (docs/npcs/ROADMAP.md). Decisions are in section 1 (Confirmed by Ismail) and section 9. Vanilla facts V1 to V11 so far: <paste or 'not checked yet'>."