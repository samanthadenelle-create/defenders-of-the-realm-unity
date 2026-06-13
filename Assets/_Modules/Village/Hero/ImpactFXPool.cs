// =============================================================================
// ImpactFXPool — pooling for transient IMPACT VFX GameObjects.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS:
//   Impact bursts were spawned PER SHOT and torn down per shot:
//     • ProjectileVFXCatalog.SpawnImpact — Object.Instantiate + Object.Destroy
//     • ProjectileMover.Arrive (prefab ImpactFX path) — Instantiate + Destroy
//   At hero/companion fire cadence that is per-shot GC + transient GameObjects.
//   This pools the burst bodies (keyed by source prefab) and re-homes them after
//   their particle lifetime instead of destroying them — GC-free reuse.
//
// MODELLED ON ProjectilePool (WO-82): self-bootstrap RuntimeInitializeOnLoadMethod,
// DontDestroyOnLoad, per-key queue, dead-ref skip, expands on demand.
//
// RESET CONTRACT:
//   • Acquire: re-home at the impact pose, re-enable, Clear()+Play() every child
//     ParticleSystem so the burst restarts clean (no leftover particles).
//   • Reclaim (after lifetime): stop emission, disable, re-parent under the pool.
//   A leased burst is purely visual + self-terminating, so no callbacks to unhook.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Persistent pool of reusable impact-burst FX bodies, keyed by source prefab.
    /// Singleton, self-installed, survives scene loads.</summary>
    public sealed class ImpactFXPool : MonoBehaviour
    {
        public static ImpactFXPool Instance { get; private set; }

        // One queue per source prefab — a Fire burst is never handed back as Ice.
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools
            = new Dictionary<GameObject, Queue<GameObject>>();

        // Reverse map (instance → its source prefab) so a reclaimed body re-queues
        // under the right key without the caller re-supplying it.
        private readonly Dictionary<GameObject, GameObject> _sourceOf
            = new Dictionary<GameObject, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("ImpactFXPool").AddComponent<ImpactFXPool>();   // Awake handles DDOL
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Fire an impact burst from <paramref name="prefab"/> at the given pose.
        /// Leases a pooled body (expands if drained), replays its particles, and reclaims it
        /// after <paramref name="lifetime"/> seconds. Pass a negative lifetime to derive it
        /// from the prefab's particle lifetimes. <paramref name="prepared"/> = false runs the
        /// catalog clean/URP fixup once when the body is first built (for ProjectileVFXCatalog
        /// impact prefabs); true leaves a self-contained prefab untouched (for a user-assigned
        /// ProjectileMover.ImpactFX).</summary>
        public void Play(GameObject prefab, Vector3 position, Quaternion rotation,
                         float lifetime = -1f, bool prepared = true)
        {
            if (prefab == null) return;

            var body = Acquire(prefab, prepared);
            if (body == null) return;

            var t = body.transform;
            t.SetParent(null, false);
            t.position = position;
            t.rotation = rotation;
            body.SetActive(true);

            ProjectileVFXCatalog.ReplayPooled(body);

            float life = lifetime > 0f ? lifetime : ProjectileVFXCatalog.PooledLifetime(body);
            StartCoroutine(ReclaimAfter(body, prefab, life));
        }

        private GameObject Acquire(GameObject prefab, bool prepared)
        {
            if (!_pools.TryGetValue(prefab, out var q))
            {
                q = new Queue<GameObject>();
                _pools[prefab] = q;
            }

            GameObject body = null;
            while (body == null && q.Count > 0) body = q.Dequeue();   // skip dead refs
            if (body == null) body = CreateNew(prefab, prepared);
            return body;
        }

        // Built under this (DontDestroyOnLoad) pool so bodies survive scene loads.
        private GameObject CreateNew(GameObject prefab, bool prepared)
        {
            var go = Object.Instantiate(prefab, transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Catalog impact prefabs ship demo physics/scripts + built-in particle shaders;
            // strip + URP-remap once. A self-contained ProjectileMover.ImpactFX is left as-is.
            if (!prepared) ProjectileVFXCatalog.PreparePooledInstance(go);

            _sourceOf[go] = prefab;
            go.SetActive(false);
            return go;
        }

        private IEnumerator ReclaimAfter(GameObject body, GameObject prefab, float life)
        {
            if (life > 0f) yield return new WaitForSeconds(life);
            if (body == null) yield break;   // destroyed under us (scene unload) — drop it

            // Stop emission so a reused body doesn't carry stragglers.
            foreach (var ps in body.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            body.SetActive(false);
            var t = body.transform;
            if (t.parent != transform) t.SetParent(transform, false);

            if (!_pools.TryGetValue(prefab, out var q))
            {
                q = new Queue<GameObject>();
                _pools[prefab] = q;
            }
            q.Enqueue(body);
        }
    }
}
