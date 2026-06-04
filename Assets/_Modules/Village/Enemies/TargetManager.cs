// =============================================================================
// TargetManager — a persistent registry of every live Enemy, so targeting is a
// cheap list walk instead of a physics OverlapSphere (owner-proposed, 2026-06-02).
// -----------------------------------------------------------------------------
// WHY (validated against this codebase, not adopted verbatim):
//   The reticle's old Physics.OverlapSphere into a 64-slot buffer OVERFLOWED in
//   the dense village (~2,900 colliders) and crowded enemies out — the "no lock"
//   bug. It was also range-capped because a wider sweep made the overflow worse.
//   A registry sidesteps ALL of that: it holds ONLY enemies, costs nothing to
//   widen the range on, never depends on a collider/layer being right, and (being
//   DontDestroyOnLoad) survives a Village↔OuterWorld additive transition. The
//   hero reticle, towers, and pets can all share this ONE source of truth.
//
// REGISTRATION: every Enemy auto-registers in OnEnable and removes itself in
// OnDisable / on death (Enemy.cs) — so EVERY spawner (waves, RegionMobSpawner,
// tribes, wards, family-test) is covered with no per-spawner wiring (CLAUDE.md §9,
// one path). Self-bootstrapping DDOL singleton; Register() lazily creates it so an
// enemy that wakes before any scene system is still recorded.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Persistent registry of live <see cref="Enemy"/> instances for cheap,
    /// overflow-proof targeting queries shared by the hero reticle / towers / pets.</summary>
    public sealed class TargetManager : MonoBehaviour
    {
        public static TargetManager Instance { get; private set; }

        private readonly List<Enemy> _enemies = new List<Enemy>(128);

        /// <summary>Live enemy count (diagnostic / HUD).</summary>
        public int Count => _enemies.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("TargetManager").AddComponent<TargetManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Registration (called by Enemy lifecycle) ─────────────────────────

        /// <summary>Records an enemy (idempotent). Lazily spins up the singleton so an
        /// enemy that enables before any scene wiring is still tracked.</summary>
        public static void Register(Enemy e)
        {
            if (e == null) return;
            if (Instance == null) Bootstrap();
            var list = Instance._enemies;
            if (!list.Contains(e)) list.Add(e);
        }

        /// <summary>Drops an enemy from the registry (on disable / death).</summary>
        public static void Unregister(Enemy e)
        {
            if (e == null || Instance == null) return;
            Instance._enemies.Remove(e);
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>The nearest living enemy within <paramref name="maxRange"/> of
        /// <paramref name="fromPosition"/>, or null. Compacts dead/destroyed entries
        /// as it walks (so the list self-cleans even without an explicit unregister).</summary>
        public Enemy GetClosestTarget(Vector3 fromPosition, float maxRange)
        {
            Enemy closest = null;
            float bestSqr = maxRange * maxRange;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _enemies[i];
                if (e == null || !e.IsAlive) { _enemies.RemoveAt(i); continue; }
                float sqr = (fromPosition - e.transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; closest = e; }
            }
            return closest;
        }

        /// <summary>Fills <paramref name="outList"/> with the living enemies within
        /// <paramref name="maxRange"/>, sorted nearest-first. Self-cleans dead entries.</summary>
        public void CollectInRange(Vector3 fromPosition, float maxRange, List<Enemy> outList)
        {
            outList.Clear();
            float maxSqr = maxRange * maxRange;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _enemies[i];
                if (e == null || !e.IsAlive) { _enemies.RemoveAt(i); continue; }
                if ((fromPosition - e.transform.position).sqrMagnitude <= maxSqr) outList.Add(e);
            }
            outList.Sort((a, b) =>
                (fromPosition - a.transform.position).sqrMagnitude.CompareTo(
                (fromPosition - b.transform.position).sqrMagnitude));
        }
    }
}
