// =============================================================================
// GameOverScreen — "You Have Fallen" / "The Heart Has Fallen" overlay with a
// Try Again / Exit choice. DEF-125.
// -----------------------------------------------------------------------------
// Fires on BOTH death contexts (owner 2026-06-02 chose: yes, a screen on hero
// death — "need a try again option on death"):
//   • HERO dies (HeroHealth.OnDeath)        -> "You Have Fallen"
//   • HEART/tree falls (OnHeartDestroyed)   -> "The Heart of Elarion Has Fallen"
// Both pause the game and offer:  [R] Try Again (reload Village) · [Esc] Title.
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
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DeNelle.Core;

namespace DeNelle.Village
{
    /// <summary>Hero/Heart death overlay with [R] retry / [Esc] exit key prompts.</summary>
    public sealed class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }
        private const string TargetScene = "Village";

        private HeartController _heart;
        private HeroHealth _hero;
        private GameObject _overlay;
        private bool _shown;
        private RectTransform _retryBtn;   // mobile tap target — Try Again
        private RectTransform _exitBtn;    // mobile tap target — Leave to Title

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
            if (SceneManager.GetActiveScene().name == TargetScene) Hook();
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
            if (scene.name == TargetScene) Hook();
        }

        private void Hook()
        {
            if (_heart == null) _heart = FindFirstObjectByType<HeartController>();
            if (_heart != null)
            {
                _heart.OnHeartDestroyed -= ShowHeartFell;
                _heart.OnHeartDestroyed += ShowHeartFell;
            }
            if (_hero == null) _hero = HeroHealth.Instance ?? FindFirstObjectByType<HeroHealth>();
            if (_hero != null)
            {
                _hero.OnDeath -= ShowHeroFell;
                _hero.OnDeath += ShowHeroFell;
            }
        }

        private void Update()
        {
            // Late-resolve hero/heart if they spawned after this bootstrap.
            if ((_heart == null || _hero == null) && SceneManager.GetActiveScene().name == TargetScene) Hook();
            if (!_shown) return;

            // Key prompts (new Input System + legacy fallback). Runs at timeScale 0.
            var kb = Keyboard.current;
            bool retry = (kb != null && kb.rKey.wasPressedThisFrame)      || Input.GetKeyDown(KeyCode.R);
            bool exit  = (kb != null && kb.escapeKey.wasPressedThisFrame)  || Input.GetKeyDown(KeyCode.Escape);

            // Mobile/touch: poll the pointer against the button rects (owner 2026-06-02:
            // "stuck at the dead screen, i dont have an esc or b button"). uGUI buttons
            // need an EventSystem the build doesn't have, so we hit-test manually — the
            // same EventSystem-free approach as VirtualJoystick.
            if (!retry && !exit && TryGetTap(out Vector2 tap))
            {
                if (_retryBtn != null && RectTransformUtility.RectangleContainsScreenPoint(_retryBtn, tap, null)) retry = true;
                else if (_exitBtn != null && RectTransformUtility.RectangleContainsScreenPoint(_exitBtn, tap, null)) exit = true;
            }

            if (retry)     { Time.timeScale = 1f; SceneRouter.LoadScene(SceneRouter.Village); }
            else if (exit) { Time.timeScale = 1f; SceneRouter.LoadScene(SceneRouter.Title); }
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

        private void ShowHeartFell() => Show(
            "THE HEART OF ELARION HAS FALLEN",
            "The light guttered, and the dark poured in.\nBut Elarion remembers those who rise again.");

        private void ShowHeroFell() => Show(
            "YOU HAVE FALLEN",
            "The dark takes you, but Elarion still needs its defender.\nRise, and try again.");

        private void Show(string title, string body)
        {
            if (_shown) return;
            _shown = true;
            // Game-over music (owner 2026-06-02) — the somber Defeat track on BOTH
            // death contexts (hero fell + heart/tree fell), since both route through
            // this one Show(). Null-guarded cross-module call per CLAUDE.md §10.
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

            // Full-screen dark backdrop.
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(_overlay.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.0f, 0.04f, 0.88f);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

            // Narrative + key prompts (proven build-safe font path from ThreatSkullPlate).
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(_overlay.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1f, 0.86f, 0.55f);
            txt.fontSize = 38;
            txt.supportRichText = true;
            txt.text =
                "<b>" + title + "</b>\n\n" +
                body + "\n\n" +
                "Tap a button below  —  or press [ R ] / [ Esc ]";
            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0.08f, 0.40f); rt.anchorMax = new Vector2(0.92f, 0.78f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Tap buttons (mobile) — side by side under the text.
            _retryBtn = BuildTapButton("TRY AGAIN", new Color(0.18f, 0.46f, 0.24f),
                                       new Vector2(0.16f, 0.16f), new Vector2(0.48f, 0.28f));
            _exitBtn  = BuildTapButton("LEAVE",     new Color(0.46f, 0.20f, 0.20f),
                                       new Vector2(0.52f, 0.16f), new Vector2(0.84f, 0.28f));
        }

        /// <summary>A code-built tap button (Image + centred Text). Returns its RectTransform
        /// for manual hit-testing in <see cref="Update"/> (no EventSystem in builds).</summary>
        private RectTransform BuildTapButton(string label, Color bg, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(_overlay.transform, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<Text>();
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = Color.white;
            lbl.fontSize = 30;
            lbl.fontStyle = FontStyle.Bold;
            lbl.text = label;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
