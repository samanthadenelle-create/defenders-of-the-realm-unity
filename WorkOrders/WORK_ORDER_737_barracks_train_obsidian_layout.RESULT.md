# WORK ORDER 737 — Barracks Train Panel: Obsidian Master-Detail Layout — RESULT

**Status:** IMPLEMENTED (edit-only; orchestrator batch-gates + builds)
**Date:** 2026-07-16
**File:** `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs` (full rewrite of Open/Rebuild/BuildDetail + row/detail builders)
**Paired with:** WO-733 (unlock helper — projected, never re-implemented here)

---

## How it stays FrameCrafting / kit-only (no UXML, no nested panel)

- ONE factory entry: `ElarionUiKit.BuildObsidianPanel(..., frameName: RpgUiCatalog.FrameCrafting, medallionIcon:"sword")`.
  Title `"Barracks - Train"` (ASCII hyphen — was em-dash).
- Content drops into the frame's measured zones only: `layout.bodyLeft` (dark list well),
  `layout.bodyRight` (parchment detail well), `layout.footer` (wallet). **No second `BuildObsidianPanel`
  is nested** — the detail card is plain content parented to the parchment `bodyRight`.
- Every widget is a kit primitive: `MakeScrollZone` (list scroller), `BuildObsidianButton` (CTAs),
  `BuildWalletRow` + `CurrencyChip` (footer), `ShowToast` (feedback), `ElarionUiKit.Label` / `EnsureFont`
  / `FitSingleLine` (text), `RpgUiCatalog.Get(RoleSlot/RoleIcons, ...)` (sprite-first plates/icons).
- Code-built uGUI only — **no UXML** (canon §8).

## bodyLeft — the troop ladder (dark well, scrollable)

- `MakeScrollZone(bodyLeft)` built ONCE in `Open()`; `_listContent` cached. Rebuild only repaints rows
  (scroll persists structurally). Rows use the proven PartyShop recipe: `Image+Button+LayoutElement`
  with `preferredHeight = minHeight = RowHeightPx(80)`.
- **All 7 troops** shown, sorted by `UnlockBarracksTier` ASC then catalog order (stable insertion sort).
  Locked troops are **never hidden** (ladder education). List build wrapped in `Guard.TryEach("Barracks","troop-row",...)`.
- Row anatomy (L→R): icon (dim α0.5 when locked) | DisplayName (never raw id) + role line | right chip.
- **Row states → binding:**

| State | Condition | Plate / cues |
|-------|-----------|--------------|
| Selected | `id == _selectedTroopId` | `RowSelected` warm-gold plate + **gold left edge bar** (shape cue, not colour alone) + Gilt name |
| Unlocked, unselected | `TroopUnlock.IsTrainable(def)` | `RowUnlocked` neutral steel plate, Parchment name, owned `xN` badge if owned>0 |
| Locked | `!IsTrainable(def)` | `RowUnlocked * LockedTint` (0.52/0.55/0.80, mirrors `BuildingUpgradePanelMvvm.LockedTint`) + icon α0.5 + `T{n} LOCK` chip |

- Every row selectable (locked included) so tapping a locked row selects it and the detail explains the unlock.

## bodyRight — detail card (parchment ink, non-overlapping Y bands)

| Band | Content | Binding |
|------|---------|---------|
| 0.92–0.99 | DisplayName (bold Ink) | `DisplayName(def)` |
| 0.855–0.915 | `Role  -  {n} slots  -  Barracks T{n}` | def.Role/Slots/UnlockBarracksTier |
| 0.72–0.85 | Portrait socket (slot art + troop icon; dim when locked) | `TroopIcon(def)` sprite-first, glyph fallback |
| 0.64–0.71 | `Owned: N` / `Recovering: M` (InkBad italic if M>0) | `OwnedCount` / `WoundedCount` |
| 0.575–0.635 | `Army: used / max slots` (InkBad when `!hasRoom`) | `army.SlotsUsed/MaxArmySize` |
| 0.48–0.555 | `HP n  -  DMG n  -  Range n` | def stats |
| 0.385–0.465 | `Cost: ...` tinted InkGood/InkBad by afford (InkDim when locked) | `EconomyService.CanAfford(CostOf)` |
| 0.16–0.37 | **STATE BLOCK** (see below) | mutually exclusive |
| 0.16–0.26 | Hint (only in unlocked states) | static `DetailHint` |
| 0.03–0.14 | CTA row: Train / Train x5 | see CTA rules |

### STATE BLOCK (mutually exclusive) → binding

- **A — Locked** (`!IsTrainable`): a dim parchment veil plate (α0.45, `slot_item` sprite-first) with
  `LOCKED` / `TroopUnlock.LockedReason(def)` ("Unlocks at Barracks Tier {n} - {TierName}") /
  "Upgrade the Barracks to recruit." Train CTAs **Gray, non-interactable**. **Never red** (lock is not destructive).
- **B — Unlocked, can't afford/cap** (`trainable && !canTrain`): a bold note line —
  `"Army cap full - deploy or expand."` (when `!hasRoom`, InkBad) or `"Not enough resources."` (InkBad);
  cap line + cost line already tinted InkBad. Train CTAs **Gray, non-interactable**.
- **C — Unlocked, affordable** (`canTrain`): `"Ready to train."` InkGood; Train = **Green**, Train x5 = **Yellow**, both interactable.

`canTrain = IsTrainable(def) && affordable && hasRoom`. CTA meaning carried by **text + enabled state**,
not colour alone (colorblind-safe, Grok-02 §4.2).

## footer — wallet (kit contract)

`BuildWalletRow(footer, [Wood, Iron, Food, Crystal])`; chips own CompactNumber (no hand-formatted string,
no ellipsis); `UpdateWallet` calls `chip.SetAmount` only; subscribed to `EconomyService.OnChanged → Rebuild`.
Footer is the frame's own zone — CTA band (≤0.14) sits well above it.

## Toast coverage (`ShowToast`)

- Trained OK → Confirm; cap/resources fail → Danger (`TrainAndRefresh`).
- **Locked-tier**: `"{Name} unlocks at Barracks Tier {n}."` Danger — defensive path in `TrainAndRefresh`
  (CTA is already disabled when locked).
- **ff.barracks feature locked**: `"The Barracks is not built yet."` Danger — added to the refused
  `TroopDialogueCommands.ShowTrainingUI` branch.

## Presentation-never-invents-rules

The view only projects service state: `TroopUnlock.IsTrainable/LockedReason`, `EconomyService.CanAfford`,
`ArmyStorage.CanTrain/SlotsUsed/MaxArmySize`, `OwnedCount/WoundedCount`. No tier math, no spend logic in the view.
`FlowTrace.Step("Barracks", ...)` on open + rebuild; `Guard.TryEach` around the row list build.

---

## Pair-walk checklist (WO-737 acceptance)

- [x] Real FrameCrafting frame (BuildObsidianPanel + FrameCrafting), two-tone dark list | parchment detail.
- [x] ONE close control (kit shared close; no per-panel X).
- [x] Wallet chips in footer only; no ellipsis currency.
- [x] Locked rows visible with LockedTint plate + dim icon + `T{n} LOCK` chip.
- [x] Selected locked troop → STATE BLOCK A, no green Train.
- [x] Selected unlocked affordable → green Train (+ Yellow Train x5).
- [x] No double frame / no UXML / no hand-drawn gold boxes as chrome.
- [x] Scroll on bodyLeft only (MakeScrollZone), not the whole frame; handles all 7 rows.
- [x] Mobile: 80px rows, CTAs in bottom thumb band, rows tappable.
- [ ] Graphics-build screenshot pair-walk vs Crafting_Panel PNG — deferred to PO felt-verify (headless is -nographics/blank).

## Brace / NUL gate
- `TroopTrainingPanel.cs`: braces 56/56, parens 398/398, NUL 0 — PASS.

## Not done here (out of scope / handoff)
- CompileGate + graphics build: orchestrator (edit-only agent, no gate/commit per CLAUDE.md §11).
- Screenshot pair-walk vs Blink Crafting template: PO felt-pass.
