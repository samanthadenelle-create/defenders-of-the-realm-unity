# WO-465 — Implement Premium Mobile-First Inventory UI (Horizontal + Portrait) – Match Mockup Exactly

**Status:** 🟢 done (gate validated + committed locally)

**Lane:** 4 — UI/HUD (parallel)

**Priority:** P0 (player-facing inventory for raid-style mobile RPG)

**Source:** This session / owner paste

**Created:** 2026-06-14

**Mandatory reads (per spec):** CLAUDE.md (every session), docs/MASTER_CATALOG.md + docs/MASTER_CATALOG/village-hero.md, docs/ARCHITECTURE_PRINCIPLES.md, PIPELINE_STATE.md, relevant HANDOVER, Assets/_Modules/README.md, the mockup image (landscape premium dark wood + gold provided).

## Acceptance Criteria
- The inventory now matches the attached mockup exactly in styling, layout, and premium dark-wood + rich gold aesthetic.
- Top: header bar with X close (no PLAY leak in tabs).
- Left narrow column: large hero portrait in gold-trimmed oval medallion (blue inner/gold rim), name, "Lvl N", HP + MP (or LVL) bars using Tech P1/P2 + gold accents.
- Main area: scrollable ornate grid, 5 columns landscape / 4 portrait (responsive), cellSize/padding tuned for clean fit, no crowding/empty intrusion.
- Tabs: Weapons / Armor / Outfits / Consumables — gold-trimmed using Profile tab sprites (P1/P2/fill/bg) ONLY; no "Play buttons" baked text.
- Cells: clean RPG tile frames (RpgUiCatalog.PanelInventory) + Tech inner rims/sockets, large centered item icon (ItemIconCatalog or direct), small count in corner, equipped/locked chips where applicable; NO long name labels inside grid.
- Bottom: resource well / icons.
- Heavy use of "Tech hud elements" pack (Profile tabs, Healing tabs H*, Sword glyphs, GreenUielements bars, Rpg icons) + ElarionUiKit (Scrim/PanelFramed/Well/Dress*/Add* + palettes Gilt/Glass/Accent) + RpgUiCatalog for tiles.
- Touch-friendly (large cells, direct tap-to-equip).
- Preserves 100% prior behavior: Open/Close/Toggle/EnsureExists, GearLoadout (EquippedWeapon/Armor, Equip*ById, OnGearChanged), tab state, selection, VillageHud bridges, no new deps on Village from HUD.
- Horizontal + portrait support via anchor layout + isLandscape column count.
- No sidebar list intrusion into grid area; grid owns the space like mockup.
- Code-built uGUI only (Canvas 31000, CanvasScaler 1080x1920 match-0.5, no UXML/UIDocument in builds).

## What NOT to touch
- Do not hand-edit Village.unity or any .unity
- Do not alter core gameplay (GearLoadout, VillageInventory, Economy)
- Do not change cross-assembly (keep DeNelle.Village → Core only; use CoreServices where needed)
- No new reflection in bridges beyond existing patterns
- Keep the split partials thin (orchestration in main, impl in UIBuilder/PaperDoll/Grid/Sidebar)

## Files to edit / add (explicit)
- Assets/_Modules/Village/Hero/HeroInventoryController.cs (partial, orchestration + SafeRun + Subscribe + tab/grid/paperdoll rebuild hooks)
- Assets/_Modules/Village/Hero/InventoryUIBuilder.cs (BuildRoot full redesign to mockup proportions + header + close + left paperdoll niche + tabs row + _gridRoot Well + footer; BuildTabs using Profile sprites only)
- Assets/_Modules/Village/Hero/InventoryPaperDoll.cs (RebuildPaperDoll: medBand P1 + AddInnerRim + AddCircle + AddCircleRim for gold oval, large glyph, AddLabel Lvl/Name/LV, PaperDollBarTech HP/LVL only — no EQUIPMENT list)
- Assets/_Modules/Village/Hero/InventoryGrid.cs (RebuildGrid + Build*Cells: viewport/ScrollRect/RectMask + GridLayoutGroup FixedColumnCount (5 landscape/4 portrait), cell ~78x72; BuildGearCell using RpgUiCatalog.PanelInventory + Tech H3 + rims + SelGlow + icon + num + chips; onTap → Equip*ById + RebuildGrid + RebuildPaperDoll)
- Assets/_Modules/Village/Hero/InventorySidebar.cs (kept for any legacy detail logic; calls removed from main flow)
- Assets/_Modules/Core/UI/ElarionUiKit.cs (if palette/kit extensions needed for mockup match)
- docs/MASTER_CATALOG/village-hero.md (update for split + redesign)

## Implementation notes (from execution)
- Used code-built uGUI + RectTransform anchors (0-1 normalized) for responsive H/P.
- Direct onTap lambdas for equip (no detour).
- Removed RebuildSidebar calls from BuildRoot/main to match mockup (full grid focus).
- Tabs strictly override to Profile pack (P1/P2) — eliminated prior PLAY button leak.
- Grid: ScrollRect vertical, ContentSizeFitter Preferred, RectMask2D; no names in cells per mockup icon-card style.
- PaperDoll: focused oval medallion + stats bars only (mockup left column).
- All prior func (select tab, gear change listener, loadout resolve, PanelManager) preserved.
- After edit: python brace check on every .cs; NUL/brace/compile via CompileGate.

## Verification
- Braces balanced on all 6 .cs (counts 79/29/22/35/28/106).
- CompileGate (run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate-wo465-final-validation.log) emitted COMPILE_GATE_OK :: scripts compiled clean; no CS errors; exit 0; no Unity left running.
- Git: explicit paths only (no `add -A`), sole committer, local commit only (no push until owner retest/OK).
- Matches mockup (reference the landscape image provided in chat as the target): left portrait emphasis, clean tabs, 5-col grid cards, premium dark+gold, bottom bar, touch cells, no sidebar/PLAY/intrusion.

Owner to retest in editor (open inventory in Village/MainCastle, rotate device sim for portrait/landscape, verify vs mockup, equip/unequip flows, tabs, close). Then push the passing commit.
