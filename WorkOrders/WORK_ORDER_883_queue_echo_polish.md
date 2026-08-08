> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: shipped in commit 572f1289; QueueRailView.cs +61 lines and EchoCardVM.cs +30 lines.
> The previous Status line read "READY (low - polish on shipped WO-864 + WO-852)." and was wrong.

# WORK ORDER 883 — Polish: QueueCardRail truncation/clock + EchoCard chip-note clip

**Status:** DONE (reconciled 2026-08-08) — polish on shipped WO-864 + WO-852. **Lane:** HUD/UI. **WO#:** UI-seat block; **883**.
**Source:** `docs/ui-review/screens-2026-08-04/QueueCardRail_2340x1080.png` + `EchoCard_2340x1080.png`.
Both screens LANDED well (three-rail queue + fixed Echo picker) — these are small residuals.

## 1. QueueCardRail (`ObsidianQueueHud` cards)
- **Card name truncation** — `Arcane S…` / chip `Ar…`. Widen the name band or drop the font a notch so common
  building/troop names fit (or ellipsize cleanly, not mid-word-ugly).
- **Timer clock icon** — the timer is gold text only; WO-864 specced a clock **sprite** beside it. Add the clock
  sprite (a `RpgUiCatalog` sprite, NOT a glyph — tofu) for the CoC read, if cheap.
- **Header descenders** — `…busy` descenders kiss the first card's top edge; add a few px of gap.
- (`+2 MORE` overflow aggregation vs one-card-per-slot — leave as-is unless the owner prefers per-slot cards.)

## 2. EchoCard (`EchoCardView` picker)
- **Chip note clipped** — the selected chip's `best -- this Echo's calling` note is cut at the scroll-area bottom
  edge, and reads redundant against the footer's `Gathering Food … (best -- this Echo's calling)`. Either give the
  note room in the scroll band, OR drop the per-chip note (the footer already says it) — a small band-height/dedup fix.

## 3. MVVM law
View-layout only; the VM provides the strings (name/timer/note). The View renders them — no computing/duplicating.
Fixed-pixel bands.

## 4. Acceptance
- [ ] On-device: queue card names fit (no `Arcane S…`); optional clock sprite by the timer; header clears the cards.
- [ ] Echo selected-chip note is not clipped and not redundant. `CompileGate` green.
