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
        private bool _loaded;   // pulled from persisted GameState.GearInventory yet?

        public IReadOnlyDictionary<string, int> Counts { get { EnsureLoaded(); return _counts; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureLoaded();
        }

        // v20: gear inventory persists via GameState.GearInventory (Neon-synced). Pull once the save
        // is loaded; push back on every change (below). LAZY so a boot-order race can never overwrite
        // loaded gear with an empty set before GameStateService is ready (the _loaded latch only sets
        // once State exists).
        private bool _stateReplacedHooked;   // WO-949: one-shot StateReplaced subscription latch

        private void EnsureLoaded()
        {
            HookStateReplaced();
            if (_loaded) return;
            var st = DeNelle.Core.State.GameStateService.Instance?.State;
            if (st == null) return;   // save not ready yet — retry on next access
            if (st.GearInventory != null && st.GearInventory.Count > 0)
                _counts = new Dictionary<string, int>(st.GearInventory);
            _loaded = true;
        }

        // WO-949: the _loaded latch pulled the persisted larder exactly ONCE per app run — so a
        // New Game (ResetToNewGame reseeds GearInventory, incl. the founding potion grant) or a
        // full save Load left this DDOL singleton holding the PREVIOUS session's counts, and its
        // next SyncToState silently clobbered the fresh save's larder with them. Re-pull whenever
        // the state is wholesale replaced. Subscribed lazily (EnsureLoaded runs on every access)
        // because GameStateService may not exist yet at this component's Awake.
        private void HookStateReplaced()
        {
            if (_stateReplacedHooked) return;
            var gs = DeNelle.Core.State.GameStateService.Instance;
            if (gs == null) return;   // not up yet — retry on next access
            gs.StateReplaced.AddListener(OnStateReplaced);
            _stateReplacedHooked = true;
        }

        private void OnStateReplaced()
        {
            var st = DeNelle.Core.State.GameStateService.Instance?.State;
            if (st == null) return;
            _counts = st.GearInventory != null
                ? new Dictionary<string, int>(st.GearInventory)
                : new Dictionary<string, int>();
            _loaded = true;
            DeNelle.Core.Diagnostics.FlowTrace.Step("Founding",
                "VillageInventory re-pulled the larder on StateReplaced (" + _counts.Count +
                " distinct id(s)) — a New Game / Load can never inherit the old session's counts (WO-949).");
            Changed?.Invoke();
        }

        private void SyncToState()
        {
            var gs = DeNelle.Core.State.GameStateService.Instance;
            if (gs?.State == null) return;
            gs.State.GearInventory = new Dictionary<string, int>(_counts);
            gs.Save();
        }

        private void OnDestroy()
        {
            // WO-949: release the StateReplaced subscription so a destroyed (duplicate) instance
            // can never keep writing a dead component's counts. Null-safe on teardown order.
            if (_stateReplacedHooked)
            {
                var gs = DeNelle.Core.State.GameStateService.Instance;
                if (gs != null) gs.StateReplaced.RemoveListener(OnStateReplaced);
                _stateReplacedHooked = false;
            }
            if (Instance == this) Instance = null;
        }

        public int Get(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return 0;
            return _counts.TryGetValue(id, out var v) ? v : 0;
        }

        public void Add(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            EnsureLoaded();
            if (!_counts.ContainsKey(id)) _counts[id] = 0;
            _counts[id] += amount;
            SyncToState();   // persist the purchase (local save + Neon sync)
            Changed?.Invoke();
        }

        public bool TryConsume(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return false;
            EnsureLoaded();
            if (!_counts.TryGetValue(id, out var have) || have < amount) return false;
            _counts[id] = have - amount;
            if (_counts[id] <= 0) _counts.Remove(id);
            SyncToState();   // persist the consume
            Changed?.Invoke();
            return true;
        }

        // Legacy / compatibility for any code still calling AddResource
        public void AddResource(string type, int amount)
        {
            Add(type, amount);
        }

        /// <summary>Clear for testing / reset.</summary>
        public void Clear()
        {
            _counts.Clear();
            _loaded = true;        // an explicit clear is intentional state, not a pre-load empty
            SyncToState();
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