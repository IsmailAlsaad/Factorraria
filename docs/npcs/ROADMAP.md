# NPC Story and Interactions: Development Roadmap

The NPC layer that makes the mod playable: the Dryad arrives from the Eye of Cthulhu, the Mechanic is hidden underground, her backpack waits in the dungeon, an "Important dialogue" system (exclamation mark) guides the player, and the Mechanic hands out the first super weapon scrolls. Written against repo commit `75653b2` (2026-10-30). Where this document and the code disagree, the code wins. Section 12 (Progress) says what is built. REVISED 2026-10-30 (chat of patch N2.1): the tied Mechanic and Her Backpack are NOT placed at world generation any more. They are rare NPCs that spawn near a player under conditions, and the Lifeform Analyzer detects them (section 1, "Rare spawns").

## 1. Decisions

### Fixed by Ismail
- **Dryad.** She drops from the Eye of Cthulhu on its first defeat, but only if she has not arrived yet.
- **Mechanic.** Her tied (bound) NPC spawns at a random underground spot, the same idea as the Goblin Tinkerer, instead of in the dungeon. Rescuing her works like any bound NPC. REVISED: she is a rare spawn near the player, not placed at world generation (see "Rare spawns" below).
- **Her Backpack.** A new entity takes her place in the dungeon. REVISED: it is a rare spawn in the dungeon near the player after Skeletron, not placed at world generation (see "Rare spawns" below). Right click: the player receives an item called **The Mechanic's Bag**.
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
- D11. REVISED (rare spawns): Skeletron-first fork. `DungeonFork` is set, the tied Mechanic's rare spawn moves from the Cavern layer to the dungeon, the Backpack rule is off, the Dryad story is switched off, the Mechanic's own scroll dialogue still works after her rescue. Nothing is removed or replaced.
- D12. SUPERSEDED (rare spawns, see "Rare spawns" below): there is no placement search any more; the vanilla spawn system picks the spot near the player.
- D13. SUPERSEDED: old worlds are ignored (Ismail, 2026-10-30). No migration and no load-time placement; only new worlds get the story, placement runs in `PostWorldGen`.
- D14. The exclamation mark is drawn above any NPC whose entry condition is met for the LOCAL player, only while the NPC is on screen and its chat is not open.

### Confirmed by Ismail (2026-10-30)
- D1 (the Dryad appears at the Eye's death spot), D2 (what "not arrived yet" means), D7 (the vanilla shop list returns after the bag) and D8 (the Dryad's exit needs the Mechanic housed) are confirmed.
- **Her Backpack chat:** text "Looks dirty and full of wires", buttons "Take it" and "Close".
- **Bag turn-in (replaces D6):** while the player carries the Bag, the Mechanic gets an important dialogue with the "!". She says "Have you found my bag?" with the buttons "Give the bag" and "Close". So the important dialogue framework (N2.1) supports two chat buttons per entry (labels and a click callback), not only text.
- **Bag loss (replaces Q4):** the Bag respawns in the dungeon (Her Backpack comes back) if the player loses it. It stops respawning when a player holds it in the inventory, or after it was given to the Mechanic (`MechanicShopUnlocked`). Rule: a server watcher clears `BackpackTaken` (the Backpack then comes back through its rare spawn, it is not placed at a saved position) when `BackpackTaken` is true, `MechanicShopUnlocked` is false, no online player holds a Bag in the inventory (main slots, banks and the cursor item) and no Bag lies on the ground, for `BackpackRespawnDelayTicks` in a row (so a Bag that was just dropped or is being picked up does not trigger it). Known limits: a Bag in a chest counts as lost, and with nobody online the watcher does not run; a second Bag from a respawn is harmless because only one is consumed.
- **Old worlds (replaces D13 and Q12):** ignored.
- Not answered yet: scrolls lost (Q8). The other open items stay on their defaults.
- **Rare spawns (replaces worldgen placement), Ismail 2026-10-30:** neither the underground tied Mechanic nor Her Backpack is placed during world generation. Both are RARE NPCs that appear near a player when a condition holds (underground, after a boss, and so on), like vanilla rare creatures. The vanilla **Lifeform Analyzer** must detect both, so each sets `NPC.rarity` above 0 (V12 checks what the analyzer really reads). Defaults chosen by Claude, all open to change (Q11, Q14, tunables in section 8):
  - D15. Tied Mechanic: the vanilla `NPCID.BoundMechanic`, added to the spawn pool by a GlobalNPC `EditSpawnPool` (server / single-player), only while the Eye of Cthulhu was defeated (`NPC.downedBoss1`; required since Ismail 2026-10-31), `MechanicRescued` is false, `DungeonFork` is false, no tied or town Mechanic exists, and the player is in the Cavern layer (not in the dungeon, not in the Underworld). Weight `MechanicSpawnWeight` (small).
  - D16. Her Backpack: a `ModNPC` with `SpawnChance`, only while Skeletron was defeated (`NPC.downedBoss3`), the Mechanic was rescued before (`MechanicRescued` is true; Ismail 2026-10-31), the player is in the dungeon, `BackpackTaken` is false, `MechanicShopUnlocked` is false, `DungeonFork` is false and no Backpack exists. Weight `BackpackSpawnWeight`.
  - D17. Both can despawn like any vanilla NPC when the player leaves; they roll again later. Nothing about them is saved except the flags. At most one of each exists at a time.
  - D18. Skeletron-first fork (D11): `DungeonFork` moves the tied Mechanic's rule from the Cavern layer to the dungeon (weight `DungeonMechanicSpawnWeight`); the Backpack rule is off.
  - D19. REMOVED (Ismail 2026-10-31): there is no world generation cleanup. The tied Mechanic is just an NPC; world generation is not touched and nothing about it is assumed. Old worlds are ignored.

## 2. The story flow

Route A (normal):
1. World generation is not touched. Nothing is placed: the tied Mechanic (Cavern layer) and Her Backpack (dungeon, after Skeletron) are rare NPCs that spawn near a player; the Lifeform Analyzer shows them.
2. Eye of Cthulhu first defeat: the Dryad appears if she never arrived.
3. Dryad "!" (dialogue 1) repeats until the Mechanic is rescued and housed.
4. The player finds and frees the Mechanic. She becomes a town NPC (vanilla rescue).
5. Mechanic "!" (scroll dialogue): she explains the missile and gives the 3 scrolls (once). Dryad "!" (dialogue 2) shows from the Dryad's exit until the scrolls are given.
6. Later: Skeletron falls, the dungeon opens, Her Backpack can spawn near the player in the dungeon (rare), and the player takes the Bag from it.
7. Mechanic "!" while the player carries the Bag: the Bag is consumed, the full shop unlocks.

Route B (Skeletron before the Eye of Cthulhu):
1. First Skeletron kill with the Eye never killed: the fork runs (D11).
2. The tied Mechanic can now spawn (rare) in the dungeon instead of the Cavern layer. Rescuing her sets the shop unlock at once. The Backpack and Bag are no longer needed.

## 3. The model (exact rules the code must follow)

**World flags** (`StorySystem`, string keys, saved in the world file, synced on join and on change):
`EyeKillSeen`, `DryadSeen`, `BackpackTaken`, `MechanicRescued`, `MechanicShopUnlocked`, `ScrollsGiven`, `DungeonFork`, plus one `Done.<DialogueId>` flag per important dialogue, plus the temporary `TestDialogueExit` (removed in N9). No `MechanicPlaced` / `BackpackPlaced` (removed in patch N2.1: nothing is placed any more) and no saved positions: the two rare NPCs spawn dynamically (rules M1 to M7).

**Rules**
- S1. Only the server (or single-player) writes a flag. Clients read.
- S2. `MechanicRescued` is latched by a server watcher the first tick `NPC.savedMech` is true. If `DungeonFork` is set at that moment, `MechanicShopUnlocked` is set too.
- S3. `DryadSeen` is latched by a server watcher the first tick any Dryad NPC exists.
- S4. Eye of Cthulhu death (server, `OnKill`): if `EyeKillSeen` is false, set it; then if `DryadSeen` is false and no Dryad exists, spawn the Dryad at the death position, announce her arrival, set `DryadSeen`.
- S5. Skeletron death (server, `OnKill`): if `DungeonFork` is false and `EyeKillSeen` is false and `MechanicRescued` is false, set `DungeonFork` (D11, D18: this only switches the spawn rules, nothing is removed or replaced).

**Important dialogue entry** (registered in `ImportantDialogueDefinitions`, the file Ismail edits, like `ScrollPoolDefinitions`):
- `Id` (unique string), `NpcType`, `Priority` (higher shows first when several are active on one NPC),
- `Condition(Player)`: when the entry starts. Evaluated on the local client, may look at the local inventory.
- `Exit()`: when it ends. World level only, evaluated on the server every `DialogueExitCheckTicks`. When true, the server sets `Done.<Id>` and the entry is retired for good (latched, so an exit that could flip back, like "has a house", cannot revive it).
- `Text(Player)`: the localization key `Mods.Factorraria.Dialogue.<Id>.Text`, `{PlayerName}` replaced.
- `Button1`, `Button2` (optional, N2.1): a label (`Mods.Factorraria.Dialogue.<Id>.Button1` / `.Button2`) and an `OnClick(Player, NPC)` callback, local client. A button takes the first free chat button slot, so a vanilla Shop or Help button stays first; vanilla Close is always separate.
- `OnTalkStart(Player, NPC)` (optional): runs once on the local client when the player starts a conversation with that NPC while the entry is active. Used for the bag turn-in and the scroll gift (they send a request, see section 5).
- Active entry of an NPC = the highest `Priority` entry with `Condition` true and `Done` false. The NPC shows "!" while any entry is active; the chat text is the active entry's text; when none is active the NPC chats normally.

**Shop.** The Mechanic's vanilla shop entries get an extra condition (`MechanicShopUnlocked`). Two entries are added with no condition: `ItemID.ConveyorBeltLeft` (Clockwise) and `ItemID.ConveyorBeltRight` (Counter Clockwise) (verified from `Localization/en-US.hjson`).

**Rare spawns (rules M1 to M7; replace worldgen placement).**
- M1. REMOVED (Ismail 2026-10-31): world generation is not touched at all, no placement and no cleanup. The tied Mechanic is just an NPC.
- M2. The tied Mechanic is the vanilla `NPCID.BoundMechanic`, added to the spawn pool by a GlobalNPC `EditSpawnPool` (server / single-player only) under the D15 conditions, and given `NPC.rarity` above 0 so the Lifeform Analyzer shows her. The spawn spot is vanilla's (near the player, off screen), so no search code is needed. If a pool spawn does not give a correct tied NPC (V13), copy the fields vanilla sets for a tied Goblin Tinkerer in a GlobalNPC `OnSpawn`.
- M3. Her Backpack is a `ModNPC`: friendly, never moves, cannot be hurt, `NPC.rarity` above 0, `SpawnChance` under the D16 conditions. It is not saved with the world (a rare spawn, it may despawn).
- M4. Taking the Bag: the server verifies the NPC, the distance and `BackpackTaken`, gives one Bag to the player, removes the Backpack, sets `BackpackTaken`.
- M5. The Bag is a quest-rarity item. It should survive lava (V9).
- M6. At most one tied Mechanic and one Backpack exist at a time (the spawn rules check `NPC.AnyNPCs`).
- M7. Lifeform Analyzer: each rare NPC sets `NPC.rarity` above 0 (value and any other condition the analyzer needs: V12). The Backpack must not be hidden from it by being friendly or a town type; if it is, add a workaround in N4.1.

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
- Verified in the public tModLoader 1.4.4 source for patch N2.1 (recheck on 2026.08.3.0): `SetChatButtons` exists ONLY on `ModNPC` (`NPCLoader.SetChatButtons(ref button, ref button2)` is public static and only calls `ModNPC`). `GlobalNPC` has `PreChatButtonClicked(NPC, bool firstButton)` (false cancels the vanilla button code) and `OnChatButtonClicked`, both local client. `MonoModHooks.Add(MethodBase, Delegate)` is public and tModLoader removes the detour on unload. `GlobalNPC.EditSpawnPool(IDictionary<int,float> pool, NPCSpawnInfo)` and `EditSpawnRate` exist. `NPC.rarity` is a public int; the Lifeform Analyzer is `InfoDisplay.LifeformAnalyzer`, but its NPC scan is in unpatched vanilla (V12).

NOT verified (the vanilla source is not public from here, see section 10): the exact rescue code, the Dryad arrival rules, and the order of events in vanilla NPC death.

## 5. Multiplayer design (rule 9)

- **Server (or single-player) owns:** every flag, the rare spawn rules (`EditSpawnPool` and `SpawnChance` run on the server / single-player), the Dryad spawn, the fork flag, the Backpack removal, the bag check and removal of the result, the scroll hand-out, the done-latching of dialogue exits.
- **Clients own (local only):** the exclamation mark draw, choosing the chat text, the shop contents (computed from the synced flags), the talk-start detection (`Player.talkNPC` changes from none to an NPC), requests.
- **Synced state:** the flags. Join: a `story-flags` join provider sends them all. Change: `StoryFlagChanged` (server to all clients).
- **Packets (all code in `Common/Net/StoryNet.cs`):**
  - `StoryFlagChanged` (ToClient): flag key, value.
  - `StoryActionRequest` (ToServer): kind byte (`TakeBag`, `TurnInBag`, `ClaimScrolls`) plus the NPC index.
  - `StoryActionResult` (ToClient): kind, ok, reason. On `TurnInBag` ok the client removes one Bag from its inventory and calls `FactorrariaNet.SyncInventoryItem`.
- **Server validation for every request:** the player is alive and in game, the NPC exists with the right type, the player is within `InteractReachTiles` of it, the flag state allows it (not taken, not given, not unlocked), the bag exists in the server's copy of the inventory. A refused request leaves the client unchanged.
- **Single-player:** the request helper calls the server handler directly (working rule 3); the result is applied at once.
- **Rate limit:** register the requests with a cost like the other ToServer packets.
- **Join mid-story:** the flags arrive with the join sync; the Backpack and the tied Mechanic are normal NPCs (not saved with the world, they can despawn) and sync with the vanilla NPC sync.

## 6. Data model and files

New:
- `Common/Story/StorySystem.cs`: flags, save and load, the `savedMech` and Dryad watchers, the Skeletron and Eye handlers (as a `GlobalNPC` in `StoryEventsGlobalNPC.cs`).
- `Common/Story/StoryFlags.cs`: the flag keys.
- `Common/Net/StoryNet.cs`: the three packets, the join provider, the request helpers and handlers.
- `Common/Story/ImportantDialogue/`: `ImportantDialogueEntry.cs` (entry and button types), `ImportantDialogueRegistry.cs`, `ImportantDialogueDefinitions.cs` (the file Ismail edits), `ImportantDialogueSystem.cs` (server exit check and Done latching, local condition cache, the "!" draw), `ImportantDialogueGlobalNPC.cs` (chat text, "!", button clicks), `ImportantDialoguePlayer.cs` (talk-start edge), `ImportantDialogueChatButtons.cs` (the `NPCLoader.SetChatButtons` detour). Built in patch N2.1.
- `Common/Story/MechanicShopGlobalNPC.cs`: `ModifyShop`.
- `Common/Story/RareNpcSpawns.cs`: the GlobalNPC `EditSpawnPool` rule for the tied Mechanic, `NPC.rarity` for the Lifeform Analyzer, the fork rule change, the `savedMech` watcher (N4.1).
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
Run the checks in section 10 (V1, V2, V3, V4, V9, V12, V13) on the local vanilla source and report the findings. N1 to N3 do not depend on them.

### Phase N1 - Story foundation
- N1.1 `StorySystem`: flags, save and load (TagCompound), the join provider, `StoryFlagChanged`, `/fstory status` and `/fstory flag`. No gameplay yet.
- Tests: set a flag with the command, save and reload the world, the flag is still set; a second client joining sees the flag; a client's own `/fstory flag` is refused (server only).

### Phase N2 - Important dialogue framework
- N2.1 (as built) The registry, the definitions file (TWO temporary test entries on the Guide), `ImportantDialogueSystem` (server exit check and `Done.<Id>` latching, local condition cache, the "!" draw), `ImportantDialogueGlobalNPC` (chat text, "!", button clicks), `ImportantDialoguePlayer` (talk-start edge) and `ImportantDialogueChatButtons` (up to two chat buttons per entry).
- Chat buttons: tModLoader has `SetChatButtons` only on ModNPC, so N2.1 detours `NPCLoader.SetChatButtons` (client only, `MonoModHooks.Add`) and adds the entry's labels to the first free button slot (a vanilla Shop or Help button stays first, vanilla Close is separate). Clicks come through `GlobalNPC.PreChatButtonClicked` and cancel the vanilla button code. Texts and labels live in `Dialogue.<Id>.Text`, `.Button1`, `.Button2` of the localization file.
- Test entries: `Test.Guide` (low priority, the player carries a Torch, exit = flag `TestDialogueExit`) and `Test.Guide.High` (priority 10, the player carries Wood, one chat button, no exit; retire it with `/fstory flag Done.Test.Guide.High on`). `/fstory dialogue` lists every entry with its state. Both entries, the flag and the command part are removed in N9.
- Tests: the Guide shows "!" only while a Torch is in the inventory; the chat text is the entry's text and repeats on every click; `/fstory flag TestDialogueExit on` removes the "!" within about a second and the normal chat comes back, and it stays gone even after the flag is turned off again; the "!" hides while chatting and when the Guide is off screen; with Torch and Wood both carried the Wood entry wins and the Guide's chat window gets a "Test button" (second slot, next to Help and Close) that prints a line in the chat log and replaces the chat text; "talk start" lines print once per conversation; a second client sees the same "!" and text. Resolves V6, V7 and V14.
- Art: `Content/UI/ImportantDialogue/Exclamation.png` (optional, up to about 16x28). Missing = a yellow bordered "!" text.

### Phase N3 - Mechanic shop
- N3.1 `MechanicShopGlobalNPC`: gate the vanilla entries behind `MechanicShopUnlocked`, add the two conveyor belts.
- Tests: the Mechanic's shop (spawn her with a vanilla method or the debug command) shows only the two belts; `/fstory flag MechanicShopUnlocked on` brings the vanilla list back; the belts' prices and names look right; a second client sees the same list.

### Phase N4 - Rare spawns and the Backpack
- N4.1 `RareNpcSpawns` (a GlobalNPC `EditSpawnPool` for the tied Mechanic under D15, `NPC.rarity` for the Lifeform Analyzer), the `savedMech` watcher (S2), `/fstory spawn mechanic|backpack` and `/fstory dump` debug tools, a minimal `HerBackpackNPC` (stands, rare spawn through `SpawnChance`, no interaction yet).
- Tests (new world): no tied Mechanic in the dungeon; `/fstory spawn mechanic` and `/fstory spawn backpack` place them next to the player; with the conditions met and the weights raised they appear on their own (Mechanic in the Cavern layer, Backpack in the dungeon after Skeletron); the Lifeform Analyzer shows both; at most one of each exists; the Mechanic can be rescued and becomes the town NPC; `MechanicRescued` turns on and she never spawns tied again; on a dedicated server with two clients.
- N4.2 Backpack interaction and `MechanicsBagItem`: chat window "Looks dirty and full of wires" with the buttons "Take it" and "Close", the `TakeBag` request and result, and the bag-loss watcher (see "Confirmed by Ismail", `BackpackRespawnDelayTicks`): it clears `BackpackTaken` so the Backpack can spawn again.
- Tests: the Bag arrives once, the Backpack disappears for every client, a second player cannot take a second Bag, quest tooltip, lava does not destroy the Bag (V9), reload keeps `BackpackTaken`, dropping the Bag in lava or a void and waiting makes the Backpack spawnable again.

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
- N8.1 The Skeletron handler (S5): it sets `DungeonFork`, which moves the tied Mechanic's spawn rule to the dungeon (D18) and switches the Backpack rule and the Dryad story off. Nothing is removed or replaced.
- Tests: kill Skeletron with `EyeKillSeen` false: `DungeonFork` turns on, the tied Mechanic stops spawning in the Cavern layer and can spawn (rare) in the dungeon, rescuing her unlocks the full shop at once and the Dryad story stays off; killing the Eye first and then Skeletron changes nothing; Skeletron killed twice does nothing the second time; the fork when the Bag is already taken.

### Phase N9 - Hardening
- Tunables moved to a config, localization pass (Ismail's text), remove `/fstory` and the test entry, dedicated server plus two clients full run of both routes, join sync with a world in the middle of the story, update this document.

## 8. Tunables (names are final in the patches)
`MechanicSpawnWeight`, `DungeonMechanicSpawnWeight`, `BackpackSpawnWeight`, `RareNpcRarity`, `InteractReachTiles`, `DialogueExitCheckTicks` (30), `ConditionCacheTicks` (10), `ExclamationBobPixels`, `ExclamationOffsetY`, `RequireHouseForDryadExit` (true), `ScrollsPerPart` (1), `BackpackRespawnDelayTicks` (600).
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
- Q11. REPLACED by the rare-spawn model: there is no placement search. Which zones and bosses gate the two spawns is Q14.
- Q12. RESOLVED by Ismail: old worlds are ignored.
- Q13. Should the Dryad's dialogue 1 also start when she arrived by the vanilla route (Eater of Worlds or Brain of Cthulhu first) and the Eye was never killed? Default: no, the story starts at the Eye (so skipping the Eye means the dungeon route only).
- Q14. Rare spawn conditions and rarity (D15 to D18): which zones and bosses gate the tied Mechanic and Her Backpack, the spawn weights and the `NPC.rarity` values are guesses. Ismail tunes them in game with the tunables of section 8. Should Her Backpack also be allowed before Skeletron (dungeon entered some other way)? Default: no.

## 10. Verification list (checked in the local vanilla source before the patch that needs it)
Search the local decompiled vanilla source (Ismail's tModLoader source folder; the 1.3 decompile can mislead, prefer 1.4). Suggested searches: `BoundMechanic`, `BoundGoblin`, `savedMech`, `NPCID.Dryad`, `downedBoss1`.
- V1 (N4.1, BLOCKER) Two things. (a) REMOVED 2026-10-31 (no world generation cleanup any more). (b) Does a `BoundMechanic` created by the spawn pool behave like a tied NPC: tied AI, `homeless`, `direction`, rescue turns her into the town Mechanic and sets `NPC.savedMech`? Fallback for (b): copy the fields vanilla sets for the tied Goblin Tinkerer in a GlobalNPC `OnSpawn` (see V13).
- V2 (N4.1) The rescue code: does it set `NPC.savedMech`, does it call `Transform`, and is `savedMech` synced to clients.
- V3 (N6.1) Vanilla Dryad arrival rule (which boss flags), the arrival announcement call, and that a homeless spawned town NPC persists.
- V4 (N6.1, N8.1) When `OnKill` runs relative to the vanilla boss-defeated flags. Our own flags make this safe either way; this only confirms the hook fires on the server in single-player and multiplayer.
- V5 (N3.1) `NPCShop.Entry` members (`Disable`, `AddCondition`) and the Mechanic's shop name (expected `"Shop"`). RESOLVED for the members (public tModLoader 1.4.4 source): `Entry.AddCondition(Condition)`, `Entry.Disable()`, `NPCShop.Entries`, `TryGetEntry(int, out Entry)`, `Add(int, params Condition[])` and `new Condition(string key, Func<bool>)` exist. The shop name `"Shop"` is the tModLoader default and is checked in game by the N3.1 test (an empty or unchanged shop means the name differs).
- V6 (N2.1) `GlobalNPC.PostDraw` for the mark and where `Player.talkNPC` is set, including after the chat closes.
- V7 (N2.1) Whether `GetChat` can run more than once per conversation (it must stay free of side effects; the plan already keeps side effects in the talk-start edge).
- V8 OBSOLETE (the Backpack is a rare spawn and is no longer saved with the world).
- V9 (N4.2) Lava immunity for a quest-rarity item (`ItemID.Sets.IsLavaImmuneRegardlessOfRarity` or rarity rules). RESOLVED (public tModLoader 1.4.4 source): only items with rarity 0 (white) burn in lava unless the set marks them; the Bag is quest rarity and is also put in the set. Still checked in the N4.2 test.
- V10 (N3.1) Which belt item is Clockwise: resolved, `ConveyorBeltLeft`.
- V11 OBSOLETE (no dungeon search is needed any more).
- V12 (N4.1, N4.2) The Lifeform Analyzer: what does it read to list a creature (`NPC.rarity` above 0? also `friendly`, `townNPC`, `lifeMax`, a distance limit?), and what name does it show for a ModNPC? Search `accCritterGuide`, `LifeformAnalyzer`, `rarity` in the local `Main.cs` / `Player.cs`. If it skips friendly or town-type NPCs, N4.1 needs a workaround. Check whether vanilla tied NPCs already set a rarity.
- V13 (N4.1) `GlobalNPC.EditSpawnPool` with a friendly vanilla type: does `NPC.SpawnNPC` accept a `BoundMechanic` from the pool and spawn it with correct tied state, or must the fields be set by hand? Check `NPCSpawnInfo` (`SpawnTileType`, `Player`, `Sky`, `PlayerInTown`) for the zone tests, and that friendly types are not filtered out of the pool.
- V14 (N2.1) The chat button detour: the Guide's extra button appears and its click arrives in `PreChatButtonClicked(false)`; NPCs whose vanilla first or second button is already used (Dryad Status, Nurse Heal) keep it; vanilla Close stays. Tested with the Guide in N2.1.

## 11. Risks
- Rare spawns can be too rare or too common to feel right (tunables `MechanicSpawnWeight`, `BackpackSpawnWeight`, Q14) and a rare NPC can despawn before the player reaches it; the Lifeform Analyzer is the player's way to find them (V12).
- A tied Mechanic created by the spawn pool may lack the tied-NPC setup vanilla does at generation (V1b, V13); a friendly Backpack may be skipped by the Lifeform Analyzer (V12).
- (Removed 2026-10-31 together with the world generation cleanup.)
- Gating the vanilla shop entries can clash with other mods that edit the Mechanic's shop.
- A talk-start edge detection that misses (chat opened and closed within a tick) would skip a turn-in. The player can simply talk again.
- Losing the Bag or the scrolls can block progress (Q4, Q8). The debug commands cover testing only.
- Worlds with the Mechanic already rescued must not get a second tied Mechanic (her spawn rule checks `MechanicRescued` and `NPC.savedMech`).
- Dryad spawning without a house: she wanders to the world spawn like any homeless town NPC; the player has to house her.
- The Skeletron fork removes nothing now (it only moves the spawn rules); it only runs when the Mechanic was never rescued.

## 12. Progress

One line per patch: `[ ]` not started, `[~]` applied, untested, `[x]` tested and done.

- [ ] N0 Verification of the vanilla facts (V1 to V11, Ismail reads the local source)
- [~] N1.1 Story foundation (patch_npc_n1_1_story_foundation.ps1, applied, untested; Common/Story/StoryFlags.cs, StorySystem.cs, StoryCommand.cs, Common/Net/StoryNet.cs, packet StoryFlagChanged = 58. AUTHORITY: the server / single-player writes flags (`StorySystem.ServerSet`), clients only read. SYNC: join provider "story-flags" sends the whole set, StoryFlagChanged broadcasts each change, single-player applies directly with no packet. SAVE: world data key "Flags". `/fstory status | known | flag <name> [on|off]` is a World command, so in multiplayer it runs on the server. Tests: see the N1.1 reply. REVISED in patch N2.1: the flags MechanicPlaced and BackpackPlaced were removed (nothing is placed any more) and the temporary TestDialogueExit was added)
- [x] N2.1 Important dialogue framework (patch_npc_n2_1_important_dialogue.ps1 plus patch_npc_n2_1_fixes.ps1, TESTED and working by Ismail 2026-10-30: the Merchant test button, the flag exit, the "!" art and the Guide's full button slots are resolved or understood; Common/Story/ImportantDialogue/*.cs. AUTHORITY: the server / single-player latches `Done.<Id>` when an entry's Exit is true (checked every `DialogueExitCheckTicks`); clients only read flags and run Condition, the chat text, the chat buttons, the "!" mark and OnTalkStart locally. SYNC: no new packet, the Done flags travel with the N1.1 flag sync. Chat buttons: `NPCLoader.SetChatButtons` is detoured on clients (tModLoader has no GlobalNPC.SetChatButtons) and clicks arrive in `PreChatButtonClicked`. Two TEST entries on the Guide (Torch = low priority, Wood = high priority with a button), removed in N9; new flag TestDialogueExit; `/fstory dialogue`. Tests: see section 7, Phase N2. FIX NOTES (after first test): `/fstory flag` now cleans the typed name and matches ignoring case (`StoryFlags.Resolve`) and prints char codes when unknown; Exclamation.png is looked up at several paths and the result is logged; a chat button that finds both vanilla slots full is logged (the Guide probably uses Help and Crafting, so its test button cannot show); Test.Merchant added to test the button on a free slot. OPEN: decide how story buttons get a slot on NPCs whose vanilla buttons fill both slots)
- [x] N3.1 Mechanic shop (patch_npc_n3_1_mechanic_shop.ps1, TESTED by Ismail 2026-10-31 ("everything works"); Common/Story/MechanicShopGlobalNPC.cs. AUTHORITY: none needed, `GlobalNPC.ModifyShop` runs once at load on every side, each vanilla entry of the Mechanic's "Shop" gets the condition `MechanicShopUnlocked` (entries are gated, not deleted) and the two belts `ConveyorBeltLeft` / `ConveyorBeltRight` are added with no condition. The conditions run on the client when the shop opens and only read the synced flag. NO NEW PACKET. Tests: see section 7, Phase N3. Needs a Mechanic NPC to test: `/fstory spawn mechanic` comes in N4.1)
- [x] N4.1 Rare spawns (patch_npc_n4_1_rare_spawns.ps1, TESTED by Ismail 2026-10-31 ("everything works": the tied Mechanic and Her Backpack both spawned; the Mechanic spawned before the Eye of Cthulhu was killed, see the open decision in the next chat prompt); the PostWorldGen cleanup it contained was removed in N4.2; Common/Story/RareNpcSpawns.cs, Content/NPCs/HerBackpack/HerBackpackNPC.cs, StoryCommand additions. AUTHORITY: the server / single-player runs every rule: `GlobalNPC.EditSpawnPool` adds `NPCID.BoundMechanic` (D15, weight `RareNpcRules.MechanicSpawnWeight`), `HerBackpackNPC.SpawnChance` (D16) and the `savedMech` watcher (S2, sets `MechanicRescued`). Clients only see the NPCs through the vanilla NPC sync. SYNC: no new packet. `NPC.rarity` is raised to `RareNpcRarity` for the tied Mechanic and set on the Backpack so the Lifeform Analyzer can list them (V12, to confirm in game). The Backpack has no interaction yet (N4.2) and uses a vanilla item icon until `Content/NPCs/HerBackpack/HerBackpack.png` exists. New debug commands: `/fstory spawn mechanic|backpack`, `/fstory dump`, `/fstory weight <name> <n>`. OPEN verification, answered by the tests: V1b and V13 (does a spawned tied Mechanic behave like a tied NPC and can be rescued), V2, V12 (Lifeform Analyzer))
- [x] N4.2 Her Backpack interaction and The Mechanic's Bag (patch_npc_n4_2_backpack_bag.ps1, TESTED by Ismail 2026-10-31 ("everything works"); Content/Items/Story/MechanicsBagItem.cs, StoryNet additions, HerBackpackNPC chat, RareNpcSystem bag-loss watcher, `/fstory bag`. AUTHORITY: the server decides. Right click on Her Backpack opens the chat ("Looks dirty and full of wires.", button "Take it"); the button sends `StoryActionRequest` (kind TakeBag, NPC index); the server checks the NPC is a live Her Backpack, the player is within `StoryNet.InteractReachTiles` and neither `BackpackTaken` nor `MechanicShopUnlocked` is set, then sets `BackpackTaken`, gives ONE `MechanicsBagItem` (`QuickSpawnItem`), deactivates the Backpack (NPC sync) and answers with `StoryActionResult`. SYNC: two new packets (59 request, 60 result); the flag uses the N1.1 sync. Single-player runs the handler directly. The bag-loss watcher (`RareNpcSystem`, server only) clears `BackpackTaken` when no player inventory / bank / trash and no item on the ground holds the Bag for `BackpackRespawnDelayTicks` (600); a Bag in a chest counts as lost. The Bag is a quest-rarity item, lava immune (V9 resolved: vanilla only burns white-rarity items). REMOVED in this patch (Ismail: the Mechanic is an NPC, nothing is generated in the world): the `PostWorldGen` cleanup of the vanilla dungeon tied Mechanic (M1, D19, V1a). Needs art: `Content/Items/Story/MechanicsBagItem.png` (item icon, any size) and `Content/NPCs/HerBackpack/HerBackpack.png` (the NPC, one frame; hitbox is `HerBackpackNPC.BackpackWidth` x `BackpackHeight`, 28 x 32). Tests: see section 7, Phase N4)
- [~] N4.3 Story routes (patch_npc_n4_3_story_routes.ps1, applied, untested; Ismail's notes after the N4.2 test; Common/Story/RareNpcSpawns.cs, HerBackpackNPC.cs, `/fstory dump`). ROUTE A: the tied Mechanic only spawns underground once the Eye of Cthulhu was defeated (`NPC.downedBoss1`, vanilla's flag); Her Backpack only spawns after she was rescued (`MechanicRescued`). So: Eye, rescue (shop incomplete), Skeletron, dungeon, Backpack, Bag, shop unlocked. ROUTE B: if Skeletron falls while the Eye never did and she was not rescued, a server-side watcher in `RareNpcSystem` latches `DungeonFork` (this is the effect of rule S5, done as a watcher instead of an OnKill hook; N8.1 can build on it); then she spawns in the dungeon only, the Backpack never spawns, and her rescue unlocks the whole shop at once. Her Backpack's chat has no Happiness button (`NPCID.Sets.NoTownNPCHappiness`, to confirm in game). NO NEW PACKET: the rules run on the server and `DungeonFork` travels with the N1.1 flag sync)
- [ ] N5.1 Bag turn-in and full shop unlock
- [ ] N6.1 The Dryad (Eye of Cthulhu arrival, dialogues 1 and 2)
- [ ] N7.1 Super weapon intro and the three scrolls
- [ ] N8.1 Skeletron first fork
- [ ] N9 Hardening, remove debug, dedicated server test

Next chat prompt (after N4.3 applied): "N2.1 to N4.2 are tested and done. N4.3 (story routes: the Eye gate for the underground Mechanic, the Backpack only after her rescue, the Skeletron-first fork latch, no Happiness button on Her Backpack) is applied; test results: <paste, include: no tied Mechanic underground before the Eye is defeated, she spawns after it, the Backpack does not spawn before she is rescued, route B (Skeletron first) latches DungeonFork, she then spawns only in the dungeon, the Backpack never spawns and her rescue unlocks the full shop at once, and whether the Happiness button is gone on Her Backpack>. Record the results with a small patch first (do not guess results I did not give), then implement patch N5.1 (the Mechanic.Bag important dialogue and the TurnInBag request) of the NPC story roadmap (docs/npcs/ROADMAP.md). Decisions are in section 1 (Confirmed by Ismail) and section 9. State the authority/sync design first. Open decision to settle first: how a story chat button gets a slot on an NPC whose vanilla buttons fill both slots (the Guide uses both; check the Mechanic's)."