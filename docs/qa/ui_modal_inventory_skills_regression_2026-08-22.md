# UI, modal safety, inventory, and skills regression — 2026-08-22

Purpose: device acceptance cases for the 2026-08-22 Seeker findings. These cases complement the focused static oracles; they verify the player-visible behavior and the seams between systems.

Evidence rule: record one screenshot before the action, one after, and the bounded device log. A case passes only when both the visible result and state mutation are correct.

## A. Pause and combat-modal safety

### A1 — Explicit Pause freezes the world

1. Enter the village during visible NPC/enemy movement or an active wave.
2. Note Heart HP, enemy position, wave timer, and a visible world timer.
3. Open Pause and wait 15 seconds without backgrounding the app.

Pass: the Pause overlay says `Paused`; Heart/hero HP, enemy positions, AI, projectiles, wave progression, and scaled world timers do not change. UI remains responsive.

### A2 — Resume restores the exact prior simulation state

1. Continue from A1.
2. Press Resume once.

Pass: the overlay closes, simulation resumes once, and it does not remain frozen or run faster than before. A second tap cannot double-resume or alter time scale.

### A3 — Settings nested under Pause retains pause ownership

1. Pause during visible world activity.
2. Open Settings, change no option, wait 10 seconds, then close Settings.

Pass: gameplay remains frozen throughout; closing Settings returns to Pause, not gameplay. Resume is the only action that resumes.

### A4 — Repeated and back-button transitions are idempotent

Exercise `Pause -> Settings -> Back -> Resume`, `Pause -> Back`, and rapid double taps on Pause/Resume.

Pass: no orphan scrim, duplicate panel, stuck freeze, transient unpause, or incorrect `Paused` label.

### A5 — Ordinary modal open during wave preparation is closed at activation

1. During the wave countdown, open Skills and open its spend confirmation.
2. Let the wave become Active without touching the UI.

Pass: confirmation and Skills close before the first enemy acts; no Wisdom is spent; no duplicate unlock occurs; combat controls are available.

### A6 — Ordinary gameplay panels are rejected during an active wave

During an active wave, attempt to open Skills, Bag, Gear, Store, Daily Chest, and any ordinary building modal.

Pass: each is rejected with no scrim, hidden panel, currency mutation, ad start, or wallet handoff. Explicit Pause remains available.

### A7 — Panels recover after wave termination

After both a victory path and a defeat/end-state path, return to an admissible village state and open Skills, Bag, and Store.

Pass: panels open normally; no stale battle lock or modal ownership remains.

### A8 — Defeat replaces residual UI safely

Allow the Heart to be destroyed while no ordinary panel is admissible.

Pass: End State is the sole modal, the world freezes once, and the trace never reports an ordinary modal as active alongside an active wave.

## B. Gear and inventory

### B1 — Gear overview has no dead affordance

Open Bag/Gear overview and tap every visible action and preview-shaped surface.

Pass: no blank preview is presented as actionable; every enabled control produces a visible result; decorative regions do not look tappable.

### B2 — Already-equipped item is unambiguous

Open Weapons and Armor, select the currently worn item, and tap its state treatment.

Pass: it reads as a non-action state such as `Worn` or `Equipped`, not an enabled button. It does not imply a failed Equip action.

### B3 — Equip a different item

With two owned weapons or armor pieces, select the non-equipped item, compare it, and press Equip once.

Pass: equipped item changes exactly once, stats and comparison refresh immediately, the new item becomes non-actionable `Equipped`, and persistence survives panel close/reopen.

### B4 — Comparison guidance appears only when actionable

Check Weapons/Armor with zero, one, and two-or-more owned items; then check Trinkets.

Pass: `tap another to compare` appears only for Weapons/Armor when at least two comparable items exist. It never appears on an empty trinket state.

### B5 — Trinket empty-state teaches the real loop

Open Trinkets with none owned.

Pass: copy explains that rough stones are found in dungeons and polished by the Jeweler to craft Rings of Power. It does not claim trinkets only come from Ember Deep.

### B6 — Layout and touch integrity

At Seeker landscape resolution, inspect Gear overview, Weapons, Armor, and Trinkets.

Pass: HUD does not cover modal headers; helper text does not collide with rails/buttons; names and stat deltas are readable; cards meet touch floor; sparse inventory does not leave a misleading giant interactive void.

## C. Skills and hot-swap

### C1 — Base node enters a focused branch

From the Skills overview, choose each available base/category node.

Pass: the destination clearly identifies Common/Class/Specialization context; Back or breadcrumb returns one level without closing the whole feature or losing selection.

### C2 — Skill type is visible before spending

Inspect locked and unlocked active, passive, and hot-swappable skills.

Pass: type is distinguishable without relying only on color. Hotbar-eligible actives are explicitly labeled; passives cannot be mistaken for assignable skills.

### C3 — Unlock flows directly into assignment

Unlock a hotbar-eligible active skill with an empty slot available.

Pass: after one confirmation, the UI immediately offers or completes assignment without requiring the node to be reselected. Wisdom is deducted exactly once.

### C4 — Full hotbar replacement is explicit

With all hotbar slots occupied, unlock or select another eligible active.

Pass: the UI names the target slot/current skill before replacement; cancellation changes nothing; confirmation replaces exactly one slot and persists.

### C5 — Assigned state and slot number remain visible

Assign skills to multiple slots, close/reopen Skills, and revisit their branches.

Pass: each assigned node shows its numbered slot; unassigned active and passive states remain distinct; the hotbar dock matches runtime combat order.

### C6 — Locked/cancelled paths do not mutate state

Attempt a prerequisite-locked skill, insufficient-Wisdom skill, and cancel a valid spend.

Pass: no skill unlock, Wisdom deduction, slot change, or duplicate callback occurs.

### C7 — Text and layout integrity

Inspect the overview, every branch, locked detail, spend confirmation, owned passive, owned active, and replacement prompt.

Pass: no overlapping text/buttons; full skill and currency names are readable; status text has its own band; the tree uses the available body without clipping nodes or leaving the primary interaction compressed above a large dead region.

### C8 — Combat gate preserves state

Open an unconfirmed skill purchase during wave preparation and let the wave activate; after the wave, reopen Skills.

Pass: the modal was dismissed, no Wisdom was spent, no skill was unlocked or assigned, and the exact prior persistent state remains intact.

## D. Cross-feature smoke

1. Equip a new item, unlock and assign a skill, close all panels, Pause/Settings/Resume, complete a wave, and reopen Gear and Skills.
2. Repeat after app background/foreground and after returning to title and re-entering the save.

Pass: equipment, unlocked skills, hotbar order, Wisdom, and currencies persist exactly; no stale modal/battle lock remains; time scale is normal; no duplicate grants or spends appear in the trace.

## Required automated coverage alongside this matrix

- Static oracle: active WaveManager owns a BattleLock probe and closes ordinary panels after entering Active but before spawn/event dispatch.
- Runtime/editor probe: ordinary panels reject during Active, battle-allowed Pause admits, and ordinary panels admit again after termination.
- Pause oracle: Pause writes a frozen simulation state, nested Settings cannot resume it, Resume restores a valid prior scale once, and disable/quit cannot leak a frozen next scene.
- Inventory oracle: empty-state copy and comparison predicates are derived from category/count, and equipped rows cannot expose an enabled Equip action.
- Skills oracle: active/passive/hotbar semantics are non-color-only, unlock-to-assignment is one continuous state transition, cancellation is mutation-free, and assigned slot persistence is authoritative.
- Layout oracle at 2340x1080: all interactive controls meet the shared touch floor; text bands clear the shared font floor; no header, helper, status, or action rectangles overlap.

