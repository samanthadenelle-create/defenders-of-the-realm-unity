# WORK ORDER 433 — Shop Blink cohesion: Obsidian Purchase button + active-row hold

**Status: READY TO IMPLEMENT** · Follow-on to WO-431 (MVVM) + WO-432 (slot plates).
**Lane:** cosmetic / presentation, flag-gated. Behavior-preserving.
**Why:** owner felt-test (flag ON) — after WO-432 the panel reads Obsidian-dark, but the
**Purchase** button is still the **gold** slab (`button_gold`), the only element from a
different Blink art family → "feels off / out of family." Also the selected row doesn't
visibly "hold" (no active-row highlight), so the viewer↔row link reads weak. Close both.

---

## Part A — Purchase button into the Obsidian family (flag-gated)

The Blink Obsidian set ships colored action buttons (`Buttons_Obsidian/Rounded1_Green` /
`Rectangle1_Green` = the "confirm/buy" button — obsidian-toned green, not gold). Use the
**green confirm** sprite for Purchase when `FeatureFlags.BlinkChrome` is ON; keep
`button_gold` when OFF.

1. Identify the source sprite behind `Rounded1_Green` (preferred) or `Rectangle1_Green`
   under `Assets/Blink/Art/UI/Obsidian_UI/` (open the prefab, follow its Image sprite GUID
   to the .png). Confirm the path exists.
2. Add it to `Assets/Editor/BlinkUiImporter.cs` → role `button`, canonical name
   `button_confirm` (mirror the existing `button_gold` entry incl. its Border).
3. Add `ButtonConfirm = "button_confirm"` to `RpgUiCatalog.cs` (next to `ButtonGold`).
4. Re-run the importer (batchmode `DeNelle.Editor.BlinkUiImporter.Run`) → lands
   `Resources/RpgUi/button/button_confirm.png` (+ .meta) — LFS texture, committer stages by path.
5. In `ShopPanel.cs`, the Purchase button's `packSpriteName` becomes flag-gated:
   `FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold`.
   Nothing else about the button changes (label, outline, onClick → vm.Buy stay).
   Null-safe: if `button_confirm` doesn't resolve, fall back to `ButtonGold`.

## Part B — Active-row hold (bind to vm.SelectedId)

`ShopVM` already exposes `SelectedId` (selecting a row raises `Changed` → the View re-renders).
Make the View visibly HOLD the selected row:

1. When building rows, record each row's id alongside its row `Image` (a small
   `List<(string id, Image plate)>` or tag the GameObject name with the id — confirm what the
   refactored `CreateRow` already carries).
2. In `Render()` (runs on every `vm.Changed`), tint/frame the row whose id == `vm.SelectedId`
   with a clear "selected" treatment (a brighter plate tint or a gilt edge — reuse the existing
   `TabSelectedTint`/accent so it matches), and reset all others to the normal plate look.
3. This is a selection AFFORDANCE (which row is active) — keep it visible in BOTH flag states
   (it's not decorative chrome). Works over the Blink slot plate (flag ON) and the Cell tile
   (flag OFF) alike. Must not change selection LOGIC — only the visual reflection of `SelectedId`.

---

## Acceptance criteria
- [ ] Compile gate green (`COMPILE_GATE_OK`).
- [ ] `button_confirm.png` present under `Resources/RpgUi/button/`.
- [ ] Flag OFF: Purchase is the gold slab, exactly as today (no change).
- [ ] Flag ON: Purchase is the green Obsidian confirm button — reads in-family with the panel.
- [ ] Tapping a row visibly holds/highlights it; tapping another moves the highlight; the
      held row matches what the detail "viewer" shows (both driven by `vm.SelectedId`).
- [ ] Behavior/layout/logic unchanged; ShopVM untouched except (if needed) exposing `SelectedId`
      read-only (it already does). Brace-balanced; flag-OFF look untouched.

## What NOT to touch
- No ShopVM logic changes; no scroll-mechanism changes; no restyle when flag OFF.
- Don't alter the row click→`vm.Select`→`vm.Buy` wiring; Part B is visual reflection only.
- Edit `.cs` via Write/Edit on the Windows path only (§0). Do not commit (lead commits by path;
  `button_confirm.png` is LFS).

## Gate note (§2c)
Cosmetic + a visual binding of existing state — owner flag-ON play-capture is the verification;
no new unit test required unless a computed value is introduced.

*Cross-ref:* WO-431/432, `docs/UI_MVVM_BINDING_MAP.md §3`, `docs/BLINK_UI.md`.
