// =============================================================================
// TowerManagerPanel — tower management UI. Lists every placed tower with its
// level + range/damage; selecting one highlights it in-world with a marker and
// exposes per-tower Upgrade / Raze (acts on the SELECTED tower, not the last one).
// -----------------------------------------------------------------------------
// Code-built UI (renders in player builds, unlike .uxml). Self-installs on a
// borrowed PanelSettings (the MusicToggleBootstrap pattern) and toggles with the
// M key; the BuildMenu's "Manage Towers" button also toggles it via Instance.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.UI
{
    /// <summary>Lists + manages placed towers (level, range/damage, select → upgrade/raze).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TowerManagerPanel : MonoBehaviour
    {
        public static TowerManagerPanel Instance { get; private set; }

        private VisualElement _root, _panel, _actions;
        private ScrollView _list;
        private Label _detail;
        private Tower _selected;
        private GameObject _marker;
        private bool _visible;
        private float _nextRefresh;

        // --- self-install on a borrowed PanelSettings (renders in builds) --------
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Install();

        private static void Install()
        {
            if (Instance != null) return;
            PanelSettings ps = null; float top = float.MinValue;
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (d == null || d.panelSettings == null) continue;
                if (d.sortingOrder >= top) { top = d.sortingOrder; ps = d.panelSettings; }
            }
            if (ps == null) return;

            var go = new GameObject("TowerManagerPanel");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.sortingOrder = top + 70;
            go.AddComponent<TowerManagerPanel>();
            go.SetActive(true);
        }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; ClearMarker(); }

        private void OnEnable() { BuildUi(); Hide(); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) Toggle();
            if (_visible && Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }
        }

        /// <summary>Show/hide the manager (M key, or the BuildMenu "Manage Towers" button).</summary>
        public void Toggle() { if (_visible) Hide(); else Show(); }

        public void Show() { if (_panel == null) return; _visible = true; _panel.style.display = DisplayStyle.Flex; Refresh(); }
        public void Hide() { _visible = false; if (_panel != null) _panel.style.display = DisplayStyle.None; }

        // --- UI ------------------------------------------------------------------
        private void BuildUi()
        {
            if (_panel != null) return;
            var doc = GetComponent<UIDocument>();
            _root = doc != null ? doc.rootVisualElement : null;
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore;   // only the panel captures clicks

            _panel = new VisualElement { name = "tower-manager" };
            var s = _panel.style;
            s.position = Position.Absolute; s.left = 20f; s.top = 80f; s.width = 300f;
            s.paddingTop = 14f; s.paddingBottom = 14f; s.paddingLeft = 16f; s.paddingRight = 16f;
            s.backgroundColor = new Color(0.08f, 0.10f, 0.16f, 0.96f);
            s.borderTopLeftRadius = 10f; s.borderTopRightRadius = 10f;
            s.borderBottomLeftRadius = 10f; s.borderBottomRightRadius = 10f;

            var title = new Label("Towers   (press M)");
            title.style.color = Color.white; title.style.fontSize = 18f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginBottom = 8f;
            _panel.Add(title);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.maxHeight = 260f; _list.style.marginBottom = 8f;
            _panel.Add(_list);

            _detail = new Label("Select a tower to manage.");
            _detail.style.color = new Color(0.85f, 0.85f, 0.9f); _detail.style.fontSize = 12f;
            _detail.style.whiteSpace = WhiteSpace.Normal; _detail.style.marginBottom = 6f;
            _panel.Add(_detail);

            _actions = new VisualElement();
            _panel.Add(_actions);

            _panel.Add(Btn("Close", new Color(0.30f, 0.30f, 0.36f, 0.95f), Hide));

            _root.Add(_panel);
        }

        private void Refresh()
        {
            if (_list == null) return;
            _list.Clear();

            var towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            if (towers.Length == 0)
            {
                var none = new Label("No towers placed yet.");
                none.style.color = new Color(0.8f, 0.8f, 0.85f); none.style.fontSize = 12f;
                _list.Add(none);
                _selected = null; ClearMarker();
            }
            else
            {
                // Drop a stale selection if its tower was destroyed.
                if (_selected == null) ClearMarker();

                for (int i = 0; i < towers.Length; i++)
                {
                    var t = towers[i];
                    bool sel = ReferenceEquals(t, _selected);
                    var row = Btn($"Tower {i + 1}  —  Lv {t.CurrentLevel}   (rng {t.CurrentRange:0}, dmg {t.CurrentDamage:0})",
                        sel ? new Color(0.20f, 0.42f, 0.30f, 0.98f) : new Color(0.18f, 0.22f, 0.30f, 0.95f),
                        () => Select(t));
                    row.style.height = 32f; row.style.fontSize = 12f;
                    _list.Add(row);
                }
            }
            RefreshDetail();
        }

        private void Select(Tower t) { SetMarker(t); _selected = t; Refresh(); }

        private void RefreshDetail()
        {
            if (_actions == null) return;
            _actions.Clear();

            if (_selected == null) { if (_detail != null) _detail.text = "Select a tower to manage."; return; }

            if (_detail != null)
                _detail.text = $"Selected: Lv {_selected.CurrentLevel}/{Tower.MaxLevel}   •   rng {_selected.CurrentRange:0}   dmg {_selected.CurrentDamage:0}";

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var up = Btn("Upgrade", new Color(0.30f, 0.52f, 0.34f, 0.95f), () =>
            {
                if (_selected != null) { _selected.Upgrade(); Refresh(); }
            });
            up.style.flexGrow = 1f; up.style.marginRight = 4f;
            row.Add(up);

            var raze = Btn("Raze", new Color(0.62f, 0.20f, 0.20f, 0.95f), () =>
            {
                if (_selected != null) { Destroy(_selected.gameObject); _selected = null; ClearMarker(); Refresh(); }
            });
            raze.style.flexGrow = 1f;
            row.Add(raze);

            _actions.Add(row);
        }

        // --- in-world selection marker (bright unlit sphere above the tower) -----
        private void SetMarker(Tower t)
        {
            ClearMarker();
            if (t == null) return;

            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "TowerSelectionMarker";
            var col = _marker.GetComponent<Collider>(); if (col != null) Destroy(col);
            _marker.transform.SetParent(t.transform, false);

            var fp = t.transform.Find("FirePoint");
            float y = (fp != null ? fp.localPosition.y : 3.5f) + 0.9f;
            _marker.transform.localPosition = new Vector3(0f, y, 0f);
            _marker.transform.localScale = Vector3.one * 0.5f;

            var rend = _marker.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                var mat = new Material(sh);
                var amber = new Color(1f, 0.85f, 0.2f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", amber); else mat.color = amber;
                rend.sharedMaterial = mat;
            }
        }

        private void ClearMarker() { if (_marker != null) Destroy(_marker); _marker = null; }

        private static Button Btn(string label, Color bg, System.Action onClick)
        {
            var b = new Button(onClick) { text = label };
            var s = b.style;
            s.height = 38f; s.marginTop = 3f; s.marginBottom = 3f;
            s.fontSize = 14f; s.unityFontStyleAndWeight = FontStyle.Bold;
            s.unityTextAlign = TextAnchor.MiddleCenter; s.color = Color.white;
            s.backgroundColor = bg;
            s.borderTopLeftRadius = 7f; s.borderTopRightRadius = 7f;
            s.borderBottomLeftRadius = 7f; s.borderBottomRightRadius = 7f;
            return b;
        }
    }
}
