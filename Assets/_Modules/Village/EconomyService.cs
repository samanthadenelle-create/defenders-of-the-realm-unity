// =============================================================================
// EconomyService — DEF-78: full multi-resource tracking.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Owns the four build-economy resources — Wood, Stone, Iron, Crystals — and
//   exposes a clean API for spending, earning, and checking affordability across
//   any combination of them. Previously a Wood-only stub; this pass completes
//   the full spec.
//
// API:
//   bool CanAfford(ResourceCost)        — multi-resource affordability check
//   bool TrySpend(ResourceCost)         — spend atomically; returns false + no-ops on failure
//   void Grant(ResourceCost)            — add resources (wave rewards, harvesting, etc.)
//   void Grant(int w, int s, int i, int c) — convenience overload
//   event Action<ResourceSnapshot> OnChanged — fires after every mutation
//
// BACKWARDS COMPAT:
//   The old CanAfford(int cost) and Spend(int cost) (Wood-only) remain as
//   deprecated aliases so TowerPlacementSystem / TowerUpgradeButton don't break.
//
// STARTING RESOURCES: editable in the Inspector. Defaults match the existing
//   stub (Wood 200, Stone 150, Iron 80, Crystals 50) so existing scenes are
//   unaffected.
//
// PERSISTENCE: in-session only (no PlayerPrefs). Resources reset on scene
//   reload intentionally — a run is a single play session. Cross-run persistence
//   is a later pass (same deferral as HeroProgression).
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Snapshot of all four economy resources — passed with <see cref="EconomyService.OnChanged"/>.
    /// </summary>
    public readonly struct ResourceSnapshot
    {
        public readonly int Wood;
        public readonly int Stone;
        public readonly int Iron;
        public readonly int Crystals;

        public ResourceSnapshot(int wood, int stone, int iron, int crystals)
        {
            Wood     = wood;
            Stone    = stone;
            Iron     = iron;
            Crystals = crystals;
        }
    }

    /// <summary>
    /// A multi-resource cost or reward. Zero means "no requirement" for that slot.
    /// </summary>
    [Serializable]
    public struct ResourceCost
    {
        [Min(0)] public int Wood;
        [Min(0)] public int Stone;
        [Min(0)] public int Iron;
        [Min(0)] public int Crystals;

        public ResourceCost(int wood = 0, int stone = 0, int iron = 0, int crystals = 0)
        {
            Wood     = wood;
            Stone    = stone;
            Iron     = iron;
            Crystals = crystals;
        }

        /// <summary>True when all four values are zero — a free action.</summary>
        public bool IsZero => Wood == 0 && Stone == 0 && Iron == 0 && Crystals == 0;

        public static ResourceCost WoodOnly(int amount)     => new ResourceCost(wood:     amount);
        public static ResourceCost StoneOnly(int amount)    => new ResourceCost(stone:    amount);
        public static ResourceCost IronOnly(int amount)     => new ResourceCost(iron:     amount);
        public static ResourceCost CrystalsOnly(int amount) => new ResourceCost(crystals: amount);
    }

    /// <summary>
    /// Singleton resource tracker for the build economy. Provides multi-resource
    /// affordability checks, atomic spending, and a change event for the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EconomyService : MonoBehaviour
    {
        public static EconomyService Instance { get; private set; }

        // ── Starting amounts (Inspector) ──────────────────────────────────────

        [Header("Starting Resources")]
        [SerializeField, Min(0)] private int _wood     = 200;
        [SerializeField, Min(0)] private int _stone    = 150;
        [SerializeField, Min(0)] private int _iron     = 80;
        [SerializeField, Min(0)] private int _crystals = 50;

        // ── Public read-only properties ───────────────────────────────────────

        public int Wood     => _wood;
        public int Stone    => _stone;
        public int Iron     => _iron;
        public int Crystals => _crystals;

        public ResourceSnapshot Snapshot => new ResourceSnapshot(_wood, _stone, _iron, _crystals);

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fires after any resource change with the new totals.</summary>
        public event Action<ResourceSnapshot> OnChanged;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[EconomyService]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<EconomyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Multi-resource API ────────────────────────────────────────────────

        /// <summary>Returns true when all four resource pools cover <paramref name="cost"/>.</summary>
        public bool CanAfford(ResourceCost cost)
        {
            return _wood     >= cost.Wood
                && _stone    >= cost.Stone
                && _iron     >= cost.Iron
                && _crystals >= cost.Crystals;
        }

        /// <summary>
        /// Atomically spends <paramref name="cost"/> if affordable. Returns true on
        /// success, false (no mutation) when any resource is short.
        /// </summary>
        public bool TrySpend(ResourceCost cost)
        {
            if (!CanAfford(cost)) return false;
            _wood     -= cost.Wood;
            _stone    -= cost.Stone;
            _iron     -= cost.Iron;
            _crystals -= cost.Crystals;
            NotifyChanged();
            return true;
        }

        /// <summary>Adds resources — for wave rewards, harvesting, etc. Negative values are clamped to 0.</summary>
        public void Grant(ResourceCost amount)
        {
            _wood     += Mathf.Max(0, amount.Wood);
            _stone    += Mathf.Max(0, amount.Stone);
            _iron     += Mathf.Max(0, amount.Iron);
            _crystals += Mathf.Max(0, amount.Crystals);
            NotifyChanged();
        }

        /// <summary>Convenience overload — specify only the resources you want to grant.</summary>
        public void Grant(int wood = 0, int stone = 0, int iron = 0, int crystals = 0)
        {
            Grant(new ResourceCost(wood, stone, iron, crystals));
        }

        // ── Backwards-compatible single-resource API (Wood only) ─────────────
        // These remain so TowerPlacementSystem / TowerUpgradeButton don't break
        // while they migrate to the ResourceCost overloads.

        /// <inheritdoc cref="CanAfford(ResourceCost)"/>
        [Obsolete("Use CanAfford(ResourceCost) for multi-resource checks.")]
        public bool CanAfford(int woodCost) => _wood >= woodCost;

        /// <summary>Spends Wood only. Prefer <see cref="TrySpend(ResourceCost)"/>.</summary>
        [Obsolete("Use TrySpend(ResourceCost) for multi-resource spending.")]
        public void Spend(int woodCost)
        {
            if (_wood < woodCost) return;
            _wood -= woodCost;
            NotifyChanged();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void NotifyChanged() => OnChanged?.Invoke(Snapshot);
    }
}
