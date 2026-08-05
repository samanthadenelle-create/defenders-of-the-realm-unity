// =============================================================================
// HeroSkillTreePanelMvvm — the Knight skill-tree VIEW (MVVM slice). A DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// NODE-GRAPH layout (Obsidian dark): nodes are placed at AUTHORED canvas x/y
// (SkillNodeVM.X/Y, 0..1; y 0=top) inside a scrollable fixed-pixel content rect,
// and gilt CONNECTOR lines are drawn from each node back to its prerequisite
// nodes (the unlock path reads as a graph, not a grid). A node click STAGES /
// UNSTAGES it into a plan; nothing is spent until the player presses CONFIRM
// (plan→commit flow). ALL state/logic (positions, owned/stageable/lock reasons,
// pending set, plan cost) lives in HeroSkillTreeVM — the View never reads game
// state (ui-mvvm-binding-seam rule).
//
// WO-865 (2026-08-04, from the real Seeker capture 07-skills-panel.png): the panel
// body is laid out as DISJOINT FIXED-PIXEL BANDS, never fractions of the body well.
// See the geometry block below the field list for the arithmetic that proves why —
// in one line: FrameTalent's body resolves to ~493 px tall at 2340x1080, the action
// row was a 0.065 fraction of it (32 px), and ElarionUiKit.ClampMinTouch then grew
// each button to the 112 px touch floor SYMMETRICALLY, straight over the graph well
// and the quick-swap slots. Same failure class as WO-841 / WO-852. The stack now is
//   columns region (graph well + ability band | detail column)  /  action row
// and the node graph lives on a fixed-pixel lattice inside a RectMask2D scroll well
// with a reserved row for the "Universal - any class" band.
//
// WO-676 §B (owner-approved icon-only redesign, 2026-07-11): nodes are ICON-ONLY
// plates carrying exactly ONE state affordance — cost pip (unlockable),
// −n pip + ring (planned), check stamp (owned), dim (locked). ALL name/desc/state
// text lives in the right-hand detail column. Wisdom is a CurrencyChip (top-right);
// the plan summary folds into the CONFIRM label ("CONFIRM n · −cost"); quick-swap
// and respec feedback are transient toasts (BuildFeedbackToast) — target ≤2
// persistent text strips outside the graph. Colorblind law: every state carries a
// shape/stamp/pip, never hue alone (dim = luminance, pips/stamps = shape+text).
//
// Code-built uGUI ONLY (no UXML — §8). Edge geometry uses a fixed-pixel content
// rect (sized in RebuildGraph from the authored bounds) so rotated connector images
// are deterministic at build time (no dependence on a layout pass). The content
// scrolls (owner: one scrollable canvas, Knight + Shared, no pagination yet).
//
// OWNER F8 2026-07-11 (minimal pass): node FACES are deliberately MINIMAL — a flat
// obsidian plate + thin gilt line border, small tinted-down icon ("remove the
// background image and just a simple icon with the lines"). The painted talent_N
// plate art is retired from node faces; the ornate Obsidian look stays on the
// PANEL frame (BuildObsidianPanel) and the quick-swap tiles. Every sprite lookup
// remains null-safe.
//
// Registers PanelId.HeroSkillTree (+ legacy HeroTalents route; the Skills tab).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Talents
{
    [DisallowMultipleComponent]
    public sealed class HeroSkillTreePanelMvvm : MonoBehaviour, IPanelView
    {
        private HeroSkillTreeVM _vm;

        private GameObject _ui;
        private RectTransform _graphContent;     // fixed-size scroll content (nodes + edges live here)
        private TMPro.TextMeshProUGUI _headerLabel;
        private ElarionUiKit.CurrencyChipHandle _wisdomChip;   // §B.2 — Wisdom = CurrencyChip, top-right
        private Button _confirmBtn;
        private TMPro.TextMeshProUGUI _confirmLabel;           // plan summary folds into the CONFIRM label
        private Button _cancelBtn;
        private Button _respecBtn;

        // Single-screen folds (owner 2026-06-28): the right-side detail strip (selected
        // node name + description + state) and the quick-swap row (slots 1-4).
        private TMPro.TextMeshProUGUI _detailName;
        private TMPro.TextMeshProUGUI _detailDesc;
        private TMPro.TextMeshProUGUI _detailState;
        private GameObject _quickRoot;

        // §B.2 — quick-swap/respec feedback is a transient TOAST, not a persistent strip.
        // null = not yet baselined (the first Render only records; it never toasts stale text).
        private string _lastQuickStatus;
        private string _lastRespecStatus;
        // Detail strip FOLDS (eyes-sweep 2026-07-06): the "Select a talent" empty-state
        // painted OVER the SELECTED TALENT header + body. The two are ALTERNATIVES —
        // RenderDetail activates exactly one, never both.
        private GameObject _detailGroup;   // header + name + description + state
        private GameObject _emptyGroup;    // "Select a talent" prompt + hint copy

        private PanelHandle _panelHandle;

        // =====================================================================
        // WO-865 FIXED-PIXEL BAND GEOMETRY (reference px on the 1080x1920 modal
        // canvas). NEVER a fraction of the parent -- that is the documented root
        // cause of WO-841 / WO-852 and of every defect in the 2026-08-04 Seeker
        // capture (docs/ui-review/2026-08-04-seeker/07-skills-panel.png).
        //
        // PROVING ARITHMETIC (2340x1080, the capture's device):
        //   scaler 1080x1920 match 0.5 -> scale 1.1040 -> canvas local 2119.6x978.3
        //   panel 0.07,0.05-0.93,0.95  -> 1822.9 x 880.5
        //   FrameTalent body after the shared-Close band reservation -> 1695 x 493 px.
        //   The OLD footer band was 0.070..0.135 OF THAT BODY = 32 px. The kit touch
        //   floor (ElarionUiKit.ClampMinTouch, MinTouchPx 112) then grew every action
        //   button SYMMETRICALLY ABOUT ITS CENTRE by +-40 px, so its top reached
        //   106.6 px while the graph well's floor sat at 0.165*493 = 81.4 px. That
        //   25 px of growth is why Cancel / CONFIRM / Respec painted OVER the grid and
        //   over quick-slots 3 and 4, and why slot 4 vanished under Respec entirely.
        //
        // Every band below is a FIXED pixel height >= its own touch/line-box floor, so
        // the stack is disjoint by construction at ANY aspect ratio. The only
        // fraction left is the LEFT/RIGHT column split (a width, never a text band).
        // =====================================================================

        /// <summary>Inset from the body well's edges (ref px).</summary>
        public const float BodyPadPx = 6f;
        /// <summary>Gap between two stacked fixed-pixel bands (ref px).</summary>
        public const float BandGapPx = 8f;
        /// <summary>The bottom action row (Cancel / Respec / CONFIRM). Buttons are tappable, so
        /// the band IS the kit touch floor -- ClampMinTouch can then never grow one past it.</summary>
        public const float ActionRowPx = ElarionUiKit.MinTouchPx;
        /// <summary>The ability (quick-swap) band. Slots are tap targets, so this is >= the touch
        /// floor; it is a little taller than the floor because the tile stacks TWO fixed line
        /// boxes (slot numeral + a two-line ability name).</summary>
        public const float AbilityRowPx = 132f;
        /// <summary>A graph node plate. Nodes are BUTTONS, so they carry the touch floor too
        /// (they were 96 px -- below the floor -- before WO-865).</summary>
        public const float NodeSizePx = ElarionUiKit.MinTouchPx;

        /// <summary>Right column: the WISDOM currency chip band.</summary>
        public const float WisdomBandPx = 52f;
        /// <summary>Right column: the "SELECTED TALENT" caption band (FontMicro 32 -> line ~40).</summary>
        public const float DetailHeadPx = 40f;
        /// <summary>Right column: the talent NAME band (FontTitle, fitted; line box floor 40).</summary>
        public const float DetailNamePx = 60f;
        /// <summary>Right column: the state / "Requires ..." band (FontMicro 32 -> line ~40).</summary>
        public const float DetailStatePx = 42f;

        /// <summary>Ability tile: the slot numeral line box -- a WHOLE line box at the kit
        /// FontFloor (30 x 1.25 = 37.5), never the 34 px that only just misses it.</summary>
        public const float SlotKeyBandPx = 38f;
        /// <summary>Ability tile: the ability-name box -- TWO whole FontFloor line boxes, because
        /// the longest catalog name ("Suppressing Volley") needs to wrap to read in full inside a
        /// ~250 px tile. The old band was 23 px (0.5 of a 47 px fraction tile), i.e. below ONE
        /// line box, which is what ellipsized "Emberbrand Throw" to "Emberbrand Thro".</summary>
        public const float SlotNameBandPx = 80f;
        /// <summary>Ability tile: pad above the numeral / below the name (ref px).</summary>
        public const float SlotPadPx = 6f;

        /// <summary>The "Universal - any class" section band's own line box (FontMicro+2 -> line ~44).</summary>
        public const float SectionBandPx = 46f;
        /// <summary>Clear air above and below the section band's label.</summary>
        public const float SectionGapPx = 14f;
        /// <summary>The MINIMUM node-row pitch across the section band: one node plate plus the
        /// band's own reserved row. RebuildGraph shifts every node authored BELOW the band down
        /// by whatever is missing, so a node plate can never paint over the label again
        /// (the capture's "Univers[icon]y class": band y 0.965 vs shared row y 0.98 were 15.6 px
        /// apart with 96 px plates, and nodes are built after the band).</summary>
        public const float SectionClearPx = NodeSizePx + SectionGapPx * 2f + SectionBandPx;

        /// <summary>Graph lattice: reference px per 1.0 of authored node X. Sized so the tightest
        /// authored column gap (0.12 on the shared row) still clears a node plate.</summary>
        public const float GraphUnitWpx = 1180f;
        /// <summary>Graph lattice: reference px per 1.0 of authored node Y.</summary>
        public const float GraphUnitHpx = 780f;
        /// <summary>Graph content padding: half a node plate plus air, so the extreme nodes are
        /// wholly INSIDE the scroll content and are never sliced at the mask edge at rest.</summary>
        public const float GraphPadPx = NodeSizePx * 0.5f + 16f;

        /// <summary>Authored Y the "Universal - any class" band divides the graph at.</summary>
        public const float SectionBandY = 0.965f;

        // ── The only FRACTIONS left in the layout: the column split and the in-tile text
        // insets. Both are WIDTHS, never a text band's height -- the class of failure the
        // fixed-pixel law exists to stop is a band too short for its own line box.
        /// <summary>Right edge of the graph column, as a fraction of the columns region.</summary>
        public const float GraphColumnX1 = 0.615f;
        /// <summary>Left edge of the detail column, as a fraction of the columns region.</summary>
        public const float DetailColumnX0 = 0.640f;
        /// <summary>Gap between two ability slot tiles, as a fraction of the ability band.</summary>
        public const float SlotGapFrac = 0.012f;
        /// <summary>Side inset of the ability-name label inside its tile (fraction of the tile).</summary>
        public const float SlotTextInsetFrac = 0.03f;
        /// <summary>Side inset of every label in the detail column (fraction of the column).</summary>
        public const float DetailTextInsetFrac = 0.03f;

        // Graph lattice origin + the section band's reserved-row shift, recomputed from the VM's
        // authored nodes on every rebuild (fixed PIXELS derived from data -- never a parent fraction).
        private float _minX, _maxX, _minY, _maxY;
        private float _bandShiftPx;
        private float _bandCentrePy;
        private bool _bandPlaced;

        public bool IsOpen => _ui != null;

        // ── fixed-pixel band pins (the WO-832 §4 / WO-841 / WO-852 pattern) ──────
        // Re-hang a control on its parent's TOP or BOTTOM edge with a FIXED ref-pixel
        // band; X anchors/offsets are preserved. A band pinned this way never scales
        // with the pane and can never under-height its own line box.

        private static void PinBandFromTop(RectTransform rt, float topPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -(topPx + heightPx));
            rt.offsetMax = new Vector2(rt.offsetMax.x, -topPx);
        }

        private static void PinBandFromBottom(RectTransform rt, float bottomPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomPx);
            rt.offsetMax = new Vector2(rt.offsetMax.x, bottomPx + heightPx);
        }

        /// <summary>Stretch a control between FIXED pixel insets from its parent's top and bottom
        /// edges. The flexible middle of a band stack -- its extent is still decided by pixels.</summary>
        private static void PinRegion(RectTransform rt, float bottomPx, float topPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomPx);
            rt.offsetMax = new Vector2(rt.offsetMax.x, -topPx);
        }

        /// <summary>A transparent full-width layout host under <paramref name="parent"/>, spanning
        /// the x fractions given. Vertical seat is set afterwards by one of the pins above.</summary>
        private static RectTransform BandHost(Transform parent, string name, float x0, float x1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        // ── Registration (mirror BuildingUpgradePanelMvvm) ────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Skills", Close, () => IsOpen);
            PanelRouter.Register(PanelId.HeroSkillTree, Open);
            // EYES-SWEEP 2026-07-06: the legacy PanelId.HeroTalents route is REMOVED (was
            // ff.herotalents-gated; a stale PlayerPrefs "ff.herotalents"=1 re-armed the dead route and
            // the capture fleet rendered panel_HeroTalents fully black). One panel, ONE route:
            // HeroSkillTree. All entry points (ArcaneTower building, dialogue OpenTalents) route to
            // PanelId.HeroSkillTree unconditionally; PanelId.HeroTalents is retired-unroutable.
        }

        private void OnDestroy()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.HeroSkillTree, Open);
        }

        // ── Open: build chrome, construct + bind the VM ───────────────────────────

        public void Open()
        {
            Close();
            BuildChrome();

            _vm = new HeroSkillTreeVM(Close);
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return; // rejected (e.g. in battle) — NotifyOpened already invoked Close.

            Debug.Log("[HeroSkillTreePanelMvvm] Opened. Bound HeroSkillTreeVM (node-graph MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as HeroSkillTreeVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // ── Render: repaint from vm.* ONLY ────────────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;
            if (_headerLabel != null) _headerLabel.text = _vm.Title;

            // §B.2 — Wisdom is a CurrencyChip (count-tween; no text wallet strip).
            if (_wisdomChip != null) _wisdomChip.SetAmount(_vm.RemainingWisdom);

            // §B.2 — the plan summary folds into the CONFIRM label: "CONFIRM n · −cost".
            if (_confirmLabel != null)
            {
                int n = _vm.PendingCount;
                // ASCII "-" (the TMP font has no U+2212 minus; eyes-on 2026-07-03).
                _confirmLabel.text = n > 0
                    ? "CONFIRM " + n + ", -" + _vm.PendingCost
                    : "CONFIRM";
            }
            if (_confirmBtn != null)
            {
                _confirmBtn.interactable = _vm.CanConfirm;
                SetButtonAlpha(_confirmBtn, _vm.CanConfirm ? 1f : 0.4f);
            }
            if (_cancelBtn != null)
            {
                bool any = _vm.PendingCount > 0;
                _cancelBtn.interactable = any;
                SetButtonAlpha(_cancelBtn, any ? 1f : 0.4f);
            }
            if (_respecBtn != null)
            {
                bool can = _vm.CanRespec;
                _respecBtn.interactable = can;
                SetButtonAlpha(_respecBtn, can ? 1f : 0.4f);
            }
            // §B.2 — respec feedback is a transient toast (no persistent status strip).
            // First Render only baselines (null tracker) so a stale VM line never re-toasts.
            string respec = _vm.RespecStatus ?? "";
            if (_lastRespecStatus != null && respec != _lastRespecStatus && respec.Length > 0)
                BuildFeedbackToast.Show(respec);
            _lastRespecStatus = respec;

            RebuildGraph();
            RenderDetail();
            RebuildQuickSlots();
        }

        // ── Detail strip (selected node name + description + state) ──────────────

        private void RenderDetail()
        {
            if (_vm == null) return;
            bool has = _vm.HasSelection;
            // Empty-state renders INSTEAD of the detail fold — never on top of it.
            if (_detailGroup != null) _detailGroup.SetActive(has);
            if (_emptyGroup != null) _emptyGroup.SetActive(!has);
            if (has)
            {
                if (_detailName != null) _detailName.text = _vm.SelectedNodeName;
                if (_detailDesc != null) _detailDesc.text = _vm.SelectedNodeDescription;
                // §B.4 — the detail state line doubles as the quick-swap hint (the VM's
                // state line already says "tap a slot (1-4)" for an owned active skill).
                if (_detailState != null) _detailState.text = _vm.SelectedNodeStateLine;
            }
            // §B.2 — quick-swap ACTION feedback ("X → quick-swap 2.") is a transient toast;
            // the persistent hint strip is gone. First Render baselines (null tracker).
            string quick = _vm.QuickSwapStatus ?? "";
            if (_lastQuickStatus != null && quick != _lastQuickStatus && quick.Length > 0)
                BuildFeedbackToast.Show(quick);
            _lastQuickStatus = quick;
        }

        // ── Build the node graph (edges behind, nodes in front) ──────────────────

        private void RebuildGraph()
        {
            ClearContent();
            if (_graphContent == null || _vm == null) return;

            // WO-865 step 1 — the FIXED-PIXEL lattice. Authored x/y (0..1) are normalised to the
            // authored MINIMUM and scaled by a fixed px-per-unit, so the scroll content is an exact
            // pixel rect at every aspect. No leading dead space, no fraction of the viewport.
            MeasureGraphBounds();

            // WO-865 step 2 — the section band's RESERVED ROW. Everything authored below the band
            // is pushed down by exactly the pixels missing from a clear row, so a node plate can
            // never paint over the "Universal - any class" label again.
            _bandShiftPx = ComputeBandShiftPx();

            float contentW = GraphPadPx * 2f + (_maxX - _minX) * GraphUnitWpx;
            float contentH = GraphPadPx * 2f + (_maxY - _minY) * GraphUnitHpx + _bandShiftPx;
            _graphContent.sizeDelta = new Vector2(contentW, contentH);
            _graphContent.anchoredPosition = Vector2.zero;   // rest = flush top-LEFT, never mid-plate

            // Lookup id -> node + pixel centre, for the prerequisite connectors.
            var center = new Dictionary<string, Vector2>(64);
            var nodeById = new Dictionary<string, SkillNodeVM>(64);
            CollectPositions(_vm.Nodes, center, nodeById);
            CollectPositions(_vm.Shared, center, nodeById);

            // Edges first (drawn behind the node plates).
            DrawEdges(_vm.Nodes, center, nodeById);
            DrawEdges(_vm.Shared, center, nodeById);

            // Section divider (above the shared band) — WO-675 crown-glyph band grammar, now
            // seated in the middle of the row the shift above reserved for it.
            if (_bandPlaced) BuildSectionBand("Universal - any class", _bandCentrePy, contentW);

            // Nodes on top.
            foreach (var n in _vm.Nodes) BuildGraphNode(n, center);
            foreach (var n in _vm.Shared) BuildGraphNode(n, center);
        }

        // Authored x/y bounds across BOTH lists (the lattice origin). Defaults match
        // CollectPositions so an unset (-1) node lands at the same 0.5/0.5 centre.
        private void MeasureGraphBounds()
        {
            _minX = _minY = 1f; _maxX = _maxY = 0f;
            bool any = false;
            any |= AccumulateBounds(_vm.Nodes);
            any |= AccumulateBounds(_vm.Shared);
            if (!any) { _minX = _minY = 0f; _maxX = _maxY = 1f; }
            if (_maxX < _minX) _maxX = _minX;
            if (_maxY < _minY) _maxY = _minY;
        }

        private bool AccumulateBounds(IReadOnlyList<SkillNodeVM> list)
        {
            if (list == null) return false;
            bool any = false;
            foreach (var n in list)
            {
                if (string.IsNullOrEmpty(n.Id)) continue;
                float x = n.X >= 0f ? n.X : 0.5f;
                float y = n.Y >= 0f ? n.Y : 0.5f;
                if (x < _minX) _minX = x;
                if (x > _maxX) _maxX = x;
                if (y < _minY) _minY = y;
                if (y > _maxY) _maxY = y;
                any = true;
            }
            return any;
        }

        // How many pixels the rows BELOW the section band must move down so the band's label
        // gets a whole clear row. Computed from the data (never a hard-coded guess): the raw
        // pitch between the last row above the band and the first row below it, versus the
        // SectionClearPx floor (one node plate + the band + air on both sides).
        private float ComputeBandShiftPx()
        {
            _bandPlaced = false;
            float lastAbove = float.NegativeInfinity, firstBelow = float.PositiveInfinity;
            ScanBandRows(_vm.Nodes, ref lastAbove, ref firstBelow);
            ScanBandRows(_vm.Shared, ref lastAbove, ref firstBelow);
            if (float.IsNegativeInfinity(lastAbove) || float.IsPositiveInfinity(firstBelow))
                return 0f;   // nothing on one side of the band — no row to reserve

            float rawPitch = (firstBelow - lastAbove) * GraphUnitHpx;
            float shift = Mathf.Max(0f, SectionClearPx - rawPitch);

            // The band sits at the midpoint of the cleared gap: below the plate bottom of the
            // last row above, above the plate top of the (already shifted) first row below.
            float abovePlateBottom = -(GraphPadPx + (lastAbove - _minY) * GraphUnitHpx) - NodeSizePx * 0.5f;
            float belowPlateTop = -(GraphPadPx + (firstBelow - _minY) * GraphUnitHpx) - shift + NodeSizePx * 0.5f;
            _bandCentrePy = (abovePlateBottom + belowPlateTop) * 0.5f;
            _bandPlaced = true;
            return shift;
        }

        private static void ScanBandRows(IReadOnlyList<SkillNodeVM> list, ref float lastAbove, ref float firstBelow)
        {
            if (list == null) return;
            foreach (var n in list)
            {
                if (string.IsNullOrEmpty(n.Id)) continue;
                float y = n.Y >= 0f ? n.Y : 0.5f;
                if (y <= SectionBandY) { if (y > lastAbove) lastAbove = y; }
                else { if (y < firstBelow) firstBelow = y; }
            }
        }

        private void CollectPositions(IReadOnlyList<SkillNodeVM> list,
                                      Dictionary<string, Vector2> center,
                                      Dictionary<string, SkillNodeVM> nodeById)
        {
            if (list == null) return;
            foreach (var n in list)
            {
                if (string.IsNullOrEmpty(n.Id)) continue;
                float x = n.X >= 0f ? n.X : 0.5f;
                float y = n.Y >= 0f ? n.Y : 0.5f;
                center[n.Id] = CenterPx(x, y);
                nodeById[n.Id] = n;
            }
        }

        private void DrawEdges(IReadOnlyList<SkillNodeVM> list,
                               Dictionary<string, Vector2> center,
                               Dictionary<string, SkillNodeVM> nodeById)
        {
            if (list == null) return;
            foreach (var n in list)
            {
                if (n.Prereqs == null || !center.TryGetValue(n.Id, out var to)) continue;
                bool childActive = n.Owned || n.IsPending;
                foreach (var pr in n.Prereqs)
                {
                    if (string.IsNullOrEmpty(pr) || !center.TryGetValue(pr, out var from)) continue;
                    bool parentActive = nodeById.TryGetValue(pr, out var pn) && (pn.Owned || pn.IsPending);
                    // A connector glows once BOTH ends are owned/planned (the path is live).
                    bool live = childActive && parentActive;
                    BuildEdge(from, to, live);
                }
            }
        }

        // px centre for an authored (x,y): content anchored top-LEFT, y grows down. Normalised to
        // the authored minimum and padded by GraphPadPx (>= half a node plate), so the extreme
        // rows/columns sit WHOLLY inside the content rect and are never sliced mid-plate at rest.
        // Rows below the section band carry the reserved-row shift.
        private Vector2 CenterPx(float x, float y)
        {
            float px = GraphPadPx + (x - _minX) * GraphUnitWpx;
            float py = -(GraphPadPx + (y - _minY) * GraphUnitHpx);
            if (y > SectionBandY) py -= _bandShiftPx;
            return new Vector2(px, py);
        }

        private void BuildEdge(Vector2 a, Vector2 b, bool live)
        {
            var go = new GameObject("Edge", typeof(Image));
            go.transform.SetParent(_graphContent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            Vector2 mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            r.anchoredPosition = mid;
            // §B.3 — quiet the string-web: live path 4px gilt, inactive 1.5px @ ~0.12 alpha.
            r.sizeDelta = new Vector2(len, live ? 4f : 1.5f);
            r.localRotation = Quaternion.Euler(0f, 0f, ang);
            var img = go.GetComponent<Image>();
            img.color = live
                ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f)
                : new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.12f);
            img.raycastTarget = false;
        }

        // Section divider band (WO-675 §2 grammar shared with the upgrade panel): crown
        // glyph + small gilt label + a thin gilt rule running the rest of the row.
        private void BuildSectionBand(string text, float centrePy, float contentW)
        {
            var host = new GameObject("SectionBand", typeof(RectTransform));
            host.transform.SetParent(_graphContent, false);
            var r = (RectTransform)host.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            // FIXED-PIXEL band height (was 36 against a font-34 line box of ~44 — the band
            // could not seat its own line, the ObsidianQueueHud.HeaderHeightPx failure class).
            r.sizeDelta = new Vector2(contentW * 0.92f, SectionBandPx);
            r.anchoredPosition = new Vector2(contentW * 0.5f, centrePy);

            // Crown glyph (sprite-first, hidden on miss — the rule+label still carry the band).
            Sprite crown = RpgUiCatalog.Get(RpgUiCatalog.RoleCrown, RpgUiCatalog.CrownTier1);
            if (crown != null)
            {
                var cGo = new GameObject("Crown", typeof(Image));
                cGo.transform.SetParent(host.transform, false);
                var cr = (RectTransform)cGo.transform;
                cr.anchorMin = new Vector2(0f, 0.10f); cr.anchorMax = new Vector2(0.030f, 0.90f);
                cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
                var cImg = cGo.GetComponent<Image>();
                cImg.sprite = crown;
                cImg.preserveAspect = true;
                cImg.color = ElarionUi.Gilt;
                cImg.raycastTarget = false;
            }

            // Widened 0.40 -> 0.46: "Universal - any class" at FontMicro+2 needs ~393 px and the
            // old cell resolved to ~386 — it was one glyph from ellipsizing its own band label.
            var label = ElarionUiKit.Label(host.transform, text, 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontMicro + 2, TMPro.TextAlignmentOptions.MidlineLeft, 0.038f, 0.46f, bold: true);
            label.raycastTarget = false;
            ElarionUiKit.FitSingleLine(label);

            // Thin gilt rule filling the remainder of the band row.
            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(host.transform, false);
            var rr = (RectTransform)rule.transform;
            rr.anchorMin = new Vector2(0.47f, 0.5f); rr.anchorMax = new Vector2(1f, 0.5f);
            rr.offsetMin = new Vector2(0f, -0.75f); rr.offsetMax = new Vector2(0f, 0.75f);
            var rImg = rule.GetComponent<Image>();
            rImg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f);
            rImg.raycastTarget = false;
        }

        // ── One graph node (§B.1 icon-only: plate + icon + ONE affordance) ───────
        // cost pip (unlockable) / −n pip + ring (planned) / check stamp (owned) /
        // dim (locked). ALL text lives in the detail column. Click = select+stage.

        private void BuildGraphNode(SkillNodeVM node, Dictionary<string, Vector2> center)
        {
            if (string.IsNullOrEmpty(node.Id) || !center.TryGetValue(node.Id, out var c)) return;

            var go = new GameObject("Node_" + node.Id, typeof(Image), typeof(Button));
            go.transform.SetParent(_graphContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = c;
            // A node is a BUTTON — it carries the kit touch floor (was 96 px, below it).
            rt.sizeDelta = new Vector2(NodeSizePx, NodeSizePx);

            var img = go.GetComponent<Image>();
            // OWNER F8 2026-07-11 ("remove the background image and just a simple icon with the
            // lines, simple and minimalistic"): the node face is MINIMAL — a flat obsidian plate
            // with a THIN gilt LINE border. The painted talent_1..4 plate art is retired from the
            // node face (the ornate look stays on the panel frame); the graph reads as quiet
            // icons + connector lines. Root image = the gilt border; the dark fill is a child
            // inset by the line width, so the border renders as a crisp ~1.5px line.
            ElarionUiKit.ApplyRounded(img);
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, BorderAlpha(node));

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(1.5f, 1.5f);
            fillRt.offsetMax = new Vector2(-1.5f, -1.5f);
            var fillImg = fillGo.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(fillImg);
            fillImg.color = PlateFill(node);
            fillImg.raycastTarget = false;

            // Capstone — a THICKER gilt rim (procedural, no art) so the tier-capper still reads
            // special without reintroducing painted borders. Behind the plate, peeks ~5px.
            if (node.IsCapstone)
            {
                var frame = new GameObject("CapstoneFrame", typeof(Image));
                frame.transform.SetParent(go.transform, false);
                var fr = frame.GetComponent<RectTransform>();
                fr.anchorMin = new Vector2(-0.05f, -0.05f);
                fr.anchorMax = new Vector2(1.05f, 1.05f);
                fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
                var fImg = frame.GetComponent<Image>();
                ElarionUiKit.ApplyRounded(fImg);
                fImg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                                       node.Owned || node.CanUnlock || node.IsPending ? 0.85f : 0.40f);
                fImg.raycastTarget = false;
                frame.transform.SetAsFirstSibling();
            }

            // Planned ring — a CLEAN rounded ring (owner F8 2026-07-11: the old un-rounded raw
            // Image drew a solid square that peeked past the plate as a "rough yellow scribble").
            // Rounded + behind the opaque plate → reads as a thin ~5px ring around the border.
            if (node.IsPending)
            {
                var ring = new GameObject("PlanRing", typeof(Image));
                ring.transform.SetParent(go.transform, false);
                var rr = ring.GetComponent<RectTransform>();
                rr.anchorMin = new Vector2(-0.05f, -0.05f);
                rr.anchorMax = new Vector2(1.05f, 1.05f);
                rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
                var rImg = ring.GetComponent<Image>();
                ElarionUiKit.ApplyRounded(rImg);
                rImg.color = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.9f);
                rImg.raycastTarget = false;
                ring.transform.SetAsFirstSibling();
            }

            // Click → SELECT (always, so a locked perk can be read in the detail strip);
            // the VM folds the plan toggle (stage/unstage) in for actionable nodes.
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            btn.interactable = true;
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Select(id); });

            // Icon — SMALL and quiet in the plate centre (owner F8 2026-07-11 minimal pass).
            // The Talents/* sprites are full-bleed paintings; at ~60% of the plate, tinted
            // down toward parchment-grey, they read as quiet emblems instead of background art.
            var sprite = LoadIcon(node.IconPath);
            bool locked = !node.Owned && !node.CanUnlock && !node.IsPending;
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.20f, 0.20f);
                ir.anchorMax = new Vector2(0.80f, 0.80f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = sprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = locked
                    ? new Color(0.82f, 0.80f, 0.77f, 0.35f)  // dim = the locked affordance
                    : new Color(0.86f, 0.84f, 0.81f, 0.95f); // tinted-down glyph read
            }
            else
            {
                // No icon art yet — a two-letter monogram keeps the node identifiable
                // (never a blank plate; name/desc still live in the detail column).
                string mono = string.IsNullOrEmpty(node.Name) ? "?" : node.Name.Substring(0, Mathf.Min(2, node.Name.Length));
                var monoLbl = ElarionUiKit.Label(go.transform, mono, 0.24f, 0.76f,
                    locked ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                ElarionUiKit.FitSingleLine(monoLbl);
            }

            // ONE affordance per state (colorblind law — shape/stamp/pip, never hue alone):
            //   owned  → check stamp   · planned → ring (above) + "−n" pip
            //   can-unlock → cost pip  · locked  → dim (icon alpha + plate tint, luminance)
            if (node.Owned)
                BuildNodeCheckStamp(go.transform);                                        // check STAMP (shape, font-free)
            else if (node.IsPending)
                BuildNodeStamp(go.transform, "-" + node.WisdomCost, ElarionUi.Affordable); // planned -n pip (+ ring above)
            else if (node.CanUnlock)
                BuildNodeStamp(go.transform, node.WisdomCost.ToString(), ElarionUi.Parchment); // cost pip
        }

        // Small bottom-right pip disc: dark plate + a short glyph ("-2", "3").
        // ASCII only — eyes-on 2026-07-03: ✓/✗/− are missing from the TMP font.
        private static RectTransform BuildNodeStamp(Transform nodeRoot, string glyph, Color color)
        {
            var pip = new GameObject("Stamp", typeof(Image));
            pip.transform.SetParent(nodeRoot, false);
            var pr = (RectTransform)pip.transform;
            pr.anchorMin = new Vector2(0.62f, -0.06f);
            pr.anchorMax = new Vector2(1.06f, 0.38f);
            pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            var pImg = pip.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(pImg);
            pImg.color = new Color(0.05f, 0.045f, 0.06f, 0.92f);   // near-black disc
            pImg.raycastTarget = false;

            if (!string.IsNullOrEmpty(glyph))
            {
                var lbl = ElarionUiKit.Label(pip.transform, glyph, 0.08f, 0.92f, color,
                    ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                lbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(lbl);
            }
            return pr;
        }

        // Owned = a CHECK stamp drawn from two rotated gilt bars (no font dependency —
        // the TMP font has no ✓ glyph; a shape also satisfies the colorblind law).
        private static void BuildNodeCheckStamp(Transform nodeRoot)
        {
            var pr = BuildNodeStamp(nodeRoot, null, Color.clear);

            var shortBar = new GameObject("CheckA", typeof(Image));
            shortBar.transform.SetParent(pr, false);
            var sr = (RectTransform)shortBar.transform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.34f, 0.38f);
            sr.sizeDelta = new Vector2(13f, 4.5f);
            sr.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var sImg = shortBar.GetComponent<Image>();
            sImg.color = ElarionUi.Gilt;
            sImg.raycastTarget = false;

            var longBar = new GameObject("CheckB", typeof(Image));
            longBar.transform.SetParent(pr, false);
            var lr = (RectTransform)longBar.transform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.60f, 0.50f);
            lr.sizeDelta = new Vector2(20f, 4.5f);
            lr.localRotation = Quaternion.Euler(0f, 0f, -50f);
            var lImg = longBar.GetComponent<Image>();
            lImg.color = ElarionUi.Gilt;
            lImg.raycastTarget = false;
        }

        // Flat obsidian fill (minimal face — owner F8 2026-07-11): state lives in the ONE
        // affordance (check/ring+pip/cost pip/dim); the fill only carries the locked-dim
        // luminance step (colorblind law — never hue alone).
        private static Color PlateFill(SkillNodeVM node)
        {
            bool locked = !node.Owned && !node.CanUnlock && !node.IsPending;
            return locked
                ? new Color(0.030f, 0.028f, 0.040f, 0.96f)
                : new Color(0.055f, 0.050f, 0.070f, 0.96f);
        }

        // Thin gilt LINE border: actionable/live nodes carry a brighter line; locked recedes
        // (luminance step, still visible so the graph shape always reads).
        private static float BorderAlpha(SkillNodeVM node)
        {
            if (node.Owned || node.IsPending) return 0.90f;
            if (node.CanUnlock) return 0.70f;
            return 0.28f;
        }

        // ── Chrome (presentation only) ────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("HeroSkillTreePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header + ONE Close.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Skills",
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), () => { if (_vm != null) _vm.Close(); },
                headerX0: 0.04f, headerX1: 0.74f, frameName: RpgUiCatalog.FrameTalent,
                medallionIcon: "talent");
            // Fit ALL content into the frame's BODY drop-zone (the templated well) instead of
            // floating over the whole panel rect — the old 0..1-over-content layout overlapped the
            // frame's ornate border. Every sub-builder now lays out (in fractions) INSIDE the body
            // zone. Falls back to the transparent content overlay when no frame is mirrored.
            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)chrome.content.transform;
            Transform panel = bodyHost;
            _headerLabel = chrome.title;

            // ── WO-865: THE BAND STACK ────────────────────────────────────────────
            // Three DISJOINT fixed-pixel regions, seated from the body well's own edges.
            // Nothing can float over anything else because nothing shares a pixel:
            //
            //   [ columns region ]  <- stretch between fixed insets
            //        left  : graph scroll well  (+ its own ability band at the foot)
            //        right : wisdom / selected talent / description / state
            //   [ action row    ]  <- fixed ActionRowPx, pinned to the body floor
            //
            // The ability band lives INSIDE the left column rather than spanning the body:
            // the body well is only ~493 px tall at 2340x1080, and a full-width ability band
            // would starve the description column to a single line. In the left column each
            // of the four slots still gets ~255 x 112 px — enough for "Emberbrand Throw" at
            // the FontFloor, which is the truncation the capture showed.

            var actionRow = BandHost(panel, "ActionRowBand", 0f, 1f);
            PinBandFromBottom(actionRow, BodyPadPx, ActionRowPx);

            var columns = BandHost(panel, "ColumnsRegion", 0f, 1f);
            PinRegion(columns, BodyPadPx + ActionRowPx + BandGapPx, BodyPadPx);

            var leftCol = BandHost(columns, "GraphColumn", 0f, GraphColumnX1);
            var rightCol = BandHost(columns, "DetailColumn", DetailColumnX0, 1f);

            var abilityBand = BandHost(leftCol, "AbilityBand", 0f, 1f);
            PinBandFromBottom(abilityBand, 0f, AbilityRowPx);
            _quickRoot = abilityBand.gameObject;

            var graphWell = BandHost(leftCol, "GraphWell", 0f, 1f);
            PinRegion(graphWell, AbilityRowPx + BandGapPx, 0f);

            // (The old "Equip" button that opened a second loadout screen is GONE — the
            // quick-swap band below folds that assign flow into THIS screen, owner 2026-06-28.)

            BuildScrollGraph(graphWell);
            BuildDetailColumn(rightCol);
            BuildActionRow(actionRow);
        }

        // The right-hand column: WISDOM chip, then the SELECTED-talent detail strip
        // (name + description + state). Browse → select → read → confirm → assign, all on
        // one screen (no second loadout panel; the quick-swap band lives under the graph).
        //
        // Every row here is a FIXED-PIXEL band pinned off the column's own top/bottom edge.
        // The description is the only flexible row, and even it is bounded by pixel insets.
        private void BuildDetailColumn(RectTransform col)
        {
            const float txX0 = DetailTextInsetFrac, txX1 = 1f - DetailTextInsetFrac;

            // §B.2 — Wisdom wallet = the ONE CurrencyChip (top of the detail column;
            // tag "WISDOM" guarantees identity even if the icon art is absent).
            _wisdomChip = ElarionUiKit.CurrencyChip(col, ElarionUiKit.CurrencyKind.Wisdom,
                new Vector2(0.28f, 1f), new Vector2(1f, 1f), tag: "WISDOM");
            if (_wisdomChip != null && _wisdomChip.root != null)
                PinBandFromTop((RectTransform)_wisdomChip.root.transform, 0f, WisdomBandPx);

            // Fixed-pixel row plan for the column (offsets from ITS top / ITS bottom):
            float headTop = WisdomBandPx + BandGapPx;
            float nameTop = headTop + DetailHeadPx + 4f;
            float descTop = nameTop + DetailNamePx + 6f;
            float descBottom = 4f + DetailStatePx + BandGapPx;

            // Two ALTERNATIVE folds (eyes-sweep 2026-07-06 fix): the empty-state prompt
            // and the selected-talent detail share the column's bands but live under
            // separate full-rect hosts — RenderDetail activates exactly ONE.
            _detailGroup = MakeGroupHost(col, "DetailGroup");
            _emptyGroup = MakeGroupHost(col, "EmptyStateGroup");
            _detailGroup.SetActive(false);   // empty-state is the default fold until a node is selected

            var selHeader = ElarionUiKit.Label(_detailGroup.transform, "SELECTED TALENT", 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            PinBandFromTop(selHeader.rectTransform, headTop, DetailHeadPx);
            ElarionUiKit.FitSingleLine(selHeader);

            _detailName = ElarionUiKit.Label(_detailGroup.transform, "", 0f, 1f, ElarionUi.Parchment,
                ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            PinBandFromTop(_detailName.rectTransform, nameTop, DetailNamePx);
            ElarionUiKit.FitSingleLine(_detailName);   // long talent names shrink/ellipsize, never spill

            _detailDesc = ElarionUiKit.Label(_detailGroup.transform, "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.TopLeft, txX0, txX1);
            PinRegion(_detailDesc.rectTransform, descBottom, descTop);
            ElarionUiKit.FitBlock(_detailDesc);        // wraps + truncates inside its band

            _detailState = ElarionUiKit.Label(_detailGroup.transform, "", 0f, 1f, ElarionUi.Affordable,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            PinBandFromBottom(_detailState.rectTransform, 4f, DetailStatePx);
            ElarionUiKit.FitSingleLine(_detailState);

            // Empty-state fold — SAME bands, rendered INSTEAD of the detail fold.
            var emptyTitle = ElarionUiKit.Label(_emptyGroup.transform, "Select a talent", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            PinBandFromTop(emptyTitle.rectTransform, nameTop, DetailNamePx);
            ElarionUiKit.FitSingleLine(emptyTitle);
            var emptyBody = ElarionUiKit.Label(_emptyGroup.transform,
                "Tap any node to read what it does before you confirm.",
                0f, 1f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.TopLeft, txX0, txX1);
            PinRegion(emptyBody.rectTransform, descBottom, descTop);
            ElarionUiKit.FitBlock(emptyBody);
        }

        // Full-rect transparent layout host — children keep their fractional anchors;
        // toggling the host swaps the whole fold on/off atomically.
        private static GameObject MakeGroupHost(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        // The scrollable graph viewport (mask) + fixed-size content (nodes/edges).
        // The host is the WO-865 graph well — a fixed-pixel region of the left column, so
        // this scroll area simply fills it and the RectMask2D clips at the well's edges.
        private void BuildScrollGraph(Transform panel)
        {
            var areaGo = new GameObject("GraphScroll", typeof(RectTransform), typeof(ScrollRect));
            areaGo.transform.SetParent(panel, false);
            var ar = areaGo.GetComponent<RectTransform>();
            ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one;
            ar.offsetMin = Vector2.zero; ar.offsetMax = Vector2.zero;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(areaGo.transform, false);
            var vr = viewportGo.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewportGo.GetComponent<Image>();
            // BLACK GRID node-canvas (owner: "a grid that's black like the image for maximum
            // value/contrast"). A procedural near-black tile with a single faint gilt-grey rule
            // on two edges, tiled across the viewport. Opaque, so it overrides the obsidian fill
            // in the graph rect, and raycastable so drag-scroll still works.
            var grid = GridSprite();
            if (grid != null)
            {
                vImg.sprite = grid;
                vImg.type = Image.Type.Tiled;
                vImg.color = Color.white;
            }
            else
            {
                vImg.color = new Color(0.012f, 0.012f, 0.016f, 1f); // flat black fallback
            }

            var contentGo = new GameObject("GraphContent", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _graphContent = contentGo.GetComponent<RectTransform>();
            // TOP-LEFT anchored + pivoted (was centre-anchored). Centring a content rect WIDER
            // than the mask is exactly what sliced a node plate off BOTH frame edges in the
            // 2026-08-04 capture. Flush top-left + GraphPadPx of padding means the rest position
            // always shows whole plates at the leading edges; the trailing edge peeks, which is
            // the scroll affordance. RebuildGraph sizes the rect in exact pixels.
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0f, 1f);
            _graphContent.pivot = new Vector2(0f, 1f);
            _graphContent.sizeDelta = new Vector2(GraphUnitWpx, GraphUnitHpx);
            _graphContent.anchoredPosition = Vector2.zero;

            var scroll = areaGo.GetComponent<ScrollRect>();
            scroll.content = _graphContent;
            scroll.viewport = vr;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 28f;
        }

        // Cancel / Respec / CONFIRM (§B.2 — no plan-summary strip; the plan folds into
        // the CONFIRM label, "CONFIRM n, -cost", written by Render).
        //
        // WO-865 — ONE BUTTON LANGUAGE. The capture showed three chromes in one row: Cancel
        // plain, CONFIRM with a green pack fill that ran past its own rect and under the
        // ability list, Respec a light grey box. All three now build through the SAME call
        // (ButtonKind.Quiet + RpgUiCatalog.ButtonFrame) — the green ButtonConfirm overlay that
        // produced the bleed is gone. Emphasis, not chrome, marks the primary: CONFIRM gets a
        // procedural gilt ring (a SHAPE, not a hue — the owner is red/green colourblind), a
        // wider rect and gilt ink; the state itself is carried by the LABEL TEXT ("CONFIRM"
        // vs "CONFIRM 2, -40").
        //
        // Each button stretches 0..1 inside the fixed ActionRowPx band, so its resolved height
        // IS MinTouchPx and ClampMinTouch has nothing to grow. That growth — 32 px of fraction
        // band inflated to 112 — is what put this row on top of the grid.
        private void BuildActionRow(RectTransform row)
        {
            const float cancelX0 = 0.020f, cancelX1 = 0.250f;
            const float respecX0 = 0.270f, respecX1 = 0.560f;
            const float confirmX0 = 0.620f, confirmX1 = 0.980f;

            _cancelBtn = ElarionUiKit.ButtonPack(row, "Cancel", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(cancelX0, 0f), new Vector2(cancelX1, 1f),
                () => { if (_vm != null) _vm.CancelPlan(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            StyleActionLabel(_cancelBtn, ElarionUi.Parchment);

            // RESPEC — refund this hero's talents for a Crystal cost (owner F8 "no respec option").
            // Surfaces the legacy TalentTreePanel respec on the LIVE MVVM panel via vm.Respec().
            int respecCost = _vm != null ? _vm.RespecCost : HeroSkillTreeVMRespecFallbackCost;
            _respecBtn = ElarionUiKit.ButtonPack(row, "Respec " + respecCost + "c", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(respecX0, 0f), new Vector2(respecX1, 1f),
                () => { if (_vm != null) _vm.Respec(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            StyleActionLabel(_respecBtn, ElarionUi.Parchment);

            // PRIMARY emphasis ring — built BEFORE the button so the button's own plate draws
            // over it and only the rim peeks. Procedural + rect-bounded: it cannot bleed the way
            // the 9-sliced green pack fill did.
            var ring = new GameObject("ConfirmEmphasis", typeof(Image));
            ring.transform.SetParent(row, false);
            var ringRt = (RectTransform)ring.transform;
            ringRt.anchorMin = new Vector2(confirmX0, 0f);
            ringRt.anchorMax = new Vector2(confirmX1, 1f);
            ringRt.offsetMin = new Vector2(-5f, -5f);
            ringRt.offsetMax = new Vector2(5f, 5f);
            var ringImg = ring.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(ringImg);
            ringImg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.80f);
            ringImg.raycastTarget = false;

            _confirmBtn = ElarionUiKit.ButtonPack(row, "CONFIRM", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(confirmX0, 0f), new Vector2(confirmX1, 1f),
                () => { if (_vm != null) _vm.ConfirmOrAssign(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            _confirmLabel = StyleActionLabel(_confirmBtn, ElarionUi.Gilt);

            // §B.2 — respec status is a transient toast (see Render), not a persistent strip.
            // Close is the SHARED bottom-band Obsidian Close button (WO-554) — no per-panel Close.
        }

        // One label treatment for the whole action row: bold, fitted, ink by EMPHASIS only.
        private static TMPro.TextMeshProUGUI StyleActionLabel(Button btn, Color ink)
        {
            var lbl = btn != null ? btn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (lbl == null) return null;
            lbl.color = ink;
            lbl.fontStyle = TMPro.FontStyles.Bold;
            ElarionUiKit.FitSingleLine(lbl);   // never spills, never clips ("Respec 300 Cry...")
            return lbl;
        }

        // Display-only fallback if the button is built before the VM binds (cost still comes
        // from HeroTalentCatalog at click time via vm.Respec); matches RespecCostCrystals default.
        private const int HeroSkillTreeVMRespecFallbackCost = 300;

        private static void SetButtonAlpha(Button btn, float a)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) { var c = img.color; c.a = a; img.color = c; }
        }

        // ── Quick-swap row (folds the loadout screen into this panel) ─────────────

        // WO-865 — the ABILITY BAND: ONE row of slot tiles across the graph column, inside a
        // band that is already AbilityRowPx tall (>= the kit touch floor). The old 2x2
        // grid sliced a 0.21-of-body host into 1/2 x 1/2 fractions: each tile resolved to ~47 px
        // (below the touch floor) and its NAME band to ~23 px — under the FontFloor line box, so
        // "Emberbrand Throw" ellipsized to "Emberbrand Thro" and the touch-floor guard grew the
        // action buttons straight over slots 3 and 4.
        private void RebuildQuickSlots()
        {
            ClearChildren(_quickRoot);
            if (_quickRoot == null || _vm == null) return;

            var slots = _vm.QuickSlots;
            int n = slots != null ? slots.Count : 0;
            if (n <= 0) return;

            // Horizontal split only — the tile HEIGHT is the band, so it cannot under-run the
            // touch floor no matter how many slots exist.
            float gapX = SlotGapFrac;
            float w = (1f - gapX * (n - 1)) / n;
            bool assignTarget = _vm.SelectedIsAssignable;
            for (int i = 0; i < n; i++)
            {
                float x0 = i * (w + gapX);
                BuildQuickSlotTile(_quickRoot.transform, slots[i], x0, x0 + w, assignTarget);
            }
        }

        private void BuildQuickSlotTile(Transform parent, LoadoutSlotVM slot,
                                        float x0, float x1, bool assignTarget)
        {
            var tile = new GameObject("Quick_" + slot.SlotKey, typeof(Image), typeof(Button));
            tile.transform.SetParent(parent, false);
            var rt = tile.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = tile.GetComponent<Image>();
            Sprite plate = RpgUiCatalog.Get("slot", "slot_talent");
            if (plate != null) { img.sprite = plate; img.type = Image.Type.Sliced; }
            else ElarionUiKit.ApplyRounded(img);

            // Once an assignable skill is selected, every slot glows gold (the tap target);
            // empty reads as a quiet socket, filled reads gold-warm. Tap a filled slot with
            // nothing assignable selected to clear it.
            Color fill;
            if (assignTarget) fill = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.50f);
            else if (slot.IsEmpty) fill = ElarionUiKit.Track;
            else fill = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.22f);
            img.color = fill;

            var btn = tile.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            int idx = slot.SlotIndex;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.AssignSelectedToSlot(idx); });

            // FIXED-PIXEL line boxes inside the tile: numeral over name, each a whole line.
            var keyLbl = ElarionUiKit.Label(tile.transform, slot.SlotKey, 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
            PinBandFromTop(keyLbl.rectTransform, SlotPadPx, SlotKeyBandPx);
            ElarionUiKit.FitSingleLine(keyLbl);

            // Text-encoded state (never colour alone — the owner is red/green colourblind):
            // an empty slot SAYS so.
            string body = slot.IsEmpty ? (assignTarget ? "tap to set" : "empty") : slot.AbilityName;
            Color bodyColor = slot.IsEmpty
                ? (assignTarget ? ElarionUi.Gilt : ElarionUi.ParchmentDim)
                : ElarionUi.Parchment;
            var bodyLbl = ElarionUiKit.Label(tile.transform, body, 0f, 1f, bodyColor,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center,
                SlotTextInsetFrac, 1f - SlotTextInsetFrac, bold: !slot.IsEmpty);
            PinBandFromBottom(bodyLbl.rectTransform, SlotPadPx, SlotNameBandPx);
            // TWO whole FontFloor line boxes in a ~250 px tile: the name WRAPS rather than
            // ellipsizing, so "Emberbrand Throw" / "Suppressing Volley" read in FULL.
            ElarionUiKit.FitBlock(bodyLbl);
        }

        // ── Black-grid node-canvas sprite (generated once) ────────────────────────

        private static Sprite s_gridSprite;
        private static bool s_gridTried;
        private static Sprite GridSprite()
        {
            if (s_gridSprite != null || s_gridTried) return s_gridSprite;
            s_gridTried = true;
            try
            {
                const int S = 64;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Bilinear;
                var bg = new Color(0.012f, 0.012f, 0.016f, 1f);  // near-black cell
                var line = new Color(0.15f, 0.16f, 0.21f, 1f);   // faint grid rule
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                        px[y * S + x] = (x == 0 || y == 0) ? line : bg;
                tex.SetPixels(px);
                tex.Apply();
                s_gridSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                s_gridSprite.name = "SkillTreeGrid";
            }
            catch { s_gridSprite = null; }   // WebGL/headless guard — flat-black fallback used
            return s_gridSprite;
        }

        // Icon cache — Resources.Load is cheap but cached avoids reloading every Render.
        private static readonly Dictionary<string, Sprite> s_iconCache = new Dictionary<string, Sprite>();
        private static Sprite LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (s_iconCache.TryGetValue(path, out var cached)) return cached;
            Sprite sp = Resources.Load<Sprite>(path);
            s_iconCache[path] = sp;   // cache nulls too (atlas not sliced yet) so we don't retry each frame
            return sp;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearContent()
        {
            if (_graphContent == null) return;
            for (int i = _graphContent.childCount - 1; i >= 0; i--)
            {
                var c = _graphContent.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void ClearChildren(GameObject host)
        {
            if (host == null) return;
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                var c = host.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            _wisdomChip = null;
            _confirmBtn = null;
            _confirmLabel = null;
            _cancelBtn = null;
            _respecBtn = null;
            _detailName = null;
            _detailDesc = null;
            _detailState = null;
            _quickRoot = null;
            _lastQuickStatus = null;
            _lastRespecStatus = null;
            _detailGroup = null;
            _emptyGroup = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _graphContent = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
