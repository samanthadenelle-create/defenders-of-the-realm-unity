# WORK ORDER 862 — Dungeon Treasure reward panel: fix the overlapping text (stack, don't center)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** HUD/UI — single new file `DungeonTreasurePanel.cs` (WO-850). Layout only; grant/callback logic untouched.
**WO#:** UI-seat block (860–899); 860=weapons, 861=characters, **862**=this.
**Origin:** owner felt-test 2026-08-02, "TREASURE FOUND" reward on dungeon clear — the item list overlaps the
"The cache holds:" header AND the "First clear -- a new recipe is remembered." line (screenshot).

---

## 1. RCA (from live source `Assets/_Modules/Dungeons/DungeonTreasurePanel.cs`)
The author correctly applied the fixed-pixel-band lesson (`EnsureBand` gives each label a real pixel HEIGHT so
glyphs don't cull — WO-832/841). The bug is **positioning, not height:**
- `EnsureBand` (`:208-217`) collapses each label to its fixed height **centered on its ORIGINAL anchor MIDPOINT**:
  heading `0.70–0.78` → mid **0.74** (`:104`); payout `0.40–0.66` → mid **0.53** (`:129`); unlock `0.30–0.38` → mid
  **0.34** (`:137`). Each is independently centered on a fixed parent fraction — they are NOT stacked.
- **The payout band's height GROWS with item count:** `EnsureBand(payout, LinePx * lines.Count)` = 60px × N (`:132`).
  For a 5-item cache that's **300px**, centered at 0.53 → the band spans roughly 0.30–0.76 of the content, which
  **overlaps the heading (mid 0.74) above and the unlock (mid 0.34) below.** Fewer items would fit; 4–5 items overflow.
- Net: a variable-height middle element centered on a fixed fraction collides with its fixed-fraction neighbors.

## 2. The fix — deterministic top-down STACK
Lay the four elements as a real vertical stack whose middle element's height flexes with the item count, instead of
three independently-centered fixed-fraction bands:
- Order top→bottom: **heading → payout (height = 60px × N) → "First clear" (when present) → Take button.**
- Each element is placed directly below the previous using its ACTUAL pixel height (a cumulative top-anchored offset),
  OR use a `VerticalLayoutGroup` + `ContentSizeFitter` on a content column (the RumorBoard/kit pattern). The payout's
  growth then PUSHES the unlock + button down, never overlaps them.
- Keep `EnsureBand`'s fixed-pixel heights (they're correct) — just drive POSITION from the stack, not fixed midpoints.
- **Overflow guard:** if heading + payout + unlock + button exceed the modal body (a very large cache), either grow
  the modal height with the item count OR put the payout block in a scroll well (the `EchoRosterView`/RumorBoard
  scroll pattern) so all items stay reachable above the Take button. For the current caches (≤5 items) a taller modal
  or a compact `LinePx` is enough — but bound it so a big bundle can't overflow again.
- Keep the ONE exit (Take), the ASCII-only lines, colorblind "Name xN" text, and the grant/callback flow EXACTLY as-is
  — this WO changes only where the labels sit.

## 3. Files to edit
- `Assets/_Modules/Dungeons/DungeonTreasurePanel.cs` — the `Show()` layout (`:104-142`): stack the heading/payout/
  unlock/button; keep `EnsureBand` heights; add the overflow guard. No change to `CloseAndGrant`/`Teardown`/the grant.

## 4. Acceptance criteria (headless + felt)
- [ ] With a 5-item cache + firstClear, NO overlap: "The cache holds:" header, all item lines, and "First clear -- a
      new recipe is remembered." are each fully readable and vertically separated, above the Take button.
- [ ] Works across item counts (1, 3, 5, and a large bundle) — the payout grows and pushes the rest down / scrolls,
      never overlaps; nothing spills past the modal or under Take.
- [ ] Grant still fires exactly once on Take (and on arbiter-forced close); no double-pay (behavior unchanged).
- [ ] `RunCaptureHeadless` (or the `DungeonTreasureRegression`) confirms the clean layout; `CompileGate` green.

## 5. Separate (route to WO-834 §3)
The dev **"What looks wrong?"** capture field bleeding over the "TREASURE FOUND" title is the same
`BreakCaptureHarness` note-box release-leak flagged in WO-834 §3 — fix there, not here.

## 6. Do NOT
- Do NOT touch the grant/ownership logic (`CloseAndGrant`/`Teardown`/`s_onTake`) — it's correct (WO-844/850 lessons).
- Do NOT go back to fractional-height bands (keep `EnsureBand`); the bug is POSITION, not height.
- Do NOT add a second exit — Take stays the only CTA (owner F8 seq 628). ASCII-only; colorblind text.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `DungeonTreasurePanel.cs:59-64,126-174` — stack layout. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
