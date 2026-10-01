# Cline's Memory Bank (Factorraria)

I am Cline, an engineer on the **Factorraria** mod. My memory resets between sessions, so I rely ENTIRELY on this Memory Bank to understand the project and continue work. I MUST read ALL files in `memory-bank/` at the start of EVERY task — this is not optional.

## Memory Bank Structure (`memory-bank/` in the project root)
- `projectbrief.md` — foundation: what Factorraria is, core goals, scope.
- `productContext.md` — why it exists, problems it solves, player/UX goals.
- `activeContext.md` — current focus, recent changes, next steps. **Changes most often.**
- `systemPatterns.md` — architecture, design patterns, component relationships.
- `techContext.md` — tech stack, tModLoader version, dependencies, build setup, constraints.
- `progress.md` — what works, what's left, known issues, decision history.

## Rules for me
- Read all memory-bank files before starting work, then read the specific source files I'm about to touch.
- Update `activeContext.md` after meaningful changes; update `progress.md` at milestones.
- When the user says **"update memory bank"**, review and update ALL files.
- When the user says **"follow your custom instructions"**, read the Memory Bank and continue where work left off.
- Keep entries concise and factual. Do not invent progress that wasn't made.

## Reference locations (also see .clinerules/project.md)
- tModLoader API source: `C:\Users\ismai\OneDrive\Documents\My Games\Terraria\tModLoader-2026.08.3.0\patches\tModLoader\Terraria\`
- Vanilla sources: same root `\patches\Terraria\` and `\patches\TerrariaNetCore\`.
- Mod reference DLLs: `...\tModLoader\ModSources\ModAssemblies\` (ModLiquidLib, Luminance).