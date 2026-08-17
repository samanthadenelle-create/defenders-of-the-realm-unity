// =============================================================================
// HudActionBarModelTests (EditMode) — WO-835 applicability model invariants.
// -----------------------------------------------------------------------------
// Locks the action-bar predicate logic MOVED out of HudKitController.Update()
// into the pure Core HudActionBarModel (owner architecture law 2026-08-02).
// FAKE ISource — no scene, no PostureSignals, no GameStateService, no
// RaidEntryGate. Asserts the EXACT ordered output array per signal combination
// (the WO-835 §3b table) + the edge-triggered event contract.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DeNelle.Core.HudModel;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class HudActionBarModelTests
    {
        private sealed class FakeSource : HudActionBarModel.ISource
        {
            public bool Talk;
            public bool Capable;
            public bool ArmyReady = true;
            public bool Onboarded = true;
            public bool Focused;
            // WO-1008 dim-REASON inputs (never visibility inputs). Default = a partly filled
            // army so the legacy "capable but not full" cases keep meaning ArmyNotFull.
            public int Deployable = 3;
            public int Queued;
            public int Cap = 5;

            public bool TalkAvailable => Talk;
            public bool RaidCapable => Capable;
            public bool RaidArmyReady => ArmyReady;
            public int RaidDeployableSlots => Deployable;
            public int RaidQueuedSlots => Queued;
            public int RaidCapSlots => Cap;
            public bool MapUnlocked => Onboarded;
            public bool BuildingFocused => Focused;
        }

        private static (HudActionBarModel model, FakeSource src) TownModel()
        {
            var src = new FakeSource();
            var model = new HudActionBarModel(src);
            model.SetPosture(HudActionBarModel.PostureTown);
            return (model, src);
        }

        private static ActionBarButtonId[] Ids(HudActionBarModel m) => m.Active.ToArray();

        // ── the WO-835 acceptance baseline: no NPC, no raids, onboarded ──────

        // ⚠ WO-911 (owner ruling Q10+Q13, 2026-08-06): the baseline set CHANGED.
        // Map left the bar for a tab inside Bag, and the Upgrade face was re-pointed to the
        // unified Manage/Queues screen — which makes it always-applicable in town rather than
        // context-gated. Bar: 7 -> 6 faces. These expectations are UPDATED, not relaxed.
        [Test]
        public void town_baseline_shows_build_bag_quests_manage_packed()
        {
            var (model, _) = TownModel();
            Assert.AreEqual(
                new[] { ActionBarButtonId.Build, ActionBarButtonId.Bag, ActionBarButtonId.Quests,
                        ActionBarButtonId.Upgrade },
                Ids(model),
                "in town with no NPC near and no raid capability, the bar is exactly Build/Bag/Quests/Manage " +
                "(WO-911: Map moved into Bag; Upgrade re-pointed to Manage and is always applicable)");
        }

        [Test]
        public void talk_packs_in_when_npc_in_range_and_out_when_gone()
        {
            var (model, src) = TownModel();
            src.Talk = true;
            model.Tick();
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Talk, "Talk must appear when an NPC is in range");
            Assert.AreEqual(1, System.Array.IndexOf(Ids(model), ActionBarButtonId.Talk),
                "Talk keeps its canonical slot right after Build (ordered array)");

            src.Talk = false;
            model.Tick();
            CollectionAssert.DoesNotContain(Ids(model), ActionBarButtonId.Talk,
                "Talk must repack OUT (hide, not dim) when the NPC leaves range");
        }

        // ── Raids: hide when not capable; dim (visible) when capable but army not full ──

        [Test]
        public void raids_absent_when_not_capable()
        {
            var (model, src) = TownModel();
            src.Capable = false;
            src.ArmyReady = false;
            model.Tick();
            CollectionAssert.DoesNotContain(Ids(model), ActionBarButtonId.Raids,
                "no barracks / raid flag off => Raids is ABSENT, not dimmed (WO-1008: an empty " +
                "ARMY is no longer a hide reason, but a missing building still is)");
            Assert.IsFalse(model.RaidsDimmed, "an absent Raids face must not report dimmed");
            Assert.AreEqual(HudActionBarModel.RaidDimReason.None, model.RaidsDimReason,
                "an absent face carries no dim reason");
        }

        // ── WO-1008: the two dim reasons are DISTINCT, and both speak in words ──

        [Test]
        public void barracks_with_zero_troops_dims_with_a_no_troops_reason_not_a_hidden_face()
        {
            var (model, src) = TownModel();
            src.Capable = true;        // barracks built + flag on
            src.ArmyReady = false;
            src.Deployable = 0; src.Queued = 0; src.Cap = 5;
            model.Tick();

            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Raids,
                "a built Barracks with ZERO troops must show a greyed Raids face, never hide it " +
                "(owner 2026-08-16: 'I do not see a way to start a raid')");
            Assert.IsTrue(model.RaidsDimmed, "zero troops greys the face");
            Assert.AreEqual(HudActionBarModel.RaidDimReason.NoTroops, model.RaidsDimReason);
            Assert.AreNotEqual(HudActionBarModel.RaidsBaseLabel, model.RaidsFaceLabel,
                "the greyed face must differ from the live face in TEXT, not hue alone " +
                "(the owner is red/green colourblind)");
            StringAssert.Contains("Barracks", model.RaidsDimMessage,
                "the no-troops reason must name the fix");
        }

        [Test]
        public void the_two_dim_reasons_do_not_share_copy()
        {
            var (model, src) = TownModel();
            src.Capable = true; src.ArmyReady = false;
            src.Deployable = 0; src.Queued = 0; src.Cap = 5;
            model.Tick();
            string noTroopsLabel = model.RaidsFaceLabel;
            string noTroopsMsg = model.RaidsDimMessage;

            src.Deployable = 3;
            model.Tick();
            Assert.AreEqual(HudActionBarModel.RaidDimReason.ArmyNotFull, model.RaidsDimReason,
                "some troops but under cap is the WO-820 full-army gate, a different reason");
            Assert.AreNotEqual(noTroopsLabel, model.RaidsFaceLabel,
                "a single generic grey tells the player nothing - the face text must differ");
            Assert.AreNotEqual(noTroopsMsg, model.RaidsDimMessage,
                "the two dim reasons must not share their message");
        }

        [Test]
        public void raids_present_and_dimmed_when_capable_but_army_not_full()
        {
            var (model, src) = TownModel();
            src.Capable = true;
            src.ArmyReady = false;
            model.Tick();
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Raids,
                "capable (building + >=1 troop) => Raids visible even when the army is not full");
            Assert.IsTrue(model.RaidsDimmed,
                "WO-820 semantics preserved: capable but not-full army DIMS the visible face");

            src.ArmyReady = true;
            model.Tick();
            Assert.IsFalse(model.RaidsDimmed, "full army restores the face");
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Raids);
        }

        // ── Map: OFF THE BAR ENTIRELY (WO-911 ruling Q10+Q13) ────────────────

        [Test]
        public void map_never_packs_into_the_bar_in_either_onboarded_state()
        {
            var (model, src) = TownModel();
            src.Onboarded = false;
            model.Tick();
            CollectionAssert.DoesNotContain(Ids(model), ActionBarButtonId.Map, "pre-onboard => no Map face");

            src.Onboarded = true;
            model.Tick();
            CollectionAssert.DoesNotContain(Ids(model), ActionBarButtonId.Map,
                "WO-911: Map moved INTO Bag as a (feature-flagged) tab. It must never return to the bar — " +
                "that move is half of how the bar went 7 -> 6 faces without needing an 8th slot. " +
                "ActionBarButtonId.Map stays DORMANT at ordinal 4 so the other faces keep their indices.");
        }

        // ── The re-pointed Manage face: always in town, focus or not ─────────

        [Test]
        public void manage_face_is_always_in_town_regardless_of_building_focus()
        {
            var (model, src) = TownModel();
            src.Focused = false;
            model.Tick();
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Upgrade,
                "WO-911: the Upgrade face is RE-POINTED to the Manage/Queues screen and is the single door to " +
                "all three production lines. Gating it on a focused building is precisely the undiscoverability " +
                "the work order exists to remove.");
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Quests);

            src.Focused = true;
            model.Tick();
            var ids = Ids(model);
            CollectionAssert.Contains(ids, ActionBarButtonId.Upgrade, "focus must not remove the Manage face");
            CollectionAssert.Contains(ids, ActionBarButtonId.Quests,
                "Quests STAYS while a building is focused (owner: 'quests active more often')");
        }

        [Test]
        public void all_signals_on_yields_the_full_six_in_canonical_order()
        {
            var (model, src) = TownModel();
            src.Talk = true; src.Capable = true; src.Onboarded = true; src.Focused = true;
            model.Tick();
            Assert.AreEqual(
                new[]
                {
                    ActionBarButtonId.Build, ActionBarButtonId.Talk, ActionBarButtonId.Bag,
                    ActionBarButtonId.Raids, ActionBarButtonId.Quests, ActionBarButtonId.Upgrade,
                },
                Ids(model),
                "the 6-face MAX renders in enum order regardless of activation sequence (WO-911)");
            Assert.LessOrEqual(model.Active.Count, HudActionBarModel.MaxVisibleFaces,
                "the View sizes a bar slot from MaxVisibleFaces; more faces than that would overflow the zone");
        }

        // ── posture layer: explore subset; non-calm postures drop the bar ────

        [Test]
        public void explore_shows_only_bag_plus_talk_when_available()
        {
            var src = new FakeSource { Onboarded = true, Capable = true, Focused = true };
            var model = new HudActionBarModel(src);
            model.SetPosture(HudActionBarModel.PostureExplore);
            Assert.AreEqual(new[] { ActionBarButtonId.Bag }, Ids(model),
                "explore ignores town-only faces even with their signals on (occupancy row parity)");

            src.Talk = true;
            model.Tick();
            Assert.AreEqual(new[] { ActionBarButtonId.Talk, ActionBarButtonId.Bag }, Ids(model));
        }

        [Test]
        public void non_calm_postures_empty_the_bar()
        {
            var (model, _) = TownModel();
            Assert.IsTrue(model.Active.Count > 0);
            model.SetPosture("build");
            Assert.AreEqual(0, model.Active.Count, "build posture => empty set (occupancy drops the bar)");
            model.SetPosture("hostile(activebattle)");
            Assert.AreEqual(0, model.Active.Count);
        }

        // ── event contract: edge-triggered, never per-tick ───────────────────

        [Test]
        public void active_buttons_changed_fires_only_on_a_real_set_change()
        {
            var (model, src) = TownModel();
            int events = 0;
            model.ActiveButtonsChanged += () => events++;

            model.Tick();
            model.Tick();
            Assert.AreEqual(0, events, "no input change => no event (the View must never relayout per-frame)");

            src.Talk = true;
            model.Tick();
            Assert.AreEqual(1, events, "one set change => exactly one event");

            model.Tick();
            Assert.AreEqual(1, events, "steady state stays quiet");
        }

        [Test]
        public void raids_dim_event_is_edge_triggered_and_separate_from_the_set()
        {
            var (model, src) = TownModel();
            src.Capable = true;
            src.ArmyReady = true;
            model.Tick();

            int setEvents = 0, dimEvents = 0;
            model.ActiveButtonsChanged += () => setEvents++;
            model.RaidsDimmedChanged += () => dimEvents++;

            src.ArmyReady = false;
            model.Tick();
            Assert.AreEqual(0, setEvents, "a dim flip alone must not repack the bar");
            Assert.AreEqual(1, dimEvents);

            model.Tick();
            Assert.AreEqual(1, dimEvents, "dim event is edge-triggered");
        }
    }
}
