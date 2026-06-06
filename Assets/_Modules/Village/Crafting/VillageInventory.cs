using UnityEngine;
using System;
using System.Collections.Generic;

namespace DeNelle.Village.Crafting
{
    /// <summary>
    /// Persisted (or session) item / larder inventory for crafting, consumables, and drops.
    /// Exposes the API expected by VillageCraftingPanel, ItemInventory (facade), ItemDropSystem, etc.
    /// Singleton via .Instance (MonoBehaviour pattern).
    /// </summary>
    public class VillageInventory : MonoBehaviour
    {
        public static VillageInventory Instance { get; private set; }

        public event Action Changed;

        [SerializeField]
        private Dictionary<string, int> _counts = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Counts => _counts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // In a full impl this would load from GameState / persistent store.
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            return _counts.TryGetValue(id, out var v) ? v : 0;
        }

        public void Add(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            if (!_counts.ContainsKey(id)) _counts[id] = 0;
            _counts[id] += amount;
            Changed?.Invoke();
        }

        public bool TryConsume(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return false;
            if (!_counts.TryGetValue(id, out var have) || have < amount) return false;
            _counts[id] = have - amount;
            if (_counts[id] <= 0) _counts.Remove(id);
            Changed?.Invoke();
            return true;
        }

        // Legacy / compatibility for any code still calling AddResource
        public void AddResource(string type, int amount)
        {
            Add(type, amount);
        }

        /// <summary>Clear for testing / reset (does not persist).</summary>
        public void Clear()
        {
            _counts.Clear();
            Changed?.Invoke();
        }

        // Stubs for crafting API used by VillageCraftingPanel (recipes check ingredients
        // in larder). Real impl should look up RecipeDef costs and TryConsume each.
        public bool CanCraft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            // TODO: proper recipe lookup + ingredient check. Stub allows UI for now.
            return true;
        }

        public bool TryCraft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            // TODO: consume exact ingredients per recipe, then Add output.
            Changed?.Invoke();
            return true; // stub success
        }

        // --- Bootstrap (ensures Instance even if no scene object yet) ---
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[VillageInventory]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VillageInventory>();
        }
    }
}