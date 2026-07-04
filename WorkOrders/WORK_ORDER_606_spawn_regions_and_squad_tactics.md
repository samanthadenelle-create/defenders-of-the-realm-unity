# WO-606 — Geotagged Spawn Areas (V1) — DESIGN NOTE

**Status:** IMPLEMENTED (V1, lean, data-driven). Not gated/built/committed by the implementing agent — hand to CLI for the batch gate + commit.
**Owner directive (2026-07-03):** "just a JSON to spawn geotagged areas" (~1hr). Data-driven content scaling by location; do NOT build a heavy coordinator framework.

This note documents the shipped JSON schema, what got wired, and the squad-behavior fast-follows. It replaces the earlier V2 draft (owner re-scoped this to a lean V1).

---

## What this is

A designer authors **circle footprints in world space** (`spawn-areas.json`). A world position resolves to the **containing** area (nearest center on overlap); outside every authored area it resolves to **nothing → no spawn** (emergent exclusion — composes with the off-navmesh moat carve). This **replaces the one lookup** both spawners did (`ZoneManager.GetZone(pos) → RegionSpawnTable.PickEnemyId/HasRoster`) with a data-driven area draw. When the JSON is absent/empty, the spawners fall back to the legacy roster path — **non-breaking**.

Progression = geography: near the castle = small arena / low level / small troop; far out = big troop / high level.

---

## JSON schema — `Data/Canonical/spawn-areas.json` (dual copy: `Assets/Resources/...` + `Assets/StreamingAssets/...`, Resources-first via `CanonicalJson.Read`)

```jsonc
{
  "version": 1,
  "defaultAreaId": "goldfields",
  "areas": [
    {
      "id": "goldfields",
      "center": { "x": 260, "z": 0 },     // circle centre (world XZ) — the GEOTAG
      "radius": 190,                        // footprint radius; pos inside = this area
      "families": [                         // weighted pool; one is drawn per encounter
        { "id": "orc-warband", "weight": 3.0,
          "tank": "orc-tank", "dps": "orc-warrior", "healer": "orc-mage" }  // role→enemy-id
      ],
      "levelRange":  { "min": 1, "max": 3 },        // enemy level; feeds the existing 'threat' int
      "composition": { "tank": 1, "dps": 2, "healer": 0 },  // role COUNTS to stage
      "arenaPreset": "small",              // small|med|large — forwarded as a string (see fast-follow #1)
      "seedBudget":  6                      // progression knob (chunk-composer WO-479); lightly caps pack size
    }
    // stoneback / mirewood / ashwood escalate level, troop size, arena preset, seedBudget
  ]
}
```

- **4 areas authored:** goldfields (E, near, orc, small, L1-3), stoneback (W, orc+skeleton, med, L3-6), mirewood (S, skeleton+troll, med, L6-10), ashwood (N, troll+skeleton, large, L10-16). Centers are ~260m out so the village/castle box + moat (near origin) sits outside every radius → no spawns there.
- **Owner-tunable:** center/radius/weights/levelRange/composition/preset/seedBudget are all data.
- A drawn family fills the composition's role slots (tanks first = leader, then dps, then healers) with its archetype ids; roles are then inferred per-body from the id (`orc-tank`→Tank, `orc-warrior`→DPS, `orc-mage`→Ranged) by the existing `EnemyBrain`/`BattleArena.RoleForId` logic — composition is **not** green-field.

---

## What got wired

1. **`Assets/_Modules/Core/World/SpawnAreaTable.cs` (NEW, `DeNelle.Core.World`)** — headless-safe loader + resolver, cloned from `RegionSpawnTable`/`GarrisonRecipeCatalog` (Newtonsoft + `CanonicalJson.Read`, Resources-first). API: `ResolveArea(pos)` (containing, nearest-center; null outside all), `HasAreaAt(pos)`, `HasAny`, `Default`, `BuildDraw(pos) → SpawnDraw { EnemyIds[], Level, ArenaPreset, SeedBudget, Valid }`. Weighted family pick + level roll. FlowTrace-instrumented (`[Flow:SpawnArea]`).
2. **`OverworldEncounterSpawner.cs`** (the LIVE loop) — in `SpawnRep`, the rep's fight family + level + arena preset now come from `SpawnAreaTable.BuildDraw(anchor)` (replacing the hardcoded `OrcFamily` + `ZoneThreatAt`). Falls back to `OrcFamily`/`ZoneThreatAt` when no area. `RepEngageWatcher.Init(family, threat, arenaPreset)` forwards the preset into `EncounterParams.ArenaPreset` on engage.
3. **`RegionMobSpawner.cs`** (ambient open-world packs; currently suppressed while `ff.overworldencounter` is ON) — the player-outside gate + top-up gate now use `SpawnAreaTable.HasAreaAt` when areas are authored; `SpawnPack` draws its enemy ids + level from `SpawnAreaTable.BuildDraw(origin)` (role-ordered composition), `seedBudget` lightly caps pack size. Legacy `RegionSpawnTable` per-member pick remains the fallback. Existing `PackRoleForIndex` role-stamp is unchanged (open-world pack healers already heal).
4. **`EncounterParams.cs`** — added `string ArenaPreset` (forwarded data; see fast-follow #1).

Emergent exclusion holds: only authored areas spawn; moat/water/seam/non-play belong to no area → no spawns.

**Verification done:** brace-balanced + no NUL bytes on all 4 `.cs`; both JSON copies parse (version 1, 4 areas). NOT compile-gated/committed (per owner) — CLI to batch-gate.

---

## Fast-follows (deliberately NOT built now)

1. **arenaPreset → actual arena size.** `BattleArena`'s footprint is `const` (`ArenaHalfWidth`/`ArenaHalfDepth`) and biome is a `BackdropContext` string — there is **no size param**. So `arenaPreset` is carried as a string on `EncounterParams.ArenaPreset` (data only) and BattleArena ignores it today. A later hook can convert those consts to preset-driven fields. **Do not build distinct arena geometry as part of V1.**
2. **Arena healers that actually heal.** In the isolated arena, `brain.SetHeroOnlyTarget(true)` makes `ChooseTarget` short-circuit to the hero *before* the role switch (`EnemyBrain.cs` ~line 883), so a Healer-role enemy in the arena never heals allies. (It's moot for the authored families — their "healer" slot ids `orc-mage`/`skeleton-mage`/`orc-shaman` resolve to Ranged/kiter in the arena, which is fine.) Open-world `RegionMobSpawner` pack healers DO heal. Making arena healers heal = a small follow-up in `EnemyBrain.ChooseTarget` (let Healer role resolve an ally even under hero-only).
3. **Squad formation positions in the arena.** V1 relies on BattleArena's existing north-line stage + `FamilyLeader` march + Kiter/Flanker tactics. Explicit "tanks front / dps behind / healer rear" formation slotting is a later polish (the roles already differentiate behavior).
4. **Skeleton/Troll enemy defs + models.** The far-region families reference `skeleton-*`/`troll-*` ids that aren't statted in `enemies.json` yet; they build via `EnemyFactory` fallbacks until statted. Orc ids are proven. Add defs/models when the Tripo skeleton/troll art lands, then the far regions theme correctly with zero JSON-schema change.

---

*Files: `Assets/_Modules/Core/World/SpawnAreaTable.cs` (new), `Assets/Resources/Data/Canonical/spawn-areas.json` (new), `Assets/StreamingAssets/Data/Canonical/spawn-areas.json` (new), `Assets/_Modules/Village/Arena/EncounterParams.cs`, `Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs`, `Assets/_Modules/Village/World/RegionMobSpawner.cs`.*
