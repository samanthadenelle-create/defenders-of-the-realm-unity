// =============================================================================
// GameOverScreen — "You Have Fallen" / "The Root Went Silent" hub defeat flow.
// DEF-125, re-skinned WO-B (UI conformance audit 2026-07-02 §2e/§3.2).
// -----------------------------------------------------------------------------
// Fires on BOTH death contexts (owner 2026-06-02 chose: yes, a screen on hero
// death — "need a try again option on death"):
//   • HERO dies (HeroHealth.OnDeath)        -> "You Have Fallen"   (silence)
//   • HEART/root falls (OnHeartDestroyed)   -> "The Root Went Silent" (+ Defeat music)
// DEF-141: Defeat music plays on the HEART/root context only, not on hero death.
//
// PRESENTATION (WO-B): this class is now TRIGGERS + ROUTING only. The screen
// itself is the ONE shared Obsidian end-state template (EndStateView /
// EndStateVM.FromGameOver) — real EventSystem kit buttons (EndStateView.Show
// ensures an EventSystem). The old bespoke overlay (manual Update() pointer
// hit-testing against RectTransforms because builds lacked an EventSystem —
// the audit §2e defect) is RETIRED, along with its TryGetTap/BuildOverlay/
// BuildTapButton plumbing.
//
// ONE way out (owner button law, read from the template): the end-state exposes
// exactly ONE primary action — TRY AGAIN (reload the defeat scene / run the
// caller's retry). The old second LEAVE-to-Title button is deliberately dropped;
// ShowDefeat keeps its onLeave parameter for the WO-320 API shape (no live
// callers today) and uses it as the retry FALLBACK when no onRetry was given.
//
// The pause (Time.timeScale = 0) + reload-on-retry supersede the hero's silent
// auto-respawn (DEF-102) — the player chooses. EndStateView is pause-safe: its
// reveal tween runs on unscaled time, and this VM never auto-dismisses (an
// auto-fired Retry would reload the scene without player intent).
// Self-bootstrapping DDOL. Copy is DEF-141/WO-235 locked canon.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;      // WO-1353: WorldHold is the ONE writer of Time.timeScale
using DeNelle.Village.UI;   // EndStateView/VM (the shared template) + LevelUpSkillPopup

namespace DeNelle.Village
{
    /// <summary>Hero/Heart death trigger hub — shows the shared Obsidian end-state with Try Again.</summary>
    public sealed class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }

        // WO (F8 2026-06-28): the defeat overlay must fire in EVERY home/hub scene that
        // can host a defendable Heart + a wave loop — not just Village2. The wave loop now
        // runs in MainCastle_Hall (WO-584), whose Heart hit 0 with NO game-over because this
        // screen only hooked the hardcoded "Village2". Gate on the canonical HubScenes list
        // (Village2 + MainCastle_Hall + CastleHub*) so adding a hub = one edit there. Arena /
        // RaidBase_* scenes are intentionally NOT hubs — they keep their own death flow.
        private static bool IsDefeatScene(string sceneName) => HubScenes.IsHub(sceneName);

        private HeartController _heart;
        private HeroHealth _hero;
        private bool _shown;
        private float _lastHookAttempt;    // DEF-136: throttle the per-frame Hook() scene search

        // WO-320: scene-agnostic defeat entry. When non-null these are invoked on
        // Retry instead of the hub SceneRouter default, so callers in OTHER scenes
        // can reuse this exact flow with their own return-scene wiring. The hub
        // hooked path leaves these null. _onLeave is kept for the WO-320 API shape
        // (the shared template has ONE exit) and serves as the retry fallback.
        private System.Action _onRetry;
        private System.Action _onLeave;
        private string _defeatScene;       // scene active when defeat showed — Retry reloads THIS

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("GameOverScreen").AddComponent<GameOverScreen>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsDefeatScene(SceneManager.GetActiveScene().name)) Hook();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseWorldHold("game-over screen host DESTROYED while the death freeze was held");
        }

        // =====================================================================
        //  WORLD CLOCK — WO-1353. The death freeze used to be three bare writes of
        //  Time.timeScale (Show -> 0, OnRetry -> 1, OnSceneLoaded -> 1). It is a correct
        //  PAIR and it stays a pair; what changes is that the pair is now expressed as a
        //  HOLD on the one owner, so a fourth exit nobody has written yet cannot strand
        //  the world frozen, and the watchdog can name this screen if one does.
        //  ⭐ DeathTrace.TimeScaleFroze / TimeScaleRestored are KEPT VERBATIM alongside
        //  the hold, per WO-1353 §3: the death flow's own step-in/step-out reporting is
        //  the trace an F8 capture is read from, and folding it into the owner means
        //  sharing the mechanism, not losing the report.
        // =====================================================================

        /// <summary>Reason token the death-freeze hold carries.</summary>
        private const string HoldReason = "game-over";

        private WorldHold.Handle _worldHold;

        /// <summary>The ONE step-out for the death freeze. Idempotent - every exit calls it.</summary>
        private void ReleaseWorldHold(string why)
        {
            var hold = _worldHold;
            _worldHold = null;
            if (hold == null) return;
            hold.Dispose();
            FlowTrace.Step("EndState",
                $"game-over world hold released - {why}. Live holds now [{WorldHold.Describe()}], " +
                $"timeScale {Time.timeScale:F2}.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The end-state view tears itself down on sceneLoaded (its own hook);
            // this class only resets its trigger state for the incoming scene.
            _shown = false;
            ReleaseWorldHold($"scene '{scene.name}' loaded (raid-evac / route out of the paused " +
                             "defeat screen)");
            // F8-15: freeze STEP-OUT on the scene-swap restore path (raid-evac / route out of the
            // paused defeat screen re-runs time here). Pairs with GameOverScreen.Show's freeze.
            DeathTrace.TimeScaleRestored("GameOverScreen.OnSceneLoaded('" + scene.name + "')");
            _heart = null;
            _hero = null;
            ClearCustomActions(); // WO-320: don't let a caller retry/leave action survive the scene swap
            if (IsDefeatScene(scene.name)) Hook();
        }

        private void Hook()
        {
            if (_heart == null) _heart = FindAnyObjectByType<HeartController>();
            if (_heart != null)
            {
                _heart.OnHeartDestroyed -= ShowHeartFell;
                _heart.OnHeartDestroyed += ShowHeartFell;
            }
            if (_hero == null) _hero = HeroHealth.Instance ?? FindAnyObjectByType<HeroHealth>();
            if (_hero != null)
            {
                _hero.OnDeath -= ShowHeroFell;
                _hero.OnDeath += ShowHeroFell;
            }
        }

        private void Update()
        {
            // Late-resolve hero/heart if they spawned after this bootstrap. DEF-136:
            // FindAnyObjectByType is a scene-wide search; running it every frame churns
            // on mobile. Throttle to once per ~0.5s and stop once both refs resolve.
            if ((_heart == null || _hero == null)
                && IsDefeatScene(SceneManager.GetActiveScene().name)
                && Time.unscaledTime - _lastHookAttempt > 0.5f)
            {
                _lastHookAttempt = Time.unscaledTime;
                Hook();
            }
        }

        // DEF-141 / WO-235 locked canon copy for Heartwood (root) destruction.
        // "THE ROOT WENT SILENT" replaces the retired "HEART OF ELARION HAS FALLEN".
        private void ShowHeartFell() => Show(
            "THE ROOT WENT SILENT",
            "The root went silent.\nThe dark poured in where the light had been,\nbut Elarion remembers those who rise again.",
            isHeartDestroyed: true);

        /// <summary>
        /// WO-320: scene-agnostic defeat panel. Any scene (e.g. Defend-the-Tower) can
        /// call this to show the SAME pause + Try-Again end-state with its OWN wiring,
        /// without depending on the hub hook gate. Retry runs <paramref name="onRetry"/>
        /// (falling back to <paramref name="onLeave"/>, then the scene reload) — the
        /// shared template exposes ONE primary action (owner button law).
        /// Self-bootstraps the singleton if a scene calls before AfterSceneLoad.
        /// </summary>
        public static void ShowDefeat(string title, string body,
                                      System.Action onRetry, System.Action onLeave)
        {
            if (Instance == null) Bootstrap();
            Instance?.ShowDefeatInstance(title, body, onRetry, onLeave);
        }

        private void ShowDefeatInstance(string title, string body,
                                        System.Action onRetry, System.Action onLeave)
        {
            _onRetry = onRetry;
            _onLeave = onLeave;
            // Defeat context = "the thing you defended fell" -> play the somber track,
            // matching the Heart/root branch (hero death is silence, but a DTT loss is
            // a structure loss). isHeartDestroyed routes the music in Show().
            Show(title, string.IsNullOrEmpty(body)
                    ? "The dark poured in where the light had been,\nbut Elarion remembers those who rise again."
                    : body,
                 isHeartDestroyed: true);
        }

        private void ClearCustomActions() { _onRetry = null; _onLeave = null; }

        private void ShowHeroFell()
        {
            // DOUBLE DEATH SCREEN (owner F8 2026-07-12, DeathTrace proof: 'YOU HAVE
            // FALLEN' by GameOverScreen.Show + 'Defeat' by BattleArenaHud in one death):
            // while a BattleArena fight owns the death, ITS loss flow (Regroup sting +
            // revive-at-return) is the ONE death screen — this hub game-over must stand
            // down, exactly like HeroDeathEndState already does. Bonus hazard removed:
            // Show() freezes Time.timeScale, which stalled the arena's scaled-time
            // return-fade coroutines under the stacked screen.
            if (DeNelle.Village.Arena.BattleArena.AnyBattleInProgress)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Step("EndState",
                    "hub game-over (hero-fell) STAND-DOWN — battle in progress; arena Defeat flow owns this death.");
                return;
            }

            // OPEN-WORLD FIELD DEATH (F8 2026-07-16 "rspawned in world not town"): the merged
            // Main_Castle_Overworld is BOTH the home hub AND the explorable world. Dying to a field
            // mob out there is NOT a hub-defense loss — it must NOT pause+reload via this defense-
            // fail screen. The Time.timeScale=0 pause froze HeroHealth's scaled-time respawn down-
            // beat, and the Retry scene-reload dropped the hero back out in the world. Stand down
            // (mirroring the BattleArena stand-down above) so HeroHealth respawns the hero at the
            // TOWN courtyard and HeroDeathEndState shows the non-pausing "rise again" sting. The
            // HEART-death (defense fail) path — ShowHeartFell — is UNTOUCHED and still owns the
            // overworld Heart breach.
            string overworldScene = SceneManager.GetActiveScene().name;
            if (HubScenes.IsOverworld(overworldScene))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Step("Respawn",
                    "hub game-over (hero-fell) STAND-DOWN in overworld '" + overworldScene +
                    "' -> HeroHealth town-respawn + HeroDeathEndState sting own open-world death (no pause/reload).");
                return;
            }
            Show(
                "YOU HAVE FALLEN",
                "The dark takes you, but Elarion still needs its defender.\nRise, and try again.",
                isHeartDestroyed: false);
        }

        private void Show(string title, string body, bool isHeartDestroyed)
        {
            if (_shown) return;
            _shown = true;
            // Remember WHERE we fell so Retry reloads THIS scene (MainCastle_Hall, Village2,
            // …), not the old hardcoded Village2 default — wrong when the wave loop ran in the hub.
            _defeatScene = SceneManager.GetActiveScene().name;
            // WO-333: the level-up skill-point panel (LevelUpSkillPopup) is a persistent
            // HUD layer that otherwise stays open BEHIND the game-over overlay. Force-close
            // any open instances before we build the overlay. Null-guarded per CLAUDE.md §10.
            foreach (var p in FindObjectsByType<LevelUpSkillPopup>())
                if (p != null) p.Hide();
            // DEF-141 / WO-235: the somber Defeat track (GameOver.mp3) belongs to the
            // Heartwood/root destruction ONLY. Hero death is silence (single tone) — so
            // we gate the music on the death context. Null-guarded per CLAUDE.md §10.
            if (isHeartDestroyed)
                CoreServices.Audio?.PlayMusic(DeNelle.Core.Audio.MusicTrack.Defeat);
            // F8-15 death forensic window: the hub game-over PAUSES time here — anything queued
            // after this (respawn coroutine, warps) freezes until Retry. Freeze STEP-IN: records
            // the pending freeze so DeathTrace.PollFreezeStuck self-reports if it is never restored.
            // WO-1360: PLAYER-OWNED. The death screen ends when the player taps Retry, which can
            // be hours (or a backgrounded app). A ceiling here would unfreeze the world behind a
            // Game Over card. Release stays paired to Retry / sceneLoaded / OnDestroy below.
            _worldHold = WorldHold.AcquirePlayerOwned(HoldReason);
            DeathTrace.TimeScaleFroze("GameOverScreen.Show",
                $"'{title}' in '{_defeatScene}' — scaled-time respawn/down-beat coroutines freeze until Retry/sceneLoaded");

            // WO-B: the ONE shared Obsidian end-state template renders the screen
            // (real EventSystem buttons — EndStateView ensures one; the old manual
            // hit-test overlay is retired). Primary = Try Again -> OnRetry().
            FlowTrace.Step("EndState",
                $"hub game-over ({(isHeartDestroyed ? "heart-fell" : "hero-fell")}) in '{_defeatScene}' -> shared end-state.");
            EndStateView.Show(EndStateVM.FromGameOver(isHeartDestroyed, title, body, OnRetry));
        }

        /// <summary>The single end-state action: unpause and rerun the fight — the
        /// caller-supplied retry (WO-320) when present, else reload the defeat scene.</summary>
        private void OnRetry()
        {
            ReleaseWorldHold("the player chose Retry");
            // F8-15: freeze STEP-OUT — the player chose Retry, unpausing the death flow. This is the
            // continue path out of the paused game-over; pairs with GameOverScreen.Show's freeze.
            DeathTrace.TimeScaleRestored("GameOverScreen.OnRetry");
            DeathTrace.Note($"RETRY chosen -> unpause + reload/route (scene='{_defeatScene}', customRetry={( _onRetry != null)})");
            _shown = false;
            // WO-320: prefer the caller-supplied retry (e.g. reload the DTT scene);
            // onLeave is the legacy secondary — used only as a fallback route when a
            // caller wired ONLY onLeave. Default: reload the scene where we fell.
            var retry = _onRetry ?? _onLeave;
            if (retry != null) { ClearCustomActions(); retry(); }
            else SceneRouter.LoadScene(string.IsNullOrEmpty(_defeatScene) ? SceneRouter.Village : _defeatScene);
        }
    }
}
