// =============================================================================
// DungeonRuntimeStateResetTests (EditMode) — locks WO-770.9 (fixes D11):
// the DungeonRuntimeState.OnEnable stale-read fix (DungeonRuntimeState.cs :524-545).
// -----------------------------------------------------------------------------
// A dungeon run SO persists its edits between editor play sessions. Before the fix,
// OnEnable only reset the FLAGS — so a fresh session could read the PRIOR run's
// dungeon id, current room, and read/cleared/opened progress lists in the window
// before StartRun overwrites them (a UI panel that reads on enable saw ghost data).
//
// This test proves the actual reset behaviour: start a run, populate the identity +
// all five progress lists + the encounter handoff, then re-fire OnEnable (private,
// via reflection — the domain-reload / SO-load hook) and assert EVERYTHING that
// bounds the stale-read window is cleared, both the flags AND the identity/lists.
// =============================================================================

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Dungeons;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class DungeonRuntimeStateResetTests
    {
        private static void InvokeOnEnable(DungeonRuntimeState state)
        {
            var mi = typeof(DungeonRuntimeState).GetMethod(
                "OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(mi, Is.Not.Null,
                "DungeonRuntimeState.OnEnable() must exist — it is the stale-run reset hook");
            mi.Invoke(state, null);
        }

        [Test]
        public void on_enable_clears_run_identity_and_all_progress_lists()
        {
            var state = ScriptableObject.CreateInstance<DungeonRuntimeState>();
            try
            {
                // Populate a live run: identity, all five progress lists, boss + handoff.
                state.StartRun("healers-cottage", "entry", new Vector3(1f, 0f, 2f), 42);
                state.ReadLoreStone("lore-1", 4);
                state.ReachCheckpoint("shrine-1");
                state.OpenChest("chest-1");
                state.MarkSecretRoomFound("secret-1");
                state.RegisterScriptedEncounter("scripted-1");
                state.BeginEncounterHandoff("enc-1", true, new Vector3(3f, 0f, 4f));
                state.MarkBossDefeated();
                state.SetHeroVitals(50f, 100f, 20f, 40f);

                // Pre-conditions: the run really is populated (so the reset means something).
                Assert.That(state.DungeonId, Is.EqualTo("healers-cottage"));
                Assert.That(state.CurrentRoomId, Is.EqualTo("entry"));
                Assert.That(state.LoreStonesRead, Is.Not.Empty);
                Assert.That(state.CheckpointsReached, Is.Not.Empty);
                Assert.That(state.ChestsOpened, Is.Not.Empty);
                Assert.That(state.SecretRoomsFound, Is.Not.Empty);
                Assert.That(state.HasFiredScriptedEncounter("scripted-1"), Is.True);
                Assert.That(state.HasPendingEncounter, Is.True);
                Assert.That(state.BossDefeated, Is.True);

                // Simulate a fresh session (domain reload / SO load re-fires OnEnable).
                InvokeOnEnable(state);

                // WO-770.9: identity + all five lists cleared (the stale-read window closed).
                Assert.That(state.DungeonId, Is.Empty, "dungeon id must reset (D11 stale-read)");
                Assert.That(state.CurrentRoomId, Is.Empty, "current room must reset (D11 stale-read)");
                Assert.That(state.LoreStonesRead, Is.Empty, "lore-stones list must clear");
                Assert.That(state.CheckpointsReached, Is.Empty, "checkpoints list must clear");
                Assert.That(state.ChestsOpened, Is.Empty, "chests list must clear");
                Assert.That(state.SecretRoomsFound, Is.Empty, "secret-rooms list must clear");
                Assert.That(state.HasFiredScriptedEncounter("scripted-1"), Is.False,
                    "scripted-encounters list must clear");

                // ...and the flags/handoff reset too (the original OnEnable contract).
                Assert.That(state.RunActive, Is.False, "run must not be active after reset");
                Assert.That(state.InCombat, Is.False, "combat lock must clear");
                Assert.That(state.BossDefeated, Is.False, "boss-defeated flag must clear");
                Assert.That(state.HasPendingEncounter, Is.False, "pending encounter must clear");
                Assert.That(state.HasHeroVitals, Is.False, "hero vitals must reset to unset");
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }
    }
}
