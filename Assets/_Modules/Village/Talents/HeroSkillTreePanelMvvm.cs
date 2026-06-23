// =============================================================================
// HeroSkillTreePanelMvvm — the Knight skill-tree VIEW (MVVM slice). A DUMB SKIN:
// it builds presentation (ElarionUiKit dark-glass + gold frame) and BINDS a
// HeroSkillTreeVM. ALL state/logic (tree build, unlock, lock reasons, equipped
// marks) lives in the VM — the View never reads game state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// Code-built uGUI ONLY (no UXML — §8; the legacy HeroTalentPanel UIDocument
// renders empty in player builds, which this REPLACES). Layout:
//   * Header: title + "Wisdom N   Skill Points M".
//   * Column-per-branch (Ranged / Heal-Sustain / Control), each a vertical stack
//     of TIER ROWS top-down (tier 3 at the top, tier 1 at the bottom — the canon
//     tree shape from the old panel). Each node is a card.
//   * Locked nodes are DIMMED and show their LockReason (the specific "why",
//     never a bare "LOCKED"). Affordable/unlockable nodes glow gold with a Spend
//     button; owned nodes read green.
//   * Prerequisite lines: a thin gilt connector is drawn from each node up to its
//     parent tier band (column-local) so the player reads the unlock path.
//   * An "Equip" button opens the loadout chooser (PanelRouter.Open(HeroLoadout)).
//
// Registers PanelId.HeroSkillTree (plain + the Skills inventory tab routes to it).
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
        private GameObject _contentRoot;        // the columns host (rebuilt on Render)
        private TMPro.TextMeshProUGUI _headerLabel;
        private TMPro.TextMeshProUGUI _walletText;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        // ── Registration (mirror BuildingUpgradePanelMvvm) ────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Skills", Close, () => IsOpen);
            PanelRouter.Register(PanelId.HeroSkillTree, Open);
            // RETIRE the UIDocument HeroTalentPanel: take over its legacy route id too, so the
            // Arcane Tower interactable + dialogue "OpenTalents" open THIS code-built panel
            // (the old panel renders empty in player builds — §8). Its bootstrap is gated off.
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

            Debug.Log("[HeroSkillTreePanelMvvm] Opened. Bound HeroSkillTreeVM (MVVM).");
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
                _walletText.text = "Wisdom " + _vm.RemainingWisdom + "    Skill Points " + _vm.RemainingSkillPoints;
            RebuildColumns();
        }

        private void RebuildColumns()
        {
            ClearContent();
            if (_contentRoot == null) return;

            var branches = _vm.Branches;
            int colCount = branches != null ? branches.Count : 0;
            if (colCount <= 0) return;

            // Group nodes by column, then bucket by tier within the column.
            // tierBuckets[col][tier(1..3)] = list of nodes.
            var byColumn = new List<List<SkillNodeVM>>[colCount];
            for (int c = 0; c < colCount; c++)
            {
                byColumn[c] = new List<List<SkillNodeVM>>();
                for (int t = 0; t < 3; t++) byColumn[c].Add(new List<SkillNodeVM>());
            }
            foreach (var n in _vm.Nodes)
            {
                if (n.Column < 0 || n.Column >= colCount) continue;
                int tIdx = Mathf.Clamp(n.Tier, 1, 3) - 1;
                byColumn[n.Column][tIdx].Add(n);
            }

            float colGap = 0.02f;
            float colW = (1f - colGap * (colCount - 1)) / colCount;
            for (int c = 0; c < colCount; c++)
            {
                float x0 = c * (colW + colGap);
                float x1 = x0 + colW;
                BuildColumn(branches[c], byColumn[c], x0, x1);
            }
        }

        // ── Chrome (presentation only; mirrors BuildingUpgradePanelMvvm) ──────────

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

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelWindowDark);
            var panel = panelGo.transform;

            Color fillColor = new Color(0.07f, 0.055f, 0.042f, 0.985f);
            if (DeNelle.Core.FeatureFlags.BlinkChrome) fillColor.a = 0f;
            var solidFill = ElarionUiKit.AddImage(panel, "SkillSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f), fillColor);
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            _headerLabel = ElarionUiKit.Header(panel, "Skills", x0: 0.04f, x1: 0.96f, y0: 0.91f, y1: 0.975f);

            // Wisdom + Skill-point readout under the header.
            var walletGo = new GameObject("Wallet", typeof(TMPro.TextMeshProUGUI));
            walletGo.transform.SetParent(panel, false);
            var wr = walletGo.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.04f, 0.84f); wr.anchorMax = new Vector2(0.96f, 0.90f);
            wr.offsetMin = Vector2.zero; wr.offsetMax = Vector2.zero;
            _walletText = walletGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_walletText);
            _walletText.fontSize = ElarionUi.FontLabel;
            _walletText.color = ElarionUi.Gilt;
            _walletText.alignment = TMPro.TextAlignmentOptions.Center;
            _walletText.raycastTarget = false;

            // Columns host (the three branch columns + tier rows).
            _contentRoot = new GameObject("Columns", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.04f, 0.15f); cr.anchorMax = new Vector2(0.96f, 0.83f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;

            // Equip button (opens the loadout chooser).
            var equipBtn = ElarionUiKit.ButtonPack(panel, "Equip Skills", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.34f, 0.075f), new Vector2(0.66f, 0.135f),
                OpenLoadout,
                packSpriteName: DeNelle.Core.FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold);
            var equipLbl = equipBtn != null ? equipBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (equipLbl != null)
            {
                equipLbl.color = ElarionUi.Parchment; equipLbl.fontStyle = TMPro.FontStyles.Bold;
                equipLbl.outlineColor = new Color32(20, 12, 4, 235); equipLbl.outlineWidth = 0.22f;
                equipLbl.transform.SetAsLastSibling();
            }

            // Close.
            var closeBtn = ElarionUiKit.ButtonPack(panel, "Close", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.06f, 0.04f), new Vector2(0.30f, 0.10f), () => { if (_vm != null) _vm.Close(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var closeLbl = closeBtn != null ? closeBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (closeLbl != null)
            {
                closeLbl.color = ElarionUi.Parchment; closeLbl.fontStyle = TMPro.FontStyles.Bold;
                closeLbl.transform.SetAsLastSibling();
            }
        }

        private void OpenLoadout()
        {
            // Open the loadout chooser; PanelManager swaps this panel out (one-at-a-time).
            if (!PanelRouter.Open(PanelId.HeroLoadout))
                Debug.LogWarning("[HeroSkillTreePanelMvvm] HeroLoadout panel not registered — Equip is a no-op.");
        }

        // ── Branch column: a captioned vertical stack of tier rows (top-down 3->1) ──

        private void BuildColumn(string branchName, List<List<SkillNodeVM>> tierBuckets, float x0, float x1)
        {
            var col = ElarionUiKit.Well(_contentRoot.transform, new Vector2(x0, 0f), new Vector2(x1, 1f));
            var colImg = col.GetComponent<Image>();
            if (colImg != null) colImg.raycastTarget = false;

            // Branch caption at the top.
            ElarionUiKit.Label(col.transform, branchName, 0.93f, 0.99f, ElarionUi.Gilt,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Three tier bands, tier 3 at the top (y high) down to tier 1 (y low).
            // Band y-ranges leave a top strip for the caption.
            float[] bandY0 = { 0.62f, 0.34f, 0.06f }; // index 0 = tier3 (top)
            float[] bandY1 = { 0.90f, 0.60f, 0.32f };

            for (int band = 0; band < 3; band++)
            {
                int tier = 3 - band;                 // band 0 -> tier 3
                var nodes = tierBuckets[tier - 1];
                float by0 = bandY0[band], by1 = bandY1[band];

                // Tier caption (small, left edge of the band).
                ElarionUiKit.Label(col.transform, "Tier " + tier, by1 - 0.04f, by1, ElarionUi.ParchmentDim,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.05f, 0.5f, bold: true);

                if (nodes == null || nodes.Count == 0) continue;

                float nodeGap = 0.04f;
                float nodeW = (0.92f - nodeGap * (nodes.Count - 1)) / nodes.Count;
                for (int i = 0; i < nodes.Count; i++)
                {
                    float nx0 = 0.04f + i * (nodeW + nodeGap);
                    float nx1 = nx0 + nodeW;
                    // Connector up to the band above (only when there IS a band above and a prereq).
                    if (band > 0 && nodes[i].Prereqs != null && nodes[i].Prereqs.Count > 0)
                        BuildPrereqLine(col.transform, (nx0 + nx1) * 0.5f, by1, bandY0[band - 1]);
                    BuildNodeCard(col.transform, nodes[i], nx0, nx1, by0, by1 - 0.05f);
                }
            }
        }

        // A thin gilt vertical connector from a node's top up into the tier band above —
        // a column-local "prerequisite line" so the unlock path reads at a glance.
        private void BuildPrereqLine(Transform col, float cx, float fromY, float toY)
        {
            var go = new GameObject("PrereqLine", typeof(Image));
            go.transform.SetParent(col, false);
            var r = go.GetComponent<RectTransform>();
            float w = 0.006f;
            r.anchorMin = new Vector2(cx - w, fromY);
            r.anchorMax = new Vector2(cx + w, toY);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f);
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();
        }

        // ── Node card (presentation; data from the bound SkillNodeVM) ─────────────

        private void BuildNodeCard(Transform col, SkillNodeVM node, float x0, float x1, float y0, float y1)
        {
            var card = new GameObject("Node_" + node.Id, typeof(Image), typeof(Button));
            card.transform.SetParent(col, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = card.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img);

            // Plate colour by state: owned green, unlockable gold, locked dim.
            Color plate;
            if (node.Owned) plate = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.22f);
            else if (node.CanUnlock) plate = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.20f);
            else plate = new Color(ElarionUiKit.Cell.r, ElarionUiKit.Cell.g, ElarionUiKit.Cell.b, 0.40f);
            img.color = plate;

            var btn = card.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            btn.interactable = node.CanUnlock;
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Unlock(id); });

            // Name.
            Color nameColor = node.Owned || node.CanUnlock ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
            ElarionUiKit.Label(card.transform, node.Name, 0.58f, 0.96f, nameColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            // Kind chip (Skill / Stat) — top-left micro.
            string kindChip = node.Kind == SkillNodeKind.Skill ? "SKILL" : "STAT";
            ElarionUiKit.Label(card.transform, kindChip, 0.80f, 0.97f,
                node.Kind == SkillNodeKind.Skill ? ElarionUi.Aether : ElarionUi.ParchmentDim,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.06f, 0.6f, bold: true);

            // Equipped chip (Skill nodes that are slotted in the loadout).
            if (node.IsEquipped)
                ElarionUiKit.Label(card.transform, "EQUIPPED", 0.80f, 0.97f, ElarionUi.Affordable,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Right, 0.4f, 0.94f, bold: true);

            // State / reason line + cost.
            string stateLine;
            Color stateColor;
            if (node.Owned)
            {
                stateLine = "Owned";
                stateColor = ElarionUi.Gilt;
            }
            else if (node.CanUnlock)
            {
                stateLine = node.WisdomCost + " Wisdom";
                stateColor = ElarionUi.Affordable;
            }
            else
            {
                // Show the LockReason (the specific "why"), not a bare LOCKED.
                stateLine = string.IsNullOrEmpty(node.LockReason) ? "Locked" : node.LockReason;
                stateColor = ElarionUi.Danger;
            }
            ElarionUiKit.Label(card.transform, stateLine, 0.06f, 0.50f, stateColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearContent()
        {
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            _walletText = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
