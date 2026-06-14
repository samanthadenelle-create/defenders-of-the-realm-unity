// =============================================================================
// GarrisonController — the runtime brain on an additive-loaded enemy GARRISON
// scene (Garrison_TrollOutpost / Garrison_RuinedKeep / Garrison_HillFort).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE LOOP: a garrison scene is loaded ADDITIVELY (its geometry + props are
// authored by GarrisonSceneBuilder), then something (a raid manager / a debug
// "Activate" / the scene-loaded callback) calls Activate() once the navmesh is
// live. Activate() spawns the initial Troll/Stonebelly garrison at the authored
// EnemySpawnPoints. When the raid ends, CleanupAndUnload() unloads THIS scene
// (gameObject.scene) — no orphans, no leaks.
//
// SPAWNING IS NOT REINVENTED. Every guard is built through the ONE canonical
// enemy-creation path the whole project uses (CLAUDE.md §9):
//   EnemyFactory.Build(def, pos, rot, parent)  -> a real, hittable Enemy
//   enemy.Configure(id, def, anchor)            -> stat block + tether goal
//   enemy.SetBrainTarget(anchor)                -> HOLD the garrison (don't march
//                                                  the Heart); hero aggro still
//                                                  pulls them into the fight
// This mirrors EnemyOutpost.SpawnGuard exactly — the garrison defenders read the
// same as every other open-world enemy and fight via the existing TargetManager
// auto-combat (ZERO new combat / targeting code).
//
// enemyPrefabs[] is an OPTIONAL inspector hook: if the owner drops authored enemy
// prefabs in, a spawn point can instantiate one directly instead of building from
// a code EnemyDef. Empty (the default) -> every spawn uses the EnemyFactory path
// with the Troll/Stonebelly stat blocks. So it degrades gracefully either way.
//
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// LogWarning, never error, on a missing piece (pack/nav-miss safe).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
// EnemyDef / Enemy / EnemyFactory all live in the parent namespace DeNelle.Village,
// visible here because DeNelle.Village.World.Camps nests under it (same as EnemyOutpost).

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Lives on a garrison scene's <c>GarrisonRoot</c>. Spawns a Troll/Stonebelly
    /// garrison at the authored spawn points on <see cref="Activate"/>, and unloads
    /// its own scene on <see cref="CleanupAndUnload"/>. Spawning routes through the
    /// canonical <see cref="EnemyFactory"/> path — no parallel spawner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarrisonController : MonoBehaviour
    {
        // -- Inspector / builder-wired -----------------------------------------

        [Header("Spawn layout (wired by GarrisonSceneBuilder)")]
        [Tooltip("Authored garrison stand positions. Each spawns one defender on Activate(). " +
                 "Usually the children of the EnemySpawnPoints group.")]
        public Transform[] spawnPoints;

        [Tooltip("OPTIONAL authored enemy prefabs. If non-empty, a spawn point instantiates one " +
                 "of these (round-robin) instead of building a Troll/Stonebelly via EnemyFactory. " +
                 "Leave empty to always use the canonical EnemyFactory.Build path.")]
        public GameObject[] enemyPrefabs;

        [Header("Tuning")]
        [Tooltip("Threat tier — scales guard HP/damage exactly like EnemyOutpost / CampGuards.")]
        public int threatLevel = 2;

        [Header("Recipe (wired by GarrisonSceneBuilder from garrison-recipes.json)")]
        [Tooltip("Enemy ids that staff this garrison (EnemyFactory model-map keys, e.g. " +
                 "\"troll\", \"orc-berserker\", \"hollow-walker\"). Round-robined across the " +
                 "spawn points. Empty => the legacy Troll/Stonebelly default mix.")]
        public string[] enemyTypeIds;

        [Tooltip("Inclusive [min,max] level band. Each defender rolls a level in this band and " +
                 "its stats scale with the level. [0,0] (default) => no level scaling (legacy).")]
        public int minLevel = 0;
        public int maxLevel = 0;

        [Tooltip("Auto-spawn on Start (handy for opening the scene directly to test). " +
                 "A raid manager that owns the lifetime should leave this OFF and call Activate().")]
        public bool activateOnStart = false;

        // -- Runtime state -----------------------------------------------------

        /// <summary>Living garrison members remaining (0 once the garrison is wiped).</summary>
        public int AliveCount => _aliveCount;

        /// <summary>Total members this garrison spawned.</summary>
        public int TotalGarrison => _garrison.Count;

        /// <summary>True once the whole garrison is dead (the scene is clear).</summary>
        public bool Cleared { get; private set; }

        /// <summary>Raised once every defender is dead (the garrison is cleared).</summary>
        public event System.Action<GarrisonController> OnCleared;

        private readonly List<Enemy> _garrison = new List<Enemy>();
        private Transform _garrisonRoot;
        private int _aliveCount;
        private bool _activated;
        private int _prefabCursor;
        private int _typeCursor;
        private System.Random _levelRng;

        private void Start()
        {
            if (activateOnStart) Activate();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _garrison.Count; i++)
                if (_garrison[i] != null) _garrison[i].Died -= HandleGarrisonDied;
        }

        // =====================================================================
        // ACTIVATE — spawn the initial garrison. Idempotent (a second call is a
        // logged no-op). Call this AFTER the navmesh is live (EnemyFactory snaps
        // each spawn to the nearest navmesh point, but the surface must be baked).
        // =====================================================================

        /// <summary>Spawn the initial Troll/Stonebelly garrison at the authored spawn points.</summary>
        public void Activate()
        {
            if (_activated)
            {
                Debug.LogWarning($"[GarrisonController] {name} already activated — ignoring duplicate Activate().");
                return;
            }
            _activated = true;
            SpawnInitialGuards();
            ArmGarrisonTurrets();
        }

        // =====================================================================
        // ARM TURRETS — make the authored watchtower props FUNCTIONAL turrets that
        // shoot the PLAYER PARTY (owner decision). The garrison scene authors
        // watchtowers as inert decoration (GarrisonSceneBuilder.PlaceRing labels
        // them "Watchtower_*"); here we attach an EnemyOwned DefenseTower so they
        // open fire on the hero + companions. Runtime-only (no scene re-bake) and
        // guarded by SceneOwnership.IsEnemyOwned so this can NEVER arm a friendly
        // village tower against the player. Idempotent: a tower that already
        // carries a DefenseTower is skipped.
        // =====================================================================

        [Header("Garrison turrets")]
        [Tooltip("Range (world units) for an armed garrison watchtower turret.")]
        public float turretRange = 16f;
        [Tooltip("Damage per shot for an armed garrison watchtower turret.")]
        public float turretDamage = 8f;
        [Tooltip("Shots per second for an armed garrison watchtower turret.")]
        public float turretFireRate = 0.8f;

        private void ArmGarrisonTurrets()
        {
            // Scan + arm via the SHARED armer (the ownership hard-guard + Watchtower_*
            // scan live there now, identical logic). Scope to THIS controller's own
            // scene only (not other additive scenes).
            int armed = GarrisonTurretArmer.ArmWatchtowers(
                gameObject.scene, turretRange, turretDamage, turretFireRate);

            if (armed > 0)
                Debug.Log($"[GarrisonController] {name} armed {armed} watchtower turret(s) (EnemyOwned) on the player party.");
        }

        /// <summary>
        /// Build one defender per spawn point through the canonical EnemyFactory path
        /// (or instantiate an authored prefab if <see cref="enemyPrefabs"/> is supplied).
        /// Each defender is tethered to its own anchor so it HOLDS the garrison.
        /// </summary>
        public void SpawnInitialGuards()
        {
            _garrisonRoot = new GameObject("[Garrison]").transform;
            _garrisonRoot.SetParent(transform, false);
            _garrisonRoot.localPosition = Vector3.zero;

            var points = ResolveSpawnPoints();
            if (points.Count == 0)
            {
                Debug.LogWarning($"[GarrisonController] {name} has no spawn points — no garrison spawned.");
                return;
            }

            // Deterministic level-roller so a given garrison reads the same every load.
            _levelRng = new System.Random((name != null ? name.GetHashCode() : 0) ^ 0x5EED);

            for (int i = 0; i < points.Count; i++)
            {
                Transform sp = points[i];
                if (sp == null) continue;

                // A defender is a TROLL by default; every 3rd is the lighter, faster
                // "Stonebelly" variant for silhouette variety (still the Troll family/model).
                bool stonebelly = (i % 3) == 2;
                SpawnOne(sp.position, sp.rotation, stonebelly, i);
            }

            if (_aliveCount == 0)
            {
                Debug.LogWarning($"[GarrisonController] {name} spawned 0 living defenders " +
                                 "(no navmesh under the spawn points? prefabs null?) — treating as cleared.");
                MarkCleared();
            }
            else
            {
                Debug.Log($"[GarrisonController] {name} garrison spawned: {_aliveCount} defender(s) " +
                          $"across {points.Count} spawn point(s), threat {threatLevel}.");
            }
        }

        // Resolve the spawn list: explicit spawnPoints[] first; else fall back to the
        // children of an "EnemySpawnPoints" group under this root (the builder layout).
        private List<Transform> ResolveSpawnPoints()
        {
            var list = new List<Transform>();
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                    if (spawnPoints[i] != null) list.Add(spawnPoints[i]);
                if (list.Count > 0) return list;
            }

            var group = transform.Find("EnemySpawnPoints");
            if (group != null)
            {
                for (int i = 0; i < group.childCount; i++)
                    list.Add(group.GetChild(i));
            }
            return list;
        }

        // Spawn exactly one defender at a point. Prefer an authored prefab when supplied,
        // else build through the canonical EnemyFactory path with a Troll/Stonebelly def.
        private void SpawnOne(Vector3 wantPos, Quaternion rot, bool stonebelly, int index)
        {
            Vector3 pos = SnapToNav(wantPos);

            // OPTIONAL authored prefab path (round-robin). The prefab is expected to
            // already carry an Enemy (so it tracks + dies like the rest); if it does
            // not, we still parent + track it but it won't count toward the clear.
            if (enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                GameObject prefab = enemyPrefabs[_prefabCursor % enemyPrefabs.Length];
                _prefabCursor++;
                if (prefab != null)
                {
                    var inst = Object.Instantiate(prefab, pos, rot, _garrisonRoot);
                    var e = inst.GetComponent<Enemy>();
                    if (e != null)
                    {
                        var a = MakeAnchor($"GuardAnchor-{index}", pos);
                        e.SetBrainTarget(a);
                        Track(e);
                    }
                    else
                    {
                        Debug.LogWarning($"[GarrisonController] enemyPrefab '{prefab.name}' has no Enemy component — " +
                                         "spawned as scenery, not counted in the garrison.");
                    }
                    return;
                }
            }

            // Canonical path: build a real, hittable Enemy via the ONE shared factory.
            // RECIPE-FIRST: when enemyTypeIds[] is supplied (by the recipe-driven builder)
            // the defender's id + base stats come from that id; otherwise the legacy
            // Troll/Stonebelly mix is used. Either way a LEVEL is rolled from [minLevel,
            // maxLevel] and folded into the stat scale (level 1 + threat == legacy).
            int level = RollLevel();
            EnemyDef def = (enemyTypeIds != null && enemyTypeIds.Length > 0)
                ? GarrisonStatBlocks.BuildTypedDef(NextTypeId(), level)
                : (stonebelly ? GarrisonStatBlocks.BuildStonebellyDef(threatLevel)
                              : GarrisonStatBlocks.BuildTrollDef(threatLevel));
            GarrisonStatBlocks.ApplyLevelScale(def, level);

            var enemy = EnemyFactory.Build(def, pos, rot, _garrisonRoot);
            if (enemy == null)
            {
                Debug.LogWarning($"[GarrisonController] EnemyFactory returned null for '{def.Id}' at {pos} — skipped.");
                return;
            }
            enemy.gameObject.name = $"GarrisonGuard ({def.Id}-Lv{level}-{index})";

            var anchor = MakeAnchor($"GuardAnchor-{index}", pos);
            enemy.Configure($"garrison-{def.Id}-{index}", def, anchor);
            enemy.SetBrainTarget(anchor);   // HOLD the garrison; hero aggro still pulls them in

            Track(enemy);
        }

        private void Track(Enemy e)
        {
            e.Died += HandleGarrisonDied;
            _garrison.Add(e);
            _aliveCount++;
        }

        // A local tether anchor so the defender holds the garrison instead of marching
        // off. Mirrors EnemyOutpost.MakeAnchor.
        private Transform MakeAnchor(string anchorName, Vector3 pos)
        {
            var go = new GameObject(anchorName);
            go.transform.SetParent(_garrisonRoot, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static Vector3 SnapToNav(Vector3 want)
        {
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                return hit.position;
            return want;
        }

        // =====================================================================
        // CLEAR — last defender dies -> mark cleared + raise OnCleared.
        // =====================================================================

        private void HandleGarrisonDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleGarrisonDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0) MarkCleared();
        }

        private void MarkCleared()
        {
            if (Cleared) return;
            Cleared = true;
            Debug.Log($"[GarrisonController] {name} CLEARED — garrison wiped.");
            OnCleared?.Invoke(this);
        }

        // =====================================================================
        // CLEANUP + UNLOAD — additive teardown. Unloads THIS controller's scene
        // (the whole garrison scene) so there are no orphans/leaks. Safe to call
        // even if the scene was opened single (it no-ops with a warning then).
        // =====================================================================

        /// <summary>
        /// Unload the garrison scene this controller lives in. Use when the raid ends
        /// and the player returns to the open world. Asynchronous; returns the operation
        /// (or null if the scene can't be unloaded — e.g. it is the only loaded scene).
        /// </summary>
        public AsyncOperation CleanupAndUnload()
        {
            // Drop any still-living defenders' event hooks first (defensive — OnDestroy
            // also does this, but the scene unload destroys them en masse).
            for (int i = 0; i < _garrison.Count; i++)
                if (_garrison[i] != null) _garrison[i].Died -= HandleGarrisonDied;

            Scene myScene = gameObject.scene;
            if (!myScene.IsValid() || !myScene.isLoaded)
            {
                Debug.LogWarning($"[GarrisonController] {name} scene is not a valid loaded scene — nothing to unload.");
                return null;
            }
            if (SceneManager.sceneCount <= 1)
            {
                Debug.LogWarning($"[GarrisonController] {name} is in the only loaded scene — refusing to unload " +
                                 "(would leave no active scene). Load the return scene first.");
                return null;
            }

            Debug.Log($"[GarrisonController] Unloading garrison scene '{myScene.name}'.");
            return SceneManager.UnloadSceneAsync(myScene);
        }

        // =====================================================================
        // RECIPE-DRIVEN level + type helpers.
        // -----------------------------------------------------------------------------
        //  * RollLevel    — pick an inclusive level in [minLevel, maxLevel]. When the band
        //                   is unset ([0,0]) returns 0 == "no level scaling" (legacy).
        //  * NextTypeId   — round-robin the recipe's enemyTypeIds across spawn points.
        //  * BuildTypedDef— a stat block for an arbitrary recipe enemy id. Known ids reuse
        //                   the family templates; unknown ids get a sane generic brute so a
        //                   new JSON enemy id never crashes the spawn (LogWarning only).
        //  * ApplyLevelScale — fold the rolled level into HP / damage / size. This is the
        //                   ONE place levelRange touches combat: ~+8% HP and ~+5% damage per
        //                   level over 1, on top of the existing threat scale. No new combat
        //                   code — EnemyFactory + Enemy consume the scaled EnemyDef as-is.
        // =====================================================================

        private int RollLevel()
        {
            int lo = Mathf.Max(0, minLevel);
            int hi = Mathf.Max(lo, maxLevel);
            if (hi <= 0) return 0;            // band unset => no level scaling (legacy)
            if (_levelRng == null) _levelRng = new System.Random();
            return _levelRng.Next(Mathf.Max(1, lo), hi + 1);
        }

        private string NextTypeId()
        {
            if (enemyTypeIds == null || enemyTypeIds.Length == 0) return "troll";
            string id = enemyTypeIds[_typeCursor % enemyTypeIds.Length];
            _typeCursor++;
            return string.IsNullOrEmpty(id) ? "troll" : id;
        }

        // Stat blocks (BuildTrollDef / BuildStonebellyDef / BuildTypedDef) and the
        // level-scale fold (ApplyLevelScale) now live in the SHARED, stateless
        // GarrisonStatBlocks so the config-driven RaidGarrisonSpawner builds defenders
        // from the EXACT same numbers — see GarrisonStatBlocks.cs.

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Cleared
                ? new Color(0.2f, 0.9f, 0.4f, 0.5f)
                : new Color(0.9f, 0.35f, 0.15f, 0.5f);
            if (spawnPoints != null)
                foreach (var sp in spawnPoints)
                    if (sp != null) Gizmos.DrawWireSphere(sp.position, 1.2f);
        }
#endif
    }
}
