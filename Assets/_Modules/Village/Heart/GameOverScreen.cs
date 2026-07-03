// =============================================================================
// GameOverScreen — "You Have Fallen" / "The Heart Has Fallen" overlay with a
// Try Again / Exit choice. DEF-125.
// -----------------------------------------------------------------------------
// Fires on BOTH death contexts (owner 2026-06-02 chose: yes, a screen on hero
// death — "need a try again option on death"):
//   • HERO dies (HeroHealth.OnDeath)        -> "You Have Fallen"   (silence)
//   • HEART/root falls (OnHeartDestroyed)   -> "The Root Went Silent" (+ Defeat music)
// Both pause the game and offer:  [R] Try Again (reload Village) · [Esc] Title.
// DEF-141: Defeat music plays on the HEART/root context only, not on hero death.
//
// Code-built uGUI (screen-space Canvas + Text via the proven ThreatSkullPlate
// font path — NO UXML, which doesn't render in player builds). KEYBOARD prompts,
// not buttons, so there's no EventSystem/click plumbing to fail in WebGL. The
// pause + reload-on-retry supersede the hero's silent auto-respawn (DEF-102) —
// the player now chooses. Self-bootstrapping DDOL. Copy is a stub for creative.
// Follow-up: mobile tap-buttons (current is keyboard-only).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.UI;
using DeNelle.Village.UI;   // WO-333: force-close LevelUpSkillPopup on game-over

namespace DeNelle.Village
{
    /// <summary>Hero/Heart death overlay with [R] retry / [Esc] exit key prompts.</summary>
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
        private GameObject _overlay;
        private bool _shown;
        private RectTransform _retryBtn;   // mobile tap target — Try Again
        private RectTransform _exitBtn;    // mobile tap target — Leave to Title
        private float _lastHookAttempt;    // DEF-136: throttle the per-frame Hook() scene search

        // WO-320: scene-agnostic defeat entry. When non-null these are invoked on
        // Retry / Leave instead of the Village2 SceneRouter defaults, so callers in
        // OTHER scenes (e.g. Defend-the-Tower) can reuse this exact panel with their
        // own return-scene wiring. Village2's hooked path leaves these null.
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
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _shown = false;
            if (_overlay != null) { Destroy(_overlay); _overlay = null; }
            _retryBtn = null; _exitBtn = null;
            Time.timeScale = 1f;
            _heart = null;
            _hero = null;
            ClearCustomActions(); // WO-320: don't let a DTT retry/leave action survive the scene swap
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
            if (!_shown) return;

            // Mobile/touch: poll the pointer against the button rects (owner 2026-06-02:
            // "stuck at the dead screen, i dont have an esc or b button"). uGUI buttons
            // need an EventSystem the build doesn't have, so we hit-test manually — the
            // same EventSystem-free approach as VirtualJoystick. Retry/Exit are reached
            // by the on-screen TRY AGAIN / LEAVE tap buttons (no keyboard prompts).
            bool retry = false, exit = false;
            if (TryGetTap(out Vector2 tap))
            {
                if (_retryBtn != null && RectTransformUtility.RectangleContainsScreenPoint(_retryBtn, tap, null)) retry = true;
                else if (_exitBtn != null && RectTransformUtility.RectangleContainsScreenPoint(_exitBtn, tap, null)) exit = true;
            }

            if (retry)
            {
                Time.timeScale = 1f;
                // WO-320: prefer the caller-supplied retry (e.g. reload the DTT scene);
                // fall back to the Village2 reload when no custom action was provided.
                if (_onRetry != null) { var a = _onRetry; ClearCustomActions(); a(); }
                else SceneRouter.LoadScene(string.IsNullOrEmpty(_defeatScene) ? SceneRouter.Village : _defeatScene);
            }
            else if (exit)
            {
                Time.timeScale = 1f;
                if (_onLeave != null) { var a = _onLeave; ClearCustomActions(); a(); }
                else SceneRouter.LoadScene(SceneRouter.Title);
            }
        }

        /// <summary>First-touch / mouse-down screen position this frame (no EventSystem).</summary>
        private static bool TryGetTap(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { pos = t.position; return true; }
            }
            if (Input.GetMouseButtonDown(0)) { pos = (Vector2)Input.mousePosition; return true; }
            pos = default;
            return false;
        }

        // DEF-141 / WO-235 locked canon copy for Heartwood (root) destruction.
        // "THE ROOT WENT SILENT" replaces the retired "HEART OF ELARION HAS FALLEN".
        private void ShowHeartFell() => Show(
            "THE ROOT WENT SILENT",
            "The root went silent.\nThe dark poured in where the light had been,\nbut Elarion remembers those who rise again.",
            isHeartDestroyed: true);

        /// <summary>
        /// WO-320: scene-agnostic defeat panel. Any scene (e.g. Defend-the-Tower) can
        /// call this to show the SAME pause + Retry/Leave overlay with its OWN wiring,
        /// without depending on the Village2 hook gate. Retry/Leave run the supplied
        /// callbacks (e.g. reload the DTT/return scene) instead of the Village defaults.
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
            // Defeat context = "the thing you defended fell" → play the somber track,
            // matching the Heart/root branch (hero death is silence, but a DTT loss is
            // a structure loss). isHeartDestroyed routes the music in Show().
            Show(title, string.IsNullOrEmpty(body)
                    ? "The dark poured in where the light had been,\nbut Elarion remembers those who rise again."
                    : body,
                 isHeartDestroyed: true);
        }

        private void ClearCustomActions() { _onRetry = null; _onLeave = null; }

        private void ShowHeroFell() => Show(
            "YOU HAVE FALLEN",
            "The dark takes you, but Elarion still needs its defender.\nRise, and try again.",
            isHeartDestroyed: false);

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
            Time.timeScale = 0f;
            BuildOverlay(title, body);
        }

        private void BuildOverlay(string title, string body)
        {
            _overlay = new GameObject("GameOverOverlay");
            DontDestroyOnLoad(_overlay);

            var canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;   // above all gameplay UI
            var scaler = _overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var rootT = _overlay.transform;

            // Full-screen dark scrim behind the modal — the shared kit scrim tint.
            ElarionUiKit.Scrim(rootT);

            // Framed dark-glass card (the TOWN-HUD panel language: deep glass + gold rim).
            var card = ElarionUiKit.Panel(rootT, new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.82f),
                                          deep: true, innerRim: true);
            var cardT = card.transform;

            // Gilt title banner (kit Header glyph + gilt rule) — the "fallen" headline.
            ElarionUiKit.Header(cardT, title, x0: 0.06f, x1: 0.94f, y0: 0.80f, y1: 0.94f);

            // Body narrative — warm parchment, centred, multi-line.
            ElarionUiKit.Label(cardT, body, 0.40f, 0.76f, ElarionUi.Parchment,
                               ElarionUi.FontHead, TextAlignmentOptions.Center,
                               0.08f, 0.92f);

            // Prompt hint — muted parchment under the body.
            ElarionUiKit.Label(cardT, "Tap a button below",
                               0.30f, 0.40f, ElarionUi.ParchmentDim,
                               ElarionUi.FontLabel, TextAlignmentOptions.Center,
                               0.08f, 0.92f);

            // Tap buttons (mobile) — kit Confirm (green) / Danger (red), side by side.
            // Manual hit-testing in Update() (no EventSystem in builds), so we keep
            // their RectTransforms rather than relying on Button.onClick.
            _retryBtn = BuildTapButton("TRY AGAIN", ElarionUiKit.ButtonKind.Confirm,
                                       new Vector2(0.10f, 0.08f), new Vector2(0.49f, 0.24f), cardT);
            _exitBtn  = BuildTapButton("LEAVE",     ElarionUiKit.ButtonKind.Danger,
                                       new Vector2(0.51f, 0.08f), new Vector2(0.90f, 0.24f), cardT);
        }

        /// <summary>A code-built tap button styled via the shared kit (rounded glass fill +
        /// rim + bold parchment label). Returns its RectTransform for manual hit-testing in
        /// <see cref="Update"/> (no EventSystem in builds — we don't wire Button.onClick).</summary>
        private RectTransform BuildTapButton(string label, ElarionUiKit.ButtonKind kind,
                                             Vector2 anchorMin, Vector2 anchorMax, Transform parent)
        {
            var btn = ElarionUiKit.Button(parent, label, kind, anchorMin, anchorMax);
            return btn.GetComponent<RectTransform>();
        }
    }
}
