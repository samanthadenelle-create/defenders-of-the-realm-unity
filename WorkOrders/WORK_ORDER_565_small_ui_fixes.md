# WORK ORDER 565 — Small UI/UX gap fixes (no-op / uncompletable controls)

**Status:** IMPLEMENTED (edit-only; not gated/committed by this agent)
**Date:** 2026-06-28
**Branch base:** wip/village2-and-f8-tickets (ff-merged to tip d8299b6c before work)
**Lane:** UI / data — file-disjoint, no scene files touched.

Three small "ship a no-op / uncompletable" gaps from the gap audit, each fixed minimally.

---

## 1. Inventory Sort + Filter buttons wired to EMPTY lambdas — FIXED (hidden)

**File:** `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs` (was lines 79–84)

The "Sort" and "Filter" footer buttons were visible controls bound to empty
`() => { /* TODO */ }` lambdas — they silently did nothing.

**Decision: HIDE (removed both button-creation calls).** Per the WO directive,
default to hiding when real behaviour would be non-trivial:
- **Filter** is already provided by the category tab row
  (Weapons / Armor / Accessories / Consumables) — a separate Filter control is redundant.
- **Sort** would require `InventoryVM`-level ordering (by rarity/type/name) plus a grid
  re-bind (`RebuildGrid` projects `_vm.Slots` directly) — non-trivial, out of scope for a
  small fix. Shipping a real sort here would be a half-feature.

WO-554 Obsidian chrome is untouched — only the two dead buttons were removed. The wallet
resource wells keep their right-aligned positions (`wStart 0.470`); the freed left of the
footer simply stays clear. A replacement comment documents why and how to re-add later.

Note: the `CreamLabel(Button)` private helper is now unused but retained (no C# compile
warning for unused private methods; it will be reused when sort/filter returns).

## 2. Daily wildcard `wildcard.increase-bond-rank` uncompletable — ALREADY RESOLVED (no-op)

**Files checked:** `Assets/Resources/Data/Canonical/daily-quests.json`,
`Assets/StreamingAssets/Data/Canonical/daily-quests.json`

Verified post-WO-558: the template no longer exists in either copy. Both files' `notes`
field explicitly lists `increase-bond-rank` among the legacy non-ticking templates that
WO-558 **REMOVED** (no gameplay code reported them; hero-specific ones conflict with the
single-Knight north star). No daily can roll it. **No edit required.**

## 3. Desktop ability hotkeys (1/2/3/4) removed — SKIPPED + FLAGGED

**File:** `Assets/_Modules/Village/Hero/HeroAbilityInput.cs` (`ReadSlot`, lines ~91–106)

The keyboard 1/2/3/4 ability hotkeys were removed; the in-code comment shows this was a
**deliberate mobile-first decision** ("Mobile-first: the keyboard 1/2/3/4 ability-slot
hotkeys are REMOVED"), not an oversight. Abilities fire from the on-screen HUD buttons and
gamepad face buttons.

Re-adding desktop number keys would contradict documented design intent. Per the WO
directive ("if risky or unclear, SKIP and flag — don't guess input bindings"), **skipped.**

**OWNER DECISION FLAG:** do we want desktop keyboard ability keys back for the
WebGL/desktop build? The 1/2/3/4 number row does NOT conflict with WASD movement or the
Space/left-click primary-attack bindings, so re-adding them in `ReadSlot()` would be safe
and trivial — but it reverses the explicit mobile-first removal. Design call, not a bug.

---

## Validation
- Brace check `InventoryUIBuilder.cs`: balanced (33/33). ✓
- No JSON edited (item 2 already resolved) — no JSON validation needed.
- No `.cs` edited for items 2/3.
- No scene files touched.

## Modified file list (for reconcile)
- `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs` (item 1 — removed 2 dead buttons)
- `WorkOrders/WORK_ORDER_565_small_ui_fixes.md` (this file, new)

## Owner-decision flags
1. (Item 1) Confirm hiding Sort/Filter is acceptable, or queue a real VM-backed
   sort (by rarity/type/name) as a follow-up WO if sorting is desired.
2. (Item 3) Re-add desktop 1/2/3/4 ability keys for WebGL/desktop? Safe + trivial,
   but reverses the deliberate mobile-first removal.
