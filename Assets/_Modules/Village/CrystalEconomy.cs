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
// Persistence: stored in PersistedState.AetherCrystals (double? in SaveSchema
// v11). Reads and writes go through GameStateService so the value round-trips
// correctly. A null stored value reads as 0 (fresh-save default).
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

        /// <summary>The player's current Aether Crystal balance (never negative).</summary>
        public int CurrentCrystals
        {
            get
            {
                var state = GetState();
                return state == null ? 0 : state.AetherCrystals;
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

            int current = state.AetherCrystals;
            if (current < cost)
            {
                Debug.Log($"[CrystalEconomy] Insufficient Crystals — need {cost}, have {current}.");
                return false;
            }

            state.AetherCrystals = current - cost;
            svc.Save();
            Debug.Log($"[CrystalEconomy] Spent {cost} Crystals — balance now {state.AetherCrystals}.");
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

            state.AetherCrystals += amount;
            svc.Save();
            Debug.Log($"[CrystalEconomy] +{amount} Crystals awarded — balance now {state.AetherCrystals}.");
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
