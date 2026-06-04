# WORK ORDER 150 — Village Roster Reconcile: Keep 5 Buildings, Skip Deleted Content (magenta-ghost fix)

**Status: READY TO IMPLEMENT**
**Priority:** High — magenta/broken meshes in the baked village; village should contain only the owner's intended buildings
**Created:** 2026-05-30
**Source:** Owner playtest screenshot (magenta blob) + owner roster decision
**Lane:** Architect / World — **`Assets/Editor/VillageSceneBuilder.cs` (serialization bottleneck, CLAUDE.md §9) + a rebake.** CLI implements (single writer), compile-gates, bakes. UI did not touch the file.

> Same skip-guard pattern CLI already applied for orchard/farmhut/spire in the parapet bake — extend it to the remaining deleted content AND trim the building roster to the owner's five.

---

## Problem

The full `BuildVillage` (run to show the WO-136 parapet) re-spawned content the owner had manually deleted — KayKit dressing, trees, portals, and the crystal — which now render as **magenta / missing-material meshes** (the purple blob) because their underlying assets are gone. The builder also still spawns a 6-building roster; the owner wants it trimmed to five.

---

## Target building roster (KEEP exactly these 5)

| Keep | Code entry (`Buildings[]`) | Type | Action when [F] | Change needed |
|---|---|---|---|---|
| **Store** | Market | 5 | opens PackStore | none |
| **Forge** | Workshop | 3 | crafting panel | **relabel "Workshop" → "Forge"** (Label + prompt text) |
| **Pet Home** | Pet House | 1 | pet skill-tree | none |
| **Tower** | Arcane Tower | 2 | **tower-upgrade (KEEP as-is)** | none |
| **Farm** | Farm | 4 | — | none |

### REMOVE — relocating to world nodes (not a gap)
- **Crystal Mine** (Type 0, `id="crystal-mine"`, plot −20/+10) — remove from the `Buildings[]` array. **Crystals are moving onto harvestable world nodes** (per owner; aligns with WO-111 resource pillar / outer-world regions). This is an intentional relocation of the crystal economy out of the village, NOT a lost income source — no economy gap.
- **Dungeon portals** — likewise **relocating to world nodes.** Skipping the in-village portal generator is intentional; the dungeon entrances live in the world now, not the town.

### LATER (not this WO)
- **Lumbermill** — owner wants it, but it's **not implemented yet**. Add as a new `Buildings[]` entry + interactor in a future WO. Do not stub it here.

---

## Also skip (deleted non-building content re-spawning as magenta)

Guard/skip the generators for content the owner deleted, matching the orchard skip-pattern already in the builder (prefer a conditional flag like `_skipDeletedContent`, not hard deletion):

- **KayKit dressing** — deleted KayKit props/buildings (the magenta blob).
- **Trees** — remaining tree/foliage generators beyond the orchard already skipped.
- **Portals** — dungeon/portal placement (`DungeonPortal` / portal generator) — **relocating to world nodes** (see REMOVE above).
- **Standalone crystal** — any decorative crystal prop separate from the Crystal Mine building.

> CLI: grep `BuildVillage` for these generator calls and apply the same guard used for orchard/trees/spire.

---

## Layout principle — one building per area

The five kept buildings should each sit in **its own distinct area/quadrant** (one building per zone, not clustered). The builder already places them at spread plots (Pet Home +20/+10, Tower −20/−10, Forge/Workshop +20/−10, Farm −15/+14, Store/Market +15/−20); with Crystal Mine removed the −20/+10 quadrant frees up. CLI: verify the five read as one-per-area with clear spacing after the rebake; nudge plots only if two now crowd the same zone.

## Acceptance criteria

1. After rebake, **no magenta/missing-material meshes** anywhere (screenshot blob gone).
2. Village contains **exactly five buildings**: Store (Market), Forge (Workshop, relabeled), Pet Home, Tower (Arcane Tower), Farm. Crystal Mine absent.
3. The five buildings each occupy a **distinct area/quadrant** (one per area, visibly spaced — not clustered).
3. Deleted KayKit pieces, trees, portals, and standalone crystal **do not re-spawn**.
4. Each kept building's [F] interaction opens the correct panel: Store→PackStore, Forge→crafting, Pet Home→pet skill-tree, Tower→tower-upgrade, Farm→(none). The "Workshop" label/prompt now reads **"Forge"**.
5. WO-136 parapet, walkways/ramps, walls/towers/gates, hex ground — all still present and correct.
6. Scene integrity holds: no dup IDs, no junk, no resave corruption (match parapet bake: ~8931 anchors / 0 dup / 0 junk).
7. NavMesh rebuilt; spawn-0..3 → Heart path still valid.
8. Brace-balance check passes on `VillageSceneBuilder.cs`.

## What NOT to touch

- Don't hard-delete generators if a flag-guard works (keep recoverable).
- Don't touch the parapet, walkways, walls/towers/gates, hex ground.
- Don't add Lumbermill (future WO).
- Don't hand-edit `Village.unity`; regenerate via the builder, editor closed.

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passes on `VillageSceneBuilder.cs`
- [ ] No `.unity` hand-edit; rebuilt via batchmode builder, editor closed
- [ ] Exactly 5 buildings; Crystal Mine removed; "Forge" label live
- [ ] Magenta ghosts gone; KayKit/trees/portals/crystal do not re-spawn
- [ ] Parapet + walkways + walls intact; NavMesh rebuilt; spawn→Heart verified
- [ ] Owner confirmed crystal economy source (if Crystal Mine removed)
- [ ] `WORK_ORDER_150_skip_deleted_generators.RESULT.md` written when complete
