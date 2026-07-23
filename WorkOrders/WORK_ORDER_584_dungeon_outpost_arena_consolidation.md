# WORK ORDER 584 — Dungeon / Outpost / Arena Consolidation (one space primitive)

> ⚠ CORRECTION 2026-07-22: this WO's flag name is STALE. The gate as shipped is **`ff.dungeonrealtime`
> (default TRUE)** — it routes dungeon/outpost fights INTO the real-time `BattleArena`; set it to 0 to
> restore the legacy ATB path. There is **no `ff.atbdungeon`** flag (it was never created — grep-verified
> 0 hits). Read every `ff.atbdungeon` below as `ff.dungeonrealtime` with **inverted sense** (real-time is
> the default-ON path, ATB is the OFF fallback). Source: `Assets/_Modules/Core/FeatureFlags.cs`
> (`DungeonRealtimeBattle => Get("dungeonrealtime", defaultOn: true)`), `EncounterTrigger.cs`, `DungeonStubEncounter.cs`.

**Status:** READY TO IMPLEMENT (design owner-ratified 2026-06-28)
**Silo:** Combat/AI + World/Environment (code + content; isolated spaces — no seam work)
**Canon:** memory `dungeon-outpost-arena-one-space-primitive`; refines `overworld-encounter-isolated-battle`,
`scene-chunk-dungeon-composer-northstar`, `region-gate-crossing-primitive`, `atb-flat-vs-overworld-animated-combat`.

---

## 1. The decision (why this WO exists)

The dungeon now renders cleanly. Owner ruling: **one combat-space primitive, three skins, one warp
entrance** — replaces the flat ATB dungeon fight and closes the WO-453 open-world zoning gap **without
any cross-region navmesh/seam work** (each space is isolated; you *port* in, not *seam* across).

```
WORLD prefab (cave / enemy-encampment)
   → RegionGate WARP (placeable anywhere, ~0 cost)
      → RESOLVER (spaceType → DungeonResolver / OutpostResolver)
         → Arena-skinned space (skin + spawn-set + ownership flag)
            → clear it (the verified real-time Arena loop)
               → ownership flips Enemy → PlayerCamp (same space re-dresses in place)
```

**Key proven fact (scout-verified, read not inferred):** the Knight's skills already run ONLY through
the shared, system-agnostic Arena stack — `HeroAbilities.TryCast → ResolveEffect`, reading
`HeroLoadout` (W/E/R) + applying `HeroTalentModifiers`. **ATB resolves from static `Defs.HERO_ABILITIES`
and never reads the talent tree or loadout at all** (`BattleATB/Engine/Actions.cs:127-254`). So dropping
ATB for the dungeon loses **no** skill depth — it gains it. **This is a SKIN job, not a skills port.**

---

## 2. Scope (slices — ship + felt-verify each before the next)

### Slice 1 — Dungeon onto the Arena (skin, ATB off behind a flag)
- Route the dungeon encounter into the generic `BattleArena` (reuse, by extraction — do NOT rewrite).
- Gate the ATB dungeon path behind a feature flag (`ff.atbdungeon` OFF by default). **Do NOT delete the
  ATB module** — dormant + reversible until the Arena-dungeon felt-confirms (owner closes).
- Acceptance: enter dungeon → real-time Arena loop with Knight skills/talents working → win returns home.

### Slice 2 — Resolver registry + `spaceType`
- The RegionGate stays a dumb door; add a **`spaceType`** data field on the entrance that routes to a
  **resolver registry**: `ISpaceResolver` → `DungeonResolver`, `OutpostResolver`. Each is a data-driven
  builder that emits { skin, spawn-set, ownership } onto the shared Arena space. New flavor later = new
  prefab + new resolver entry, **zero new traversal/combat code.**
- Data-driven (owner thinks in data structures): resolver chosen by a field, not a code branch.

### Slice 3 — Outpost world prefab + reskin
- Author an **enemy-encampment prefab** (tents / palisade / watch-fire — reads as a camp at a glance) as
  the OUTPOST world touchpoint, distinct from the dungeon cave/entrance prefab. On touch → RegionGate →
  `OutpostResolver` → outpost-skinned space + garrison spawn-set.
- The encampment prefab doubles as the captured-camp dressing (see Slice 4).

### Slice 4 — Ownership flip (capture → camp)
- Add an **`ownership` state** to the space: `Enemy` / `Cleared` / `PlayerCamp` — a data field a thin
  interpreter reads. Clear the garrison → flip the flag → the SAME space re-dresses **in place** as a
  player camp (garrison spawns off, camp props/services on). **~one boolean of new logic, no scene flip.**
- Deliberately SEPARATE from settlements (owner: "not flipping to settlements") — a held forward-base.

### Slice 5 — KayKit dungeon content pipeline (chunk-composer source)
- Feed the **KayKit Dungeon Remastered 1.1** kit into the JSON chunk-composer (AI-composable hard
  dungeons). See §3 for the chunk inventory.
- **One-time import pass first:** the pack is raw FBX/OBJ **source meshes (0 prefabs)** → material-fix +
  prefab-ify the STRUCTURAL set into snappable, anchor-relative chunks (same pattern as the polyperfect
  `_M` tier setup). Cheap, one-time.

---

## 3. KayKit Dungeon Remastered 1.1 — chunk inventory (inventoried 2026-06-28)

**Location:** `Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/` (GITIGNORED — owner copies in
locally; ~1.5 GB; NOT polyperfect, NOT under Resources). 858 model files = ~429 unique meshes
(FBX + OBJ pairs), **0 prefabs** (raw source → needs the import pass above).

**Structural (chunk-composer building blocks):**
| piece | count | use |
|---|---|---|
| wall | 38 | room/corridor walls, chokes, fake-walls |
| floor | 34 | floor tiles, multi-room layouts |
| stairs | 15 | multi-level dungeons |
| scaffold | 12 | verticality / bridges |
| barrier | 5 | blockers / chokepoints |
| pillar / column / ceiling | 2 / 1 / 1 | room framing |

**Dressing & treasure (depth + treasure easy wins):**
- **banners ×42** (all colors/patterns — faction-skin a lair vs. captured camp)
- **chests ×5, coins ×4, keys/keyrings ×4** (treasure + lock-and-key depth)
- barrels / crates / boxes / kegs, torches / candles (lighting), tables / beds / bars / bookcases /
  shelves (room life), bottles / plates / books (clutter)

This kit supplies, in one pack: **structure** for the composer, **banners** for the enemy↔captured
reskin, and **chests/coins/keys** for the treasure layer.

---

## 3b. Outpost layout recipe (harvested from Grok's EnemyOutpostGenerator draft, 2026-06-28)

Grok proposed a standalone `EnemyOutpostGenerator` MonoBehaviour. **Do NOT adopt the script** — it
inspector-drag-drops prefab refs (banned, `never-dragdrop`), greenfields over our existing builders,
calls the wrong NavMesh namespace (`UnityEngine.AI.NavMeshSurface` → must be `Unity.AI.Navigation`,
pkg `com.unity.ai.navigation` 2.0.4 is present), and uses naive grid math that ignores KayKit
pivots/offsets (the exact problem Offset Forge / RotationCorrectionRegistry solves). Its `ClearPrevious`
also orphans objects after a domain reload.

**Harvest the RECIPE BEATS** (these are good) into the builders below:
1. Base floor grid → 2. Outer walls with deliberate **choke gaps** at center → 3. Central **master room**
with pillars → 4. **Multi-level** platforms (upper tier) → 5. **Stairs** linking levels at chokes →
6. Decor: **banners** (faction color) + **crates** as cover near chokes → 7. NavMesh bake.

**Reuse targets (do NOT reinvent):**
- `Assets/Editor/DungeonComposer.cs` + `Assets/Editor/EnemyStrongholdBuilder.cs` — existing editor
  builders; extend these with anchor-relative chunk snapping + offsets via the registry (NOT naive grid).
- **WO-479** scene chunk-composer — the JSON-driven composer spine.
- **WO-475** stronghold → player-settlement conversion — **this IS the capture→camp flip (Slice 4)**; build
  on it, don't author a parallel one.
- Prefab refs come from a registry/Resources/data recipe — never inspector fields.
- NavMesh: correct namespace `Unity.AI.Navigation.NavMeshSurface` (2.0.4 present).

**Build loop = the PROVEN formula (owner 2026-06-28): CLI creates → owner hand-edits → CLI offsets.**
The KayKit-pivot/alignment concern is resolved by this loop, NOT by perfect grid math. CLI authors the
generator (registry-loaded, canon-clean) placing chunks roughly; owner hand-tunes the layout/seating by
eye in the editor; CLI then CAPTURES the corrections into the offset registry (Offset Forge /
RotationCorrectionRegistry, memory `model-alignment-offset-tool`) so the build is repeatable forever.
So the "create" step does NOT need pixel-perfect pivots — it needs to be canon-clean (no inspector
drag-drop, reuse existing builders) and close enough for the owner to hand-tune.

## 4. What NOT to touch
- **No seam / cross-region navmesh work** — spaces are isolated, entered by RegionGate WARP. That's the
  whole point (sidesteps the unsolved V2 seam problem). Do not stream or stitch.
- **Do NOT rewrite the combat loop** — reuse `BattleArena` + `HeroAbilities` by extraction. ZERO new
  combat code (BattleArena header already asserts this).
- **Do NOT delete the ATB module** in this WO — flag it off, reversible. Removal is a later, separate WO
  after owner felt-closes the Arena-dungeon.
- **No settlement system** — captured camp is a forward-base state flag, not a town.

## 5. Acceptance criteria
- [ ] Dungeon encounter runs the real-time Arena loop; Knight skills + talent modifiers fire (proven via
      `HeroAbilities`/`HeroLoadout`/`HeroTalentModifiers`, not ATB).
- [ ] ATB dungeon path behind `ff.atbdungeon` (OFF); ATB module intact.
- [ ] `spaceType` routes RegionGate → correct resolver; adding a flavor needs no combat/traversal code.
- [ ] Enemy-encampment prefab distinct from dungeon entrance in-world; outpost resolves to garrison space.
- [ ] `ownership` flips Enemy→PlayerCamp on clear; same space re-dresses in place; no scene reload.
- [ ] KayKit structural set prefab-ified + material-fixed; chunk-composer can place them.
- [ ] Each slice felt-verified by owner (PO closes) before the next.
