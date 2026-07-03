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

            // Hub scenes: GameOverScreen owns the death flow (pause + Retry/Leave).
            if (HubScenes.IsHub(scene)) return;

            // Arena battle: BattleArena's loss flow shows the Defeat end-state itself.
            if (BattleArena.AnyBattleInProgress) return;

            bool enemyOwned = SceneOwnership.IsEnemyOwned;
            FlowTrace.Step("EndState",
                $"hero death in non-hub scene '{scene}' (enemyOwned={enemyOwned}) -> defeat sting.");
            EndStateView.Show(EndStateVM.FromHeroDeath(enemyOwned));
        }
    }
}
