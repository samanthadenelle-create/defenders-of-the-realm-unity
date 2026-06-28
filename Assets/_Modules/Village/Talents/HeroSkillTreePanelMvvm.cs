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
// Code-built uGUI ONLY (no UXML — §8). Edge geometry uses a fixed-size content
// rect (CW×CH px) so rotated connector images are deterministic at build time
// (no dependence on a layout pass). The content scrolls (owner: one scrollable
// canvas, Knight + Shared, no pagination yet).
//
// This panel LEADS the Obsidian look: it uses the mirrored talent sprites
// (slot_talent / panel_talent) whenever present — independent of the global
// BlinkChrome "hide our dressing" flag — and falls back to procedural rounded
// plates + the default dark frame when the art is absent. Every sprite lookup
// is null-safe, so it renders correctly in both states.
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
        private TMPro.TextMeshProUGUI _walletText;
        private TMPro.TextMeshProUGUI _planText;
        private Button _confirmBtn;
        private Button _cancelBtn;

        private PanelHandle _panelHandle;

        // Fixed graph canvas size (px). Edge lines are rotated images positioned in
        // this space — a definite size keeps geometry exact without a layout pass.
        private const float CW = 1400f;
        private const float CH = 1040f;
        private const float NodeSize = 132f;

        public bool IsOpen => _ui != null;

        // ── Registration (mirror BuildingUpgradePanelMvvm) ────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Skills", Close, () => IsOpen);
            PanelRouter.Register(PanelId.HeroSkillTree, Open);
            // Take over the legacy UIDocument route id too (the old panel renders empty
            // in player builds — §8); its bootstrap is gated off.
            PanelRouter.Register(PanelId.HeroTalents, Open);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.HeroSkillTree, Open);
            PanelRouter.Unregister(PanelId.HeroTalents, Open);
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

            if (_walletText != null)
            {
                int w = _vm.RemainingWisdom;
                int p = _vm.PendingCost;
                _walletText.text = p > 0
                    ? "Wisdom " + w + "    Planning -" + p + "  (" + (w - p) + " left)"
                    : "Wisdom " + w + "    Skill Points " + _vm.RemainingSkillPoints;
            }

            if (_planText != null)
            {
                int n = _vm.PendingCount;
                _planText.text = n > 0
                    ? "Plan: " + n + " node" + (n == 1 ? "" : "s") + "  ·  " + _vm.PendingCost + " Wisdom"
                    : "Tap nodes to plan an unlock path";
            }
            if (_confirmBtn != null)
            {
                _confirmBtn.interactable = _vm.CanCommit;
                SetButtonAlpha(_confirmBtn, _vm.CanCommit ? 1f : 0.4f);
            }
            if (_cancelBtn != null)
            {
                bool any = _vm.PendingCount > 0;
                _cancelBtn.interactable = any;
                SetButtonAlpha(_cancelBtn, any ? 1f : 0.4f);
            }

            RebuildGraph();
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

            // Section label (above the shared band).
            BuildSectionLabel("Universal — any class", 0.965f);

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
        private Vector2 CenterPx(float x, float y) => new Vector2((x - 0.5f) * CW, -(y * CH));

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
            r.sizeDelta = new Vector2(len, live ? 5f : 3f);
            r.localRotation = Quaternion.Euler(0f, 0f, ang);
            var img = go.GetComponent<Image>();
            img.color = live
                ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f)
                : new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.22f);
            img.raycastTarget = false;
        }

        private void BuildSectionLabel(string text, float y)
        {
            var go = new GameObject("Section", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(_graphContent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(CW * 0.5f, 36f);
            r.anchoredPosition = new Vector2(-CW * 0.22f, -(y * CH));
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = ElarionUi.FontMicro + 2f;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.raycastTarget = false;
        }

        // ── One graph node (plate + icon + name + state; click = stage/unstage) ──

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
            // Obsidian node plate (slot_talent) whenever the art is present — this panel
            // leads the Obsidian look regardless of the global BlinkChrome dressing flag
            // (owner wants the talent tree Obsidian); rounded procedural fallback otherwise.
            Sprite plateSprite = RpgUiCatalog.Get("slot", "slot_talent");
            if (plateSprite != null)
            {
                img.sprite = plateSprite;
                img.type = Image.Type.Sliced;
                img.color = PlateTint(node);
            }
            else
            {
                ElarionUiKit.ApplyRounded(img);
                img.color = PlateColor(node);
            }

            // Capstone frame — distinct gilt border behind the plate.
            if (node.IsCapstone)
            {
                Sprite frameSprite = RpgUiCatalog.Get("slot", "slot_talent_6");
                var frame = new GameObject("CapstoneFrame", typeof(Image));
                frame.transform.SetParent(go.transform, false);
                var fr = frame.GetComponent<RectTransform>();
                fr.anchorMin = new Vector2(-0.10f, -0.10f);
                fr.anchorMax = new Vector2(1.10f, 1.10f);
                fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
                var fImg = frame.GetComponent<Image>();
                if (frameSprite != null) { fImg.sprite = frameSprite; fImg.type = Image.Type.Sliced; }
                fImg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b,
                                       node.Owned || node.CanUnlock || node.IsPending ? 0.85f : 0.45f);
                fImg.raycastTarget = false;
                frame.transform.SetAsFirstSibling();
            }

            // Planned ring — a bright outline so a staged node reads at a glance.
            if (node.IsPending)
            {
                var ring = new GameObject("PlanRing", typeof(Image));
                ring.transform.SetParent(go.transform, false);
                var rr = ring.GetComponent<RectTransform>();
                rr.anchorMin = new Vector2(-0.06f, -0.06f);
                rr.anchorMax = new Vector2(1.06f, 1.06f);
                rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
                var rImg = ring.GetComponent<Image>();
                rImg.color = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.9f);
                rImg.raycastTarget = false;
                ring.transform.SetAsFirstSibling();
            }

            // Click → stage (if stageable) or unstage (if already planned).
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            bool clickable = node.IsPending || node.CanUnlock;
            btn.interactable = clickable;
            string id = node.Id;
            bool pending = node.IsPending;
            btn.onClick.AddListener(() =>
            {
                if (_vm == null) return;
                if (pending) _vm.Unstage(id);
                else _vm.Stage(id);
            });

            // Icon (upper portion).
            var sprite = LoadIcon(node.IconPath);
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.22f, 0.40f);
                ir.anchorMax = new Vector2(0.78f, 0.92f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = sprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                if (!node.Owned && !node.CanUnlock && !node.IsPending)
                    iImg.color = new Color(1f, 1f, 1f, 0.40f); // dim locked
            }

            // Name (mid band).
            Color nameColor = node.Owned || node.CanUnlock || node.IsPending
                ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
            float nameTop = sprite != null ? 0.40f : 0.82f;
            ElarionUiKit.Label(go.transform, node.Name, nameTop - 0.22f, nameTop, nameColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);

            // State line (bottom): owned / planned / cost / lock reason.
            string stateLine; Color stateColor;
            if (node.Owned) { stateLine = "Owned"; stateColor = ElarionUi.Gilt; }
            else if (node.IsPending) { stateLine = "Planned -" + node.WisdomCost; stateColor = ElarionUi.Affordable; }
            else if (node.CanUnlock) { stateLine = node.WisdomCost + " Wisdom"; stateColor = ElarionUi.Affordable; }
            else { stateLine = string.IsNullOrEmpty(node.LockReason) ? "Locked" : node.LockReason; stateColor = ElarionUi.Danger; }
            ElarionUiKit.Label(go.transform, stateLine, 0.02f, 0.20f, stateColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f);

            // Equipped chip (Skill nodes slotted in the loadout).
            if (node.IsEquipped)
                ElarionUiKit.Label(go.transform, "EQUIPPED", 0.80f, 0.99f, ElarionUi.Affordable,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Right, 0.30f, 0.98f, bold: true);
        }

        // Plate colour by state (procedural fallback path).
        private static Color PlateColor(SkillNodeVM node)
        {
            if (node.Owned) return new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.26f);
            if (node.IsPending) return new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.34f);
            if (node.CanUnlock) return new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.22f);
            return new Color(ElarionUiKit.Cell.r, ElarionUiKit.Cell.g, ElarionUiKit.Cell.b, 0.42f);
        }

        // Tint for the sprite plate (sliced Obsidian art) by state.
        private static Color PlateTint(SkillNodeVM node)
        {
            if (node.Owned) return new Color(0.78f, 1f, 0.82f, 1f);
            if (node.IsPending) return new Color(1f, 0.92f, 0.62f, 1f);
            if (node.CanUnlock) return Color.white;
            return new Color(0.62f, 0.60f, 0.58f, 0.9f); // dim locked
        }

        // ── Chrome (presentation only) ────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("HeroSkillTreePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            var backdrop = ElarionUiKit.AddImage(_ui.transform, "SkillBackdrop",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f), rounded: false);
            var bdImg = backdrop.GetComponent<Image>();
            if (bdImg != null) bdImg.raycastTarget = false;

            // Obsidian talent window frame (panel_talent) when present; default frame otherwise.
            string panelSprite = RpgUiCatalog.Get("panel", "panel_talent") != null
                ? "panel_talent" : RpgUiCatalog.PanelWindowDark;
            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f),
                                                   deep: true, packSpriteName: panelSprite);
            var panel = panelGo.transform;

            // Always keep a dark backing behind the (9-slice) Obsidian frame so the
            // graph + text stay readable — the frame is a border, not a full fill.
            Color fillColor = new Color(0.07f, 0.055f, 0.042f, 0.985f);
            var solidFill = ElarionUiKit.AddImage(panel, "SkillSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f), fillColor);
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            _headerLabel = ElarionUiKit.Header(panel, "Skills", x0: 0.04f, x1: 0.74f, y0: 0.91f, y1: 0.975f);

            // Wisdom readout under the header.
            var walletGo = new GameObject("Wallet", typeof(TMPro.TextMeshProUGUI));
            walletGo.transform.SetParent(panel, false);
            var wr = walletGo.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.04f, 0.85f); wr.anchorMax = new Vector2(0.96f, 0.905f);
            wr.offsetMin = Vector2.zero; wr.offsetMax = Vector2.zero;
            _walletText = walletGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_walletText);
            _walletText.fontSize = ElarionUi.FontLabel;
            _walletText.color = ElarionUi.Gilt;
            _walletText.alignment = TMPro.TextAlignmentOptions.Center;
            _walletText.raycastTarget = false;

            // Equip button (top-right of header).
            var equipBtn = ElarionUiKit.ButtonPack(panel, "Equip", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.78f, 0.915f), new Vector2(0.96f, 0.975f), OpenLoadout,
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var equipLbl = equipBtn != null ? equipBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (equipLbl != null) { equipLbl.color = ElarionUi.Parchment; equipLbl.fontStyle = TMPro.FontStyles.Bold; }

            BuildScrollGraph(panel);
            BuildFooter(panel);
        }

        // The scrollable graph viewport (mask) + fixed-size content (nodes/edges).
        private void BuildScrollGraph(Transform panel)
        {
            var areaGo = new GameObject("GraphScroll", typeof(RectTransform), typeof(ScrollRect));
            areaGo.transform.SetParent(panel, false);
            var ar = areaGo.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0.045f, 0.165f); ar.anchorMax = new Vector2(0.955f, 0.84f);
            ar.offsetMin = Vector2.zero; ar.offsetMax = Vector2.zero;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(areaGo.transform, false);
            var vr = viewportGo.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewportGo.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible, but raycastable for drag-scroll

            var contentGo = new GameObject("GraphContent", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _graphContent = contentGo.GetComponent<RectTransform>();
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0.5f, 1f);
            _graphContent.pivot = new Vector2(0.5f, 1f);
            _graphContent.sizeDelta = new Vector2(CW, CH);
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

        // Plan summary + CONFIRM / Cancel / Close.
        private void BuildFooter(Transform panel)
        {
            var planGo = new GameObject("PlanSummary", typeof(TMPro.TextMeshProUGUI));
            planGo.transform.SetParent(panel, false);
            var pr = planGo.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.06f, 0.075f); pr.anchorMax = new Vector2(0.40f, 0.135f);
            pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            _planText = planGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_planText);
            _planText.fontSize = ElarionUi.FontMicro + 2f;
            _planText.color = ElarionUi.Gilt;
            _planText.alignment = TMPro.TextAlignmentOptions.Left;
            _planText.raycastTarget = false;

            _confirmBtn = ElarionUiKit.ButtonPack(panel, "CONFIRM", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.55f, 0.07f), new Vector2(0.80f, 0.135f),
                () => { if (_vm != null) _vm.Commit(); },
                packSpriteName: RpgUiCatalog.Get("button", "button_confirm") != null
                    ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold);
            var confLbl = _confirmBtn != null ? _confirmBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (confLbl != null)
            {
                confLbl.color = ElarionUi.Parchment; confLbl.fontStyle = TMPro.FontStyles.Bold;
                confLbl.outlineColor = new Color32(20, 12, 4, 235); confLbl.outlineWidth = 0.22f;
            }

            _cancelBtn = ElarionUiKit.ButtonPack(panel, "Cancel", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.41f, 0.075f), new Vector2(0.53f, 0.135f),
                () => { if (_vm != null) _vm.CancelPlan(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var canLbl = _cancelBtn != null ? _cancelBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (canLbl != null) { canLbl.color = ElarionUi.Parchment; canLbl.fontStyle = TMPro.FontStyles.Bold; }

            var closeBtn = ElarionUiKit.ButtonPack(panel, "Close", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.82f, 0.075f), new Vector2(0.95f, 0.135f),
                () => { if (_vm != null) _vm.Close(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var closeLbl = closeBtn != null ? closeBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (closeLbl != null) { closeLbl.color = ElarionUi.Parchment; closeLbl.fontStyle = TMPro.FontStyles.Bold; }
        }

        private static void SetButtonAlpha(Button btn, float a)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) { var c = img.color; c.a = a; img.color = c; }
        }

        private void OpenLoadout()
        {
            if (!PanelRouter.Open(PanelId.HeroLoadout))
                Debug.LogWarning("[HeroSkillTreePanelMvvm] HeroLoadout panel not registered — Equip is a no-op.");
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

        private void Close()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            _walletText = null;
            _planText = null;
            _confirmBtn = null;
            _cancelBtn = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _graphContent = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
