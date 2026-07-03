// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// VillageSmokeTests (PlayMode) — WO-329 regression suite.
// -----------------------------------------------------------------------------
// Headless smoke pass over the live Village scene. These cannot run in EditMode
// (they need a loaded, playing scene), but they DO run headless in batchmode so
// the CLI gate can catch the big regressions automatically:
//   - the Village scene loads at all,
//   - the hero spawns non-null, is tagged "Player", and has an Animator
//     (guards the "no hero / T-pose / no controller" class of breakage),
//   - WaveManager.ForceBeginNextWave() advances the wave loop out of Idle
//     (WO-327), and
//   - no NullReferenceException is logged during a short run (guards WO-328).
//
// The hub scene MUST be present in Build Settings (Assets/Scenes/Village2.unity)
// for the loads below to resolve. (Village.unity was removed 2026-06-29 — orphaned;
// Village2 is the live hub scene.)
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using DeNelle.Village;

namespace DeNelle.Tests.PlayMode
{
    [TestFixture]
    public class VillageSmokeTests
    {
        private const string SceneName = "Village2";
        private readonly List<string> _exceptions = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _exceptions.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
                _exceptions.Add(condition + "\n" + stackTrace);
        }

        private IEnumerator LoadVillage()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            // Let bootstrap / Awake / Start / first frames settle.
            for (int i = 0; i < 5; i++) yield return null;
        }

        private static GameObject FindHero()
        {
            return GameObject.FindWithTag("Player");
        }

        [UnityTest]
        public IEnumerator village_scene_loads()
        {
            yield return LoadVillage();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneName),
                "Village scene must be the active scene after load.");
            Assert.That(SceneManager.GetActiveScene().isLoaded, Is.True);
        }

        [UnityTest]
        public IEnumerator hero_spawns_non_null_with_animator()
        {
            yield return LoadVillage();
            var hero = FindHero();
            Assert.That(hero, Is.Not.Null, "A 'Player'-tagged hero must exist in the Village scene.");
            var animator = hero.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, "The hero must have an Animator (no T-pose / dead rig).");
        }

        [UnityTest]
        public IEnumerator force_begin_next_wave_advances_the_loop()
        {
            yield return LoadVillage();
            var wm = Object.FindAnyObjectByType<WaveManager>();
            Assert.That(wm, Is.Not.Null, "Village scene must contain a WaveManager.");

            wm.ForceBeginNextWave();
            for (int i = 0; i < 60; i++) yield return null;  // ~1s of frames to let the loop kick

            Assert.That(wm.Phase, Is.Not.EqualTo(WavePhase.Idle),
                "ForceBeginNextWave must move the WaveManager out of Idle (WO-327).");
        }

        [UnityTest]
        public IEnumerator short_run_logs_no_null_reference_exception()
        {
            yield return LoadVillage();
            var wm = Object.FindAnyObjectByType<WaveManager>();
            if (wm != null) wm.ForceBeginNextWave();

            for (int i = 0; i < 90; i++) yield return null;  // ~1.5s headless run

            foreach (var ex in _exceptions)
            {
                Assert.That(ex.Contains("NullReferenceException"), Is.False,
                    "A NullReferenceException was logged during the short run (WO-328):\n" + ex);
            }
        }
    }
}
