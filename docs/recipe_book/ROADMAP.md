# Recipe book roadmap (tab discovery, new-recipe highlights, tab and favorite filters)

Status: R0 to R3 tested and working (incl. R1.6 to R1.8); R4 and R5 dropped; R6 and R1.9 applied (untested); R7 applied (untested). Written from Ismail's notes of 2026-11-16, on top of patch `patch_vanilla_station_recipes.ps1` (3.2b in docs/multiplayer/ROADMAP.md: vanilla station tabs, crafted-at-any-station rule, Decay Chamber in the Default pool). That patch must be applied first.

Code map: `Common/Knowledge/` (RecipeCatalog, RecipeKnowledgeSystem, VanillaStationBooks, ScrollPools), `Common/Machines/RecipeVisibility.cs` (states), `Common/UI/RecipeBookUI/` (RecipeBookState, RecipeBookElements, RecipeBookPlayer, RecipeBookLayout), `Common/Net/RecipeKnowledgeNet.cs`.

## Progress

One line per patch. `[ ]` not started, `[~]` applied untested, `[x]` tested and done.

- [x] R0 Tab names without "(Vanilla)", machine and vanilla twin tabs merged (tested)
- [x] R1 Tab discovery (a tab unlocks when its station or machine item is held or the station is near; tested)
- [x] R1.1 Tab discovery tweak (machines inherit station tabs; By Hand first; station sprites and hover tip; tested)
- [x] R1.2 R1.1 follow-up (detail page names only discovered stations and wraps; sprite cycling only held items; a parchment discovers its machine; tested)
- [x] R1.3 R1.2 follow-up (Hellforge takes over Furnaces; discovered stations only in the tip; tip text centered; a discovered machine discovers its inherited stations; no search bar on an empty book; tested)
- [x] R1.4 Furnace tab folded into Hellforge's (one tab; one entry per product; recipes merged across machines with a fuel row per machine; tested)
- [x] R1.5 Fuel rows follow discovery (a machine's fuel row only once that machine is discovered; "or" between rows; one "Fuel" label on top; tested)
- [x] R2 New-recipe highlight (per-character viewed set seeded once per world after the join sync; pulsing gold row + "!" after the name for new recipes and new tabs; a folded tab shows "!" when a recipe inside is new; closing the book or selecting a recipe clears; machine tab rows now a darker brown band with a warm hover; tested)
- [x] R2.1 R2 follow-up (machine tab hit area and band 4 px taller on top; "!" offsets are tunables `RecipeBookNew.EntryMarkOffset` / `TabMarkOffset`; a tab keeps its "!" while any recipe in it is new, open or folded; the vanilla inventory ignores the cursor while it is inside the book (`PlayerInput.IgnoreMouseInterface` around the inventory layer); tested)
- [x] R2.2 R2.1 fix (`PlayerInput.IgnoreMouseInterface` is read-only, so the vanilla inventory is blocked another way: the mouse buttons are cleared for the inventory layer while the cursor is inside the book, then restored, and the inventory's hover tooltip and cursor override are wiped; tested)
- [x] R1.6 Heavy Work Bench gets its own tab (Work Benches and Heavy Work Bench are two tabs, each discovered by its own item or tile, until the Mechanical Assembler is discovered; then they merge into its tab; one-time world migration keeps tabs the world crafted in; characters that viewed the old merged tab see nothing new; Autohammer / Anvils unchanged; tested)
- [x] R1.7 Detail page station block (one row per alternative station with its sprite; recipes that need several stations show "[sprite] A & [sprite] B" wrapped on one row; vanilla conditions after the stations; own PNG for a station with no item, Demon Altar; tested)
- [x] R1.8 Tabs ordered by unlock sequence (per character and world, By Hand first; a merged tab takes the earliest place of its stations; tested)
- [x] R3 "New machine discovered!" pop-up (client that made the discovery only; local CombatText, no sound; one text for machines and stations; works with the book closed; once per discovery, not after relog; tested)
- [-] R4 Show/hide each tab (dropped by Ismail 2026-11-16)
- [-] R5 Favorites-only / show-all button (dropped by Ismail 2026-11-16)
- [~] R1.9 Fuel options side by side (the fuel cells of the machines that make a recipe sit in one row to the right of the arrow with "or" between them; the recipe slides left when there is no room; applied, untested)
- [~] R6 Recipe book button outline for new recipes (the book button gets a white outline PNG, tinted with a slowly cycling hue, while anything in the book is new; applied, untested)
- [~] R7 Rows / Grid mode for the left page (a button right of the search bar switches between today's rows and small item squares; the mode is saved per character; Left Alt favorites in both modes with the vanilla favorite cursor; applied, untested)

## Working rules

1. One chat = one patch, delivered as a PowerShell patch script (see PROJECT_CONTEXT). State the authority/sync design first (multiplayer first).
2. World knowledge (what the world has discovered) is server-owned and synced. Personal UI state (what this player has looked at, hidden tabs, favorites-only) is per character, client-only, saved in `RecipeBookPlayer`, never synced (same rule as favorites today).
3. Existing worlds and characters must migrate silently: nothing may suddenly turn "new", and no tab may vanish from a world that already crafted in it.
4. Art: Ismail makes all art. Highlights and pop-ups are drawn in code; the only art needed is R6 (see there).

## Decisions taken so far (from the notes)

- Drop " (Vanilla)" from the station tab names.
- A tab only exists in the book once it is discovered; discovery is "unlocked" the same way recipes are (world-wide).
- Pop-up text for a new tab, highlight for a new recipe, a book-icon outline while anything is new, a way to hide tabs, a favorites-only toggle.

## R0 Tab names without "(Vanilla)"

Change: `RecipeBookLayout.MachineName` stops appending " (Vanilla)"; hjson `Machines.VanillaHand` becomes "By Hand". Tiny, no sync.

Collision decision (taken 2026-11-16): option (c), merge. A machine and its vanilla twin tile share ONE tab under the machine's name, machine recipes first, then the station's recipes for products the machine does not list (one entry per product; the crafted-anywhere rule already treats them as one). Display only: catalog keys and saves are unchanged. The pairs live in `Common/Knowledge/RecipeTabDefinitions.cs` (`Twins`): Furnace + Furnaces, Hellforge, Solidifier, Ice Machine, Liquid Distillator + Imbuing Station, Decay Chamber + Lesion Station, Autohammer. Mechanical Assembler and Anvils are not twins (they import another tile's recipes) and stay separate. `RecipeCatalog.TabOf(machine)` gives the tab; `RecipeBookState` uses it for headers, folding, order and the detail title. A station recipe left out of a merged tab maps to the machine's entry (`aliases`), so jumps and saved selections still land. A favorite stored on a left-out station key is not shown (the machine entry of the same product is the one to star). R1 should discover a merged tab as one id.

## R1 Tab discovery

Rule: a tab (a machine or a vanilla station) is hidden until the world discovers it. "By Hand" is always discovered. Recipes inside a discovered tab keep today's rules (Crafted, parchment, faded by held ingredient).

How a tab is discovered (any one of these):
1. The player holds the tab's item at least once (inventory scan, same as seen ingredients). Machines: the machine's item. Vanilla stations: every item whose `createTile` is the station tile (computed from ContentSamples, so no hand-written list; Anvils = Iron and Lead Anvil, etc.).
2. The player stands near the station. Use vanilla's crafting range (`Player.adjTile[tile]`, the same test the game uses to allow crafting there) instead of the exact hitbox: "you could craft here" is the natural meaning of discovering a station, it works for big and odd shapes, and it is the only way for stations that have no item (Demon Altar, Crystal Ball) and for ones found in the world (Hellforge in the Underworld, furnaces in houses). Recommended: yes, near counts.
3. (Removed by R1.1: a crafted or unlocked recipe no longer discovers its tab.)

Sync design (multiplayer first): discovery is world knowledge, owned by the server, like the seen set. Reuse the seen-set encoding (liquids already ride in it as negative markers): a tab marker is a separate negative range (e.g. `-(1000000 + stationIndex)`), so it is saved, joined and broadcast by the existing `SeenReport` / `SeenDelta` / join provider with no new packet. Clients report; the server validates the marker is a real tab and (for the near rule) accepts it only while the reporting player really is near such a tile (server checks its own tile data), rate-limited like seen reports. Single-player short-circuits as today.

Needs: a registry `RecipeTabDiscovery`: tab name -> item types and tile ids. Vanilla stations filled automatically in `VanillaStationBooks`; machine tabs listed by hand in one config class in the style of `ScrollPoolDefinitions` (the file you edit).

Migration: on load, every tab that already has a crafted or unlocked key is treated as discovered (computed, not saved), so old worlds lose nothing.

Side effect worth having: this removes most of the "holding Wood shows hundreds of faded Work Bench recipes" clutter, because the Work Bench tab (and its faded recipes) stay hidden until you hold or stand near a Work Bench. The open question from 3.2b (require ALL inputs seen) can then probably stay closed.

Open questions: should a discovered machine tab also reveal its vanilla twin (Furnace machine and Furnaces tile share the item/tile)? Default yes, they share the trigger.

Test checklist (to refine when coded): new world: only machine tabs you have held and "By Hand" show; pick up a Work Bench: its tab appears; stand next to a Demon Altar: its tab appears; old world: tabs with crafted recipes are still there; two clients: one discovers, the other's book gets the tab within a second; late joiner has it; restart keeps it.

Implemented (R1, applied, untested): new `Common/Knowledge/RecipeTabDiscovery.cs` (the table, the local scan, the server check) and a `RecipeTabDiscoverySystem` that scans the local player every `ScanTicks` (30). Tab ids are stable, never an index: a vanilla station (or a machine's vanilla twin) uses its TileID, a machine on its own tile uses `ExtraIdBase` (100000) plus its position in `RecipeTabDefinitions.ExtraMachineTiles` (append only). Marker = `RecipeKnowledgeSystem.TabMarker(id)` = `-(1000000 + id)`, saved as `SeenTabIds` (liquids keep `SeenLiquidIds`). Items come from `ContentSamples` (every item whose `createTile` is the tab's tile). "Near" = vanilla's `adjTile` flag OR a tile scan of `NearTilesX/Y` (4 x 3) around the player; the server accepts a marker only when its own copy of the sender holds one of the tab's items or a matching tile is within that range plus `ServerNearExtra` (3). Rule 3 (a crafted or unlocked recipe discovers its tab) uses the raw saved sets, not the crafted-anywhere rule, but note the "obtained items count as crafted" server scan: holding a product (an Iron Bar) marks its recipes crafted in every tab that makes it, which then discovers those tabs. Also fixed here: a host-and-play host's own seen reports were never broadcast (`ReportSeen` now sends a `SeenDelta` when the process is the server). Debug: `/recipes tabs`, `/recipes tabs forget`. The book skips undiscovered tabs in `RecipeBookState.Rebuild`; `FindBookRecipeFor` skips them so an ingredient jump never targets a hidden tab.

## R1.1 Tab discovery tweak

Decided 2026-11-16. Applied, untested.

- Rule 3 changed: crafting a recipe never discovers a tab (obtaining a product would reveal every station that makes it). A parchment-unlocked recipe discovers exactly the machine/station its key names (`Hit` in `RecipeTabDiscovery`, computed from `UnlockedKeys`, never saved). Otherwise only holding the station/machine item or standing within crafting reach does. Old worlds: no tab marker was ever migrated, so a tab that was only visible through rule 3 comes back the next time the player holds or stands near its station (open question: add a one-time migration if that annoys).
- "By Hand" is always the first tab (`RecipeBookState.Rebuild`, order -1).
- Inheritance: `RecipeTabDefinitions.Inherits` lists the machines that import recipes from a different vanilla tile (Autohammer: Anvils; Mechanical Assembler: Work Benches, Heavy Work Bench). `RecipeCatalog.TabOf` folds that station's tab into the machine's tab (twins win, so Furnaces stays with the Furnace; Hellforge also imports Furnaces but is not listed). Each inherited station has its own discovery marker (its TileID, the same marker the old standalone tab used). A tab is shown when its own machine OR any inherited station is discovered.
- Title: until the machine itself is discovered the header is titled after the discovered inherited station(s) ("Anvils", "Work Benches / Heavy Work Bench"); after that it is the machine's name. A sprite sits left of the title: the discovered machine/station item, cycling every `RecipeBookHeaderRow.IconSeconds` (1) when several items place the tile (Iron Anvil, Lead Anvil). Hovering the title of a machine whose own tile is discovered shows "Inherits from [icon] Anvils" (`RecipeBookTabTip`, drawn by `RecipeBookTabTipOverlay`).
- Detail page: the line under the product name lists every station of the tab that makes the product, joined with "or" (`RecipeCatalog.StationsFor`): "Autohammer or Anvils" when the Anvils make it too, just "Autohammer" for its own recipes. Names come from `Machines.<name>` in the hjson, so "Anvils" can be renamed there.
- Multiplayer: no new packets. Inherited stations use the existing tab markers; `ServerAccepts` already works per tile.

R1.2 (follow-up, applied, untested):
- Detail page: the "Autohammer or Anvils" line names only discovered stations (`RecipeTabDiscovery.IsOwnDiscovered`); if none of them is discovered yet it names all. It wraps under the product name and pushes the divider and the recipe body down (`StationLineStep`, 20 px per extra line).
- Header and tip sprites cycle only through items the world has HELD: a third marker range `-(2000000 + item type)` in the seen set (`RecipeKnowledgeSystem.StationItemMarker`), saved as `SeenStationItems` by item name, reported by the same scan, accepted by the server only if the sender holds the item (`ServerAcceptsItem`). A tab discovered without holding anything (stood near it, parchment) shows its first item.
- A parchment-unlocked recipe of a machine nobody found discovers that machine/station (so its tab appears). This reverses the first R1.1 rule; crafted recipes still never discover a tab.

Test checklist: new world, hold an Iron Anvil: the tab appears titled "Anvils" with the anvil sprite; hold an Autohammer (or stand next to one): it becomes "Autohammer" with the Autohammer sprite and the hover tip lists Anvils; a recipe on both shows "Autohammer or Anvils", an Autohammer-only recipe shows "Autohammer"; read a parchment for a machine you have not found: no tab appears; By Hand is first; Work Bench then Heavy Work Bench: title shows both; a second client gets the tab; relog keeps it.

## R1.3 R1.2 follow-up

Decided 2026-11-16. Applied, untested. No new packets: everything is computed from the markers the book already has (`Hit` in `RecipeTabDiscovery`), nothing new is saved.

- Hover tip "Inherits from [icon] X and [icon] Y" lists only stations that are discovered (`RecipeTabDiscovery.InheritedNames` now filters by `Hit`). None discovered: no tip. The machine itself not discovered: no tip, as before.
- Tip text is centered on the icon: `RecipeBookTabTipOverlay.InkCenterY` (8.5 px from the top of a drawn line at scale 1, copied from the header row's proven alignment) replaces `font.LineSpacing`. The font-metrics API was not verified, so this is a tunable, not a measurement.
- Hellforge takes the Furnaces station over, the same pattern as Mechanical Assembler and the Work Benches: `Inherit("Hellforge", TileID.Furnaces)`, and the `Twin("Furnace", TileID.Furnaces)` pair is gone. The Furnace machine keeps its own tab with only its own recipes (it no longer lists the vanilla Furnaces recipes). `RecipeTabDefinitions.DiscoveryTiles` keeps the Furnace tab discoverable through the Furnaces tile (same marker id = the tile, so old worlds keep it); the Hellforge tab's Furnaces station is a second `Tab` that shares that marker and its items (`sharedSubs` in `Build`).
- `Hit` of an inherited station is true when its owner machine is discovered (held, near, or unlocked by a parchment). So a parchment for an Autohammer recipe discovers the Anvils too, Mechanical Assembler discovers Work Benches and Heavy Work Bench, and Hellforge discovers Furnaces. Computed, never saved.
- The search bar is not built while the right page says "The pages are blank" (`IsEmpty`); `Rebuild` rebuilds it as soon as something is listed.

Test checklist: see the patch reply. Next chat: "Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md (R1 to R1.3 and the Progress list), then Common/Knowledge/RecipeTabDiscovery.cs and RecipeTabDefinitions.cs. State: R0 to R1.3 are applied; test results: <paste>. Log them in Progress, then implement R2."

## R1.4 One Furnace/Hellforge tab, merged recipes, fuel rows

Decided 2026-11-16. Applied, untested. Supersedes the Furnace bullet of R1.3 (the Furnace no longer has a tab of its own; the Hellforge no longer inherits only the vanilla Furnaces station). No packets, nothing saved.

- `RecipeTabDefinitions.InheritedTabs`: Hellforge inherits the whole Furnace tab. `Twin("Furnace", Furnaces)` is back, so the Furnace machine and the vanilla Furnaces station are one unit; the unit joins the Hellforge tab (`RecipeCatalog.BuildTabs`, `unitOf`, `InheritingTabs()`). `DiscoveryTiles` (R1.3) is gone. Until the Hellforge is discovered the tab is titled "Furnace" (the unit is discovered through the Furnaces tile; same marker id as before, so old worlds keep it); after that "Hellforge", with the tip "Inherits from [icon] Furnace". The detail line reads "Hellforge or Furnace" for products both make.
- One entry per product per tab (`RecipeBookState.Rebuild`): when several machines of a tab list the same product, the first machine's entry stays and the others become aliases of it (as stations already were).
- The right page cycles `RecipeCatalog.MergedRecipes(machine, group)`: the recipes of that product across every machine of the tab, identical ones (same inputs, liquids and outputs) merged into one entry that remembers which machines do it. A recipe only the Hellforge has stays its own page.
- Fuel: one fuel cell per machine that makes the shown recipe and has fuels, stacked beside the arrow (Furnace items cycling on top, Hellforge lava below; `fuelRowStep`). A Hellforge-only recipe shows only the lava; the arrow grows for the extra row.
- Not changed: the ingredient search (`#Name`) only looks at the kept entry's own recipes.

Next chat: "Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md (R1 to R1.4 and the Progress list). State: R0 to R1.4 are applied; test results: <paste>. Log them in Progress, then implement R2."

## R1.5 Fuel rows follow discovery

Decided 2026-11-16. Applied, untested. Client-only drawing, no packets, nothing saved.

- A machine's fuel row (R1.4) is drawn only when that machine is discovered (`RecipeTabDiscovery.IsOwnDiscovered`): the Hellforge's lava row appears when the Hellforge is discovered, not before; before that only the Furnace's row shows. If none of the recipe's machines is discovered (parchment of a machine nobody found), all rows show so the fuel is not lost.
- An "or" sits between two rows, centered on the slot column (`fuelRowStep` 60 leaves the gap).
- One "Fuel" label, centered above the top-most slot (`fuelLabelHeight` 18 px; the arrow is longer by that much whenever a recipe has fuel).

## R1.6 Heavy Work Bench gets its own tab

Decided 2026-11-16. Applied, untested. No packets; the only saved addition is a world flag.

- Config: `RecipeTabDefinitions.SplitUntilOwnerDiscovered` lists the owners whose `Inherits` stations stay separate tabs until the owner is discovered. Only `MechanicalAssembler` is listed. Autohammer / Anvils and Hellforge / Furnace are NOT listed, so they merge at once as before (decided 2026-11-16: Anvils stay as they are; add `"Autohammer"` to the list if that ever changes).
- `RecipeCatalog.TabOf` is no longer constant: a split station returns its own name until `RecipeTabDiscovery.IsOwnDiscovered(owner)`, then the owner's name. `RecipeCatalog.FullTabOf` is the old static answer; discovery ids and tables are built from it, so markers and `/recipes tabs` are unchanged. Never cache `TabOf` across frames.
- `RecipeTabDiscovery.IsDiscovered(owner)` ignores split stations, so discovering a Work Bench no longer shows the Mechanical Assembler's own recipes. Each split station is discoverable by its own marker (its tile id, as before).
- Folded tabs (`collapsedMachines`), viewed keys ("tab|Name") and the header's `childKeys` all use `TabOf`, so they follow the split. A stale folded entry for a station after the merge is harmless (the set lives only while the book object lives). The merged tab starts unfolded.
- New-ness: recipe keys do not depend on tabs, so merging lights up nothing that was already viewed (only the Mechanical Assembler's own recipes and its tab, which are genuinely new). A character that viewed the old merged tab ("tab|MechanicalAssembler") gets the two split tabs without a "!" (`RecipeBookNew.IsTabNew`).
- Migration (rule 3): a world saved before this patch had one merged tab. On load, once (world flag `TabSplitMigrated`), if that merged tab was visible, every split station the world already crafted in gets its own marker (`RecipeTabDiscovery.LegacySplitMarkers`, raw crafted keys), so no tab the world crafted in vanishes. Server / single-player only; clients get the markers through the join sync.
- Detail page: `StationsFor` / `MergedRecipes` / the fuel rows go through `TabOf`, so a split tab names only its own station and the "or" naming returns once merged.
- Sort: a merged tab sits where the Mechanical Assembler is in the catalog; a split tab sits where its station is, so the tab moves when the merge happens.

Test checklist: see the patch reply.

## R1.7 Detail page station block

Asked 2026-11-16. Applied, untested. Client-only drawing, no packets, nothing saved.

- Replaces the "Autohammer or Anvils" text under the product name (`RecipeBookDetail.StationBlock` / `DrawStationBlock`). It is now built for the recipe SHOWN (the pager cycles recipes), not for the whole product: each machine or station that makes that recipe is one row, `[sprite] Name`, an alternative always starts a new row. Only discovered stations are named (all of them if none is yet), as before (`RecipeCatalog.UnitOf` gives the name of a machine inside its tab).
- A vanilla recipe that needs several stations at once (the Watch: Tables and Chairs) is already listed in every station tab it needs (`ImportStationRecipes`), and machine books import by `requiredTile.Contains`. The row now reads `[sprite] Tables & [sprite] Chairs`: the tab's own station first, then every other required tile (`RecipeTabDiscovery.TilesOfTab` says which tiles the tab already stands for). The row wraps at a station, with the "&" kept on the end of the previous piece. Extra stations are always named even if undiscovered (you need them to craft).
- Conditions: `CustomRecipe.Source` now remembers the vanilla `Recipe` it was converted from (not saved or sent). Its `Conditions` descriptions (`Condition.Description.Value`: "Near water", "In the Graveyard", ...) are listed one per row after the stations. Hand-written machine recipes have no Source, so no extra stations and no conditions.
- Station art: `RecipeBookStationIcon`. A station with items shows the item (cycling); a station with none uses `Common/UI/CustomUIElements/StationIcons/StationIcon_<TileID name>.png` (Demon Altar: `StationIcon_DemonAltar.png`, about 20x20, bigger is scaled down). Used by the tab header and the station rows. Missing PNG = no sprite, no crash. The Demon Altar tab cycles `StationIcon_DemonAltar.png` and `StationIcon_CrimsonAltar.png` (same tile; `RecipeBookStationIcon.Variants` lists the cycle, same speed as item sprites, `RecipeBookHeaderRow.IconSeconds`); a missing one is skipped.
- Unverified API (not in the repo, check against the local tModLoader source when it builds): `Recipe.Conditions` (`List<Condition>`) and `Condition.Description` (`LocalizedText`).

Test checklist: see the patch reply.

## R1.8 Tabs ordered by unlock sequence

Asked 2026-11-16. Applied, untested. Client-only, nothing synced.

- Order: By Hand first, then the tabs in the order they first appeared in this character's book in this world (`RecipeBookPlayer.TabRank`, saved as `tabOrders`: one list per world key). `RecipeBookNew.PollTabOrder` records a new tab within a second of its discovery (book open or closed); `Rebuild` records too, so the list is never sorted with a missing tab.
- Why per character and not world-wide: the order is a personal UI choice like favorites and the viewed set (working rule 2), so it needs no packet and no world save. Two players see the tabs in the order each of them found them. A world-wide order would need an ordered, synced list; say so if you want it.
- Migration: a character that already has tabs in a world gets them in the old catalog order the first time (the real order was never recorded); everything found afterwards is appended.
- A tab whose rank is not recorded yet (for example before the join sync) sorts after the recorded ones, in catalog order. A merged tab (R1.6) takes the earliest rank of the stations that were its own tabs, so the Mechanical Assembler tab keeps the Work Benches' place.

## R1.9 Fuel options side by side

Asked 2026-11-16. Applied, untested. Client-only drawing. Replaces the stacked fuel layout of R1.4 / R1.5 (same rules for WHICH fuels show).

- One "Fuel" label above the first cell. The first machine's fuel cell (the Furnace) sits to the right of the arrow, then "or" to the right of it, vertically centered on the cell, then the next machine's cell (the Hellforge's lava). The arrow no longer grows with the number of machines.
- When the row does not fit between the arrow and the right edge of the page, the whole recipe below the station rows (ingredients, arrow, products, time) slides left by the missing amount (`FuelShift`, at most `fuelMaxShift`). The station block, product header and pager do not move.
- Tunables (instance fields at the top of `RecipeBookDetail`): `fuelBoxOffsetX` (arrow centre to the first cell), `fuelBoxOffsetY` (cells up/down), `fuelOrPadding` (space each side of "or"), `fuelOrOffsetY`, `fuelOrScale`, `fuelRightMargin` (how close the last cell may get to the page edge), `fuelMaxShift`, `fuelLabelHeight`, `arrowLength`, `fuelCycleTicks`.

## R2 New-recipe highlight

Per character, client-only, saved in `RecipeBookPlayer` next to favorites: a set of catalog keys the player has already looked at ("viewed").

- A recipe is NEW when it is listed in the book (`RecipeVisibility.InBook`) and its key is not in the viewed set.
- Drawn in code (no art): a soft highlight behind the entry row, fading in/out; also on a tab header when the tab itself is new (R1).
- The highlight clears when: the recipe is selected/viewed (click or opened by a jump), or the book is closed (every recipe that was listed at that moment is marked viewed).
- A recipe that becomes visible while the book is open shows the highlight at once and follows the same rules.
- Migration: the first time a character loads with this feature, seed the viewed set with everything currently in the book, so an existing save does not light up.
- Join in multiplayer: the first sync after joining must not mark everything new; seed after the join sync has been applied (same moment the migration seed runs on a fresh character).

Multiplayer: no new packets. The server only changes world knowledge; each client decides "new" locally from its own viewed set.

Decision (confirmed 2026-11-16): closing the book marks every recipe that was listed at that moment as viewed, even entries that were never scrolled into view.

Look (Ismail's notes, applied in R2):
- Machine tab rows on the left page are a slightly darker, browner band than the page (`RecipeBookLayout.HeaderTint`); the hover highlight is a warm light (`HeaderHoverColor`) so it reads on that band.
- A new recipe is a temporary background colour like a favorite (gold, pulsing; `RecipeBookNew.RowColor`) plus a "!" at the right edge of the name. A new tab does the same on its row. A folded tab shows only the "!" when something inside is new.
- Art (Ismail): `Common/UI/CustomUIElements/RecipeBookNew.png`, the "!". Drawn at native size, vertically centered, right after the name (entry rows are 36 px tall, tab rows 28 px, so keep it at most about 24 px tall). Until it exists a code-drawn red "!" is used.

How it works: `RecipeBookPlayer` keeps the viewed set (catalog keys and "tab|Name" keys) and the worlds it was seeded for, saved as `viewedKeys` / `viewedWorlds`. `RecipeBookNew.TrySeed` runs from `PostUpdate` after `JoinSync.Completed` (always true in single-player) and, the first time this character sees this world (`Main.ActiveWorldFileData.UniqueId`), marks everything already in the book as viewed. `IsNew` is false until that ran. Selecting an entry, clicking a tab row, or closing the book marks viewed. No packets.

Test checklist: a parchment unlocks a recipe: opening the book shows it highlighted; clicking it clears only that one; closing and reopening clears the rest; a recipe unlocked while the book is open lights up; old character shows no highlights on first load; a second player's viewed state is separate.

## R3 "New machine discovered!" pop-up

When a tab is discovered (R1), show text above the player ("New machine discovered!"; for a vanilla station "New station discovered!", or one text for both, your call) with `CombatText.NewText` on the client. Discovery is world-wide, so the question is who sees it:
- Default: only the client that triggered the discovery (it reports it, so it knows), everyone else just gets the new tab (highlighted header).
- Alternative: every client shows it when the delta arrives.
No sound by default; a short one can be added. Needs a localization key in `RecipeBook` in the hjson.

Test: pick up a Hellforge: the text appears once, not again after relog; a second client does/does not see it per the chosen rule; discovering a tab while the book is closed still shows the text.

Implemented (R3, applied, untested; decisions 2026-11-16: only the discovering client, no sound, one key):
- Authority/sync: no packets. The client that reports a tab marker (`RecipeTabDiscovery.Report`, called by the local scan) knows the marker was not in its seen set yet, so it queues the text. Everyone else just receives the marker as a `SeenDelta` and never passes through `Report`. Single-player and a host-and-play host behave the same (the host's own scan). A dedicated server never shows anything.
- Once: the marker is saved in the world (`SeenTabIds`), so a relog finds it already seen and nothing is queued. The scan (and so the pop-up) waits for `JoinSync.Completed`, otherwise a joining client would see its own already-discovered tabs as new before the join sync arrived.
- Text: one key, `Mods.Factorraria.RecipeBook.NewTabDiscovered` ("New machine discovered!"), the same for machines and vanilla stations (decided 2026-11-16: the same text for stations). Shown with `CombatText.NewText` above the player (local only, no sound), at most one per scan (`ScanTicks`, 30) so several tabs found at once do not overlap. Tunables: `PopUpColor`, `PopUpDramatic`. Works with the book closed (it does not touch the UI).
- Not covered: a tab revealed by a parchment (computed, no marker) or by a machine discovering its inherited stations shows no text; a tab migrated by R1.6 shows no text; two clients that find the same tab within the sync delay each see the text once.

Next chat: "Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md (R1.6, R2 to R2.2, R3 and the Progress list). State: R0 to R1.5 are tested; R1.6, R2, R2.1, R2.2 and R3 are applied; test results: <paste>. Log them in Progress, then implement R4 (show/hide each tab; state the authority/sync design first)."

## R4 Show/hide each tab

Per character, client-only: a set of hidden tab names in `RecipeBookPlayer`. Hiding is different from folding (the arrow toggles folding; hiding removes the tab from the list).
- UI: a small tabs button beside the search bar opens a list of discovered tabs with a checkbox each (code-drawn, like the existing elements), plus "show all".
- A hidden tab does not count for the "new" highlight or the button outline (R2, R6), unless it has a never-viewed new recipe? Decision: hidden tabs never highlight.
- Search still searches hidden tabs (so nothing is unreachable); results show their tab header. Decision to confirm.

Test: hide a tab, reopen the book (still hidden), search a product from it (found), show all.

## R5 Favorites-only / show-all button

A toggle beside the search bar: "all recipes" / "favorites only". Per character, remembered. In favorites-only mode the list shows favorited recipes grouped under their tabs; a tab with none disappears; hidden tabs (R4) are ignored in this mode because the player asked for favorites explicitly (decision to confirm). Empty state text: "No favorites yet."

Test: star a few recipes, toggle, scroll, search inside favorites, relog (remembered).

## R6 Recipe book button outline

`RecipeBookButton` draws an outline while anything is new (a new recipe from R2 or a new tab from R1) and the book is closed; it goes away when the book is opened and everything has been cleared by R2's rules (that is, after the book is closed again, or immediately if you choose "clear on open"; default: stays until the new items are cleared).

Art needed (yours): `Common/UI/CustomUIElements/RecipeBookButton_New.png`, 36x36 (same size as `RecipeBookButton.png`), a WHITE outline on a transparent background, drawn over the normal button. Colour (Ismail, 2026-11-16): the code tints the white outline and cycles its hue slowly through all RGB colours (HSV hue sweep, full saturation; the period is a tunable, not const). Until the PNG exists the code falls back to a code-drawn border with the same hue cycling (use `ModContent.RequestIfExists`, no crash). Same "new" rule as the "!" marks of R2: the outline shows while anything is new (a new recipe or a new tab, from `RecipeBookPlayer.IsNew`).

Implemented (R6, applied, untested). Client-only drawing, no packets, nothing saved.
- Art (Ismail's file name): `Common/UI/CustomUIElements/RecipebookIcon_Discovered.png` (note the lowercase b in "Recipebook"; the path is `RecipeBookArt.DiscoveredPath`). White outline on transparent, same size as the button; drawn over the button, stretched to the button rectangle, tinted by `RecipeBookNew.OutlineColor()` (hue sweep, `OutlineHuePeriod` seconds, default 6). Missing PNG: a 2 px border with the same colour.
- Rule: `RecipeBookNew.AnyNewInBook()`: any key the book lists that is new (recipes by `IsNew`, tabs by `IsTabNew`, so the R1.6 legacy rule and non-discoverable tabs behave like the "!" marks). It looks again only when the knowledge, seen, held or viewed versions changed (`RecipeBookPlayer.ViewedVersion`), or once a second. It stays on while the book is open and goes out as the new items are cleared (a click clears one, closing the book clears the rest). Not shown before the join sync (`ViewedReady`).
- The button only exists while the inventory is open, so the outline is seen there.

Test: unlock a recipe with the book closed: outline shows; open: outline stays until cleared per the rules; hidden tab recipes do not light it (R4 was dropped, so ignore that part).

## R7 Rows / Grid mode

Asked 2026-11-17. Applied, untested. Client-only, no packets, one saved bool.

Authority/sync: the mode is a personal UI choice like favorites and the viewed set (working rule 2): `RecipeBookPlayer.GridMode`, saved as `bookGridMode` (written only when true, so old characters load in Rows). Nothing is synced, the server never knows. Single-player behaves the same.

- Button (`RecipeBookModeButton`, `RecipeBookGrid.cs`): right of the search bar, its right edge on the scrollbar's right edge (`RecipeBookLayout.ModeButton`; the search bar got `ModeButtonSize + ModeButtonGap` narrower). Hidden while the book is empty. Art, all in `Common/UI/CustomUIElements/`: `SwitchRecipeBookMode_Rows.png`, `_Grid.png`, `_Rows_Highlight.png`, `_Grid_Highlight.png`, 24x24 (`ModeButtonSize`; drawn at native size, centered; a missing PNG draws a code glyph). The button shows the art of the mode a click switches TO (Rows mode shows the Grid art); `RecipeBookLayout.ModeIconShowsTarget = false` shows the current mode instead. Hover text keys `Mods.Factorraria.RecipeBook.ModeToRows` / `ModeToGrid` are registered in code with defaults (add them to the hjson to translate).
- Grid (`RecipeBookGridCell : RecipeBookEntry`): the squares flow left to right then down, as many per row as fit the list width (`RecipeBookLayout.GridColumns`, centered by `GridLeft`), restarting under every tab header. Same sort as rows. Icon only; hover shows the name or "???" while faded. A new recipe: pulsing gold square plus the "!" at its top right (`GridMarkOffset`). A favorite: `RecipeBookStar_On` at the top left, scaled by `GridStarScale` (`GridStarPad`), not clickable. Selection frame, search, folding, pinned headers, scroll and the scrollbar height, jumps (Select / scroll-to) all go through the same code as rows; `RecipeBookState.entryHeight` is the height of one entry in the built mode. Switching mode rebuilds on the next Update and scrolls the selected recipe back into view. An art entry (no product item) shows its PNG shrunk into the square.
- Favorite key (rows and grid): holding `Main.FavoriteKey` (Left Alt) over an entry turns the cursor into `CursorOverrideID.FavoriteStar` (what the vanilla inventory does in `ItemSlot.OverrideHover`) and a click toggles the favorite WITHOUT selecting. Without the key a click selects as before; in Rows the star is still clickable.
- Tunables (`RecipeBookLayout`, not const): `GridCellSize` 36, `GridGap` 4, `GridIconInset` 4, `GridStarScale`, `GridStarPad`, `GridMarkOffset`, `GridCellFill`, `GridCellBorder`, `ModeButtonSize`, `ModeButtonGap`, `ModeIconShowsTarget`.

Next chat prompt: "Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md (Progress list and R7), then Common/UI/RecipeBookUI/RecipeBookGrid.cs, RecipeBookState.cs and RecipeBookLayout.cs. State: R0 to R3 and R1.6 to R1.8 are tested; R1.9, R6 and R7 are applied; test results: <paste>. Log them in Progress first, then fix what failed."

## Order and dependencies

R0 any time. R1 first (it adds tab discovery, which R3, R4 and R6 use). R2 is independent of R1 but needs a new-tab flag from it for headers, so do it after R1. R3 after R1. R4 and R5 are independent UI patches. R6 last (needs R1 and R2).

## Next chat prompt

"Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md (R2, R3 and the Progress list), then Common/UI/RecipeBookUI/RecipeBookNew.cs, RecipeBookPlayer.cs and Common/Knowledge/RecipeTabDiscovery.cs. State: R0 to R1.5 are tested; R2 (new-recipe highlight, viewed set, tab band colour, the '!' art path RecipeBookNew.png) is applied; test results: <paste>. Log them in Progress, then implement R3 (state the authority/sync design first, then deliver a patch with the changed list, things to test, and a Progress edit)."