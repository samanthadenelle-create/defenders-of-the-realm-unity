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
//
// -----------------------------------------------------------------------------
// WO-880 — THE CLIP (the LAYOUT half of the ticket; the data half is in the VM).
//
// The body was sliced by FRACTION OF PARENT — the list well ran 0.16..0.97 of the
// body and the action row 0.03..0.13 — which is exactly the band class WO-841 /
// WO-852 outlawed. Two consequences, both visible in the 2026-08-04 capture:
//
//   1 [half row]  At 2340x1080 the CanvasScaler (1080x1920, match 0.5) resolves the
//                 canvas to 2120x978 ref px; this modal is anchored (0.18,0.12)-
//                 (0.82,0.88), so the panel is 1357x743 ref px. ElarionUiKit's
//                 close-band reservation raises FrameCore's body floor from 0.075 to
//                 footer.w + 0.015 = 0.300, leaving a body of 0.300..0.835 = 398 ref
//                 px. The 0.81 fractional well is then 322 px against a row PITCH of
//                 112 + 8 = 120 px: 2.68 rows. The third row is cut at 73% of its
//                 height — the hard mid-height cut in the shot.
//   2 [overlap]   The action row's 0.10-of-body band is ~40 ref px, far under the
//                 MinTouchPx(112) floor, so ElarionUiKit.ClampMinTouch grows each
//                 button SYMMETRICALLY ABOUT ITS CENTRE to 112 — ~24 px below the
//                 body (over the footer read-out) and ~24 px up into the list well.
//                 Same bug class as WO-852's Echo chips.
//
// THE FIX: the body is now a FIXED-PIXEL stack — top pad, a list well SNAPPED DOWN
// to a whole number of row pitches, a gap, and a MinTouchPx action band pinned to
// the body's bottom. No fraction of the parent survives, a row can never be half
// clipped, and the touch floor can never grow a band into its neighbour.
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
        private GameObject _actionBand;       // WO-880 fixed MinTouchPx band (survives Refresh clears)
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
                ElarionUiKit.ModalArchetype.Standard, Hide,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "shield");

            var layout = _modal.chrome.layout;
            _bodyHost = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            BuildTowerScrollWell();
            BuildActionBand();
            // Resolve the fresh canvas' rects NOW so the first Refresh snaps the well to whole
            // rows on the very first frame the panel is shown (and in the edit-mode UI capture,
            // which builds + refreshes without ever running a frame).
            Canvas.ForceUpdateCanvases();
            SnapListWellToWholeRows();

            // Footer strip carries the selected-tower readout.
            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer
                : _modal.chrome.content.transform;
            // WO-880: 13 was BELOW ElarionUiKit.FontHardFloor(20) — sub-legible on the Seeker.
            // Raised to the hard floor, which is the most the kit footer band can seat: FrameCore's
            // re-seated footer is 0.245..0.285 of the panel = ~29 ref px at the device aspect, and
            // a 20 px line box is 25 px. Going to FontFloor(30) would be a 37.5 px box and TMP
            // would CULL the whole line (the WO-866 RumorBoard lesson).
            _detail = MakeText(footHost, "Select a tower to manage.", ElarionUiKit.FontHardFloor, ElarionUi.ParchmentDim,
                FontStyles.Italic, TextAlignmentOptions.Center,
                new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));

            _modal.canvas.SetActive(false);   // built hidden; Show shows it
        }

        // -- Tower list scroll well (WO-795: rows never truncate; overflow scrolls) --
        // -- WO-880: every band below is FIXED PIXELS, never a fraction of the body. --

        /// <summary>Fixed row height (tappable rows: the kit min touch target).</summary>
        public const float RowPixelH = ElarionUiKit.MinTouchPx;
        /// <summary>Gap between two list rows (the VerticalLayoutGroup spacing).</summary>
        public const float RowGapPx = 8f;
        /// <summary>Row-to-row pitch — the unit the list well height must be a whole multiple of.</summary>
        public const float RowPitchPx = RowPixelH + RowGapPx;
        /// <summary>Pad between the body's top edge and the list well.</summary>
        public const float ListTopPadPx = 8f;
        /// <summary>Left/right inset of the list well and action band inside the body.</summary>
        public const float ListSideInsetPx = 24f;
        /// <summary>The Upgrade/Raze band height — pinned AT the touch floor so ClampMinTouch
        /// has nothing left to grow (the symmetric-growth overlap this WO fixes).</summary>
        public const float ActionBandPx = ElarionUiKit.MinTouchPx;
        /// <summary>Clearance between the list well's floor and the action band.</summary>
        public const float ActionGapPx = 12f;
        /// <summary>Pad between the action band and the body's bottom edge.</summary>
        public const float BodyBottomPadPx = 8f;

        /// <summary>
        /// PURE: how many WHOLE rows the list well may show inside a body of
        /// <paramref name="bodyHeightPx"/>, once the fixed pads + the action band are reserved.
        /// Never less than 1. The oracle drives this directly — no scene, no canvas.
        /// </summary>
        public static int WholeRowsThatFit(float bodyHeightPx)
        {
            float available = bodyHeightPx - ListTopPadPx - ActionGapPx - ActionBandPx - BodyBottomPadPx;
            int rows = Mathf.FloorToInt((available + RowGapPx) / RowPitchPx);
            return rows < 1 ? 1 : rows;
        }

        /// <summary>
        /// PURE: the list well height for a body of <paramref name="bodyHeightPx"/> — an EXACT
        /// whole number of row pitches (minus the trailing gap), so the mask edge always lands
        /// on a row boundary and a row can never be cut mid-height.
        /// </summary>
        public static float SnappedWellHeightPx(float bodyHeightPx)
            => WholeRowsThatFit(bodyHeightPx) * RowPitchPx - RowGapPx;

        /// <summary>Build the vertical scroll well for the tower list, ONCE per build
        /// (RumorBoardPanel WO-795 pattern): Viewport (near-invisible Image drag catcher
        /// + RectMask2D) + top-anchored Content (VerticalLayoutGroup + ContentSizeFitter).
        /// Refresh only clears/refills the Content, so scroll position survives the
        /// 0.5s live refresh. The action band stays OUTSIDE, in its own fixed band.</summary>
        private void BuildTowerScrollWell()
        {
            if (_bodyHost == null) return;

            var viewportGo = new GameObject("TowerListViewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(_bodyHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            // TOP-anchored, horizontally stretched, PIXEL height. SnapListWellToWholeRows sets
            // the height once the body's rect is known; this seeds one row so the rect is valid
            // even if a layout pass has not run yet.
            vpr.anchorMin = new Vector2(0f, 1f);
            vpr.anchorMax = new Vector2(1f, 1f);
            vpr.pivot     = new Vector2(0.5f, 1f);
            vpr.offsetMax = new Vector2(-ListSideInsetPx, -ListTopPadPx);
            vpr.offsetMin = new Vector2(ListSideInsetPx, -(ListTopPadPx + RowPixelH));
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
            vlg.spacing = RowGapPx;
            // WO-880: NO trailing pad any more. The old pad existed because the well was a
            // fraction of the body and the last row could land under the mask edge; the well is
            // now an exact whole number of row pitches, so the final row always ends ON the mask
            // boundary. A pad here would only add phantom scroll travel below the last tower.
            vlg.padding = new RectOffset(0, 0, 0, 0);
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
            viewportGo.AddComponent<WellRowSnapper>().Bind(this);
        }

        /// <summary>
        /// Re-runs <see cref="SnapListWellToWholeRows"/> whenever the well's rect changes shape —
        /// i.e. whenever the canvas is resized. Needed because the headless UI capture builds the
        /// modal ONCE and then renders it at two aspects (1920x1080 and 2340x1080); a well snapped
        /// only at build time would ship a part row in the second shot, which is the very thing
        /// this WO removes. [ExecuteAlways] so it also fires during that EDIT-mode capture.
        ///
        /// NOTE (consolidation, not a duplicate by choice): WO-882 landed an equivalent
        /// <c>DeNelle.HUD.ScrollWellRowSnap</c>, but it lives in the HUD assembly and CLAUDE.md §5
        /// forbids Village -> HUD. When one of the two is promoted to DeNelle.Core.UI, both panels
        /// should bind THAT and these ~20 lines should go.
        /// </summary>
        [ExecuteAlways]
        [DisallowMultipleComponent]
        [RequireComponent(typeof(RectTransform))]
        private sealed class WellRowSnapper : MonoBehaviour
        {
            private TowerManagerPanel _owner;
            public void Bind(TowerManagerPanel owner) { _owner = owner; }

            // Re-entrant by design: the snap writes offsetMin/offsetMax, which fires this again.
            // SnapListWellToWholeRows short-circuits once the well is already at the snapped
            // height, so the recursion stops at depth 1.
            private void OnRectTransformDimensionsChange()
            {
                if (_owner != null) _owner.SnapListWellToWholeRows();
            }
        }

        /// <summary>
        /// WO-880 — the Upgrade/Raze band: a BOTTOM-anchored, horizontally stretched band of
        /// EXACTLY <see cref="ActionBandPx"/> (= ElarionUiKit.MinTouchPx). Built ONCE and
        /// preserved across Refresh; RefreshDetail only refills its children. Because the band is
        /// already at the touch floor, ClampMinTouch has nothing to grow — the old ~40 px
        /// fraction band was grown symmetrically about its centre into BOTH the list well above
        /// and the footer read-out below (the WO-852 Echo-chip failure mode).
        /// </summary>
        private void BuildActionBand()
        {
            if (_bodyHost == null) return;

            var bandGo = new GameObject("TowerActionBand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bandGo.transform.SetParent(_bodyHost, false);
            var br = bandGo.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(1f, 0f);
            br.pivot     = new Vector2(0.5f, 0f);
            br.offsetMin = new Vector2(ListSideInsetPx, BodyBottomPadPx);
            br.offsetMax = new Vector2(-ListSideInsetPx, BodyBottomPadPx + ActionBandPx);

            var hlg = bandGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childControlWidth  = true; hlg.childForceExpandWidth  = true;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            _actionBand = bandGo;
        }

        /// <summary>
        /// WO-880 — pin the list well to a WHOLE number of row pitches for the body's CURRENT
        /// pixel height, so the RectMask2D edge always lands on a row boundary. Cheap (two rect
        /// writes) and idempotent, so the 0.5s Refresh can call it; a body whose rect has not
        /// resolved yet (height ~0 on the very first build) is simply left for the next pass.
        /// </summary>
        private void SnapListWellToWholeRows()
        {
            if (_listViewport == null) return;
            var bodyRt = _bodyHost as RectTransform;
            if (bodyRt == null) return;

            float bodyH = bodyRt.rect.height;
            if (bodyH <= 1f) return;   // layout not resolved yet — retry on the next Refresh

            float wellH = SnappedWellHeightPx(bodyH);
            var vpr = (RectTransform)_listViewport.transform;
            if (Mathf.Approximately(vpr.offsetMin.y, -(ListTopPadPx + wellH))) return;   // already snapped
            vpr.offsetMax = new Vector2(-ListSideInsetPx, -ListTopPadPx);
            vpr.offsetMin = new Vector2(ListSideInsetPx, -(ListTopPadPx + wellH));
        }

        private void Refresh()
        {
            if (_bodyHost == null || _listContent == null) return;

            // Clear the body EXCEPT the two PERSISTENT bands — the scroll well (so the
            // ScrollRect and its scroll position survive the 0.5s live refresh) and the
            // fixed action band (WO-880; rebuilding it per pass is what let a fraction
            // band be re-stamped every refresh). The empty-state text lives directly on
            // the body and is rebuilt each pass.
            for (int i = _bodyHost.childCount - 1; i >= 0; i--)
            {
                var child = _bodyHost.GetChild(i).gameObject;
                if (child == _listViewport || child == _actionBand) continue;
                Destroy(child);
            }
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            SnapListWellToWholeRows();           // whole-row well before anything is stamped into it

            _vm.Refresh();                       // re-poll the live towers (drops a stale selection)
            var rows = _vm.Rows;
            if (rows.Count == 0)
            {
                MakeText(_bodyHost, "No towers placed yet.", ElarionUiKit.FontFloor, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    ListSideInsetPx, ListTopPadPx, RowPixelH);
                ClearMarker();
            }
            else
            {
                // Drop a stale selection marker if its tower was destroyed.
                if (_vm.SelectedRow == null) ClearMarker();

                // WO-795: fixed-height LayoutElement row hosts inside the scroll
                // Content — EVERY tower lists; overflow scrolls, never truncates.
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    bool sel = ReferenceEquals(row, _vm.SelectedRow);
                    // ASCII-only label (no glyphs — missing from the TMP font), COMPOSED BY THE
                    // VM: this View never reads a tower's level/range/damage off the scene object.
                    string label = _vm.ManagerRowFor(row, i + 1);
                    PlacedTowerRow captured = row;

                    // Fixed-height row host; the kit button fills it (anchors 0..1).
                    var host = new GameObject("Row_" + (i + 1), typeof(RectTransform), typeof(LayoutElement));
                    host.transform.SetParent(_listContent, false);
                    var le = host.GetComponent<LayoutElement>();
                    le.preferredHeight = RowPixelH;
                    le.minHeight = RowPixelH;

                    ElarionUiKit.BuildObsidianButton(host.transform, label,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        // Selected row reads with the Yellow accent (selection canon) AND the
                        // leading "> " the VM puts in the label — never colour alone.
                        sel ? ElarionUiKit.ObsidianButtonColor.Yellow
                            : ElarionUiKit.ObsidianButtonColor.Gray,
                        Vector2.zero, Vector2.one,
                        () => Select(captured));
                }
            }
            RefreshDetail();
        }

        private void Select(PlacedTowerRow row)
        {
            SetMarker(row != null ? row.Go : null);
            _vm.SelectRow(row);
            Refresh();
        }

        private void RefreshDetail()
        {
            // The action band is persistent; its CHILDREN are rebuilt per pass.
            if (_actionBand != null)
                for (int i = _actionBand.transform.childCount - 1; i >= 0; i--)
                    Destroy(_actionBand.transform.GetChild(i).gameObject);

            if (_vm.SelectedRow == null)
            {
                if (_detail != null) _detail.text = "Select a tower to manage.";
                return;
            }

            // Silo 3 UI: display tier + upgrade cost alongside level/stats — composed by
            // the VM (Tower.EffectiveTier / NextUpgradeCost / CurrentLevel live in the VM now).
            if (_detail != null) _detail.text = _vm.DetailLine;
            if (_actionBand == null) return;

            // Action band along the base of the body well (fixed MinTouchPx band, WO-880).
            // DEPRECATED (owner 2026-06-27, tower-upgrade CONSOLIDATION): this Upgrade
            // button was one of three duplicate paths and called the FREE Tower.Upgrade().
            // The canonical surface is now the proximity HUD context button
            // (TowerInteractable -> HudBuildingFocus -> Tower.TryUpgrade). This button is
            // no longer free — it routes through the single cost-enforced Tower.TryUpgrade
            // (via the VM's UpgradeSelected). RAZE + SELECTION are PRESERVED (this panel's home).
            //
            // WO-880: the verbs are shown only when the VM says they can ACT on this selection.
            // A Build-Mode (catalog) tower has no Tower component to upgrade and its BaseLayout
            // record would survive a raw Destroy, so its footer says "Upgrade in Build Mode"
            // instead of offering a button that would lie.
            if (_vm.CanUpgradeSelected)
                AddActionButton("Upgrade", ElarionUiKit.ObsidianButtonColor.Green,
                    () => { _vm.UpgradeSelected(); Refresh(); });

            if (_vm.CanRazeSelected)
                AddActionButton("Raze", ElarionUiKit.ObsidianButtonColor.Red,
                    () => { _vm.RazeSelected(); ClearMarker(); Refresh(); });
        }

        /// <summary>One verb in the fixed action band — the kit button fills its layout slot and a
        /// LayoutElement pins the touch floor (the AddColumnButton recipe, horizontal).</summary>
        private void AddActionButton(string label, ElarionUiKit.ObsidianButtonColor color, System.Action onClick)
        {
            var btn = ElarionUiKit.BuildObsidianButton(_actionBand.transform, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, color, Vector2.zero, Vector2.one, onClick);
            if (btn == null) return;
            var le = btn.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = ActionBandPx;
            le.preferredHeight = ActionBandPx;
            le.flexibleHeight = 0f;
        }

        // --- in-world selection marker (bright unlit sphere above the tower) -----
        private void SetMarker(GameObject towerGo)
        {
            ClearMarker();
            if (towerGo == null) return;

            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "TowerSelectionMarker";
            var col = _marker.GetComponent<Collider>(); if (col != null) Destroy(col);
            _marker.transform.SetParent(towerGo.transform, false);

            var fp = towerGo.transform.Find("FirePoint");
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
        /// <summary>Text in a rect. With <paramref name="heightPx"/> &gt; 0 the rect becomes a
        /// FIXED-PIXEL band hung <paramref name="topPadPx"/> below its anchor and inset
        /// <paramref name="sideInsetPx"/> either side (WO-880: no fraction-of-parent text bands).</summary>
        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max,
            float sideInsetPx = 0f, float topPadPx = 0f, float heightPx = 0f)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            if (heightPx > 0f)
            {
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMax = new Vector2(-sideInsetPx, -topPadPx);
                rt.offsetMin = new Vector2(sideInsetPx, -(topPadPx + heightPx));
            }
            else
            {
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
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
