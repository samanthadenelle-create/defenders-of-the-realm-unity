// =============================================================================
// ProjectilePool — WO-82. A persistent object pool for tower projectiles, so
// auto-firing towers never Instantiate/Destroy per shot (GC-free on mobile).
// -----------------------------------------------------------------------------
// Reconciled to this project: self-bootstraps (RuntimeInitializeOnLoadMethod) so
// no scene wiring is needed, and pools CODE-built projectiles (PooledProjectile
// builds its own visual) — there's no authored prefab, and code visuals are the
// project's reliable path. Expands on demand if drained.
//
// BUGFIX (WO-82): pooled projectiles are created UNDER this DontDestroyOnLoad pool
// so they survive scene loads. Previously they were loose root objects — a scene
// change destroyed them while the pool kept their (now dead) references, and the
// next GetProjectile dequeued a destroyed entry -> NullReferenceException on
// SetActive. GetProjectile also skips any null entries defensively.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;   // TGVRU: instrument the pool flow (§12)

namespace DeNelle.Village
{
    /// <summary>Persistent pool of reusable tower projectiles. Singleton, self-installed.</summary>
    public sealed class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        [SerializeField] private int _initialPoolSize = 40;

        private readonly Queue<PooledProjectile> _pool = new Queue<PooledProjectile>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("ProjectilePool").AddComponent<ProjectilePool>();   // Awake handles DDOL
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < _initialPoolSize; i++)
                _pool.Enqueue(CreateNew());
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // Parented to this (DontDestroyOnLoad) pool so pooled projectiles survive
        // scene loads instead of being destroyed under the unloading scene.
        private PooledProjectile CreateNew()
        {
            // G(uard): the GameObject build + AddComponent can throw (out-of-memory, a
            // PooledProjectile.Awake that throws). A null here would NRE the next GetProjectile,
            // so we Fail-loud + return null and let the caller decide — never a silent half-built body.
            PooledProjectile proj = null;
            FlowTrace.Try("ProjPool", "CreateNew projectile", () =>
            {
                var go = new GameObject("Projectile");
                go.transform.SetParent(transform, false);
                proj = go.AddComponent<PooledProjectile>();   // builds its own visual in Awake
                go.SetActive(false);
            });
            if (proj == null)
                FlowTrace.Fail("ProjPool", "CreateNew returned null — projectile body failed to build (see prior FAILED line).");
            return proj;
        }

        /// <summary>Lease a projectile (expands if drained, skips any destroyed entries).</summary>
        public PooledProjectile GetProjectile()
        {
            PooledProjectile proj = null;
            while (proj == null && _pool.Count > 0) proj = _pool.Dequeue();   // skip dead refs
            bool expanded = proj == null;
            if (proj == null) proj = CreateNew();

            // R(eturn-fallback never silent): an empty/expansion-failed pool that yields no body
            // would NRE on SetActive — Fail-loud and bail instead of crashing the caller.
            if (proj == null)
            {
                FlowTrace.Fail("ProjPool", "GetProjectile: no body available (pool drained AND CreateNew failed) — returning null.");
                return null;
            }

            proj.gameObject.SetActive(true);

            // V(erify the leased body actually reset + can render): a pooled body must carry a live
            // PooledProjectile and a renderable visual once active, else it flies invisible (the
            // invisible-projectile class). Throttled — towers lease many per second; the trend is enough.
            bool hasRenderer = proj.GetComponentInChildren<Renderer>(true) != null
                            || proj.GetComponentInChildren<ParticleSystem>(true) != null;
            FlowTrace.Throttle("ProjPool", "lease", 1f,
                $"GetProjectile leased (expanded={expanded}, poolLeft={_pool.Count}, hasRenderer={hasRenderer}).");
            if (!hasRenderer)
                FlowTrace.Once("ProjPool", "lease-no-renderer",
                    "GetProjectile: leased body has NO Renderer/ParticleSystem yet — visual is built per-shot in Initialize (expected pre-arm), flag if it stays invisible after firing.");
            return proj;
        }

        /// <summary>Return a spent projectile to the pool (deactivated + re-homed).</summary>
        public void ReturnToPool(PooledProjectile proj)
        {
            if (proj == null)
            {
                FlowTrace.Warn("ProjPool", "ReturnToPool: null projectile — ignored (a spent body went missing before return).");
                return;
            }
            proj.gameObject.SetActive(false);
            if (proj.transform.parent != transform) proj.transform.SetParent(transform, false);

            // V(erify teardown): a returned body MUST be deactivated + re-homed under the pool, or a
            // re-lease hands back a live/stray-parented projectile. Trace the teardown so a leak shows.
            if (proj.gameObject.activeSelf)
                FlowTrace.Warn("ProjPool", "ReturnToPool: body still active after SetActive(false) — would re-lease live.");

            _pool.Enqueue(proj);
            FlowTrace.Throttle("ProjPool", "return", 1f, $"ReturnToPool: body re-homed (poolSize={_pool.Count}).");
        }
    }
}
