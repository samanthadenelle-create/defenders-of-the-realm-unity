# WORK ORDER 552 — Store / Inventory Screens: Object-Alignment + Obsidian Styling Audit

**Status: READY TO IMPLEMENT (audit complete; one fix applied this pass)**
**Owner-note:** WO number 552 is provisional — slot into a lane in
`MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md` (next-free authority,
NOT the filesystem max).
**Date:** 2026-06-28
**Lane:** Presentation / UI (HP B2B — presentation layer only; no object/data touched)

---

## 1. Goal

Audit **every** store / shop / item-grid screen for the two defects the regular inventory
recently carried, and bring each to the EquipmentPanel Obsidian gold-standard:

1. **GRID-ALIGNMENT BUG CLASS** — a uGUI `GridLayoutGroup` that never sets
   `constraint = GridLayoutGroup.Constraint.FixedColumnCount` (defaults to `Flexible` →
   `constraintCount` ignored → cells collapse / don't align), AND/OR scroll content built
   without a forced layout pass (`Canvas.ForceUpdateCanvases()` +
   `LayoutRebuilder.ForceRebuildLayoutImmediate(viewport+content)`).
2. **OBSIDIAN STYLING** — dark obsidian backing, `ElarionUiKit.PanelFramed` ornate frame,
   `ElarionUiKit.Header` gold titles, `RpgUiCatalog` item-cell plates, `ElarionUiKit.ButtonPack`
   buttons, `ElarionUi` parchment/gilt text — Obsidian as the PRIMARY look, not a fallback.

---

## 2. Headline finding

**The codebase is already overwhelmingly compliant.** The grid-alignment bug class exists in
exactly ONE place in the whole project — and it is already fixed:

- **Only ONE production `GridLayoutGroup` exists**: `InventoryGrid.cs` (the regular inventory),
  and it ALREADY sets `constraint = FixedColumnCount` + `constraintCount` (5 landscape / 4
  portrait) and runs `Canvas.ForceUpdateCanvases()` + `ForceRebuildLayoutImmediate(viewport, content)`
  (InventoryGrid.cs:55-56, 70-73). No other store/shop/grid screen uses a uGUI `GridLayoutGroup`,
  so there are **no other instances of the FixedColumnCount defect to fix.**
- Every uGUI list-style store/shop screen uses `VerticalLayoutGroup` + `ContentSizeFitter` +
  per-row `LayoutElement` + a `FinalizeScroll()` that does the forced-layout pass — the proven
  anti-collapse mechanism. All correct.
- All uGUI store/shop surfaces already use the EquipmentPanel Obsidian kit
  (`PanelFramed(PanelVendor / PanelWindowDark)`, dark obsidian solid-fill, `Header`, `ButtonPack`,
  `RpgUiCatalog` slot plates, `ElarionUi` text).
- The remaining "store" surfaces (PackStore, CosmeticShopPanel, VillageCraftingPanel, BuildMenu,
  BuildPaletteUI, dungeon CraftingPanelController) are **UI Toolkit** (UXML/VisualElement), a
  DIFFERENT rendering tech — they have no uGUI `GridLayoutGroup` and so cannot carry this bug.
  They are already themed via `ElarionUi` / `ShopTheme` (dark stone + runic gold). See §4 note.

**One genuine consistency gap found & fixed this pass:** CraftingPanelMvvm recipe cards used a
plain `Cell` tint instead of the `RpgUiCatalog` Obsidian per-item slot plate the other item
surfaces use. Brought to the slot-plate standard (sprite-first, Cell fallback). See §3.

---

## 3. Per-screen ledger

| Screen | File | Tech | Mis-aligned? | Off-style? | Action |
|---|---|---|---|---|---|
| **EquipmentPanel** (Gear Preview) | `Village/Hero/EquipmentPanel.cs` | uGUI | No — slot plates anchored; drawer list `VerticalLayoutGroup` + `FinalizeScroll` (534-538) | No — the GOLD STANDARD | None |
| **Regular inventory** (reference) | `Village/Hero/InventoryGrid.cs` | uGUI | No — `FixedColumnCount` + `constraintCount` + forced layout (55-73) — already fixed | No | None |
| **ShopPanel** (vendor/gear shop) | `Village/Hero/ShopPanel.cs` | uGUI | No — `VerticalLayoutGroup` + `FinalizeScroll` (615-622); guarded by `ShopPanelRowRenderTests` | No — `PanelFramed(PanelVendor)`, dark obsidian fill, slot-plate rows | None |
| **PartyShopPanelMvvm** | `Village/Hero/PartyShopPanelMvvm.cs` | uGUI | No — `VerticalLayoutGroup` + `FinalizeScroll` (712-719) | No — `PanelFramed(PanelVendor)`, slot-plate rows + preview backing | None |
| **TroopTrainingPanel** (vendor-style) | `Village/Hero/TroopTrainingPanel.cs` | uGUI | No — `VerticalLayoutGroup` + `FinalizeScroll` (331-337) | No — `PanelFramed(PanelWindowDark)` | None |
| **CraftingPanelMvvm** (Alchemy) | `Village/Items/CraftingPanelMvvm.cs` | uGUI | No — manual anchor-math grid (3-col), self-aligning, no layout group needs no forced pass | **Minor** — recipe cards used `Cell` tint, not the Obsidian slot plate | **FIXED** — slot-plate sprite-first dressing, `Cell` fallback (see §5) |
| **CosmeticShopPanel** | `HUD/CosmeticShopPanel.cs` | **UI Toolkit** | N/A — vertical `ScrollView`, no uGUI grid | No — `ElarionUi.StylePanel(dark)` + `ShopTheme` cards | None (see §4) |
| **PackStore** (5-pack store) | `Wallet/PackStore.cs` | **UI Toolkit** | N/A — vertical `ScrollView` | No — code-built `ShopTheme` scaffold (UXML bypassed per §8) | None (see §4) |
| **VillageCraftingPanel** (Workshop) | `Village/Crafting/VillageCraftingPanel.cs` | **UI Toolkit** | N/A — flex column list | No — `ElarionUi.StylePanel(dark)` + wells | None (see §4) |
| **BuildMenu** | `Village/Buildings/UI/BuildMenu.cs` | **UI Toolkit** | N/A — flex list / code fallback | No — `ElarionUi.StyleButton/StylePanel` | None (build-mode tool, not an item store) |
| **BuildPaletteUI** | `Village/BuildMode/BuildPaletteUI.cs` | **UI Toolkit** | N/A — horizontal `ScrollView` strip | No — `ElarionUi.PanelStone/StyleButton` | None (build-mode tool) |
| **CraftingPanelController** (dungeon) | `Dungeons/UI/CraftingPanelController.cs` | **UI Toolkit** | N/A — UXML cells | No — `CraftingPanel.uss` themed | None (see §4) |

---

## 4. Flagged for OWNER decision (not actioned)

The **UI-Toolkit** store surfaces (PackStore, CosmeticShopPanel, VillageCraftingPanel,
dungeon CraftingPanelController, BuildMenu, BuildPaletteUI) are styled with the
`ElarionUi` / `ShopTheme` UI-Toolkit theming, NOT the uGUI `RpgUiCatalog` Obsidian sprite kit.
They are visually dark-stone + runic-gold and internally consistent, but they are a SEPARATE
visual language from the uGUI Obsidian panels (EquipmentPanel etc.). Bringing them onto the
*exact* `RpgUiCatalog` sprite-plate look would require porting them from UI Toolkit to code-built
uGUI — a large, structural effort, not a styling tweak — and PIPELINE_STATE §8 warns UXML renders
empty in player builds (PackStore already code-builds its scaffold to dodge this).

**Recommendation:** treat UI-Toolkit → uGUI Obsidian unification as a separate, scoped WO if the
owner wants pixel-identical Obsidian across BOTH tech stacks. Out of scope for an
alignment/styling pass. No action taken here.

---

## 5. Fix applied this pass

**`Assets/_Modules/Village/Items/CraftingPanelMvvm.cs`** — `BuildRecipeCard` (~line 142):
recipe cards now dress sprite-FIRST with the `RpgUiCatalog.Get(RoleSlot, SlotItem)` Obsidian
per-item plate (matching EquipmentPanel / InventoryGrid / ShopPanel item cells), with the
existing `ElarionUiKit.Cell` procedural tint kept as the WebGL-safe fallback when the pack art
is absent. Additive presentation only — no MVVM/data binding touched. Brace check: **OK (29)**.

---

## 6. Acceptance criteria

- [x] Every store/shop/grid screen enumerated (grep `GridLayoutGroup`, `ScrollRect`,
      `VerticalLayoutGroup`, `ForceRebuildLayoutImmediate`, shop/store/crafting class names).
- [x] Every uGUI `GridLayoutGroup` verified for `FixedColumnCount` + forced layout pass — the
      only one (InventoryGrid) is correct.
- [x] Every uGUI store list verified for the forced-layout pass after populating — all present.
- [x] Obsidian styling verified against the EquipmentPanel standard on all uGUI surfaces.
- [x] One genuine gap (CraftingPanelMvvm plate) brought to standard.
- [x] No data binding / MVVM altered (layout + style only).
- [x] §5 assembly rules respected (DeNelle.Village → DeNelle.Core.UI only).
- [x] Brace balance passes on the touched file.

## 7. What NOT to touch
- Do not port the UI-Toolkit screens to uGUI under this WO (§4 — separate scoped WO).
- Do not re-edit InventoryGrid / EquipmentPanel / ShopPanel — already compliant.
- Do not alter any VM / catalog / economy logic.
