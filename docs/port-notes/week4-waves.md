# Week 4 — Wave Manager + Enemies + Breach-to-ATB

**Date:** 2026-05-19
**Slice:** v2-unity-port-spec.md Part 5 Week 4 — "the village plays Wave 1 end-to-end. An enemy can breach; on breach, the scene transitions to ATB."
**Status:** Source files written. Integration items below are open (no Unity access — cannot bake NavMesh, build prefabs, or wire the scene).

## Files produced

| File | Purpose |
| ---- | ------- |
| `Assets/StreamingAssets/Data/Canonical/enemies.json` | The in-village wave roster — the Hollow Ones (KayKit skeleton archetypes + Necromancer boss). |
| `Assets/StreamingAssets/Data/Canonical/waves.json` | Per-wave spawn schedule + Prepare-Phase countdowns. Wave 1 = 8 Hollow Walkers from the north gate. |
| `Assets/_Modules/Village/Waves/WaveData.cs` | Typed C# records (`EnemyDef`/`EnemyCatalog`, `WaveBatch`/`WaveDef`/`WaveSchedule`) + `WaveDataLoader` (async StreamingAssets reader). |
| `Assets/_Modules/Village/Waves/WaveManager.cs` | Countdown timer, per-wave/per-batch spawning, inner-ring breach detection, ATB hand-off. |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | NavMeshAgent enemy — marches to the Heart, contact-attacks structures, dies at 0 HP. Defines `IDamageableStructure`. |

All five live in the `DeNelle.Village` asmdef (it already references `DeNelle.Core`, `DeNelle.Data`, `UniTask`) — no asmdef change needed. The legacy `UnityEngine.AI` namespace (NavMeshAgent) comes from `com.unity.modules.ai` 1.0.0, already in the manifest and auto-referenced.

`VillageController.cs` was deliberately NOT edited — wiring the WaveManager into the scene is the integrator's job.

## Sourced vs. authored data

**Sourced verbatim from the React v1 repo** (read-only — nothing written there):

- `src/modules/village/enemies/enemyArchetypes.ts` — `KAY_ENEMIES` gives the three skeleton stat blocks: Minion hp 52 / speed 2.5, Warrior hp 156 / speed 2.2, Rogue hp 88 / speed 3.1 (HP already includes the global `ENEMY_HP_BUFF` ×2). `NECROMANCER` gives the boss: hp 1700 / speed 1.5. `ENEMY_DMG_TO_HEART = 6` and `BOSS_DMG_TO_HEART = 17` become the per-hit contact damage; `ENEMY_ATTACK_INTERVAL = 1.3` becomes `attackInterval`.
- `src/modules/village/waves/waveConfig.ts` — `PREPARE_SECONDS = 45` (first wave) and `LATER_PREPARE_SECONDS = 300` (later waves) become the `countdownSeconds` values.
- `src/data/battleScaling.ts` — `waveScaling().enemyCount`: wave 1 = 8, later waves = `min(8 + steps*2, 12)`. Confirms Wave 1 = 8 enemies, matching the port spec.
- `src/modules/village/waves/EnemyWaves.tsx` — confirms boss recurrence `BOSS_EVERY = 6`.

**Authored (defensible values, not in the React repo):**

- **Enemy ids/names.** React keys enemies by GLB url; this port assigns canon ids `hollow-walker` / `hollow-warrior` / `hollow-rogue` / `necromancer` and the canon name "Hollow Ones" (narrative bible / port spec Part 1). The mapping skeleton→Hollow One is a naming choice; stats are unchanged.
- **Waves 2 and 3.** The React stream generates waves procedurally — there is no literal wave-2/3 table to copy. `waves.json` authors two follow-on waves so the `WaveManager` has a multi-wave schedule to drive. Enemy counts (7+3, 6+3+3) sit inside the React `enemyCount` cap of 12. These are placeholders; re-sync if the React stream ships an explicit wave table.
- **`delay` / `interval` per batch.** The React `EnemyWaves.tsx` dumps a wave in one tick at scattered ring positions. This port staggers spawning (`interval` seconds between enemies, `delay` before a batch starts) so a march reads cleanly from a single spawn marker. Tuning-only; no canon impact.
- **`ai` archetypes** (`walker`/`charger`/`skirmisher`) — from the port spec Part 3 enemy row. Wave 1 is all `walker`. `EnemyAiKind.Charger` is defined but no enemy uses it yet.
- **`innerRingRadius = 9u`** — the breach ring. The curtain wall (`WallLayout`) is a rectangle with half-extents 28u × 21u; 9u sits well inside it, around the building cluster near the Heart. Integrator should tune against the actual built scene.

## API wired against

**SceneRouter** (`Assets/_Modules/Core/SceneRouter.cs`) — the breach uses the real API:

```csharp
SceneRouter.GoBattle(new BattleParams {
    Wave = currentWaveId,
    BreachedIds = <3D-layer ids of breaching enemies>,
    ParticipatingPetIds = System.Array.Empty<string>(),
}).Forget();
```

`GoBattle` stashes the `BattleParams` on `SceneRouter.PendingBattle` and fades into the `ATBBattle` scene. `BattleParams` fields used: `Wave` (int), `BreachedIds` (string[]), `ParticipatingPetIds` (string[]) — exactly the public shape. `GoBattle` returns a `UniTask`; the WaveManager fire-and-forgets it with `.Forget()` (never `async void`, port spec Part 3).

**BattleController** (`Assets/_Modules/BattleATB/BattleController.cs`) — the far side of the hand-off:

- `BattleController.BuildSetup()` reads `SceneRouter.PendingBattle`; a `Wave > 0` is treated as a village breach (`BattleSource.Village`).
- It currently maps the breach to a single fallback enemy def (`_fallbackEnemyDefId = "skeleton"`). Its own code comment notes that mapping `BreachedIds` (3D-layer ids) → engine `ENEMY_DEFS` keys "is the Week-4 breach trigger's job."
- This slice carries that mapping data forward: `Enemy.EngineDefId` returns `"skeleton"` for the skeleton archetypes and `"necromancer"` for the boss — the engine def keys BattleController expects. **The per-enemy roster mapper inside BattleController is still a stub** (it builds one enemy, not the full breach roster). Completing that is a small BattleController follow-up, out of this slice's scope; the WaveManager already supplies everything it needs via `PendingBattle.BreachedIds`.
- After the battle resolves, `BattleController.ReturnAfterResult()` fades back to the `Village` scene. The Village scene reloads fresh, so a new `WaveManager.Start()` runs `BeginLoop()` again and the wave loop resumes.

## Integration items (open — need Unity)

1. **NavMesh baking — REQUIRED.** Enemies use `UnityEngine.AI.NavMeshAgent`. The Village scene **must have a baked NavMesh** or no enemy will move. `Enemy.DriveNav()` detects "no NavMesh" and logs one warning per enemy rather than crashing, but the loop is non-functional until baking is done.
   - Only `com.unity.modules.ai` (legacy) is in the manifest — **not** the high-level `com.unity.ai.navigation` package (NavMeshSurface / runtime bake). So baking is via the legacy **Window → AI → Navigation** panel: mark the ground plane + wall/building meshes Navigation Static and Bake. If the integrator prefers runtime baking, add `com.unity.ai.navigation` to the manifest first.
2. **WaveManager component.** Add a `WaveManager` MonoBehaviour to the Village scene (its own GameObject — it is a self-contained sub-system). Optionally wire `_heart`, `_spawnPoints`, `_enemyRoot`, `_enemyPrefab` in the inspector; left blank, `WaveManager` auto-finds the `HeartController` and every `WaveSpawnPoint` in the scene at `Start`.
3. **WaveSpawnPoint placement.** `waves.json` references spawn ids `spawn-0` (N) … `spawn-3` (W). `WaveSpawnPoint` markers with those `SpawnId`s must exist in the scene (the Week-3 `VillageSceneBuilder` places them). Wave 1 needs `spawn-0` only.
4. **Enemy prefab.** Build a prefab from `Assets/Models/KayKit/enemies/Skeleton_Minion.glb` (the Hollow Walker mesh — same KayKit skeleton the React stream uses) with an `Enemy` + `NavMeshAgent` component, and assign it to `WaveManager._enemyPrefab`. Until then `WaveManager` spawns a primitive-capsule placeholder so the loop is still testable.
5. **`IDamageableStructure` on Building/Wall/Gate.** `Enemy` contact-attacks anything implementing `IDamageableStructure` (defined in `Enemy.cs`). `Building.cs`, `WallSegment.cs` and `Gate.cs` do NOT implement it yet — they have `_hp` fields but no public damage method. When their HP gameplay lands, add `IDamageableStructure` to them; until then enemies simply path through to the Heart (which still triggers the breach correctly).
6. **VillageController wiring.** Per task constraints `VillageController.cs` was not edited. If the integrator wants `VillageController` to own/expose the `WaveManager` (e.g. a `RegisterWaveManager` similar to `RegisterBuilding`), that is a one-line addition there.
7. **BattleController roster mapper.** See "API wired against" above — `BattleController.BuildSetup()` still builds a single fallback enemy. Expanding it to build one combatant per `PendingBattle.BreachedIds` entry is a follow-up in the BattleATB module.
8. **Heart threat state.** `WaveManager` calls `HeartController.SetState(Vigilant / Critical / Serene)` on wave start / breach / clear. `HeartController.SetState` currently just records the value (per-frame emissive ease is Week 4+ Heart work) — wave-driven state is now feeding it.

## Decisions worth a row in unity-decisions.md (not added — that file is integrator-owned)

- Split the village wave roster into a dedicated `enemies.json` rather than overloading the (future) 400-row Heartforge `enemies.json`. The wave loop only needs the ~4 KayKit archetypes; keeping them separate avoids loading 400 rows to spawn skeletons. Reversible — could merge later behind a `tier`/`source` filter.
- `IDamageableStructure` interface defined inside `Enemy.cs` so `Enemy` has zero compile-time dependency on the Building/Gate damage API (which does not exist yet). Reversible — mechanical to move to a shared file.
- Breach hand-off ABANDONS the rest of the in-progress wave (the breaching enemies are `Kill()`-ed into the ATB scene; remaining field enemies are cleared; the loop re-runs the same wave on return). Simpler than serialising mid-wave field state across the scene load. Reversible if mid-wave persistence is later wanted.
