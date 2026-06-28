// =============================================================================
// BuildingUpgradePanelMvvm — the building-upgrade VIEW (MVVM slice). A DUMB SKIN:
// it builds presentation (ElarionUiKit dark-glass + gold frame) and BINDS a
// BuildingUpgradeVM. ALL state/logic (family decide, tier ladder, affordability,
// execute) lives in the VM — the View never reads game state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// Code-built uGUI ONLY (no UXML — §8). MIRRORS ShopPanel exactly: BuildModalCanvas
// (sortingOrder 31000 + overrideSorting) + Scrim(onTapClose) + PanelFramed, a big
// main "Upgrade Building" ButtonPack(Gold), and a dynamic, layout-driven scroll
// grid of tier cards (VerticalLayoutGroup + ContentSizeFitter + per-row
// LayoutElement — the ShopPanel anti-collapse rendering mechanism).
//
// SHIPS BEHIND FeatureFlags.BuildingUpgradePanel (OFF). Its bootstrap only spawns it
// (and suppresses the legacy UIDocument BuildingUpgradePanel) when the flag is ON,
// so the two never double-register PanelId.BuildingUpgrade. Distinct GameObject
// name ("BuildingUpgradePanelMvvm") so it can't collide with the UIDocument panel.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    public sealed class BuildingUpgradePanelMvvm : MonoBehaviour, IPanelView
    {
        private static readonly Color CurrentTint = new Color(1.18f, 1.12f, 0.92f, 1f);

        private BuildingUpgradeVM _vm;
        private string _buildingId;

        private GameObject _ui;
        private GameObject _contentRoot;
        private RectTransform _scrollContent;
        private TMPro.TextMeshProUGUI _headerLabel;
        private TMPro.TextMeshProUGUI _walletText;
        private TMPro.TextMeshProUGUI _statusText;
        private TMPro.TextMeshProUGUI _mainBtnLabel;
        private Button _mainBtn;

        private PanelHandle _panelHandle;

        // Rows recorded per rebuild as (id, plate Image) so Render can hold the current tier.
        private readonly List<(string id, Image plate)> _rowPlates = new List<(string id, Image plate)>();

        public bool IsOpen => _ui != null;

        // ── Registration (mirror VillageCraftingPanel / the old UIDocument panel) ──

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Building Upgrade", Close, () => IsOpen);
            PanelRouter.Register(PanelId.BuildingUpgrade, OpenGeneric);
            PanelRouter.Register(PanelId.BuildingUpgrade, (System.Action<string>)Open);
        }

        private void OnDestroy()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.BuildingUpgrade, OpenGeneric);
            PanelRouter.Unregister(PanelId.BuildingUpgrade, (System.Action<string>)Open);
        }

        // PanelRouter plain (no-context) open — no focus building; pick the first city tier
        // building so a generic open still shows something useful.
        private void OpenGeneric() => Open(DefaultBuildingId());

        private static string DefaultBuildingId()
        {
            var all = DeNelle.Core.State.BuildingTierCatalog.All;
            if (all != null && all.Count > 0 && all[0] != null) return all[0].Id;
            return ResourceBuildingProgression.FarmId;
        }

        // ── Open: build chrome, construct + bind the VM ───────────────────────────

        public void Open(string buildingId)
        {
            Close();
            _buildingId = string.IsNullOrEmpty(buildingId) ? DefaultBuildingId() : buildingId;

            // Construct the VM FIRST so the chrome's title (and its drop-shadow) can be composed
            // ONCE from the live building name — single clean string, no stale "Upgrade Building"
            // shadow showing through the building-name title (the old overlap bug).
            _vm = new BuildingUpgradeVM(_buildingId, EconomyService.Instance, Close);

            BuildChrome();

            Bind(_vm);

            // Arbiter closes any other open panel first (DEF-212) + applies the battle-lock.
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                // Rejected (e.g. in battle) — NotifyOpened already invoked our Close.
                return;
            }

            Debug.Log($"[BuildingUpgradePanelMvvm] Opened for '{_buildingId}'. Bound BuildingUpgradeVM (MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as BuildingUpgradeVM;
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

            // Header text is composed ONCE in BuildChrome ("Upgrade: <Building>") so the gilt title and
            // its drop-shadow stay in sync — deliberately NOT re-texted here (re-texting only the gilt
            // label left the shadow reading the stale "Upgrade Building", the title-overlap bug).

            if (_walletText != null)
                _walletText.text = $"Wood: {_vm.Wood}   Food: {_vm.Food}   Iron: {_vm.Iron}   Crystals: {_vm.Crystals}";

            if (_statusText != null) _statusText.text = _vm.Status;

            if (_mainBtnLabel != null) _mainBtnLabel.text = _vm.MainButtonLabel;
            if (_mainBtn != null)
            {
                _mainBtn.interactable = _vm.MainButtonEnabled;
                ApplyMainButtonState(_vm.IsMaxed);
            }

            RebuildList();
        }

        private void RebuildList()
        {
            ClearContent();
            _rowPlates.Clear();

            var listRoot = BuildScrollContent(_vm.Upgrades.Count);
            foreach (var item in _vm.Upgrades)
                CreateRow(listRoot, item);
            FinalizeScroll();
        }

        // ── Main button state: FULLY INERT when maxed, live gold CTA otherwise ─────
        // Maxed (no upgrade left): kill the Selectable transition so there is NO hover/
        // selection highlight "circle", and dim the plate + label so it reads as a settled
        // "Maxed" chip — not a clickable CTA. Otherwise restore the standard gold-button
        // feedback (ColorTint); the merely-unaffordable case stays a live CTA that simply
        // greys via the disabled colour, so the upgrade path is never broken.
        private void ApplyMainButtonState(bool inert)
        {
            if (_mainBtn == null) return;
            var img = _mainBtn.targetGraphic as Image;
            if (inert)
            {
                _mainBtn.transition = Selectable.Transition.None;
                if (img != null) img.color = new Color(0.30f, 0.27f, 0.22f, 0.85f);   // dim, settled
                if (_mainBtnLabel != null) _mainBtnLabel.color = ElarionUi.ParchmentDim;
            }
            else
            {
                ElarionUiKit.StyleButtonColors(_mainBtn);   // restore ColorTint + colour block
                if (img != null) img.color = Color.white;   // gold pack sprite shows at full
                if (_mainBtnLabel != null) _mainBtnLabel.color = ElarionUi.Parchment;
            }
        }

        // ── Chrome (presentation only; mirrors ShopPanel.BuildChrome) ─────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("BuildingUpgradePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header + ONE Close.
            // Compose the title ONCE here ("Upgrade: <Building>") so the gilt title and its drop-shadow
            // carry the SAME text — Render no longer re-texts the header (that left the shadow stale).
            string titleText = "Upgrade: " + (_vm != null ? _vm.Title : "Building");
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, titleText,
                new Vector2(0.14f, 0.07f), new Vector2(0.86f, 0.93f), () => _vm?.Close(),
                headerX0: 0.04f, headerX1: 0.96f);
            var panel = chrome.content.transform;
            _headerLabel = chrome.title;

            // Wallet readout under the header.
            var walletGo = new GameObject("Wallet", typeof(TMPro.TextMeshProUGUI));
            walletGo.transform.SetParent(panel, false);
            var wr = walletGo.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.04f, 0.82f); wr.anchorMax = new Vector2(0.96f, 0.88f);
            wr.offsetMin = Vector2.zero; wr.offsetMax = Vector2.zero;
            _walletText = walletGo.GetComponent<TMPro.TextMeshProUGUI>();
            _walletText.fontSize = ElarionUi.FontLabel;
            _walletText.color = ElarionUi.Gilt;
            _walletText.alignment = TMPro.TextAlignmentOptions.Center;
            _walletText.raycastTarget = false;

            // Scroll list area (the tier grid).
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.04f, 0.20f); cr.anchorMax = new Vector2(0.96f, 0.80f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;

            // Big main "Upgrade Building" CTA.
            _mainBtn = ElarionUiKit.ButtonPack(panel, "Upgrade Building", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.30f, 0.085f), new Vector2(0.70f, 0.165f),
                () => _vm?.UpgradeNext(),
                packSpriteName: DeNelle.Core.FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold);
            _mainBtnLabel = _mainBtn != null ? _mainBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (_mainBtnLabel != null)
            {
                _mainBtnLabel.color = ElarionUi.Parchment; _mainBtnLabel.fontStyle = TMPro.FontStyles.Bold;
                _mainBtnLabel.outlineColor = new Color32(20, 12, 4, 235); _mainBtnLabel.outlineWidth = 0.22f;
                _mainBtnLabel.transform.SetAsLastSibling();
            }

            // Close is the SHARED top-right Obsidian Close button (WO-554) — no per-panel footer Close.

            // Status line (bottom band).
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.34f, 0.03f); sRect.anchorMax = new Vector2(0.94f, 0.075f);
            sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;
        }

        // ── Scroll list (the ShopPanel anti-collapse rendering mechanism) ─────────

        private const float RowHeightPx = 72f;
        private const float RowGapPx    = 4f;

        private Transform BuildScrollContent(int rowCount)
        {
            var well = ElarionUiKit.Well(_contentRoot.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null)
            {
                wImg.raycastTarget = false;
                if (DeNelle.Core.FeatureFlags.BlinkChrome) { var c = wImg.color; c.a = 0f; wImg.color = c; }
            }

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _scrollContent = cr;
            return content.transform;
        }

        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            Canvas.ForceUpdateCanvases();
            var contentArea = _contentRoot != null ? _contentRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        // ── Tier card row (presentation; data from the bound ItemVM) ──────────────

        private void CreateRow(Transform parent, ItemVM item)
        {
            var row = new GameObject("TierRow_" + item.Id, typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;

            var rowImg = row.GetComponent<Image>();
            DressRowPlate(rowImg);
            _rowPlates.Add((item.Id, rowImg));

            // Current-tier (Equipped flag) holds a brighter plate so the ladder reads at a glance.
            if (item.Equipped)
            {
                var c = rowImg.color;
                rowImg.color = new Color(c.r * CurrentTint.r, c.g * CurrentTint.g, c.b * CurrentTint.b, c.a);
            }

            var rowBtn = row.GetComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            ElarionUiKit.StyleButtonColors(rowBtn);
            rowBtn.interactable = !item.Locked && !item.Equipped;
            // Owned/locked rows are non-actionable -> drop the Selectable transition so they show no
            // hover/selection highlight either (consistent with the inert main button when maxed).
            if (!rowBtn.interactable) rowBtn.transition = Selectable.Transition.None;
            string id = item.Id;
            rowBtn.onClick.AddListener(() => _vm?.Select(id));

            // WO-432 — a research-perk row shows its icon at the LEFT (Resources/HudIcons/BuildingUpgrades/
            // <IconName>, the owner's <Building>_T1_<Perk> sprites); tier rows have none. When an icon is
            // present the name/cost shift right to make room. Missing icon = no icon (row still reads).
            float textX0 = 0.04f;
            if (item.IconRole == BuildingUpgradeVM.IconRolePerk && !string.IsNullOrEmpty(item.IconName))
            {
                var icon = Resources.Load<Sprite>("HudIcons/BuildingUpgrades/" + item.IconName);
                if (icon != null)
                {
                    var iconGo = new GameObject("PerkIcon", typeof(Image));
                    iconGo.transform.SetParent(row.transform, false);
                    var irt = iconGo.GetComponent<RectTransform>();
                    irt.anchorMin = new Vector2(0.02f, 0.12f); irt.anchorMax = new Vector2(0.14f, 0.88f);
                    irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                    var iImg = iconGo.GetComponent<Image>();
                    iImg.sprite = icon;
                    iImg.preserveAspect = true;
                    iImg.raycastTarget = false;
                    textX0 = 0.17f;   // name/cost start after the icon
                }
            }

            // Name (tier/perk label).
            ElarionUiKit.Label(row.transform, item.Name, 0.50f, 0.95f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, textX0, 0.74f, bold: true);

            // Cost / state line (from the VM — affordability colour mapped from item.Affordable).
            string costLine = _vm != null ? _vm.CostFor(item.Id) : "";
            Color costColor = item.Equipped ? ElarionUi.Gilt
                            : item.Locked ? ElarionUi.ParchmentDim
                            : (item.Affordable ? ElarionUi.Affordable : ElarionUi.Danger);
            ElarionUiKit.Label(row.transform, costLine, 0.06f, 0.50f, costColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, textX0, 0.74f);

            // State chip on the right.
            string chip = item.Equipped ? "OWNED" : item.Locked ? "LOCKED" : "NEXT";
            Color chipColor = item.Equipped ? ElarionUi.Gilt : item.Locked ? ElarionUi.ParchmentDim : ElarionUi.Affordable;
            ElarionUiKit.Label(row.transform, chip, 0.30f, 0.70f, chipColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Right, 0.76f, 0.96f, bold: true);
        }

        private static void DressRowPlate(Image rowImg)
        {
            if (rowImg == null) return;
            if (DeNelle.Core.FeatureFlags.BlinkChrome)
            {
                var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
                if (plate != null)
                {
                    rowImg.sprite = plate;
                    rowImg.type   = Image.Type.Sliced;
                    rowImg.color  = Color.white;
                    return;
                }
            }
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);
        }

        // ── Teardown (mirror ShopPanel) ──────────────────────────────────────────

        private void ClearContent()
        {
            _scrollContent = null;
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
            _vm?.Dispose();
            _vm = null;
            _walletText = null;
            _statusText = null;
            _mainBtnLabel = null;
            _mainBtn = null;
            _headerLabel = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _scrollContent = null;
            _rowPlates.Clear();
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
