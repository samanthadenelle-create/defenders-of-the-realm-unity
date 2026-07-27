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
        // WO-775: the seed reads the LIVE hero rig — never the retired 120/60 placeholder literals.
        private const float OldPlaceholderHp = 120f;
        private const float OldPlaceholderMana = 60f;

        private static void InvokeOnEnable(DungeonRuntimeState state)
        {
            var mi = typeof(DungeonRuntimeState).GetMethod(
                "OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(mi, Is.Not.Null,
                "DungeonRuntimeState.OnEnable() must exist — it is the stale-run reset hook");
            mi.Invoke(state, null);
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var fi = target.GetType().GetField(
                field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(fi, Is.Not.Null,
                $"expected private field '{field}' on {target.GetType().Name}");
            fi.SetValue(target, value);
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

        // WO-775 — the dungeon seed must read the LIVE hero (HeroHealth.MaxHp +
        // HeroAbilities.MaxMana), not the retired 120/60 placeholder literals. A geared
        // hero whose MaxHp folds to 155 enters the dungeon; the run-state vitals must
        // equal the hero's numbers and must NOT be 120/60.
        [Test]
        public void enter_dungeon_seeds_hero_vitals_from_live_hero_not_placeholder()
        {
            var heroGo = new GameObject("TestHero");
            var controllerGo = new GameObject("TestDungeonController");
            var state = ScriptableObject.CreateInstance<DungeonRuntimeState>();
            try
            {
                // A geared hero: MaxHp folds to 155 (base 155, no gear/talents in a bare
                // test), MaxMana 40 — both DELIBERATELY unequal to the retired 120/60.
                var heroHealth = heroGo.AddComponent<DeNelle.Village.HeroHealth>();
                var heroAbilities = heroGo.AddComponent<DeNelle.Village.HeroAbilities>();
                SetPrivateField(heroHealth, "_maxHp", 155f);
                SetPrivateField(heroHealth, "_hp", 155f);
                SetPrivateField(heroAbilities, "_maxMana", 40f);
                SetPrivateField(heroAbilities, "_mana", 25f);

                // Pre-conditions: the live rig resolves the geared numbers, and they are
                // NOT the placeholders — so the assertions below actually mean something.
                Assert.That(heroHealth.MaxHp, Is.EqualTo(155f),
                    "test hero MaxHp must fold to 155 (base 155, no gear/talents)");
                Assert.That(heroAbilities.MaxMana, Is.EqualTo(40f));
                Assert.That(heroHealth.MaxHp, Is.Not.EqualTo(OldPlaceholderHp));
                Assert.That(heroAbilities.MaxMana, Is.Not.EqualTo(OldPlaceholderMana));

                var controller = controllerGo.AddComponent<DungeonController>();
                SetPrivateField(controller, "_hero", heroGo.transform);
                SetPrivateField(controller, "_runtimeState", state);

                // Drive the WO-775 seed — the body EnterDungeon runs at its fresh-run gate.
                var mi = typeof(DungeonController).GetMethod(
                    "SeedHeroVitalsFromLiveHero", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(mi, Is.Not.Null,
                    "DungeonController.SeedHeroVitalsFromLiveHero() must exist — the WO-775 live-hero seed");
                mi.Invoke(controller, null);

                // ACCEPTANCE: the run-state vitals match the LIVE hero, and are NOT 120/60.
                Assert.That(state.HasHeroVitals, Is.True, "vitals must be seeded");
                Assert.That(state.HeroMaxHp, Is.EqualTo(heroHealth.MaxHp),
                    "seeded HeroMaxHp must equal the live hero's MaxHp");
                Assert.That(state.HeroMaxMana, Is.EqualTo(heroAbilities.MaxMana),
                    "seeded HeroMaxMana must equal the live hero's MaxMana");
                Assert.That(state.HeroMaxHp, Is.Not.EqualTo(OldPlaceholderHp),
                    "seeded HeroMaxHp must NOT be the retired 120 placeholder");
                Assert.That(state.HeroMaxMana, Is.Not.EqualTo(OldPlaceholderMana),
                    "seeded HeroMaxMana must NOT be the retired 60 placeholder");
            }
            finally
            {
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(controllerGo);
                Object.DestroyImmediate(heroGo);
            }
        }
    }
}
