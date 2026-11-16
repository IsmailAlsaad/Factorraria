# Recipe book roadmap (tab discovery, new-recipe highlights, tab and favorite filters)

Status: R0 to R1.5 tested and working; R2 applied (untested); R3 to R6 are plan only. Written from Ismail's notes of 2026-11-16, on top of patch `patch_vanilla_station_recipes.ps1` (3.2b in docs/multiplayer/ROADMAP.md: vanilla station tabs, crafted-at-any-station rule, Decay Chamber in the Default pool). That patch must be applied first.

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
- [~] R2 New-recipe highlight (per-character viewed set seeded once per world after the join sync; pulsing gold row + "!" after the name for new recipes and new tabs; a folded tab shows "!" when a recipe inside is new; closing the book or selecting a recipe clears; machine tab rows now a darker brown band with a warm hover; applied, untested)
- [~] R2.1 R2 follow-up (machine tab hit area and band 4 px taller on top; "!" offsets are tunables `RecipeBookNew.EntryMarkOffset` / `TabMarkOffset`; a tab keeps its "!" while any recipe in it is new, open or folded; the vanilla inventory ignores the cursor while it is inside the book (`PlayerInput.IgnoreMouseInterface` around the inventory layer); applied, untested)
- [~] R2.2 R2.1 fix (`PlayerInput.IgnoreMouseInterface` is read-only, so the vanilla inventory is blocked another way: the mouse buttons are cleared for the inventory layer while the cursor is inside the book, then restored, and the inventory's hover tooltip and cursor override are wiped; applied, untested)
- [ ] R3 "New machine discovered!" pop-up
- [ ] R4 Show/hide each tab
- [ ] R5 Favorites-only / show-all button
- [ ] R6 Recipe book button outline for new recipes

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

Test: unlock a recipe with the book closed: outline shows; open: outline stays until cleared per the rules; hidden tab recipes do not light it.

## Order and dependencies

R0 any time. R1 first (it adds tab discovery, which R3, R4 and R6 use). R2 is independent of R1 but needs a new-tab flag from it for headers, so do it after R1. R3 after R1. R4 and R5 are independent UI patches. R6 last (needs R1 and R2).

## Next chat prompt

"Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md (R2, R3 and the Progress list), then Common/UI/RecipeBookUI/RecipeBookNew.cs, RecipeBookPlayer.cs and Common/Knowledge/RecipeTabDiscovery.cs. State: R0 to R1.5 are tested; R2 (new-recipe highlight, viewed set, tab band colour, the '!' art path RecipeBookNew.png) is applied; test results: <paste>. Log them in Progress, then implement R3 (state the authority/sync design first, then deliver a patch with the changed list, things to test, and a Progress edit)."