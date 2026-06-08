// =============================================================================
// CrystalEconomy — lightweight singleton service for Aether Crystal balance.
// -----------------------------------------------------------------------------
// Aether Crystals are the rare off-chain currency used exclusively for tower
// empowerment. They are local-only — never connected to the SKR token, USDC,
// or the Solana wallet. No blockchain calls, no WalletService dependency.
//
// Sources (per tower-empowerment-spec.md §4):
//   • Crystal Mine (max level)  → +1 per completed wave (wire via WaveManager)
//   • Wave bonus chest           → +1–2 (rare, no-enemy-heart-reach bonus)
//   • Boss wave completion       → +3 (Necromancer wave-boss defeat)
//   • Dungeon completion         → +2–5 (per dungeon, once)
//
// Persistence: as of save v18 this class is a THIN FAÇADE over
// GameState.Resources.Crystals — the SINGLE source of truth the HUD, PackStore,
// BuildMenu and build/upgrade paths all read. The legacy PersistedState.AetherCrystals
// pool was folded into Resources.Crystals (SaveMigrator MigrateToV18). All reads/writes
// route through GameStateService.AddCrystals (clamps >= 0, persists, raises
// ResourcesChanged). The class + public API are kept because many callers reference it.
//
// Usage:
//   CrystalEconomy.Instance.CurrentCrystals   // read balance
//   CrystalEconomy.Instance.CanAfford(cost)   // afford check (no deduction)
//   CrystalEconomy.Instance.TrySpend(cost)    // deduct and save; false if short
//   CrystalEconomy.Instance.AddCrystals(n)    // award crystals and save
// =============================================================================

using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Singleton service managing the player's Aether Crystal balance.
    /// Add to a persistent GameObject in the Village scene (or a DontDestroyOnLoad
    /// manager object). Only one instance is active at any time — duplicates are
    /// destroyed in Awake.
    /// </summary>
    public sealed class CrystalEconomy : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static CrystalEconomy Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Balance ────────────────────────────────────────────────────────────

        /// <summary>
        /// The player's current Crystal balance (never negative). Thin façade over
        /// GameState.Resources.Crystals — the SINGLE source of truth the HUD, PackStore
        /// and build/upgrade paths all read. (The legacy AetherCrystals pool was folded
        /// into Resources.Crystals in save v18; this class is now just a convenience
        /// shim for the many DeNelle.Village callers.)
        /// </summary>
        public int CurrentCrystals
        {
            get
            {
                var state = GetState();
                return state == null ? 0 : state.Resources.Crystals;
            }
        }

        /// <summary>Returns true when the player can afford <paramref name="cost"/> Crystals.</summary>
        public bool CanAfford(int cost) => CurrentCrystals >= cost;

        // ── Spend / Award ──────────────────────────────────────────────────────

        /// <summary>
        /// Deducts <paramref name="cost"/> Crystals and saves. Returns false (no-op)
        /// if the player cannot afford it.
        /// </summary>
        public bool TrySpend(int cost)
        {
            if (cost <= 0) return true;   // free — always succeeds

            var svc   = GameStateService.Instance;
            var state = svc?.State;
            if (state == null)
            {
                Debug.LogWarning("[CrystalEconomy] GameStateService unavailable — spend rejected.");
                return false;
            }

            int current = state.Resources.Crystals;
            if (current < cost)
            {
                Debug.Log($"[CrystalEconomy] Insufficient Crystals — need {cost}, have {current}.");
                return false;
            }

            // AddCrystals clamps >= 0, persists, and raises ResourcesChanged.
            svc.AddCrystals(-cost);
            Debug.Log($"[CrystalEconomy] Spent {cost} Crystals — balance now {state.Resources.Crystals}.");
            return true;
        }

        /// <summary>
        /// Awards <paramref name="amount"/> Crystals to the player's balance and saves.
        /// Safe to call from any context (wave complete, boss defeat, dungeon reward).
        /// </summary>
        public void AddCrystals(int amount)
        {
            if (amount <= 0) return;

            var svc   = GameStateService.Instance;
            var state = svc?.State;
            if (state == null)
            {
                Debug.LogWarning("[CrystalEconomy] GameStateService unavailable — award dropped.");
                return;
            }

            // AddCrystals writes Resources.Crystals, clamps >= 0, persists, raises ResourcesChanged.
            svc.AddCrystals(amount);
            Debug.Log($"[CrystalEconomy] +{amount} Crystals awarded — balance now {state.Resources.Crystals}.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GameState GetState()
        {
            return GameStateService.Instance?.State;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Defenders/Debug/Award 10 Aether Crystals")]
        private static void EditorAward10()
        {
            if (Application.isPlaying && Instance != null)
                Instance.AddCrystals(10);
            else
                Debug.Log("[CrystalEconomy] Enter Play Mode first.");
        }
#endif
    }
}
