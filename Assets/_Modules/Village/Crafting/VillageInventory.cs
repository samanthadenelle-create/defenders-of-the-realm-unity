// =============================================================================
// VillageInventory — singleton ingredient/output store for the village-side
// crafting station. Persists to PlayerPrefs so the village larder rides
// across sessions (unlike the per-run DungeonInventory ScriptableObject).
// -----------------------------------------------------------------------------
// Pattern model: DailyQuestService — RuntimeInitializeOnLoadMethod bootstraps a
// hidden GameObject, DontDestroyOnLoad, JSON-encodes its state into a single
// PlayerPrefs key. The dungeon-side crafting inventory is intentionally
// SEPARATE (resets each run) — they are not the same store.
//
// Public surface:
//   • int Get(id)                      — current count, 0 if unknown
//   • void Add(id, amount)             — adds, clamps to >= 0, raises Changed
//   • bool TryConsume(id, amount)      — returns false if short, else consumes
//   • bool TryCraft(recipeId)          — checks + consumes ingredients + grants output
//   • event Action Changed             — any mutation; UI panel subscribes
//
// First-run seed: 5 of every ingredient listed in crafting-recipes.json so the
// panel is testable the moment the player enters the village.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Crafting
{
    [DisallowMultipleComponent]
    public sealed class VillageInventory : MonoBehaviour
    {
        private const string PrefKey = "dotr-village-inventory-v1";
        private const int FirstRunSeedAmount = 5;

        public static VillageInventory Instance { get; private set; }

        /// <summary>Raised on every mutation (add / consume / craft).</summary>
        public event Action Changed;

        [Serializable]
        private sealed class SaveData
        {
            public List<string> Ids = new List<string>();
            public List<int> Amounts = new List<int>();
        }

        // Live counts. Keyed by ingredient id OR recipe output id (same string).
        public IReadOnlyDictionary<string, int> Counts => _counts;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("VillageInventory");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<VillageInventory>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
            if (_counts.Count == 0)
            {
                SeedFirstRun();
                Save();
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        public int Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            return _counts.TryGetValue(id, out var n) ? n : 0;
        }

        public void Add(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount == 0) return;
            int cur = Get(id);
            int next = Mathf.Max(0, cur + amount);
            if (next == 0) _counts.Remove(id);
            else _counts[id] = next;
            Save();
            Changed?.Invoke();
        }

        public bool TryConsume(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return false;
            int cur = Get(id);
            if (cur < amount) return false;
            int next = cur - amount;
            if (next <= 0) _counts.Remove(id);
            else _counts[id] = next;
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Crafts the recipe with <paramref name="recipeId"/>: checks every
        /// ingredient line is satisfied, consumes them, grants one of the
        /// output (keyed by the recipe id itself), and raises <see cref="Changed"/>.
        /// </summary>
        public bool TryCraft(string recipeId)
        {
            var recipe = CraftingRecipeCatalog.Find(recipeId);
            if (recipe == null || recipe.Ingredients == null) return false;

            // Verify the larder can cover every line BEFORE consuming any.
            foreach (var line in recipe.Ingredients)
            {
                if (line == null) continue;
                if (Get(line.IngredientId) < line.Count) return false;
            }

            // Consume.
            foreach (var line in recipe.Ingredients)
            {
                if (line == null) continue;
                int next = Get(line.IngredientId) - line.Count;
                if (next <= 0) _counts.Remove(line.IngredientId);
                else _counts[line.IngredientId] = next;
            }

            // Grant the output.
            int curOut = Get(recipe.OutputId);
            _counts[recipe.OutputId] = curOut + 1;

            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>True when the larder can cover every ingredient line right now.</summary>
        public bool CanCraft(string recipeId)
        {
            var recipe = CraftingRecipeCatalog.Find(recipeId);
            if (recipe == null || recipe.Ingredients == null) return false;
            foreach (var line in recipe.Ingredients)
            {
                if (line == null) continue;
                if (Get(line.IngredientId) < line.Count) return false;
            }
            return true;
        }

        // ── Persistence ──────────────────────────────────────────────────────

        private void Load()
        {
            _counts.Clear();
            if (!PlayerPrefs.HasKey(PrefKey)) return;
            try
            {
                var data = JsonConvert.DeserializeObject<SaveData>(PlayerPrefs.GetString(PrefKey));
                if (data == null || data.Ids == null || data.Amounts == null) return;
                int n = Mathf.Min(data.Ids.Count, data.Amounts.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(data.Ids[i])) continue;
                    if (data.Amounts[i] <= 0) continue;
                    _counts[data.Ids[i]] = data.Amounts[i];
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VillageInventory] load failed: " + ex.Message);
            }
        }

        private void Save()
        {
            try
            {
                var data = new SaveData();
                foreach (var kv in _counts)
                {
                    data.Ids.Add(kv.Key);
                    data.Amounts.Add(kv.Value);
                }
                PlayerPrefs.SetString(PrefKey, JsonConvert.SerializeObject(data));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VillageInventory] save failed: " + ex.Message);
            }
        }

        private void SeedFirstRun()
        {
            // Five of every common ingredient declared in crafting-recipes.json
            // so the village crafting panel is testable on first launch.
            foreach (var ing in CraftingRecipeCatalog.Ingredients)
            {
                if (ing == null || string.IsNullOrEmpty(ing.Id)) continue;
                _counts[ing.Id] = FirstRunSeedAmount;
            }
        }

        /// <summary>Dev-only: wipes the larder and re-seeds. Used by reset tools.</summary>
        public void ResetAndReseed()
        {
            _counts.Clear();
            SeedFirstRun();
            Save();
            Changed?.Invoke();
        }
    }
}
