// =============================================================================
// DailyQuestGateBridge — owner playtest fix: the "Walk past all four village
// gates in one day" daily quest (template id "explore.walk-all-gates", tracked
// 0/4 → 4/4) never progressed because nothing reported gate visits to the
// DailyQuestService. This bridge closes that gap, mirroring DailyQuestTowerBridge.
// -----------------------------------------------------------------------------
// How it detects a gate-pass (no scene edit, no Gate.cs / GateProximityOpener
// edit):
//   • Each Gate already flips Gate.IsOpenForHero true while the hero stands in
//     its GateProximityOpener radius (the same 8 m approach zone that lets the
//     hero walk out). We poll the live Gate set each frame and watch that flag's
//     RISING EDGE per gate.
//   • A gate that has never been visited TODAY and whose IsOpenForHero just went
//     true counts as one gate-walk → one Report() tick. De-dup is per gate-id
//     per local day, so walking the same gate twice (or lingering in its radius)
//     counts ONCE; visiting all four distinct gates completes the quest at 4/4.
//   • The visited set is keyed by local date and persisted to PlayerPrefs so the
//     progress survives a scene reload / app restart within the same day and
//     resets cleanly at the next local midnight (matching the quest set's reset).
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod into a DontDestroyOnLoad
// object — the same lifecycle DailyQuestService / DailyQuestTowerBridge use.
// Village → Core only; all cross-module calls are null-conditional.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Quests;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class DailyQuestGateBridge : MonoBehaviour
    {
        /// <summary>Template id of the "Walk past all four village gates" quest.</summary>
        private const string GateWalkTemplateId = "explore.walk-all-gates";

        /// <summary>PlayerPrefs key holding "date|gateId,gateId,..." for today's visits.</summary>
        private const string VisitedPrefKey = "dotr-daily-quest-gates-visited-v1";

        private static DailyQuestGateBridge _instance;

        // Gate-ids visited so far TODAY (de-duplicated). Re-loaded from PlayerPrefs
        // for the current local date so progress is stable across scene reloads.
        private readonly HashSet<string> _visitedToday = new HashSet<string>();
        private string _visitedDate;

        // Per-gate previous IsOpenForHero, so we only act on the false→true edge.
        private readonly Dictionary<Gate, bool> _wasOpen = new Dictionary<Gate, bool>();

        // Re-scan the scene's gates occasionally (they spawn with the village and
        // can change across scene loads) without paying a FindObjects cost every frame.
        private float _rescanTimer;
        private const float RescanInterval = 2f;
        private readonly List<Gate> _gates = new List<Gate>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("DailyQuestGateBridge");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<DailyQuestGateBridge>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            LoadVisitedForToday();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Roll the visited set over at local midnight (cheap string compare).
            string today = LocalDateString();
            if (today != _visitedDate) LoadVisitedForToday();

            // The "Walk all gates" quest only lives in the exploration slot. If it
            // isn't in today's set there's nothing to track — skip the work.
            if (!GateWalkActiveToday()) { _rescanTimer = 0f; _gates.Clear(); return; }

            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = RescanInterval;
                RescanGates();
            }

            for (int i = 0; i < _gates.Count; i++)
            {
                var gate = _gates[i];
                if (gate == null) continue;

                bool open = gate.IsOpenForHero;
                _wasOpen.TryGetValue(gate, out bool prev);
                _wasOpen[gate] = open;

                // Rising edge: the hero just entered this gate's proximity zone.
                if (open && !prev) RegisterGateVisit(gate.GateId);
            }
        }

        // ── Gate discovery ─────────────────────────────────────────────────────

        private void RescanGates()
        {
            _gates.Clear();
            // Unity 6 API; include inactive so a gate disabled for a frame still counts.
            var found = Object.FindObjectsByType<Gate>(FindObjectsInactive.Include);
            if (found == null) return;
            _gates.AddRange(found);

            // Drop edge-state for gates that no longer exist (scene unload).
            if (_wasOpen.Count > 0)
            {
                var stale = new List<Gate>();
                foreach (var kv in _wasOpen) if (kv.Key == null) stale.Add(kv.Key);
                foreach (var g in stale) _wasOpen.Remove(g);
            }
        }

        // ── Visit registration + de-dup ────────────────────────────────────────

        private void RegisterGateVisit(string gateId)
        {
            if (string.IsNullOrEmpty(gateId)) return;
            if (!_visitedToday.Add(gateId)) return; // already counted today — no double-tick

            SaveVisited();

            // Same DailyQuests API the tower bridge uses: one tick per distinct gate
            // visited → advances "explore.walk-all-gates" 0/4 → 4/4, firing
            // QuestCompleted (→ reward dispense) when the fourth distinct gate lands.
            DailyQuestService.Instance?.Report(GateWalkTemplateId, 1);
        }

        private bool GateWalkActiveToday()
        {
            var svc = DailyQuestService.Instance;
            if (svc == null) return false;
            var set = svc.Today;
            if (set?.Quests == null) return false;
            foreach (var q in set.Quests)
                if (q != null && !q.Completed && q.TemplateId == GateWalkTemplateId) return true;
            return false;
        }

        // ── Per-day persistence ────────────────────────────────────────────────

        private void LoadVisitedForToday()
        {
            _visitedDate = LocalDateString();
            _visitedToday.Clear();
            _wasOpen.Clear();

            string raw = PlayerPrefs.GetString(VisitedPrefKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;

            int bar = raw.IndexOf('|');
            if (bar <= 0) return;
            string date = raw.Substring(0, bar);
            if (date != _visitedDate) return; // stored data is from a prior day — ignore

            string ids = raw.Substring(bar + 1);
            foreach (var id in ids.Split(','))
                if (!string.IsNullOrEmpty(id)) _visitedToday.Add(id);
        }

        private void SaveVisited()
        {
            string joined = string.Join(",", _visitedToday);
            PlayerPrefs.SetString(VisitedPrefKey, _visitedDate + "|" + joined);
            PlayerPrefs.Save();
        }

        private static string LocalDateString() => System.DateTime.Now.ToString("yyyy-MM-dd");

        // ── TODO: bond-rank quest ("wildcard.increase-bond-rank") ──────────────
        // The third daily quest ("Increase a Bond Rank") is ALSO untracked, but
        // unlike gate-walks there is no runtime "bond rank increased" signal to
        // hook: BondRank is loaded from save data (PetDeployer.SetBondRanks) and
        // only consumed by combat — nothing in this branch RAISES a pet/companion
        // bond rank during play, so there is no event/edge to report. Once a real
        // bond-up mechanic exists (e.g. PetDeployer / a CompanionBondService raising
        // a rank and firing an event), mirror this bridge: subscribe to that event
        // and call DailyQuestService.Instance?.Report("wildcard.increase-bond-rank", 1).
        // Flagged to the gatekeeper as a separate gap — not forced here.
    }
}
