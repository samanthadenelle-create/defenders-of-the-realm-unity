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
using DeNelle.Core.Quests;
using DeNelle.Core.State;   // WO-1220 — GameStateService.NewGameStarted / TalentPrefKey

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
        // WO-1220 — the key is now owned by GameStateService (ProgressionPrefKeys), so the New
        // Game reset and this store can never drift onto two different keys. It was a private
        // const here, invisible to the reset, which is why the reset never cleared it.
        private const string PrefKey = DeNelle.Core.State.GameStateService.TalentPrefKey;

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
            // WO-1220 — this service is DontDestroyOnLoad and holds Wisdom + the unlocked
            // talent-node ids IN MEMORY. GameStateService.ResetToNewGame deletes the PlayerPrefs
            // blob, but without this subscription the live singleton would still be holding the
            // previous hero's tree and would write it straight back out on the next Grant/Unlock
            // — which is how a brand-new Ranger came up with a Mage's shared.n5 applied.
            GameStateService.NewGameStarted += ResetForNewGame;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameStateService.NewGameStarted -= ResetForNewGame;   // WO-1220 — static event: never leak.
        }

        /// <summary>
        /// WO-1220 — a New Game starts with ZERO Wisdom and ZERO unlocked talent nodes.
        ///
        /// Talent node ids are hero-prefixed ("mage.n3") but the unlocked SET is a single
        /// shared pool, and the <c>shared.*</c> nodes carry no hero prefix at all — so a
        /// surviving set does not merely leak the old hero's talents, it applies them to
        /// whatever class the player picks next. Clearing the set and the balance together,
        /// then persisting, leaves the store in exactly the state a first-ever launch sees.
        /// </summary>
        public void ResetForNewGame()
        {
            DeNelle.Core.Diagnostics.FlowTrace.Step("HeroTalents",
                $"ResetForNewGame: dropping {_wisdom} Wisdom and {_unlocked.Count} unlocked talent " +
                "node(s) — a New Game starts on an empty tree, whatever class was played before.");
            _wisdom = 0;
            _unlocked = new HashSet<string>();
            Save();
            Changed?.Invoke();
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
            // WO-558: wildcard daily-quest progress — one tick per talent learned.
            DailyQuestService.Instance?.Report("wildcard.learn-talent", 1);
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
