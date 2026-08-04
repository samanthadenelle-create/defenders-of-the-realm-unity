// =============================================================================
// StoryQuestSignalBridge -- Village-side listener that COMPLETES story-quest
// stages from gameplay signals (WO-854 Phase 2).
// -----------------------------------------------------------------------------
// Before this bridge, QuestService.AdvanceQuest had exactly one runtime caller:
// a dialogue authoring an explicit <<AdvanceQuest>> verb (DialogueCommandSink).
// A stage whose objective is "clear a wave" / "place a mill" / "win in the arena"
// had no way to finish at all.
//
// This bridge closes that gap. QuestStage.completeOn (Core) describes the
// condition; QuestCompletion.ToSignalId() composes the TutorialSignals bus id it
// waits for; this bridge subscribes the bus and calls AdvanceQuest on a match.
// Core describes and raises, Village bridges -- the QuestRewardBridge pattern.
// Village -> Core only; every cross-module call is null-conditional.
//
// LATCH DISCIPLINE (the trap this file exists to avoid): TutorialSignals LATCHES
// (TutorialSignals.cs:55-56,77-78) -- HasFired stays true until someone Clears it.
// A stage awaiting an id that already fired earlier in the session would complete
// the instant the quest was accepted. So the bridge calls
// TutorialSignals.Clear(awaitedId) at the moment a stage becomes current, and
// then only ever accepts a FRESH Raise. Never read the latch here.
//
// Two completeOn kinds are deliberately NOT bus-driven:
//   * "flag"            -- satisfied by QuestService.HasFlag, polled below.
//   * "dialogueCommand" -- the legacy path; the dialogue calls AdvanceQuest itself,
//                         so this bridge stays out of the way entirely.
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod into a DontDestroyOnLoad
// object, mirroring QuestRewardBridge's lifecycle.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;
using DeNelle.Core.State;
using DeNelle.Core.Tutorial;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class StoryQuestSignalBridge : MonoBehaviour
    {
        // Re-arm sweep cadence. Quest starts/advances arrive on QuestChanged, so this
        // poll only catches state that appeared without an event (a save load) and
        // drives the non-bus "flag" kind.
        private const float PollInterval = 1f;

        private static StoryQuestSignalBridge _instance;

        private bool _busSubscribed;
        private bool _questSubscribed;
        private float _nextPollAt;
        // Set by QuestChanged / by an advance; consumed next Update so the re-arm never
        // runs inside AdvanceQuest's own Persist -> QuestChanged callback.
        private bool _rearmDue = true;

        // questId -> the bus signal id its CURRENT stage awaits. Bus-driven stages only.
        private readonly Dictionary<string, string> _awaited =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // questId -> "<stageId>@<beatIndex>" the entry above was composed from. A change
        // here means a new stage became current, which is what triggers the latch Clear.
        private readonly Dictionary<string, string> _armedAt =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // Reused buffers so a signal fire allocates nothing and, more importantly, so we
        // never mutate a dictionary while enumerating it (AdvanceQuest re-arms). One list
        // per call site: Dispatch can run while Update's lists are mid-loop.
        private readonly List<string> _matched = new List<string>();
        private readonly List<string> _stale = new List<string>();
        private readonly List<string> _flagReady = new List<string>();

        // Advancing a quest pays its reward, which moves the wallet, which raises
        // "economy.can_afford_upgrade" back onto this same bus. Dispatch must therefore
        // never re-enter itself: a signal arriving mid-dispatch queues here instead.
        private readonly List<string> _pending = new List<string>();
        private bool _dispatching;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("StoryQuestSignalBridge");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoryQuestSignalBridge>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            TrySubscribe();
        }

        private void OnEnable() => TrySubscribe();

        private void OnDestroy()
        {
            if (_busSubscribed) { TutorialSignals.Raised -= OnSignalRaised; _busSubscribed = false; }
            if (_questSubscribed && QuestService.Instance != null)
            {
                QuestService.Instance.QuestChanged -= OnQuestChanged;
                _questSubscribed = false;
            }
            if (_instance == this) _instance = null;
        }

        private void TrySubscribe()
        {
            if (!_busSubscribed)
            {
                TutorialSignals.Raised += OnSignalRaised;
                _busSubscribed = true;
            }
            if (_questSubscribed) return;
            // QuestService self-bootstraps too; if it wasn't up yet, Update retries.
            var svc = QuestService.Instance;
            if (svc == null) return;
            svc.QuestChanged += OnQuestChanged;
            _questSubscribed = true;
            _rearmDue = true;
        }

        private void Update()
        {
            if (!_questSubscribed) TrySubscribe();

            bool due = _rearmDue;
            if (!due && Time.unscaledTime >= _nextPollAt) due = true;
            if (!due) return;

            _rearmDue = false;
            _nextPollAt = Time.unscaledTime + PollInterval;
            RearmActiveStages();
            EvaluateFlagStages();
        }

        private void OnQuestChanged() => _rearmDue = true;

        // -- Arming ------------------------------------------------------------

        private static QuestProgress Progress => GameStateService.Instance?.State?.Quests;

        /// <summary>
        /// Recomposes the awaited signal for every active quest, and CLEARS the bus latch
        /// for any stage that just became current. Drops entries for quests that are no
        /// longer active. Idempotent: a quest already armed on the same stage is skipped,
        /// so the latch is cleared exactly once per stage.
        /// </summary>
        private void RearmActiveStages()
        {
            var prog = Progress;
            if (prog == null || prog.Active == null) return;
            var svc = QuestService.Instance;
            if (svc == null) return;

            // Forget quests that left the Active ledger (completed or abandoned).
            _stale.Clear();
            foreach (var kv in _armedAt) if (!prog.Active.ContainsKey(kv.Key)) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                _armedAt.Remove(_stale[i]);
                _awaited.Remove(_stale[i]);
            }

            foreach (var kv in prog.Active)
            {
                string questId = kv.Key;
                var state = kv.Value;
                if (string.IsNullOrEmpty(questId) || state == null) continue;

                string armKey = (state.StageId ?? string.Empty) + "@" + state.BeatIndex;
                if (_armedAt.TryGetValue(questId, out string prevKey) && prevKey == armKey) continue;

                _armedAt[questId] = armKey;
                _awaited.Remove(questId);

                var stage = svc.GetStage(questId);
                var cond = stage?.CompleteOn;
                if (cond == null) continue;   // legacy dialogue-command stage -- not ours

                string signal = cond.ToSignalId();
                if (string.IsNullOrEmpty(signal))
                {
                    // "flag" is handled by EvaluateFlagStages; "dialogueCommand" by the
                    // dialogue itself. Anything else is an authoring error worth naming.
                    string kind = cond.NormalizedKind;
                    if (kind != QuestCompletion.KindFlag && kind != QuestCompletion.KindDialogueCommand)
                        FlowTrace.Fail("Quest",
                            $"stage '{questId}/{stage?.StageId}' has completeOn kind '{cond.Kind}' " +
                            "that composes no signal (unknown kind, or a kind that needs a targetId and has none) " +
                            "-- this stage can never complete from the bus.");
                    continue;
                }

                // THE LATCH CLEAR. Without this a stage awaiting an id that already fired
                // this session would complete the moment the quest is accepted.
                TutorialSignals.Clear(signal);
                _awaited[questId] = signal;

                if (!QuestCompletion.IsEmitterLive(cond.NormalizedKind))
                    FlowTrace.Warn("Quest",
                        $"stage '{questId}/{stage?.StageId}' awaits '{signal}' but NOTHING raises that id yet " +
                        "(kind reserved for Silo E / WO-827) -- the stage is armed but unreachable.");
                else
                    FlowTrace.Step("Quest",
                        $"armed '{questId}' stage '{stage?.StageId}' -> awaiting '{signal}' " +
                        $"(latch cleared, {cond.RequiredCount} firing(s) needed).");
            }
        }

        // -- Bus matching ------------------------------------------------------

        private void OnSignalRaised(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return;
            if (_dispatching) { _pending.Add(signalId); return; }

            _dispatching = true;
            try
            {
                Dispatch(signalId);
                while (_pending.Count > 0)
                {
                    string next = _pending[0];
                    _pending.RemoveAt(0);
                    Dispatch(next);
                }
            }
            finally
            {
                _pending.Clear();
                _dispatching = false;
            }
        }

        private void Dispatch(string signalId)
        {
            if (_awaited.Count == 0) return;

            // Snapshot the matches before advancing -- AdvanceQuest re-enters through
            // QuestChanged and TryComplete rewrites _awaited.
            _matched.Clear();
            foreach (var kv in _awaited)
                if (string.Equals(kv.Value, signalId, StringComparison.OrdinalIgnoreCase))
                    _matched.Add(kv.Key);

            for (int i = 0; i < _matched.Count; i++) TryComplete(_matched[i], signalId);
        }

        /// <summary>
        /// Books one firing against a quest's current stage. Advances immediately when the
        /// condition needs a single firing; otherwise tallies into QuestState.Counters and
        /// advances on the last one.
        /// </summary>
        private void TryComplete(string questId, string signalId)
        {
            var svc = QuestService.Instance;
            if (svc == null) return;
            var stage = svc.GetStage(questId);
            var cond = stage?.CompleteOn;
            if (cond == null) return;

            int need = cond.RequiredCount;
            if (need <= 1) { Advance(questId, stage.StageId, signalId); return; }

            var prog = Progress;
            QuestState state = null;
            if (prog != null && prog.Active != null) prog.Active.TryGetValue(questId, out state);
            if (state == null) return;
            if (state.Counters == null) state.Counters = new Dictionary<string, int>();

            string key = cond.CounterKey(stage.StageId);
            state.Counters.TryGetValue(key, out int have);
            have++;

            if (have < need)
            {
                state.Counters[key] = have;
                GameStateService.Instance?.Save();
                FlowTrace.Step("Quest",
                    $"'{questId}' stage '{stage.StageId}' counted '{signalId}' {have}/{need}.");
                return;
            }

            // Satisfied -- drop the tally so the stage leaves no residue in the save.
            state.Counters.Remove(key);
            Advance(questId, stage.StageId, signalId);
        }

        private void Advance(string questId, string stageId, string signalId)
        {
            _awaited.Remove(questId);
            FlowTrace.Step("Quest",
                $"signal '{signalId}' completes '{questId}' stage '{stageId}' -> AdvanceQuest.");
            QuestService.Instance?.AdvanceQuest(questId);
            _rearmDue = true;
        }

        // -- The non-bus kind: "flag" ------------------------------------------

        /// <summary>
        /// Completes any active stage whose completeOn kind is "flag" once QuestService
        /// holds that flag. Polled rather than evented because the flag is written inside
        /// QuestService.SetFlag, and advancing from its QuestChanged callback would
        /// re-enter the service mid-Persist.
        /// </summary>
        private void EvaluateFlagStages()
        {
            var prog = Progress;
            if (prog == null || prog.Active == null || prog.Active.Count == 0) return;
            var svc = QuestService.Instance;
            if (svc == null) return;

            _flagReady.Clear();
            foreach (var kv in prog.Active)
            {
                var cond = svc.GetStage(kv.Key)?.CompleteOn;
                if (cond == null || cond.NormalizedKind != QuestCompletion.KindFlag) continue;
                if (string.IsNullOrEmpty(cond.TargetId)) continue;
                if (svc.HasFlag(kv.Key, cond.TargetId)) _flagReady.Add(kv.Key);
            }

            for (int i = 0; i < _flagReady.Count; i++)
            {
                string questId = _flagReady[i];
                string stageId = svc.GetStage(questId)?.StageId;
                Advance(questId, stageId, "questflag");
            }
        }
    }
}
