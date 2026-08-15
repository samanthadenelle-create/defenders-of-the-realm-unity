// =============================================================================
// HeroSkillTreePanelMvvm — the hero skill-tree VIEW (MVVM slice). A DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// WO-896 (owner 2026-08-15) — SPARSE OBSIDIAN TALENT GRAPH, not a dense grid and
// not the busy "horizontal tracks + MORE BELOW" mitigation. Authoritative look =
// the Obsidian kit Talent Tree demo (Screenshot 2026-08-15 175853.png):
//   * Few LARGE nodes, lots of dark empty space
//   * Gold connectors along prerequisites (diagonal OK)
//   * One thick gold FOCUS plate for the selected / next node
//   * Rank pip on the plate ("1/1" owned, "0/1" locked)
//   * Calm chrome — FrameTalent + crest title "TALENT TREE"
//   * Detail / loadout / Cancel-Respec-CONFIRM stay (game needs them; demo is pure)
//
// COLOURBLIND LAW (owner is red/green colourblind): every state is separable with
// colour stripped. Owned = filled plate + rank 1/1; Next/Selected = oversized +
// thick gold outer frame; Locked = dim + padlock; Inert = slash + "!". Connectors
// differ by THICKNESS (solid 8 / solid 4 / dashed 3), never by hue.
//
// WO-865 FIXED-PIXEL BANDS still own the outer chrome (action / ability / detail).
// The graph itself is a free-form scroll canvas on authored x/y (auto-layout when
// unset) so nodes never pack into a spreadsheet.
//
// Code-built uGUI ONLY (no UXML — §8). Registers PanelId.HeroSkillTree.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;
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

        /// <summary>A section label's own line box (FontMicro+2 -> line ~44).</summary>
        public const float SectionBandPx = 46f;
        /// <summary>Clear air above and below a section label.</summary>
        public const float SectionGapPx = 14f;
        /// <summary>The MINIMUM clear pitch a node row must own: one node plate plus a label
        /// band plus air on both sides. WO-865 introduced it to stop a plate painting over the
        /// "Universal - any class" divider; WO-896 keeps it as the FLOOR every track row is
        /// clamped to in RebuildTracks, so a row can never squeeze its plate against its name.</summary>
        public const float SectionClearPx = NodeSizePx + SectionGapPx * 2f + SectionBandPx;

        // ── RETIRED LATTICE (WO-896) — the authored x/y node GRAPH no longer drives layout.
        // These four constants stayed because they are still TRUE of the authored data in
        // hero-talents.json (which keeps its x/y/section authoring) and because
        // SkillsPanelLayoutRegression [grid] pins them against that data. Re-pointing that
        // oracle at the track layout is its own ticket — deleting the constants here would
        // simply blind it. NOTHING in this file positions a node from them any more.

        /// <summary>Retired lattice: reference px per 1.0 of authored node X (see the note above).</summary>
        public const float GraphUnitWpx = 1180f;
        /// <summary>Retired lattice: reference px per 1.0 of authored node Y.</summary>
        public const float GraphUnitHpx = 780f;
        /// <summary>Retired lattice: content padding of half a node plate plus air.</summary>
        public const float GraphPadPx = NodeSizePx * 0.5f + 16f;
        /// <summary>Retired lattice: authored Y the "Universal - any class" band divided the graph at.</summary>
        public const float SectionBandY = 0.965f;

        // ── WO-896 SPARSE GRAPH LATTICE (Obsidian demo north star) ─────────────────
        // Nodes sit on an authored 0..1 canvas (hero-talents.json x/y). GraphUnit* maps
        // that canvas to fixed reference pixels so the lattice still clears a full node
        // plate (SkillsPanelLayoutRegression [grid] pins this). Unset nodes auto-layout
        // from tier/column without packing into a dense grid.

        /// <summary>FOCUS plate (selected OR track "next") — deliberately LARGER so it
        /// reads at arm's length with colour stripped (colourblind law).</summary>
        public const float NodeFocusPx = 148f;

        /// <summary>Connector stroke for an earned path (both ends owned/planned).</summary>
        public const float ConnectorThickPx = 8f;
        /// <summary>Connector stroke for the live frontier (parent owned, child not).</summary>
        public const float ConnectorMidPx = 5f;
        /// <summary>Connector stroke for not-yet-earned links.</summary>
        public const float ConnectorThinPx = 3f;

        /// <summary>How far a connector stops short of each plate centre so the line
        /// ends at the plate rim rather than running under the icon.</summary>
        public const float ConnectorInsetFrac = 0.48f;

        /// <summary>Rank pip ("1/1") band seated on the TOP of every plate — demo look.</summary>
        public const float RankBandPx = 28f;

        // Track-row leftovers kept so SkillsPanelLayoutRegression / older names stay
        // resolvable; the live layout never builds track rows.
        public const float TrackTitleWpx = 0f;
        public const float NodePitchPx = 200f;
        public const float NodeNextPx = NodeFocusPx;
        public const float NodeLabelBandPx = 0f;
        public const float NodeLabelGapPx = 0f;
        public const float TrackTopPadPx = 10f;
        public const float TrackRowPx = NodeSizePx + TrackTopPadPx * 2f;
        public const float TrackGapPx = 24f;
        public const int TrackWrapCount = 4;
        public const float ContentPadPx = GraphPadPx;
        public const float TrackKindBandPx = 38f;
        public const float TrackNameBandPx = 76f;
        public const float TrackNoteBandPx = 38f;

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

        // Last drawn layout signature — §12 instrumentation logs a line only when the drawn
        // shape CHANGES, never once per Render (Render fires on every selection tap).
        private string _lastLayoutSig;

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

            RebuildTracks();
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

        // ── WO-896: SPARSE TALENT GRAPH (Obsidian demo) ───────────────────────────
        // Flatten every track seat onto a free-form canvas. Authored x/y drive placement
        // when present; missing seats auto-layout by tier/column (and branch) with room
        // to breathe. Gold connectors follow real prerequisites (diagonal OK). No name
        // labels under plates (detail column owns the copy). No "MORE BELOW" cue.

        private void RebuildTracks()
        {
            if (_graphContent == null || _vm == null) { ClearContent(); return; }
            Vector2 keptScroll = _graphContent.anchoredPosition;
            ClearContent();

            var seats = new List<SkillTrackNodeVM>(64);
            var byId = new Dictionary<string, SkillTrackNodeVM>(64);
            var tracks = _vm.Tracks;
            if (tracks != null)
            {
                for (int t = 0; t < tracks.Count; t++)
                {
                    var track = tracks[t];
                    if (track == null || track.Nodes == null) continue;
                    for (int i = 0; i < track.Nodes.Count; i++)
                    {
                        var seat = track.Nodes[i];
                        if (string.IsNullOrEmpty(seat.Node.Id)) continue;
                        seats.Add(seat);
                        byId[seat.Node.Id] = seat;
                    }
                }
            }

            if (seats.Count == 0)
            {
                var empty = ElarionUiKit.Label(_graphContent, "No talents to show yet.", 0f, 1f,
                    ElarionUi.ParchmentDim, ElarionUi.FontBody,
                    TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                ElarionUiKit.FitSingleLine(empty);
                FlowTrace.Warn("SkillTree", "sparse graph drew ZERO nodes - the graph well is empty");
                _graphContent.sizeDelta = new Vector2(
                    ContentPadPx * 2f + GraphUnitWpx * 0.5f,
                    ContentPadPx * 2f + GraphUnitHpx * 0.4f);
                return;
            }

            // Norm positions (0..1+, may extend past 1 for auto secondary branches).
            var norm = new Dictionary<string, Vector2>(seats.Count);
            ResolveGraphNorms(seats, norm);

            float pad = GraphPadPx;
            float maxX = 0f, maxY = 0f;
            var centers = new Dictionary<string, Vector2>(seats.Count);
            foreach (var kv in norm)
            {
                float cx = pad + kv.Value.x * GraphUnitWpx;
                float cyDown = pad + kv.Value.y * GraphUnitHpx; // px DOWN from content top
                centers[kv.Key] = new Vector2(cx, cyDown);
                if (cx > maxX) maxX = cx;
                if (cyDown > maxY) maxY = cyDown;
            }

            float contentW = maxX + pad + NodeFocusPx;
            float contentH = maxY + pad + NodeFocusPx + RankBandPx;
            _graphContent.sizeDelta = new Vector2(contentW, contentH);

            // Connectors FIRST so opaque plates draw over their ends.
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                var prereqs = seat.Node.Prereqs;
                if (prereqs == null) continue;
                if (!centers.TryGetValue(seat.Node.Id, out var to)) continue;
                for (int p = 0; p < prereqs.Count; p++)
                {
                    string pr = prereqs[p];
                    if (string.IsNullOrEmpty(pr)) continue;
                    if (!centers.TryGetValue(pr, out var from)) continue;
                    if (!byId.TryGetValue(pr, out var parentSeat)) continue;
                    BuildGraphConnector(_graphContent, from, to, parentSeat, seat);
                }
            }

            string selectedId = _vm.SelectedNodeId ?? "";
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (!centers.TryGetValue(seat.Node.Id, out var c)) continue;
                bool focus = seat.State == SkillNodeState.Next
                          || (!string.IsNullOrEmpty(selectedId) && selectedId == seat.Node.Id);
                Guard.Try("SkillTree", "build graph node '" + seat.Node.Id + "'",
                    () => BuildGraphNode(seat, c.x, c.y, focus));
            }

            // Keep scroll place; rest = flush top-left so the first nodes are whole.
            var well = _graphContent.parent as RectTransform;
            float wellW = well != null ? well.rect.width : 0f;
            float wellH = well != null ? well.rect.height : 0f;
            float maxDown = wellH > 1f ? Mathf.Max(0f, contentH - wellH) : 0f;
            float maxRight = wellW > 1f ? Mathf.Max(0f, contentW - wellW) : 0f;
            _graphContent.anchoredPosition = new Vector2(
                Mathf.Clamp(keptScroll.x, -maxRight, 0f),
                Mathf.Clamp(keptScroll.y, 0f, maxDown));

            string sig = seats.Count + "/" + contentW.ToString("F0") + "x" + contentH.ToString("F0");
            if (sig != _lastLayoutSig)
            {
                _lastLayoutSig = sig;
                FlowTrace.Step("SkillTree", "sparse graph drawn: " + seats.Count + " node(s), content " +
                                            contentW.ToString("F0") + "x" + contentH.ToString("F0") + " px");
            }
        }

        /// <summary>Map every seat to a 0..1(+)-ish canvas position. Authored x/y win;
        /// missing seats auto-layout by tier/column so ranger/mage (no authoring) and
        /// knight secondary branches still read as a calm tree, not a packed grid.</summary>
        private static void ResolveGraphNorms(List<SkillTrackNodeVM> seats,
                                              Dictionary<string, Vector2> norm)
        {
            // Pass 1 — authored.
            int autoIdx = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                var n = seats[i].Node;
                if (n.X >= 0f && n.Y >= 0f)
                    norm[n.Id] = new Vector2(n.X, n.Y);
            }

            // Pass 2 — missing: prefer a seat under a placed prerequisite, else tier/column.
            for (int pass = 0; pass < 3; pass++)
            {
                bool any = false;
                for (int i = 0; i < seats.Count; i++)
                {
                    var n = seats[i].Node;
                    if (norm.ContainsKey(n.Id)) continue;
                    any = true;

                    Vector2? under = null;
                    if (n.Prereqs != null)
                    {
                        for (int p = 0; p < n.Prereqs.Count; p++)
                        {
                            string pr = n.Prereqs[p];
                            if (!string.IsNullOrEmpty(pr) && norm.TryGetValue(pr, out var pp))
                            {
                                // Slight fan-out so multi-children of one parent don't stack.
                                float fan = ((autoIdx++ % 5) - 2) * 0.06f;
                                under = new Vector2(
                                    Mathf.Clamp01(pp.x + fan),
                                    Mathf.Min(1.15f, pp.y + 0.16f));
                                break;
                            }
                        }
                    }
                    if (under.HasValue) { norm[n.Id] = under.Value; continue; }

                    // Tier/column fallback (shared rides a calm bottom band).
                    float y;
                    if (n.IsShared) y = 0.94f;
                    else
                    {
                        int t = Mathf.Clamp(n.Tier <= 0 ? 1 : n.Tier, 1, 4);
                        y = 0.14f + (t - 1) * 0.18f;
                        if (n.Column >= 5) y = Mathf.Min(1.05f, y + 0.10f);
                    }
                    int col = Mathf.Max(0, n.Column);
                    float x = 0.08f + (col % 10) * 0.09f;
                    if (n.IsShared) x = 0.08f + (i % 8) * 0.11f;
                    norm[n.Id] = new Vector2(Mathf.Clamp(x, 0.04f, 1.10f), y);
                }
                if (!any) break;
            }
        }

        // Gold progression line between two plate centres. Thickness = state (colourblind).
        private static void BuildGraphConnector(Transform host, Vector2 fromDown, Vector2 toDown,
                                                SkillTrackNodeVM parent, SkillTrackNodeVM child)
        {
            // Content space: +x right, +yDown down. Image rotation uses the same axes once
            // we convert yDown -> anchored y (-yDown).
            float x0 = fromDown.x, y0 = fromDown.y;
            float x1 = toDown.x, y1 = toDown.y;
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 8f) return;

            float inset = NodeSizePx * ConnectorInsetFrac;
            if (len <= inset * 2f + 4f) return;
            float ux = dx / len, uy = dy / len;
            float ax = x0 + ux * inset, ay = y0 + uy * inset;
            float bx = x1 - ux * inset, by = y1 - uy * inset;
            float mx = (ax + bx) * 0.5f, my = (ay + by) * 0.5f;
            float barLen = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            // Atan2 of (dx, -dy) because anchored Y is flipped vs our down-positive layout.
            float angle = Mathf.Atan2(-(by - ay), (bx - ax)) * Mathf.Rad2Deg;

            bool parentActive = parent.State == SkillNodeState.Owned || parent.State == SkillNodeState.Planned;
            bool childActive = child.State == SkillNodeState.Owned || child.State == SkillNodeState.Planned;
            float thick, alpha;
            if (parentActive && childActive) { thick = ConnectorThickPx; alpha = 0.92f; }
            else if (parentActive) { thick = ConnectorMidPx; alpha = 0.82f; }
            else { thick = ConnectorThinPx; alpha = 0.40f; }

            if (parentActive || childActive)
                BuildRotatedBar(host, mx, my, barLen, thick, angle, alpha);
            else
                BuildRotatedDash(host, ax, ay, bx, by, thick, alpha);
        }

        private static void BuildRotatedBar(Transform host, float cxDown, float cyDown,
                                            float length, float thickness, float angleDeg, float alpha)
        {
            var go = new GameObject("Edge", typeof(Image));
            go.transform.SetParent(host, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(length, thickness);
            rt.anchoredPosition = new Vector2(cxDown, -cyDown);
            rt.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, alpha);
            img.raycastTarget = false;
        }

        private static void BuildRotatedDash(Transform host, float ax, float ay, float bx, float by,
                                             float thickness, float alpha)
        {
            float dx = bx - ax, dy = by - ay;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 4f) return;
            float angle = Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg;
            const float dash = 14f, gap = 10f;
            float step = dash + gap;
            float ux = dx / len, uy = dy / len;
            for (float d = 0f; d < len; d += step)
            {
                float seg = Mathf.Min(dash, len - d);
                if (seg < 2f) break;
                float mx = ax + ux * (d + seg * 0.5f);
                float my = ay + uy * (d + seg * 0.5f);
                BuildRotatedBar(host, mx, my, seg, thickness, angle, alpha);
            }
        }

        // ── ONE NODE ON THE GRAPH ────────────────────────────────────────────────
        // COLOURBLIND LAW matrix (unchanged intent, sparse presentation):
        //  state      plate FILL        border   size   badge SHAPE          rank
        //  ---------  ----------------  -------  -----  -------------------  -----
        //  Owned      SOLID (light)     2 px     112    check tick           1/1
        //  Planned    solid + ring      3 px     112    "-n" text pip        0/1
        //  Next       hollow + focus    6 px     148    cost number pip      0/1
        //  Available  hollow            3 px     112    cost number pip      0/1
        //  Inert      hollow + SLASH    3 px     112    "!" pip              0/1
        //  Locked     hollow (dimmest)  1.5 px   112    PADLOCK glyph        0/1
        // Selected also wears the focus frame even when not Next.

        private void BuildGraphNode(SkillTrackNodeVM seat, float cxDown, float cyDown, bool focus)
        {
            var node = seat.Node;
            if (string.IsNullOrEmpty(node.Id)) return;
            var state = seat.State;

            float size = focus ? NodeFocusPx : NodeSizePx;

            var go = new GameObject("Node_" + node.Id, typeof(Image), typeof(Button));
            go.transform.SetParent(_graphContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cxDown, -cyDown);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            // Obsidian talent border when mirrored — demo plate art; rounded fallback otherwise.
            Sprite border = TalentBorderSprite(node);
            if (border != null)
            {
                img.sprite = border;
                img.type = Image.Type.Sliced;
                img.color = BorderTintFor(state, focus);
            }
            else
            {
                ElarionUiKit.ApplyRounded(img);
                img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, BorderAlphaFor(state));
            }

            float borderW = BorderWidthFor(state);
            if (focus) borderW = Mathf.Max(borderW, 6f);

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            // Talent borders have ornate rims — inset the fill so the kit art rim shows.
            float inset = border != null ? size * 0.14f : borderW;
            fillRt.offsetMin = new Vector2(inset, inset);
            fillRt.offsetMax = new Vector2(-inset, -inset);
            var fillImg = fillGo.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(fillImg);
            fillImg.color = PlateFillFor(state);
            fillImg.raycastTarget = false;

            // Focus = thick gold outer frame (demo's selected plate).
            if (focus)
                BuildOuterRing(go.transform, 0.07f,
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.98f));
            else if (node.IsCapstone)
                BuildOuterRing(go.transform, 0.05f,
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                              state == SkillNodeState.Locked ? 0.40f : 0.85f));
            if (state == SkillNodeState.Planned)
                BuildOuterRing(go.transform, 0.05f,
                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.9f));

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            btn.interactable = true;
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Select(id); });

            bool dim = state == SkillNodeState.Locked || state == SkillNodeState.Inert;
            var sprite = LoadIcon(node.IconPath);
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.22f, 0.20f);
                ir.anchorMax = new Vector2(0.78f, 0.76f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = sprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = state == SkillNodeState.Owned ? new Color(0.10f, 0.09f, 0.08f, 0.95f)
                           : dim ? new Color(0.82f, 0.80f, 0.77f, 0.35f)
                           : new Color(0.86f, 0.84f, 0.81f, 0.95f);
            }
            else
            {
                string mono = string.IsNullOrEmpty(node.Name)
                    ? "?" : node.Name.Substring(0, Mathf.Min(2, node.Name.Length));
                var monoLbl = ElarionUiKit.Label(go.transform, mono, 0.24f, 0.76f,
                    state == SkillNodeState.Owned ? new Color(0.10f, 0.09f, 0.08f, 1f)
                        : dim ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                ElarionUiKit.FitSingleLine(monoLbl);
            }

            // Rank pip — demo "3/3" grammar; our unlocks are binary so 1/1 or 0/1.
            string rank = (state == SkillNodeState.Owned || state == SkillNodeState.Planned) ? "1/1" : "0/1";
            var rankLbl = ElarionUiKit.Label(go.transform, rank, 0.08f, 0.34f,
                state == SkillNodeState.Owned ? ElarionUi.Gilt : ElarionUi.Parchment,
                (int)ElarionUiKit.FontFloor, TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
            rankLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(rankLbl);

            if (state == SkillNodeState.Inert) BuildNodeSlash(go.transform, size);

            switch (state)
            {
                case SkillNodeState.Owned:
                    BuildNodeCheckStamp(go.transform);
                    break;
                case SkillNodeState.Planned:
                    BuildNodeStamp(go.transform, "-" + node.WisdomCost, ElarionUi.Affordable);
                    break;
                case SkillNodeState.Next:
                case SkillNodeState.Available:
                    BuildNodeStamp(go.transform, node.WisdomCost.ToString(), ElarionUi.Parchment);
                    break;
                case SkillNodeState.Inert:
                    BuildNodeStamp(go.transform, "!", ElarionUi.Parchment);
                    break;
                default:
                    BuildNodeLockGlyph(go.transform);
                    break;
            }
        }

        private static Sprite TalentBorderSprite(SkillNodeVM node)
        {
            // Capstone uses the ornate 6-slot border; tiers 1-4 pick talent_N / slot_talent_N.
            if (node.IsCapstone)
            {
                var cap = RpgUiCatalog.Get("slot", "slot_talent_6");
                if (cap != null) return cap;
            }
            int t = Mathf.Clamp(node.Tier <= 0 ? 1 : node.Tier, 1, 4);
            var sp = RpgUiCatalog.Get("slot", "slot_talent_" + t);
            if (sp != null) return sp;
            sp = RpgUiCatalog.Get("slot", "talent_" + t);
            if (sp != null) return sp;
            return RpgUiCatalog.Get("slot", "slot_talent");
        }

        private static Color BorderTintFor(SkillNodeState state, bool focus)
        {
            if (focus) return new Color(1f, 0.92f, 0.55f, 1f);
            switch (state)
            {
                case SkillNodeState.Owned:
                    return new Color(1f, 0.88f, 0.45f, 1f);
                case SkillNodeState.Planned:
                    return new Color(0.85f, 0.95f, 0.70f, 1f);
                case SkillNodeState.Next:
                case SkillNodeState.Available:
                    return new Color(0.95f, 0.90f, 0.70f, 0.95f);
                case SkillNodeState.Inert:
                    return new Color(0.55f, 0.55f, 0.55f, 0.70f);
                default:
                    return new Color(0.45f, 0.45f, 0.50f, 0.55f);
            }
        }

        /// <summary>A fixed-pixel child rect seated from its parent's TOP-LEFT corner
        /// (<paramref name="y"/> counts DOWN). Kept for band helpers / ability tiles.</summary>
        private static RectTransform PxRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        // A rounded ring behind the plate, peeking <paramref name="grow"/> of the plate on
        // every side (the plate is opaque and drawn over it).
        private static void BuildOuterRing(Transform nodeRoot, float grow, Color color)
        {
            var ring = new GameObject("Ring", typeof(Image));
            ring.transform.SetParent(nodeRoot, false);
            var rr = ring.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(-grow, -grow);
            rr.anchorMax = new Vector2(1f + grow, 1f + grow);
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var rImg = ring.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(rImg);
            rImg.color = color;
            rImg.raycastTarget = false;
            ring.transform.SetAsFirstSibling();
        }

        // WO-910 inert mark — one bar struck across the plate face.
        private static void BuildNodeSlash(Transform nodeRoot, float size)
        {
            var bar = new GameObject("InertSlash", typeof(Image));
            bar.transform.SetParent(nodeRoot, false);
            var br = (RectTransform)bar.transform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = Vector2.zero;
            br.sizeDelta = new Vector2(size * 0.74f, 5f);
            br.localRotation = Quaternion.Euler(0f, 0f, -22f);
            var bImg = bar.GetComponent<Image>();
            bImg.color = new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.75f);
            bImg.raycastTarget = false;
        }

        // Locked = a PADLOCK drawn from four bars inside the badge disc (font-free: the TMP
        // font has no lock glyph, and a SHAPE satisfies the colourblind law).
        private static void BuildNodeLockGlyph(Transform nodeRoot)
        {
            var pr = BuildNodeStamp(nodeRoot, null, Color.clear);
            var ink = new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.85f);

            LockBar(pr, new Vector2(0.5f, 0.36f), new Vector2(26f, 20f), ink);   // body
            LockBar(pr, new Vector2(0.34f, 0.62f), new Vector2(4f, 16f), ink);   // shackle left
            LockBar(pr, new Vector2(0.66f, 0.62f), new Vector2(4f, 16f), ink);   // shackle right
            LockBar(pr, new Vector2(0.5f, 0.74f), new Vector2(20f, 4f), ink);    // shackle top
        }

        private static void LockBar(Transform parent, Vector2 anchor, Vector2 size, Color ink)
        {
            var go = new GameObject("Lock", typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = ink;
            img.raycastTarget = false;
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

        // Plate FILL by state. OWNED is the only FILLED plate — a solid light face against
        // five dark ones, i.e. a luminance step, not a hue (colourblind law). PLANNED is
        // half-filled, so "already yours" and "about to be yours" still differ in greyscale.
        private static Color PlateFillFor(SkillNodeState state)
        {
            switch (state)
            {
                case SkillNodeState.Owned:
                    return new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.88f);
                case SkillNodeState.Planned:
                    return new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.42f);
                case SkillNodeState.Locked:
                    return new Color(0.030f, 0.028f, 0.040f, 0.96f);
                case SkillNodeState.Inert:
                    return new Color(0.040f, 0.038f, 0.050f, 0.96f);
                default:
                    return new Color(0.055f, 0.050f, 0.070f, 0.96f);
            }
        }

        // Gilt LINE border alpha by state (paired with BorderWidthFor — the WIDTH is the
        // colour-free signal; the alpha only reinforces it).
        private static float BorderAlphaFor(SkillNodeState state)
        {
            switch (state)
            {
                case SkillNodeState.Owned: return 0.95f;
                case SkillNodeState.Planned: return 0.90f;
                case SkillNodeState.Next: return 1.00f;
                case SkillNodeState.Available: return 0.75f;
                case SkillNodeState.Inert: return 0.45f;
                default: return 0.28f;
            }
        }

        // Border WIDTH in px — the focus node's ring is 4x the locked node's.
        private static float BorderWidthFor(SkillNodeState state)
        {
            switch (state)
            {
                case SkillNodeState.Next: return 6f;
                case SkillNodeState.Planned: return 3f;
                case SkillNodeState.Available: return 3f;
                case SkillNodeState.Inert: return 3f;
                case SkillNodeState.Owned: return 2f;
                default: return 1.5f;
            }
        }

        // ── Chrome (presentation only) ────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("HeroSkillTreePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header + ONE Close.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "TALENT TREE",
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
            // Calm dark canvas (Obsidian demo): near-black slab, no busy grid lines.
            // Opaque so it overrides the frame fill; raycastable so drag-scroll still works.
            vImg.color = new Color(0.018f, 0.016f, 0.022f, 1f);

            var contentGo = new GameObject("GraphContent", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _graphContent = contentGo.GetComponent<RectTransform>();
            // TOP-LEFT anchored + pivoted (was centre-anchored). Centring a content rect WIDER
            // than the mask is exactly what sliced a node plate off BOTH frame edges in the
            // 2026-08-04 capture. Flush top-left + ContentPadPx of padding means the rest
            // position always shows the FIRST TRACK ROW WHOLE — plate and name label — which is
            // the WO-896 "no clipped top row" criterion; later rows are reached by scrolling.
            // RebuildTracks sizes the rect in exact pixels.
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0f, 1f);
            _graphContent.pivot = new Vector2(0f, 1f);
            _graphContent.sizeDelta = new Vector2(
                ContentPadPx * 2f + GraphUnitWpx,
                ContentPadPx * 2f + GraphUnitHpx * 0.55f);
            _graphContent.anchoredPosition = Vector2.zero;

            // WO-896: no MORE BELOW cue — sparse graph + scroll is the product.

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
            _lastLayoutSig = null;
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
