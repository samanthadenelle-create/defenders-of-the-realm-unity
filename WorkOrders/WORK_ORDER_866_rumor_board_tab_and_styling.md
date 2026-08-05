# WORK ORDER 866 — Brom's Rumor Board: clipped-tab bug + frame/content styling pass

**Status:** READY TO IMPLEMENT
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** HUD/UI — `RumorBoardPanel.cs`. **WO#:** UI-seat block; **866**=this.
**Source:** `docs/ui-review/2026-08-04-seeker/README.md` §2 + `04-rumor-board.png` / `05-rumor-board-b.png` (Seeker).

---

## 1. Bug (fix first)
**The tab strip is CLIPPED.** `* All` / `Story` / `Daily` / `G…` — the 4th tab is cut off where the right detail
panel's edge crosses it; the tab row and the detail panel **overlap**. Give the tab row its OWN fixed-pixel band that
the detail panel cannot cross (fraction-band failure class again — review §0). All tabs fully visible.

## 2. Styling pass
- **Frame ↔ content mismatch (most of why it looks unfinished):** an ornate metal frame (rivets, scrollwork) wraps
  flat black rectangles with plain text — the frame promises a crafted board, the contents read as a debug list.
  Bring the content chrome up to the frame (or calm the frame) so they agree.
- **~Half the panel is dead space:** one quest, then a large empty black region in both columns. The board is sized
  for content it doesn't have → an early player sees an empty box. Add an **empty/early state** ("you're early —
  more rumors as you progress") instead of dead black, and/or size the board to its content.
- **Three visual languages for the same info class → unify to ONE:** `Crystals 150` / `Food 20` outlined chips,
  `Story Quest` / `New` outlined differently, and the quest row a filled black bar. Pick one reward/tag/row language.
- **`Close` floats** — centred at the bottom straddling both columns and overlapping the frame edge. Put it in
  consistent panel chrome (its own footer band), not floating.

## 3. KEEP — do NOT restyle away
**`* All` marks selection with BOTH an asterisk AND an underline** — text-encoded, not colour. This is CORRECT for the
colourblind owner and is the pattern the rest of the rework should FOLLOW. **A styling pass must NOT replace it with a
colour highlight.**

## 4. Binding (review §0)
Fixed-pixel bands only; `MinTouchPx = 112`; text-encoded state never colour; ASCII-only TMP; strict MVVM (ratchet
armed, no new reflection bridge / `static_gate.py` entry); landscape.

## 5. Acceptance
- [ ] On-device (2340×1080): all tabs fully visible; tab row and detail panel do not overlap.
- [ ] One reward/tag/row visual language across the board; frame and content agree.
- [ ] Early/empty state instead of a large dead-black region; Close in consistent footer chrome, not floating.
- [ ] `* All` selection STILL uses asterisk + underline (not colour). `CompileGate` green; verify on Seeker.

## 6. Do NOT
- Do NOT replace the `*`+underline selection with a colour highlight (it's the KEEP).
- Do NOT use fraction bands; do NOT change quest DATA/logic — presentation only.
