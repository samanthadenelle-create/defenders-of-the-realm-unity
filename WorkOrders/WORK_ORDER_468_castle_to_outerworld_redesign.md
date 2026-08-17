<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; the flow is castle exit → OuterWorld (≥4× larger) → portal, i.e. a redesign of a scene-to-scene crossing that the merged world removed.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 468 — Castle → OuterWorld → Outpost redesign (DOR-LVL-012)

**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
**Priority: CRITICAL (blocker)** · **Owner: Samantha** · **Date: 2026-06-19**
**Supersedes:** the `AddCastleBridgeSeam` bridge visual (the "single misplaced bridge tile on grass") and
folds the crossing into the RegionGate primitive (memory `region-gate-crossing-primitive`): the CAVE is
the diegetic gate, click/stream mode.

## Owner objective (verbatim intent)
Completely redesign castle→outer-world using the EXISTING portal asset + existing transition logic.
Make it visually appealing with proper scale and distance. **Do not deliver another half-measure.**

## The flow (target)
Castle exit → **clean regular seam** → **OuterWorld (≥4× larger)** → walk a **real distance** (castle
visible behind the player) along a clean intentional path → **CAVE / existing portal asset at the far
end (5–6× castle length from the exit)** → **player CLICKS the portal** (NO auto-teleport) → **narrative
prompt** ("Click to enter the enemy outpost" / "Enter the enemy stronghold") → loads the **enemy outpost
scene (Village2 stronghold)** via existing SceneRouter / scene-loader logic.

## Requirements
**Scale & distance**
- OuterWorld area ≥ 4× current size. Portal placed 5–6× castle-length from the castle exit. Player must
  feel a significant journey.
**Visual quality**
- Clean, intentional path leading away from the castle. Castle visible behind the player as they walk.
- NO floating / misplaced assets. Visually appealing outdoor environment. Remove the bridge tile.
**Enemies**
- All enemies correctly scaled to the player. Ranger-class enemies look like proper rangers (correct
  clothing, bow equipped + visible).
**Transition point**
- Use the EXISTING portal/cave asset at the far end. Click-to-activate (no auto-teleport). Clear
  narrative interaction text. Wire to load the enemy outpost via existing SceneRouter / loader.

## Strict acceptance criteria
1. Real, noticeable distance castle→portal (5–6× castle length).
2. Clean, visually appealing outdoor area; no visual garbage / scale issues.
3. All enemies correctly scaled + visually appropriate (rangers look like rangers, bow visible).
4. Portal requires a player CLICK with narrative text.
5. Full flow castle → long outdoor path → portal click → enemy outpost works cleanly.
6. Regression-guarded: SEAM-REACHABLE fleet oracle passes for the new seam + portal; bot can traverse
   castle → OuterWorld → portal.

## Build approach (data-driven, no hand-edited scenes; builders + batchmode, editor closed)
- Remove the `CastleBridgeSeam` visual; keep/clean a normal castle-exit seam to OuterWorld.
- Enlarge + lay out OuterWorld via its builder (terrain + path + props), portal at the far end.
- Place the existing portal/cave prefab; wire DungeonPortal/DungeonEntrance click-to-enter + narrative →
  SceneRouter load of the enemy outpost (Village2).
- Fix enemy scale + ranger appearance at the spawn/factory level.

## SME investigation (read-only agents, in flight) — findings fill the build steps
- A: OuterWorld builder / current size / how to enlarge 4× + lay the path / castle-behind feasibility.
- B: the existing portal/cave asset + DungeonPortal/DungeonEntrance click+narrative + SceneRouter→outpost.
- C: enemy scale + ranger-class appearance (clothing + visible bow) in OuterWorld.
