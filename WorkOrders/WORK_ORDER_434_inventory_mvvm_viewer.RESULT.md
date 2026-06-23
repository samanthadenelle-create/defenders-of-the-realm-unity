# WORK ORDER 434 — RESULT: Inventory + Equipment on MVVM, with live viewer ✅ DONE (A→D)

All four phases gate-green and pushed. The inventory side of the presentation layer is now a pure
viewer over the owned model, dressed in Blink, with a live gear-apply preview.

| Phase | What | Commit | Gate |
|-------|------|--------|------|
| A | Model seam (`IInventoryStore`/`IEquipTarget`) + `GearLoadout.Unequip` | `7d434828` | COMPILE_GATE_OK, 13 tests |
| B | `InventoryVM` + `EquipVM` (pure, owned-list closes data gap) | `45192901` | COMPILE_GATE_OK, 17 tests |
| C | Rebind both panels onto the VMs + Blink dressing (flag-gated) | `6cf9efea` | COMPILE_GATE_OK |
| D | `HeroPreviewViewer` live RT preview wired into the equip panel | (this commit) | COMPILE_GATE_OK |

## What shipped
- **Model-driven:** inventory shows OWNED gear (`VillageInventory` → `GearCatalog` JSON), not class-
  eligible catalog. Equip/Unequip/Swap route through the VM; world hero updates via the existing
  auto-visual path. Verified live in playtest (owner: "UI looks good"; equipped weapon shows on hero).
- **Pure, tested VMs:** no UnityEngine UI types; mockable seams; 30 unit tests across A+B.
- **Views are dumb skins:** `HeroInventoryController` + `EquipmentPanel` implement `IPanelView`, bind
  their VM, render from `vm.*`, route input to commands. No direct state pulls remain.
- **Live viewer (D):** `HeroPreviewViewer` (TowerPreviewCamera pattern — far-off clone, dedicated
  layer, disabled camera + manual Render, RenderTexture → RawImage in the equip medallion band).
  Reusable for ANY actor (the troop-creation/raid preview component). Refreshes on `EquipVM.Changed`;
  disposes on close; degrades gracefully (no body/RT → hidden, no NRE).

## Owner play-test (Phase C/D — felt verification)
- Open equip via NPC `OpenEquip`: confirm the live hero portrait renders; equipping a **weapon**
  updates the preview mesh live; target-chip switch (companions) re-points the preview.
- `ff.blinkchrome` ON: grid cells + equip rows wear the Obsidian slot plate.
- Inventory is OWNED-driven (empty until you buy/loot — buy in the shop to populate).

## Known limitations (intended / logged)
- **Armor body-art is a NO-OP stub** (`EquipmentController.SetArmorTier`) — equipping armor won't change
  the preview body until that art lands. Weapon shows.
- **HP/MP bars** read full (no live feed in this assembly); **Outfits tab** empty (no cosmetic model).
- **Weapon grip/orientation** reads off — separate §4 issue, logged as **WO-435** (with full RCA), fix later.
- No `HeroPreview` layer in TagManager → falls back to `TowerPreview`/layer 31 (works; add a dedicated
  layer for a clean mask if desired).

## Deferred / next
- Full unification of the two panels (kept both, both bound) — cleanup WO.
- **WO-435** weapon grip (§4 orient) — logged + RCA'd.
- Same pattern next: HUD, talent trees (per-class model exists), troop creation (reuse `HeroPreviewViewer`).

*Cross-ref:* `docs/UI_MVVM_BINDING_MAP.md §2/§5`, WO-430/431/432/433, `TowerPreviewCamera.cs`.
