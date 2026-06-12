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
        [SerializeField, Min(0)] private int _wood     = 200; // real starter — wave rewards + builds read against this
        [SerializeField, Min(0)] private int _iron     = 80;  // real starter
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

        // ── Pet / Outpost territory (WO-106: pet resource farming + outpost system) ──
        // These let the economy be the single source for passive rates, secured count,
        // and a simple difficulty/scaling multiplier consumed by wave systems later.
        // Incremented by ClaimableCamp on successful defense secure. Pet harvest and
        // outpost trickle both route through Grant so in-session mirrors + listeners
        // (HUD) stay consistent with GameState.

        /// <summary>Number of outposts successfully claimed + defended (secured) this run/save.</summary>
        public int SecuredOutpostCount { get; private set; }

        /// <summary>
        /// Simple territory control multiplier for difficulty scaling and passive
        /// economy power. Starts at 1.0; each secured outpost adds a small bonus.
        /// Tune via code or later ProgressionConstants. Read-only for consumers
        /// (WaveManager, enemy spawners, offline accrual).
        /// </summary>
        public float TerritoryMultiplier => 1f + 0.05f * SecuredOutpostCount;

        /// <summary>
        /// Call when an outpost camp is successfully defended and secured.
        /// Increments the count (and thus the multiplier) and notifies listeners.
        /// Idempotent per camp via the caller (ClaimableCamp persists the secured flag).
        /// </summary>
        public void OnOutpostSecured()
        {
            SecuredOutpostCount = Mathf.Max(0, SecuredOutpostCount + 1);
            NotifyChanged();
        }

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

        private bool _bridgeAttached;

        private void OnEnable()  => AttachResourcesBridge();
        private void Start()     => AttachResourcesBridge();   // retry: GameStateService may not have existed at OnEnable

        /// <summary>
        /// HUD-refresh bridge: crystal/food gains from harvest/mine/empower/camp paths
        /// write GameState.Resources and raise GameStateService.ResourcesChanged (Core),
        /// NOT this service's OnChanged. The village HUD listens to OnChanged, so without
        /// this bridge it would not visually update on those GameState-backed gains.
        /// Re-emit OnChanged whenever the GameState resource wallet changes. Idempotent —
        /// guards against double-subscribe across OnEnable + Start.
        /// </summary>
        private void AttachResourcesBridge()
        {
            if (_bridgeAttached) return;
            var gs = GameStateService.Instance;
            if (gs == null) return;
            gs.ResourcesChanged.AddListener(OnGameStateResourcesChanged);
            _bridgeAttached = true;
        }

        private void OnDisable()
        {
            var gs = GameStateService.Instance;
            if (gs != null) gs.ResourcesChanged.RemoveListener(OnGameStateResourcesChanged);
            _bridgeAttached = false;
        }

        /// <summary>Re-emit <see cref="OnChanged"/> when the Core resource wallet changes
        /// (crystal/food gains route through GameStateService, not this service's methods).</summary>
        private void OnGameStateResourcesChanged() => NotifyChanged();

        private void OnDestroy()
        {
            var gs = GameStateService.Instance;
            if (gs != null) gs.ResourcesChanged.RemoveListener(OnGameStateResourcesChanged);
            _bridgeAttached = false;
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

        /// <summary>
        /// DEV grant that lands Wood/Iron in BOTH spendable wallets the game keeps.
        /// <para>
        /// Wood/Iron currently live in two stores that do not sync: this service's
        /// in-session pool (what ShopPanel + the HUD resource bar read) and
        /// GameState.Wood / GameState.Iron (what the building-upgrade flow's
        /// ResourceLedger reads + spends). A plain <see cref="Grant(int,int,int,int)"/>
        /// only fills the in-session pool, so dev-granted Wood/Iron was invisible to
        /// the upgrade flow; writing GameState directly only filled the other one, so
        /// it was invisible to the shop + HUD. This method writes BOTH so one dev
        /// action yields Wood/Iron the shop AND the structure-upgrade flow can spend,
        /// and the HUD reflects it. Food/Crystals are already GameState-backed, so the
        /// normal Grant path keeps them single-sourced.
        /// </para>
        /// </summary>
        public void GrantSpendable(int wood = 0, int food = 0, int iron = 0, int crystals = 0)
        {
            // Mirror Wood/Iron into the persisted GameState wallet the upgrade flow spends.
            var gs = GameStateService.Instance;
            if (gs != null && gs.State != null)
            {
                if (wood > 0) gs.State.Wood = Mathf.Max(0, gs.State.Wood + wood);
                if (iron > 0) gs.State.Iron = Mathf.Max(0, gs.State.Iron + iron);
            }

            // Grant fills the in-session Wood/Iron pool (shop + HUD bar) and routes
            // Food/Crystals through GameState (AddFood/AddCrystals -> Save + ResourcesChanged).
            Grant(new ResourceCost(wood, food, iron, crystals));

            // Persist + announce the GameState Wood/Iron mutation so the upgrade flow
            // sees it and any GameState-bound listeners refresh. (Grant already raised
            // OnChanged for the in-session pool; if Food/Crystals were > 0 it also fired
            // ResourcesChanged, but a Wood/Iron-only grant would not, so do it here.)
            if (gs != null && gs.State != null && (wood > 0 || iron > 0))
            {
                gs.Save();
                gs.ResourcesChanged.Invoke();
            }
        }

        // ── Unified resource income API (WO-106 continuation) ─────────────────
        // All new pet harvesting, outpost ticks, node claims, troop rewards, etc.
        // MUST go through here (or the Grant overloads) so in-session mirrors,
        // persisted GameState, and OnChanged listeners stay consistent.
        // This is the single source of truth for the entire resource economy.

        /// <summary>
        /// Preferred unified entry point for all harvesting / passive income.
        /// Maps the Core-canonical ResourceType (Iron/Wood/Food/AetherCrystal) to
        /// the correct bucket and calls Grant. Negative amounts are clamped.
        /// </summary>
        public void AddResource(DeNelle.Core.ResourceType type, int amount)
        {
            if (amount <= 0) return;
            switch (type)
            {
                case DeNelle.Core.ResourceType.Iron:
                    Grant(iron: amount);
                    break;
                case DeNelle.Core.ResourceType.Wood:
                    Grant(wood: amount);
                    break;
                case DeNelle.Core.ResourceType.Food:
                    Grant(food: amount);
                    break;
                case DeNelle.Core.ResourceType.AetherCrystal:
                    Grant(crystals: amount);
                    break;
            }
        }

        /// <summary>
        /// Overload accepting the local MineResource enum (used by MineNode/Outpost)
        /// so existing harvest code can migrate to the single AddResource style.
        /// </summary>
        public void AddResource(MineResource type, int amount)
        {
            if (amount <= 0) return;
            switch (type)
            {
                case MineResource.Iron:
                    Grant(iron: amount);
                    break;
                case MineResource.Wood:
                    Grant(wood: amount);
                    break;
                case MineResource.Food:
                    Grant(food: amount);
                    break;
                case MineResource.AetherCrystal:
                    Grant(crystals: amount);
                    break;
            }
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
