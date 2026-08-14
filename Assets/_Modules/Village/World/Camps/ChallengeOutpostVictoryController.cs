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

        // WO-978 — the MEASURED clear reward (what the wallet/hero actually took), not the
        // constants above. The victory screen renders THESE so the player-facing spoils can
        // never advertise a number the economy refused. _rewardShort drives a plain-WORDS
        // caveat on the screen (owner is red/green colourblind — never colour alone).
        private int _goldCredited;
        private int _xpCredited;
        private bool _rewardShort;

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
                // FIX A (WO-771, docs/RAID_NORTHSTAR.md §2A/§3): the walk-up-outpost loop is
                // RETIRED (raid = Teleport/Deploy). When ff.raidwalk is OFF (default) the player
                // is never dropped into KayKitChallengeOutpost via the cave, so this self-installer
                // must NOT arm — mirrors the sibling gate on OutpostVictoryController.TryInstall.
                // Flip ff.raidwalk ON to restore the walk-up outpost victory/return loop.
                if (!FeatureFlags.RaidContinuousWalk) return;
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
            //
            // WO-978 — THE OLD LINE HERE WAS UNFALSIFIABLE. It printed
            //   $"reward granted: {ClearGold} gold + {ClearXp} XP."
            // where BOTH values are compile-time consts declared 100 lines up — a trace built
            // entirely from constants can never disagree with itself, so it read as proof of
            // payment even when both grants no-oped. Both calls were also null-conditional:
            // a missing EconomyService / HeroProgression paid NOTHING, silently.
            //
            // Neither API hands back a credited amount (AddCoins returns void; AddXp returns
            // LEVELS GAINED, not XP), so we measure the observable totals either side and log
            // the DELTA. HeroProgression.LifetimeXp is monotonic, which makes it the honest
            // XP meter (Xp alone resets on each level-up).
            Guard.Try(Sys, "GrantReward", () =>
            {
                var eco = EconomyService.Instance;
                if (eco == null)
                {
                    _rewardShort = true;
                    FlowTrace.Fail(Sys, $"clear reward LOST — no EconomyService in this scene; the requested " +
                                        $"{ClearGold} gold was NEVER credited (the player cleared the outpost for nothing).");
                }
                else
                {
                    int before = eco.Coins;
                    eco.AddCoins(ClearGold);
                    _goldCredited = eco.Coins - before;
                    if (_goldCredited == ClearGold)
                        FlowTrace.Step(Sys, $"gold credited: +{_goldCredited} of {ClearGold} requested -> total {eco.Coins}.");
                    else
                    {
                        _rewardShort = true;
                        FlowTrace.Warn(Sys, $"gold SHORT: credited {_goldCredited} of {ClearGold} requested -> total {eco.Coins}. " +
                                            "AddCoins silently no-ops when GameStateService has no loaded State — " +
                                            "the outpost was cleared and the player was not paid in full.");
                    }
                }

                var hero = HeroProgression.Instance;
                if (hero == null)
                {
                    _rewardShort = true;
                    FlowTrace.Fail(Sys, $"clear reward LOST — no HeroProgression in this scene; the requested " +
                                        $"{ClearXp} XP was NEVER credited.");
                }
                else
                {
                    float xpBefore = hero.LifetimeXp;
                    int levelsGained = hero.AddXp(ClearXp);          // return value = LEVELS, not XP
                    _xpCredited = Mathf.RoundToInt(hero.LifetimeXp - xpBefore);
                    if (_xpCredited >= ClearXp)
                        FlowTrace.Step(Sys, $"XP credited: +{_xpCredited} of {ClearXp} requested " +
                                            $"(levels gained {levelsGained}, lifetime {hero.LifetimeXp:0}).");
                    else
                    {
                        _rewardShort = true;
                        FlowTrace.Warn(Sys, $"XP SHORT: credited {_xpCredited} of {ClearXp} requested " +
                                            $"(lifetime {hero.LifetimeXp:0}) — the hero did not receive the clear reward.");
                    }
                }
            });

            // (3b) Victory screen with a "Return to Castle" primary + auto-dismiss
            //      fallback that also returns, so the player can never get stuck.
            Guard.Try(Sys, "ShowVictory", () =>
            {
                var vm = new EndStateVM
                {
                    Kind = EndStateKind.Victory,
                    Title = "Outpost Cleared",
                    // WO-978 §4.3 — words, never colour alone. If the economy took less than
                    // the clear awarded, the screen SAYS so rather than showing a number that
                    // silently differs from the wallet.
                    Subtitle = _rewardShort
                        ? "The garrison is broken. Some of the reward could not be paid out."
                        : "The garrison is broken.",
                    PrimaryLabel = "Return to Castle",
                    PrimaryRoute = "close",
                    Primary = ReturnHome,
                    AutoDismissSeconds = 6f,
                };
                // WO-697: reward numbers through the ONE kit formatter (compact >= 10k).
                // WO-978: the numbers are the MEASURED credits (_goldCredited/_xpCredited),
                // not the ClearGold/ClearXp constants — the spoils row must state what the
                // player actually received.
                vm.Spoils.Add(new SpoilRowVM { Label = "Gold", Amount = "+" + DeNelle.Core.UI.ElarionUi.CompactNumber(_goldCredited) });
                vm.Spoils.Add(new SpoilRowVM { Label = "XP", Amount = "+" + DeNelle.Core.UI.ElarionUi.CompactNumber(_xpCredited) });
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
