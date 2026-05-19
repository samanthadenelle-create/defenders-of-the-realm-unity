// =============================================================================
// ATBRuntimeState — runtime-only ScriptableObject wrapping the ATB engine
// -----------------------------------------------------------------------------
// C# port of src/store/atbStore.ts. A non-persisted ScriptableObject that
// mirrors the Zustand `useAtbStore`: it holds the latest immutable battle
// snapshot the pure engine produced and exposes typed actions to drive the
// fight. NO combat logic lives here — every mutation routes through the
// DeNelle.BattleATB.Engine static classes (Turn, BattleStateOps).
//
// Zustand parallel (v2 port-spec Part 3 "ScriptableObject + UnityEvent for
// state"): the ScriptableObject holds the data; UnityEvents fire on mutation;
// UI elements subscribe in OnEnable, unsubscribe in OnDisable.
//
// Lifecycle (matches atbStore.ts):
//   StartBattle(setup, source) → builds a BattleState, runs the intro, and
//                                auto-advances to the first hero turn / AI turn.
//   ChooseAction(action)       → submits the hero's action, then auto-resolves
//                                every following AI turn until the next hero
//                                turn or battle end (when AutoStepAi is true).
//   StepAi()                   → resolves a single AI turn (paced UIs).
//   AutoResolve()              → runs the whole fight with the hero on autopilot.
//   EndBattle()                → tear-down; keeps Result so a host can read it.
//
// This SO deliberately does NOT touch GameState — the caller (the breach
// trigger / dungeon handoff) reads Result and applies Heart / building damage.
//
// Runtime-only: this asset is created fresh per session; nothing here is
// serialised to PlayerPrefs. It carries [CreateAssetMenu] only so a designer
// can drop a shared instance into the BattleController in the inspector.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using DeNelle.BattleATB.Engine;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.BattleATB.State
{
    /// <summary>Where a battle was launched from. Mirrors atbStore `BattleSource`.</summary>
    public enum BattleSource
    {
        /// <summary>Village breach — Heart / building damage commits on close.</summary>
        Village,

        /// <summary>Dungeon encounter — NO village damage on close.</summary>
        Dungeon,
    }

    /// <summary>
    /// The settled result of a battle, kept after the live snapshot is cleared.
    /// Mirrors atbStore `AtbBattleResult`. <see cref="Outcome"/> is never
    /// <see cref="BattleOutcome.None"/> once a battle has actually ended.
    /// </summary>
    public sealed class AtbBattleResult
    {
        public BattleOutcome Outcome;
        public BattleSource Source;
    }

    /// <summary>A UnityEvent carrying the live battle snapshot.</summary>
    [System.Serializable]
    public sealed class BattleStateEvent : UnityEvent<BattleState> { }

    /// <summary>A UnityEvent carrying a settled battle result.</summary>
    [System.Serializable]
    public sealed class BattleResultEvent : UnityEvent<AtbBattleResult> { }

    /// <summary>
    /// Runtime-only ScriptableObject mirror of <c>src/store/atbStore.ts</c>.
    /// Holds the live battle snapshot and raises UnityEvents on mutation.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ATBRuntimeState",
        menuName = "DeNelle/Battle ATB/Runtime State",
        order = 0)]
    public sealed class ATBRuntimeState : ScriptableObject
    {
        // ---------------------------------------------------------------------
        // Post-battle full reset (atbStore COTT-5)
        // ---------------------------------------------------------------------

        /// <summary>
        /// v1 simplification — after EVERY ATB battle resolves, player-side
        /// combatants' HP and MP are fully restored. Mirrors atbStore
        /// <c>FULL_RESET_AFTER_FIGHT</c>. Flip to false when checkpoints make
        /// resting load-bearing.
        /// </summary>
        public const bool FullResetAfterFight = true;

        /// <summary>Loop guard for the AI auto-resolution drain (atbStore `limit`).</summary>
        private const int AiDrainLimit = 2000;

        /// <summary>Loop guard for the full auto-resolve path (atbStore `guard`).</summary>
        private const int AutoResolveGuard = 20000;

        // ---------------------------------------------------------------------
        // UnityEvents — the "subscribe" half of the Zustand parallel
        // ---------------------------------------------------------------------

        [Header("Events — UI subscribes in OnEnable, unsubscribes in OnDisable")]

        [Tooltip("Fires after the player submits a hero action (before AI drain).")]
        public BattleStateEvent OnActionSubmitted = new BattleStateEvent();

        [Tooltip("Fires after a turn fully resolves and the snapshot updates.")]
        public BattleStateEvent OnTurnResolved = new BattleStateEvent();

        [Tooltip("Fires once the battle reaches a final victory/defeat outcome.")]
        public BattleResultEvent OnOutcome = new BattleResultEvent();

        [Tooltip("Fires whenever the live battle snapshot is replaced (any cause).")]
        public BattleStateEvent OnBattleChanged = new BattleStateEvent();

        // ---------------------------------------------------------------------
        // State — the live snapshot + bookkeeping
        // ---------------------------------------------------------------------

        /// <summary>The current battle snapshot, or null when no battle is active.</summary>
        public BattleState Battle { get; private set; }

        /// <summary>Where the active (or most recent) battle was launched from.</summary>
        public BattleSource Source { get; private set; } = BattleSource.Village;

        /// <summary>
        /// The settled outcome of the most recent battle, surviving
        /// <see cref="EndBattle"/> so a host can read it on close. Cleared when a
        /// new battle begins.
        /// </summary>
        public AtbBattleResult Result { get; private set; }

        /// <summary>True while the engine is mid auto-resolution of AI turns.</summary>
        public bool Resolving { get; private set; }

        /// <summary>
        /// When false, AI turns wait for an explicit <see cref="StepAi"/> call so a
        /// UI can animate them one at a time. True (default): <see cref="ChooseAction"/>
        /// resolves all following AI turns in one go.
        /// </summary>
        public bool AutoStepAi { get; private set; } = true;

        // ---------------------------------------------------------------------
        // Derived selectors — cheap helpers, safe to call in render
        // ---------------------------------------------------------------------

        /// <summary>The unit whose turn it is, or null.</summary>
        public BattleUnit ActiveUnit()
        {
            if (Battle == null || string.IsNullOrEmpty(Battle.ActiveUnitId)) return null;
            return BattleStateOps.GetUnit(Battle, Battle.ActiveUnitId);
        }

        /// <summary>True when the engine is waiting for the player to choose an action.</summary>
        public bool IsAwaitingPlayer()
        {
            return Battle != null && Battle.Phase == BattlePhase.AwaitingInput;
        }

        /// <summary>True once the battle has a final outcome.</summary>
        public bool IsOver()
        {
            return Battle != null && Battle.Phase == BattlePhase.Ended;
        }

        /// <summary>The battle outcome, or <see cref="BattleOutcome.None"/> while in progress.</summary>
        public BattleOutcome Outcome()
        {
            return Battle != null ? Battle.Outcome : BattleOutcome.None;
        }

        /// <summary>Living party units (hero + pets).</summary>
        public List<BattleUnit> Party()
        {
            if (Battle == null) return new List<BattleUnit>();
            return Battle.Units.Where(u => u.Side == Side.Party && u.Alive).ToList();
        }

        /// <summary>Living enemy units.</summary>
        public List<BattleUnit> Enemies()
        {
            if (Battle == null) return new List<BattleUnit>();
            return Battle.Units.Where(u => u.Side == Side.Enemy && u.Alive).ToList();
        }

        // ---------------------------------------------------------------------
        // Actions — the contract the UI / BattleController consumes
        // ---------------------------------------------------------------------

        /// <summary>
        /// Build and start a battle from a setup, tagging it with its
        /// <paramref name="source"/>. Runs the intro and advances to the first
        /// decision point. Replaces any battle already in progress. Mirrors
        /// atbStore <c>startBattle</c>.
        /// </summary>
        public void StartBattle(BattleSetup setup, BattleSource source = BattleSource.Village)
        {
            Source = source;

            // CreateBattle + StartBattle each return a fresh object the engine
            // owns; we may auto-resolve the opening AI turns before handing over.
            BattleState state = Turn.StartBattle(BattleStateOps.CreateBattle(setup));
            if (AutoStepAi)
            {
                state = RunAiUntilPlayer(state);
            }

            // Clone at the boundary so this SO owns an isolated snapshot the
            // engine can never mutate again — external readers get a stable ref.
            state = BattleStateOps.CloneBattle(state);
            state = ApplyFullResetAfterFight(state);

            Battle = state;
            Resolving = false;
            // A new battle clears any settled result from the previous one.
            Result = DeriveResult(state, null);

            OnBattleChanged.Invoke(Battle);
            OnTurnResolved.Invoke(Battle);
            if (Result != null) OnOutcome.Invoke(Result);
        }

        /// <summary>
        /// Submit the hero's chosen action. Only valid while
        /// <see cref="IsAwaitingPlayer"/> is true; ignored otherwise. After the
        /// hero acts, AI turns auto-resolve up to the next hero turn (or battle
        /// end) when <see cref="AutoStepAi"/> is true. Mirrors atbStore
        /// <c>chooseAction</c>.
        /// </summary>
        public void ChooseAction(BattleAction action)
        {
            if (Battle == null || Battle.Phase != BattlePhase.AwaitingInput) return;

            Resolving = true;
            BattleState next = Turn.SubmitAction(Battle, action);

            // OnActionSubmitted fires on the post-submit state, before the AI
            // drain — gives the UI a chance to react to the hero's move alone.
            OnActionSubmitted.Invoke(next);

            if (AutoStepAi)
            {
                next = RunAiUntilPlayer(next);
            }

            // Clone at the boundary — the engine returned (and mutated) the same
            // reference it was handed; this SO must own an isolated copy.
            next = BattleStateOps.CloneBattle(next);
            next = ApplyFullResetAfterFight(next);

            Battle = next;
            Result = DeriveResult(next, Result);
            Resolving = false;

            OnBattleChanged.Invoke(Battle);
            OnTurnResolved.Invoke(Battle);
            if (Battle.Phase == BattlePhase.Ended && Result != null) OnOutcome.Invoke(Result);
        }

        /// <summary>
        /// Resolve exactly one pending AI turn. Use this when
        /// <see cref="AutoStepAi"/> is false to pace enemy/pet turns against
        /// animations. No-op when it's the hero's turn or the battle is over.
        /// Mirrors atbStore <c>stepAi</c>.
        /// </summary>
        public void StepAi()
        {
            if (Battle == null || Battle.Phase != BattlePhase.Resolving) return;

            BattleState next = BattleStateOps.CloneBattle(Turn.ResolveAiTurn(Battle));
            next = ApplyFullResetAfterFight(next);

            Battle = next;
            Result = DeriveResult(next, Result);

            OnBattleChanged.Invoke(Battle);
            OnTurnResolved.Invoke(Battle);
            if (Battle.Phase == BattlePhase.Ended && Result != null) OnOutcome.Invoke(Result);
        }

        /// <summary>
        /// Run the entire remaining battle to completion with the hero on
        /// autopilot. Mirrors atbStore <c>autoResolve</c>.
        /// </summary>
        public void AutoResolve()
        {
            if (Battle == null || Battle.Phase == BattlePhase.Ended) return;

            Resolving = true;
            BattleState s = Battle;
            int guard = 0;
            while (s.Phase != BattlePhase.Ended && guard < AutoResolveGuard)
            {
                guard += 1;
                if (s.Phase == BattlePhase.AwaitingInput && !string.IsNullOrEmpty(s.ActiveUnitId))
                {
                    BattleUnit hero = BattleStateOps.GetUnit(s, s.ActiveUnitId);
                    if (hero == null) break;
                    s = Turn.SubmitAction(s, Turn.AutoHeroAction(s, hero));
                }
                else if (s.Phase == BattlePhase.Resolving)
                {
                    s = Turn.ResolveAiTurn(s);
                }
                else
                {
                    s = Turn.BeginNextTurn(s);
                }
            }

            // Clone at the boundary — the loop mutated the engine state in place.
            s = BattleStateOps.CloneBattle(s);
            s = ApplyFullResetAfterFight(s);

            Battle = s;
            Result = DeriveResult(s, Result);
            Resolving = false;

            OnBattleChanged.Invoke(Battle);
            OnTurnResolved.Invoke(Battle);
            if (Battle.Phase == BattlePhase.Ended && Result != null) OnOutcome.Invoke(Result);
        }

        /// <summary>Toggle whether AI turns auto-resolve after the hero acts.</summary>
        public void SetAutoStepAi(bool on)
        {
            AutoStepAi = on;
        }

        /// <summary>
        /// Clear the live snapshot. KEEPS <see cref="Result"/> so a host can read
        /// the outcome on close; <see cref="Result"/> resets when the next battle
        /// begins. <see cref="Source"/> is left as-is for the same reason.
        /// Mirrors atbStore <c>endBattle</c>.
        /// </summary>
        public void EndBattle()
        {
            Battle = null;
            Resolving = false;
            OnBattleChanged.Invoke(null);
        }

        // ---------------------------------------------------------------------
        // Internal — AI drain + result derivation (atbStore private helpers)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolve AI turns until the engine waits on the player again, the battle
        /// ends, or the loop guard trips. Pure — operates on the engine snapshot
        /// directly. Mirrors atbStore <c>runAiUntilPlayer</c>.
        /// </summary>
        private static BattleState RunAiUntilPlayer(BattleState state)
        {
            BattleState s = state;
            int guard = 0;
            while (s.Phase == BattlePhase.Resolving && guard < AiDrainLimit)
            {
                guard += 1;
                s = Turn.ResolveAiTurn(s);
            }
            // If the engine somehow parked in 'filling', nudge it forward once.
            if (s.Phase == BattlePhase.Filling) s = Turn.BeginNextTurn(s);
            return s;
        }

        /// <summary>
        /// Derive the settled result from a battle state. Returns
        /// <paramref name="prev"/> unchanged while the battle is still in
        /// progress, and a fresh <see cref="AtbBattleResult"/> once the engine
        /// reports <see cref="BattlePhase.Ended"/>. Mirrors atbStore <c>deriveResult</c>.
        /// </summary>
        private AtbBattleResult DeriveResult(BattleState state, AtbBattleResult prev)
        {
            if (state.Phase == BattlePhase.Ended && state.Outcome != BattleOutcome.None)
            {
                return new AtbBattleResult { Outcome = state.Outcome, Source = Source };
            }
            return prev;
        }

        /// <summary>
        /// If <see cref="FullResetAfterFight"/> is on and <paramref name="state"/>
        /// is an ENDED battle, restore every player-side combatant to full HP/MP.
        /// Operates on the SO-owned clone, so mutating units in place is safe.
        /// Mirrors atbStore <c>applyFullResetAfterFight</c>.
        /// </summary>
        private static BattleState ApplyFullResetAfterFight(BattleState state)
        {
            if (!FullResetAfterFight || state.Phase != BattlePhase.Ended) return state;
            foreach (BattleUnit unit in state.Units)
            {
                if (unit.Side != Side.Party) continue;
                unit.Hp = unit.MaxHp;
                unit.Resource = unit.MaxResource;
            }
            return state;
        }

        // ---------------------------------------------------------------------
        // Lifecycle — runtime-only guarantee
        // ---------------------------------------------------------------------

        /// <summary>
        /// Reset transient state when the asset is enabled. Guarantees the SO is
        /// never carrying a stale snapshot across a play-mode session, since it
        /// is not persisted.
        /// </summary>
        private void OnEnable()
        {
            Battle = null;
            Result = null;
            Resolving = false;
            Source = BattleSource.Village;
        }
    }
}
