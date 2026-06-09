# WORK_ORDER_388 — Load the player's real castle as the Arena defender base

**Status:** SPEC — implementation gated on the 2 verification questions (§3). Surgical change is small; the glue depends on the verify result.
**Lane:** 2 Combat/AI (+ a tiny UI toggle in ArenaPanel). Code-only; may need a NavMesh step.
**Source:** This session (2026-06-09). Owner-relayed proposal, refined.

## Goal
When the player does an Arena raid, fight against (or mirror) the **player's own built castle** (`GameState.BaseLayout`) instead of the seeded `ArenaCatalog` wood/stone forts. Foundation for the CoC raid loop + the watchable battle (WO-386).

## Today (verified, WO from prior investigation)
Every raid uses a SEEDED fort: `ArenaPanel.ConfirmRaid(opp)` → `ArenaMode.TryStartRaid(opp)` → `SpawnOpponentBase` → `EnemyOutpost.ConfigureArena(opp.Id, opp.Threat, opp.BaseRecipe, opp.GuardCount)` (`ArenaMode.cs:153`) → `BuildFortification` → `OutpostFoundationGenerator.Realize`. `opp.BaseRecipe` = `OutpostFoundationGenerator.GenerateFootprintRecipe(...)`. The player base (`GameState.BaseLayout`) is explicitly NOT used (`EnemyOutpost.cs:220`).

## The change (SURGICAL — do NOT wholesale-replace SpawnOpponentBase)
The defender-recipe pipeline already takes `List<PlacedStructureData>` end-to-end. Change ONLY the recipe argument at `ArenaMode.cs:153` + add a helper. Preserve everything else in `SpawnOpponentBase`/`ConfigureArena` (`_suppressClearReward` at :168, the `OnCleared` subscription at :131, the `_arenaRecipe ?? generated` fallback at :223).
```csharp
// at the ConfigureArena call site: pass the resolved defender recipe
_outpost.ConfigureArena(opponent.Id, opponent.Threat, GetDefenderRecipe(opponent), opponent.GuardCount);

private List<PlacedStructureData> GetDefenderRecipe(ArenaOpponentDef opponent)
{
    // GATED behind the "Use My Castle" toggle (default OFF = seeded, safe live path).
    if (UsePlayerCastle)
    {
        var state = GameStateService.Instance?.State;
        if (state?.BaseLayout != null && state.BaseLayout.Count > 0)
        {
            Debug.Log($"[ArenaMode] Defender = player's castle ({state.BaseLayout.Count} structures).");
            return state.BaseLayout;
        }
        Debug.LogWarning("[ArenaMode] Use-My-Castle on but no player base — falling back to seeded fort.");
    }
    return opponent.BaseRecipe;   // seeded (the BuildFortification ?? also covers null)
}
```

## Debug toggle (owner-requested)
Add a **"Use My Castle"** toggle to `ArenaPanel` (default OFF) bound to `ArenaMode.UsePlayerCastle`, so you can A/B player-castle vs seeded opponents in-play without code changes. This is also the SAFETY boundary: default-off keeps the verified seeded path intact until the player-castle path is proven.

## §3. VERIFIED 2026-06-09 + Phase-2 implementation plan (DECIDED: runtime NavMesh bake)

**RESULT — Q1 ✅ no bridge; Q2 🚩 needs a runtime bake (the real work).**
- **Q1 id-resolution SOLVED:** `OutpostFoundationGenerator.Realize` resolves each piece via `CatalogRegistry.Get(itemId)` + `StructureFactory.Create` — the SAME two calls `BaseLayoutLoader.Spawn` uses. One shared registry, identical id namespace → `GameState.BaseLayout` renders correctly; unknown ids log+skip (safe). Caveat: `Realize` ignores per-piece `level` → defender fort renders at base tier (cosmetic).
- **Q2 NavMesh:** there is NO runtime bake anywhere — the Arena relies on PRE-BAKED scene mesh, and fort pieces add CARVING NavMeshObstacles (they *remove* mesh). Anchor = `hero.pos + 24m fwd` (arbitrary). Imported castle → high silent-win/stuck risk. **Decision: Phase-2 runtime bake.**
- **asmdef ✅:** `DeNelle.Village.asmdef` ALREADY references `Unity.AI.Navigation` (line 22) → use `NavMeshSurface` DIRECTLY (no reflection; Grok's typed code compiles).

### Phase-2 plan (corrected vs the relayed code — do NOT drop Grok's bake in verbatim)
1. **Recipe swap + toggle** (§ above): `GetDefenderRecipe` returns `GameState.BaseLayout` when "Use My Castle" is ON, else seeded; pass **`null` (not empty)** when `BaseLayout.Count==0` so the `_arenaRecipe ?? generated` fallback fires.
2. **Bake INSIDE `EnemyOutpost`, AFTER `BuildFortification()`** (Start → BuildFortification → bake → SpawnGarrison), NOT in `ArenaMode` after `ConfigureArena` — the fort is built in `Start` (next frame), so an earlier bake = empty mesh.
3. **Add a walkable floor BEFORE baking** — recipe is structures, no ground. Add an invisible `MeshCollider` floor child sized to the realized footprint (+margin) at the outpost root (the `CastleHubBuilder` invisible-nav-floor pattern). The existing CARVING wall-obstacles then cut the walls out → floor walkable, walls block. **This is the piece Grok's version is missing.**
4. **Bake (sync v1):** `NavMeshSurface` on the outpost host — `collectObjects=Children`, `useGeometry=PhysicsColliders`, agent radius 0.4 / height 1.8 / climb 0.8 / slope 45 / step 0.45 (match enemies + stairs), `layerMask = Default|Structure|Environment`, voxel ~0.15 / tile 256 (tunable for mobile). Use **`BuildNavMesh()` (sync)** — `BuildNavMeshAsync()` is NOT a `NavMeshSurface` method (async needs `NavMeshBuilder.UpdateNavMeshDataAsync`); defer async + progress bar until the sync path is proven.
5. **Clamp the anchor** onto walkable mesh (`NavMesh.SamplePosition` on `ResolveRaidAnchor`) so the local bake + world seam are sane.
6. **Multi-level (spiral stairs / upper battlements of an imported castle) DEFERRED** — same StairwayBuilder + upper-plane story as WO-384; v1 = ground-level walkable.

### Implementation shape (dedicated `ArenaNavMeshBaker.cs` — owner-relayed design, corrected)
- New `ArenaNavMeshBaker` MonoBehaviour (`DeNelle.Village.Arena`): `BakeForCastle(root)` → coroutine that waits for the fort to build (`Realize`+`Start`), adds the invisible walkable floor, configures + bakes the `NavMeshSurface`. Clean separation — keep it.
- CORRECTIONS to the relayed draft: (a) **sync `BuildNavMesh()`**, NOT `BuildNavMeshAsync()` (not a `NavMeshSurface` method); (b) floor scale ~**5** (≈50m), NOT 60 (=600m — a Unity Plane is 10m/unit); size to footprint+margin if cheap; (c) **toggle-gated** — `ArenaMode` adds the baker + passes `GameState.BaseLayout` ONLY when "Use My Castle" is ON (the relayed `SpawnOpponentBase` bakes + swaps for EVERY raid — do NOT change the seeded path); (d) `agentTypeID=0` to match the default enemy/hero agent.
- The bake is RUNTIME (at raid start) — so NO editor bake needed; once committed, you just toggle "Use My Castle" and raid to test it.

### Build timing
Runtime-bake CODE change in `EnemyOutpost`/`ArenaMode` + new `ArenaNavMeshBaker.cs` → implement with **Unity CLOSED** for the compile-gate (and so I don't inject unverified code into a live playtest session). The navmesh bake itself happens at runtime in Play — no editor bake.

---

### (Original verify questions — now answered above)
1. **ID resolution.** Does `OutpostFoundationGenerator.Realize(recipe, ...)` resolve `PlacedStructureData` ids via `CatalogRegistry`/`StructureFactory` (so player BaseLayout catalog-ids render) — or only its OWN generated ids (`IdGate`/`IdCornerTower`/wall ids)? Player layout uses `CatalogRegistry` ids; if Realize can't resolve them, the castle won't render → may trip the silent-win. If a mismatch: add an id-bridge (map/resolve player ids through the same factory Realize uses), OR route player layout through `BaseLayoutLoader`'s resolution path. **This is the gating unknown.**
2. **NavMesh at the raid anchor.** How does the Arena fort get walkable NavMesh today (runtime bake / NavMeshSurface / existing scene mesh)? Garrison are NavMeshAgents. A larger/different player castle must still produce walkable NavMesh + a clear garrison spawn, or `EnemyOutpost.SpawnGarrison` (`:254-260`) auto-`Clear()`s → **silent win with nothing drawn**. May need a rebake/local-surface step at the anchor.

## Acceptance criteria
- [ ] Toggle OFF → behaviour identical to today (seeded forts). No regression.
- [ ] Toggle ON with a built player base → the **player's actual castle renders** at the raid anchor (real structures, not a generated ring) and the garrison spawns on walkable NavMesh.
- [ ] Toggle ON with NO player base → graceful fallback to seeded (logged), no silent win.
- [ ] Kill→XP + win/lose + SKR result flow unchanged.
- [ ] Player-base item-ids resolve (no missing/null structures); if any don't, they're logged, not silently dropped.

## What NOT to touch
- Do NOT wholesale-replace `SpawnOpponentBase` — change only the recipe argument + add the helper/toggle.
- Keep `_suppressClearReward`, the `OnCleared` subscription, and the `_arenaRecipe ?? generated` fallback intact.
- Default the toggle OFF until §3 is verified — don't break the live seeded path.
