// =============================================================================
// DungeonSettleTests (EditMode) — locks the WO-770.3 defeat contract (fixes D4):
// a LOST dungeon ATB fight must NOT be scored as a win.
// -----------------------------------------------------------------------------
// The bug was DungeonController.ResolvePendingEncounter hardcoding `bool victory =
// true`, so a defeated boss fight still credited the boss + unlocked the exit. The
// fix routes the real BattleResultKind carrier into DungeonRuntimeState.
// ResumeAfterEncounter(victory) (DungeonRuntimeState.cs :377-391), whose contract
// is: credit the boss ONLY on a boss-encounter victory.
//
// These tests assert that logical contract directly on the public API:
//   - BattleResultKind is the 3-value carrier (None/Victory/Defeat),
//   - a DEFEAT resume (victory=false) on a boss encounter leaves BossDefeated FALSE
//     (the D4 regression: a loss no longer credits the boss),
//   - a VICTORY resume on a boss encounter sets BossDefeated TRUE,
//   - a VICTORY resume on a NON-boss encounter does NOT set BossDefeated,
//   - resume with no pending encounter is a no-op (returns false),
//   - either resume clears the combat lock + the pending handoff.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Dungeons;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class DungeonSettleTests
    {
        private static DungeonRuntimeState NewRun()
        {
            var state = ScriptableObject.CreateInstance<DungeonRuntimeState>();
            state.StartRun("healers-cottage", "entry", Vector3.zero, 1);
            return state;
        }

        [Test]
        public void battle_result_kind_carrier_has_three_outcomes()
        {
            // The Core-level carrier that replaced the hardcoded `victory = true`.
            Assert.That((int)BattleResultKind.None, Is.EqualTo(0), "None is the default (no result yet)");
            Assert.That(BattleResultKind.Victory, Is.Not.EqualTo(BattleResultKind.Defeat),
                "Victory and Defeat must be distinct outcomes");
            Assert.That(System.Enum.IsDefined(typeof(BattleResultKind), BattleResultKind.Defeat), Is.True,
                "Defeat must be a real carrier value (the whole point of WO-770.3)");
        }

        [Test]
        public void boss_defeat_does_not_credit_the_boss()
        {
            var state = NewRun();
            try
            {
                state.BeginEncounterHandoff("boss-enc", isBoss: true, resumePosition: Vector3.zero);
                Assert.That(state.HasPendingEncounter, Is.True);

                bool resumed = state.ResumeAfterEncounter(victory: false);

                Assert.That(resumed, Is.True, "a pending encounter was resumed");
                Assert.That(state.BossDefeated, Is.False,
                    "a LOST boss fight must NOT credit the boss (D4: no false win)");
                Assert.That(state.HasPendingEncounter, Is.False, "the handoff must clear on resume");
                Assert.That(state.InCombat, Is.False, "the combat lock must clear on resume");
            }
            finally { Object.DestroyImmediate(state); }
        }

        [Test]
        public void boss_victory_credits_the_boss()
        {
            var state = NewRun();
            try
            {
                state.BeginEncounterHandoff("boss-enc", isBoss: true, resumePosition: Vector3.zero);

                bool resumed = state.ResumeAfterEncounter(victory: true);

                Assert.That(resumed, Is.True);
                Assert.That(state.BossDefeated, Is.True,
                    "a WON boss fight credits the boss + unlocks the Apothecary exit");
                Assert.That(state.HasPendingEncounter, Is.False);
                Assert.That(state.InCombat, Is.False);
            }
            finally { Object.DestroyImmediate(state); }
        }

        [Test]
        public void non_boss_victory_does_not_credit_the_boss()
        {
            var state = NewRun();
            try
            {
                state.BeginEncounterHandoff("random-enc", isBoss: false, resumePosition: Vector3.zero);

                bool resumed = state.ResumeAfterEncounter(victory: true);

                Assert.That(resumed, Is.True);
                Assert.That(state.BossDefeated, Is.False,
                    "winning a NON-boss encounter must not unlock the boss exit");
            }
            finally { Object.DestroyImmediate(state); }
        }

        [Test]
        public void resume_with_no_pending_encounter_is_a_noop()
        {
            var state = NewRun();
            try
            {
                Assert.That(state.HasPendingEncounter, Is.False, "no handoff was begun");
                Assert.That(state.ResumeAfterEncounter(false), Is.False,
                    "resume must be a no-op (return false) with nothing pending");
                Assert.That(state.ResumeAfterEncounter(true), Is.False);
                Assert.That(state.BossDefeated, Is.False);
            }
            finally { Object.DestroyImmediate(state); }
        }
    }
}
