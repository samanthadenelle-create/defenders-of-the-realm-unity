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
            var go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);
            var proj = go.AddComponent<PooledProjectile>();   // builds its own visual in Awake
            go.SetActive(false);
            return proj;
        }

        /// <summary>Lease a projectile (expands if drained, skips any destroyed entries).</summary>
        public PooledProjectile GetProjectile()
        {
            PooledProjectile proj = null;
            while (proj == null && _pool.Count > 0) proj = _pool.Dequeue();   // skip dead refs
            if (proj == null) proj = CreateNew();
            proj.gameObject.SetActive(true);
            return proj;
        }

        /// <summary>Return a spent projectile to the pool (deactivated + re-homed).</summary>
        public void ReturnToPool(PooledProjectile proj)
        {
            if (proj == null) return;
            proj.gameObject.SetActive(false);
            if (proj.transform.parent != transform) proj.transform.SetParent(transform, false);
            _pool.Enqueue(proj);
        }
    }
}
