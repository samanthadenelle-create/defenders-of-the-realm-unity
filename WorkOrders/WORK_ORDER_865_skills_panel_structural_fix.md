# WORK ORDER 865 — Skills panel: structural fix (overflow / z-order / truncation) — BROKEN, do first

**Status:** READY TO IMPLEMENT — **P0, the only genuinely broken screen in the 08-04 review.**
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** HUD/UI — the "Grom (Knight) Skills" panel (`TalentTreePanel.cs` / `HeroSkillTreePanelMvvm.cs` — CLI confirm).
**WO#:** UI-seat block (860–899); 860–864 used, **865**=this.
**Source:** `docs/ui-review/2026-08-04-seeker/README.md` §1 + `07-skills-panel.png` (real Seeker capture, 2340×1080).

---

## 1. What's broken (confirmed from the capture)
Structural failure, not styling. Everything below is the **fraction-band failure class** (review §0 / WO-841/852):
percent-of-parent layouts look right in-editor and overflow/overlap at 2340×1080.
- **Grid overflows BOTH edges** — leftmost node cut at the left frame edge, rightmost node cut at the right. The
  grid is wider than the frame that holds it.
- **A node is drawn OVER a label** — "Universal / any class" reads as "Univers[icon]y class"; a skill node overlaps
  the text.
- **Cancel / CONFIRM / Respec float OVER the grid** and over the ability list beneath them (not their own band).
- **"Emberbrand Thro" is truncated** ("Throw"); ability slot **4** is empty/clipped by the Respec button.
- **CONFIRM's green fill bleeds past its own button bounds** to the right.
- **Three button chrome styles in one row** — Cancel (plain), CONFIRM (green fill), Respec 300c (grey box).

## 2. The fix — three DISJOINT fixed-pixel bands + one button language
Lay the panel body as three vertically-stacked **fixed-pixel** regions that cannot overlap (never fraction-of-parent):
1. **Grid region (fixed height, CLIPPED + SCROLLS):** the node grid lives in a `RectMask2D` scroll well sized to the
   frame. It is wider than the frame → it **scrolls horizontally** inside the mask; nodes NEVER paint past the frame
   edges. The "Universal / any class" label gets its own reserved cell so no node overlaps it.
2. **Ability list band (fixed height, reserved, un-overlappable):** Sweeping Cut / Mend / Emberbrand **Throw** (full,
   untruncated) / slot 4 — each in a fixed line box (whole `FontFloor` lines), never clipped by the action row.
3. **Action row band (fixed height, bottom):** Cancel / CONFIRM / Respec sit in their OWN band BELOW the content —
   never floating over the grid/list. Each ≥ `MinTouchPx` (112).
- **One button chrome, differentiated by EMPHASIS not chrome:** e.g. CONFIRM = primary (filled), Cancel = quiet
  (text/outline), Respec = secondary (outline) — but one visual language, not three boxes. Fix the CONFIRM fill so it
  cannot bleed past its bounds (size the fill to the button rect, not an oversized overlay).
- Right column (WISDOM 169 / SELECTED TALENT / the "Requires Legendary Vanguard" line) stays; keep it in its own fixed band.

## 3. Binding (review §0)
Fixed-pixel bands only; `MinTouchPx = 112`; **text-encoded state, never colour alone** (owner colourblind); ASCII-only
TMP (no glyph icons → tofu); strict MVVM (`[ui-mvvm]` ratchet `HardFailOnNew=true` — no new reflection bridge / no
`static_gate.py` entry); landscape only. Do NOT swap one fraction layout for another (it regresses on the next aspect).

## 4. Acceptance
- [ ] On the Seeker at 2340×1080 (`adb shell screencap` → open the PNG): no node cut at either frame edge; the grid
      scrolls inside a clipped region; no node overlaps a label.
- [ ] Cancel/CONFIRM/Respec sit in their own band below the content — nothing floats over the grid/ability list.
- [ ] "Emberbrand Throw" reads in full; slot 4 is visible and not clipped by Respec.
- [ ] CONFIRM's fill stays within its bounds; one button language, differentiated by emphasis.
- [ ] `CompileGate` green. (`UI_CAPTURE_OK` is necessary but NOT sufficient — verify on-device, per review §0/§6.)

## 5. Do NOT
- Do NOT fix overflow by shrinking to fractions — use fixed-pixel bands + a scroll mask.
- Do NOT change the talent DATA / commit logic — layout only.
- Do NOT introduce a new reflection bridge or `static_gate.py` allowlist entry (MVVM ratchet armed).
- The "Calls and notifications will vibrate" pill is an Android system toast — NOT ours; ignore it.
