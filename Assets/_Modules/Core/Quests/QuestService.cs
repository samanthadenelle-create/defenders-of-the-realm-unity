// =============================================================================
// QuestService — the GENERAL story-quest runtime (vendor / forgemaster / pet
// narrative lane). FOUNDATIONAL: dialogue verbs + the quest-tracker HUD ride on
// this. Distinct from DailyQuestService (separate ledger: dailies persist to
// PlayerPrefs; story quests persist to GameState.Quests so they sync with the
// save/backend).
// -----------------------------------------------------------------------------
// Singleton, self-bootstrapped (RuntimeInitializeOnLoadMethod). Reads + writes
// GameStateService.Instance.State.Quests (QuestProgress) and calls Save() on
// every mutation. Quest *content* comes from QuestCatalog (quests.json).
//
// Core purity: this NEVER references EconomyService / the wallet. When a stage
// reward is earned it only RAISES RewardEarned — a Village-side bridge
// (QuestRewardBridge) listens and dispenses crystals/food/items.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Core.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestService : MonoBehaviour
    {
        public static QuestService Instance { get; private set; }

        /// <summary>Fired after any quest state mutation (HUD repaints on this).</summary>
        public event Action QuestChanged;

        /// <summary>
        /// Fired when a stage's reward is earned (on AdvanceQuest / CompleteQuest).
        /// Core raises only the numbers; a Village bridge grants them — Core never
        /// references the wallet.
        /// </summary>
        public event Action<QuestReward> RewardEarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("QuestService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<QuestService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── State access ──────────────────────────────────────────────────────

        // The persisted ledger lives on GameState. Null until GameStateService is
        // up; every accessor null-guards so an early call is a safe no-op.
        private QuestProgress Progress => GameStateService.Instance?.State?.Quests;

        private void Persist()
        {
            GameStateService.Instance?.Save();
            QuestChanged?.Invoke();
        }

        // ── Public API — lifecycle ────────────────────────────────────────────

        /// <summary>
        /// Moves a quest from Available → Active at stage 0. No-op if already active
        /// or completed, or if the quest id isn't in the catalog.
        /// </summary>
        public void StartQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var prog = Progress;
            if (prog == null) return;
            if (prog.Active.ContainsKey(id)) return;
            if (prog.Completed.TryGetValue(id, out bool done) && done) return;

            var def = QuestCatalog.FindQuest(id);
            if (def == null) { Debug.LogWarning($"[QuestService] StartQuest unknown id '{id}'."); return; }

            string firstStage = (def.Stages != null && def.Stages.Count > 0) ? def.Stages[0].StageId : null;
            prog.Active[id] = new QuestState { BeatIndex = 0, StageId = firstStage };
            prog.Available.Remove(id);
            Persist();
        }

        /// <summary>
        /// Advances an active quest to its next stage, firing the *completed* stage's
        /// reward (and granting a keystone if that stage carries one). If it was the
        /// final stage the quest auto-completes.
        /// </summary>
        public void AdvanceQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var prog = Progress;
            if (prog == null) return;
            if (!prog.Active.TryGetValue(id, out var st) || st == null) return;

            var stages = QuestCatalog.Stages(id);
            // Reward + keystone come from the stage we are LEAVING.
            if (stages != null && st.BeatIndex >= 0 && st.BeatIndex < stages.Count)
            {
                var leaving = stages[st.BeatIndex];
                if (leaving != null)
                {
                    if (leaving.GrantsKeystone) GiveKeystoneInternal(id + ":" + (leaving.StageId ?? st.BeatIndex.ToString()));
                    if (leaving.Reward != null) RewardEarned?.Invoke(leaving.Reward);
                }
            }

            int next = st.BeatIndex + 1;
            if (stages != null && next >= stages.Count)
            {
                CompleteQuest(id); // final stage cleared
                return;
            }

            st.BeatIndex = next;
            st.StageId = (stages != null && next < stages.Count) ? stages[next].StageId : null;
            Persist();
        }

        /// <summary>Moves a quest from Active → Completed (idempotent).</summary>
        public void CompleteQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var prog = Progress;
            if (prog == null) return;
            prog.Active.Remove(id);
            prog.Available.Remove(id);
            prog.Completed[id] = true;
            Persist();
        }

        // ── Public API — reads ────────────────────────────────────────────────

        /// <summary>Current stage def for an active quest, or null.</summary>
        public QuestStage GetStage(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var prog = Progress;
            if (prog == null || !prog.Active.TryGetValue(id, out var st) || st == null) return null;
            var stages = QuestCatalog.Stages(id);
            if (stages == null || st.BeatIndex < 0 || st.BeatIndex >= stages.Count) return null;
            return stages[st.BeatIndex];
        }

        /// <summary>Active quest ids (snapshot — safe to enumerate/paint).</summary>
        public IReadOnlyList<string> ActiveQuestIds()
        {
            var prog = Progress;
            var list = new List<string>();
            if (prog != null) foreach (var kv in prog.Active) list.Add(kv.Key);
            return list;
        }

        public bool IsActive(string id)
        {
            var prog = Progress;
            return prog != null && !string.IsNullOrEmpty(id) && prog.Active.ContainsKey(id);
        }

        public bool IsCompleted(string id)
        {
            var prog = Progress;
            return prog != null && !string.IsNullOrEmpty(id)
                && prog.Completed.TryGetValue(id, out bool done) && done;
        }

        // ── Public API — flags ────────────────────────────────────────────────

        /// <summary>Sets a per-quest boolean flag (e.g. an objective sub-step).</summary>
        public void SetFlag(string id, string flag)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(flag)) return;
            var prog = Progress;
            if (prog == null) return;
            if (!prog.Active.TryGetValue(id, out var st) || st == null)
            {
                // Allow setting a flag on a not-yet-active quest by seeding a state.
                st = new QuestState { BeatIndex = 0 };
                prog.Active[id] = st;
            }
            if (st.Flags == null) st.Flags = new Dictionary<string, bool>();
            st.Flags[flag] = true;
            Persist();
        }

        public bool HasFlag(string id, string flag)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(flag)) return false;
            var prog = Progress;
            if (prog == null || !prog.Active.TryGetValue(id, out var st) || st == null || st.Flags == null)
                return false;
            return st.Flags.TryGetValue(flag, out bool v) && v;
        }

        // ── Public API — keystones (aggregate story-progression ledger) ───────

        /// <summary>Awards a named keystone (no-op if already held).</summary>
        public void GiveKeystone(string name)
        {
            if (GiveKeystoneInternal(name)) Persist();
        }

        // Adds without persisting (so AdvanceQuest can batch the save). Returns true
        // if the keystone was newly added.
        private bool GiveKeystoneInternal(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var prog = Progress;
            if (prog == null) return false;
            if (prog.Keystones == null) prog.Keystones = new List<string>();
            if (prog.Keystones.Contains(name)) return false;
            prog.Keystones.Add(name);
            return true;
        }

        public bool HasKeystone(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var prog = Progress;
            return prog != null && prog.Keystones != null && prog.Keystones.Contains(name);
        }

        public int KeystoneCount
        {
            get
            {
                var prog = Progress;
                return prog != null && prog.Keystones != null ? prog.Keystones.Count : 0;
            }
        }
    }
}
