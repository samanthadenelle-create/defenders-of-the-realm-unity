// =============================================================================
// TowerManagerPanel — tower management UI. Lists every placed tower with its
// level + range/damage; selecting one highlights it in-world with a marker and
// exposes per-tower Upgrade / Raze (acts on the SELECTED tower, not the last one).
// -----------------------------------------------------------------------------
// WO-D conversion (2026-07-03, coverage matrix row #40): UIDocument/UITK card ->
// code-built uGUI on the Obsidian master frame (BuildObsidianModal: FrameCore +
// shield medallion + the ONE shared Close), per the HelpMenu reference recipe.
// Self-install no longer needs a borrowed PanelSettings (that was a UIDocument
// requirement) — the kit modal owns its canvas. Contracts preserved: Instance,
// Toggle()/Show()/Hide(), the PanelManager "Tower Manager" arbiter handle, the
// 0.5s live refresh, the in-world selection marker, TryUpgrade/Raze verbs.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>Lists + manages placed towers (level, range/damage, select → upgrade/raze).</summary>
    [DisallowMultipleComponent]
    public sealed class TowerManagerPanel : MonoBehaviour
    {
        public static TowerManagerPanel Instance { get; private set; }

        private ElarionUiKit.ObsidianModal _modal;
        private Transform _bodyHost;          // frame body drop-zone — rows + actions
        private TextMeshProUGUI _detail;      // footer strip — selected-tower readout
        private GameObject _marker;
        private bool _visible;
        private float _nextRefresh;

        // MVVM Silo C — the tower list VM owns the placed-tower scan + the selection +
        // the upgrade/raze commands. This View reads only the VM's list/selection.
        private PlacedTowerListVM _vm;

        // PanelManager mutual-exclusion handle (one panel at a time).
        private PanelHandle _panelHandle;

        // --- self-install (kit modal — no PanelSettings needed any more) ---------
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
            var go = new GameObject("TowerManagerPanel");
            go.AddComponent<TowerManagerPanel>();
        }

        private void Awake()
        {
            Instance = this;
            _vm = PlacedTowerListVM.CreateDefault(Hide);
            // Register with the modal arbiter so opening this closes any other panel
            // (and vice-versa). Probe = the panel's own visibility flag.
            _panelHandle = PanelManager.Register("Tower Manager", Hide, () => _visible);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearMarker();
            _vm?.Dispose();
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        private void Update()
        {
            // Mobile-first: Tower management opens via the BuildMenu "Manage Towers"
            // button (a build-mode activity). The desktop 'M' key trigger was removed.
            if (_visible && Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }
        }

        /// <summary>Show/hide the manager (via the BuildMenu "Manage Towers" button).</summary>
        public void Toggle() { if (_visible) Hide(); else Show(); }

        public void Show()
        {
            EnsureBuilt();
            if (_modal == null || _modal.canvas == null) return;
            _visible = true;
            _modal.canvas.SetActive(true);
            // Announce open: closes any previously-open panel. Battle-lock may reject —
            // revert and stay hidden (VillageCraftingPanel pattern).
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                _visible = false;
                _modal.canvas.SetActive(false);
                return;
            }
            Refresh();
        }

        public void Hide()
        {
            _visible = false;
            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);
            PanelManager.NotifyClosed(_panelHandle);
        }

        // --- UI (kit modal, lazy on first Show) ------------------------------------
        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            _modal = ElarionUiKit.BuildObsidianModal("TowerManagerUI", "Towers",
                new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f), Hide,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "shield");

            var layout = _modal.chrome.layout;
            _bodyHost = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // Footer strip carries the selected-tower readout.
            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer
                : _modal.chrome.content.transform;
            _detail = MakeText(footHost, "Select a tower to manage.", 13, ElarionUi.ParchmentDim,
                FontStyles.Italic, TextAlignmentOptions.Center,
                new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));

            _modal.canvas.SetActive(false);   // built hidden; Show shows it
        }

        private void Refresh()
        {
            if (_bodyHost == null) return;
            for (int i = _bodyHost.childCount - 1; i >= 0; i--)
                Destroy(_bodyHost.GetChild(i).gameObject);

            _vm.Refresh();                       // re-poll the live towers (drops a stale selection)
            var towers = _vm.Towers;
            if (towers.Count == 0)
            {
                MakeText(_bodyHost, "No towers placed yet.", 14, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.95f));
                ClearMarker();
            }
            else
            {
                // Drop a stale selection marker if its tower was destroyed.
                if (_vm.Selected == null) ClearMarker();

                const float rowH = 0.08f, gap = 0.014f;
                float top = 0.97f;
                for (int i = 0; i < towers.Count; i++)
                {
                    var t = towers[i];
                    bool sel = ReferenceEquals(t, _vm.Selected);
                    // ASCII-only label (no glyphs — missing from the TMP font).
                    string label = PlacedTowerListVM.FormatManagerRow(
                        i + 1, t.CurrentLevel, t.CurrentRange, t.CurrentDamage, sel);
                    Tower captured = t;
                    ElarionUiKit.BuildObsidianButton(_bodyHost, label,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        // Selected row reads with the Yellow accent (selection canon).
                        sel ? ElarionUiKit.ObsidianButtonColor.Yellow
                            : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.06f, top - rowH), new Vector2(0.94f, top),
                        () => Select(captured));
                    top -= rowH + gap;
                    if (top < 0.24f) break;   // bounded: leave room for the action row
                }
            }
            RefreshDetail();
        }

        private void Select(Tower t) { SetMarker(t); _vm.Select(t); Refresh(); }

        private void RefreshDetail()
        {
            if (_vm.Selected == null)
            {
                if (_detail != null) _detail.text = "Select a tower to manage.";
                return;
            }

            // Silo 3 UI: display tier + upgrade cost alongside level/stats — composed by
            // the VM (Tower.EffectiveTier / NextUpgradeCost / CurrentLevel live in the VM now).
            if (_detail != null) _detail.text = _vm.DetailLine;

            // Action row along the base of the body well.
            // DEPRECATED (owner 2026-06-27, tower-upgrade CONSOLIDATION): this Upgrade
            // button was one of three duplicate paths and called the FREE Tower.Upgrade().
            // The canonical surface is now the proximity HUD context button
            // (TowerInteractable -> HudBuildingFocus -> Tower.TryUpgrade). This button is
            // no longer free — it routes through the single cost-enforced Tower.TryUpgrade
            // (via the VM's UpgradeSelected). RAZE + SELECTION are PRESERVED (this panel's home).
            ElarionUiKit.BuildObsidianButton(_bodyHost, "Upgrade",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.10f, 0.03f), new Vector2(0.48f, 0.13f), () =>
                {
                    _vm.UpgradeSelected(); Refresh();
                });

            ElarionUiKit.BuildObsidianButton(_bodyHost, "Raze",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                new Vector2(0.52f, 0.03f), new Vector2(0.90f, 0.13f), () =>
                {
                    _vm.RazeSelected(); ClearMarker(); Refresh();
                });
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

        // ── uGUI helper (LeaderboardPanel/VillageCraftingPanel shape) ─────────
        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
