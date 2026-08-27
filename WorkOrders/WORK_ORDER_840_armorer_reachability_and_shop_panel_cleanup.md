# WORK ORDER 840 — Armorer (armor-only) reachability + Shop panel UI cleanup

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
reachability gap is REAL but its one-line fix lives in `CastleVendorNpcInjector.AnchorRoles` (fenced to
another agent this wave — exact fix documented in the implementation note at the bottom).
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** Village vendor data + HUD/UI. Panel = `PartyShopPanelMvvm.cs` (the live shop; `ShopPanel.cs` is legacy/off).
**Origin:** owner felt-test 2026-08-02, "The Forge" screen — *"Purchase from Armorer should only be armor and needs
the UI cleaner."* Ties to the morning UI review (`docs/qa/UI_REVIEW_2026-08-01.md`, party_shop).

---

## PART A — "Armorer should only be armor" (RCA: the FILTER is already correct; the gap is REACHABILITY)

**Do NOT add a category filter — it already exists and is correct.** Verified read-only:
- Vendor stock is a per-vendor query keyed by a `vendorContext`, not "show everything".
- `Assets/Resources/Data/Canonical/vendors.json` (+ StreamingAssets mirror) already declares:
  `armorer → categories:["armor"]` (`vendors.json:36-46`) and `forge → categories:["weapon"]` (`vendors.json:25-35`).
- `VendorStockContract.AllowedFor(context)` (`VendorStockContract.cs:85-134`) → `VendorStockResolver.Resolve(...)`
  (`VendorStockResolver.cs:203-325`) enforces those bands (armor branch `:237-251`, weapon `:221-235`).
- **"The Forge" is the WEAPON vendor** (`vendors.json:27` displayName), and showing weapons there is **correct**.
  The Armorer is a *separate* context that already resolves to armor-only.

**The real issue = the Armorer may not be reachable, so the Forge is the only gear vendor the player can open.**
`CastleVendorNpcInjector.cs:250` (`AnchorRoles`) notes `("Blacksmith","armorer")` has *"no placed armorer catalog
row yet (L1) — awaits one."* i.e. the blacksmith→armorer NPC/building may not be placed/reachable in the world.

### A. Task (verify-first, §12)
1. **Confirm** whether an Armorer vendor NPC (role `blacksmith` → context `armorer`) actually spawns and is
   interactable in the town scene, and whether opening it resolves the armor-only catalog. Headless/log-verify.
2. If reachable → open it, confirm armor-only, and this part is DONE (no code change; close as verified).
3. If NOT reachable (the L1 "awaits one" gap) → place/enable the Armorer vendor so the player can buy armor:
   ensure the `blacksmith`/`armorer` anchor row exists and the NPC is injected (`CastleVendorNpcInjector` +
   the vendor placement/catalog row). Keep the existing `armorer → ["armor"]` mapping — do not touch the filter.
4. `OWNER CONFIRM`: is the Forge (weapons) + a separate Armorer (armor) the intended split (WO-444 says yes), or
   should ONE smith sell both? Default = keep them split (data already models it); this WO just makes the Armorer
   reachable. Flagged.

**Do NOT** re-implement or duplicate the vendor category filter — it exists and is correct (`vendors.json` +
`VendorStockContract`/`VendorStockResolver`). Re-adding it would be a redundant second source of truth.

---

## PART B — Shop panel UI cleanup (`PartyShopPanelMvvm.cs`)
All issues + anchors verified in `Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs`:

1. **Hero name "Grom" overlaps the portrait.** The per-chip name Label (`:655`, chip-local Y `0.02–0.40`) sits under
   the portrait Image (`:641–644`, Y `0.42–0.95`) on a capped-width chip (`chipW=min(w,0.16)`, `:624`) in the short
   party band (`pb 0.80–0.90`, `:402`) — it crowds the portrait's lower edge. The selected-member sub-header
   `_memberLabel` "Grom - Knight (Lv N)" (`:406–417`, set `:307`) is ALSO shown, so the name is redundant.
   **Fix:** hide the redundant per-chip name when the sub-header already shows it, and/or give the name its own band
   (taller party bar / name below the chip) so it never overlaps the portrait.

2. **"All" tab gold pennant overlaps the tab row.** `_categoryBar` (All/Armor/Weapons, `cb 0.705–0.748`, `:437`)
   and `_typeBar` (All/1h/Shield/2h, `tyb 0.655–0.70`, `:449`) leave only ~0.005 gap, so the selected chip's gold
   plate overdraws the neighbor row. **Fix:** widen the vertical gap between `cb` and `tyb` (or inset the selected-
   plate art) so the pennant can't bleed.

3. **Truncated action buttons "Impro…" / "Uneq…".** Three buttons packed into narrow slots — Improve `0.04–0.28`
   (`:510`), Purchase/Sell `0.30–0.60` (`:480`), Equip/Unequip `0.64–0.86` (`:491`) — with `0.86–1.0` left EMPTY.
   Labels ("Improve", "Unequip", "Purchase NNN Gold") ellipsize. **Fix:** widen the button rects to use the empty
   right margin and/or shorten labels ("Improve"→ok, "Unequip"→ok if wider) so nothing truncates. Min touch size kept.

4. **Tall list, only ~2 items → reads empty.** The list column (`cr 0.04–0.52 / 0.23–0.645`, `:459`) is tall but the
   roster-filtered V1 stock (knight-only, `VendorStockResolver.cs:102,228`) is small. This is content/filter behavior,
   not a render bug. **Fix (layout only):** vertically-center or shrink the list column when the row count is low so
   the column doesn't read as mostly-empty black. Do NOT change the roster filter.

5. **Garbled bottom coin chip (broken icon; "1m").** "1m" is correct (`ElarionUi.CompactNumber` of a large gold
   total). The BROKEN icon is the currency glyph sprite failing to resolve inside `ElarionUiKit.CurrencyChip`
   (built `:390`, amount set `:310`). **Fix lives in the shared kit** (`ElarionUiKit.CurrencyChip` / the `RpgUiCatalog`
   coin sprite key), NOT this panel. NOTE: this is a shared-kit fix — it also fixes the same garbled coin/resource
   chips seen in Inventory/Party in the morning review. Read `ElarionUiKit` to find the missing sprite key.

## Files to edit
- `Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs` — B1–B4 (anchors/labels/list-centering).
- `Assets/_Modules/Core/UI/ElarionUiKit*.cs` — B5 (currency-chip icon resolve; shared — verify other panels).
- Part A: `Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs` (+ the armorer anchor/catalog row) ONLY if the
  Armorer is unreachable. `vendors.json` mapping stays as-is.

## Acceptance criteria
- [ ] Opening the ARMORER shows armor only (verified — already the data contract); the Armorer is reachable in town.
- [ ] The Forge continues to show weapons only (unchanged, correct).
- [ ] Shop panel: hero name never overlaps the portrait; the category/type tab pennants don't overlap; action buttons
      show full labels (no "Impro…/Uneq…"); the list column doesn't read as empty with few items; the coin chip icon
      renders (no broken glyph).
- [ ] `CompileGate` green; `RunCaptureHeadless` party_shop (editor CLOSED) confirms the cleanup.

## Do NOT
- Do NOT add/duplicate the vendor category filter (already correct in `vendors.json` + `VendorStockContract`).
- Do NOT change the roster/stock filter to "fill" the list — fix emptiness with layout only.
- Do NOT hand-edit scenes; keep `vendors.json` Resources/StreamingAssets copies byte-identical if touched.

---

## Implementation note — 2026-08-02 (edit-only agent, code-verified)

### Part A verdict: filter CORRECT (re-verified), Armorer NOT reachable — RCA + exact fix
- Filter re-verified from code: `vendors.json` `armorer -> ["armor"]` (:37-46), `VendorRegistry` loads it,
  `VendorStockContract.AllowedFor` armor branch (:105) enforces it. Untouched, as directed.
- The WO's premise "no placed armorer catalog row yet" is STALE: `structures-catalog.json:746` HAS the
  `armorer` row (displayName "Blacksmith", GameplayBuilding, singleton, cost 130) — added by WO-673 L4.
  Both catalog copies carry it.
- The REAL reachability chain, all verified from code, is triple-blocked:
  1. **Palette:** WO-707 grooming LOCKED id `armorer` out of the Town palette (`build-categories.json`
     lockedIds + `BuildCategoryRegistry.BuildFallback`), reasoning "armor=Armorer via id forge". That
     reasoning is FALSE at the vendor layer (see 3) — so the only placeable route to the armor vendor
     was removed.
  2. **Default-Town/legacy saves:** the injector's Lever-1 baked fallback for role Blacksmith gates on
     `MayBakedTwinSurface("armorer")` (`CastleVendorNpcInjector.cs:445`), but the migration template
     grant (`StrategicPlacementMigration` BakedRows) marks `workshop` ever-built, never `armorer` —
     gate closes post-migration, so the pre-standing town seats NO armor vendor.
  3. **The "Armorer" tile the player CAN place (catalog id `forge`, displayName "Armorer", visual
     `Structures/armorer`, baked twin `Forge_Armor_Storefront`) seats the WEAPONS vendor:**
     `AnchorRoles` maps `("Forge","forge")` -> `VendorFor("forge")` -> context `forge` -> "The Forge"
     weapons shop. This is almost certainly the owner's felt bug: the building labelled/skinned as the
     Armorer opens a weapons shop. Placement-land taxonomy (WO-707 + StrategicPlacementMigration:
     `workshop`=weapons Blacksmith storefront, `forge`=armor storefront) and vendor-land
     (`forge`=weapons context) disagree about what id `forge` means.
- **Exact fix (one line, in the FENCED `CastleVendorNpcInjector.cs` — for the injector-owning agent/CLI):**
  in `AnchorRoles`, change `("Forge", "forge")` to `("Blacksmith", "forge")` (keep
  `("Forge","collector_forge")` and `("Forge","workshop"...)` via `RoleForBuildingId` as the weapons
  seats; also update `RoleForBuildingId`'s implicit reverse map if needed). Then the placed/replayed
  `forge` building ("Armorer" tile, armor-skinned, Forge_Armor_Storefront twin) seats the Blacksmith
  role -> vendor context `armorer` -> armor-only shop, on BOTH placed and Default-Town saves, honoring
  WO-707 one-tile-per-trade. NO palette unlock needed (un-retiring the `armorer` tile would contradict
  WO-707 and was deliberately not done).
- `OWNER CONFIRM` (carried): Forge(weapons)+Armorer(armor) split kept per WO-444; also confirm the
  displayName knot (catalog id `forge` labelled "Armorer" vs vendors.json vendor id `forge` labelled
  "The Forge") — a data-only rename either way once the AnchorRoles fix lands.
- No regression added: the pinned data contract (armorer=armor-only bands) is already asserted by
  `DataRegression.CheckVendorStock`; a reachability oracle would fail-by-design until the AnchorRoles
  fix lands — spelled out here instead of shipping a red gate.

### Part B: all five items implemented
- B1 `PartyShopPanelMvvm.RebuildPartyBar`: the SELECTED member's chip no longer draws the redundant
  name (the `_memberLabel` sub-header carries it); unselected chips keep theirs.
- B2 `BuildChrome`/`CreateCategory`/`RebuildTypeBar`: category bar 0.703-0.744, type bar 0.648-0.690,
  chips inset 0.10-0.90 — real pennant-clearing gaps at every seam of the filter stack.
- B3 action row widened into the empty right margin: Improve 0.02-0.30, Purchase/Sell 0.32-0.68,
  Equip/Unequip 0.70-0.98 (heights/touch size unchanged).
- B4 `CenterShortList` (new, called from `FinalizeScroll`): when stacked rows are shorter than the
  viewport, the slack becomes symmetric layout-group padding — rows vertically centred; taller content
  keeps the plain top-anchored scroll. Roster/stock filter untouched.
- B5 **RCA correction** — the WO's "missing sprite key" hypothesis is DISPROVED: `gold` maps to
  `currency/currency_gold` in concept-icons.json, the PNG is a valid Sprite import and renders clean.
  The garble (per the party_shop capture) is the `element_stat` plate: ~1024px-wide ornate CENTER art
  9-slice-compressed ~7x inside a small chip = chrome scribble noise. `ElarionUiKit.CurrencyChip` now
  renders a flat dark-glass plate with the sprite contributing its chrome BORDER only
  (`fillCenter=false` child frame), plus a slightly larger icon well (0.08-0.92) — shared-kit, so the
  Inventory/Party garbled chips (UI review P2-9) inherit the fix.

Braces balanced (PartyShopPanelMvvm 151/151, ElarionUiKitObsidian 248/248), no NULs, vendors.json
copies untouched and verified byte-identical. Gates (CompileGate + RunCaptureHeadless party_shop) owed
by CLI per pipeline.
