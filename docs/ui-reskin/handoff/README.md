# Elarion UI Redesign Handoff

This package is the implementation handoff for the complete approved UI reskin for **Defenders of the Realm: Echoes of Elarion**.

## Work orders

- `COMPLETE_UI_RESKIN_WORK_ORDER.md` — master scope, design-system rules, migration plan, and final acceptance criteria for every player-facing UI surface.
- `MANAGE_MVVM_WORK_ORDER.md` — dedicated MVVM management views with shared controls, item detail cards, queue rules, and live action timers.
- `ADAPTIVE_HUD_WORK_ORDER.md` — one HUD that changes state between peaceful exploration and combat without changing its visual structure.

## Visual references

- `reference/manage-troops-approved.png` — approved Manage styling and detail-card composition.
- `reference/manage-troops-timer-state.png` — simplified queue/timer behavior reference.
- `reference/hud-peaceful-approved.png` — peaceful exploration state.
- `reference/hud-combat-approved.png` — final six-action combat state using the same HUD shell.
- `reference/pause-approved.png` — approved compact pause modal.
- `reference/hero-equipment-approved.png` — approved Hero/Equipment comparison state.
- `reference/hero-equipment-equipped-state.png` — contextual Remove state for an equipped item.
- `reference/realm-store-approved.png` — approved Store adaptation of the three-column shell.
- `reference/night-market-approved.png` — approved Pack Shop dashboard with Packs, Moving, Actions, and Close the Gap visible together.
- `reference/hud-original.png` — original screen for comparison only; do not reproduce its layout.

## Shared visual system

Use the existing `Elarion_Medieval_UI_Kit` assets: near-black iron, restrained antique-gold trim, warm ivory text, circular four-point medallions, compact ornament, and consistent scalable frames. Build every screen from native UI controls and layered assets. Never ship a reference image as a flattened interactive screen. The legacy stretched-grey component family is retired.

## Codex start instruction

Inspect the repository before editing. Locate the current view/navigation system, MVVM conventions, game-state services, queues, timers, resource checks, safe-area handling, input paths, and relevant tests. Preserve gameplay behavior and existing public contracts. Implement the work orders with the smallest coherent change set, then validate both visual state and live data behavior.
