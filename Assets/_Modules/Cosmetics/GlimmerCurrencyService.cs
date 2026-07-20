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
using DeNelle.Core.Diagnostics;

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
            FlowTrace.Step("Glimmer", $"TryPurchase id='{id ?? "<null>"}'");
            if (string.IsNullOrEmpty(id)) { FlowTrace.Warn("Glimmer", "TryPurchase rejected: null/empty id"); return false; }
            var def = CosmeticCatalog.Find(id);
            if (def == null) { FlowTrace.Warn("Glimmer", $"TryPurchase rejected: '{id}' not in CosmeticCatalog (cosmetics.json)"); return false; }

            EnsureState();
            if (_ownedSet.Contains(id)) { FlowTrace.Warn("Glimmer", $"TryPurchase rejected: '{id}' already owned"); return false; }
            if (def.IsAchievement) { FlowTrace.Warn("Glimmer", $"TryPurchase rejected: '{id}' is achievement-gated (not buyable)"); return false; }
            if (def.GlimmerCost <= 0) { FlowTrace.Warn("Glimmer", $"TryPurchase rejected: '{id}' has cost<=0 ({def.GlimmerCost})"); return false; }
            if (_state.Glimmer < def.GlimmerCost) { FlowTrace.Warn("Glimmer", $"TryPurchase rejected: insufficient balance for '{id}' (have {_state.Glimmer}, need {def.GlimmerCost})"); return false; }

            // Debit-and-grant — the highest-risk economy op. A debit that is not matched by a
            // grant means the player paid and got nothing. Prove the invariant from the trace.
            int before = _state.Glimmer;
            _state.Glimmer -= def.GlimmerCost;
            FlowTrace.Step("Glimmer", $"DEBIT {def.GlimmerCost} for '{id}' (balance {before} -> {_state.Glimmer})");
            bool granted = _ownedSet.Add(id);
            if (!granted)
                FlowTrace.Fail("Glimmer", $"DEBIT-WITHOUT-GRANT: spent {def.GlimmerCost} on '{id}' but ownership set already contained it — player paid, got nothing");
            _state.OwnedCosmetics.Add(id);
            Save();
            Changed?.Invoke();
            FlowTrace.Step("Glimmer", $"purchase COMMITTED '{id}' owned={_ownedSet.Count} balance={_state.Glimmer}");
            return true;
        }

        /// <summary>
        /// Equips an OWNED cosmetic into its category slot. Equipping a not-
        /// owned id is a no-op. Pass null or an empty string to clear the slot.
        /// Emits Changed on a change.
        /// </summary>
        public void Equip(string id)
        {
            FlowTrace.Step("Glimmer", $"Equip id='{id ?? "<null>"}'");
            EnsureState();

            // Clear request.
            if (string.IsNullOrEmpty(id))
            {
                // We do not know which slot to clear without an id - callers
                // wanting to clear should use UnequipCategory.
                FlowTrace.Warn("Glimmer", "Equip no-op: null/empty id (use UnequipCategory to clear a slot)");
                return;
            }

            var def = CosmeticCatalog.Find(id);
            if (def == null) { FlowTrace.Warn("Glimmer", $"Equip no-op: '{id}' not in CosmeticCatalog"); return; }
            if (!_ownedSet.Contains(id)) { FlowTrace.Warn("Glimmer", $"Equip no-op: '{id}' not owned"); return; }

            var category = def.Category ?? string.Empty;
            if (_state.EquippedByCategory.TryGetValue(category, out var current) && current == id)
                return; // already equipped - no churn

            _state.EquippedByCategory[category] = id;
            FlowTrace.Step("Glimmer", $"equipped '{id}' into category '{category}'");
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
            FlowTrace.Step("Glimmer", $"TryAddGlimmer amount={amount}");
            if (amount <= 0) { FlowTrace.Warn("Glimmer", $"TryAddGlimmer rejected: non-positive amount ({amount})"); return false; }
            EnsureState();
            int before = _state.Glimmer;
            _state.Glimmer += amount;
            FlowTrace.Step("Glimmer", $"GRANT {amount} (balance {before} -> {_state.Glimmer}) — CryptoPaymentManager/quest/IAP landing point");
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
            FlowTrace.Step("Glimmer", $"SpendGlimmer amount={amount}");
            if (amount <= 0) { FlowTrace.Warn("Glimmer", $"SpendGlimmer rejected: non-positive amount ({amount})"); return false; }
            EnsureState();
            if (_state.Glimmer < amount) { FlowTrace.Warn("Glimmer", $"SpendGlimmer rejected: insufficient balance (have {_state.Glimmer}, need {amount})"); return false; }
            int before = _state.Glimmer;
            _state.Glimmer -= amount;
            FlowTrace.Step("Glimmer", $"SPEND {amount} (balance {before} -> {_state.Glimmer}) — caller (BattlePass premium etc.) owns the matching grant");
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
            FlowTrace.Step("Glimmer", $"GrantAchievement id='{id ?? "<null>"}'");
            if (string.IsNullOrEmpty(id)) { FlowTrace.Warn("Glimmer", "GrantAchievement rejected: null/empty id"); return false; }
            var def = CosmeticCatalog.Find(id);
            if (def == null) { FlowTrace.Warn("Glimmer", $"GrantAchievement rejected: '{id}' not in CosmeticCatalog"); return false; }
            EnsureState();
            if (_ownedSet.Contains(id)) { FlowTrace.Warn("Glimmer", $"GrantAchievement no-op: '{id}' already owned"); return false; }
            _ownedSet.Add(id);
            _state.OwnedCosmetics.Add(id);
            FlowTrace.Step("Glimmer", $"achievement cosmetic '{id}' granted (free path) owned={_ownedSet.Count}");
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Marks a cosmetic SKU owned WITHOUT requiring it to be in CosmeticCatalog
        /// (cosmetics.json). Used by the pack-store entitlement path (ECON-02): pack
        /// cosmetic SKUs (e.g. "cosmetic.founders-vow.hero-outfit") are pack rewards,
        /// not shop-catalog items, so GrantAchievement/TryPurchase — both of which
        /// require CosmeticCatalog.Find(id) != null — no-op on them and leave
        /// Owns(sku)==false (unequippable). This writes straight into the same
        /// _ownedSet/_state.OwnedCosmetics backing that Owns() reads and persists via
        /// the same Save() idiom, so a paid pack cosmetic is genuinely owned.
        /// Returns true if the SKU was newly added.
        /// </summary>
        public bool MarkCosmeticOwned(string id)
        {
            FlowTrace.Step("Glimmer", $"MarkCosmeticOwned id='{id ?? "<null>"}'");
            if (string.IsNullOrEmpty(id)) { FlowTrace.Warn("Glimmer", "MarkCosmeticOwned rejected: null/empty id"); return false; }
            EnsureState();
            if (_ownedSet.Contains(id)) { FlowTrace.Warn("Glimmer", $"MarkCosmeticOwned no-op: '{id}' already owned"); return false; }
            _ownedSet.Add(id);
            _state.OwnedCosmetics.Add(id);
            FlowTrace.Step("Glimmer", $"cosmetic '{id}' marked owned (pack entitlement, catalog-independent) owned={_ownedSet.Count}");
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
                FlowTrace.Fail("Glimmer", $"load failed (wallet resets to fresh state, purchases at risk): {ex.GetType().Name}: {ex.Message}");
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
                FlowTrace.Fail("Glimmer", $"save failed (balance/ownership not persisted — a paid grant could be lost): {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning("[GlimmerCurrencyService] save failed: " + ex.Message);
            }
        }
    }
}
