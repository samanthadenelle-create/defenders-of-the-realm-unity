// =============================================================================
// GameOverScreen — "The Heart of Elarion Has Fallen" overlay when the Heart is
// destroyed (defense lost). DEF-125 (the "tree died" half).
// -----------------------------------------------------------------------------
// Code-built uGUI (screen-space Canvas + Text via the proven ThreatSkullPlate
// font path — NO UXML, which doesn't render in player builds). KEYBOARD prompts,
// not buttons, so there's no EventSystem/click plumbing to fail in WebGL:
//   [R] Try Again (reload Village)   ·   [Esc] Leave to Title
// Pauses the game (timeScale 0; Update still runs unscaled to read input).
// Self-bootstrapping DDOL; hooks HeartController.OnHeartDestroyed on Village load.
//
// SCOPE: this is the HEART (game-over) case only — unambiguous, no conflict with
// the hero's auto-respawn (DEF-102). The hero-death "You Fell" beat is a separate
// design call (keep respawn vs screen) tracked in DEF-125. Narrative copy is a
// stub — creative refines.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DeNelle.Core;

namespace DeNelle.Village
{
    /// <summary>Heart-fell game-over overlay with [R] retry / [Esc] exit key prompts.</summary>
    public sealed class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }
        private const string TargetScene = "Village";

        private HeartController _heart;
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
            if (scene.name == TargetScene) Hook();
        }

        private void Hook()
        {
            if (_heart == null) _heart = FindFirstObjectByType<HeartController>();
            if (_heart != null)
            {
                _heart.OnHeartDestroyed -= ShowGameOver;
                _heart.OnHeartDestroyed += ShowGameOver;
            }
        }

        private void Update()
        {
            // Late-resolve the Heart if it spawned after this bootstrap.
            if (_heart == null && SceneManager.GetActiveScene().name == TargetScene) Hook();
            if (!_shown) return;

            // Key prompts (new Input System + legacy fallback). Runs at timeScale 0.
            var kb = Keyboard.current;
            bool retry = (kb != null && kb.rKey.wasPressedThisFrame)     || Input.GetKeyDown(KeyCode.R);
            bool exit  = (kb != null && kb.escapeKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Escape);
            if (retry)     { Time.timeScale = 1f; SceneRouter.LoadScene(SceneRouter.Village); }
            else if (exit) { Time.timeScale = 1f; SceneRouter.LoadScene(SceneRouter.Title); }
        }

        private void ShowGameOver()
        {
            if (_shown) return;
            _shown = true;
            Time.timeScale = 0f;
            BuildOverlay();
        }

        private void BuildOverlay()
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
                "<b>THE HEART OF ELARION HAS FALLEN</b>\n\n" +
                "The light guttered, and the dark poured in.\n" +
                "But Elarion remembers those who rise again.\n\n" +
                "[ R ]  Try Again         [ Esc ]  Leave to Title";
            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0.08f, 0.28f); rt.anchorMax = new Vector2(0.92f, 0.72f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
