# WORK ORDER 881 — Lore Reading modal: long text clipped by Close (no scroll) + Alduin/Aldwin name

**Status:** READY. **Lane:** HUD/UI + data — `LoreReadingModal` (+ the lore text source). **WO#:** UI-seat block; **881**.
**Source:** `docs/ui-review/screens-2026-08-04/LoreReadingModal_2340x1080.png`.

## 1. Bad (from the capture)
- **Layout:** the lore body overflows — the second paragraph ("It will grow into something old and quiet. The Folk
  used…") is **cut off mid-line by the Close button / modal bottom.** Long entries have **no scroll**, so content is
  lost behind Close.
- **Data/copy:** the title reads **"ALDUIN'S JOURNAL"** but the Echo is **"Aldwin"** (Aldwin, the Ice Echo). Likely a
  typo (Alduin ≠ Aldwin) — confirm and fix at the copy source, not the View.

## 2. Fix — scroll in the View; copy in the data (MVVM law)
- **Layout (View):** put the lore body in a **scroll well** (RectMask2D + vertical ScrollRect) between the title and a
  fixed footer band that holds Close — so any-length entry scrolls and Close never overlaps the text. Fixed-pixel
  title/footer bands.
- **Copy (data, NOT the View):** if "Alduin" is a typo for "Aldwin", fix it in the **lore/string source** (the journal
  copy the VM feeds), not by hardcoding in the View. The View renders whatever text it's given — it does not author or
  correct copy.

## 3. Acceptance
- [ ] On-device: a long lore entry scrolls fully and is never clipped by Close; Close in a fixed footer band.
- [ ] The journal name matches the Echo (Aldwin), fixed at the copy source. `CompileGate` green. Verify on Seeker.

## 4. Do NOT
- Do NOT clip long text; do NOT correct copy inside the View (fix the source). No fraction bands.
