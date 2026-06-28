// =============================================================================
// GlimmerCurrencyService - the persistent wallet + ownership store for the
// Cosmetic Shop. Ports docs/cosmetic-shop-spec.md Section 6 (state + storage):
// Glimmer is a soft secondary currency, ownedCosmetics is the set of unlocked
// item ids, equippedCosmetics is one id per slot (here keyed by category).
// -----------------------------------------------------------------------------
// Bootstrapped before scene load - same pattern as DailyQuestService. The
// instance lives on a DontDestroyOnLoad GameObject so the wallet survives
// scene transitions. Save state is held in PlayerPrefs under
// "dotr-cosmetics-v1" as a JSON blob (Newtonsoft); the v1 suffix lets us bump
// the schema later without trampling a player's purchases.
//
// The service exposes:
//   - int Glimmer                read-only balance (use TryAddGlimmer to grant)
//   - bool TryPurchase(id)       spend Glimmer to unlock a cosmetic
//   - void Equip(id)             equip an OWNED cosmetic into its category slot
//   - string EquippedFor(cat)    currently-equipped id for a category (or null)
//   - bool Owns(id)              ownership query
//   - bool TryAddGlimmer(n)      grant Glimmer (called by future earn-points)
//   - bool GrantAchievement(id)  unlock an achievement-only cosmetic
//   - event Changed              any wallet / ownership / equip mutation
//
// IMPORTANT: per the port rules, this service must NOT touch GameState.cs.
// Crystal and Glimmer remain separate; the spec is explicit that
// "Crystals to Glimmer is not allowed" (Section 2.3).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Quests;

namespace DeNelle.Cosmetics
{
    /// <summary>
    /// Persisted shape of the cosmetic wallet. Stored as JSON in PlayerPrefs.
    /// </summary>
    [Serializable]
    public sealed class GlimmerSaveData
    {
        [JsonProperty("glimmer")] public int Glimmer;
        [JsonProperty("ownedCosmetics")] public List<string> OwnedCosmetics = new List<string>();

        /// <summary>category -> equipped cosmetic id.</summary>
        [JsonProperty("equippedByCategory")] public Dictionary<string, string> EquippedByCategory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Singleton MonoBehaviour - wallet, ownership, equip state. Persists via
    /// PlayerPrefs. Bootstrapped before scene load so the HUD can subscribe to
    /// Changed as soon as the shop panel comes up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlimmerCurrencyService : MonoBehaviour
    {
        public const string PrefKey = "dotr-cosmetics-v1";
        public const int StartingGlimmer = 25; // seeds the wallet so the shop has something to spend on day one.

        public static GlimmerCurrencyService Instance { get; private set; }

        /// <summary>Fires after any mutation - balance, ownership, or equip.</summary>
        public event Action Changed;

        private GlimmerSaveData _state;
        private readonly HashSet<string> _ownedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Current Glimmer balance (read-only - mutate via TryPurchase / TryAddGlimmer).</summary>
        public int Glimmer
        {
            get { EnsureState(); return _state.Glimmer; }
        }

        /// <summary>Snapshot of currently-owned cosmetic ids.</summary>
        public IReadOnlyCollection<string> OwnedCosmetics
        {
            get { EnsureState(); return _ownedSet; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GlimmerCurrencyService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<GlimmerCurrencyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureState();
        }

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>True if the player currently owns the given cosmetic id.</summary>
        public bool Owns(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureState();
            return _ownedSet.Contains(id);
        }

        /// <summary>The equipped cosmetic id for the given category, or null.</summary>
        public string EquippedFor(string category)
        {
            if (string.IsNullOrEmpty(category)) return null;
            EnsureState();
            return _state.EquippedByCategory.TryGetValue(category, out var id) ? id : null;
        }

        /// <summary>
        /// Spends Glimmer to unlock a cosmetic. Returns true on success.
        /// Refuses already-owned items, missing ids, achievement-gated items,
        /// and insufficient balance. Emits Changed on success.
        /// </summary>
        public bool TryPurchase(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var def = CosmeticCatalog.Find(id);
            if (def == null) return false;

            EnsureState();
            if (_ownedSet.Contains(id)) return false;
            if (def.IsAchievement) return false;
            if (def.GlimmerCost <= 0) return false;
            if (_state.Glimmer < def.GlimmerCost) return false;

            _state.Glimmer -= def.GlimmerCost;
            _ownedSet.Add(id);
            _state.OwnedCosmetics.Add(id);
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Equips an OWNED cosmetic into its category slot. Equipping a not-
        /// owned id is a no-op. Pass null or an empty string to clear the slot.
        /// Emits Changed on a change.
        /// </summary>
        public void Equip(string id)
        {
            EnsureState();

            // Clear request.
            if (string.IsNullOrEmpty(id))
            {
                // We do not know which slot to clear without an id - callers
                // wanting to clear should use UnequipCategory.
                return;
            }

            var def = CosmeticCatalog.Find(id);
            if (def == null) return;
            if (!_ownedSet.Contains(id)) return;

            var category = def.Category ?? string.Empty;
            if (_state.EquippedByCategory.TryGetValue(category, out var current) && current == id)
                return; // already equipped - no churn

            _state.EquippedByCategory[category] = id;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Clears the equipped item in the given category, if any.</summary>
        public void UnequipCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return;
            EnsureState();
            if (!_state.EquippedByCategory.Remove(category)) return;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Grants Glimmer (wave milestones, daily quests, IAP). Returns true on a non-zero grant.</summary>
        public bool TryAddGlimmer(int amount)
        {
            if (amount <= 0) return false;
            EnsureState();
            _state.Glimmer += amount;
            Save();
            Changed?.Invoke();
            // WO-558: wildcard daily-quest progress — "earn N glimmer" advances by the granted amount.
            DailyQuestService.Instance?.Report("wildcard.earn-glimmer", amount);
            return true;
        }

        /// <summary>
        /// Deducts <paramref name="amount"/> Glimmer from the balance.
        /// Returns true when the balance was sufficient and the spend succeeded.
        /// Returns false without mutating state when the balance is insufficient
        /// or <paramref name="amount"/> is non-positive.
        /// </summary>
        public bool SpendGlimmer(int amount)
        {
            if (amount <= 0) return false;
            EnsureState();
            if (_state.Glimmer < amount) return false;
            _state.Glimmer -= amount;
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Grants an achievement-gated cosmetic outside the Glimmer path. Used
        /// by milestone code once the free-path triggers ship (Section 9 of the
        /// spec). Returns true if the cosmetic was newly granted.
        /// </summary>
        public bool GrantAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var def = CosmeticCatalog.Find(id);
            if (def == null) return false;
            EnsureState();
            if (_ownedSet.Contains(id)) return false;
            _ownedSet.Add(id);
            _state.OwnedCosmetics.Add(id);
            Save();
            Changed?.Invoke();
            return true;
        }

        // ─── Internals ──────────────────────────────────────────────────────

        private void EnsureState()
        {
            if (_state != null) return;
            if (TryLoad(out var loaded) && loaded != null)
            {
                _state = loaded;
            }
            else
            {
                _state = new GlimmerSaveData { Glimmer = StartingGlimmer };
                Save();
            }
            // Hydrate the lookup set from the persisted list.
            _ownedSet.Clear();
            if (_state.OwnedCosmetics != null)
                foreach (var id in _state.OwnedCosmetics)
                    if (!string.IsNullOrEmpty(id)) _ownedSet.Add(id);
            // Guard against null dict from older saves.
            if (_state.EquippedByCategory == null)
                _state.EquippedByCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private bool TryLoad(out GlimmerSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            try
            {
                data = JsonConvert.DeserializeObject<GlimmerSaveData>(PlayerPrefs.GetString(PrefKey));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GlimmerCurrencyService] load failed: " + ex.Message);
                data = null;
            }
            return data != null;
        }

        private void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefKey, JsonConvert.SerializeObject(_state));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GlimmerCurrencyService] save failed: " + ex.Message);
            }
        }
    }
}
