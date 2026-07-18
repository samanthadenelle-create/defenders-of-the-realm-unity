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
// WO-676 §B (owner-approved icon-only redesign, 2026-07-11): nodes are ICON-ONLY
// ~96px plates carrying exactly ONE state affordance — cost pip (unlockable),
// −n pip + ring (planned), check stamp (owned), dim (locked). ALL name/desc/state
// text lives in the right-hand detail column. Wisdom is a CurrencyChip (top-right);
// the plan summary folds into the CONFIRM label ("CONFIRM n · −cost"); quick-swap
// and respec feedback are transient toasts (BuildFeedbackToast) — target ≤2
// persistent text strips outside the graph. Colorblind law: every state carries a
// shape/stamp/pip, never hue alone (dim = luminance, pips/stamps = shape+text).
//
// Code-built uGUI ONLY (no UXML — §8). Edge geometry uses a fixed-size content
// rect (CW×CH px) so rotated connector images are deterministic at build time
// (no dependence on a layout pass). The content scrolls (owner: one scrollable
// canvas, Knight + Shared, no pagination yet).
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

        // Fixed graph canvas size (px). Edge lines are rotated images positioned in
        // this space — a definite size keeps geometry exact without a layout pass.
        private const float CW = 1400f;
        private const float CH = 1040f;
        private const float NodeSize = 96f;   // §B.1 icon-only plates (was 132 text-stacked)

        public bool IsOpen => _ui != null;

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

            // Lookup id -> node + pixel centre, for the prerequisite connectors.
            var center = new Dictionary<string, Vector2>(64);
            var nodeById = new Dictionary<string, SkillNodeVM>(64);
            CollectPositions(_vm.Nodes, center, nodeById);
            CollectPositions(_vm.Shared, center, nodeById);

            // Edges first (drawn behind the node plates).
            DrawEdges(_vm.Nodes, center, nodeById);
            DrawEdges(_vm.Shared, center, nodeById);

            // Section divider (above the shared band) — WO-675 crown-glyph band grammar.
            BuildSectionBand("Universal - any class", 0.965f);

            // Nodes on top.
            foreach (var n in _vm.Nodes) BuildGraphNode(n, center);
            foreach (var n in _vm.Shared) BuildGraphNode(n, center);
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

        // px centre for an authored (x,y): content anchored top-CENTRE, y grows down.
        // Shifted down by NodeSize/2 (matching the content top pad) so the top-tier row
        // is fully inside the padded content rect and the bottom-tier row clears the base.
        private Vector2 CenterPx(float x, float y) => new Vector2((x - 0.5f) * CW, -(y * CH) - NodeSize * 0.5f);

        private void BuildEdge(Vector2 a, Vector2 b, bool live)
        {
            var go = new GameObject("Edge", typeof(Image));
            go.transform.SetParent(_graphContent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
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
        private void BuildSectionBand(string text, float y)
        {
            var host = new GameObject("SectionBand", typeof(RectTransform));
            host.transform.SetParent(_graphContent, false);
            var r = (RectTransform)host.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(CW * 0.92f, 36f);
            r.anchoredPosition = new Vector2(0f, -(y * CH) - NodeSize * 0.5f);

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

            var label = ElarionUiKit.Label(host.transform, text, 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontMicro + 2, TMPro.TextAlignmentOptions.MidlineLeft, 0.038f, 0.40f, bold: true);
            label.raycastTarget = false;
            ElarionUiKit.FitSingleLine(label);

            // Thin gilt rule filling the remainder of the band row.
            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(host.transform, false);
            var rr = (RectTransform)rule.transform;
            rr.anchorMin = new Vector2(0.41f, 0.5f); rr.anchorMax = new Vector2(1f, 0.5f);
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
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = c;
            rt.sizeDelta = new Vector2(NodeSize, NodeSize);

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

            // §B.2 — Wisdom wallet = the ONE CurrencyChip (top-right of the body zone;
            // tag "WISDOM" guarantees identity even if the icon art is absent).
            // Owner F8 2026-07-11: widened (0.76 → 0.70) — the old width truncated the
            // tag to "WISD..."; "WISDOM 1,016" must fit on one line.
            _wisdomChip = ElarionUiKit.CurrencyChip(panel, ElarionUiKit.CurrencyKind.Wisdom,
                new Vector2(0.70f, 0.855f), new Vector2(0.955f, 0.915f), tag: "WISDOM");

            // (The old "Equip" button that opened a second loadout screen is GONE — the
            // quick-swap row below folds that assign flow into THIS screen, owner 2026-06-28.)

            BuildScrollGraph(panel);
            BuildDetailAndQuickSwap(panel);
            BuildFooter(panel);
        }

        // The right-hand column: a SELECTED-talent detail strip (name + description +
        // state) over a QUICK-SWAP row (slots 1-4). Browse → select → read → confirm →
        // assign, all on one screen (no second loadout panel).
        private void BuildDetailAndQuickSwap(Transform panel)
        {
            const float colX0 = 0.675f, colX1 = 0.955f;
            const float txX0 = 0.69f, txX1 = 0.94f;

            // Right-column host. The frame supplies the chrome (UI canon §4: no per-screen wells) —
            // so this is now a TRANSPARENT layout host, not a self-styled dark plate, to avoid
            // double-framing inside the body zone. It still groups the detail strip + quick-swap row.
            var detailBg = ElarionUiKit.AddImage(panel, "DetailHost",
                new Vector2(colX0, 0.40f), new Vector2(colX1, 0.84f),
                new Color(0f, 0f, 0f, 0f));
            var dbImg = detailBg.GetComponent<Image>();
            if (dbImg != null) dbImg.raycastTarget = false;

            // Two ALTERNATIVE folds (eyes-sweep 2026-07-06 fix): the empty-state prompt
            // and the selected-talent detail share the strip's bands but live under
            // separate full-rect hosts — RenderDetail activates exactly ONE. Children
            // keep their panel-fraction anchors (the hosts span the whole body zone).
            _detailGroup = MakeGroupHost(panel, "DetailGroup");
            _emptyGroup = MakeGroupHost(panel, "EmptyStateGroup");
            _detailGroup.SetActive(false);   // empty-state is the default fold until a node is selected

            var selHeader = ElarionUiKit.Label(_detailGroup.transform, "SELECTED TALENT", 0.805f, 0.835f, ElarionUi.Gilt,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            ElarionUiKit.FitSingleLine(selHeader);

            _detailName = ElarionUiKit.Label(_detailGroup.transform, "", 0.745f, 0.805f, ElarionUi.Parchment,
                ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            ElarionUiKit.FitSingleLine(_detailName);   // long talent names shrink/ellipsize, never spill

            _detailDesc = ElarionUiKit.Label(_detailGroup.transform, "",
                0.475f, 0.735f, ElarionUi.Parchment, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.TopLeft, txX0, txX1);
            ElarionUiKit.FitBlock(_detailDesc);        // wraps + truncates inside its band

            _detailState = ElarionUiKit.Label(_detailGroup.transform, "", 0.41f, 0.465f, ElarionUi.Affordable,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            ElarionUiKit.FitSingleLine(_detailState);

            // Empty-state fold — SAME bands, rendered INSTEAD of the detail fold.
            var emptyTitle = ElarionUiKit.Label(_emptyGroup.transform, "Select a talent", 0.745f, 0.805f,
                ElarionUi.Parchment, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, txX0, txX1, bold: true);
            ElarionUiKit.FitSingleLine(emptyTitle);
            var emptyBody = ElarionUiKit.Label(_emptyGroup.transform,
                "Tap any node to read what it does before you confirm.",
                0.475f, 0.735f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.TopLeft, txX0, txX1);
            ElarionUiKit.FitBlock(emptyBody);

            // Quick-swap slot row (always visible — outside both folds). §B.4: the caption
            // + hint strips are GONE — slots keep their numerals; the detail state line
            // carries the "tap a slot (1-4)" hint; action feedback is a toast.
            _quickRoot = new GameObject("QuickSwapRow", typeof(RectTransform));
            _quickRoot.transform.SetParent(panel, false);
            var qr = _quickRoot.GetComponent<RectTransform>();
            qr.anchorMin = new Vector2(colX0 + 0.008f, 0.165f);
            qr.anchorMax = new Vector2(colX1 - 0.008f, 0.375f);
            qr.offsetMin = Vector2.zero; qr.offsetMax = Vector2.zero;
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
        private void BuildScrollGraph(Transform panel)
        {
            var areaGo = new GameObject("GraphScroll", typeof(RectTransform), typeof(ScrollRect));
            areaGo.transform.SetParent(panel, false);
            var ar = areaGo.GetComponent<RectTransform>();
            // Left ~62% of the panel; the right column is the detail + quick-swap strip.
            ar.anchorMin = new Vector2(0.045f, 0.165f); ar.anchorMax = new Vector2(0.655f, 0.84f);
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
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0.5f, 1f);
            _graphContent.pivot = new Vector2(0.5f, 1f);
            // Pad the scroll content by a full node on top+bottom so the FIRST and LAST
            // tier rows (authored at y≈0 / y≈1) aren't half-clipped by the viewport — a
            // node's centre sits at y*CH and its plate extends ±NodeSize/2, which fell
            // outside a CH-tall content rect (bottom green row was clipped). CenterPx +
            // BuildSectionBand shift down by NodeSize/2 to sit inside this padded rect.
            _graphContent.sizeDelta = new Vector2(CW, CH + NodeSize);
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

        // CONFIRM / Cancel / Respec (§B.2 — no plan-summary strip; the plan folds into
        // the CONFIRM label, "CONFIRM n · −cost", written by Render).
        private void BuildFooter(Transform panel)
        {
            _confirmBtn = ElarionUiKit.ButtonPack(panel, "CONFIRM", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.52f, 0.07f), new Vector2(0.80f, 0.135f),
                () => { if (_vm != null) _vm.ConfirmOrAssign(); },
                packSpriteName: RpgUiCatalog.Get("button", "button_confirm") != null
                    ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold);
            var confLbl = _confirmBtn != null ? _confirmBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (confLbl != null)
            {
                confLbl.color = ElarionUi.Parchment; confLbl.fontStyle = TMPro.FontStyles.Bold;
                confLbl.outlineColor = new Color32(20, 12, 4, 235); confLbl.outlineWidth = 0.22f;
            }
            _confirmLabel = confLbl;

            _cancelBtn = ElarionUiKit.ButtonPack(panel, "Cancel", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.38f, 0.075f), new Vector2(0.50f, 0.135f),
                () => { if (_vm != null) _vm.CancelPlan(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var canLbl = _cancelBtn != null ? _cancelBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (canLbl != null) { canLbl.color = ElarionUi.Parchment; canLbl.fontStyle = TMPro.FontStyles.Bold; }

            // RESPEC — refund this hero's talents for a Crystal cost (owner F8 "no respec option").
            // Surfaces the legacy TalentTreePanel respec on the LIVE MVVM panel via vm.Respec().
            // Owner F8 2026-07-11: "Respec  300 Crystals" truncated to "Respec 300 Cry..." in
            // the narrow button — short label ("Respec 300c") + FitSingleLine so it never spills.
            int respecCost = _vm != null ? _vm.RespecCost : HeroSkillTreeVMRespecFallbackCost;
            _respecBtn = ElarionUiKit.ButtonPack(panel, "Respec " + respecCost + "c", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.815f, 0.075f), new Vector2(0.955f, 0.135f),
                () => { if (_vm != null) _vm.Respec(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var resLbl = _respecBtn != null ? _respecBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (resLbl != null)
            {
                resLbl.color = ElarionUi.Parchment; resLbl.fontStyle = TMPro.FontStyles.Bold;
                ElarionUiKit.FitSingleLine(resLbl);
            }

            // §B.2 — respec status is a transient toast (see Render), not a persistent strip.
            // Close is the SHARED top-right Obsidian Close button (WO-554) — no per-panel footer Close.
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

        private void RebuildQuickSlots()
        {
            ClearChildren(_quickRoot);
            if (_quickRoot == null || _vm == null) return;

            var slots = _vm.QuickSlots;
            int n = slots != null ? slots.Count : 0;
            if (n <= 0) return;

            const int cols = 2;
            float gapX = 0.04f, gapY = 0.10f;
            int rows = (n + cols - 1) / cols;
            float w = (1f - gapX * (cols - 1)) / cols;
            float h = (1f - gapY * (rows - 1)) / rows;
            bool assignTarget = _vm.SelectedIsAssignable;
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, r = i / cols;
                float x0 = c * (w + gapX);
                float y1 = 1f - r * (h + gapY);
                float y0 = y1 - h;
                BuildQuickSlotTile(_quickRoot.transform, slots[i], x0, x0 + w, y0, y1, assignTarget);
            }
        }

        private void BuildQuickSlotTile(Transform parent, LoadoutSlotVM slot,
                                        float x0, float x1, float y0, float y1, bool assignTarget)
        {
            var tile = new GameObject("Quick_" + slot.SlotKey, typeof(Image), typeof(Button));
            tile.transform.SetParent(parent, false);
            var rt = tile.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
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

            var keyLbl = ElarionUiKit.Label(tile.transform, slot.SlotKey, 0.60f, 0.95f, ElarionUi.Gilt,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
            ElarionUiKit.FitSingleLine(keyLbl);

            string body = slot.IsEmpty ? (assignTarget ? "tap to set" : "+") : slot.AbilityName;
            Color bodyColor = slot.IsEmpty
                ? (assignTarget ? ElarionUi.Gilt : ElarionUi.ParchmentDim)
                : ElarionUi.Parchment;
            var bodyLbl = ElarionUiKit.Label(tile.transform, body, 0.06f, 0.56f, bodyColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: !slot.IsEmpty);
            // Ability names wrap + truncate inside their band — never over the slot key.
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
