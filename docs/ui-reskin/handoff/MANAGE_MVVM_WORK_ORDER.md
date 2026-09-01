# Work Order — Manage MVVM UI System

**Project:** Defenders of the Realm: Echoes of Elarion  
**Priority:** High  
**Type:** UI architecture, reskin, and interaction cleanup  
**Visual sources of truth:** `reference/manage-troops-approved.png`, `reference/manage-troops-timer-state.png`

## Goal

Replace the current crowded management screens with four dedicated MVVM views that share one premium visual system and reusable controls. Preserve existing gameplay rules, costs, unlocks, timers, saves, and queue services.

## Navigation

The Manage hub contains four paths:

1. Defense → dedicated Defense view and ViewModel
2. Buildings → dedicated Buildings view and ViewModel
3. Troops → dedicated Troops view and ViewModel
4. Research → dedicated Research view and ViewModel

Do not build one large view full of category conditionals. The four destinations share shell and card controls but own their category-specific bindings and commands.

## Required shared controls

- `ManageShell`: frame, title, Back, Queue, Close, and the Builders/Training/Research status row.
- `ItemSelector`: up to five large circular portrait medallions.
- `ItemDetailCard`: selected item name, level, short role, benefit, cost, requirement, status, and actions.
- `UpgradeOptionCard`: icon, name, one-line benefit, cost, requirement/status, and action.
- `QueueButton`: always labeled `QUEUE`; it does not repeat a queue fraction.
- `TimedActionButton`: idle, disabled, locked, and live-countdown states.

Use composition or shared interfaces/base classes according to the project’s established MVVM style. Do not introduce a second framework.

## Screen rules

- One selected item owns the shared detail card; selecting a different medallion swaps only bound data.
- Show up to five item medallions at once and make them visually prominent.
- Show Back/Next paging only when more than five items exist. Do not render empty arrows for a single set.
- Keep unavailable items visible. Desaturate them and show a padlock plus the exact requirement.
- Do not sort or label the list as “Affordable first.”
- Do not use loose explanatory paragraphs, repeated queue summaries, or unnecessary page text.
- Keep content to name, level, one-line benefit, cost, requirement/status, and actions.

## Item flow

1. Enter a category and select its first valid item by default.
2. Selecting a medallion updates the shared detail card.
3. The item card exposes the relevant primary action, such as `TRAIN`, `BUILD`, or `RESEARCH`, plus `UPGRADE` when supported.
4. Selecting `UPGRADE` reuses the main content canvas for that item’s upgrade options. It is a view state, not a separate modal and not a permanent tab bar.
5. A compact Back action returns from upgrade options to the selected item without losing selection or scroll/page state.

## Upgrade options

- List available, unaffordable, and locked options together.
- Every row shows a simple benefit and cost directly below the option name.
- Available: normal gold action treatment.
- Insufficient resources: desaturated action with the missing resource amount.
- Locked: greyed row, padlock, exact requirement, and no active action.
- Do not hide future options; visibility communicates progression.

## Queue behavior

- Each applicable queue accepts at most five entries.
- Remove all `0/5 queued`, `x/5 queued`, and repeated `IN QUEUE` lines from category content and top status panels.
- The top-right control reads only `QUEUE` and opens the existing queue presentation.
- When the relevant queue reaches five entries, actions that would add another entry become disabled/locked.
- Reuse the existing queue as the source of truth. Do not maintain a second UI-only queue count.

## Timed actions

- `UPGRADE`, `TRAIN`, `BUILD`, and `RESEARCH` submit immediately when valid; do not add a confirmation modal.
- After a successful upgrade submission, that option’s action becomes a live timer, for example `UPGRADING • 04:32`.
- After a successful training submission, the action shows stack count and total stack time, for example `×3 • 06:45`.
- Stack time equals the remaining time of the active unit plus all queued units in that stack.
- Timers bind to authoritative game time/queue state and survive view navigation and save/load.
- Do not create a cancel behavior unless one already exists in gameplay.

## Recommended ViewModel contracts

Adapt names to repository conventions; behavior matters more than these exact types.

- `ManageHubViewModel`: category availability and navigation commands.
- `DefenseManageViewModel`, `BuildingsManageViewModel`, `TroopsManageViewModel`, `ResearchManageViewModel`.
- Shared item contract: identity, portrait, name, level, role, benefit, cost, unlock requirement, availability state, queue state, remaining duration, and commands.
- Shared collection state: selected item, visible five-item page, page index/count, `CanPageBack`, `CanPageNext`.
- Action state enum or equivalent: `Available`, `InsufficientResources`, `Locked`, `InProgress`, `QueueFull`.
- Commands must guard rapid repeat input and re-check authoritative eligibility at execution time.

## Visual requirements

- Match the supplied references: black iron/leather, thin antique-gold framing, warm ivory text, restrained filigree, and large circular portrait medallions.
- Use one consistent type hierarchy and spacing grid.
- Runtime text, timers, counts, lock states, and progress remain separate from frame art.
- Prefer nine-slice/scalable frames and preserve safe-area insets across supported landscape ratios.
- Avoid stretched grey buttons, giant borders, duplicated headings, floating text, and mixed component styles.

## Acceptance criteria

- [ ] Manage hub opens four distinct MVVM destinations.
- [ ] All destinations use the shared shell, selector, detail card, status treatment, and action-state visuals.
- [ ] Selecting an item updates one shared detail card without spawning a modal.
- [ ] Up to five large item medallions fit; paging appears only for six or more.
- [ ] Locked and unaffordable content stays visible with clear state and requirement.
- [ ] Upgrade options show simple benefit and cost.
- [ ] No `0/5 queued` or repeated queue line appears anywhere in these views.
- [ ] Queue stops at five and additional queue actions lock safely.
- [ ] Upgrade actions become authoritative live countdowns.
- [ ] Training stacks show quantity and total remaining stack time.
- [ ] Timers remain accurate after navigation and save/load.
- [ ] Rapid taps cannot submit duplicate actions.
- [ ] Existing costs, progression, resource deduction, unlocks, and queue logic remain unchanged.
- [ ] Primary and extreme supported landscape ratios show no overlap, crop, or illegible text.

## Validation evidence

Provide screenshots of every category, a locked item, an unaffordable item, a full queue, an active upgrade timer, and a multi-unit training stack. Include focused MVVM/command tests plus relevant queue, save/load, navigation, and resource regression results.

## Codex execution instruction

Inspect first, then implement. Find existing screens, queue/timer services, unlock and affordability evaluators, navigation commands, reusable controls, and tests. Preserve live domain logic and bind the new ViewModels to it. Do not hard-code values shown in reference art. Keep unrelated user changes intact.
