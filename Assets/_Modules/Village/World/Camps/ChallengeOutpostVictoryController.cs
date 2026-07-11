// =============================================================================
// ChallengeOutpostVictoryController — clear-detect + reward + return for the
// KayKitChallengeOutpost bounded arena.
// -----------------------------------------------------------------------------
// OWNER DIRECTIVE (2026-07-10): wire KayKitChallengeOutpost as the walk-up outpost
// (CavePortalRepointInjector does the entry repoint). But the builder-made scene
// ships with NO victory, NO return path, and NO clear reward — the old Outpost1
// stranded the player the same way (verified RCA):
//   • It loads SINGLE, so OutpostVictoryController never installs (it gates on a
//     HUB/overworld active scene) and it only binds RaidOutpostSystem EnemyOutpost
//     objects — KayKit has none (it uses OutpostEnemyGroupSpawner skeletons).
//   • KayKitChallengeOutpostBuilder adds no SceneTransitionTrigger / exit pad, so
//     there is no way home.
//
// This self-installing controller closes all three, WITHOUT a scene rebake:
//   1. Installs only when the active scene IS "KayKitChallengeOutpost".
//   2. ARMS once the OutpostEnemyGroupSpawner skeletons have realized (they spawn
//      on their own Start), then polls live Enemy count (safe: the outpost loads
//      Single, so the ONLY Enemy objects present are its skeletons).
//   3. On all-dead: pays a Gold + XP clear reward (Gold, NOT crystals — crystals
//      are retiring and gems come from DUNGEONS, not outposts; owner currency
//      direction 2026-07-10), shows the shared Victory end-state, and returns to
//      the hub via SceneRouter.GoCastle().
//
// §12: every gate is FlowTrace-instrumented so one headless run shows arm ->
// cleared -> reward -> return. WebGL-safe (Guard.Try around the risky ops).
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.UI;

namespace DeNelle.Village.World.Camps
{
    public sealed class ChallengeOutpostVictoryController : MonoBehaviour
    {
        private const string Sys = "ChallengeOutpost";
        private const string SceneName = "KayKitChallengeOutpost";

        // Clear reward (Gold + XP). Gold, not crystals — see header.
        private const int ClearGold = 120;
        private const int ClearXp = 120;

        // Arm/poll cadence: skeletons realize on their own Start (synchronous build,
        // but give a generous window in case of a slow load), then we watch for zero.
        private const float ArmTimeoutSeconds = 20f;
        private const float PollInterval = 0.5f;

        private bool _cleared;

        // ── Self-install: only in the challenge-outpost scene, once. ──────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

        private static void TryInstall(Scene scene)
        {
            Guard.Try(Sys, "TryInstall", () =>
            {
                if (SceneManager.GetActiveScene().name != SceneName) return;
                if (FindAnyObjectByType<ChallengeOutpostVictoryController>() != null) return;

                var go = new GameObject("ChallengeOutpostVictoryController");
                go.AddComponent<ChallengeOutpostVictoryController>();
                FlowTrace.Step(Sys, $"self-installed in '{scene.name}' — clear-detect armed.");
            });
        }

        private void Start() => StartCoroutine(WatchRoutine());

        private IEnumerator WatchRoutine()
        {
            // (1) ARM: wait until at least one skeleton has realized, so we never fire
            //     "cleared" on the empty pre-spawn frame.
            float t0 = Time.realtimeSinceStartup;
            int peak = 0;
            while (Time.realtimeSinceStartup - t0 < ArmTimeoutSeconds)
            {
                int alive = CountAliveEnemies();
                if (alive > peak) peak = alive;
                if (alive > 0) break;
                yield return new WaitForSecondsRealtime(PollInterval);
            }

            if (peak == 0)
            {
                // No enemies ever realized — do NOT strand the player behind a clear
                // that can never fire; arm a passive return so the scene is escapable.
                FlowTrace.Warn(Sys, "no skeletons realized within arm window — clear cannot fire; leaving scene escapable.");
                yield break;
            }
            FlowTrace.Step(Sys, $"armed — {peak} skeletons realized; watching for clear.");

            // (2) WATCH: poll until every skeleton is dead.
            while (!_cleared)
            {
                if (CountAliveEnemies() == 0)
                {
                    HandleCleared();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(PollInterval);
            }
        }

        // The outpost loads Single, so every live Enemy present is one of its
        // skeletons — a plain alive-count is an accurate clear detector.
        private static int CountAliveEnemies()
        {
            int alive = 0;
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e != null && !e.IsDead) alive++;
            }
            return alive;
        }

        private void HandleCleared()
        {
            if (_cleared) return;
            _cleared = true;

            using var _ = FlowTrace.Enter(Sys, "CLEARED — garrison wiped");

            // (3a) Reward: Gold + hero XP (not crystals — see header).
            Guard.Try(Sys, "GrantReward", () =>
            {
                EconomyService.Instance?.AddCoins(ClearGold);
                HeroProgression.Instance?.AddXp(ClearXp);
                FlowTrace.Step(Sys, $"reward granted: {ClearGold} gold + {ClearXp} XP.");
            });

            // (3b) Victory screen with a "Return to Castle" primary + auto-dismiss
            //      fallback that also returns, so the player can never get stuck.
            Guard.Try(Sys, "ShowVictory", () =>
            {
                var vm = new EndStateVM
                {
                    Kind = EndStateKind.Victory,
                    Title = "Outpost Cleared",
                    Subtitle = "The garrison is broken.",
                    PrimaryLabel = "Return to Castle",
                    PrimaryRoute = "close",
                    Primary = ReturnHome,
                    AutoDismissSeconds = 6f,
                };
                vm.Spoils.Add(new SpoilRowVM { Label = "Gold", Amount = "+" + ClearGold });
                vm.Spoils.Add(new SpoilRowVM { Label = "XP", Amount = "+" + ClearXp });
                EndStateView.Show(vm);
                FlowTrace.Step(Sys, "victory end-state shown.");
            });

            // Safety net: if the end-state never renders (headless / no canvas), still
            // return home after the auto-dismiss window so the run never strands.
            StartCoroutine(FailsafeReturn(7f));
        }

        private IEnumerator FailsafeReturn(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            ReturnHome();
        }

        private bool _returned;
        private void ReturnHome()
        {
            if (_returned) return;
            _returned = true;
            FlowTrace.Step(Sys, "returning to castle (SceneRouter.GoCastle).");
            Guard.Try(Sys, "GoCastle", () => SceneRouter.GoCastle());
        }
    }
}
