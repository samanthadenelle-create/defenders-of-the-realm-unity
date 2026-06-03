// =============================================================================
// EconomyService — DEF-78: full multi-resource tracking.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Owns the four build-economy resources — Wood, Food, Iron, Crystals — and
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
//   stub (Wood 200, Iron 80) so existing scenes are unaffected.
//
// DEF-121 / WO-230 — the four harvestables are Wood / Food / Iron / Crystals.
//   The retired "Stone" axis is repurposed to FOOD. Like Crystals, Food is now
//   GameState-backed (GameState.Resources.Food) — the single wallet field the HUD,
//   BuildingUpgradePanel and harvest paths all share — so the build economy and the
//   harvest/upgrade economy never diverge. No "Magic" harvestable exists: Magic is
//   a building-UPGRADE tech axis only (see ResourceBuildingProgression).
//
// PERSISTENCE: Wood/Iron are in-session only (no PlayerPrefs) — they reset on scene
//   reload intentionally (a run is a single play session; WO-134 owns their
//   persistence). CRYSTALS and FOOD are the exception: each is a thin facade over
//   GameState.Resources (Crystals/Food) — the SINGLE source of truth the BuildMenu
//   and village HUD display and that wave/empower/harvest paths feed. So
//   EconomyService keeps no divergent in-session crystal/food pool; every
//   crystal/food read/spend/grant here round-trips through GameStateService and persists.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Snapshot of all four economy resources — passed with <see cref="EconomyService.OnChanged"/>.
    /// </summary>
    public readonly struct ResourceSnapshot
    {
        public readonly int Wood;
        public readonly int Food;
        public readonly int Iron;
        public readonly int Crystals;

        public ResourceSnapshot(int wood, int food, int iron, int crystals)
        {
            Wood     = wood;
            Food     = food;
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
        [Min(0)] public int Food;
        [Min(0)] public int Iron;
        [Min(0)] public int Crystals;

        public ResourceCost(int wood = 0, int food = 0, int iron = 0, int crystals = 0)
        {
            Wood     = wood;
            Food     = food;
            Iron     = iron;
            Crystals = crystals;
        }

        /// <summary>True when all four values are zero — a free action.</summary>
        public bool IsZero => Wood == 0 && Food == 0 && Iron == 0 && Crystals == 0;

        public static ResourceCost WoodOnly(int amount)     => new ResourceCost(wood:     amount);
        public static ResourceCost FoodOnly(int amount)     => new ResourceCost(food:     amount);
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

        [Header("Starting Resources (Wood/Iron — in-session)")]
        [SerializeField, Min(0)] private int _wood     = 200;
        [SerializeField, Min(0)] private int _iron     = 80;
        // NOTE (WO-131 / DEF-121): crystals AND food are NOT in-session fields here.
        // Both are backed by GameState.Resources — see the Crystals/Food properties.

        // ── Public read-only properties ───────────────────────────────────────

        public int Wood     => _wood;
        public int Iron     => _iron;

        /// <summary>
        /// DEF-121 — Food is GameState-backed (GameState.Resources.Food), the single
        /// wallet field the HUD / BuildingUpgradePanel / harvest paths share. Reads
        /// through GameStateService; returns 0 when state is absent.
        /// </summary>
        public int Food
        {
            get
            {
                var state = GameStateService.Instance?.State;
                return state != null ? state.Resources.Food : 0;
            }
        }

        /// <summary>
        /// WO-131 — crystals are the SINGLE source of truth on
        /// GameState.Resources.Crystals (the store the HUD/BuildMenu display), NOT a
        /// local pool. Reads through GameStateService; returns 0 when state is absent.
        /// </summary>
        public int Crystals
        {
            get
            {
                var state = GameStateService.Instance?.State;
                return state != null ? state.Resources.Crystals : 0;
            }
        }

        public ResourceSnapshot Snapshot => new ResourceSnapshot(_wood, Food, _iron, Crystals);

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
            return _wood  >= cost.Wood
                && Food   >= cost.Food            // DEF-121 — GameState-backed
                && _iron  >= cost.Iron
                && Crystals >= cost.Crystals;   // WO-131 — GameState-backed
        }

        /// <summary>
        /// Atomically spends <paramref name="cost"/> if affordable. Returns true on
        /// success, false (no mutation) when any resource is short. The crystal and
        /// food slots are deducted from GameState.Resources (persisted), the
        /// Wood/Iron slots from the in-session pool.
        /// </summary>
        public bool TrySpend(ResourceCost cost)
        {
            if (!CanAfford(cost)) return false;
            _wood  -= cost.Wood;
            _iron  -= cost.Iron;
            if (cost.Food > 0)
                GameStateService.Instance?.AddFood(-cost.Food);           // DEF-121 — GameState-backed spend
            if (cost.Crystals > 0)
                GameStateService.Instance?.AddCrystals(-cost.Crystals);   // GameState-backed spend
            NotifyChanged();
            return true;
        }

        /// <summary>Adds resources — for wave rewards, harvesting, etc. Negative values are clamped to 0.</summary>
        public void Grant(ResourceCost amount)
        {
            _wood  += Mathf.Max(0, amount.Wood);
            _iron  += Mathf.Max(0, amount.Iron);
            int food = Mathf.Max(0, amount.Food);
            if (food > 0)
                GameStateService.Instance?.AddFood(food);           // DEF-121 — GameState-backed grant
            int crystals = Mathf.Max(0, amount.Crystals);
            if (crystals > 0)
                GameStateService.Instance?.AddCrystals(crystals);   // WO-131 — GameState-backed grant
            NotifyChanged();
        }

        /// <summary>Convenience overload — specify only the resources you want to grant.</summary>
        public void Grant(int wood = 0, int food = 0, int iron = 0, int crystals = 0)
        {
            Grant(new ResourceCost(wood, food, iron, crystals));
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
