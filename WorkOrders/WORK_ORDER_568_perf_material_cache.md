# WORK ORDER 568 — Perf: shared-material cache in TripoMaterialFixer (P0-2)

**Status:** IMPLEMENTED (edit-only worktree; CLI to gate + commit)
**Date:** 2026-06-28
**Lane:** Combat/AI (code only, no scene files) — §9
**Source:** `docs/PERF/PERFORMANCE_AUDIT_2026-06-28.md` → P0-2 (the #1 runtime FPS win)
**File touched:** `Assets/_Modules/Core/TripoMaterialFixer.cs`

---

## RCA — confirmed from code (not assumption)

- **Alloc site:** `TripoMaterialFixer.cs:167` (pre-edit) — `var newMat = new Material(lit);`
  inside the per-renderer / per-material-slot loop of `Run()`. One brand-new **unshared**
  Material allocated for every slot, every time the fixer runs.
- **Per-spawn confirmation:** `Run()` fires from `Start()` (one-shot, guarded by `_ran`). The
  fixer is attached per enemy spawn for the whole roster via `EnemyFactory.cs:116`
  (Orc Warband / orc family) and `EnemyFactory.cs:134` (Troll); Demon/OgreMage take the
  texture-by-`.fbm` path. With `OverworldEncounterSpawner` re-topping ~6 reps continuously and
  the arena re-staging 3–7-body families, every body got unique materials.
- **What actually varies per built material (the cache key):**
  - shader — always `Universal Render Pipeline/Lit` (constant, but keyed for safety)
  - base map (`_MainTex`/`_BaseMap`, or `fallbackTex`)
  - base color (`_Color` after the near-black/alpha fixup + `_fallbackTint` override)
  - normal map (`_BumpMap`)
  - emission map + emission color + emissive flag (override-for-pets vs source-emission)
  - `_Smoothness`, `_Metallic` (per-fixer serialized fields)
- **Consequence proven by the audit:** (a) SRP batching can never coalesce two identical orcs
  (different material instances); (b) rebuilt materials are never `Destroy()`-ed on death →
  native material memory churns/accumulates until `Resources.UnloadUnusedAssets`.

## Implementation — shared-material cache

- **Key:** `MatKey` readonly struct (`IEquatable`) = `(shaderId, baseMapId, normalId,
  emissionMapId, baseColor, emissionColor, emissive, smoothness, metallic)`. Texture identity
  via `GetInstanceID()` (null → 0) to dodge Unity's overloaded `==`. Value-equality + stable
  `GetHashCode`.
- **Cache + lifetime:** `private static readonly Dictionary<MatKey, Material> s_matCache`
  (`TripoMaterialFixer.cs` field block, near the top of the class). **Static + long-lived** so
  the 2nd..Nth identical orc is a HIT across respawns — the whole point of killing the churn.
  A tiny bounded set (one entry per unique look).
- **Build-once helper:** `GetOrCreateSharedMaterial(...)` (added just above
  `VerifyAllRenderersUrp`). On HIT (and entry still alive) returns the cached instance; on MISS
  builds the URP/Lit material with the **identical property writes, in the same order** as the
  old inline block, caches it, returns it.
- **sharedMaterial usage:** the loop assigns `matsRef[i] = sharedMat` and writes back via the
  pre-existing `r.sharedMaterials = matsRef;` (already used `sharedMaterials`, never `.material`)
  — so no per-renderer instance is forced. Identical-look slots reference ONE Material.
- **Dead-entry guard:** `TryGetValue(...) && cached != null` rebuilds if an
  `UnloadUnusedAssets` sweep destroyed a cached material, so we never assign a destroyed mat.

## Identical visual — why it holds
- Same resolve logic (tex/col fixups, fallback tex, fallback tint, normal, emission override vs
  source) feeds the same property writes. The cache key encodes every input that affects the
  output, so two slots share a material **only when they would have rendered identically**. Any
  slot with a unique texture/tint/emission gets its own entry → no look changes.

## FlowTrace proof (headless-provable)
- Static counters `s_cacheHits` / `s_cacheNew` incremented in the helper.
- Existing summary line now reports them per Run:
  `"{name}: rebuilt N slot(s) across M renderer(s). matCache: H hit / K new, size=S (P0-2 shared-material win)."`
  A high hit:new ratio across spawns = the win landed (e.g. first orc = 1 new, every later
  identical orc = +1 hit, cache size flat).

## Validation
- Brace check: **45 open / 45 close — balanced.** No NUL bytes.
- No `.unity` scene edits. No new `System.Reflection`. Code-only, single file.

## NOT done here (separate follow-up)
- **P0-1 (Mesh-Baker 6→1 combine)** is an OFFLINE ASSET BAKE on the `Resources/Enemies` Tripo
  prefabs (one combined SkinnedMeshRenderer + atlas per character) — no runtime code. Out of
  scope for this code-only WO; route as its own asset-bake work order.

## Owner-decision flags
- The cache is shared across ALL `TripoMaterialFixer` users (enemies, pets, heroes, buildings).
  Cross-type sharing only happens on a byte-identical tuple → visually safe and a bonus batching
  win. Flagging in case the owner wants the cache scoped to enemies only (not recommended).
- Materials are intentionally never freed (bounded unique-look set). If the look-space ever grows
  large (many distinct tinted variants), revisit with an LRU — not needed at current roster size.
