# WORK ORDER 693 — Jeweler/Crafting detail pane: mobile readability + compact detail card *(renumbered from WO-683, 2026-07-13 collision cleanup)*

**Status: READY TO IMPLEMENT** (owner report + screenshot 2026-07-12: "text should be set to
readable on mobile; we don't need a huge area on the right" — mockup approved in-chat).
**Lane:** UI (View-only). **Files:** `JewelerPanelMvvm.cs` + `CraftingPanelMvvm.cs` (they share
the FrameCrafting master-detail shape — fix the shared pattern once, apply to both).

## Defects (from the screenshot, verified against the panel family)

1. **Detail text below the readable floor** — the Requires rows / flavor line / cost line render
   ~8px-equivalent on device. The kit's `FitBlock` bounded auto-size keeps a 10px MIN that is
   too small for phone DPI in this pane's tiny bands; the bands themselves are the problem.
2. **Color-only unmet state** — missing requirements read via red text alone (owner is
   red/green colorblind; the colorblind law is BINDING). No have/need counts read at a glance.
3. **Dead space** — the parchment detail well is mostly empty around a micro text block; the
   content should define the card, not float in the frame's full zone.
4. **List rows carry a dangling "–" suffix** (an empty count/state placeholder) — noise.
5. **Instruction line** ("Set gems into a ring…") is a tiny orphan strip under the split.

## The fix (per the approved mockup — one shared detail-card builder)

- **Detail = a compact content-sized card** seated at the TOP of the detail zone (not stretched
  to fill it): item icon plate + name (15px-class) + one flavor line → divider → REQUIRES rows →
  divider → COST chips → the ONE action button. Card height = content; the zone's leftover
  space stays empty parchment (calm, per the busyness directive).
- **Requirement rows, structured:** `[met-glyph] Name …… have/need` — met = check glyph +
  "1 / 1", unmet = X glyph + "0 / 2"; state carried by glyph + count, color is reinforcement
  only. Row font = label class (13px-equivalent), never below the floor.
- **Font floor raised for detail surfaces:** minimum readable size on small screens becomes a
  kit constant (one place, e.g. `ElarionUi.FontFloorMobile`) used by `FitBlock`/`FitSingleLine`
  callers in detail panes — bands must GROW or content must truncate-with-ellipsis rather than
  shrink below the floor. No per-screen font literals.
- **BESTOWS section (owner addition 2026-07-12: "it has to provide value").** Above REQUIRES:
  the stat/ability grants the piece bestows, as structured rows (`[stat-glyph] Max health …
  +50`), plus a rarity chip by the name and a quiet "Requires hero level N" row. **Data-only —
  the fields already exist in `accessories.json`** (verified: `amulet_elarion` carries
  `hpBonus: 50`, `defense: 0.07`, `rarity: epic`, `req.level: 10`, `flavor`). The VM relays the
  def's bonus fields; render every non-zero bonus generically (hpBonus/defense/…) so new
  accessory stats appear without View edits (One Model: the card is a READER of the def).
  Positive grants = check-glyph green class + text value; never color-only.
- **Cost as currency chips** (the mirrored `currency_*` icons — same grammar as WO-675/676).
- **CTA carries the blocker:** disabled Set Gems reads "SET GEMS — missing 2 gems" (one-action
  law; the tiny instruction strip is DELETED — its content lives on the CTA/empty-state).
- **List rows:** drop the dangling "–"; right-align a readable state ("Ready" + check, or
  "2 of 3" progress). Selected row = gold rim (existing selection treatment).
- **Empty-state** (nothing selected): the detail card shows "Select a piece to inspect" +
  the one-line explanation — reusing the skill tree's empty-fold pattern.

## Gates / acceptance
- [ ] On a phone-aspect viewport (~390px logical width class): every string in both panels ≥
      the new floor; requirements readable at arm's length (owner judges on device).
- [ ] Unmet/met requirement state readable with color stripped (glyph + counts).
- [ ] Every craftable piece shows its BESTOWS rows from accessories.json (spot-check: Elarion
      Amulet shows +50 max health / +7% defense / level 10); a piece with no bonuses shows none
      (no empty header).
- [ ] No dangling "–" rows; disabled CTA names why; instruction strip gone.
- [ ] Desktop unchanged in structure (same frame, same split; only the detail card + type).
- [ ] Screenshot-vs-mockup verify (canon §7) on Jeweler AND Crafting; COMPILE_GATE_OK +
      fleet 13/13 panels + popup-close clean + owner felt-pass ON A PHONE (PO closes).

## What NOT to touch
Recipe/craft logic + VMs (View-only) · FrameCrafting zones in `ZonesFor` unless the footer/
close reservation demands it (then factory-level only) · other panels (adopt-on-touch later).

*Cross-refs:* mockup approved in-chat 2026-07-12 · `docs/UI_BLINK_TEMPLATE_CANON.md` ·
WO-675/676 (shared chip/state grammar + the owner's busyness directive) · WO-680 A1 (footer
reservation pattern).
