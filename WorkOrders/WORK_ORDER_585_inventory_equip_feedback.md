# WORK ORDER 585 — Inventory equip feedback (items feel inert)

**Status:** READY TO IMPLEMENT (held for owner green-light — overlaps WO-582 Gear Preview)
**Date:** 2026-06-28
**Silo:** UI (CLI-owned per `ui-work-cli-owns-docs-first-screenshot-compare`)
**Source:** owner felt-test 2026-06-28 ("I see the model but cannot do anything with it") + read-only RCA (gate-free agent, proven from code).

## Symptom
On the Inventory screen the window + paper-doll hero render, weapons list (as glyph
fallbacks), but tapping/dragging items does nothing visible. Reads as totally inert.

## Root cause (classified — built-with-handler, no perceivable response; NOT data-empty, NOT unbound)
The cells ARE wired end-to-end: `InventoryGrid.cs:318` builds a real `Button`+`Image`
(raycastTarget true), `:346` `btn.onClick.AddListener(onTap)`, `:140-147` onTap →
`_vm.SelectById` then `_vm.Equip()` (or `Use()`), both implemented in `InventoryVM.cs`
(`SelectById:190`, `Equip:244`). So input + handler + command all exist. It feels dead because:
1. **Re-equip-same-item = no-op.** Gear is class+level auto-equip (`HeroInventoryController.cs:24-31`);
   WO-578 surfaces the *already-equipped* gear as "owned." Tapping one re-equips the identical
   item → zero visible change.
2. **No feedback UI is rendered.** `Render()` skips the sidebar (`HeroInventoryController.cs:322`
   `// No RebuildSidebar`). The `InventorySidebar` partial (detail pane + equip button) is built
   but never called. `Equip()` writes `Status` (`InventoryVM.cs:256`) but nothing renders it →
   even "Equipped X" / "No hero to equip" is invisible.
3. **Paper-doll is render-only.** `InventoryUIBuilder.cs:346` sets `_paperDollPreview.raycastTarget
   = false`; no drag/rotate handler. The viewer DOES support `SetRotation` (`:324`) but it isn't hooked.

Ruled out (statically): no input/raycast blocker — EventSystem force-ensured + watchdog
(`EventSystemEnsurer`), input module repaired in builds (`UIInputModuleFix`), chrome content layer
`raycastTarget=false` (`ElarionUiKit.cs:348`). Owner reached the panel by tapping, so uGUI input works.

## Intent (canon)
This grid = browser + quick-equip ("selection highlights in grid, equip on tap",
`InventoryUIBuilder.cs:65-66`). The RICH tap-to-change drawer belongs to **Gear Preview /
EquipmentPanel restyle (WO-582)** per `gear-preview-design` + `character-screen-obsidian-paperdoll-reference`.
→ Fix here = restore feedback; do NOT duplicate the Gear Preview drawer.

## Fix plan (minimal, reuses built components)
1. **Instrument first (§12).** `InventoryGrid.cs` onTap closure (~:140): `FlowTrace.Step("Inventory", …)`
   logging tapped id + `isConsumables` + post-select `SelectedId` + post-equip `Status`. This is the
   decisive capture (no tap instrumentation exists today — `:38/:103/:162` trace only grid *build*).
   Build → owner taps → read trace: lines appear = feedback gap (below); no lines = raycast blocker
   (generalize `PointerInterceptDiagnostic` to dump while inventory open).
2. **Restore felt response.** `HeroInventoryController.Render()` (~:315-323): call the built-but-unused
   `RebuildSidebar` so a tapped item shows a detail pane + explicit **Equip** CTA; **separate select
   from equip** (tap = select+detail; Equip button = equip). Surface `_vm.Status` and/or
   `ElarionUiKit.ToastCard` on equip so the action is visibly confirmed even when re-equipping.
3. **(Optional, owner call) inspectable model.** `InventoryUIBuilder.cs:346` raycastTarget=true + a
   drag handler → `_heroPreview.SetRotation(...)`.

## Separate cosmetic (out of scope here)
Icon glyph fallbacks: `ItemIconCatalog.ForWeapon` returns null for staff/wand/censer types
(`ItemIconCatalog.cs:75-76,88-89`) and unmapped ids; RpgUiCatalog fallback art absent → weapon-type
glyph. Fix later by adding sliced art / mapping ids. Unrelated to interaction.

## Key files
`InventoryGrid.cs` (handler+onTap), `InventoryVM.cs` (Equip/SelectById), `HeroInventoryController.cs`
(Render :322), `InventoryUIBuilder.cs` (paper-doll :346), `InventorySidebar.cs` (built-but-unused),
`ElarionUiKit.cs` (chrome — confirmed not blocking).

## Acceptance
- Tap an item → detail/selection visibly responds + an explicit Equip action; equipping shows a toast/status.
- Re-equipping the same item still gives visible confirmation (not silent).
- onTap FlowTrace present so the felt-test trace is captured.
- Does NOT pre-empt or duplicate WO-582 Gear Preview drawer.
