// =============================================================================
// WisdomCurrencyService - singleton MonoBehaviour that owns Wisdom (the
// talent-unlock currency) and the set of learned talent node ids.
// -----------------------------------------------------------------------------
// Bootstrapped via [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] so any UI
// or gameplay code can read the live values without ordering coupling. State
// persists in PlayerPrefs under "dotr-talents-v1" as a JSON payload via
// Newtonsoft (matches DailyQuestService).
//
// We deliberately keep state local to this service rather than extending
// GameState - the talents system is opt-in cosmetic-tier polish, and the
// GameState save migration churn isn't worth the coupling.
//
// Public API mirrors the spec:
//   int  Wisdom              - current Wisdom balance (read-only)
//   bool TrySpend(int n)     - returns true on success; false if too poor
//   bool Unlock(nodeId)      - checks prereqs + cost via HeroTalentCatalog,
//                              spends Wisdom and adds to the unlocked set
//   event Changed            - fired after every Wisdom or Unlocked mutation
//
// Out of scope here: per-hero Wisdom partitioning, respec, income hooks. The
// store collapses to a single Wisdom pool for the V2 minimum-viable build;
// node ids are already hero-prefixed so the unlocked set is self-segmenting.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Talents
{
    [Serializable]
    internal sealed class WisdomSaveBlob
    {
        public int Wisdom;
        public List<string> Unlocked = new List<string>();
    }

    [DisallowMultipleComponent]
    public sealed class WisdomCurrencyService : MonoBehaviour
    {
        private const string PrefKey = "dotr-talents-v1";

        public static WisdomCurrencyService Instance { get; private set; }
        public event Action Changed;

        private int _wisdom;
        private HashSet<string> _unlocked = new HashSet<string>();

        public int Wisdom => _wisdom;
        public IReadOnlyCollection<string> Unlocked => _unlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("WisdomCurrencyService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<WisdomCurrencyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        // -- Public API ---------------------------------------------------------

        /// <summary>Adds <paramref name="amount"/> Wisdom (no negative inputs).</summary>
        public void Grant(int amount)
        {
            if (amount <= 0) return;
            _wisdom += amount;
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Spends <paramref name="amount"/> if the wallet allows. Returns true
        /// on success. Negative or zero amounts are no-ops returning true.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (_wisdom < amount) return false;
            _wisdom -= amount;
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// True if <paramref name="nodeId"/> is already learned.
        /// </summary>
        public bool IsUnlocked(string nodeId) =>
            !string.IsNullOrEmpty(nodeId) && _unlocked.Contains(nodeId);

        /// <summary>
        /// Attempts to learn <paramref name="nodeId"/>: validates against the
        /// catalog (prereqs + cost), debits Wisdom, and inserts into the
        /// unlocked set. Returns false if the node is unknown, already learned,
        /// blocked by prereqs, or unaffordable.
        /// </summary>
        public bool Unlock(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;
            if (_unlocked.Contains(nodeId)) return false;
            var node = HeroTalentCatalog.FindNode(nodeId);
            if (node == null) return false;
            if (!HeroTalentCatalog.CanUnlock(nodeId, _wisdom, _unlocked)) return false;
            if (_wisdom < node.Cost) return false;

            _wisdom -= node.Cost;
            _unlocked.Add(nodeId);
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Wipes the unlocked set and refunds spent Wisdom for the supplied
        /// hero. Used by the respec flow once the crystal cost is paid.
        /// </summary>
        public void RespecHero(string heroSlug)
        {
            if (string.IsNullOrEmpty(heroSlug)) return;
            int refund = 0;
            var prefix = heroSlug + ".";
            var stillUnlocked = new HashSet<string>();
            foreach (var id in _unlocked)
            {
                if (id != null && id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var node = HeroTalentCatalog.FindNode(id);
                    if (node != null) refund += node.Cost;
                }
                else
                {
                    stillUnlocked.Add(id);
                }
            }
            _unlocked = stillUnlocked;
            _wisdom += refund;
            Save();
            Changed?.Invoke();
        }

        // -- Persistence --------------------------------------------------------

        private void Load()
        {
            if (!PlayerPrefs.HasKey(PrefKey)) return;
            try
            {
                var blob = JsonConvert.DeserializeObject<WisdomSaveBlob>(PlayerPrefs.GetString(PrefKey));
                if (blob == null) return;
                _wisdom = Mathf.Max(0, blob.Wisdom);
                _unlocked = blob.Unlocked != null
                    ? new HashSet<string>(blob.Unlocked)
                    : new HashSet<string>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WisdomCurrencyService] load failed: " + ex.Message);
            }
        }

        private void Save()
        {
            try
            {
                var blob = new WisdomSaveBlob
                {
                    Wisdom = _wisdom,
                    Unlocked = new List<string>(_unlocked),
                };
                PlayerPrefs.SetString(PrefKey, JsonConvert.SerializeObject(blob));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WisdomCurrencyService] save failed: " + ex.Message);
            }
        }
    }
}
