# Product Context — Factorraria

## Why it exists
Terraria has rich crafting but no real *automation* layer — you gather and craft by hand. Factorraria fills that
gap by giving players machines, power, fluids, and logistics so they can build factories and automate processing,
the same joy Factorio players get from designing production lines.

## Problems it solves
- Manual, repetitive crafting and resource processing.
- No in-world power or fluid networks in vanilla.
- No item transport with routing logic.

## Player experience goals
- Discovery: gradually introduce power -> fluids -> logistics.
- Satisfaction of building: tile-based machines that visibly light up / change state (`_Off` / `_On` sprites).
- Low friction: in-world UIs for configuring machines (recipes, liquids) rather than menus.
- Scalability: networks (power, liquid, conveyor priority) that behave predictably as they grow.

## Design principles (inferred from codebase)
- Consistency first: every machine follows the same folder/file pattern and shares a base machine class.
- Visual feedback: machines show on/off and liquid-state variants.
- Extensibility: registries (`BaseRecipeRegistery`, `MachineUIRegistry`, `LiquidNetworkRegistries`) so content is data-driven.
- Separate concerns: systems (grids/networks) live in `Common\Systems`; content lives in `Content\`.

## Open product questions (to confirm with Ismail)
- Intended progression order and where machines unlock.
- Whether additional liquids beyond Oil are planned.
- Target: single-player first, or multiplayer (net syncing) from the start.