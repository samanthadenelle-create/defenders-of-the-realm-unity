# WORK ORDER — Village2 → Enemy Stronghold (repurpose, recipe-driven)

**Status:** SPEC — awaiting owner confirm before the Village2 regen + Village.unity removal.
**Source:** owner-authored design (`Create Enemy Camp.txt`, the `EnemyStrongholdGenerator_NavReady` concept) +
the KayKit `assets_list.txt`. **Reconciled** onto our existing systems (NOT the standalone generator).

---

## The decision (smart path, confirmed by the reference check)

- **Keep the `Village2` scene NAME; regenerate its CONTENTS as the enemy stronghold.** `Village2` is
  referenced everywhere — `SceneRouter.Village = "Village2"`, `HubScenes`, `PersistenceBridge`,
  `VillageHudController.VillageSceneName`, `RaidOutpostSystem`, multiple `TargetScene` consts. Deleting the
  file orphans all of it; a **content-swap keeps every reference working** and the town becomes the stronghold.
- **Remove the abandoned `Village.unity`** — verified safe: it is **NOT in build settings** and **NOT in
  routing** (`SceneRouter.Village` resolves to `"Village2"`, not the old scene). It's an orphaned, dirty file.
- **Built via a builder + recipe, never hand-edited** (CLAUDE.md §3). **Batchmode only, editor closed.**

## Why NOT adopt `EnemyStrongholdGenerator_NavReady.cs` as-is
Stub (won't compile — undeclared `placeTowers/placeKeep/...`, missing methods) · inspector-drag MonoBehaviour
(opposite of "DB/script-injector → push live content") · realtime per-torch shadowed lights (Seeker/WebGL
perf killer) · `.bin` asset refs (we use KayKit `fbx(unity)` + Fix-KayKit-Materials) · no render-verify
(the invisible-blocker class we just instrumented). **We lift the DESIGN, not the script.**

## What we KEEP from the design (it's on-canon)
Layered: **outer courtyard → chokepoint → raised inner keep → boss chamber** · **verticality (stairs +
NavMeshLink)** — validates today's seam lesson · **traps** (spike/arrow/explosive) · **destruction states** ·
**boss chamber** for the "something special" boss · KayKit asset mapping.

---

## The recipe (data-driven, DB/live-content-ready) — extends `garrison-recipes.json`

A new `stronghold` recipe (one entry, `id: "village2_stronghold"`), schema = the garrison recipe + a
`layout` block for the stronghold-specific structure:

```jsonc
{
  "id": "village2_stronghold",
  "kind": "stronghold",
  "size": "large",
  "theme": "ruined",            // raider/ruined/corrupted — owner picks
  "lighting": "stronghold",     // BAKED torch glow (mobile-safe), not realtime per-torch
  "enemies": ["orc-berserker", "orc-shaman", "troll", "hollow-warrior"],
  "levelRange": [8, 14],
  "threat": 3,
  "boss": "<the special boss id>",     // owner authors the boss
  "layout": {
    "courtyard":  { "size": 14, "walls": "stone", "gate": "main", "towers": 4 },
    "chokepoint": { "width": 2,  "traps": ["spike", "arrow"] },
    "keep":       { "raised": true, "platformHeight": 1.5, "stairs": true, "navlink": true },
    "bossChamber":{ "enabled": true, "raised": true, "navlink": true, "altar": true }
  },
  "traps":       { "max": 8, "courtyard": true, "chokepoint": true },
  "destruction": { "wallDamageChance": 0.3, "level": 1 },
  "props": ["wall_stone","gate","watchtower","tower","stairs","torch","banner_red",
            "barrel","crate","chest_gold","rubble","spikes","bones"],
  "element": null
}
```
KayKit asset mapping (from the design → our `fbx(unity)` + StructureFactory keys): walls=`wall_straight/_corner/_gated`,
towers=`tower_A/tower_cannon`, keep=`castle`/`building_townhall`, stairs=`stairs_long_modular_center`,
floor=`floor_tile_large`, props per the design's list, traps=`floor_tile_big_spikes`/`barrel_large_decorated`.

## The builder — `EnemyStrongholdBuilder` (Editor, reconciled)
- **Editor-only** static builder (menu + `-executeMethod`), reconciled onto **`StructureFactory`**
  (catalog placement), the **garrison theming/lighting** path, and **`GarrisonController`**'s enemy seed.
- **Verticality done right:** stairs get a **`NavMeshLink`** (start/end/width) so agents path up/down — the
  exact thing whose absence broke the castle seam. Floors/platforms/stairs named `Floor_/Platform_/Stairs_`
  for the bake. Bake via `OuterWorldNavBake`-style solo bake (editor-closed).
- **Mobile-safe lighting:** BAKED torch glow (or a small shared realtime budget), NOT a shadowed point light
  per torch.
- **KayKit:** load `fbx(unity)` variants + run **Fix KayKit Materials** (no magenta).
- **TGVRU-instrumented** (per WO-430): every piece render-verified (`FlowTrace.Fail` + footprint log on an
  invisible/blocker piece) — reuses the `OutpostFoundationGenerator` pattern.
- **Regenerates `Village2.unity`** (clear → build stronghold → save) in batchmode. Idempotent.

## Execution steps (after owner confirm, editor CLOSED)
1. Write `stronghold-recipe` into `garrison-recipes.json` (+ the dual StreamingAssets/Resources copy).
2. Build `EnemyStrongholdBuilder.cs` → compile-gate (`COMPILE_GATE_OK`).
3. Batchmode: regenerate `Village2.unity` from the recipe → bake NavMesh (links on stairs).
4. Remove the orphaned `Village.unity` (not in build settings / routing) — git rm + its `.meta`.
5. Owner reviews the baked stronghold; adjust the **recipe** (never the scene); rebake.

## Owner inputs needed to finalize (the authoring step)
- **Theme:** raider / ruined / corrupted?  · **The boss:** id + the "something special" gimmick (phases).
- **Layout intent:** rough courtyard size, where the chokepoint + boss chamber sit (or let the recipe default).

> This is the "you author → I script-offset from the recipe" loop: you authored the stronghold; this is its
> translation onto our recipe + builder. Confirm the approach (or tweak the recipe) and I build it.
