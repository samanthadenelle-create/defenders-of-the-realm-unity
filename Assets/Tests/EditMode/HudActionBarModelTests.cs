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

            public bool TalkAvailable => Talk;
            public bool RaidCapable => Capable;
            public bool RaidArmyReady => ArmyReady;
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

        [Test]
        public void town_baseline_shows_build_bag_map_quests_packed()
        {
            var (model, _) = TownModel();
            Assert.AreEqual(
                new[] { ActionBarButtonId.Build, ActionBarButtonId.Bag, ActionBarButtonId.Map, ActionBarButtonId.Quests },
                Ids(model),
                "in town with no NPC near, no raid capability and no focus, the bar is exactly Build/Bag/Map/Quests");
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
                "no building / no troops => Raids is ABSENT (WO-835 §3d owner default), not dimmed");
            Assert.IsFalse(model.RaidsDimmed, "an absent Raids face must not report dimmed");
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

        // ── Map: Onboarded gate (WO-825 R4) — absent means SHORTER array, never a hole ──

        [Test]
        public void map_absent_pre_onboarding_and_array_stays_contiguous()
        {
            var (model, src) = TownModel();
            src.Onboarded = false;
            model.Tick();
            var ids = Ids(model);
            CollectionAssert.DoesNotContain(ids, ActionBarButtonId.Map, "pre-onboard => no Map face");
            Assert.AreEqual(
                new[] { ActionBarButtonId.Build, ActionBarButtonId.Bag, ActionBarButtonId.Quests },
                ids,
                "the array simply shrinks — the View packs it, so a hole is impossible by construction");

            src.Onboarded = true;
            model.Tick();
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Map, "Onboarded => Map packs in");
        }

        // ── §3c split: Upgrade is its own face; Quests is never replaced ─────

        [Test]
        public void focused_building_adds_upgrade_and_keeps_quests()
        {
            var (model, src) = TownModel();
            src.Focused = true;
            model.Tick();
            var ids = Ids(model);
            CollectionAssert.Contains(ids, ActionBarButtonId.Upgrade, "focus => Upgrade packs in");
            CollectionAssert.Contains(ids, ActionBarButtonId.Quests,
                "Quests STAYS while a building is focused (owner: 'quests active more often')");

            src.Focused = false;
            model.Tick();
            CollectionAssert.DoesNotContain(Ids(model), ActionBarButtonId.Upgrade, "focus lost => Upgrade packs out");
            CollectionAssert.Contains(Ids(model), ActionBarButtonId.Quests);
        }

        [Test]
        public void all_signals_on_yields_the_full_seven_in_canonical_order()
        {
            var (model, src) = TownModel();
            src.Talk = true; src.Capable = true; src.Onboarded = true; src.Focused = true;
            model.Tick();
            Assert.AreEqual(
                new[]
                {
                    ActionBarButtonId.Build, ActionBarButtonId.Talk, ActionBarButtonId.Bag,
                    ActionBarButtonId.Raids, ActionBarButtonId.Map, ActionBarButtonId.Quests,
                    ActionBarButtonId.Upgrade,
                },
                Ids(model),
                "the 7-face MAX renders in enum order regardless of activation sequence");
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
