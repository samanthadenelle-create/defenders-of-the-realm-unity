// =============================================================================
// RealmMapPanel (WO-826) — the full-screen parchment Realm Map: Elarion at the
// centre, five fog-shrouded region nodes laid out by realm-map.json mapPoint.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Code-built uGUI on the Obsidian master frame (BuildObsidianModal — the
// RumorBoardPanel reference recipe): FrameCore chrome + the ONE shared Close +
// tap-outside scrim, registered with PanelManager (modal arbiter) AND
// PanelRouter (PanelId.RealmMap) so the HUD Map button / DevPanel open it
// reflection-free from any assembly.
//
// Layout (landscape): the parchment MAP fills the left ~58% of the body well;
// the obsidian DETAIL pane sits right, always bound to the selection (home
// auto-selected on open, so it is never blank). Portrait: map top ~55%, detail
// below. Nodes are >= 48dp buttons at mapPoint percent-of-rect (x rightward,
// y DOWNWARD — the React layout convention the data was authored in).
//
// Node language (WO-826 table; state is ALSO text in the detail — never colour
// alone, colorblind law; ASCII only — the bundled font tofus non-Latin glyphs):
//   locked     -> dark fog disc + "?" (no title revealed on the map)
//   discovered -> parchment disc, gold trim, title inked below
//   cleared    -> gilt disc + "*" marker, title inked below
//   home       -> larger gold crest plate, "*", "Elarion" always
//   selected   -> gilt halo ring behind the disc (shape marker)
// Adjacency connector lines: SKIPPED on first ship (spec: skip if noisy).
//
// Strict MVVM: this View renders RealmMapVM projections and routes taps to
// vm.Select; it reads NO game state and touches NO Village gameplay objects.
// Travel is a DISABLED stub until WO-827 (the CTA slot is reserved).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Village.Hero
{
    public sealed class RealmMapPanel : MonoBehaviour
    {
        // Node sizing (reference px): regions comfortably above the 48dp floor;
        // home reads larger (it is the centre of the world).
        // PUBLIC (WO-941): the node footprint is a FIXED-PIXEL budget and the pitch between
        // two nodes is a FRACTION of the map plate, so the two only stay disjoint while the
        // plate is tall enough. RealmMapRegression pins that arithmetic against the authored
        // mapPoints -- it cannot be pinned without the numbers.
        public const float NodePx = 96f;
        public const float HomeNodePx = 120f;
        private const float SelectHaloPx = 16f;

        // =====================================================================
        //  WO-941 -- THE NODE TITLE BAND IS PART OF THE NODE'S FOOTPRINT
        // -----------------------------------------------------------------------------
        //  Oracle, BOTH landscape sizes (2340x1080 + 2670x1200):
        //    'Nodes/Node_starfall-reach/Disc' covers 'Nodes/Node_avalon/Label' ("ELARION")
        //    by 96 x 18.3 ref px.
        //  ARITHMETIC THAT REPRODUCES IT (measured off the capture log, not inferred):
        //  the map plate resolved to 922 x 376 ref px because FrameCore's body zone floor is
        //  RAISED by the kit's footer + shared-Close reservation (0.075 -> 0.363 of the panel),
        //  and RealmMap uses NO footer -- so ~150 ref px of panel height was reserved for a
        //  band nothing draws in. avalon(y50) -> starfall(y84) is 34% of the plate height =
        //  127.7 px, while the two nodes' FIXED footprints need
        //  HomeNodePx/2 + gap + band + NodePx/2 = 60 + 4 + 34 + 48 = 146 px. Short by 18.3 --
        //  the exact overlap the oracle printed.
        //  FIX (UI_PLAYBOOK sec.3 + sec.8): the map gets its OWN content host on chrome.content
        //  whose floor is the PUBLISHED top of the shared Close box (subtract the band you must
        //  clear) and which therefore takes back the footer band nobody owns. Plate height goes
        //  376 -> ~489 ref px, the pitch to ~166 px, and the pair clears by ~20 px on every
        //  landscape aspect. Not a tune: nothing here is a hand-picked offset.
        // =====================================================================

        /// <summary>Gap between a node disc's bottom edge and its title band.</summary>
        public const float NodeLabelGapPx = 4f;
        /// <summary>Fixed title band under a revealed node (one bold 16px line box + margin).</summary>
        public const float NodeLabelBandPx = 34f;
        /// <summary>Fixed title band width -- the corridor a lower node's disc must stay out of.</summary>
        public const float NodeLabelWidthPx = 220f;

        /// <summary>Vertical pitch two nodes need before the LOWER one's disc would cover the
        /// UPPER one's title band. Pure arithmetic on the published footprint constants so
        /// RealmMapRegression asserts the SAME number the View lays out with.</summary>
        public static float RequiredPitchPx(bool upperIsHome, bool lowerIsHome)
        {
            return (upperIsHome ? HomeNodePx : NodePx) * 0.5f
                 + NodeLabelGapPx + NodeLabelBandPx
                 + (lowerIsHome ? HomeNodePx : NodePx) * 0.5f;
        }

        /// <summary>Horizontal reach two nodes need before their title/disc corridors are
        /// disjoint (half a title band + half a disc).</summary>
        public static float RequiredCorridorPx(bool lowerIsHome)
        {
            return NodeLabelWidthPx * 0.5f + (lowerIsHome ? HomeNodePx : NodePx) * 0.5f;
        }

        // -- The modal's own anchor band + the published map/plate fractions ------------
        // Declared ONCE and read by Open(), so the close-band math and the modal can never
        // drift apart, and RealmMapRegression can recompute the plate from the same numbers.
        /// <summary>The modal's vertical anchor band.</summary>
        public const float PanelAnchorMinY = 0.06f;
        /// <inheritdoc cref="PanelAnchorMinY"/>
        public const float PanelAnchorMaxY = 0.94f;
        /// <summary>Breathing gap above the shared Close box before the map host may start.</summary>
        public const float CloseReserveGapFrac = 0.02f;
        /// <summary>Floor for the map host -- also the fallback when the Close cannot be measured,
        /// so a missing measurement never silently re-opens the band.</summary>
        public const float CloseReserveFloorY = 0.18f;
        /// <summary>Sanity ceiling: a very short canvas must not lose the whole map to the band.</summary>
        public const float CloseReserveMaxFrac = 0.45f;
        /// <summary>Smallest host the map may collapse to before the reserve stops taking height.</summary>
        public const float MinHostFracH = 0.30f;
        /// <summary>Content-host fallback rect when the frame published no body zone.</summary>
        public const float FallbackHostX0 = 0.055f;
        /// <inheritdoc cref="FallbackHostX0"/>
        public const float FallbackHostX1 = 0.945f;
        /// <inheritdoc cref="FallbackHostX0"/>
        public const float FallbackHostY0 = 0.20f;
        /// <inheritdoc cref="FallbackHostX0"/>
        public const float FallbackHostY1 = 0.835f;
        /// <summary>Landscape map plate, as a fraction of the content host.</summary>
        public const float LandscapeMapX0 = 0.02f;
        /// <inheritdoc cref="LandscapeMapX0"/>
        public const float LandscapeMapY0 = 0.04f;
        /// <inheritdoc cref="LandscapeMapX0"/>
        public const float LandscapeMapX1 = 0.58f;
        /// <inheritdoc cref="LandscapeMapX0"/>
        public const float LandscapeMapY1 = 0.98f;
        /// <summary>Gold trim thickness inset between the plate and the parchment the nodes sit on.</summary>
        public const float MapTrimPx = 3f;

        /// <summary>THE CONTRACT the landscape layout must deliver, in reference px: the parchment
        /// the nodes sit on is at least this tall and this wide. It is what makes the authored
        /// mapPoints seatable at all -- <c>RealmMapRegression</c> check (e) re-derives every
        /// authored node pair against these two numbers and the footprint constants above and
        /// FAILS if any lower node's disc would land on an upper node's title band. The capture
        /// oracle (<c>UI_GEOMETRY_OK</c>) proves the shipped layout actually meets the contract;
        /// this constant is what a future region/mapPoint/footprint edit gets checked against
        /// before anyone opens a PNG. Pre-WO-941 the plate resolved to 922 x 376 -- BELOW this
        /// floor -- which is exactly why "ELARION" ended up under the Starfall disc.</summary>
        public const float LandscapeMinPlateHeightPx = 460f;
        /// <inheritdoc cref="LandscapeMinPlateHeightPx"/>
        public const float LandscapeMinPlateWidthPx = 880f;

        // Aged-parchment map colours (the map plate is parchment, text on it is Ink).
        private static readonly Color ParchmentPlate = new Color(0.851f, 0.780f, 0.639f, 1f);
        private static readonly Color FogDisc        = new Color(0.10f, 0.09f, 0.11f, 0.94f);
        private static readonly Color ClearedDisc    = new Color(0.75f, 0.62f, 0.25f, 1f);
        private static readonly Color DiscoveredDisc = new Color(0.93f, 0.88f, 0.78f, 1f);

        private GameObject _ui;
        private Transform _nodeHost;         // rebuilt node buttons live here
        private TMPro.TextMeshProUGUI _detailTag;
        private TMPro.TextMeshProUGUI _detailTitle;
        private TMPro.TextMeshProUGUI _detailGate;
        private TMPro.TextMeshProUGUI _detailBody;
        private RectTransform _detailPane;
        private GameObject _travelCtaGo;

        private RealmMapVM _vm;
        private PanelHandle _handle;

        // ── Router registration (scene-independent open; see RealmMapPanelBootstrap) ──

        private void Awake()
        {
            PanelRouter.Register(PanelId.RealmMap, (System.Action)Open);
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.RealmMap, (System.Action)Open);
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
        }

        // ── Public API ──────────────────────────────────────────────────────────

        public void Open()
        {
            Close();

            if (_handle == null)
                _handle = PanelManager.Register("Realm Map", Close, () => _ui != null);

            FlowTrace.Step("RealmMap", "open (parchment map panel)");

            // VM FIRST — it loads the catalog + progress seam itself; this View
            // never touches RealmMapCatalog / GameStateService (strict MVVM).
            _vm = RealmMapVM.CreateDefault(Close);
            _vm.Changed += Repaint;

            var modal = ElarionUiKit.BuildObsidianModal("RealmMapPanelUI", _vm.Title,
                new Vector2(0.06f, PanelAnchorMinY), new Vector2(0.94f, PanelAnchorMaxY), Close, sortingOrder: 1000,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "crest");
            _ui = modal.canvas;
            var panel = modal.chrome.content;

            // Responsive split (spec wireframe): landscape map-left / detail-right;
            // portrait map-top ~55% / detail below.
            // Kit surface, not Screen.* — same value at runtime; a capture drives it so a
            // portrait shot exercises the portrait branch (Screen never moves in batchmode).
            bool portrait = ElarionUiKit.SurfaceHeight > ElarionUiKit.SurfaceWidth;

            // WO-941: the map does NOT drop into the frame's body zone any more — it builds its
            // own host on chrome.content, floored on the PUBLISHED top of the shared Close box.
            // In portrait that host is the body zone exactly (geometry unchanged); in landscape
            // it takes back the kit's footer reservation, which RealmMap never draws in and
            // which was costing the node layer the height its fixed footprints need.
            var bodyHost = BuildContentHost(panel.transform,
                (modal.chrome.layout != null) ? modal.chrome.layout.body : null,
                modal.chrome.close, portrait);

            Vector2 mapMin, mapMax, detailMin, detailMax;
            if (portrait)
            {
                mapMin = new Vector2(0.02f, 0.45f); mapMax = new Vector2(0.98f, 0.98f);
                detailMin = new Vector2(0.02f, 0.02f); detailMax = new Vector2(0.98f, 0.43f);
            }
            else
            {
                mapMin = new Vector2(LandscapeMapX0, LandscapeMapY0);
                mapMax = new Vector2(LandscapeMapX1, LandscapeMapY1);
                detailMin = new Vector2(0.60f, 0.06f); detailMax = new Vector2(0.98f, 0.96f);
            }

            // ── LEFT: the parchment map plate (gold trim + aged parchment fill) ──
            var trimGo = new GameObject("MapTrim", typeof(Image));
            trimGo.transform.SetParent(bodyHost, false);
            var trt = (RectTransform)trimGo.transform;
            trt.anchorMin = mapMin; trt.anchorMax = mapMax;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var trimImg = trimGo.GetComponent<Image>();
            trimImg.color = ElarionUi.Gold;
            trimImg.raycastTarget = false;

            var mapGo = new GameObject("MapParchment", typeof(Image));
            mapGo.transform.SetParent(trimGo.transform, false);
            var mrt = (RectTransform)mapGo.transform;
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(MapTrimPx, MapTrimPx);
            mrt.offsetMax = new Vector2(-MapTrimPx, -MapTrimPx);
            var mapImg = mapGo.GetComponent<Image>();
            mapImg.color = ParchmentPlate;
            mapImg.raycastTarget = false;

            // Node layer (full stretch of the parchment; rebuilt every Repaint).
            var hostGo = new GameObject("Nodes", typeof(RectTransform));
            hostGo.transform.SetParent(mapGo.transform, false);
            var hrt = (RectTransform)hostGo.transform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            _nodeHost = hostGo.transform;

            // ── RIGHT: the obsidian detail pane (always bound to the selection) ──
            var detailGo = new GameObject("DetailPane", typeof(Image));
            detailGo.transform.SetParent(bodyHost, false);
            _detailPane = (RectTransform)detailGo.transform;
            _detailPane.anchorMin = detailMin; _detailPane.anchorMax = detailMax;
            _detailPane.offsetMin = Vector2.zero; _detailPane.offsetMax = Vector2.zero;
            var dImg = detailGo.GetComponent<Image>();
            // WO-941: alpha 1, not 0.92. The pane no longer sits inside Zone_Body's opaque
            // ZoneBacking plate -- it sits on the frame art itself over part of its span, and a
            // 0.92 plate takes a tint from whatever is behind it (the RumorBoard detail plate
            // learned this the expensive way: 0.92 over tan parchment reads KHAKI in linear
            // space). An opaque plate reads the same wherever the host lands.
            dImg.color = new Color(0.05f, 0.045f, 0.04f, 1f);   // solid obsidian plate
            dImg.raycastTarget = false;

            _detailTag = MakeDetailLabel(detailGo.transform, "DetailTag",
                new Vector2(0.06f, 0.925f), new Vector2(0.94f, 0.985f),
                ElarionUi.ParchmentDim, 12, bold: false);

            _detailTitle = MakeDetailLabel(detailGo.transform, "DetailTitle",
                new Vector2(0.06f, 0.83f), new Vector2(0.94f, 0.925f),
                ElarionUi.Gilt, 18, bold: true);

            _detailGate = MakeDetailLabel(detailGo.transform, "DetailGate",
                new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.82f),
                ElarionUi.Gilt, 13, bold: false);
            _detailGate.textWrappingMode = TMPro.TextWrappingModes.Normal;

            // Scrollable description well (WO-795 law: long copy scrolls, never clips).
            _detailBody = BuildScrollText(detailGo.transform,
                new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.70f));

            Repaint();

            if (!PanelManager.NotifyOpened(_handle)) return;

            FlowTrace.Step("RealmMap", "opened with " + _vm.Nodes.Count +
                " nodes, selected '" + (_vm.SelectedId ?? "<none>") + "'");
        }

        public void Close()
        {
            bool wasOpen = _ui != null;
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _nodeHost = null;
            _detailTag = null;
            _detailTitle = null;
            _detailGate = null;
            _detailBody = null;
            _detailPane = null;
            _travelCtaGo = null;
            PanelManager.NotifyClosed(_handle);
            if (wasOpen) FlowTrace.Step("RealmMap", "closed");
        }

        // ── WO-941: the map's content host + the published Close band ──────────────

        /// <summary>
        /// Build the rect the map + detail actually live in, on <paramref name="panelContent"/>.
        ///
        /// PORTRAIT: the frame's body zone, verbatim — portrait geometry is unchanged.
        /// LANDSCAPE: the same x span and top line, but the FLOOR is the published top of the
        /// shared Close box (<see cref="CloseReserveTopFraction"/>) instead of the kit's
        /// footer-reserved body floor. RealmMap draws nothing in the footer band, so that
        /// reservation was pure loss — ~150 ref px of panel height that the node layer's
        /// FIXED-PIXEL footprints need to stay disjoint from one another (see the WO-941 block
        /// above). Taking it back is the sec.8 move in both directions at once: subtract the
        /// band another surface owns (the Close), consume the band nobody owns (the footer).
        ///
        /// SIBLING SEAT — this is load-bearing, not tidiness. The host goes IMMEDIATELY AFTER
        /// Zone_Body and nowhere else:
        ///   * NOT first-sibling. The kit paints an opaque near-black ZoneBacking plate inside
        ///     layout.body for every non-two-tone frame (FrameCore included) so the live scene
        ///     cannot bleed through a hollow frame. Behind that plate, the top two thirds of the
        ///     parchment map would simply be COVERED.
        ///   * NOT last. The shared Close is built LAST under chrome.content, so anything seated
        ///     after it paints over the Close — the exact class of defect the kit's close-band
        ///     reservation exists to prevent.
        /// Immediately after Zone_Body clears the backing plate and still leaves the medallion,
        /// the footer zone, the title and the Close as later siblings, on top.
        /// </summary>
        private static RectTransform BuildContentHost(Transform panelContent, RectTransform bodyZone,
                                                      Button close, bool portrait)
        {
            float x0 = bodyZone != null ? bodyZone.anchorMin.x : FallbackHostX0;
            float x1 = bodyZone != null ? bodyZone.anchorMax.x : FallbackHostX1;
            float y0 = bodyZone != null ? bodyZone.anchorMin.y : FallbackHostY0;
            float y1 = bodyZone != null ? bodyZone.anchorMax.y : FallbackHostY1;

            if (!portrait)
            {
                float panelHPx = Mathf.Max(1f, (PanelAnchorMaxY - PanelAnchorMinY) *
                                                ElarionUiKit.PostScaleCanvasHeight(panelContent));
                // The reserve is BOTH the reclaim and the guard: it lowers the floor when the
                // kit over-reserved, and raises it if the Close ever tops out higher.
                y0 = CloseReserveTopFraction(close, panelHPx);
                if (y1 - y0 < MinHostFracH) y0 = Mathf.Max(0f, y1 - MinHostFracH);
                FlowTrace.Step("RealmMap", string.Format(
                    "content host (WO-941) x {0:F3}..{1:F3} y {2:F3}..{3:F3}, panelH {4:F0} ref px",
                    x0, x1, y0, y1, panelHPx));
            }

            var go = new GameObject("MapContentHost", typeof(RectTransform));
            go.transform.SetParent(panelContent, false);
            if (bodyZone != null && bodyZone.parent == panelContent)
                go.transform.SetSiblingIndex(bodyZone.GetSiblingIndex() + 1);
            else
                go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// Where the ONE shared Close box really TOPS OUT, as a fraction of this panel's height,
        /// plus <see cref="CloseReserveGapFrac"/>. <c>ElarionUiKit.SeatSharedCloseInside</c> seats
        /// it with anchorMin.y == anchorMax.y == the band's lower edge, pivot y = 0 and a FIXED
        /// <c>CanonCtaHeight</c> box growing UPWARD, so the top is that anchor plus the fixed
        /// height over the panel height. <paramref name="panelHPx"/> comes from
        /// <c>PostScaleCanvasHeight</c> — never a live <c>rect.height</c>, which returns RAW
        /// SCREEN PIXELS on the canvas's creation frame (the F8-5 root cause the kit documents).
        /// Falls back to <see cref="CloseReserveFloorY"/> when the Close cannot be measured.
        /// </summary>
        private static float CloseReserveTopFraction(Button close, float panelHPx)
        {
            if (close == null || panelHPx <= 1f) return CloseReserveFloorY;
            var crt = close.transform as RectTransform;
            if (crt == null) return CloseReserveFloorY;
            float closeTop = crt.anchorMin.y + ElarionUiKit.CanonCtaHeight / panelHPx;
            return Mathf.Clamp(closeTop + CloseReserveGapFrac, CloseReserveFloorY, CloseReserveMaxFrac);
        }

        // ── Paint ───────────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_nodeHost == null || _vm == null) return;

            // Rebuild the node layer (selection halo moves with the selection).
            for (int i = _nodeHost.childCount - 1; i >= 0; i--)
            {
                var c = _nodeHost.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }

            // Guard the node build (§12): one bad row logs + skips, never blanks the map.
            foreach (var node in _vm.Nodes)
            {
                var row = node;   // capture for the closure
                Guard.Try("RealmMap", "build node '" + row.Id + "'", () => BuildNode(row));
            }

            RenderDetail();
        }

        private void BuildNode(RealmMapVM.NodeRow node)
        {
            bool selected = node.Id == _vm.SelectedId;
            float size = node.IsHome ? HomeNodePx : NodePx;

            // Anchor at mapPoint percent: x rightward, y DOWNWARD from the top
            // (the React realm-map-layout convention the JSON was authored in).
            var anchor = new Vector2(node.XPercent / 100f, 1f - node.YPercent / 100f);

            var root = new GameObject("Node_" + node.Id, typeof(RectTransform));
            root.transform.SetParent(_nodeHost, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);

            // Selected = gilt halo ring behind the disc (shape marker, not colour alone —
            // the detail pane carries the state as text).
            if (selected)
            {
                var halo = new GameObject("Halo", typeof(Image));
                halo.transform.SetParent(root.transform, false);
                var hart = (RectTransform)halo.transform;
                hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one;
                hart.offsetMin = new Vector2(-SelectHaloPx, -SelectHaloPx);
                hart.offsetMax = new Vector2(SelectHaloPx, SelectHaloPx);
                var hi = halo.GetComponent<Image>();
                hi.color = ElarionUi.Gilt;
                hi.raycastTarget = false;
                ElarionUiKit.ApplyRounded(hi);
            }

            // The disc (the tap target).
            var discGo = new GameObject("Disc", typeof(Image));
            discGo.transform.SetParent(root.transform, false);
            var drt = (RectTransform)discGo.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var disc = discGo.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(disc);

            string glyph;
            Color glyphColor;
            switch (node.State)
            {
                case RealmMapVM.NodeState.Home:
                    disc.color = ElarionUi.Gold;
                    glyph = "*"; glyphColor = ElarionUi.Ink;
                    break;
                case RealmMapVM.NodeState.Cleared:
                    disc.color = ClearedDisc;
                    glyph = "*"; glyphColor = ElarionUi.Ink;
                    break;
                case RealmMapVM.NodeState.Discovered:
                    disc.color = DiscoveredDisc;
                    glyph = ""; glyphColor = ElarionUi.Ink;
                    break;
                default:   // Locked — dark fog disc, "?", no title on the map
                    disc.color = FogDisc;
                    glyph = "?"; glyphColor = ElarionUi.ParchmentDim;
                    break;
            }

            // Gold trim ring for non-locked discs (a thin inset frame line).
            if (node.State != RealmMapVM.NodeState.Locked)
            {
                var ring = new GameObject("Ring", typeof(Image));
                ring.transform.SetParent(discGo.transform, false);
                var rrt = (RectTransform)ring.transform;
                rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
                rrt.offsetMin = new Vector2(4f, 4f); rrt.offsetMax = new Vector2(-4f, -4f);
                var ri = ring.GetComponent<Image>();
                ri.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f);
                ri.raycastTarget = false;
                ElarionUiKit.ApplyRounded(ri);
            }

            if (!string.IsNullOrEmpty(glyph))
            {
                var gGo = new GameObject("Glyph", typeof(TMPro.TextMeshProUGUI));
                gGo.transform.SetParent(discGo.transform, false);
                var grt = (RectTransform)gGo.transform;
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                var gt = gGo.GetComponent<TMPro.TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(gt);
                gt.text = glyph;
                gt.fontSize = node.IsHome ? 34 : 26;
                gt.fontStyle = TMPro.FontStyles.Bold;
                gt.color = glyphColor;
                gt.alignment = TMPro.TextAlignmentOptions.Center;
                gt.raycastTarget = false;
            }

            // Title inked BELOW the disc for home + revealed regions (never for fog).
            if (node.State != RealmMapVM.NodeState.Locked)
            {
                var lGo = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
                lGo.transform.SetParent(root.transform, false);
                var lrt = (RectTransform)lGo.transform;
                lrt.anchorMin = new Vector2(0.5f, 0f); lrt.anchorMax = new Vector2(0.5f, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -NodeLabelGapPx);
                lrt.sizeDelta = new Vector2(NodeLabelWidthPx, NodeLabelBandPx);
                var lt = lGo.GetComponent<TMPro.TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(lt);
                lt.text = node.IsHome ? node.Title.ToUpperInvariant() : node.Title;
                lt.fontSize = node.IsHome ? 16 : 13;
                lt.fontStyle = TMPro.FontStyles.Bold;
                lt.color = ElarionUi.Ink;   // dark ink on parchment (contrast law)
                lt.alignment = TMPro.TextAlignmentOptions.Center;
                lt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                lt.overflowMode = TMPro.TextOverflowModes.Overflow;
                lt.raycastTarget = false;
            }

            // The whole disc selects (routes to the VM; the View holds no state).
            var btn = discGo.AddComponent<Button>();
            btn.targetGraphic = disc;
            ElarionUiKit.StyleButtonColors(btn);
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Select(id); });
        }

        // ── Detail pane (always bound to the selection) ─────────────────────────

        private void RenderDetail()
        {
            if (_detailTitle == null || _vm == null) return;

            if (_travelCtaGo != null) { Destroy(_travelCtaGo); _travelCtaGo = null; }

            _detailTag.text = _vm.DetailState;
            _detailTitle.text = _vm.DetailTitle;
            ElarionUiKit.FitSingleLine(_detailTitle);
            _detailGate.text = _vm.DetailGate;
            _detailBody.text = _vm.DetailBody;

            if (!_vm.ShowTravel) return;

            // Reserved Travel CTA slot (WO-827 lands the real travel). Disabled stub:
            // non-interactable + dimmed, and the LABEL carries the state (colorblind law).
            _travelCtaGo = new GameObject("TravelCta", typeof(RectTransform));
            _travelCtaGo.transform.SetParent(_detailPane, false);
            var crt = (RectTransform)_travelCtaGo.transform;
            crt.anchorMin = new Vector2(0.06f, 0.04f);
            crt.anchorMax = new Vector2(0.94f, 0.20f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            var cta = ElarionUiKit.BuildObsidianButton(_travelCtaGo.transform, _vm.TravelLabel,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                Vector2.zero, Vector2.one, () => { });
            if (cta != null)
            {
                cta.interactable = _vm.TravelEnabled;   // false until WO-827
                var face = cta.targetGraphic as Image;
                if (face != null) face.color = ElarionUi.Disabled;
                var label = cta.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null) label.color = ElarionUi.ParchmentDim;
            }
        }

        // ── uGUI helpers (mirror RumorBoardPanel / ClanChatPanel) ───────────────

        private static TMPro.TextMeshProUGUI MakeDetailLabel(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, Color color, float size, bool bold)
        {
            var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = size;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            t.color = color;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.raycastTarget = false;
            return t;
        }

        // A vertical-scroll text well: the description scrolls when long (WO-795 law)
        // instead of clipping mid-word. Returns the TMP text the detail body binds.
        private static TMPro.TextMeshProUGUI BuildScrollText(Transform parent,
            Vector2 aMin, Vector2 aMax)
        {
            var viewportGo = new GameObject("BodyScroll", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(parent, false);
            var vrt = (RectTransform)viewportGo.transform;
            vrt.anchorMin = aMin; vrt.anchorMax = aMax;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);   // drag catcher

            var textGo = new GameObject("BodyText", typeof(TMPro.TextMeshProUGUI), typeof(ContentSizeFitter));
            textGo.transform.SetParent(viewportGo.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = textGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = 14;
            t.color = ElarionUi.Parchment;
            t.alignment = TMPro.TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
            t.raycastTarget = false;
            textGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = trt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            return t;
        }
    }
}
