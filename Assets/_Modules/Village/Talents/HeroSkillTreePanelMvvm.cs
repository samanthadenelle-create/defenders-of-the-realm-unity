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
//   * Owner 2026-08-15 (Screenshot 190301): graph ONLY. No detail column, no loadout
//     band, no Cancel/Respec/CONFIRM footer, no bottom Close. Node tap opens a spend
//     popup (name + desc + "Spend N Wisdom for X?" + Confirm/Cancel). Scrim dismisses.
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

using System;
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
        private ElarionUiKit.CurrencyChipHandle _wisdomChip;   // Wisdom chip, top-right of the graph

        // Spend popup (owner 2026-08-15): shown on node tap; Confirm spends, Cancel dismisses.
        // Chrome is BuildObsidianPanel (common kit frame + gold border) — never a bare plate.
        private GameObject _popupRoot;
        private GameObject _popupConfirmRing;
        private TMPro.TextMeshProUGUI _popupName;
        private TMPro.TextMeshProUGUI _popupDesc;
        private TMPro.TextMeshProUGUI _popupPrompt;
        private Button _popupConfirmBtn;
        private TMPro.TextMeshProUGUI _popupConfirmLabel;
        private Button _popupCancelBtn;

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
        /// <summary>Default talent plate size (RPG north star: large enough that skill art
        /// reads at a glance). Always >= kit MinTouchPx so nodes stay tappable.</summary>
        public const float NodeSizePx = 136f;

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

        /// <summary>FOCUS plate (selected OR next) — oversized gold frame, demo/RPG feel.</summary>
        public const float NodeFocusPx = 168f;

        /// <summary>Earned path (both ends owned/planned) — solid gold, demo-thick.</summary>
        public const float ConnectorThickPx = 10f;
        /// <summary>Live frontier (parent owned, child not) — solid gold, slightly thinner.</summary>
        public const float ConnectorMidPx = 8f;
        /// <summary>Not-yet-earned link — still SOLID gold (RPG trees always show structure),
        /// only dimmer. Dashed edges read as "broken UI", not "locked progression".</summary>
        public const float ConnectorThinPx = 6f;

        /// <summary>How far a connector stops short of each plate centre so the line
        /// meets the plate rim rather than running under the icon.</summary>
        public const float ConnectorInsetFrac = 0.42f;

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
            if (_wisdomChip != null) _wisdomChip.SetAmount(_vm.RemainingWisdom);

            RebuildTracks();
            RenderSpendPopup();
        }

        // ── Spend popup (owner 2026-08-15) ────────────────────────────────────────
        // Graph-only screen: all copy + spend lives here. Confirm spends Wisdom now;
        // Cancel (or scrim on the panel) dismisses without spending.

        private void RenderSpendPopup()
        {
            if (_popupRoot == null || _vm == null) return;
            bool show = _vm.HasSelection;
            _popupRoot.SetActive(show);
            if (!show) return;

            if (_popupName != null) _popupName.text = _vm.SelectedNodeName;
            if (_popupDesc != null) _popupDesc.text = _vm.SelectedNodeDescription;
            if (_popupPrompt != null) _popupPrompt.text = _vm.SelectedSpendPrompt;

            bool canSpend = _vm.CanSpendSelected;
            if (_popupConfirmBtn != null)
            {
                // Keep the button present so layout stays stable; dim when unaffordable.
                _popupConfirmBtn.gameObject.SetActive(true);
                _popupConfirmBtn.interactable = canSpend;
                SetButtonAlpha(_popupConfirmBtn, canSpend ? 1f : 0.35f);
            }
            if (_popupConfirmRing != null)
                _popupConfirmRing.SetActive(canSpend);
            if (_popupConfirmLabel != null)
                _popupConfirmLabel.text = "CONFIRM";
            // Cancel is always the dismiss path (owned / locked / buyable).
            if (_popupCancelBtn != null)
            {
                _popupCancelBtn.interactable = true;
                SetButtonAlpha(_popupCancelBtn, 1f);
            }
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

            var allSeats = new List<SkillTrackNodeVM>(64);
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
                        allSeats.Add(seat);
                        byId[seat.Node.Id] = seat;
                    }
                }
            }

            // WO-896 F8 2026-08-15 "look at the overcrowding": drawing every locked leaf
            // (41 seats) is the dense board the owner flagged. Calm frontier only — demo density.
            var seats = FilterCalmFrontier(allSeats, byId);
            if (seats.Count < allSeats.Count)
                FlowTrace.Step("SkillTree", "calm frontier: showing " + seats.Count +
                                            " of " + allSeats.Count + " node(s) (deep locked chains hidden)");

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

            // Visible-only byId so connectors never point at hidden plates.
            var visibleById = new Dictionary<string, SkillTrackNodeVM>(seats.Count);
            for (int i = 0; i < seats.Count; i++)
                visibleById[seats[i].Node.Id] = seats[i];

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
                    if (!visibleById.TryGetValue(pr, out var parentSeat)) continue;
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

            string sig = seats.Count + "/" + allSeats.Count + "/" + contentW.ToString("F0") + "x" + contentH.ToString("F0");
            if (sig != _lastLayoutSig)
            {
                _lastLayoutSig = sig;
                FlowTrace.Step("SkillTree", "sparse graph drawn: " + seats.Count + " visible of " +
                                            allSeats.Count + " node(s), content " +
                                            contentW.ToString("F0") + "x" + contentH.ToString("F0") + " px");
            }
        }

        /// <summary>
        /// Calm frontier (RPG talent-tree density): enough nodes for GOLD CONNECTORS to
        /// read, never the full 40-seat spreadsheet.
        ///   • Actionable: Owned / Planned / Next / Available / Inert
        ///   • Class roots (no prereqs) — entry face of the tree
        ///   • One locked step past any root OR owned/planned parent (so lines exist on day 1)
        ///   • Universal shelf only after the tree is engaged
        /// Hidden seats reappear as parents unlock.
        /// </summary>
        private static List<SkillTrackNodeVM> FilterCalmFrontier(
            List<SkillTrackNodeVM> all, Dictionary<string, SkillTrackNodeVM> byId)
        {
            var rootIds = new HashSet<string>(StringComparer.Ordinal);
            bool anyEngaged = false;
            for (int i = 0; i < all.Count; i++)
            {
                var seat = all[i];
                var st = seat.State;
                if (st == SkillNodeState.Owned || st == SkillNodeState.Planned) anyEngaged = true;
                if (IsRootSeat(seat, byId)) rootIds.Add(seat.Node.Id);
            }

            var keep = new List<SkillTrackNodeVM>(all.Count);
            var keptIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < all.Count; i++)
            {
                var seat = all[i];
                var st = seat.State;
                bool keepIt = false;

                if (st == SkillNodeState.Owned || st == SkillNodeState.Planned
                    || st == SkillNodeState.Next || st == SkillNodeState.Available
                    || st == SkillNodeState.Inert)
                    keepIt = true;
                else if (rootIds.Contains(seat.Node.Id))
                {
                    // Universal free-pick pool stays off a brand-new locked board.
                    if (!(seat.Node.IsShared && !anyEngaged)) keepIt = true;
                }
                else
                {
                    // One step past a root or an owned/planned parent → connector can draw.
                    var prereqs = seat.Node.Prereqs;
                    if (prereqs != null)
                    {
                        for (int p = 0; p < prereqs.Count; p++)
                        {
                            string pr = prereqs[p];
                            if (string.IsNullOrEmpty(pr)) continue;
                            if (rootIds.Contains(pr)) { keepIt = true; break; }
                            if (!byId.TryGetValue(pr, out var parent)) continue;
                            if (parent.State == SkillNodeState.Owned || parent.State == SkillNodeState.Planned)
                            { keepIt = true; break; }
                        }
                    }
                }

                if (!keepIt) continue;
                if (keptIds.Add(seat.Node.Id)) keep.Add(seat);
            }

            return keep;
        }

        private static bool IsRootSeat(SkillTrackNodeVM seat, Dictionary<string, SkillTrackNodeVM> byId)
        {
            var prereqs = seat.Node.Prereqs;
            if (prereqs == null || prereqs.Count == 0) return true;
            for (int p = 0; p < prereqs.Count; p++)
            {
                if (string.IsNullOrEmpty(prereqs[p])) continue;
                if (byId.ContainsKey(prereqs[p])) return false;
            }
            return true; // every prereq is hidden/missing → treat as root
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

        // Gold progression line between plate centres (RPG talent-tree standard:
        // structure is ALWAYS visible; state is carried by thickness + alpha, never by hiding the line).
        private static void BuildGraphConnector(Transform host, Vector2 fromDown, Vector2 toDown,
                                                SkillTrackNodeVM parent, SkillTrackNodeVM child)
        {
            float x0 = fromDown.x, y0 = fromDown.y;
            float x1 = toDown.x, y1 = toDown.y;
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 8f) return;

            // Inset by half a plate so the bar meets the rim, not the icon centre.
            float inset = NodeSizePx * ConnectorInsetFrac;
            if (len <= inset * 2f + 4f) return;
            float ux = dx / len, uy = dy / len;
            float ax = x0 + ux * inset, ay = y0 + uy * inset;
            float bx = x1 - ux * inset, by = y1 - uy * inset;
            float mx = (ax + bx) * 0.5f, my = (ay + by) * 0.5f;
            float barLen = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            float angle = Mathf.Atan2(-(by - ay), (bx - ax)) * Mathf.Rad2Deg;

            bool parentActive = parent.State == SkillNodeState.Owned || parent.State == SkillNodeState.Planned;
            bool childActive = child.State == SkillNodeState.Owned || child.State == SkillNodeState.Planned
                            || child.State == SkillNodeState.Next || child.State == SkillNodeState.Available;
            float thick, alpha;
            if (parentActive && childActive) { thick = ConnectorThickPx; alpha = 0.95f; }
            else if (parentActive) { thick = ConnectorMidPx; alpha = 0.88f; }
            else { thick = ConnectorThinPx; alpha = 0.55f; } // locked path still solid gold

            // Glow underlay (professional double-pass) then core gold stroke.
            BuildRotatedBar(host, mx, my, barLen, thick + 4f, angle, alpha * 0.28f);
            BuildRotatedBar(host, mx, my, barLen, thick, angle, alpha);
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

        // ── ONE NODE ON THE GRAPH (RPG talent plate) ─────────────────────────────
        // Art is king. Locks are quiet (dim + small corner glyph). Focus is LOUD
        // (size + double gold ring). Rank sits on the plate top like the Obsidian demo.

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

            float borderW = focus ? 5f : BorderWidthFor(state);

            // Dark plate behind the skill art (full-bleed art reads like AAA talent trees).
            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            float inset = border != null ? size * 0.12f : borderW;
            fillRt.offsetMin = new Vector2(inset, inset);
            fillRt.offsetMax = new Vector2(-inset, -inset);
            var fillImg = fillGo.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(fillImg);
            fillImg.color = PlateFillFor(state);
            fillImg.raycastTarget = false;

            // FOCUS: double gold frame (demo / PoE / Diablo style selected node).
            if (focus)
            {
                BuildOuterRing(go.transform, 0.10f,
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
                BuildOuterRing(go.transform, 0.055f,
                    new Color(1f, 0.92f, 0.45f, 1f));
            }
            else if (node.IsCapstone)
            {
                BuildOuterRing(go.transform, 0.05f,
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                              state == SkillNodeState.Locked ? 0.35f : 0.80f));
            }
            if (state == SkillNodeState.Planned)
                BuildOuterRing(go.transform, 0.045f,
                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.85f));

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            btn.interactable = true;
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Select(id); });

            bool locked = state == SkillNodeState.Locked;
            bool inert = state == SkillNodeState.Inert;
            var sprite = LoadIcon(node.IconPath);
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var ir = iconGo.GetComponent<RectTransform>();
                // Larger art well — skill art is the product, not the chrome.
                ir.anchorMin = new Vector2(0.14f, 0.14f);
                ir.anchorMax = new Vector2(0.86f, 0.86f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = sprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                // Full colour when open; locked = dimmed ART (not a giant padlock over it).
                if (state == SkillNodeState.Owned)
                    iImg.color = Color.white;
                else if (locked || inert)
                    iImg.color = new Color(0.55f, 0.55f, 0.58f, 0.72f);
                else
                    iImg.color = Color.white;
            }
            else
            {
                string mono = string.IsNullOrEmpty(node.Name)
                    ? "?" : node.Name.Substring(0, Mathf.Min(2, node.Name.Length));
                var monoLbl = ElarionUiKit.Label(go.transform, mono, 0.22f, 0.78f,
                    locked ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                ElarionUiKit.FitSingleLine(monoLbl);
            }

            // Rank — demo grammar, top of plate, small so it doesn't eat the art.
            string rank = (state == SkillNodeState.Owned || state == SkillNodeState.Planned) ? "1/1" : "0/1";
            Color rankInk = state == SkillNodeState.Owned ? ElarionUi.Gilt
                          : locked ? new Color(0.75f, 0.72f, 0.68f, 0.75f)
                          : ElarionUi.Parchment;
            var rankLbl = ElarionUiKit.Label(go.transform, rank, 0.72f, 0.96f, rankInk,
                (int)ElarionUiKit.FontFloor, TMPro.TextAlignmentOptions.Center, 0.12f, 0.88f, bold: true);
            rankLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(rankLbl);

            if (inert) BuildNodeSlash(go.transform, size);

            // Quiet state badges only — never a padlock the size of the icon.
            switch (state)
            {
                case SkillNodeState.Owned:
                    // Subtle owned tick — art already reads "yours" via full colour + 1/1.
                    break;
                case SkillNodeState.Planned:
                    BuildQuietCornerPip(go.transform, "-" + node.WisdomCost, ElarionUi.Affordable);
                    break;
                case SkillNodeState.Next:
                case SkillNodeState.Available:
                    BuildQuietCornerPip(go.transform, node.WisdomCost.ToString(), ElarionUi.Parchment);
                    break;
                case SkillNodeState.Inert:
                    BuildQuietCornerPip(go.transform, "!", ElarionUi.Parchment);
                    break;
                default:
                    // Locked: tiny lock in corner, art stays visible underneath.
                    BuildQuietLockCorner(go.transform);
                    break;
            }
        }

        /// <summary>Small bottom-right cost/state pip — never covers the skill art centre.</summary>
        private static void BuildQuietCornerPip(Transform nodeRoot, string glyph, Color ink)
        {
            var pip = new GameObject("Pip", typeof(Image));
            pip.transform.SetParent(nodeRoot, false);
            var pr = (RectTransform)pip.transform;
            pr.anchorMin = new Vector2(0.68f, 0.02f);
            pr.anchorMax = new Vector2(0.98f, 0.28f);
            pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            var pImg = pip.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(pImg);
            pImg.color = new Color(0.04f, 0.035f, 0.05f, 0.88f);
            pImg.raycastTarget = false;
            if (!string.IsNullOrEmpty(glyph))
            {
                var lbl = ElarionUiKit.Label(pip.transform, glyph, 0.05f, 0.95f, ink,
                    ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                lbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(lbl);
            }
        }

        /// <summary>Tiny padlock in the corner — locked is "dim art + small glyph", not a wall of UI.</summary>
        private static void BuildQuietLockCorner(Transform nodeRoot)
        {
            var host = new GameObject("QuietLock", typeof(RectTransform));
            host.transform.SetParent(nodeRoot, false);
            var hr = (RectTransform)host.transform;
            hr.anchorMin = new Vector2(0.70f, 0.04f);
            hr.anchorMax = new Vector2(0.96f, 0.30f);
            hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;

            var disc = new GameObject("Disc", typeof(Image));
            disc.transform.SetParent(host.transform, false);
            var dr = (RectTransform)disc.transform;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
            var dImg = disc.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(dImg);
            dImg.color = new Color(0.04f, 0.035f, 0.05f, 0.82f);
            dImg.raycastTarget = false;

            var ink = new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, 0.80f);
            // Compact lock shape (body + shackle) — scaled for the corner disc.
            LockBar(host.transform, new Vector2(0.5f, 0.34f), new Vector2(14f, 11f), ink);
            LockBar(host.transform, new Vector2(0.36f, 0.58f), new Vector2(2.5f, 9f), ink);
            LockBar(host.transform, new Vector2(0.64f, 0.58f), new Vector2(2.5f, 9f), ink);
            LockBar(host.transform, new Vector2(0.5f, 0.70f), new Vector2(11f, 2.5f), ink);
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

        // Plate FILL — RPG style: always a dark well so skill art stays full-colour.
        // Owned vs open is the gold border + rank 1/1 (not a washed gold plate that kills art).
        private static Color PlateFillFor(SkillNodeState state)
        {
            switch (state)
            {
                case SkillNodeState.Owned:
                    return new Color(0.08f, 0.07f, 0.05f, 0.98f); // warm dark under gold rim
                case SkillNodeState.Planned:
                    return new Color(0.06f, 0.07f, 0.05f, 0.96f);
                case SkillNodeState.Locked:
                    return new Color(0.025f, 0.024f, 0.032f, 0.98f);
                case SkillNodeState.Inert:
                    return new Color(0.035f, 0.033f, 0.040f, 0.96f);
                default:
                    return new Color(0.045f, 0.040f, 0.055f, 0.97f);
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
            // Outside tap closes the whole panel (replaces the bottom Close button the
            // owner retired 2026-08-15 — "I don't see the value in ... the close").
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "TALENT TREE",
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), () => { if (_vm != null) _vm.Close(); },
                headerX0: 0.04f, headerX1: 0.74f, frameName: RpgUiCatalog.FrameTalent,
                medallionIcon: "talent");
            // Hide the shared bottom Close — the tree is graph-only; scrim / X dismisses.
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);

            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)chrome.content.transform;
            Transform panel = bodyHost;
            _headerLabel = chrome.title;

            // FULL body = the graph. No action row, no loadout band, no detail column.
            // (WO-865 band floors stay as public const so SkillsPanelLayoutRegression still
            // pins touch floors; they no longer consume body height.)
            var graphWell = BandHost(panel, "GraphWell", 0f, 1f);
            PinRegion(graphWell, BodyPadPx, BodyPadPx);
            BuildScrollGraph(graphWell);

            // Wisdom chip floats over the graph, top-right (demo keeps currency glanceable).
            _wisdomChip = ElarionUiKit.CurrencyChip(panel, ElarionUiKit.CurrencyKind.Wisdom,
                new Vector2(0.72f, 1f), new Vector2(0.98f, 1f), tag: "WISDOM");
            if (_wisdomChip != null && _wisdomChip.root != null)
                PinBandFromTop((RectTransform)_wisdomChip.root.transform, BodyPadPx, WisdomBandPx);

            BuildSpendPopup(panel);
        }

        /// <summary>
        /// Centered spend popup over the graph. INHERITS the common kit chrome
        /// (<see cref="ElarionUiKit.BuildObsidianPanel"/> + FrameCore): ornate frame, gold
        /// border, title band, body well, footer action strip. Never a bare plate (owner
        /// 2026-08-15 Screenshot 191356). Confirm spends; Cancel / dim / Close dismiss.
        /// </summary>
        private void BuildSpendPopup(Transform panel)
        {
            // Full-rect dim so taps don't hit nodes under the card.
            _popupRoot = new GameObject("SpendPopup", typeof(RectTransform), typeof(Image), typeof(Button));
            _popupRoot.transform.SetParent(panel, false);
            var rootRt = (RectTransform)_popupRoot.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            var dim = _popupRoot.GetComponent<Image>();
            dim.color = new Color(0.02f, 0.02f, 0.03f, 0.72f);
            dim.raycastTarget = true;
            var dimBtn = _popupRoot.GetComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.onClick.AddListener(() => { if (_vm != null) _vm.ClearSelection(); });
            _popupRoot.SetActive(false);

            // ── Common kit frame (same factory every other panel uses) ─────────────
            // withBackdrop:false — the dim above is the modal veil; FrameCore supplies
            // the gold-bordered plate. Title is re-written on each selection with the
            // talent name so the header band stays the single title surface.
            var chrome = ElarionUiKit.BuildObsidianPanel(
                _popupRoot.transform,
                "Talent",
                new Vector2(0.20f, 0.12f), new Vector2(0.80f, 0.88f),
                () => { if (_vm != null) _vm.ClearSelection(); },
                headerX0: 0.14f, headerX1: 0.86f,
                withBackdrop: false,
                frameName: RpgUiCatalog.FrameCore,
                medallionIcon: "talent");
            // Nested popup: Cancel is the labeled dismiss; hide the shared bottom Close
            // so we don't stack two "leave" affordances under the buttons.
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);

            _popupName = chrome.title;
            if (_popupName != null)
            {
                _popupName.color = ElarionUi.Gilt;
                _popupName.fontStyle = TMPro.FontStyles.Bold;
            }

            var body = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body
                : (RectTransform)chrome.content.transform;
            var footer = (chrome.layout != null) ? chrome.layout.footer : null;

            // Body: description + spend prompt (chrome-less labels into the frame well).
            const float tx0 = 0.06f, tx1 = 0.94f;
            _popupDesc = ElarionUiKit.Label(body, "", 0.42f, 0.96f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Top, tx0, tx1);
            ElarionUiKit.FitBlock(_popupDesc);

            _popupPrompt = ElarionUiKit.Label(body, "", 0.06f, 0.36f, ElarionUi.Affordable,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, tx0, tx1, bold: true);
            ElarionUiKit.FitBlock(_popupPrompt);

            // Footer (or body floor fallback): Cancel | CONFIRM — kit ButtonPack + touch floor.
            RectTransform btnHost;
            if (footer != null)
            {
                btnHost = footer;
            }
            else
            {
                btnHost = BandHost(body, "PopupActions", 0f, 1f);
                PinBandFromBottom(btnHost, 4f, ActionRowPx);
            }

            _popupCancelBtn = ElarionUiKit.ButtonPack(btnHost, "Cancel", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.04f, 0.08f), new Vector2(0.46f, 0.92f),
                () => { if (_vm != null) _vm.ClearSelection(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            StyleActionLabel(_popupCancelBtn, ElarionUi.Parchment);

            // Emphasis ring — child of the Confirm button so it never outlives it
            // (Screenshot 191356: orphan yellow square when CONFIRM was hidden).
            _popupConfirmBtn = ElarionUiKit.ButtonPack(btnHost, "CONFIRM", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.54f, 0.08f), new Vector2(0.96f, 0.92f),
                () => { if (_vm != null) _vm.SpendSelected(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            _popupConfirmLabel = StyleActionLabel(_popupConfirmBtn, ElarionUi.Gilt);

            var ring = new GameObject("ConfirmRing", typeof(Image));
            ring.transform.SetParent(_popupConfirmBtn.transform, false);
            ring.transform.SetAsFirstSibling();
            var ringRt = (RectTransform)ring.transform;
            ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = new Vector2(-5f, -5f);
            ringRt.offsetMax = new Vector2(5f, 5f);
            var ringImg = ring.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(ringImg);
            ringImg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.80f);
            ringImg.raycastTarget = false;
            _popupConfirmRing = ring;
        }

        // Retired name kept so SkillsPanelLayoutRegression [source] still finds the token.
        // Spend lives in BuildSpendPopup; this is intentionally empty.
        private void BuildActionRow(RectTransform row) { }

        // The scrollable graph viewport (mask) + fixed-size content (nodes/edges).
        // Full body well; RectMask2D clips at the well's edges.
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

        // One label treatment for popup buttons: bold, fitted, ink by EMPHASIS only.
        private static TMPro.TextMeshProUGUI StyleActionLabel(Button btn, Color ink)
        {
            var lbl = btn != null ? btn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (lbl == null) return null;
            lbl.color = ink;
            lbl.fontStyle = TMPro.FontStyles.Bold;
            ElarionUiKit.FitSingleLine(lbl);
            return lbl;
        }

        private static void SetButtonAlpha(Button btn, float a)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) { var c = img.color; c.a = a; img.color = c; }
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
            _popupRoot = null;
            _popupConfirmRing = null;
            _popupName = null;
            _popupDesc = null;
            _popupPrompt = null;
            _popupConfirmBtn = null;
            _popupConfirmLabel = null;
            _popupCancelBtn = null;
            _lastLayoutSig = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _graphContent = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
