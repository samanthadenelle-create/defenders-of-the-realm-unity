using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;   // WO-1523: CosmeticSignals - the one number the HUD may read

namespace DeNelle.Cosmetics
{
    [Serializable]
    public sealed class CosmeticOwnershipSaveData
    {
        [JsonProperty("ownedCosmetics")] public List<string> OwnedCosmetics = new List<string>();
        [JsonProperty("equippedByCategory")] public Dictionary<string, string> EquippedByCategory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Currency-free persisted ownership and equip state for cosmetics. The legacy
    /// PlayerPrefs key is intentionally retained so existing owned/equipped cosmetics
    /// round-trip. Newtonsoft ignores the retired legacy currency field on read.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CosmeticOwnershipService : MonoBehaviour
    {
        public const string PrefKey = "dotr-cosmetics-v1";
        public static CosmeticOwnershipService Instance { get; private set; }
        public event Action Changed;

        private CosmeticOwnershipSaveData _state;
        private readonly HashSet<string> _ownedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> OwnedCosmetics
        {
            get { EnsureState(); return _ownedSet; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("CosmeticOwnershipService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CosmeticOwnershipService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureState();
        }

        public bool Owns(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureState();
            return _ownedSet.Contains(id);
        }

        public string EquippedFor(string category)
        {
            if (string.IsNullOrEmpty(category)) return null;
            EnsureState();
            return _state.EquippedByCategory.TryGetValue(category, out var id) ? id : null;
        }

        public void Equip(string id)
        {
            EnsureState();
            if (string.IsNullOrEmpty(id)) return;
            var def = CosmeticCatalog.Find(id);
            if (def == null || !_ownedSet.Contains(id)) return;
            string category = def.Category ?? string.Empty;
            if (_state.EquippedByCategory.TryGetValue(category, out var current) && current == id) return;
            _state.EquippedByCategory[category] = id;
            Save();
            Changed?.Invoke();
        }

        public void UnequipCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return;
            EnsureState();
            if (!_state.EquippedByCategory.Remove(category)) return;
            Save();
            Changed?.Invoke();
        }

        public bool GrantAchievement(string id)
        {
            if (string.IsNullOrEmpty(id) || CosmeticCatalog.Find(id) == null) return false;
            return MarkCosmeticOwned(id);
        }

        public bool MarkCosmeticOwned(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureState();
            if (!_ownedSet.Add(id)) return false;
            _state.OwnedCosmetics.Add(id);
            PublishOwnedCount();
            Save();
            Changed?.Invoke();
            return true;
        }

        private void EnsureState()
        {
            if (_state != null) return;
            if (!TryLoad(out _state) || _state == null) _state = new CosmeticOwnershipSaveData();
            _state.OwnedCosmetics ??= new List<string>();
            _state.EquippedByCategory ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _ownedSet.Clear();
            foreach (string id in _state.OwnedCosmetics)
                if (!string.IsNullOrEmpty(id)) _ownedSet.Add(id);
            PublishOwnedCount();
        }

        /// <summary>
        /// WO-1523. Copy the OWNED COUNT (never the list) into CosmeticSignals so the Hero
        /// deck can decide whether the Wardrobe section exists without referencing this
        /// assembly - DeNelle.HUD/Core cannot see DeNelle.Cosmetics. Published on the
        /// first state read (so the boot count is live before any deck opens) AND on every
        /// grant, because the section must appear the moment the first look unlocks.
        /// </summary>
        private void PublishOwnedCount() => CosmeticSignals.SetOwnedCount(_ownedSet.Count);

        private bool TryLoad(out CosmeticOwnershipSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            try { data = JsonConvert.DeserializeObject<CosmeticOwnershipSaveData>(PlayerPrefs.GetString(PrefKey)); }
            catch (Exception ex)
            {
                FlowTrace.Fail("Cosmetics", "ownership load failed: " + ex.GetType().Name + ": " + ex.Message);
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
                FlowTrace.Fail("Cosmetics", "ownership save failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
