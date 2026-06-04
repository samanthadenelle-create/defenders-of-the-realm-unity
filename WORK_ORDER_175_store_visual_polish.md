# WORK ORDER 175 — Store / Shop Visual Polish: make it look built FOR this game

**Status: READY TO IMPLEMENT**
**Priority:** Medium-High — first-impression / monetization surface; the shop is where players spend, and
right now it reads as a generic dark UI box, not part of Elarion.
**Date:** 2026-05-31
**Lane:** UI / polish — code-built UI styling (`CosmeticShopPanel` / PackStore UI). No bake; no gameplay change.
**Source:** owner playtest (Cosmetic Shop screenshot) — *"not polished to look like a store built for this game."*

---

## The problem
The Cosmetic Shop is **functional but generic** — a flat dark rounded-rect panel with: plain text, flat
**color-swatch placeholders instead of item icons**, default OS scrollbar, default purple buttons, no
world identity. It could be any game's debug menu. It doesn't read as **Elarion's** shop — none of the
medieval-fantasy / cozy-mournful tone the rest of the game has. (Good news: layout, data, and the
"Beauty is earned, never required" line are already right — this is a **skin/restyle**, not a rebuild.)

## The goal
Re-skin the shop so it feels **authored for this world** — a merchant's stall / cosmetic boutique in
Elarion — while keeping the working layout + data. Match the game's art language (low-poly fantasy, the
violet/aether + warm-wood palette already in the world).

## What to polish (all code-built UI styling — no UXML)
1. **Framing / panel** — replace the plain dark rect with a **themed frame**: a parchment/stone/woodgrain
   panel background, a decorative border (banner ribbon, corner flourishes, or a carved-frame 9-slice), a
   proper **title treatment** ("Cosmetic Shop" in the game's display font with a small icon/crest, not
   plain text). Make it read as a *shop window*, not a dialog box.
2. **Item rows** — replace the **flat color swatches with real item icons/portraits** (the cosmetic's
   actual preview — a sprite/render of the Twilight Sprite, Emberkin Pup, Glacierborn Wolf). Even
   placeholder framed icons beat solid color blocks. Frame each in a little item-card slot.
3. **Currency display** — "Glimmer: 5405" gets a **Glimmer icon/coin glyph** + styled treatment, not plain text.
4. **Buttons** — style **Buy** as a themed button (wood/stone/aether, hover/press states, a coin icon +
   price), and the **X** close as a themed corner button. The category tabs (Hero/Pet/Village) get an
   active/selected visual state that reads clearly.
5. **Scrollbar** — replace the default OS scrollbar with a **themed/minimal** one (or auto-hide) — the
   grey native bar is the most "unfinished" tell in the shot.
6. **Tone polish** — keep the lovely flavor text + "Beauty is earned, never required." footer; give them
   the game's typography. Optional: a subtle merchant ambiance (a soft chime on Buy, a gentle panel-open
   animation).

## Constraints
- **Re-skin, don't rebuild** — keep the working layout, the catalog data, the Buy/currency logic. This is
  styling: backgrounds, borders, fonts, icons, button states, scrollbar.
- **Code-built UI** (no UXML — repo rule; PackStore's UXML render trap, PIPELINE_STATE §8). Style via the
  code-built panel.
- Reuse the game's existing palette/fonts/icon set; new art assets (frame, icons) can start as placeholders
  and be upgraded — don't block the restyle on final art.
- Mobile-readable (this is a phone game) — legible at thumb size, tap targets big enough.
- Same treatment should extend to the **PackStore** (the IAP store) so all shop surfaces share the identity.

## Acceptance criteria
1. The shop reads as **part of Elarion** (themed frame, title, palette, typography) — not a generic dark box.
2. Item rows show **real item icons/portraits**, not flat color swatches.
3. Currency, Buy buttons, category tabs, and the close button are **themed** with clear states; the default OS scrollbar is replaced/hidden.
4. Layout + data + purchase logic unchanged (re-skin only); code-built (no UXML); mobile-legible.
5. The visual identity extends to the PackStore so shop surfaces are consistent.
6. Brace balance; no gameplay/bake change.

## Open questions for owner
- **Item icons** — render the actual cosmetic (a small 3D-to-sprite render of the pet/skin), or authored 2D icons? (Recommend rendered previews so the icon = what you get.)
- **Frame style** — parchment/scroll, carved wood, or stone-tablet? (Recommend a wood-and-aether merchant frame to match the village.)
- Does this also restyle the **build menu / upgrade panels** (same generic look), or shop-only for now? (Recommend a shared UI-theme pass eventually; shop first.)

## Done checklist (CLAUDE.md §10)
- [ ] Themed panel frame + title treatment (Elarion identity)
- [ ] Real item icons/portraits (not color swatches); Glimmer icon; themed Buy/X/tabs with states
- [ ] Default scrollbar replaced/hidden; mobile-legible
- [ ] Re-skin only (layout/data/logic intact); code-built (no UXML); extends to PackStore
- [ ] Brace balance; no bake
- [ ] `WORK_ORDER_175_store_visual_polish.RESULT.md` when complete
