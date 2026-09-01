# Master Work Order — Complete UI Reskin

**Project:** Defenders of the Realm: Echoes of Elarion  
**Decision status:** Approved and locked  
**Priority:** High  
**Scope:** Every player-facing runtime screen, modal, menu, card, status element, and navigation control

## Outcome

Replace the entire legacy grey/silver UI with one coherent medieval black-iron and antique-gold design system. The approved references in this package define the final visual language and information hierarchy. Do not preserve old component styling for convenience and do not mix the two systems during normal play.

## Approved references

- `reference/manage-troops-approved.png` — management screen structure and component finish.
- `reference/manage-troops-timer-state.png` — queue and live timer presentation.
- `reference/hud-peaceful-approved.png` — peaceful HUD state.
- `reference/hud-combat-approved.png` — final six-action combat HUD.
- `reference/pause-approved.png` — pause modal.
- `reference/hero-equipment-approved.png` — Hero/Equipment comparison state.
- `reference/hero-equipment-equipped-state.png` — equipped-item contextual action state.
- `reference/realm-store-approved.png` — Store adaptation of the shared three-column shell.
- `reference/night-market-approved.png` — Pack Shop/Night Market dashboard.

Reference images are visual specifications, not flattened runtime screens.

## Non-negotiable design language

- Near-black textured iron/leather surfaces.
- Thin aged-gold borders and restrained four-point ornaments.
- Warm ivory body text and warm gold headings.
- Circular gold-notched medallions for portraits, item icons, and major actions.
- Consistent spacing, safe-area anchors, title hierarchy, and touch targets.
- Ornament only at structural anchors; readability wins.
- Native/scalable controls with runtime text and state layered separately.
- No stretched grey buttons, giant silver frames, raw black rectangles, floating labels, duplicated summaries, or mixed typography.

## Shared component library

Create or consolidate reusable native controls before migrating individual screens:

1. Full-screen and compact modal shells with safe-area padding.
2. Title bar with Back, centered heading/dividers, and optional right action.
3. Primary, standard, destructive-warning, disabled, pressed, selected, and timed buttons.
4. Circular portrait/action/item medallion with selected, locked, unavailable, cooldown, count-badge, and notification states.
5. Status bar, resource group, objective strip, and queue status controls.
6. Category selector, tab/segment control, paging arrows, and empty state.
7. List row, item card, equipment slot, upgrade row, and shared detail card.
8. Lock/requirement treatment, timer/progress treatment, toast/feedback, tooltip, and confirmation modal.
9. Four-slot peaceful dock and six-slot combat dock using the same component family.
10. Compact paused-item picker based on the modal shell.

Prefer nine-slice/scalable frames. Keep labels, values, icons, progress, locks, and cooldowns data-bound.

## Screen scope

Apply the system to every existing player-facing surface, including:

- Peaceful and combat HUD.
- Pause, Settings, Resume, Quit to Title, and Close flows.
- Manage hub plus Defense, Buildings, Troops, and Research views.
- Hero shell, Equipment, Talents, and Skills.
- Build and Build New flows, construction timers, and Finish Now.
- Realm, Echoes, Journey, Quest, and Raid surfaces.
- Bag/Item and consumable selection.
- Queue, confirmation, locked-state, reward, error, and informational modals.
- Title/loading/profile surfaces where the legacy component family appears.

Keep existing gameplay features and navigation destinations. A surface may use a compact or full shell, but it must use this component system.

## Locked product decisions

- No minimap on the HUD.
- No skill-points value on the HUD; it appears only in Hero → Skills.
- No dead `Map soon` button on Hero screens.
- Peaceful and combat are states of one adaptive HUD, not separate canvases.
- Peaceful dock: Build, Hero, Journey, Manage.
- Combat dock: Attack, Block, Skill I, Skill II, Skill III, Item.
- Item replaces the dedicated Potion combat button.
- Tapping Item freezes/pauses the gameplay frame and opens the compact consumable picker.
- Equipment and Store surfaces show gold only; unrelated resource balances do not appear there.
- Manage has four dedicated MVVM destinations that share shell and card controls.
- Queue capacity is five; do not repeat `0/5 queued` lines. A full queue locks add actions.
- Upgrade/train actions become live authoritative timers; training stacks show quantity and total stack time.
- Up to five large item/type medallions appear before Back/Next paging is introduced.
- Locked and unaffordable content remains visible, greyed, and explicit about its requirement.

## Hero/Equipment requirements

- Use `reference/hero-equipment-approved.png` as the structure.
- Header: Back, `HERO — EQUIPMENT`, Talents.
- Player strip: portrait, name/class/level, health/focus, and live resources.
- Main area: Equipped slots, category-filtered inventory, selected-item detail.
- Known slots remain Main Hand, Off Hand, Armor, Amulet, and Ring unless the domain model already defines more.
- Do not invent runtime item stats. Bind details to authoritative item definitions.
- Selecting an unequipped item compares its relevant stats against the item currently equipped in that slot.
- Show candidate value plus directional delta. Improvements use an up arrow and green; declines use a down arrow and muted red. Never rely on color alone.
- Show only stats relevant to the selected item category and keep the row order stable within that category.
- The contextual action is `EQUIP` for an unequipped item and `REMOVE` for an equipped item. `REMOVE` means unequip to inventory; do not also show `UNEQUIP`.
- Empty slots remain visible and selectable.
- Inventory counts and equipped state update immediately after actions.
- Skill points live only in the Skills destination.

## Store requirements

- Use `reference/realm-store-approved.png` and reuse the Equipment screen’s three-column shell.
- Left: store categories. Center: touch-friendly merchandise grid. Right: selected-item details, quantity, total price, and contextual purchase action.
- Show gold as the sole Store currency unless a specific existing store is explicitly designed around another authoritative payment type.
- Default purchase action is `BUY`; affordability and stock are re-evaluated at execution time.
- Quantity changes update total gold cost immediately and cannot exceed stock, configured limits, or affordable quantity.
- Unaffordable items remain visible; disable Buy and show the missing-gold state.
- Do not show player health/focus, equipped slots, or comparison deltas unless a specific store flow actually requires equipment comparison.
- Keep store catalog data, price, stock, and purchase results authoritative and never hard-code the values shown in reference art.

## Pack Shop / Night Market requirements

- Use `reference/night-market-approved.png` as the source of truth.
- The Pack Shop is intentionally a market dashboard. Do not collapse it into the simpler Realm Store category layout.
- Left: selected featured pack, pack artwork, exact contents, value statement, and recommendation badge when applicable.
- Center top: `PACKS`. Center bottom: `MOVING`. Both sections remain visible simultaneously.
- Right top: `ACTIONS` containing Redeem a Code and Monthly Ledger.
- Right bottom: `CLOSE THE GAP` containing targeted shortfall offers such as Timber Wagon, Ingot Crate, and Grain Cart.
- Bottom: Close, one contextual Buy action, token-price notice, and the zero-store-fee statement.
- Keep `You are never required to spend anything. Ever.` visible but unobtrusive.
- Header wallet balance, network, and connection readiness are live bindings. Never fabricate a balance or ready state.
- SKR is the Pack Shop purchase currency. Optional approximate fiat display may remain when already supported, but it must never crowd or clip the primary price.
- Prices, pack contents, availability, featured status, and eligibility are authoritative live data and must not be hard-coded from the reference.
- Disable purchase safely when disconnected, unaffordable, unavailable, or already owned where relevant; show the exact blocking state.
- Premium pack artwork may retain controlled violet/blue magical glow, while structural UI uses the locked black-iron/gold system.
- Do not remove functional areas merely to reduce density; solve density through consistent cards, hierarchy, and spacing.

### Season product rule

- Season Track is removed and must not appear in the production Pack Shop while no active season product exists.
- A future time-limited pass may be enabled only when its offer definition includes a meaningful daily grant plus exactly one configured temporary perk: an extra builder or 2× Echo harvesting for the pass duration.
- The daily amount, duration, perk, claim/reset timing, eligibility, and expiry behavior must come from authoritative offer configuration.
- Expiry must remove the temporary perk safely without cancelling already-started work or duplicating rewards.
- Keep the future entry feature-flagged and hidden when the offer is absent, expired, or disabled. Do not ship a dead placeholder.

## Pause requirements

- Use `reference/pause-approved.png`.
- Compact centered modal with Paused, Resume, Settings, Quit to Title, and Close.
- Keep the frozen world visible beneath a restrained dim/vignette.
- Resume is primary. Quit to Title uses the normal warning/confirmation flow already defined by gameplay.
- Close and Resume must follow the existing intended semantics; if they are duplicates, preserve compatibility but route both through one resume command.

## Architecture

- Inspect and follow the repository’s existing MVVM, navigation, input, localization, and dependency-injection patterns.
- Views own layout and visual state only. ViewModels expose display state and commands. Domain services remain authoritative for resources, inventory, combat, queues, timers, unlocks, and persistence.
- Replace duplicated per-screen styling with shared components and tokens.
- Do not create UI-only copies of gameplay counts or timers.
- Use one source of truth for pause state and prevent stacked pause ownership from resuming gameplay prematurely.
- Guard commands against rapid repeated taps and re-check eligibility when executed.
- Preserve controller/keyboard support where it already exists while making touch targets mobile-safe.

## Migration order

1. Inventory the legacy UI prefabs/views/styles and map each to a shared replacement.
2. Implement tokens and shared components with a small visual test/gallery surface.
3. Migrate adaptive HUD and paused Item flow.
4. Migrate Pause and Settings.
5. Migrate Manage hub and its four dedicated MVVM destinations.
6. Migrate Hero, Equipment, Talents, and Skills.
7. Migrate Build, Realm, Echoes, Journey, Quest, Raid, Bag/Item, and remaining modals.
8. Remove unused legacy assets/styles only after reference search confirms no consumers.
9. Run visual, interaction, persistence, and gameplay regressions.

## Mobile requirements

- Respect safe areas, cutouts, and navigation insets on every supported landscape ratio.
- Use the project’s minimum touch-target standard; if none exists, use at least 48 logical pixels and larger for primary combat actions.
- Never shrink critical text to solve layout pressure; reflow padding or paging first.
- Keep gameplay center clear and maintain stable HUD anchors across state changes.
- Prevent modal actions beneath an overlay from receiving input.

## Acceptance criteria

- [ ] No active player-facing surface uses the legacy stretched-grey/silver component family.
- [ ] All scoped screens visibly belong to the approved design system.
- [ ] Shared components replace screen-specific copies where behavior is equivalent.
- [ ] Manage, HUD, Pause, and Hero/Equipment match their supplied references in hierarchy and finish.
- [ ] Peaceful/combat transitions move no persistent HUD anchors.
- [ ] Six combat actions are touch-safe and bind to three distinct skill slots plus Item.
- [ ] Item pause/use/cancel behavior is atomic and cannot duplicate consumption.
- [ ] No minimap, HUD skill points, or `Map soon` placeholder remains.
- [ ] Equipment/Store headers show only gold, not unrelated resource balances.
- [ ] Equipment comparison shows value plus accessible up/down delta against the same equipped slot.
- [ ] Equipment uses one contextual `EQUIP`/`REMOVE` action and never shows both Remove and Unequip.
- [ ] Store uses the shared three-column shell with categories, item grid, selected details, quantity, price, and Buy.
- [ ] Night Market preserves Featured Pack, Packs, Moving, Actions, and Close the Gap on one dashboard.
- [ ] Season Track is absent unless a configured live offer supplies a daily grant and one approved temporary perk.
- [ ] Wallet/network state and all SKR prices are live, and purchase guards prevent invalid or duplicate transactions.
- [ ] Spending and zero-fee disclosures remain visible and readable.
- [ ] Live resources, stats, inventory, queues, timers, locks, and counts are not hard-coded.
- [ ] Navigation, Back, Close, pause ownership, save/load, and rapid-tap behavior pass regression.
- [ ] Supported landscape ratios have no crop, overlap, overflow, or illegible text.
- [ ] Legacy assets/styles are removed only when unused and are not referenced by runtime content.

## Required evidence

Provide a screen inventory with migrated status, screenshots for all major surfaces and state variants, matched peaceful/combat transition capture, active timer/queue states, locked/unaffordable states, Item pause/use/cancel proof, supported-ratio screenshots, test results, and the exact files added/modified/retired.

## Codex execution instruction

Treat this as a cohesive reskin program, not isolated screenshot recreation. Inspect first. Build the shared component system, migrate incrementally, preserve authoritative gameplay behavior, and keep the project runnable after each migration group. Use the supplied art kit and references as the source of truth. Do not flatten screens, hard-code reference values, or leave legacy styling visible on reachable production UI.
