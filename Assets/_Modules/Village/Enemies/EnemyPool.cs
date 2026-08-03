// =============================================================================
// EnemyPool — a persistent object pool for wave/roam enemies, so the spawners no
// longer Instantiate a fresh skinned-mesh + NavMeshAgent + Animator + AddComponent
// stack per enemy per wave and Destroy it on death. Full GameObject churn on every
// spawn is the project's biggest GC-churn / stray-accumulation source (owner:
// "use pooling to control strays — we had leaks before"); on mobile / WebGL the
// repeated allocation is an OOM risk. Pooling reuses the body instead.
// -----------------------------------------------------------------------------
// Modelled EXACTLY on ProjectilePool / VfxPool (the proven project idiom):
//   • self-bootstraps (RuntimeInitializeOnLoadMethod) — no scene wiring,
//   • DontDestroyOnLoad so pooled bodies survive a Village↔OuterWorld transition,
//   • queue-backed, drain-and-expand, dead-ref skip on acquire (the scene-unload
//     NRE guard ProjectilePool documents).
//
// KEYED POOLS: enemy bodies are heterogeneous (a skeleton, an orc, a troll, a
// prefab-authored enemy all differ), so a single queue can't be reused blindly.
// The pool is keyed by a STRING — the EnemyDef model id for factory-built bodies,
// or the prefab name for prefab-built ones — and holds one queue per key. Get()
// only ever returns a body built for that exact key, so a reused skeleton is never
// handed out where an orc was asked for.
//
// THE RESET CONTRACT (Enemy.ResetForPool / Enemy.PrepareForReuse) is where pooling
// bugs hide — a missed reset spawns an enemy that is dead / untargetable / has
// stale HP / double-subscribed events. The exhaustive contract lives on Enemy.cs
// (it owns the private state); this pool only orchestrates release/acquire + the
// GameObject (de)activation and re-parent/warp.
//
// USAGE (the spawners):
//   Enemy e = EnemyPool.Get(key, prefabOrNull, def, pos, rot, parent);
//   ... e.Configure(...) as before ...
// and on death Enemy.Die calls EnemyPool.Release(this) instead of Destroy.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics; // TGVRU: root-cause FlowTrace on the pooled enemy-acquire path

namespace DeNelle.Village
{
    /// <summary>
    /// Persistent, self-installed pool of reusable <see cref="Enemy"/> bodies,
    /// keyed by model/prefab so each key reuses only bodies of its own kind.
    /// </summary>
    public sealed class EnemyPool : MonoBehaviour
    {
        public static EnemyPool Instance { get; private set; }

        // One queue of dormant bodies per key (EnemyDef model id / prefab name).
        private readonly Dictionary<string, Queue<Enemy>> _pools =
            new Dictionary<string, Queue<Enemy>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("EnemyPool").AddComponent<EnemyPool>();   // Awake handles DDOL
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // =====================================================================
        //  Acquire
        // =====================================================================

        /// <summary>
        /// Leases an enemy body for <paramref name="key"/> at the given pose, reusing
        /// a dormant pooled body if one exists or building a fresh one otherwise.
        /// The CALLER still calls <see cref="Enemy.Configure"/> (+ wave-scaling +
        /// event hooks) exactly as before — Get only provides a clean, re-armed body.
        ///
        /// Build source: if <paramref name="prefab"/> is non-null a fresh body is
        /// <c>Instantiate</c>d from it (matching the spawner's prefab path); otherwise
        /// it is built via <see cref="EnemyFactory.Build"/> from <paramref name="def"/>
        /// (the skinned path). A reused body skips both — it is just re-placed,
        /// re-enabled and reset.
        /// </summary>
        /// <param name="key">Pool key — the EnemyDef model id or the prefab name.</param>
        /// <param name="prefab">Prefab to Instantiate when building fresh (may be null → factory).</param>
        /// <param name="def">EnemyDef used by the factory build path / the reset.</param>
        /// <param name="pos">Spawn position (already NavMesh-snapped by the caller).</param>
        /// <param name="rot">Spawn rotation.</param>
        /// <param name="parent">Parent transform for the body (may be null).</param>
        public static Enemy Get(string key, Enemy prefab, EnemyDef def,
                                Vector3 pos, Quaternion rot, Transform parent)
        {
            if (Instance == null) Bootstrap();
            return Instance.GetInternal(key, prefab, def, pos, rot, parent);
        }

        private Enemy GetInternal(string key, Enemy prefab, EnemyDef def,
                                  Vector3 pos, Quaternion rot, Transform parent)
        {
            string poolKey = string.IsNullOrEmpty(key) ? "_default" : key;
            using var _ = FlowTrace.Enter("EnemyPool",
                $"Get key='{poolKey}' src={(prefab != null ? "prefab" : "factory")} pos={pos}");

            Enemy enemy = null;
            int queued = 0;
            if (_pools.TryGetValue(poolKey, out var queue))
            {
                queued = queue.Count;
                // Skip any destroyed entries (scene-unload NRE guard, per ProjectilePool).
                while (enemy == null && queue.Count > 0) enemy = queue.Dequeue();
            }

            if (enemy == null)
            {
                FlowTrace.Step("EnemyPool",
                    $"Get key='{poolKey}': pool empty (had {queued} stale) — building fresh body.");

                // Pool drained / first use for this key — build a fresh body. Stamp
                // the key so Release routes it back to the same queue on death.
                enemy = prefab != null
                    ? Instantiate(prefab, pos, rot, parent)
                    : EnemyFactory.Build(def, pos, rot, parent);
                if (enemy == null)
                {
                    // SILENT NO-SPAWN GUARD (TGVRU, owner 2026-06-19): a null build here used to
                    // return null with NO trace, so a wave/roam/garrison spawn just... didn't, and
                    // the break-log was blind to it. Fail-loud so a capture pinpoints THIS as the
                    // dead step (missing prefab / factory build threw) instead of a guessed-at
                    // "enemies aren't spawning".
                    FlowTrace.Fail("EnemyPool",
                        $"Get key='{poolKey}' FAILED: {(prefab != null ? "Instantiate(prefab)" : "EnemyFactory.Build")} " +
                        $"returned null (def='{(def != null ? def.Id : "null")}') — NO enemy spawned (silent no-spawn).");
                    return null;
                }

                // Some prefab/placeholder bodies don't carry the damageable adapter;
                // the factory always does. Guarantee it so the hero can hit a reused
                // body (the spawners also belt-and-brace this, but keep it here).
                if (enemy.GetComponent<EnemyDamageable>() == null)
                    enemy.gameObject.AddComponent<EnemyDamageable>();

                enemy.SetPoolKey(poolKey);
                FlowTrace.Step("EnemyPool",
                    $"Get key='{poolKey}': built fresh body '{enemy.name}' (active={enemy.gameObject.activeSelf}).");
                return enemy;
            }

            FlowTrace.Step("EnemyPool",
                $"Get key='{poolKey}': reusing pooled body '{enemy.name}' ({queued} were queued).");

            // Reused body — re-home, re-place and re-arm it for a fresh spawn.
            Transform t = enemy.transform;
            if (parent != null && t.parent != parent) t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, rot);
            enemy.gameObject.SetActive(true);

            // The reset contract (HP / dead flag / animator / agent warp / events /
            // registry / AI state / ledger) lives on Enemy — it owns the private state.
            enemy.PrepareForReuse(pos, rot);

            // RESET-VERIFY (TGVRU): a pooled body that hands back inactive or still flagged
            // dead is the classic pooling bug (untargetable / invisible spawn). PrepareForReuse
            // owns the reset; PROVE it took here so a capture self-reports a broken reuse instead
            // of an enemy that silently never appears. Warn (not Fail): the body still exists, so
            // this is the diagnosable signal, not a hard stop.
            if (!enemy.gameObject.activeSelf || enemy.IsDead)
            {
                FlowTrace.Warn("EnemyPool",
                    $"Get key='{poolKey}': reused body '{enemy.name}' did NOT reset clean — " +
                    $"active={enemy.gameObject.activeSelf} IsDead={enemy.IsDead} (expected active=true, IsDead=false). " +
                    "PrepareForReuse left it half-reset — would spawn an inactive/dead enemy.");
            }
            return enemy;
        }

        // =====================================================================
        //  Release
        // =====================================================================

        /// <summary>
        /// Returns a dead/consumed enemy body to its key's pool: the body has already
        /// run <see cref="Enemy.ResetForPool"/> (unsubscribe / unregister / forget /
        /// stop coroutines / disable). If the pool singleton is gone (shutdown) the
        /// body is simply destroyed so nothing leaks.
        /// </summary>
        public static void Release(Enemy enemy)
        {
            if (enemy == null) return;
            if (Instance == null) { Destroy(enemy.gameObject); return; }
            Instance.ReleaseInternal(enemy);
        }

        private void ReleaseInternal(Enemy enemy)
        {
            string poolKey = enemy.PoolKey;

            // P1-7 (2026-08-02) — THE POOL USED TO LEAK EVERY BODY IT DID NOT CREATE.
            // SetPoolKey is stamped in exactly ONE place: the fresh-build branch of GetInternal.
            // But Enemy.Die calls EnemyPool.Release for EVERY enemy, and roughly a dozen systems
            // build bodies straight through EnemyFactory.Build without ever going near the pool
            // (RaidGarrisonSpawner, EnemyOutpost, GarrisonController, CampGuards, CampDefenseWave,
            // OutpostEnemyGroupSpawner, OverworldEncounterSpawner, BattleArena, RegionMobSpawner,
            // TribeManager, WardTetherService, TutorialFlow, the family test spawners). Those all
            // arrived here with PoolKey == null and were filed under "_default" — and NO caller
            // anywhere asks Get() for "_default" (every call site passes "model:"/"prefab:" + a
            // name). So that queue was WRITE-ONLY: every dungeon / outpost / overworld / arena /
            // raid kill parked a full skinned-mesh + Animator + NavMeshAgent under DontDestroyOnLoad
            // forever. Unbounded growth across a session — the exact OOM path on mobile that this
            // pool exists to prevent.
            //
            // DECISION: an unkeyed body is DESTROYED, not queued.
            //   * It is CORRECT-by-construction: the pool's contract is "Get(key) only ever returns
            //     a body built for that exact key". A body with no key can never satisfy any Get,
            //     so retaining it has zero upside and unbounded cost.
            //   * It LOSES NOTHING: those bodies were never re-served before today either. Reuse
            //     goes from zero to zero; only the leak goes away. Destroy is also exactly what
            //     these bodies did pre-pooling (Destroy after the death hold), and it is already
            //     this class's own shutdown fallback (Release when Instance == null).
            //   * REJECTED alternative — stamping factory-built bodies with a synthetic
            //     "model:<id>" key so they join the wave queues. That would let a raid-garrison /
            //     arena / dungeon body, carrying that lane's spawner-specific setup, be handed to a
            //     village wave. The P0-2 brain reset now scrubs the brain, but the other lanes also
            //     attach their own components/anchors/hierarchy that no reset contract covers, so
            //     cross-lane reuse is a NEW bug class traded for a fixed one. If cross-lane reuse
            //     is ever wanted it needs those spawners to route through EnemyPool.Get (which
            //     stamps the key properly) — not a key invented at release time.
            if (string.IsNullOrEmpty(poolKey))
            {
                FlowTrace.Throttle("EnemyPool", "release-unkeyed", 5f,
                    $"Return body='{enemy.name}': NO pool key (built outside EnemyPool.Get, e.g. direct " +
                    "EnemyFactory.Build) - DESTROYED rather than queued. No spawner ever requests the " +
                    "keyless queue, so queueing it would leak the body under DontDestroyOnLoad forever. " +
                    "Route this spawner through EnemyPool.Get if its bodies should be reused.");
                Destroy(enemy.gameObject);
                return;
            }

            using var _ = FlowTrace.Enter("EnemyPool", $"Return key='{poolKey}' body='{enemy.name}'");

            // Enemy.Die already invoked ResetForPool (events/registry/ledger/coroutines
            // dropped) before calling us. Deactivate + re-home under this DDOL root so
            // the dormant body survives scene loads and can't tick / be hit / be found.
            enemy.gameObject.SetActive(false);
            if (enemy.transform.parent != transform)
                enemy.transform.SetParent(transform, false);

            if (!_pools.TryGetValue(poolKey, out var queue))
            {
                queue = new Queue<Enemy>();
                _pools[poolKey] = queue;
            }
            // Guard against a double-release leaving the same body twice in the queue.
            if (!queue.Contains(enemy))
            {
                queue.Enqueue(enemy);
                FlowTrace.Step("EnemyPool", $"Return key='{poolKey}': body returned to pool (now {queue.Count} dormant).");
            }
            else
            {
                FlowTrace.Warn("EnemyPool",
                    $"Return key='{poolKey}': body '{enemy.name}' already in the pool — double-release ignored ({queue.Count} dormant).");
            }
        }
    }
}
