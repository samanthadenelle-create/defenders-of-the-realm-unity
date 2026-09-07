// =============================================================================
// HeroDeathEndState — closes the audit MISSING (UI conformance audit 2026-07-02
// §2e): outside hub scenes the hero silently respawns (HeroHealth.HandleDeath)
// or evacuates a raid with NO death screen and NO defeat sting. This bootstrap
// SUBSCRIBES to the existing HeroHealth.OnDeath event (HeroHealth.cs:522 — that
// file is NOT edited) and shows the shared Obsidian end-state defeat variant.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Division of labour (deliberate, verified from code):
//   • HUB scenes      -> GameOverScreen owns hero/heart death (pause + Retry/Leave);
//                        we stand down (HubScenes.IsHub gate, same list it uses).
//   • ARENA battles   -> BattleArena's loss flow shows the Defeat end-state via
//                        BattleArenaHud.ShowResult; we stand down while
//                        BattleArena.AnyBattleInProgress so it can't double-show.
//   • Everywhere else (dungeon / outpost / raid / open world) -> THIS shows
//                        EndStateVM.FromHeroDeath. "Rise again" routes to the
//                        EXISTING automatic respawn/evac (HeroHealth.HandleDeath
//                        already runs it); the screen is the sting + narration,
//                        so it never pauses time (a pause would freeze that
//                        scaled-time respawn coroutine).
//
// Same self-bootstrapping DDOL + throttled late-resolve pattern as GameOverScreen
// (the hero can spawn after scene load).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Arena;

namespace DeNelle.Village.UI
{
    /// <summary>Shows the shared end-state defeat sting on hero death in NON-hub scenes.</summary>
    public sealed class HeroDeathEndState : MonoBehaviour
    {
        private static HeroDeathEndState _instance;

        private HeroHealth _hero;
        private float _lastHookAttempt;   // throttle the scene-wide search (GameOverScreen pattern)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            new GameObject("HeroDeathEndState").AddComponent<HeroDeathEndState>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Hook();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unhook();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Unhook();          // the old scene's HeroHealth is gone/stale
            Hook();
        }

        private void Update()
        {
            // Late-resolve: the hero often spawns after AfterSceneLoad (HeroControlEnsurer).
            if (_hero == null && Time.unscaledTime - _lastHookAttempt > 0.5f)
            {
                _lastHookAttempt = Time.unscaledTime;
                Hook();
            }
        }

        private void Hook()
        {
            if (_hero == null) _hero = HeroHealth.Instance ?? FindAnyObjectByType<HeroHealth>();
            if (_hero == null) return;
            _hero.OnDeath -= OnHeroDeath;   // dedupe (same -=/+= idiom as GameOverScreen)
            _hero.OnDeath += OnHeroDeath;
        }

        private void Unhook()
        {
            if (_hero != null) _hero.OnDeath -= OnHeroDeath;
            _hero = null;
        }

        private void OnHeroDeath()
        {
            string scene = SceneManager.GetActiveScene().name;

            // Hub scenes: GameOverScreen owns the death flow (pause + Retry/Leave) -- EXCEPT the
            // merged overworld (Main_Castle_Overworld), where open-world field death is a town-
            // respawn, not a hub-defense loss. There GameOverScreen's hero-fell path stands down
            // (see GameOverScreen.ShowHeroFell, F8 2026-07-16), so THIS non-pausing defeat sting
            // narrates HeroHealth's town-respawn -- exactly the dungeon/outpost/raid/open-world
            // case this bootstrap was built for.
            if (HubScenes.IsHub(scene) && !HubScenes.IsOverworld(scene)) return;

            // Arena battle: BattleArena's loss flow shows the Defeat end-state itself.
            if (BattleArena.AnyBattleInProgress) return;

            // WO-1437 — the SENTENCE must match the BRANCH HeroHealth.HandleDeath actually
            // takes, and it did not. EndStateVM.FromHeroDeath's bool is copy-only: true prints
            // "The raid is lost. You retreat to the castle to fight another day.", false prints
            // "The dark takes you, but Elarion still needs its defender." Fed IsEnemyOwned
            // alone, a death AFTER the win claimed the camp (RaidClaimService flips ownership
            // player-owned) read enemyOwned=False and promised a respawn while the hero was
            // standing in a raid base — captured verbatim at 13:02:47 in
            // logs/debug/raid-stuck-2026-09-06.log:
            //     "hero death in non-hub scene 'RaidBase_raider_camp_small' (enemyOwned=False)"
            // against the SAME scene reading enemyOwned=True at 12:59:45.
            //
            // Read the same two signals HeroHealth reads, in the same order, so the copy and
            // the routing cannot disagree. This file makes NO routing decision — presentation
            // stays out of the lifecycle (ARCHITECTURE_PRINCIPLES); it only reports it.
            bool enemyOwned = SceneOwnership.IsEnemyOwned;
            bool raidInProgress = RaidScoring.RaidInProgress;
            var raidScorer = RaidScoring.Instance;
            bool raidSettled = raidScorer != null && raidScorer.Finalized;

            // WO-1526 — THE RAID IS NOT OVER, SO THERE IS NO END STATE TO SHOW. Same three
            // signals, read in the same order as HeroHealth.HandleDeath (the WO-1437 rule: the
            // copy tracks the branch, it never decides one). When the hero falls inside a LIVE
            // raid the army fights on, so a "YOU HAVE FALLEN" modal would (a) tell the player
            // "The raid is lost" about a raid that is still winnable, and (b) put a full-screen
            // panel over the deploy tray the player now needs MORE than before, not less. The
            // status line RaidDeployController.NotifyHeroDown sets ("HERO DOWN - your army fights
            // on", composed by EndStateVM) is the whole presentation of this beat.
            // `raidScorer != null` mirrors HeroHealth exactly, for the same reason: a raid whose
            // scorer failed to install still reads RaidInProgress true off the scene-name
            // fallback, and there the hero's death IS still the exit - so the fallen screen must
            // still show. The copy tracks the branch; it never picks one (WO-1437).
            if (raidInProgress && raidScorer != null && !raidSettled && !RaidScoring.RaidDeathEndsRaid)
            {
                FlowTrace.Step("EndState",
                    $"hero death in LIVE raid scene '{scene}' (enemyOwned={enemyOwned} " +
                    $"raidSettled=False raidDeathEndsRaid={RaidScoring.RaidDeathEndsRaid}) -> NO end " +
                    "state. WO-1526: the raid continues and the army fights on, so the fallen screen " +
                    "would be both untrue and in the way. The raid HUD status line carries this beat.");
                return;
            }

            bool leavingTheRaid = enemyOwned ||
                                  (raidInProgress && (raidSettled || RaidScoring.RaidDeathEndsRaid));

            FlowTrace.Step("EndState",
                $"hero death in non-hub scene '{scene}' (enemyOwned={enemyOwned} " +
                $"raidInProgress={raidInProgress} raidSettled={raidSettled} " +
                $"leavingTheRaid={leavingTheRaid}) -> defeat sting. WO-1437: the copy now tracks " +
                "HeroHealth's evac branch, not the faction flag the victory claim flips.");
            EndStateView.Show(EndStateVM.FromHeroDeath(leavingTheRaid));
        }
    }
}
