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
        private const float NodePx = 96f;
        private const float HomeNodePx = 120f;
        private const float SelectHaloPx = 16f;

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
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Close, sortingOrder: 1000,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "crest");
            _ui = modal.canvas;
            var panel = modal.chrome.content;
            var bodyHost = (modal.chrome.layout != null && modal.chrome.layout.body != null)
                ? modal.chrome.layout.body : (RectTransform)panel.transform;

            // Responsive split (spec wireframe): landscape map-left / detail-right;
            // portrait map-top ~55% / detail below.
            // Kit surface, not Screen.* — same value at runtime; a capture drives it so a
            // portrait shot exercises the portrait branch (Screen never moves in batchmode).
            bool portrait = ElarionUiKit.SurfaceHeight > ElarionUiKit.SurfaceWidth;
            Vector2 mapMin, mapMax, detailMin, detailMax;
            if (portrait)
            {
                mapMin = new Vector2(0.02f, 0.45f); mapMax = new Vector2(0.98f, 0.98f);
                detailMin = new Vector2(0.02f, 0.02f); detailMax = new Vector2(0.98f, 0.43f);
            }
            else
            {
                mapMin = new Vector2(0.02f, 0.04f); mapMax = new Vector2(0.58f, 0.98f);
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
            mrt.offsetMin = new Vector2(3f, 3f); mrt.offsetMax = new Vector2(-3f, -3f);
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
            dImg.color = new Color(0.05f, 0.045f, 0.04f, 0.92f);   // solid obsidian plate
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
                lrt.anchoredPosition = new Vector2(0f, -4f);
                lrt.sizeDelta = new Vector2(220f, 34f);
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
