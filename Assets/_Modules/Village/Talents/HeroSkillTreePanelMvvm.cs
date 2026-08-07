// =============================================================================
// HeroSkillTreePanelMvvm — the Knight skill-tree VIEW (MVVM slice). A DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// WO-896 (2026-08-05) — PROGRESSION LINE, not a grid. Owner ruling: "this skill tree
// is hard to read - simplify the tree, just have the skills connected by a line showing
// progression." The authored-x/y NODE GRAPH is retired. The panel now draws vm.Tracks:
// each track is one horizontal LINE of nodes (left = earlier, right = later) with a
// title block on its left and a connecting line running THROUGH the node centres; tracks
// stack top-down and scroll. A node click STAGES / UNSTAGES it into a plan; nothing is
// spent until CONFIRM (plan→commit flow, unchanged). ALL state/logic (track order, node
// state, lock reasons, pending set, plan cost) lives in HeroSkillTreeVM — the View never
// reads game state (ui-mvvm-binding-seam rule). The LOADOUT (quick-swap) band, the detail
// column, Cancel/Respec/CONFIRM and the WISDOM chip are untouched by WO-896.
//
// COLOURBLIND LAW (owner is red/green colourblind — the binding constraint of WO-896):
// every state is separable with COLOUR STRIPPED OUT. Owned = the only FILLED plate +
// check tick; Next = the only OVERSIZED plate (132 vs 112) + double ring + a "NEXT: "
// label prefix; Locked = thinnest border + PADLOCK glyph; Inert (WO-910: no runtime
// consumer) = a SLASH across the plate + "!" badge + "[!] " prefix. Connectors differ by
// THICKNESS and DASH PATTERN (solid 8 / solid 4 / dashed 3 / dotted 2), never by hue.
// See the full matrix above BuildTrackNode.
//
// WO-865 (2026-08-04, from the real Seeker capture 07-skills-panel.png): the panel
// body is laid out as DISJOINT FIXED-PIXEL BANDS, never fractions of the body well.
// See the geometry block below the field list for the arithmetic that proves why —
// in one line: FrameTalent's body resolves to ~493 px tall at 2340x1080, the action
// row was a 0.065 fraction of it (32 px), and ElarionUiKit.ClampMinTouch then grew
// each button to the 112 px touch floor SYMMETRICALLY, straight over the graph well
// and the quick-swap slots. Same failure class as WO-841 / WO-852. The stack now is
//   columns region (graph well + ability band | detail column)  /  action row
// and the tracks live on a fixed-pixel row lattice inside a RectMask2D scroll well
// (WO-896 replaced the authored x/y lattice + its reserved section-band row with
// stacked track rows; every row still clears the SectionClearPx floor).
//
// WO-676 §B (owner-approved icon-only redesign, 2026-07-11; AMENDED by WO-896): nodes
// are icon plates carrying ONE state badge — cost pip (available), -n pip + ring
// (planned), check stamp (owned), padlock (locked), "!" + slash (inert). WO-896 adds
// the SHORT NAME under each plate (the line has to be readable without tapping); the
// full desc/state text still lives in the right-hand detail column. Wisdom is a
// CurrencyChip (top-right);
// the plan summary folds into the CONFIRM label ("CONFIRM n · −cost"); quick-swap
// and respec feedback are transient toasts (BuildFeedbackToast) — target ≤2
// persistent text strips outside the graph. Colorblind law: every state carries a
// shape/stamp/pip, never hue alone (dim = luminance, pips/stamps = shape+text).
//
// Code-built uGUI ONLY (no UXML — §8). Line geometry uses a fixed-pixel content rect
// (sized in RebuildTracks from the track/row count) so every connector is an axis-
// aligned bar with deterministic bounds at build time (no dependence on a layout
// pass). The content scrolls (owner: one scrollable canvas, Knight + Shared).
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

        // ── WO-896 PROGRESSION-LINE LATTICE (the live one) ───────────────────────
        // A TRACK ROW is: [ title block ][ node | line | node | line | node | node ]
        // Every number is a FIXED reference pixel (the WO-865 law), so a row can never be
        // squeezed under its own touch/line-box floors no matter the device aspect.

        /// <summary>Width of a track's left-hand title block (kind tag + name + note).</summary>
        public const float TrackTitleWpx = 200f;
        /// <summary>Centre-to-centre pitch of two nodes on a track: a node plate plus clear air.
        /// It is also the width of the name label under each node.</summary>
        public const float NodePitchPx = 200f;
        /// <summary>The FOCUS ("next") plate is deliberately LARGER than every other plate — a
        /// SIZE difference, readable with colour stripped out (the owner is red/green colourblind).</summary>
        public const float NodeNextPx = 132f;
        /// <summary>The name label under a node: TWO whole FontFloor line boxes (30 x 1.25 = 37.5),
        /// so a two-word talent ("Farsight Emplacements") wraps instead of ellipsizing.</summary>
        public const float NodeLabelBandPx = 78f;
        /// <summary>Air between a node plate and its name label.</summary>
        public const float NodeLabelGapPx = 6f;
        /// <summary>Air above the node band inside a row — also the headroom the oversized "next"
        /// plate and its outer ring grow into.</summary>
        public const float TrackTopPadPx = 10f;
        /// <summary>One track row: pad + node plate + gap + a two-line name band.</summary>
        public const float TrackRowPx = TrackTopPadPx + NodeSizePx + NodeLabelGapPx + NodeLabelBandPx;
        /// <summary>Clear air between two track rows.</summary>
        public const float TrackGapPx = 18f;
        /// <summary>Nodes per row before a track wraps to a continuation row. Four keeps the whole
        /// content rect inside the graph well's width at the reference device (no side-scrolling
        /// for a normal tier-1..tier-4 chain).</summary>
        public const int TrackWrapCount = 4;
        /// <summary>Inset around the whole scroll content, so the first row is WHOLE at rest.</summary>
        public const float ContentPadPx = 14f;
        /// <summary>Title block: the small-caps kind tag ("WAR PATH") line box.</summary>
        public const float TrackKindBandPx = 38f;
        /// <summary>Title block: the track name — two line boxes.</summary>
        public const float TrackNameBandPx = 76f;
        /// <summary>Title block: the qualifier note ("after Thunderbolt") line box.</summary>
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
        // "MORE BELOW" overflow cue, pinned to the WELL (not the scrolling content) and shown
        // only when the tracks are taller than the well. The well resolves to ~221 ref px at
        // 2340x1080 — about one track row — so without a cue a player can believe the tree ends
        // at the first path. Text, not an icon: it survives greyscale (colourblind law).
        private GameObject _scrollHint;

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

        // ── WO-896: build the PROGRESSION LINE (tracks of connected nodes) ───────
        // The dense authored-x/y GRID is retired (owner ruling: "this skill tree is hard to
        // read - simplify the tree, just have the skills connected by a line showing
        // progression"). Every vm.Tracks entry becomes a ROW: a title block on the left, then
        // up to TrackWrapCount node plates sitting ON a line that runs THROUGH their centres,
        // left = earlier, right = later. Rows stack top-down inside the SAME RectMask2D scroll
        // well, on a fixed-pixel content rect that is padded on all sides — so the first row is
        // WHOLE at rest (no clipped top row, the WO-896 §0 defect) and a long tree just scrolls.

        private void RebuildTracks()
        {
            if (_graphContent == null || _vm == null) { ClearContent(); return; }
            // Render() re-runs on EVERY tap (selection is VM state), so the line is rebuilt
            // constantly. Keep where the player had scrolled to — snapping back to the top on
            // each tap would make the lower tracks unusable now that tracks stack vertically.
            Vector2 keptScroll = _graphContent.anchoredPosition;
            ClearContent();

            var tracks = _vm.Tracks;
            int trackCount = tracks != null ? tracks.Count : 0;
            float contentW = ContentPadPx * 2f + TrackTitleWpx + TrackWrapCount * NodePitchPx;
            // The row height can never fall under the section-clearance floor (a node plate plus
            // a label band plus air) — the same floor SkillsPanelLayoutRegression pins.
            float rowH = Mathf.Max(TrackRowPx, SectionClearPx);

            float y = ContentPadPx;
            int rowsBuilt = 0;
            for (int t = 0; t < trackCount; t++)
            {
                var track = tracks[t];
                if (track == null || track.Nodes == null || track.Nodes.Count == 0) continue;
                int seats = track.Nodes.Count;
                int rowsInTrack = (seats + TrackWrapCount - 1) / TrackWrapCount;
                for (int r = 0; r < rowsInTrack; r++)
                {
                    var captured = track;
                    int rowIndex = r;
                    int from = r * TrackWrapCount;
                    int count = Mathf.Min(TrackWrapCount, seats - from);
                    float rowTop = y;
                    // One bad node can never blank the whole tree (§12 / Guard law).
                    Guard.Try("SkillTree", "build track row '" + captured.Title + "' #" + rowIndex,
                        () => BuildTrackRow(captured, rowIndex, from, count, rowTop, contentW, rowH));
                    y += rowH + TrackGapPx;
                    rowsBuilt++;
                }
            }

            if (rowsBuilt == 0)
            {
                var empty = ElarionUiKit.Label(_graphContent, "No talents to show yet.", 0f, 1f,
                    ElarionUi.ParchmentDim, ElarionUi.FontBody,
                    TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                ElarionUiKit.FitSingleLine(empty);
                FlowTrace.Warn("SkillTree", "progression line drew ZERO rows - the graph well is empty");
            }

            float contentH = Mathf.Max(rowH + ContentPadPx * 2f, y - TrackGapPx + ContentPadPx);
            _graphContent.sizeDelta = new Vector2(contentW, contentH);
            // Rest position = flush top-LEFT (the first row WHOLE, never mid-plate); a scrolled
            // player keeps their place, clamped so the content can never sit off its own well.
            var well = _graphContent.parent as RectTransform;
            float wellW = well != null ? well.rect.width : 0f;
            float wellH = well != null ? well.rect.height : 0f;
            float maxDown = wellH > 1f ? Mathf.Max(0f, contentH - wellH) : 0f;
            float maxRight = wellW > 1f ? Mathf.Max(0f, contentW - wellW) : 0f;
            _graphContent.anchoredPosition = new Vector2(
                Mathf.Clamp(keptScroll.x, -maxRight, 0f),
                Mathf.Clamp(keptScroll.y, 0f, maxDown));

            UpdateScrollHint();   // (also re-evaluated live on every scroll — see BuildScrollGraph)

            string sig = trackCount + "/" + rowsBuilt + "/" + contentH.ToString("F0");
            if (sig != _lastLayoutSig)
            {
                _lastLayoutSig = sig;
                FlowTrace.Step("SkillTree", "progression line drawn: " + trackCount + " track(s), " +
                                            rowsBuilt + " row(s), content " + contentW.ToString("F0") +
                                            "x" + contentH.ToString("F0") + " px");
            }
        }

        // One track row: the title block, then the connectors (behind), then the plates + labels.
        private void BuildTrackRow(SkillTrackVM track, int rowIndex, int from, int count,
                                   float rowTop, float contentW, float rowH)
        {
            var row = PxRect(_graphContent, "Track" + rowIndex + "_" + track.Title, 0f, rowTop, contentW, rowH);
            BuildTrackTitle(row, track, rowIndex);

            var seats = track.Nodes;
            float cy = -(TrackTopPadPx + NodeSizePx * 0.5f);   // plate centre, px DOWN from the row top

            // Connectors FIRST so the opaque plates draw over their ends.
            for (int i = 1; i < count; i++)
                BuildConnector(row, seats[from + i - 1], seats[from + i], NodeCenterX(i - 1), NodeCenterX(i), cy);

            for (int i = 0; i < count; i++)
            {
                var seat = seats[from + i];
                BuildTrackNode(row, seat, NodeCenterX(i), cy);
                BuildNodeLabel(row, seat, NodeCenterX(i), cy);
            }
        }

        /// <summary>Centre X (content px) of the i-th node seat in a row.</summary>
        private static float NodeCenterX(int i)
        {
            return ContentPadPx + TrackTitleWpx + i * NodePitchPx + NodePitchPx * 0.5f;
        }

        // The left-hand title block: kind tag over the track name over an optional qualifier
        // ("after Thunderbolt" / "no order - pick any" / "prerequisite is hidden"). Continuation
        // rows carry "(cont.)" instead, so a wrapped pool never reads as a second track.
        private static void BuildTrackTitle(Transform row, SkillTrackVM track, int rowIndex)
        {
            var host = PxRect(row, "TrackTitle", ContentPadPx, 0f, TrackTitleWpx, TrackRowPx);
            if (rowIndex > 0)
            {
                var cont = ElarionUiKit.Label(host, "(cont.)", 0f, 1f, ElarionUi.ParchmentDim,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.MidlineRight, 0.04f, 0.96f);
                PinBandFromTop(cont.rectTransform, TrackTopPadPx + NodeSizePx * 0.5f - TrackKindBandPx * 0.5f,
                               TrackKindBandPx);
                ElarionUiKit.FitSingleLine(cont);
                return;
            }

            var kind = ElarionUiKit.Label(host, track.Kind, 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.MidlineLeft, 0.04f, 0.98f, bold: true);
            PinBandFromTop(kind.rectTransform, TrackTopPadPx, TrackKindBandPx);
            ElarionUiKit.FitSingleLine(kind);

            var name = ElarionUiKit.Label(host, track.Title, 0f, 1f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.TopLeft, 0.04f, 0.98f, bold: true);
            PinBandFromTop(name.rectTransform, TrackTopPadPx + TrackKindBandPx + 2f, TrackNameBandPx);
            ElarionUiKit.FitBlock(name);

            if (!string.IsNullOrEmpty(track.Note))
            {
                var note = ElarionUiKit.Label(host, track.Note, 0f, 1f, ElarionUi.ParchmentDim,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.TopLeft, 0.04f, 0.98f);
                PinBandFromTop(note.rectTransform, TrackTopPadPx + TrackKindBandPx + TrackNameBandPx + 4f,
                               TrackNoteBandPx);
                ElarionUiKit.FitSingleLine(note);
            }
        }

        // ── The connecting PROGRESSION LINE (weight + dash pattern carry the meaning) ──
        // COLOURBLIND LAW: the four connector readings differ by THICKNESS and DASH PATTERN,
        // never by hue — they are all the same gilt ink at different weights/alphas.
        //   earned path (both ends owned/planned) : SOLID, 8 px
        //   live frontier (parent owned, child not): SOLID, 4 px
        //   not earned yet                         : DASHED, 3 px
        //   unordered shelf (no prerequisite link) : DOTTED, 2 px
        private static void BuildConnector(Transform row, SkillTrackNodeVM prev, SkillTrackNodeVM seat,
                                           float xPrev, float xNext, float cy)
        {
            float x0 = xPrev + NodeSizePx * 0.5f;
            float x1 = xNext - NodeSizePx * 0.5f;
            if (x1 <= x0) return;

            bool prevActive = prev.State == SkillNodeState.Owned || prev.State == SkillNodeState.Planned;
            bool thisActive = seat.State == SkillNodeState.Owned || seat.State == SkillNodeState.Planned;

            // No prerequisite link = an unordered SHELF: a fine dotted rail and NO arrowhead,
            // so the line can never imply a pick order the data does not have.
            if (!seat.LinksToPrev) { BuildDashRun(row, x0, x1, cy, 2f, 6f, 12f, 0.28f); return; }

            if (prevActive && thisActive) BuildBar(row, x0, x1, cy, 8f, 0.90f);
            else if (prevActive) BuildBar(row, x0, x1, cy, 4f, 0.78f);
            else BuildDashRun(row, x0, x1, cy, 3f, 16f, 12f, 0.42f);
            // Every REAL progression link ends in an arrowhead: the unlock DIRECTION is a
            // shape on the line, not an inference from left-to-right reading order.
            BuildArrowHead(row, x1, cy, prevActive ? 0.90f : 0.55f);
        }

        private static void BuildBar(Transform row, float x0, float x1, float cy, float thickness, float alpha)
        {
            var go = new GameObject("Line", typeof(Image));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(x1 - x0, thickness);
            rt.anchoredPosition = new Vector2((x0 + x1) * 0.5f, cy);
            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, alpha);
            img.raycastTarget = false;
        }

        // A ">" head built from two rotated bars, tip on the receiving node's edge.
        private static void BuildArrowHead(Transform row, float tipX, float cy, float alpha)
        {
            ArrowBar(row, tipX - 5f, cy + 3.5f, -35f, alpha);
            ArrowBar(row, tipX - 5f, cy - 3.5f, 35f, alpha);
        }

        private static void ArrowBar(Transform row, float cx, float cy, float angle, float alpha)
        {
            var go = new GameObject("Arrow", typeof(Image));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(13f, 3.5f);
            rt.anchoredPosition = new Vector2(cx, cy);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, alpha);
            img.raycastTarget = false;
        }

        private static void BuildDashRun(Transform row, float x0, float x1, float cy,
                                         float thickness, float dashPx, float gapPx, float alpha)
        {
            float step = Mathf.Max(4f, dashPx + gapPx);
            for (float x = x0; x < x1; x += step)
                BuildBar(row, x, Mathf.Min(x + dashPx, x1), cy, thickness, alpha);
        }

        /// <summary>Show the "MORE BELOW" cue only while there IS more track under the fold and
        /// the player has not already scrolled to it. Cheap enough to run per scroll event.</summary>
        private void UpdateScrollHint()
        {
            if (_scrollHint == null || _graphContent == null) return;
            var well = _graphContent.parent as RectTransform;
            float wellH = well != null ? well.rect.height : 0f;
            float maxDown = wellH > 1f ? Mathf.Max(0f, _graphContent.rect.height - wellH) : 0f;
            _scrollHint.SetActive(maxDown > 8f && _graphContent.anchoredPosition.y < maxDown - 8f);
        }

        /// <summary>A fixed-pixel child rect seated from its parent's TOP-LEFT corner
        /// (<paramref name="y"/> counts DOWN). The whole line layout is built from these.</summary>
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

        // ── ONE NODE ON THE LINE ─────────────────────────────────────────────────
        // COLOURBLIND LAW (the single most important constraint in WO-896): the six states
        // are separable with ALL COLOUR STRIPPED OUT. Nothing below is carried by hue.
        //
        //  state      plate FILL        border   size   badge SHAPE          label prefix
        //  ---------  ----------------  -------  -----  -------------------  ------------
        //  Owned      SOLID (light)     2 px     112    check tick (2 bars)  -
        //  Planned    solid + ring      3 px     112    "-n" text pip        "PLANNED: "
        //  Next       hollow (dark)     6 px +   132    cost number pip      "NEXT: "
        //                               outer ring      (biggest plate)
        //  Available  hollow (dark)     3 px     112    cost number pip      -
        //  Inert      hollow + SLASH    3 px     112    "!" pip              "[!] "
        //  Locked     hollow (dimmest)  1.5 px   112    PADLOCK glyph        -
        //
        // Click = select (+ stage/unstage for an actionable node) — unchanged plan->CONFIRM flow.

        private void BuildTrackNode(Transform row, SkillTrackNodeVM seat, float cx, float cy)
        {
            var node = seat.Node;
            if (string.IsNullOrEmpty(node.Id)) return;
            var state = seat.State;

            float size = state == SkillNodeState.Next ? NodeNextPx : NodeSizePx;

            var go = new GameObject("Node_" + node.Id, typeof(Image), typeof(Button));
            go.transform.SetParent(row, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, cy);
            // A node is a BUTTON — it always carries the kit touch floor, even at 112.
            rt.sizeDelta = new Vector2(size, size);

            // Root image = the gilt LINE border; the dark fill is a child inset by the line
            // width, so the border reads as a crisp ring whose THICKNESS encodes the state.
            var img = go.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img);
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, BorderAlphaFor(state));

            float border = BorderWidthFor(state);
            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(border, border);
            fillRt.offsetMax = new Vector2(-border, -border);
            var fillImg = fillGo.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(fillImg);
            fillImg.color = PlateFillFor(state);
            fillImg.raycastTarget = false;

            // Capstone — a THICKER gilt rim behind the plate (procedural, no art), so the
            // tier-capper still reads special on the line.
            if (node.IsCapstone) BuildOuterRing(go.transform, 0.05f,
                new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                          state == SkillNodeState.Locked ? 0.40f : 0.85f));

            // The FOCUS node wears a second, wider ring — with the +20 px plate it is the one
            // node on the row you can pick out at arm's length with the colour gone.
            if (state == SkillNodeState.Next) BuildOuterRing(go.transform, 0.055f,
                new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.95f));

            // Planned ring (unchanged plan->CONFIRM affordance).
            if (state == SkillNodeState.Planned) BuildOuterRing(go.transform, 0.05f,
                new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.9f));

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            btn.interactable = true;
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Select(id); });

            // Icon — quiet in the plate centre. On an OWNED (light-filled) plate the glyph
            // flips to near-black ink: the contrast reversal is itself a colour-free tell.
            bool dim = state == SkillNodeState.Locked || state == SkillNodeState.Inert;
            var sprite = LoadIcon(node.IconPath);
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
                iImg.color = state == SkillNodeState.Owned ? new Color(0.10f, 0.09f, 0.08f, 0.95f)
                           : dim ? new Color(0.82f, 0.80f, 0.77f, 0.35f)
                           : new Color(0.86f, 0.84f, 0.81f, 0.95f);
            }
            else
            {
                // No icon art yet — a two-letter monogram keeps the node identifiable.
                string mono = string.IsNullOrEmpty(node.Name)
                    ? "?" : node.Name.Substring(0, Mathf.Min(2, node.Name.Length));
                var monoLbl = ElarionUiKit.Label(go.transform, mono, 0.24f, 0.76f,
                    state == SkillNodeState.Owned ? new Color(0.10f, 0.09f, 0.08f, 1f)
                        : dim ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                ElarionUiKit.FitSingleLine(monoLbl);
            }

            // WO-910 — an INERT node (reachable, but nothing in the game reads its effect yet)
            // wears a SLASH across the plate. It is the loudest possible "this grants nothing",
            // it survives greyscale, and it is presentation only: the node stays selectable and
            // purchasable, so the owner's pending hide-or-wire ruling is not pre-empted here.
            if (state == SkillNodeState.Inert) BuildNodeSlash(go.transform, size);

            // ONE badge per state (shape/stamp/pip — never hue alone).
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

        // The short name UNDER the node (WO-896 §1). The state prefix is ASCII text — the
        // last, hue-free line of defence for a player who cannot separate the plate colours.
        private static void BuildNodeLabel(Transform row, SkillTrackNodeVM seat, float cx, float cy)
        {
            string prefix;
            switch (seat.State)
            {
                case SkillNodeState.Next: prefix = "NEXT: "; break;
                case SkillNodeState.Planned: prefix = "PLANNED: "; break;
                case SkillNodeState.Inert: prefix = "[!] "; break;
                default: prefix = ""; break;
            }
            string name = string.IsNullOrEmpty(seat.Node.Name) ? seat.Node.Id : seat.Node.Name;

            float top = -cy + NodeSizePx * 0.5f + NodeLabelGapPx;   // px DOWN from the row top
            var host = PxRect(row, "NodeLabel", cx - NodePitchPx * 0.5f, top, NodePitchPx, NodeLabelBandPx);
            var lbl = ElarionUiKit.Label(host, prefix + name, 0f, 1f,
                seat.State == SkillNodeState.Locked || seat.State == SkillNodeState.Inert
                    ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                (int)ElarionUiKit.FontFloor, TMPro.TextAlignmentOptions.Top, 0.04f, 0.96f,
                bold: seat.State == SkillNodeState.Next || seat.State == SkillNodeState.Owned);
            ElarionUiKit.FitBlock(lbl);   // wraps to two whole line boxes, never spills
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
            // 2026-08-04 capture. Flush top-left + ContentPadPx of padding means the rest
            // position always shows the FIRST TRACK ROW WHOLE — plate and name label — which is
            // the WO-896 "no clipped top row" criterion; later rows are reached by scrolling.
            // RebuildTracks sizes the rect in exact pixels.
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0f, 1f);
            _graphContent.pivot = new Vector2(0f, 1f);
            _graphContent.sizeDelta = new Vector2(
                ContentPadPx * 2f + TrackTitleWpx + TrackWrapCount * NodePitchPx,
                TrackRowPx + ContentPadPx * 2f);
            _graphContent.anchoredPosition = Vector2.zero;

            // WO-896 overflow cue — a fixed corner tag on the WELL (never inside the scrolling
            // content, or it would scroll away with the thing it is describing).
            var hintGo = new GameObject("ScrollHint", typeof(Image));
            hintGo.transform.SetParent(viewportGo.transform, false);
            var hr = (RectTransform)hintGo.transform;
            hr.anchorMin = hr.anchorMax = Vector2.zero;
            hr.pivot = Vector2.zero;
            hr.anchoredPosition = new Vector2(6f, 6f);
            hr.sizeDelta = new Vector2(200f, 40f);
            var hImg = hintGo.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(hImg);
            hImg.color = new Color(0.05f, 0.045f, 0.06f, 0.88f);
            hImg.raycastTarget = false;   // never eats a drag-scroll
            var hLbl = ElarionUiKit.Label(hintGo.transform, "MORE BELOW", 0f, 1f, ElarionUi.Gilt,
                (int)ElarionUiKit.FontFloor, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(hLbl);
            hintGo.SetActive(false);
            _scrollHint = hintGo;

            var scroll = areaGo.GetComponent<ScrollRect>();
            scroll.content = _graphContent;
            scroll.viewport = vr;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 28f;
            scroll.onValueChanged.AddListener(_ => UpdateScrollHint());
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
            _scrollHint = null;
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
