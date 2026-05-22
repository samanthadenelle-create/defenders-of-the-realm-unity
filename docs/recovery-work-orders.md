# Unity Recovery / Investigation Work Orders

## Goal

Restore broken Unity village/game functionality without deleting the core Village controller unless absolutely necessary.

Primary suspected issue:

- Missing GUID references in hierarchy objects under `Village / Build Village`
- Assets previously rendered correctly, but now instantiate invisible or unstyled
- Likely caused during village + exterior/outer map merge/cookie-cutter work

---

# Agent 1 — Asset GUID / Meta File History Investigation

## Objective

Determine whether Unity `.meta` files or asset GUID references were removed, regenerated, renamed, or broken in Git history.

## Tasks

1. Search Git history around the point where the village and outer/exterior map work was merged.
2. Look specifically for:
   - Deleted `.meta` files
   - Recreated `.meta` files
   - Asset folder moves/renames
   - Changes under KKit / KitCat / Medieval / Dungeon / Hexagon asset folders
3. Compare working path mappings from `Village / Build Village` against actual asset paths.
4. Confirm whether GUID mismatch is caused by:
   - Missing asset file
   - Missing `.meta`
   - Wrong imported folder depth
   - Asset path text is correct but GUID is broken
5. Recommend safest fix:
   - Restore old `.meta` files
   - Reimport package
   - Re-link materials/prefabs
   - Patch mapping logic

## Success Criteria

Buildings under `Village / Build Village` render with correct materials/colors again.

---

# Agent 2 — KKit / Dungeon / Medieval Asset Reimport Strategy

## Objective

Determine whether reimporting the full KKit Dungeon Remastered / Medieval Hexagon pack fixes broken references.

## Context

Pack includes:

- 200+ stylized dungeon assets
- Walls, floors, stairs, doors, props, barrels, crates, banners, traps
- Modular low-poly design
- Single gradient atlas texture

## Tasks

1. Identify the exact asset pack folder currently in the Unity project.
2. Determine whether only partial assets were imported.
3. Test controlled reimport of:
   - Required medieval hexagon assets only, OR
   - Entire relevant Unity asset type folder, OR
   - Full pack if necessary
4. Verify whether reimport restores missing GUID references.
5. After repair, create cleanup plan:
   - Identify unused imported assets
   - Preserve only actively referenced files
   - Avoid deleting assets still used by prefabs/materials/scenes

## Success Criteria

Broken village/building assets regain correct visual rendering.

---

# Agent 3 — Dungeon Portal / Sub-Map Loading Investigation

## Objective

Fix dungeon portal transition so it loads a real dungeon scene/map instead of placeholder stubs.

## Current Problem

Portal works as a bridge and loads a new map/start location, but destination is incomplete:

- One version loads an empty square room with two pills
- Another loads a vague 3D stub with basic shapes only
- No real depth, styling, dungeon geometry, or usable layout

## Tasks

1. Locate portal transition logic.
2. Identify which scene/prefab/map the portal currently loads.
3. Determine whether it is loading:
   - A placeholder scene
   - Wrong prefab
   - Stub test map
   - Missing dungeon asset references
4. Trace intended dungeon map asset or scene.
5. Replace stub destination with proper dungeon entry scene/prefab.
6. Confirm player spawn position works correctly.

## Success Criteria

Entering the dungeon loads a styled dungeon area, not a placeholder box/stub.

---

# Agent 4 — HUD Top-Left Status Repair

## Objective

Restore full HUD status display in the top-left corner.

## Current Problem

Only HP status is showing. Missing:

- Mana
- Spire health percentage

Previously, mana showed at the bottom before being moved top-left.

## Tasks

1. Inspect HUD hierarchy and canvas anchors.
2. Verify HP, Mana, and Spire Health objects exist.
3. Check whether missing elements are:
   - Disabled
   - Off-screen
   - Behind another panel
   - Missing text binding
   - Anchored incorrectly
   - Destroyed by runtime logic
4. Restore layout so all three show top-left:
   - HP
   - Mana
   - Spire Health %
5. Confirm values update during play.

## Success Criteria

Top-left HUD shows HP, Mana, and Spire Health clearly and consistently.

---

# Agent 5 — Build Button UI Flow Repair

## Objective

Fix main HUD Build button so it opens the build/repair workflow.

## Expected Flow

Click `Build` on main HUD →
Left-side menu appears →
Player chooses:

- Towers
- Walls / fortifications
- Repairs if damaged structures exist

Then modal/dialog should show:

- Available towers
- Walls/upgrades
- Repairable destroyed items
- Costs
- Build/repair confirmation

## Current Problem

Build button exists but does not connect to the expected UI flow.

## Tasks

1. Inspect Build button OnClick binding.
2. Trace target method/script.
3. Confirm build menu panels still exist.
4. Check whether menu is disabled, orphaned, renamed, or no longer referenced.
5. Restore button linkage.
6. Verify towers/walls selection menu opens.
7. Confirm repair flow is still reachable if damaged objects exist.

## Success Criteria

Clicking Build opens the left-side build selection menu and progresses to tower/wall/repair dialogs.

---

# Agent 6 — Master Volume Toggle Restoration

## Objective

Restore simple master sound on/off toggle on the main HUD.

## Current Problem

Master volume toggle is missing from HUD.

## Tasks

1. Check Git history for prior volume toggle implementation.
2. Determine whether it was deleted, hidden, or disconnected.
3. Restore or recreate simple HUD toggle:
   - Sound On
   - Sound Off
4. Connect toggle to master audio listener/mixer/global audio manager.
5. Persist preference if existing settings system supports it.

## Success Criteria

Main HUD has a working volume on/off toggle.

---

# Agent 7 — Village + Exterior Map Architecture Review

## Objective

Determine the best long-term approach for village scene plus enemy spawn/exterior area.

## Background

Current village extends to castle walls with small placeholder space beyond.
An exterior/outer map was generated separately and attempted as an overlay/cookie-cutter merge.
That merge likely caused major breakage.

## Questions to Answer

1. Should the village builder continue generating the exterior area?
2. Should exterior/enemy spawn area be separate but loaded dynamically?
3. Should map chunks materialize only when player/enemies are near?
4. Should distant chunks unload or pool to reduce resource load?

## Tasks

1. Inspect current village scene structure.
2. Identify where exterior merge broke references or positioning.
3. Recommend architecture:
   - Single expanded village scene
   - Additive scene loading
   - Chunk-based dynamic loading
   - Object pooling
   - Distance-based activation/deactivation
4. Consider mobile performance.
5. Avoid heavy always-loaded map if unnecessary.
6. Recommend safest short-term fix and better long-term design.

## Success Criteria

Clear recommendation for village/exterior/enemy-spawn architecture that avoids breaking core village references and stays performant.

---

# Priority Order

1. Agent 1 — GUID/meta file investigation
2. Agent 2 — Asset reimport repair path
3. Agent 4 — HUD status repair
4. Agent 5 — Build button flow repair
5. Agent 3 — Dungeon portal destination repair
6. Agent 6 — Volume toggle restoration
7. Agent 7 — Long-term village/exterior architecture

---

# Important Constraint

Do not delete or rewrite the core Village controller unless all repair paths fail.

Preferred approach:

1. Diagnose
2. Restore references
3. Reconnect UI
4. Reimport assets only if needed
5. Clean unused assets after functionality is confirmed
