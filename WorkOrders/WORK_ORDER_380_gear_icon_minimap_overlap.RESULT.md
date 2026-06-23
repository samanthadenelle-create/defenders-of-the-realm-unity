# WO-380 RESULT — Gear Icon / Minimap Overlap

**Status:** ✅ CLOSED (resolved by removal — better than the spec's "move the icon")
**Commit:** `fca5e86` feat(hud): cut town minimap (WO-380)
**Verified:** Compile-gated; pending owner visual confirm of the HUD corner.

## Resolution
The spec offered three options to stop the minimap covering the settings gear (move the icon, raise sort order, or shrink the map). Owner product call: **the navigation minimap adds no value in the compact castle hub** (everything is in-frame), so we cut it entirely rather than reposition it — which removes the overlap *and* declutters the HUD.

`BuildTownMiniMap` is no longer called in `VillageHudController`; every consumer (`ProjectMiniMap`, the mode-toggle reflow, markers) null-checks, so the subsystem goes inert with no errors. Build code retained (commented) for a one-line re-enable if a larger world ever needs a map.

Threat awareness ("enemies attacking"), the minimap's only real value, is already handled in-world by `StructureAttackAlert` (red flash + bobbing "!" on hit buildings).

## Acceptance
- [x] Gear icon accessible / no overlap — the occluding element is gone
- [x] Settings reachable
- [ ] (owner) eyeball the top-right corner on next playtest to confirm
