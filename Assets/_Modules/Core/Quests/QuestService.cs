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
            if (prog == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Quest", $"StartQuest('{id}') before GameState ready (Progress null) — no-op.");
                return;
            }
            if (prog.Active.ContainsKey(id)) return;
            if (prog.Completed.TryGetValue(id, out bool done) && done) return;

            var def = QuestCatalog.FindQuest(id);
            if (def == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Quest", $"StartQuest unknown id '{id}' (not in QuestCatalog) — no-op.");
                Debug.LogWarning($"[QuestService] StartQuest unknown id '{id}'."); return;
            }

            string firstStage = (def.Stages != null && def.Stages.Count > 0) ? def.Stages[0].StageId : null;
            prog.Active[id] = new QuestState { BeatIndex = 0, StageId = firstStage };
            prog.Available.Remove(id);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Quest", $"StartQuest '{id}' -> Active @stage '{firstStage ?? "<none>"}'.");
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
            if (!prog.Active.TryGetValue(id, out var st) || st == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Quest", $"AdvanceQuest('{id}') but quest is not Active — no-op.");
                return;
            }

            var stages = QuestCatalog.Stages(id);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Quest", $"AdvanceQuest '{id}' leaving beat {st.BeatIndex}/{(stages?.Count ?? 0)}.");
            // Reward + keystone come from the stage we are LEAVING.
            if (stages != null && st.BeatIndex >= 0 && st.BeatIndex < stages.Count)
            {
                var leaving = stages[st.BeatIndex];
                if (leaving != null)
                {
                    if (leaving.GrantsKeystone) GiveKeystoneInternal(id + ":" + (leaving.StageId ?? st.BeatIndex.ToString()));
                    if (leaving.Reward != null)
                    {
                        DeNelle.Core.Diagnostics.FlowTrace.Step("Quest", $"reward earned on '{id}' beat {st.BeatIndex} (crystals={leaving.Reward.Crystals},food={leaving.Reward.Food},magic={leaving.Reward.Magic},item='{leaving.Reward.GrantItemId}').");
                        RewardEarned?.Invoke(leaving.Reward);
                    }
                }
            }

            int next = st.BeatIndex + 1;
            if (stages != null && next >= stages.Count)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Step("Quest", $"'{id}' final stage cleared -> CompleteQuest.");
                CompleteQuest(id); // final stage cleared
                return;
            }

            st.BeatIndex = next;
            st.StageId = (stages != null && next < stages.Count) ? stages[next].StageId : null;
            Persist();
        }

        /// <summary>
        /// Moves a quest from Active → Completed (idempotent).
        ///
        /// WO-854 Phase 2 hardening: this used to write Completed[id] for ANY id, with no
        /// catalog lookup -- the only quest verb that never checked. A dialogue authoring
        /// CompleteQuest against an id absent from quests.json (the shipped 'companion.sylas'
        /// verbs) therefore minted a phantom Completed entry for a quest that does not exist,
        /// and CompletedQuestCount -- which the WO-587 population-growth bridge polls -- counted
        /// it, inflating a live progression input. An unknown id is now refused and traced, the
        /// same treatment StartQuest already gave it (:92) -- and any phantom row it already left
        /// in Active/Available is evicted rather than left to rot (see the note in the body).
        /// Known ids behave exactly as before, and the read path (IsCompleted /
        /// CompletedQuestCount) is untouched.
        /// </summary>
        public void CompleteQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var prog = Progress;
            if (prog == null) return;
            if (QuestCatalog.FindQuest(id) == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Quest",
                    $"CompleteQuest unknown id '{id}' (not in QuestCatalog) -- NOT completed. A quest that " +
                    "does not exist must never reach the Completed ledger; CompletedQuestCount feeds " +
                    "population growth.");
                Debug.LogWarning($"[QuestService] CompleteQuest unknown id '{id}'.");
                // Evict any phantom Active/Available row for this id instead of returning flat.
                // AdvanceQuest routes final-stage completion through here (:140), and an id with no
                // catalog entry has no stages, so a bare return would leave a pre-fix save's phantom
                // stuck in Active forever, re-entering this path on every advance. Nothing is written
                // to Completed; the read path (IsCompleted / CompletedQuestCount) is untouched.
                // Non-short-circuit '|' on purpose: both Remove calls must run.
                bool evicted = prog.Active.Remove(id) | prog.Available.Remove(id);
                if (prog.TrackedId == id) { prog.TrackedId = null; evicted = true; }
                if (evicted) Persist();
                return;
            }
            prog.Active.Remove(id);
            prog.Available.Remove(id);
            prog.Completed[id] = true;
            if (prog.TrackedId == id) prog.TrackedId = null; // WO-454: drop the HUD pin when the tracked quest completes
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

        // ── Public API — tracked quest (WO-454: the one pinned to the far-right HUD) ──

        /// <summary>The player-selected quest id pinned to the HUD slot (null = none chosen).</summary>
        public string TrackedId
        {
            get { var prog = Progress; return prog != null ? prog.TrackedId : null; }
        }

        /// <summary>Pin a quest as the tracked HUD quest (empty/null clears it). Persists and
        /// raises QuestChanged so the HUD pin repaints to the player's selection.</summary>
        public void SetTracked(string id)
        {
            var prog = Progress;
            if (prog == null) return;
            string norm = string.IsNullOrEmpty(id) ? null : id;
            if (prog.TrackedId == norm) return;
            prog.TrackedId = norm;
            Persist();
        }

        // ── Public API — flags ────────────────────────────────────────────────

        /// <summary>
        /// Sets a per-quest boolean flag (e.g. an objective sub-step) on an ACTIVE quest.
        ///
        /// WO-854 Phase 2 hardening: this used to seed <c>prog.Active[id]</c> when the quest
        /// was not active, which STARTED the quest as a side effect -- bypassing StartQuest's
        /// catalog lookup and Available bookkeeping. Two live consequences: a dialogue that
        /// authored SetQuestFlag before StartQuest started the quest by accident, and a flag
        /// naming an id absent from quests.json (the shipped 'companion.sylas' verbs) minted a
        /// phantom Active entry with no stage chain, which CompleteQuest then counted in
        /// CompletedQuestCount. It now writes the flag only when the quest is already active
        /// and otherwise no-ops with a trace. The read side (HasFlag) is unchanged, so every
        /// flag set on a genuinely active quest behaves exactly as before.
        /// </summary>
        public void SetFlag(string id, string flag)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(flag)) return;
            var prog = Progress;
            if (prog == null) return;
            if (!prog.Active.TryGetValue(id, out var st) || st == null)
            {
                bool completed = prog.Completed != null
                    && prog.Completed.TryGetValue(id, out bool done) && done;
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Quest",
                    $"SetFlag('{id}','{flag}') but the quest is {(completed ? "already COMPLETED" : "not Active")} " +
                    "-- flag dropped (a flag no longer starts/reopens a quest as a side effect; author StartQuest first).");
                return;
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

        /// <summary>
        /// Cumulative count of quests marked Completed (true). WO-587: the Population growth
        /// bridge polls this on QuestChanged to award population XP per newly-completed quest
        /// without QuestService taking any dependency on Population (Core purity preserved).
        /// </summary>
        public int CompletedQuestCount
        {
            get
            {
                var prog = Progress;
                if (prog == null || prog.Completed == null) return 0;
                int n = 0;
                foreach (var kv in prog.Completed) if (kv.Value) n++;
                return n;
            }
        }
    }
}
