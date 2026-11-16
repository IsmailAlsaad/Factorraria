# Recipe book roadmap (tab discovery, new-recipe highlights, tab and favorite filters)

Status: PLAN ONLY. Nothing here is coded yet. Written from Ismail's notes of 2026-11-16, on top of patch `patch_vanilla_station_recipes.ps1` (3.2b in docs/multiplayer/ROADMAP.md: vanilla station tabs, crafted-at-any-station rule, Decay Chamber in the Default pool). That patch must be applied first.

Code map: `Common/Knowledge/` (RecipeCatalog, RecipeKnowledgeSystem, VanillaStationBooks, ScrollPools), `Common/Machines/RecipeVisibility.cs` (states), `Common/UI/RecipeBookUI/` (RecipeBookState, RecipeBookElements, RecipeBookPlayer, RecipeBookLayout), `Common/Net/RecipeKnowledgeNet.cs`.

## Progress

One line per patch. `[ ]` not started, `[~]` applied untested, `[x]` tested and done.

- [ ] R0 Tab names without "(Vanilla)"
- [ ] R1 Tab discovery (a tab unlocks when its station or machine item is held or the station is near)
- [ ] R2 New-recipe highlight
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

Collision to decide before coding: a machine tab and its vanilla tile tab can end up with the SAME label: Hellforge (machine) and Hellforge (vanilla), Autohammer and Autohammer, and Furnace / Furnaces, Anvils. Options: (a) accept it, the contents differ; (b) keep a suffix only on a collision; (c) merge the machine tab and its vanilla tile tab into one tab (machine recipes first; the crafted-anywhere rule already treats the same product as one). Default if you do not decide: (b).

## R1 Tab discovery

Rule: a tab (a machine or a vanilla station) is hidden until the world discovers it. "By Hand" is always discovered. Recipes inside a discovered tab keep today's rules (Crafted, parchment, faded by held ingredient).

How a tab is discovered (any one of these):
1. The player holds the tab's item at least once (inventory scan, same as seen ingredients). Machines: the machine's item. Vanilla stations: every item whose `createTile` is the station tile (computed from ContentSamples, so no hand-written list; Anvils = Iron and Lead Anvil, etc.).
2. The player stands near the station. Use vanilla's crafting range (`Player.adjTile[tile]`, the same test the game uses to allow crafting there) instead of the exact hitbox: "you could craft here" is the natural meaning of discovering a station, it works for big and odd shapes, and it is the only way for stations that have no item (Demon Altar, Crystal Ball) and for ones found in the world (Hellforge in the Underworld, furnaces in houses). Recommended: yes, near counts.
3. A recipe of the tab is crafted or unlocked by a parchment (covers old worlds and anything obtained another way).

Sync design (multiplayer first): discovery is world knowledge, owned by the server, like the seen set. Reuse the seen-set encoding (liquids already ride in it as negative markers): a tab marker is a separate negative range (e.g. `-(1000000 + stationIndex)`), so it is saved, joined and broadcast by the existing `SeenReport` / `SeenDelta` / join provider with no new packet. Clients report; the server validates the marker is a real tab and (for the near rule) accepts it only while the reporting player really is near such a tile (server checks its own tile data), rate-limited like seen reports. Single-player short-circuits as today.

Needs: a registry `RecipeTabDiscovery`: tab name -> item types and tile ids. Vanilla stations filled automatically in `VanillaStationBooks`; machine tabs listed by hand in one config class in the style of `ScrollPoolDefinitions` (the file you edit).

Migration: on load, every tab that already has a crafted or unlocked key is treated as discovered (computed, not saved), so old worlds lose nothing.

Side effect worth having: this removes most of the "holding Wood shows hundreds of faded Work Bench recipes" clutter, because the Work Bench tab (and its faded recipes) stay hidden until you hold or stand near a Work Bench. The open question from 3.2b (require ALL inputs seen) can then probably stay closed.

Open questions: should a discovered machine tab also reveal its vanilla twin (Furnace machine and Furnaces tile share the item/tile)? Default yes, they share the trigger.

Test checklist (to refine when coded): new world: only machine tabs you have held and "By Hand" show; pick up a Work Bench: its tab appears; stand next to a Demon Altar: its tab appears; old world: tabs with crafted recipes are still there; two clients: one discovers, the other's book gets the tab within a second; late joiner has it; restart keeps it.

## R2 New-recipe highlight

Per character, client-only, saved in `RecipeBookPlayer` next to favorites: a set of catalog keys the player has already looked at ("viewed").

- A recipe is NEW when it is listed in the book (`RecipeVisibility.InBook`) and its key is not in the viewed set.
- Drawn in code (no art): a soft highlight behind the entry row, fading in/out; also on a tab header when the tab itself is new (R1).
- The highlight clears when: the recipe is selected/viewed (click or opened by a jump), or the book is closed (every recipe that was listed at that moment is marked viewed).
- A recipe that becomes visible while the book is open shows the highlight at once and follows the same rules.
- Migration: the first time a character loads with this feature, seed the viewed set with everything currently in the book, so an existing save does not light up.
- Join in multiplayer: the first sync after joining must not mark everything new; seed after the join sync has been applied (same moment the migration seed runs on a fresh character).

Multiplayer: no new packets. The server only changes world knowledge; each client decides "new" locally from its own viewed set.

Decision to confirm: "closing the book clears the highlight of all current recipes" also clears entries that were never scrolled into view. Alternative: only clear the entries that were actually on screen. Default: as you wrote it (clear all).

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

Art needed (yours): `Common/UI/CustomUIElements/RecipeBookButton_New.png`, 36x36 (same size as `RecipeBookButton.png`), transparent except the outline; drawn over the normal button. Until it exists the code falls back to a code-drawn pulsing border (use `ModContent.RequestIfExists`, no crash).

Test: unlock a recipe with the book closed: outline shows; open: outline stays until cleared per the rules; hidden tab recipes do not light it.

## Order and dependencies

R0 any time. R1 first (it adds tab discovery, which R3, R4 and R6 use). R2 is independent of R1 but needs a new-tab flag from it for headers, so do it after R1. R3 after R1. R4 and R5 are independent UI patches. R6 last (needs R1 and R2).

## Next chat prompt

"Continue Factorraria recipe book work. Clone the repo, read docs/recipe_book/ROADMAP.md and docs/multiplayer/ROADMAP.md entry 3.2b, then Common/Knowledge/VanillaStationBooks.cs, RecipeCatalog.cs, RecipeKnowledgeSystem.cs, Common/Machines/RecipeVisibility.cs and Common/Net/RecipeKnowledgeNet.cs. State: the plan is written, nothing implemented; patch_vanilla_station_recipes.ps1 is applied. Implement <R0 / R1 / ...>: state the authority/sync design first, then deliver a patch with the changed list, things to test, and a Progress edit."