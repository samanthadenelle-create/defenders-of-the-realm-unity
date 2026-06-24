# WORK_ORDER_506 — BATTLE ARENA: real scene + landscape (first pass)

**Status:** READY TO IMPLEMENT (all decisions resolved owner+Grok 2026-06-24) · World/Arena lane · 2026-06-24
**Origin:** owner directive "basics first — a scene and landscape, then bells & whistles; simple play should be
a scene." Synthesized from the Grok Prompt-1 brief + the real codebase (this WO is the SME extrapolation, not a
verbatim copy of the brief).

## 1. Goal
Replace the code-assembled primitive arena with a real, authored **place** — one good-looking open-kite arena
(~60 x 48) that reads as a location, built from real landscape art (ground + rocks/trees + proper lighting) —
so VFX/bloom/polish later sit on top of something real instead of a white box.

## 2. Current state (what we're replacing — verified)
- ONE script builds the arena: `Assets/_Modules/Village/Arena/BattleArena.cs` (1108 lines).
- Built procedurally at a FAR OFFSET `ArenaCentre = (5000,0,5000)` on a runtime root `[BattleArena_Stage]`
  (`_arenaRoot`), loaded ADDITIVELY over the home scene (no scene load).
- **`BuildArena(theme)` @ line 250 is the only method that changes.** Today it makes a primitive quad floor
  (`Resources/Arena/*.mat`), 4 invisible BoxCollider walls, an edge ring of rock/tree FBX, a 4-quad backdrop
  cyclorama (`Resources/Arena/Backdrops/<theme>.jpg`), a bloom volume, cavern mood.
- Dimensions: `ArenaHalfWidth = 30`, `ArenaHalfDepth = 24` (consts, keep).

## 3. KEEP (do not rewrite — reuse the verified loop)
`BeginEncounter(EncounterParams)` -> `StageRoutine` -> `SpawnFamily` -> fight -> `Resolve(won)` ->
`WarpHero(ReturnPosition/ReturnYaw)` -> teardown. The runtime navmesh bake (`ArenaNavMeshBaker`). The
logic/presentation split (`EncounterParams` is ids-only; presentation loads from ids). ONLY the world build
inside `BuildArena` changes.

## 4. Approach — RECOMMENDED: a per-biome arena LANDSCAPE PREFAB (with additive-scene as the alt)
The existing architecture (additive-over-home, far offset, `_arenaRoot`, runtime navmesh bake) makes a
**prefab** the lowest-risk way to get a real authored place WITHOUT a scene-load rework:
- Author a reusable **arena landscape PREFAB** per biome (a real ground mesh/terrain chunk + dressed rocks/
  trees + a tuned light/ambient + the backdrop), kept under `Resources/Arena/Stages/<biome>` (so it loads by
  name like the backdrops do).
- `BuildArena(theme)` becomes: resolve biome from `EncounterParams.BackdropContext` -> `Resources.Load` +
  `Instantiate` that stage prefab onto `_arenaRoot` at `ArenaCentre` -> ensure the walkable bounds match the
  kite extents -> `ArenaNavMeshBaker` bakes -> keep `SpawnFamily`/hero stance as-is.
- This keeps it ADDITIVE + far-offset (no second .unity scene to load/unload), is mobile-cheap (one authored
  prefab, no per-frame primitive assembly), and "the fight stays where you stood" stays trivial (pick the
  biome prefab from `BackdropContext`).
- **ALT (heavier):** a dedicated additive `.unity` arena scene per biome. More authentic baked lighting, but
  adds scene load/unload + origin handling + the §3 scene-corruption-care; defer unless the prefab look falls
  short. (NOTE §3: never hand-edit `.unity`; author via a builder.)

## 5. New `BuildArena` skeleton (target shape — implementer fills in)
```
private void BuildArena(string theme)  // theme = resolved biome key
{
    _arenaRoot = new GameObject("[BattleArena_Stage]");
    _arenaRoot.transform.position = ArenaCentre;
    var stage = Resources.Load<GameObject>("Arena/Stages/" + ResolveBiome(theme))
             ?? Resources.Load<GameObject>("Arena/Stages/outerworld");  // safe fallback
    if (stage != null) Instantiate(stage, _arenaRoot.transform);        // the REAL landscape
    else BuildFallbackFloor();                                          // degrade to a plain ground, never white
    EnsureKiteBounds();        // invisible walls / clamp to ArenaHalfWidth x ArenaHalfDepth
    // navmesh bake (ArenaNavMeshBaker) + SpawnFamily happen in StageRoutine as today
}
```

## 6. RESOLVED (owner + Grok, 2026-06-24) — these are now spec, not options
- **Prefab on the existing `_arenaRoot` at the far offset — YES.** Instantiate a landscape PREFAB onto the
  existing `_arenaRoot` at `ArenaCentre`. **Do NOT use a separate `.unity` scene** (keeps teardown simple, fits
  the current additive architecture).
- **Mesh ground + props — YES; NO Unity Terrain** (heavy on mobile). Use a **tiled mesh ground plane + placed
  rock/tree props** from the existing packs (`Resources/Arena/` Rock_*/Tree_* + KayKit/Quaternius). Lightweight,
  easy to offset.
- **NavMesh-bakeable ground is CRITICAL:** the ground must have a **`MeshCollider` (Read/Write-enabled mesh) on
  the Default layer** so `ArenaNavMeshBaker` bakes it reliably. No non-bakeable fancy surfaces.
- **ONE landscape first:** a single strong **generic open landscape (forest-clearing style)**. Biome variants
  (castle/cavern/...) come later via **material swaps / prop sets**, keyed off `EncounterParams.BackdropContext`.
- **Realtime lighting** (standard skybox + directional light + ambient) — **no baked lightmaps** for the arena;
  works fine at the (5000,0,5000) offset (no position-dependent issues expected — confirm during build).

## 7. Mobile-URP lighting (no whiteout — the lesson from this session)
- NO self-emissive white materials (the prior floor emitted white -> bloom blew out). Ground/scenery use lit
  materials with a real `_BaseColor`, emission OFF/black.
- Add a `Tonemapping` (Neutral) override wherever post-processing is on; keep bloom THRESHOLD high enough
  (>=1.2) that lit ground does not bloom — only true-HDR VFX.
- Prefer baked/soft directional + modest ambient; avoid forcing camera HDR without tonemapping.
- Keep it cheap: one stage prefab, simple colliders, a single bake.

## 8. Acceptance criteria
- A fight loads a real landscape (ground + props + lighting), NOT primitive quads; reads as a place.
- The full lifecycle still works (engage -> stage -> fight -> resolve -> warp back); navmesh bakes; enemies
  path + kite in the open space; teardown clean (no leaked stage on Resolve).
- No whiteout, no boxy fog. Gate-clean; headless markers green.
- BONES vs FINESSE: the load-mechanism + navmesh + lifecycle are CLI gate-provable; the ART DRESSING of the
  stage prefab (terrain look, prop placement, lighting feel) is owner/felt-tuned (authored, eyes-on).

## 9. Do NOT touch
The fight logic/lifecycle (BeginEncounter/StageRoutine/SpawnFamily/Resolve/WarpHero), the combat systems,
EncounterParams' contract, the HUD. VillageSceneBuilder.
