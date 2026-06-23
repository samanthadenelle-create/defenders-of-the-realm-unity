# WORK ORDER 175 — Store / Shop Visual Polish — RESULT

**Status: CODE COMPLETE — pending CLI build-verify**
**Date:** 2026-05-31
**Lane:** UI / polish (code-built UI styling). No bake, no scene edit, no gameplay/data/logic change.

---

## Premise check
Premise holds. The monetization stack is already built (per project memory): the
playtested "Cosmetic Shop" is `CosmeticShopPanel` (DeNelle.HUD, C-key) and the IAP
store is `PackStore` (DeNelle.Wallet, opened by `MarketplaceInteractor` F-key in the
village). Both are already code-built UI with the UXML-ignore build fix in place.
This WO is a **re-skin only** — no greenfield, no re-wire, no re-enable needed
(scene-wiring is intact; the "needs its own PanelSettings" caveat is a separate
pre-existing concern, NOT a blocker for this styling pass).

## What changed & why
Created one shared Elarion shop identity and applied it to both shop surfaces, so
they read as one authored merchant boutique (wood-and-aether frame, gilt accents,
violet/aether highlights, gold Glimmer) instead of two generic dark dialog boxes.

### Files changed
1. **`Assets/_Modules/Core/UI/ShopTheme.cs`** (NEW) — shared static theme helper.
   - Lives in `DeNelle.Core` because both `DeNelle.HUD` and `DeNelle.Wallet` already
     reference Core and neither references the other — so the identity is shared with
     NO new cross-module dependency.
   - Palette (wood/aether/parchment/gilt/glimmer) + helpers: `StyleScrim`,
     `StylePanelFrame` (carved-wood frame), `MakeTitle` (crest + display title),
     `MakeRule` (gilt rule), `MakeGlimmerChip` (coin glyph + gold amount),
     `MakeIconSlot` (framed item-portrait slot — real preview render if present,
     else a framed gem tile, never a bare swatch), `StyleButton` (Confirm/Aether/
     Disabled with hover/press feedback via pointer callbacks — USS `:hover` doesn't
     load in builds), `StyleCloseButton` (round "✕"), `StyleTab` (selected/unselected
     with a gilt active marker), `StyleChip`, `StyleScrollWell` (themed well +
     **hides the default OS scrollbar**), `StyleCard`. All **inline-style** based so
     it survives the project's "UXML/USS doesn't render in player builds" trap.
   - Mobile-first: buttons ≥ 40–42 px tall, legible font sizes.

2. **`Assets/_Modules/HUD/CosmeticShopPanel.cs`** (the playtested screenshot panel)
   - Themed frame + crest title; Glimmer is now a gold **coin chip** (was plain
     "Glimmer: N" text); round themed **✕** close.
   - Item rows: flat color swatch → **framed item-portrait slot** (`MakeIconSlot`).
     A new `ResolvePreviewTexture(id)` loads `Resources/Cosmetics/Previews/<id>` when
     present (later art pass) and falls back to a framed gem tile until then.
   - Category tabs get a clear selected state (aether glow + gilt edge); Buy/Equip/
     Equipped/Locked buttons themed with states; price line gets a coin glyph.
   - **Default OS scrollbar replaced/hidden** via the themed scroll well.

3. **`Assets/_Modules/Wallet/PackStore.cs`** (the IAP store — extends the identity)
   - Same themed frame, crest title, gilt rules, themed scroll well (OS scrollbar
     hidden), themed cards, currency-rail **chips** with selected state, themed
     **Buy** (coin glyph + green) and **Owned** state.
   - Close button moved into a themed top-right **✕** in the header (was a plain
     bottom "Close").
   - Removed the now-unused local `StyleButton(Button, Color, Color)` helper (all
     buttons route through `ShopTheme`).

4. **`Assets/_Modules/Village/Buildings/MarketplaceInteractor.cs`**
   - `InjectCloseButton` now no-ops when PackStore's own themed `store-close-btn`
     exists, so the shop window doesn't get a second, unthemed close button.
     (Logic/wiring otherwise unchanged; the fallback injection still runs for any
     path that doesn't self-provide a close.)

## Re-skin only — confirmed
Layout, catalog data, purchase/equip logic, wallet flow, analytics, close/escape
wiring, and all public surfaces (`Render`, `Purchase`, `SetWalletService`,
`ToggleOverlay`, `PackPurchased`) are **unchanged**. Pure styling + one icon-slot
swap.

## In-editor step (CLI / owner) — OPTIONAL, not required for this WO
Nothing is required to make the re-skin take effect — it's all runtime code. Two
OPTIONAL follow-ups, neither blocks WO-175:
- **Real item previews (recommended):** drop sprite/render textures at
  `Assets/Resources/Cosmetics/Previews/<cosmetic-id>.png` (e.g.
  `Cosmetics/Previews/hero-mage-embergrove`). When present, `MakeIconSlot` shows the
  render automatically; when absent it shows the framed gem fallback. No code change.
- **PackStore re-enable / own PanelSettings:** still a separate pre-existing task
  (PIPELINE_STATE §8). Not touched here and not needed for the visual polish.

## Risks
- Low. All APIs used (`letterSpacing`, `ScaleMode.ScaleAndCrop`,
  `ScrollerVisibility.Hidden`, `verticalScroller` width collapse, pointer-event
  hover) are already used elsewhere in the project, so they compile in this Unity
  version. No reflection-bridge changes; the CosmeticShopPanel↔Cosmetics reflection
  contract is untouched.
- `MakeIconSlot` with `Resources.Load` runs once per cosmetic id (cached); a missing
  Resources path returns null cleanly (framed gem fallback), no error spam.

## Test steps (CLI build-verify then owner/Tricia playtest)
1. Compile-verify (CLI batchmode) — expect clean compile across DeNelle.Core,
   DeNelle.HUD, DeNelle.Wallet, DeNelle.Village.
2. In a hero scene, press **C** → Cosmetic Shop opens with the wood/aether frame,
   crest title, gold Glimmer coin chip, framed icon slots (gem fallback), themed
   tabs with a clear selected state, themed Buy/Equip buttons, and **no grey OS
   scrollbar**.
3. Walk to the Marketplace stall, press **F** → Realm Store opens with the same
   identity (frame, crest, rules, chips, themed Buy, top-right ✕). Confirm there is
   exactly **one** close button.
4. Confirm Buy/Equip/currency-rail selection and Escape/close still behave exactly
   as before (logic unchanged).

## Quality gate
- Brace balance: ShopTheme 26/26, CosmeticShopPanel 90/90, PackStore 80/80,
  MarketplaceInteractor 23/23 — all balanced. No leaked junk tags. No `.unity`
  hand-edit, no bake, no commit/push/build performed (per constraints).
