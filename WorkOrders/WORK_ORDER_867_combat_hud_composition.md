> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 29c80b0b; HudUiRegression.cs +505 lines.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 867 — Combat HUD: nameplate composition + right-edge grouping

**Status:** DONE
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** HUD/UI — combat HUD (`BattleArenaHud.cs` + the enemy nameplate + the right-edge ability column; CLI confirm).
**WO#:** UI-seat block; **867**=this.
**Source:** `docs/ui-review/2026-08-04-seeker/README.md` §3 + `06-combat-hud.png` (Seeker).

---

## 1. Enemy nameplate — compose four pieces into ONE plate
Today the enemy plate is **four disconnected pieces:** the `Orcish Warrior` label, an empty black bar, the actual
green/blue HP bar offset to its right, and `Lv 8` floating far right — they don't align into one plate, and a
ragged/torn edge graphic overlaps the assembly. **Rebuild as a single composed unit:** name + level + HP bar in one
fixed-pixel plate, aligned, with the HP fill inside the bar (not offset).

## 2. Ragged-edge artifacts (hero + Heart plates)
The hero and Heart plates carry the same **grey jagged shapes at the right end** that read as broken sprites, not
deliberate damage-styling. Remove/replace them so the bars end cleanly (or make the "torn edge" a deliberate, correct
sprite — but as-is it reads as a bug).

## 3. `Echoes 1/6` floater
It floats between the two plates with a stray gold rule, in no established band. Dock it into a real band (or the
existing Echoes chip), not free-floating.

## 4. Right edge — group into ONE deliberate column, ONE chrome language
The right edge is ungrouped: `Flee` (grey box), `Echoes` (different grey box), free-floating circular ability icons at
inconsistent sizes/spacings, `Dodge/Attack` in a circle, and a weapon slider — no alignment, grouping, or shared
chrome. And the **bottom ability bar is a different UI language** (dark blue-grey rounded panel + square icons vs the
circular right-edge icons), and **`LOCKING`** is a gold-outlined box — a fifth treatment.
**Fix:** one chrome language for actionable buttons; the right edge grouped into a deliberate column with **consistent
icon sizing + spacing**; reconcile the bottom bar and right-edge icons to the same language; fold `LOCKING` into it.

## 5. Binding (review §0)
Fixed-pixel bands (`MinTouchPx = 112`); text-encoded state never colour (owner colourblind); ASCII-only TMP; strict
MVVM (ratchet armed, no new reflection bridge / `static_gate.py` entry); landscape. This is world-space + native-DPI —
`RunCaptureHeadless` can't see it; **verify on the Seeker.**

## 6. Acceptance
- [ ] On-device (2340×1080): the enemy plate reads as ONE composed unit (name+level+HP aligned); no ragged/broken-
      sprite edges on the enemy/hero/Heart plates.
- [ ] `Echoes 1/6` sits in a real band, not floating.
- [ ] The right edge is one grouped column with consistent icon sizing/spacing; ONE chrome language across the action
      buttons (right edge + bottom bar + LOCKING), differentiated by emphasis not five styles.
- [ ] `CompileGate` green; verified on Seeker (headless insufficient).

## 7. Do NOT
- Do NOT change combat logic — composition/layout/chrome only. No fraction bands. No new reflection bridge.
