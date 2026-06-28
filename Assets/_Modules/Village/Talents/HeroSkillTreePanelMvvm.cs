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

        // v2 layout: a 5-slot × 4-tier grid (capstones on top row) + an 8-node Shared strip
        // along the bottom. Columns == talent slots (1..5); rows == tiers (4 at the top down
        // to 1). Prereq is same-slot vertical, so a column reads as one unlock path.
        private const int GridCols  = 5;
        private const int GridTiers = 4;
        private const float GridFloor = 0.30f;   // hero grid occupies y [GridFloor, 1]; shared strip below

        private void RebuildColumns()
        {
            ClearContent();
            if (_contentRoot == null || _vm == null) return;
            BuildHeroGrid();
            BuildSharedStrip();
        }

        // ── Hero grid: 5 slot columns × 4 tier rows (tier 4 capstones on top) ─────
        private void BuildHeroGrid()
        {
            float colGap = 0.018f;
            float colW = (1f - colGap * (GridCols - 1)) / GridCols;

            // Tier bands within [GridFloor, 1]: tier 4 (capstone) at top → tier 1 at bottom.
            float bandTop = 1f, bandBot = GridFloor;
            float bandH = (bandTop - bandBot) / GridTiers;
            float rowGap = 0.02f;

            foreach (var n in _vm.Nodes)
            {
                int col = n.Column;
                if (col < 0 || col >= GridCols) continue;
                int tier = Mathf.Clamp(n.Tier, 1, GridTiers);

                float x0 = col * (colW + colGap);
                float x1 = x0 + colW;

                // row index 0 == top == tier 4.
                int row = GridTiers - tier;
                float y1 = bandTop - row * bandH;
                float y0 = y1 - bandH + rowGap;

                // Same-slot vertical prereq connector (down to the tier below this one).
                if (tier > 1)
                    BuildPrereqLine((x0 + x1) * 0.5f, y0, y0 - rowGap);

                BuildNodeCard(_contentRoot.transform, n, x0, x1, y0, y1 - 0.012f);
            }

            // Tier labels down the far-left gutter (visual aid).
            for (int t = GridTiers; t >= 1; t--)
            {
                int row = GridTiers - t;
                float y1 = bandTop - row * bandH;
                string lbl = t == GridTiers ? "Capstone" : "Tier " + t;
                ElarionUiKit.Label(_contentRoot.transform, lbl, y1 - 0.03f, y1,
                    t == GridTiers ? ElarionUi.Gold : ElarionUi.ParchmentDim,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.0f, 0.12f, bold: true);
            }
        }

        // ── Shared Universal strip: 8 nodes in a row along the bottom band ─────────
        private void BuildSharedStrip()
        {
            var shared = _vm.Shared;
            if (shared == null || shared.Count == 0) return;

            ElarionUiKit.Label(_contentRoot.transform, "Universal — any class", 0.235f, 0.275f,
                ElarionUi.Gilt, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.0f, 1f, bold: true);

            int n = shared.Count;
            float gap = 0.012f;
            float w = (1f - gap * (n - 1)) / n;
            float y0 = 0.02f, y1 = 0.22f;
            for (int i = 0; i < n; i++)
            {
                float x0 = i * (w + gap);
                BuildNodeCard(_contentRoot.transform, shared[i], x0, x0 + w, y0, y1);
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

        // A thin gilt vertical connector from a node's bottom down to the tier below —
        // a same-slot "prerequisite line" so the vertical unlock path reads at a glance.
        // Parented directly to the content host (the v2 grid uses absolute cell anchors).
        private void BuildPrereqLine(float cx, float fromY, float toY)
        {
            var go = new GameObject("PrereqLine", typeof(Image));
            go.transform.SetParent(_contentRoot.transform, false);
            var r = go.GetComponent<RectTransform>();
            float w = 0.004f;
            r.anchorMin = new Vector2(cx - w, Mathf.Min(fromY, toY));
            r.anchorMax = new Vector2(cx + w, Mathf.Max(fromY, toY));
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.32f);
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();
        }

        // ── Node card (presentation; data from the bound SkillNodeVM) ─────────────

        private void BuildNodeCard(Transform parent, SkillNodeVM node, float x0, float x1, float y0, float y1)
        {
            var card = new GameObject("Node_" + node.Id, typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = card.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img);

            // Plate colour by state: owned green, unlockable gold, locked dim. Capstones
            // read with a richer gold wash so the ultimate row stands apart.
            Color plate;
            if (node.Owned) plate = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.24f);
            else if (node.CanUnlock) plate = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.22f);
            else plate = new Color(ElarionUiKit.Cell.r, ElarionUiKit.Cell.g, ElarionUiKit.Cell.b, 0.40f);
            if (node.IsCapstone && !node.Owned && !node.CanUnlock)
                plate = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.14f);
            img.color = plate;

            // Capstone frame: a distinct gilt border behind the card.
            if (node.IsCapstone)
            {
                var frame = ElarionUiKit.AddImage(card.transform, "CapstoneFrame",
                    new Vector2(-0.04f, -0.04f), new Vector2(1.04f, 1.04f),
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
                var fImg = frame.GetComponent<Image>();
                if (fImg != null) fImg.raycastTarget = false;
                frame.transform.SetAsFirstSibling();
            }

            var btn = card.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            btn.interactable = node.CanUnlock;
            string id = node.Id;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.Unlock(id); });

            // Icon (top portion of the card) — graceful when the sprite isn't sliced yet.
            var sprite = LoadIcon(node.IconPath);
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(card.transform, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.30f, 0.48f); ir.anchorMax = new Vector2(0.70f, 0.95f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = sprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                if (!node.Owned && !node.CanUnlock) iImg.color = new Color(1f, 1f, 1f, 0.45f); // dim locked
            }

            // Name (lower-middle band, leaving room for the icon above).
            Color nameColor = node.Owned || node.CanUnlock ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
            float nameY1 = sprite != null ? 0.48f : 0.92f;
            ElarionUiKit.Label(card.transform, node.Name, nameY1 - 0.18f, nameY1, nameColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Kind chip (Skill / Stat) — top-left micro.
            string kindChip = node.Kind == SkillNodeKind.Skill ? "SKILL" : "STAT";
            ElarionUiKit.Label(card.transform, kindChip, 0.86f, 0.99f,
                node.Kind == SkillNodeKind.Skill ? ElarionUi.Aether : ElarionUi.ParchmentDim,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.05f, 0.6f, bold: true);

            // Equipped chip (Skill nodes that are slotted in the loadout).
            if (node.IsEquipped)
                ElarionUiKit.Label(card.transform, "EQUIPPED", 0.86f, 0.99f, ElarionUi.Affordable,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Right, 0.4f, 0.95f, bold: true);

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
            ElarionUiKit.Label(card.transform, stateLine, 0.04f, 0.26f, stateColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
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
