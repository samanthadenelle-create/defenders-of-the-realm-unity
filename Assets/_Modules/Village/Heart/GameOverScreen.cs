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
            if (retry)     { Time.timeScale = 1f; SceneRouter.LoadScene(SceneRouter.Village); }
            else if (exit) { Time.timeScale = 1f; SceneRouter.LoadScene(SceneRouter.Title); }
        }

        private void ShowHeartFell() => Show(
            "THE HEART OF ELARION HAS FALLEN",
            "The light guttered, and the dark poured in.\nBut Elarion remembers those who rise again.");

        private void ShowHeroFell() => Show(
            "YOU HAVE FALLEN",
            "The dark takes you — but Elarion still needs its defender.\nRise, and try again.");

        private void Show(string title, string body)
        {
            if (_shown) return;
            _shown = true;
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
                "[ R ]  Try Again         [ Esc ]  Leave to Title";
            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0.08f, 0.28f); rt.anchorMax = new Vector2(0.92f, 0.72f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
