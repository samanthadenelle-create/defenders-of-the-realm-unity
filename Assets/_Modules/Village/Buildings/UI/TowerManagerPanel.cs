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
using UnityEngine.UI;
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
        private GameObject _listViewport;     // WO-795 scroll well (survives Refresh clears)
        private Transform _listContent;       // scroll Content — fixed-height row hosts
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

            BuildTowerScrollWell();

            // Footer strip carries the selected-tower readout.
            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer
                : _modal.chrome.content.transform;
            _detail = MakeText(footHost, "Select a tower to manage.", 13, ElarionUi.ParchmentDim,
                FontStyles.Italic, TextAlignmentOptions.Center,
                new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));

            _modal.canvas.SetActive(false);   // built hidden; Show shows it
        }

        // -- Tower list scroll well (WO-795: rows never truncate; overflow scrolls) --

        private const float RowPixelH = 112f;  // fixed row height (tappable rows: min touch target)

        /// <summary>Build the vertical scroll well for the tower list, ONCE per build
        /// (RumorBoardPanel WO-795 pattern): Viewport (near-invisible Image drag catcher
        /// + RectMask2D) + top-anchored Content (VerticalLayoutGroup + ContentSizeFitter).
        /// Refresh only clears/refills the Content, so scroll position survives the
        /// 0.5s live refresh. The action row stays OUTSIDE, on the body itself.</summary>
        private void BuildTowerScrollWell()
        {
            if (_bodyHost == null) return;

            var viewportGo = new GameObject("TowerListViewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(_bodyHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            // The old list band ran 0.97 down to the 0.24 truncation floor; the well
            // occupies that same band (action row keeps 0.03-0.13 below, untouched).
            vpr.anchorMin = new Vector2(0.06f, 0.16f);
            vpr.anchorMax = new Vector2(0.94f, 0.97f);
            vpr.offsetMin = Vector2.zero;
            vpr.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            var contentGo = new GameObject("TowerListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(0.5f, 1f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true; vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            // Bottom pad = one row so the last tower scrolls fully clear of the mask.
            vlg.padding = new RectOffset(0, 0, 0, (int)RowPixelH + 8);
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content  = cr;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _listViewport = viewportGo;
            _listContent  = contentGo.transform;
        }

        private void Refresh()
        {
            if (_bodyHost == null || _listContent == null) return;

            // Clear the body EXCEPT the persistent scroll well (so the ScrollRect and
            // its scroll position survive the 0.5s live refresh), then clear the rows
            // inside the Content. The action row + empty-state live directly on the
            // body and are rebuilt each pass, exactly as before.
            for (int i = _bodyHost.childCount - 1; i >= 0; i--)
            {
                var child = _bodyHost.GetChild(i).gameObject;
                if (child == _listViewport) continue;
                Destroy(child);
            }
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

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

                // WO-795: fixed-height LayoutElement row hosts inside the scroll
                // Content — EVERY tower lists; overflow scrolls, never truncates.
                for (int i = 0; i < towers.Count; i++)
                {
                    var t = towers[i];
                    bool sel = ReferenceEquals(t, _vm.Selected);
                    // ASCII-only label (no glyphs — missing from the TMP font).
                    string label = PlacedTowerListVM.FormatManagerRow(
                        i + 1, t.CurrentLevel, t.CurrentRange, t.CurrentDamage, sel);
                    Tower captured = t;

                    // Fixed-height row host; the kit button fills it (anchors 0..1).
                    var host = new GameObject("Row_" + (i + 1), typeof(RectTransform), typeof(LayoutElement));
                    host.transform.SetParent(_listContent, false);
                    var le = host.GetComponent<LayoutElement>();
                    le.preferredHeight = RowPixelH;
                    le.minHeight = RowPixelH;

                    ElarionUiKit.BuildObsidianButton(host.transform, label,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        // Selected row reads with the Yellow accent (selection canon).
                        sel ? ElarionUiKit.ObsidianButtonColor.Yellow
                            : ElarionUiKit.ObsidianButtonColor.Gray,
                        Vector2.zero, Vector2.one,
                        () => Select(captured));
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
