# Work Order — Single Adaptive Peaceful/Combat HUD

**Project:** Defenders of the Realm: Echoes of Elarion  
**Priority:** High  
**Type:** HUD consolidation and state-driven MVVM implementation  
**Visual sources of truth:** `reference/hud-peaceful-approved.png`, `reference/hud-combat-approved.png`

## Goal

Replace the competing HUD systems with one quiet, reusable HUD shell. Peaceful exploration and combat are states of the same view—not separate canvases or separate layouts. The 3D world remains dominant.

## Persistent shell

These components never change position when state changes:

- Top-left compact player plaque: `SYLAS • LEVEL 2` and three slim status bars.
- Heart of Elarion objective strip immediately below the player plaque.
- Top-center slim compass.
- Top-right combined currency and Echoes plaque.
- Bottom-left translucent movement joystick.
- Bottom-center adaptive dock housing.

The HUD must not contain a minimap. The HUD must not display skill points; skill points belong only in Hero → Skills.

## Peaceful state

- Event module: wave number, next-wave countdown, and integrated `START NOW` action.
- Dock contents: four large actions — `BUILD`, `HERO`, `JOURNEY`, `MANAGE`.
- Manage may show a compact numeric badge such as `3`; do not show `3 idle` in the dock.
- Contextual edge actions may appear when valid: `REALM` on the left and `HARVEST` on the right.
- Contextual actions are hidden when irrelevant; empty frames are not shown.

## Combat state

Only two modules swap content:

1. The event module keeps its frame and position but shows wave number, enemies remaining, and a thin wave-progress indicator. `START NOW` disappears.
2. The dock stays bottom-center but widens to six touch-safe actions: `ATTACK`, `BLOCK`, `SKILL I`, `SKILL II`, `SKILL III`, `ITEM`.

Peaceful contextual actions hide during combat. Player status, objective strip, compass, resources, joystick, margins, and framing remain stable.

## MVVM/state design

- Use one `AdaptiveHudView` and one owning `AdaptiveHudViewModel` or the project’s equivalent.
- Expose an explicit state such as `Peaceful`, `CombatActive`, and any existing transition/post-wave state.
- Bind the event module through an `EventStatusViewModel` whose content changes without changing its layout slot.
- Bind the dock through an `ActionDockViewModel` that supplies four peaceful slots or six combat slots from the active state.
- Drive state from the existing authoritative encounter/combat service; do not infer it from animation or visibility.
- Keep command routing state-aware and prevent hidden/disabled peaceful actions from executing in combat.
- Avoid destroying and recreating the entire HUD during transitions; update bound module content in place.

## Combat Item flow

- `ITEM` replaces the dedicated Potion button. Potions are consumables inside Item.
- Tapping `ITEM` pauses the gameplay frame through the existing authoritative pause service and opens a compact item picker over the frozen scene.
- Show only consumables currently usable by the hero, with icon, name, quantity, short effect, and one `USE` action.
- Selecting `USE` consumes exactly one item, closes the picker, and resumes gameplay.
- Closing or backing out consumes nothing and resumes gameplay.
- Prevent duplicate consumption from rapid taps and re-check inventory at command execution.
- The picker reuses the approved pause/modal component family; it is not a second inventory screen.

## Transition behavior

- State change should be calm and fast: short crossfade or content dissolve inside the two changing modules.
- Do not slide the persistent HUD to new positions.
- Hide Realm/Harvest with the same brief fade.
- Preserve input focus rules and prevent a tap during transition from invoking the outgoing command.

## Visual requirements

- Use the exact Manage visual language: near-black iron, thin antique gold, warm ivory serif labels, restrained four-point ornaments, and circular medallions.
- Keep consistent safe-area margins and align panels to shared screen anchors.
- No giant floating words, raw black rectangles, duplicated resource bars, or mixed button systems.
- Keep the center of the screen open for world and combat readability.
- Keep ornaments subordinate to state and information.

## Acceptance criteria

- [ ] One HUD view supports peaceful and combat states.
- [ ] No minimap exists in either state.
- [ ] No skill-points value exists in either state; Hero → Skills remains the only location.
- [ ] Persistent components remain pixel-stable across state changes at the primary target resolution.
- [ ] Only event-module content and dock content swap for combat.
- [ ] Realm/Harvest appear only when valid and are absent during combat.
- [ ] Peaceful dock shows Build, Hero, Journey, Manage.
- [ ] Combat dock shows Attack, Block, Skill I, Skill II, Skill III, Item.
- [ ] Item pauses the gameplay frame and opens the consumable picker.
- [ ] Using a potion consumes one, closes the picker, and resumes; cancel consumes nothing.
- [ ] Wave countdown, enemies remaining, resources, Echoes, health, and consumable count bind to live state.
- [ ] Hidden, disabled, and outgoing commands cannot fire.
- [ ] State transition does not create duplicate HUD instances or lose input bindings.
- [ ] Supported landscape ratios respect safe areas without overlap or crop.

## Validation evidence

Provide matched peaceful/combat screenshots at the same camera position, a short capture of the transition, screenshots at supported aspect-ratio extremes, and tests for state switching, command routing, live values, hidden contextual actions, and repeated transitions.

## Codex execution instruction

Inspect the current HUD hierarchy and state services before editing. Consolidate rather than layering another canvas on top. Reuse authoritative player, encounter, resource, objective, input, and navigation state. Remove the old minimap and HUD skill-points bindings cleanly, including dead subscriptions, while preserving unrelated gameplay systems.
