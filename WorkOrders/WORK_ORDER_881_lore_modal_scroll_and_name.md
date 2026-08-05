# WORK ORDER 881 — Lore Reading modal: long text clipped by Close (no scroll) + Alduin/Aldwin name

**Status:** READY. **Lane:** HUD/UI + data — `LoreReadingModal` (+ the lore text source). **WO#:** UI-seat block; **881**.
**Source:** `docs/ui-review/screens-2026-08-04/LoreReadingModal_2340x1080.png`.

> ## ⚠ CORRECTION 2026-08-05 — §1/§2/§3's "Alduin is a typo" premise is WRONG. NOT ACTIONED.
> **Alduin** and **Aldwin** are TWO DIFFERENT CHARACTERS, both authored canon, one letter apart:
> - **Alduin the Mournful** — the necromancer boss, once a healer. Authority: `canon-strings.json:26`
>   `"alduin": "Alduin the Mournful"` (+ `:27 "alduinTitle": "the Necromancer"`);
>   `docs/narrative-bible.md:57,367`; `en.json` boss/victory lines; `enemies.json` "Alduin's Necromancer".
>   **The journal in the Healer's Cottage is HIS** — `docs/DUNGEON_DESIGNS.md:35,40` ("the journal she's
>   been reading is **Alduin's** — the handwriting changes in the last pages"), and every fragment in
>   `lore-fragments.json` carries `"speaker": "Alduin the Mournful"`.
> - **Aldwin, the Ice Echo** — Echo #1, the founding Echo. Authority: `EchoRosterCatalog.cs:141`.
>
> "ALDUIN'S JOURNAL" on that capture is **CORRECT**. Renaming it to Aldwin would attribute a
> necromancer's suicide note to the player's founding companion. **No copy was changed** — the
> data (both canonical copies) is untouched and byte-identical. `DungeonLoreReadableRegression`
> now PINS both spellings at their sources so neither can be "typo-fixed" into the other.
> §2's layout half was implemented as written; §3's second acceptance line is void.

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
