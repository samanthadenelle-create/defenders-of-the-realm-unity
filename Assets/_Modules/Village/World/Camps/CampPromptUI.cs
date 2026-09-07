// =============================================================================
// CampPromptUI - the code-built claim prompt + build menu for the camp loop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Self-bootstraps ONLY when CampSystem.Enabled (ships dark otherwise). One DDOL
// instance drives every camp. MVVM (Silo G): this View reads NO game state — it
// binds a CampPromptVM and delegates all scene reads to CampProximityService:
//   * Each frame CampProximityService finds the nearest CLEARED-but-unclaimed camp
//     within ClaimRange of the hero (tag "Player", then HeroLocomotion); the VM projects
//     whether to show the world-space "Claim" prompt. Tap it -> vm.ClaimCurrent().
//   * On claim, the VM opens the build menu; the View paints the code-built pick-a-
//     building menu (Watchtower / Lumber Outpost / Farm Outpost). Tap -> vm.Build(type).
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

        // MVVM: the VM owns the prompt/menu STATE; the service owns the scene reads
        // (hero find, proximity, world->screen). The View reads neither directly.
        private CampPromptVM _vm;
        private CampProximityService _proximity;

        private Canvas _canvas;
        private RectTransform _promptRect;   // the "Claim" tap button
        private Text _promptLabel;

        // Build menu UI (state lives in the VM; this is just the built menu root).
        private GameObject _menuRoot;
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
            _proximity = new CampProximityService { ClaimRange = ClaimRange };
            _vm = new CampPromptVM(_proximity);
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!CampSystem.Enabled) { HidePrompt(); return; }

            _proximity.ClaimRange = ClaimRange;   // keep the service in sync with the inspector field
            _vm.Tick();

            // Sync the built menu root to the VM's menu state (VM owns open/close).
            SyncMenu();

            // If a build menu is open, it owns input until a choice is made.
            if (_vm.MenuOpen)
            {
                HidePrompt();
                HandleMenuInput();
                return;
            }

            if (!_vm.ShowPrompt) { HidePrompt(); return; }

            ShowPrompt();

            // Mobile-first: claim on a tap on the prompt button. The [E] key trigger was removed.
            bool tapHit = TryGetTap(out Vector2 tap) && _promptRect != null &&
                          RectTransformUtility.RectangleContainsScreenPoint(_promptRect, tap, null);

            if (tapHit)
            {
                _vm.ClaimCurrent();   // claims the camp + opens the build menu (VM state)
                HidePrompt();
                SyncMenu();
            }
        }

        // =====================================================================
        // Claim prompt (single reusable label/button, positioned at the camp).
        // =====================================================================
        private void ShowPrompt()
        {
            if (_promptRect == null) return;

            // Project the VM's prompt anchor to screen space (via the service).
            if (!_proximity.TryProject(_vm.PromptWorldAnchor, out Vector2 sp))
            {
                _promptRect.gameObject.SetActive(false);   // behind camera -> hide
                return;
            }

            _promptRect.gameObject.SetActive(true);
            _promptRect.position = new Vector3(sp.x, sp.y, 0f);
            if (_promptLabel != null)
                _promptLabel.text = _vm.PromptText;
        }

        private void HidePrompt()
        {
            if (_promptRect != null) _promptRect.gameObject.SetActive(false);
        }

        // =====================================================================
        // Build menu.
        // =====================================================================
        /// <summary>Mirrors the built menu root's visibility to the VM's menu state.</summary>
        private void SyncMenu()
        {
            if (_vm.MenuOpen)
            {
                if (_menuRoot == null) BuildMenu();
                if (!_menuRoot.activeSelf) _menuRoot.SetActive(true);
            }
            else if (_menuRoot != null && _menuRoot.activeSelf)
            {
                _menuRoot.SetActive(false);
            }
        }

        private void HandleMenuInput()
        {
            if (!TryGetTap(out Vector2 tap)) return;
            for (int i = 0; i < _menuButtons.Count; i++)
            {
                var (rect, type) = _menuButtons[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, tap, null))
                {
                    _vm.Build(type);   // builds on the menu's camp + closes the menu (VM state)
                    SyncMenu();
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
            img.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/buttons/button-normal-empty");
            img.type = Image.Type.Sliced;
            img.color = Color.white;
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
            bg.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
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
            img.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/buttons/button-disabled-empty");
            img.type = Image.Type.Sliced;
            img.color = Color.white;
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
