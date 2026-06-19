// =============================================================================
// CampPromptUI - the code-built claim prompt + build menu for the camp loop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Self-bootstraps ONLY when CampSystem.Enabled (ships dark otherwise). One DDOL
// instance drives every camp:
//   * Each frame find the nearest CLEARED-but-unclaimed camp within ClaimRange of
//     the hero (tag "Player" or "HeroTarget"). If found, show a world-space
//     "Claim" prompt. Tap it (or press [E]) -> camp.Claim().
//   * On claim, show a small code-built pick-a-building menu (Watchtower / Lumber
//     Outpost / Farm Outpost). Tapping a choice -> camp.BuildOutpost(type).
//
// Build-safe: LegacyRuntime.ttf font, NO UXML, NO EventSystem - pointer/touch is
// polled and hit-tested manually (the proven GameOverScreen / VirtualJoystick
// pattern). World-space prompt is a screen-space label positioned at the camp's
// projected screen point so no world-space-canvas plumbing is needed.
// ASCII-only strings. Canon: village is Elarion.
// =============================================================================
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.World.Camps
{
    /// <summary>Drives the hero-proximity claim prompt + the build menu for all camps.</summary>
    public sealed class CampPromptUI : MonoBehaviour
    {
        public static CampPromptUI Instance { get; private set; }

        [Tooltip("Hero must be within this many metres of a cleared camp to claim.")]
        public float ClaimRange = 7f;

        private Transform _hero;
        private Camera _cam;

        private Canvas _canvas;
        private RectTransform _promptRect;   // the "Claim" tap button
        private Text _promptLabel;
        private ClaimableCamp _promptCamp;    // camp the prompt currently targets

        // Build menu state.
        private GameObject _menuRoot;
        private ClaimableCamp _menuCamp;
        private readonly System.Collections.Generic.List<(RectTransform rect, OutpostType type)> _menuButtons =
            new System.Collections.Generic.List<(RectTransform, OutpostType)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!CampSystem.Enabled) return;   // SHIPS DARK.
            if (Instance != null) return;
            new GameObject("CampPromptUI").AddComponent<CampPromptUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!CampSystem.Enabled) { HidePrompt(); return; }

            EnsureRefs();

            // If a build menu is open, it owns input until a choice is made.
            if (_menuRoot != null && _menuRoot.activeSelf)
            {
                HandleMenuInput();
                return;
            }

            ClaimableCamp near = FindClaimableCamp();
            if (near == null) { HidePrompt(); return; }

            ShowPrompt(near);

            // Mobile-first: claim on a tap on the prompt button. The [E] key trigger was removed.
            bool tapHit = TryGetTap(out Vector2 tap) && _promptRect != null &&
                          RectTransformUtility.RectangleContainsScreenPoint(_promptRect, tap, null);

            if (tapHit)
            {
                near.Claim();
                HidePrompt();
                OpenBuildMenu(near);
            }
        }

        // =====================================================================
        // Proximity search.
        // =====================================================================
        private ClaimableCamp FindClaimableCamp()
        {
            if (_hero == null) return null;
            ClaimableCamp best = null;
            float bestSqr = ClaimRange * ClaimRange;
            var camps = CampSystem.Camps;
            for (int i = 0; i < camps.Count; i++)
            {
                var c = camps[i];
                if (c == null || !c.Cleared || c.Claimed) continue;
                float sqr = (c.transform.position - _hero.position).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = c; }
            }
            return best;
        }

        private void EnsureRefs()
        {
            if (_hero == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p == null)
                {
                    // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
                    var ht = SafeFindWithTag("HeroTarget");
                    if (ht != null) p = ht;
                }
                _hero = p != null ? p.transform : null;
            }
            if (_cam == null) _cam = Camera.main;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        // =====================================================================
        // Claim prompt (single reusable label/button, positioned at the camp).
        // =====================================================================
        private void ShowPrompt(ClaimableCamp camp)
        {
            _promptCamp = camp;
            if (_promptRect == null) return;

            // Project the camp's head position to screen space.
            if (_cam == null) _cam = Camera.main;
            Vector3 world = camp.transform.position + Vector3.up * 2.2f;
            Vector3 sp = _cam != null ? _cam.WorldToScreenPoint(world) : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);

            // Behind camera -> hide.
            if (sp.z < 0f) { _promptRect.gameObject.SetActive(false); return; }

            _promptRect.gameObject.SetActive(true);
            _promptRect.position = new Vector3(sp.x, sp.y, 0f);
            if (_promptLabel != null)
                _promptLabel.text = "[ Tap ]  Claim Camp";
        }

        private void HidePrompt()
        {
            _promptCamp = null;
            if (_promptRect != null) _promptRect.gameObject.SetActive(false);
        }

        // =====================================================================
        // Build menu.
        // =====================================================================
        private void OpenBuildMenu(ClaimableCamp camp)
        {
            _menuCamp = camp;
            if (_menuRoot == null) BuildMenu();
            _menuRoot.SetActive(true);
        }

        private void CloseBuildMenu()
        {
            _menuCamp = null;
            if (_menuRoot != null) _menuRoot.SetActive(false);
        }

        private void HandleMenuInput()
        {
            if (!TryGetTap(out Vector2 tap)) return;
            for (int i = 0; i < _menuButtons.Count; i++)
            {
                var (rect, type) = _menuButtons[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, tap, null))
                {
                    _menuCamp?.BuildOutpost(type);
                    CloseBuildMenu();
                    return;
                }
            }
        }

        // =====================================================================
        // Code-built uGUI (build-safe font, no EventSystem).
        // =====================================================================
        private void BuildCanvas()
        {
            var go = new GameObject("CampUICanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            // The reusable claim prompt button.
            var btnGo = new GameObject("ClaimPrompt");
            btnGo.transform.SetParent(_canvas.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.10f, 0.30f, 0.16f, 0.92f);
            _promptRect = img.rectTransform;
            _promptRect.sizeDelta = new Vector2(280f, 64f);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(btnGo.transform, false);
            _promptLabel = lblGo.AddComponent<Text>();
            _promptLabel.font = BuiltinFont();
            _promptLabel.alignment = TextAnchor.MiddleCenter;
            _promptLabel.color = Color.white;
            _promptLabel.fontSize = 24;
            _promptLabel.fontStyle = FontStyle.Bold;
            _promptLabel.text = "[ Tap ]  Claim Camp";
            var lrt = _promptLabel.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            _promptRect.gameObject.SetActive(false);
        }

        private void BuildMenu()
        {
            _menuRoot = new GameObject("BuildMenu");
            _menuRoot.transform.SetParent(_canvas.transform, false);
            var bg = _menuRoot.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0.30f, 0.28f);
            bgRt.anchorMax = new Vector2(0.70f, 0.72f);
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

            // Title.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_menuRoot.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.font = BuiltinFont();
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.86f, 0.55f);
            title.fontSize = 26;
            title.fontStyle = FontStyle.Bold;
            title.text = "Build an Outpost";
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.05f, 0.80f); trt.anchorMax = new Vector2(0.95f, 0.97f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            _menuButtons.Clear();
            AddMenuButton("Watchtower  (+Iron)",      OutpostType.Watchtower,    0.62f, 0.78f, new Color(0.22f, 0.30f, 0.42f));
            AddMenuButton("Lumber Outpost  (+Wood)",  OutpostType.LumberOutpost, 0.42f, 0.58f, new Color(0.38f, 0.26f, 0.14f));
            AddMenuButton("Farm Outpost  (+Stone)",   OutpostType.FarmOutpost,   0.22f, 0.38f, new Color(0.40f, 0.38f, 0.20f));

            _menuRoot.SetActive(false);
        }

        private void AddMenuButton(string label, OutpostType type, float yMin, float yMax, Color color)
        {
            var go = new GameObject("Btn_" + type);
            go.transform.SetParent(_menuRoot.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.08f, yMin); rt.anchorMax = new Vector2(0.92f, yMax);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<Text>();
            lbl.font = BuiltinFont();
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = Color.white;
            lbl.fontSize = 22;
            lbl.fontStyle = FontStyle.Bold;
            lbl.text = label;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            _menuButtons.Add((rt, type));
        }

        private static Font BuiltinFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
    }
}
