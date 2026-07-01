// =============================================================================
// PartyShopPanelMvvm - the PARTY weapon/armor shop VIEW (docs/STORE_EQUIP_SPEC.md).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// A DUMB SKIN: builds presentation (ElarionUiKit dark-glass + gold frame, the SHARED
// kit) and BINDS a PartyShopVM. ALL state/logic (party filter, buy/sell/equip,
// affordability, deltas) lives in the VM - the View never reads game state.
//
// MIRRORS EquipmentPanel + ShopPanel exactly:
//   * BuildModalCanvas (sortingOrder 31000 + overrideSorting) + Scrim(onTapClose) + PanelFramed;
//   * TOP-LEFT a row of PARTY-MEMBER icon buttons (one per member, portrait/crest) - tap
//     selects -> vm.SelectMember -> Render re-filters; the selected member is highlighted;
//   * BUY / SELL tabs (both on the SAME screen - single-tap, no leaving to sell);
//   * a dynamic scroll grid of item rows, each: the REAL item image (iconPath sprite, glyph
//     fallback), name, price, the stat + delta line, affordability colour, EQUIPPED/OWNED
//     state, and ONE single-tap buy/equip/sell action (no duplicate bars);
//   * scrim / Close ? (touch - no Escape; hotkeys are gone).
//
// Code-built uGUI ONLY (no UXML - ?8). It builds its own Canvas on Open, so it needs no
// PanelSettings. Registered with PanelManager + PanelRouter (PanelId.PartyShop). SHIPS
// BEHIND FeatureFlags.PartyShop (OFF): the bootstrap only spawns when ON, and CmdOpenShop
// suppresses the legacy ShopPanel when ON, so the two never double-open.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;                 // URP: the runtime preview camera renders to its RT (mirror BuildPreviewModal)
using UnityEngine.AddressableAssets;                   // Blink "gear/" model resolve for the 3D preview
using UnityEngine.ResourceManagement.AsyncOperations;  // the addressable handle (released on swap/close)
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Hero
{
    [DisallowMultipleComponent]
    public sealed class PartyShopPanelMvvm : MonoBehaviour, IPanelView
    {
        private static readonly Color TabSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);
        private static readonly Color TabRestTint     = new Color(0.58f, 0.55f, 0.50f, 1f);
        private static readonly Color RowSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);

        private PartyShopVM _vm;
        private InventoryStore _store;
        private readonly List<GearLoadoutEquipTarget> _targetAdapters = new List<GearLoadoutEquipTarget>();

        private string _vendorContext;
        private string _displayName;

        private GameObject _ui;
        private GameObject _contentRoot;
        private GameObject _partyBar;
        private GameObject _tabBar;
        private GameObject _categoryBar;
        private GameObject _typeBar;
        private RectTransform _scrollContent;
        private TMPro.TextMeshProUGUI _headerLabel;
        private TMPro.TextMeshProUGUI _memberLabel;
        private TMPro.TextMeshProUGUI _walletText;
        private TMPro.TextMeshProUGUI _statusText;

        // -- Preview pane (WO-501 owner point 3) - a 3D render of the selected gear + stat-diff + price --
        private GameObject _previewRoot;        // the chrome well that holds the preview widgets
        private Image _previewBacking;          // dark slate frame BEHIND the icon/3D square (transparent-icon backing)
        private RawImage _previewImage;         // fed by the live RenderTexture (3D model)
        private TMPro.TextMeshProUGUI _previewGlyph;  // 2D fallback glyph drawn over the image when no model
        private Image _previewSprite;           // 2D fallback sprite (iconPath/catalog) when no model
        private TMPro.TextMeshProUGUI _previewName;
        private TMPro.TextMeshProUGUI _previewStats;   // flavour/desc line (rarity + class fit)
        private TMPro.TextMeshProUGUI _previewDelta;   // summary delta line (kept; per-stat block below)
        private RectTransform _previewSpecs;           // per-stat block: "Label Value (Delta)" rows (WO: weapons matter)
        private TMPro.TextMeshProUGUI _previewPrice;
        private TMPro.TextMeshProUGUI _previewEmpty;  // "Select an item to preview." empty state

        // The single Purchase/Sell toggle + Equip buttons (bottom action bar).
        private Button _buySellBtn;
        private TMPro.TextMeshProUGUI _buySellLabel;
        private Button _equipBtn;
        private TMPro.TextMeshProUGUI _equipLabel;

        // -- Preview icon BACKING card (tunable) -----------------------------------
        // A dark slate frame drawn BEHIND the preview icon/3D square so a transparent (or
        // baked-grey) icon sits on a neutral premium backing instead of the gold store panel.
        // The square anchors below MUST match the _previewImage/_previewSprite square in
        // BuildPreviewPane (kept in sync via these consts).
        private static readonly Vector2 PreviewSquareMin = new Vector2(0.20f, 0.42f);
        private static readonly Vector2 PreviewSquareMax = new Vector2(0.80f, 0.98f);
        // The backing extends a little PAST the icon square so it reads as a frame around it.
        private const float PreviewBackingPad = 0.03f;
        private static readonly Color PreviewBackingColor = new Color(0.05f, 0.05f, 0.06f, 0.92f);  // dark slate

        // -- The isolated runtime 3D-preview rig (the BuildPreviewModal "Offset Forge" pattern) --
        private const int RtSize = 384;
        private const int PreviewLayer = 31;
        private GameObject _rigRoot;
        private GameObject _rigVisual;
        private Camera _rigCam;
        private RenderTexture _rigRt;
        private readonly List<Material> _rigMaterials = new List<Material>();
        private AsyncOperationHandle<GameObject> _rigHandle;
        private bool _rigHandleOpen;
        private string _rigModelId;             // the id currently mounted (skip rebuild when unchanged)
        private int _rigGeneration;             // stale-async guard (a newer select supersedes an in-flight load)
        private float _rigYaw;                  // auto-spin angle

        private PanelHandle _panelHandle;

        // Rows recorded per rebuild as (id, plate, locked) so Render can hold the selected row + keep
        // locked (show-all-but-greyed) rows dimmed across re-dress passes.
        private readonly List<(string id, Image plate, bool locked)> _rowPlates =
            new List<(string id, Image plate, bool locked)>();

        // Dim factor for a locked (ineligible-for-this-member) row so it reads as non-purchasable.
        private const float LockedRowAlpha = 0.45f;

        public bool IsOpen => _ui != null;

        // -- Registration (mirror BuildingUpgradePanelMvvm) ------------------------

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Party Shop", Close, () => IsOpen);
            PanelRouter.Register(PanelId.PartyShop, OpenGeneric);
            PanelRouter.Register(PanelId.PartyShop, (System.Action<string>)OpenContext);
        }

        private void OnDestroy()
        {
            DisposeViewModel();
            TeardownRig();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.PartyShop, OpenGeneric);
            PanelRouter.Unregister(PanelId.PartyShop, (System.Action<string>)OpenContext);
        }

        private void OpenGeneric() => Open(null, null);
        private void OpenContext(string vendorContext) => Open(vendorContext, null);

        // -- Open: resolve party + store at the open-site, build chrome, bind VM ---

        public void Open(string vendorContext, string displayName)
        {
            Close();
            _vendorContext = vendorContext ?? "";
            _displayName = displayName;

            BuildChrome();
            ConstructViewModel();
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return;   // rejected (e.g. in battle) - NotifyOpened already invoked Close.

            Debug.Log($"[PartyShopPanelMvvm] Opened for vendor '{_vendorContext}'. Bound PartyShopVM (MVVM).");
        }

        // Resolve the live targets (hero + every companion body with a GearLoadout) + the owned
        // store, mirror EquipmentPanel.ConstructViewModel, then inject into the VM. Member levels
        // come from each wearer's HeroProgression (1 when absent).
        private void ConstructViewModel()
        {
            DisposeViewModel();

            var members = new List<IEquipTarget>();
            var levels = new List<int>();
            _targetAdapters.Clear();

            // The player hero first (the default selected member).
            var hero = GameObject.FindWithTag("Player");
            if (hero == null)
            {
                var loco = FindFirstObjectByType<HeroLocomotion>();
                if (loco != null) hero = loco.gameObject;
            }
            if (hero != null)
            {
                var hl = hero.GetComponent<GearLoadout>();
                if (hl == null) hl = hero.AddComponent<GearLoadout>();
                string hjob = ResolveHeroJob(hl);
                var adapter = new GearLoadoutEquipTarget(hl, HeroName(hjob), hjob);
                _targetAdapters.Add(adapter);
                members.Add(adapter);
                levels.Add(ResolveLevel(hero));
            }

            // Companions: each StoryCompanion body carries a GearLoadout bound to its class.
            foreach (var comp in FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var cl = comp.GetComponent<GearLoadout>();
                if (cl == null) continue;
                string cjob = comp.Hero.ToString().ToLowerInvariant();
                var adapter = new GearLoadoutEquipTarget(cl, comp.DisplayName, cjob);
                _targetAdapters.Add(adapter);
                members.Add(adapter);
                levels.Add(ResolveLevel(comp.gameObject));
            }

            // WO-578: build the store AFTER the members so OwnedWeapons/OwnedArmor UNION the auto-equipped
            // gear (what the Forge surfaces as owned) with VillageInventory — store/Forge/Preview agree.
            _store = new InventoryStore(VillageInventory.Instance, members);

            var economy = EconomyService.Instance;   // resolved at the open-site, injected into the pure VM
            _vm = new PartyShopVM(_vendorContext, economy, _store, members, levels, _displayName, onClose: Close);
        }

        private static int ResolveLevel(GameObject go)
        {
            if (go == null) return 1;
            var prog = go.GetComponent<HeroProgression>();
            return prog != null ? prog.Level : 1;
        }

        private static string ResolveHeroJob(GearLoadout loadout)
        {
            var ha = loadout != null ? loadout.GetComponent<HeroAbilities>() : null;
            string j = ha != null ? ha.HeroClass : null;
            return string.IsNullOrEmpty(j) ? AbilityCatalog.DefaultClass : j;
        }

        private static string HeroName(string job)
        {
            switch ((job ?? "").ToLowerInvariant())
            {
                case "knight": return "Grom";
                case "mage":   return "Thrain";
                case "ranger": return "Sylas";
                case "cleric": return "Elara";
                default:        return Cap(job);
            }
        }

        // -- IPanelView ------------------------------------------------------------

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as PartyShopVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // -- Render: repaint from vm.* ONLY ----------------------------------------

        private void Render()
        {
            if (_vm == null) return;

            if (_headerLabel != null) _headerLabel.text = _vm.Title;
            if (_memberLabel != null) _memberLabel.text = _vm.MemberLabel;
            if (_walletText != null) _walletText.text = $"Gold: {_vm.Coins}";
            if (_statusText != null) _statusText.text = _vm.Status;

            RebuildPartyBar();
            HighlightTab(_vm.Tab);
            UpdateCategoryBar();
            RebuildTypeBar();
            RebuildList();
            HighlightSelectedRow();
            RenderPreview();
            RenderActionBar();
        }

        // -- Chrome (presentation only) --------------------------------------------

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("PartyShopPanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header + ONE Close.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Gear Shop",
                new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f), () => _vm?.Close(),
                headerX0: 0.04f, headerX1: 0.96f, frameName: RpgUiCatalog.FrameMerchant);
            var panel = chrome.content.transform;
            _headerLabel = chrome.title;

            // WO-582 (Blink frame zone-fit): fit ALL content into the frame's BODY drop-zone (the
            // templated inner well) instead of floating over the whole panel rect — this stops the
            // wallet/party/tabs/list/preview/buttons from overlapping the frame's ornate border. Falls
            // back to the panel rect when no frame is used. Mirrors CraftingPanelMvvm.BuildChrome. The
            // shared Close (chrome.close) stays on the full panel (chrome, not content).
            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)panel;

            // Wallet readout (top-right band).
            var walletGo = new GameObject("Wallet", typeof(TMPro.TextMeshProUGUI));
            walletGo.transform.SetParent(bodyHost, false);
            var wr = walletGo.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.60f, 0.905f); wr.anchorMax = new Vector2(0.96f, 0.96f);
            wr.offsetMin = Vector2.zero; wr.offsetMax = Vector2.zero;
            _walletText = walletGo.GetComponent<TMPro.TextMeshProUGUI>();
            _walletText.fontSize = ElarionUi.FontLabel;
            _walletText.color = ElarionUi.Gilt;
            _walletText.alignment = TMPro.TextAlignmentOptions.Right;
            _walletText.raycastTarget = false;

            // TOP-LEFT party-member selector bar (spec point 1).
            _partyBar = new GameObject("PartyBar", typeof(RectTransform));
            _partyBar.transform.SetParent(bodyHost, false);
            var pb = _partyBar.GetComponent<RectTransform>();
            pb.anchorMin = new Vector2(0.04f, 0.80f); pb.anchorMax = new Vector2(0.96f, 0.885f);
            pb.offsetMin = Vector2.zero; pb.offsetMax = Vector2.zero;

            // Selected-member sub-header (name - class (Lv N)).
            var memGo = new GameObject("MemberLabel", typeof(TMPro.TextMeshProUGUI));
            memGo.transform.SetParent(bodyHost, false);
            var mr = memGo.GetComponent<RectTransform>();
            mr.anchorMin = new Vector2(0.04f, 0.755f); mr.anchorMax = new Vector2(0.66f, 0.80f);
            mr.offsetMin = Vector2.zero; mr.offsetMax = Vector2.zero;
            _memberLabel = memGo.GetComponent<TMPro.TextMeshProUGUI>();
            _memberLabel.fontSize = ElarionUi.FontBody;
            _memberLabel.color = ElarionUi.Parchment;
            _memberLabel.fontStyle = TMPro.FontStyles.Bold;
            _memberLabel.alignment = TMPro.TextAlignmentOptions.Left;
            _memberLabel.raycastTarget = false;

            // BUY / SELL tabs (both on the same screen - spec point 4).
            _tabBar = new GameObject("TabBar", typeof(RectTransform));
            _tabBar.transform.SetParent(bodyHost, false);
            var tb = _tabBar.GetComponent<RectTransform>();
            tb.anchorMin = new Vector2(0.66f, 0.755f); tb.anchorMax = new Vector2(0.96f, 0.80f);
            tb.offsetMin = Vector2.zero; tb.offsetMax = Vector2.zero;
            CreateTab("BUY",  new Vector2(0.02f, 0.49f), () => _vm?.SetTab(PartyShopTab.Buy));
            CreateTab("SELL", new Vector2(0.51f, 0.98f), () => _vm?.SetTab(PartyShopTab.Sell));

            // Category selector ("dropdown selections": All / Weapons / Armor) - the missing
            // narrow over the combined weapons+armor list. Pinned/hidden for single-kind vendors
            // (CategorySelectorVisible). Sits just under the tab/member band, above the grid.
            _categoryBar = new GameObject("CategoryBar", typeof(RectTransform));
            _categoryBar.transform.SetParent(bodyHost, false);
            var cb = _categoryBar.GetComponent<RectTransform>();
            cb.anchorMin = new Vector2(0.04f, 0.705f); cb.anchorMax = new Vector2(0.96f, 0.748f);
            cb.offsetMin = Vector2.zero; cb.offsetMax = Vector2.zero;
            CreateCategory("All",     new Vector2(0.01f, 0.32f),  PartyShopCategory.All);
            CreateCategory("Armor",   new Vector2(0.34f, 0.65f),  PartyShopCategory.Armor);
            CreateCategory("Weapons", new Vector2(0.67f, 0.99f),  PartyShopCategory.Weapons);

            // Finer weapon/armor TYPE chip row (WO-501 owner point 1) - sits just under the category
            // bar. Rebuilt per Render from _vm.AvailableTypes so it only shows live chips (>0 rows).
            _typeBar = new GameObject("TypeBar", typeof(RectTransform));
            _typeBar.transform.SetParent(bodyHost, false);
            var tyb = _typeBar.GetComponent<RectTransform>();
            tyb.anchorMin = new Vector2(0.04f, 0.655f); tyb.anchorMax = new Vector2(0.96f, 0.70f);
            tyb.offsetMin = Vector2.zero; tyb.offsetMax = Vector2.zero;

            // The scroll list area - SLIM name column (WO-501 owner point 2): narrowed to the left ~36%
            // so the 3D preview pane sits beside it. The VerticalLayoutGroup auto-fits the new width.
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(bodyHost, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.04f, 0.12f); cr.anchorMax = new Vector2(0.40f, 0.645f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;

            // The 3D render preview pane (WO-501 owner point 3) beside the slim list.
            BuildPreviewPane(bodyHost);

            // -- Bottom action bar (WO-501 owner point 4): Purchase/Sell toggle + Equip --
            // Close is the SHARED top-right Obsidian Close button (WO-554) — no per-panel footer Close.

            // ONE Purchase/Sell button whose label + action TOGGLE on _vm.Tab (the proven ShopPanel
            // pattern, ShopPanel.cs:341-344) - routes through _vm.Act on the selected id.
            _buySellBtn = ElarionUiKit.ButtonPack(bodyHost, "Purchase", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.30f, 0.03f), new Vector2(0.60f, 0.105f),
                () => { var s = _vm?.SelectedId; if (!string.IsNullOrEmpty(s)) _vm.Act(s); },
                packSpriteName: DeNelle.Core.FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : null);
            CreamTab(_buySellBtn);
            _buySellLabel = _buySellBtn != null ? _buySellBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;

            // EQUIP the selected owned item to the selected member (IEquipTarget seam via the VM).
            _equipBtn = ElarionUiKit.ButtonPack(bodyHost, "Equip", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.64f, 0.03f), new Vector2(0.86f, 0.105f),
                () => _vm?.EquipSelected(),
                packSpriteName: DeNelle.Core.FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : null);
            CreamTab(_equipBtn);
            _equipLabel = _equipBtn != null ? _equipBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;

            // Status line (narrow strip above the buttons so a row can never eat the tap).
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(bodyHost, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.04f, 0.115f); sRect.anchorMax = new Vector2(0.96f, 0.16f);
            sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;

            // Raise the action buttons above the scroll content so a row never eats the tap (ShopPanel trap).
            if (_buySellBtn != null) _buySellBtn.transform.SetAsLastSibling();
            if (_equipBtn != null) _equipBtn.transform.SetAsLastSibling();
            if (chrome.close != null) chrome.close.transform.SetAsLastSibling();
        }

        private void CreateTab(string label, Vector2 anchorX, System.Action onClick)
        {
            var btn = ElarionUiKit.ButtonPack(_tabBar.transform, label, ElarionUiKit.ButtonKind.Gold,
                new Vector2(anchorX.x, 0.05f), new Vector2(anchorX.y, 0.95f), onClick,
                packSpriteName: RpgUiCatalog.ButtonFrame);
            CreamTab(btn);
        }

        private void CreateCategory(string label, Vector2 anchorX, PartyShopCategory cat)
        {
            var btn = ElarionUiKit.ButtonPack(_categoryBar.transform, label, ElarionUiKit.ButtonKind.Quiet,
                new Vector2(anchorX.x, 0.08f), new Vector2(anchorX.y, 0.92f),
                () => _vm?.SetCategory(cat),
                packSpriteName: RpgUiCatalog.ButtonFrame);
            CreamTab(btn);
        }

        // -- Finer weapon/armor TYPE chip row (WO-501 owner point 1) ------------------
        // Rebuilt per Render from _vm.AvailableTypes so only chips with >0 candidate rows show
        // (never a dead chip) + an "All" chip to clear the narrow. Highlights the active chip.
        private void RebuildTypeBar()
        {
            if (_typeBar == null || _vm == null) return;
            for (int i = _typeBar.transform.childCount - 1; i >= 0; i--)
            {
                var c = _typeBar.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }

            var avail = _vm.AvailableTypes;
            // Only one TYPE => the narrow is meaningless (the hero's gear is already one kind): hide the row.
            if (avail == null || avail.Count <= 1) { _typeBar.SetActive(false); return; }
            _typeBar.SetActive(true);

            // "All" + one chip per available type, evenly spaced across the bar.
            var chips = new List<(string label, PartyShopType type)> { ("All", PartyShopType.Any) };
            foreach (var t in avail) chips.Add((TypeLabel(t), t));

            int n = chips.Count;
            const float gap = 0.01f;
            float w = (1f - gap * (n + 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var chip = chips[i];
                float x0 = gap + i * (w + gap);
                var btn = ElarionUiKit.ButtonPack(_typeBar.transform, chip.label, ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(x0, 0.08f), new Vector2(x0 + w, 0.92f),
                    () => _vm?.SetType(chip.type),
                    packSpriteName: RpgUiCatalog.ButtonFrame);
                if (btn == null) continue;
                btn.name = "Type_" + chip.type;
                CreamTab(btn);
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = _vm.Type == chip.type ? TabSelectedTint : TabRestTint;
            }
        }

        private static string TypeLabel(PartyShopType t)
        {
            switch (t)
            {
                case PartyShopType.OneHand: return "1h";
                case PartyShopType.TwoHand: return "2h";
                case PartyShopType.Shield:  return "Shield";
                case PartyShopType.Light:   return "Light";
                case PartyShopType.Heavy:   return "Heavy";
                default:                    return "All";
            }
        }

        // Show the category selector only for vendors that stock BOTH gear kinds (else it is
        // pinned to the single kind and the row is hidden), then highlight the active category.
        private void UpdateCategoryBar()
        {
            if (_categoryBar == null || _vm == null) return;
            bool show = _vm.CategorySelectorVisible;
            _categoryBar.SetActive(show);
            if (!show) return;

            string active = _vm.Category == PartyShopCategory.Weapons ? "Btn_Weapons"
                          : _vm.Category == PartyShopCategory.Armor   ? "Btn_Armor"
                          : "Btn_All";
            foreach (Transform child in _categoryBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        private void HighlightTab(PartyShopTab tab)
        {
            if (_tabBar == null) return;
            string active = tab == PartyShopTab.Buy ? "Btn_BUY" : "Btn_SELL";
            foreach (Transform child in _tabBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        // -- Party selector (top-left member icon buttons) -------------------------

        private void RebuildPartyBar()
        {
            if (_partyBar == null || _vm == null) return;
            for (int i = _partyBar.transform.childCount - 1; i >= 0; i--)
            {
                var c = _partyBar.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }

            var party = _vm.Party;
            int n = Mathf.Max(1, party.Count);
            const float gap = 0.012f;
            float w = (1f - gap * (n + 1)) / n;
            // Cap each member chip's width so a small party doesn't stretch portraits across the bar.
            float chipW = Mathf.Min(w, 0.16f);

            for (int i = 0; i < party.Count; i++)
            {
                int idx = i;
                var member = party[i];
                float x0 = gap + i * (chipW + gap);
                var btn = ElarionUiKit.ButtonPack(_partyBar.transform, "", ElarionUiKit.ButtonKind.Gold,
                    new Vector2(x0, 0.05f), new Vector2(x0 + chipW, 0.95f),
                    () => _vm?.SelectMember(idx), packSpriteName: RpgUiCatalog.ButtonFrame);
                if (btn == null) continue;
                btn.name = "Member_" + idx;

                // Portrait/crest glyph + class initial as the member token (real portrait sprite when present).
                var icon = ResolvePortrait(member.Class);
                if (icon != null)
                {
                    var imgGo = ElarionUiKit.AddImage(btn.transform, "Portrait",
                        new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.95f), Color.white, rounded: false);
                    var img = imgGo.GetComponent<Image>();
                    img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
                }
                else
                {
                    ElarionUiKit.Label(btn.transform, ClassCrest(member.Class), 0.40f, 0.98f, ElarionUi.Gilt,
                        ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.0f, 1f, bold: true);
                }
                // Member first name under the token.
                ElarionUiKit.Label(btn.transform, member.Name, 0.02f, 0.34f, ElarionUi.Parchment,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.0f, 1f, bold: member.Selected);

                var plate = btn.GetComponent<Image>();
                if (plate != null) plate.color = member.Selected ? TabSelectedTint : TabRestTint;
            }
        }

        // Resolve a portrait sprite for a class. No dedicated class-portrait sheet exists, so we
        // map to the pack's class glyph (sword/shield/etc) as the token; null -> the View draws
        // the ClassCrest glyph instead. Presentation only.
        private static Sprite ResolvePortrait(string cls)
        {
            switch ((cls ?? "").ToLowerInvariant())
            {
                case "knight": return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
                case "cleric": return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconHeart);
                case "ranger": return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCompass);
                case "mage":   return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconQuest);
                default:        return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconTalk);
            }
        }

        private static string ClassCrest(string job)
        {
            switch ((job ?? "").ToLowerInvariant())
            {
                case "knight": return "K";
                case "mage":   return "M";
                case "ranger": return "R";
                case "cleric": return "C";
                default:        return "*";
            }
        }

        // -- Item list -------------------------------------------------------------

        private void RebuildList()
        {
            using var _ = FlowTrace.Enter("Store", $"PartyShop.RebuildList tab={_vm.Tab}");
            ClearContent();
            _rowPlates.Clear();

            int wantCount = _vm.Items.Count;
            var listRoot = BuildScrollContent();

            // Guard EACH row so one bad ItemVM is logged + skipped, never aborting the whole list
            // (the "blank party-shop tab" class, WO-412/406).
            var (built, failed) = Guard.TryEach("Store", "build party-shop row", _vm.Items,
                item => CreateRow(listRoot, item));

            // STOCKED-N COMMIT SEAM: rows offered vs built - splits data-empty from built-but-broken.
            FlowTrace.Step("Store",
                $"PartyShop stocked {built} row(s) (wanted {wantCount}, failed {failed}).");

            // VERIFY rows>0: show a VISIBLE empty-state row instead of a blank panel.
            if (built == 0)
            {
                if (wantCount == 0)
                    FlowTrace.Warn("Store",
                        $"PartyShop has NO items for tab {_vm.Tab} - showing empty-state row (data-empty).");
                else
                    FlowTrace.Fail("Store",
                        $"PartyShop had {wantCount} item(s) but built 0 rows ({failed} failed) - showing empty-state row (built-but-broken).");
                CreateEmptyStateRow(listRoot, _vm.Tab == PartyShopTab.Sell ? "Nothing to sell." : "No wares in stock.");
            }

            FinalizeScroll();
        }

        // A single visible row carrying the empty-state copy - the never-blank fallback.
        private void CreateEmptyStateRow(Transform parent, string msg)
        {
            var go = new GameObject("EmptyStateRow", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = msg;
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
        }

        private void HighlightSelectedRow()
        {
            if (_vm == null) return;
            string sel = _vm.SelectedId;
            for (int i = 0; i < _rowPlates.Count; i++)
            {
                var plate = _rowPlates[i].plate;
                if (plate == null) continue;
                DressRowPlate(plate);
                if (_rowPlates[i].locked) DimPlate(plate);   // keep locked rows greyed after the re-dress
                if (sel != null && _rowPlates[i].id == sel)
                {
                    var c = plate.color;
                    plate.color = new Color(c.r * RowSelectedTint.r, c.g * RowSelectedTint.g, c.b * RowSelectedTint.b, c.a);
                }
            }
        }

        // Grey a locked (ineligible-for-this-member) row plate so it reads as non-purchasable.
        private static void DimPlate(Image plate)
        {
            if (plate == null) return;
            var c = plate.color;
            plate.color = new Color(c.r, c.g, c.b, c.a * LockedRowAlpha);
        }

        private const float RowHeightPx = 44f;   // WO-501: name-only rows are shorter
        private const float RowGapPx    = 4f;

        private Transform BuildScrollContent()
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
            cr.sizeDelta = Vector2.zero;

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

        // SLIM name-only row (WO-501 owner point 2): one plate (for the selected-row hold tint) + the
        // NAME spanning the row. All the detail (icon/stats/delta/price/action) moved to the preview
        // pane beside the list. Tapping the row inspects it -> _vm.Select(id) -> Render -> RenderPreview.
        private void CreateRow(Transform parent, ItemVM item)
        {
            bool isSell = _vm != null && _vm.Tab == PartyShopTab.Sell;
            bool locked = item.Locked;
            bool hasReason = locked && !string.IsNullOrEmpty(item.LockReason);

            var row = new GameObject((isSell ? "SellRow_" : "BuyRow_") + item.Id,
                typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;

            var rowImg = row.GetComponent<Image>();
            DressRowPlate(rowImg);
            if (locked) DimPlate(rowImg);   // greyed non-purchasable row (re-applied in HighlightSelectedRow)
            _rowPlates.Add((item.Id, rowImg, locked));

            var rowBtn = row.GetComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            ElarionUiKit.StyleButtonColors(rowBtn);
            string id = item.Id;
            // Tap the ROW = inspect (hold-select) -> the preview + action bar follow the selection.
            // Locked rows are still selectable so the player can preview stats + see the requirement.
            rowBtn.onClick.AddListener(() => _vm?.Select(id));

            // Name - locked rows dimmed; equipped names bold + gilt so the player sees what is worn.
            // A locked row shrinks the name column to make room for the right-aligned "requires" hint.
            Color nameColor = locked ? ElarionUi.ParchmentDim
                            : (item.Equipped ? ElarionUi.Gilt : ElarionUi.Parchment);
            float nameX1 = hasReason ? 0.56f : 0.94f;
            ElarionUiKit.Label(row.transform, item.Name,
                0.0f, 1f, nameColor,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.06f, nameX1,
                bold: item.Equipped && !locked);

            // Lock reason hint ("Requires Lv 5" / "Class: Ranger"), right-aligned on locked rows.
            if (hasReason)
                ElarionUiKit.Label(row.transform, item.LockReason,
                    0.0f, 1f, ElarionUi.Danger,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Right, 0.58f, 0.94f, bold: false);
        }

        // -- 3D RENDER PREVIEW pane (WO-501 owner point 3) ----------------------------
        // A well beside the slim list holding, top->bottom: the 3D render (RawImage), a 2D
        // sprite/glyph fallback in the same square, the name, the stat line, the colored delta,
        // and a LARGE price. Built once in BuildChrome; repainted per Render via RenderPreview.
        private void BuildPreviewPane(Transform panel)
        {
            _previewRoot = ElarionUiKit.Well(panel, new Vector2(0.42f, 0.17f), new Vector2(0.96f, 0.70f));
            var wImg = _previewRoot.GetComponent<Image>();
            if (wImg != null)
            {
                wImg.raycastTarget = false;
                if (DeNelle.Core.FeatureFlags.BlinkChrome) { var c = wImg.color; c.a = 0f; wImg.color = c; }
            }
            var pane = _previewRoot.transform;

            // FRAMED BACKING CARD (behind the icon/3D square): a dark slate plate so a transparent or
            // baked-grey icon sits on a neutral premium backing, never the gold store panel / a checker.
            // Padded slightly past the icon square so it reads as a frame; reuses the pack's slot plate
            // sprite (premium frame) when present, else a rounded dark slate fill.
            var backMin = new Vector2(PreviewSquareMin.x - PreviewBackingPad, PreviewSquareMin.y - PreviewBackingPad);
            var backMax = new Vector2(PreviewSquareMax.x + PreviewBackingPad, PreviewSquareMax.y + PreviewBackingPad);
            var backGo = ElarionUiKit.AddImage(pane, "PreviewBacking", backMin, backMax, PreviewBackingColor, rounded: true);
            _previewBacking = backGo.GetComponent<Image>();
            if (_previewBacking != null)
            {
                _previewBacking.raycastTarget = false;
                var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
                if (plate != null)
                {
                    _previewBacking.sprite = plate;
                    _previewBacking.type   = Image.Type.Sliced;
                    _previewBacking.color  = PreviewBackingColor;
                }
            }
            ElarionUiKit.AddInnerRim(backGo, new Color(0f, 0f, 0f, 0.40f));
            backGo.transform.SetAsFirstSibling();   // keep it BEHIND every preview widget

            // The 3D render image (square, top of the pane). Fed by the live RenderTexture in RenderPreview.
            var imgGo = new GameObject("PreviewImage", typeof(RawImage));
            imgGo.transform.SetParent(pane, false);
            var ir = imgGo.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.20f, 0.42f); ir.anchorMax = new Vector2(0.80f, 0.98f);
            ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
            _previewImage = imgGo.GetComponent<RawImage>();
            _previewImage.color = Color.white;
            _previewImage.raycastTarget = false;

            // 2D fallback sprite in the SAME square (shown when no 3D model resolves).
            var spriteGo = ElarionUiKit.AddImage(pane, "PreviewSprite",
                new Vector2(0.20f, 0.42f), new Vector2(0.80f, 0.98f), new Color(0f, 0f, 0f, 0f), rounded: false);
            _previewSprite = spriteGo.GetComponent<Image>();
            _previewSprite.raycastTarget = false;
            _previewSprite.preserveAspect = true;

            // 2D emoji/glyph fallback over the square (last-resort never-blank).
            _previewGlyph = ElarionUiKit.Label(pane, "", 0.42f, 0.98f, ElarionUi.Parchment,
                ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);

            // Empty state (nothing selected).
            _previewEmpty = ElarionUiKit.Label(pane, "Select an item to preview.", 0.50f, 0.62f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);

            // Name (gilt bold).
            _previewName = ElarionUiKit.Label(pane, "", 0.355f, 0.41f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Flavour line (rarity + class fit) - the readable desc under the name.
            _previewStats = ElarionUiKit.Label(pane, "", 0.325f, 0.355f, ElarionUi.ParchmentDim,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);

            // -- Per-stat SPEC block (WO: "make weapons matter") - one line per spec, top->bottom:
            // "<Label>   <Value> (<Delta>)" with the delta tinted green(up)/red(down)/dim(same). Built
            // here as a vertical container; rebuilt per Render from _vm.SelectedSpecs. Sits between the
            // flavour line and the price. --
            var specsGo = new GameObject("PreviewSpecs", typeof(RectTransform));
            specsGo.transform.SetParent(pane, false);
            _previewSpecs = specsGo.GetComponent<RectTransform>();
            _previewSpecs.anchorMin = new Vector2(0.06f, 0.185f); _previewSpecs.anchorMax = new Vector2(0.94f, 0.32f);
            _previewSpecs.offsetMin = Vector2.zero; _previewSpecs.offsetMax = Vector2.zero;
            var specsVlg = specsGo.AddComponent<VerticalLayoutGroup>();
            specsVlg.childAlignment = TextAnchor.UpperCenter;
            specsVlg.spacing = 2f;
            specsVlg.childControlWidth = true;
            specsVlg.childControlHeight = true;
            specsVlg.childForceExpandWidth = true;
            specsVlg.childForceExpandHeight = false;

            // Summary delta line is folded into the per-stat block now; keep the field null so the
            // legacy single-line path is inert (no double-rendering of the delta).
            _previewDelta = null;

            // PRICE - large + readable (WO-501 owner point 3).
            _previewPrice = ElarionUiKit.Label(pane, "", 0.04f, 0.18f, ElarionUi.Gilt,
                ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
        }

        // Repaint the preview pane from _vm.Selected ONLY (name/stats/delta/price) + rebuild the 3D
        // model (or 2D fallback) for the selected id. Never-blank: 3D model -> 2D sprite -> emoji glyph.
        private void RenderPreview()
        {
            if (_vm == null || _previewRoot == null) return;
            var sel = _vm.Selected;

            if (!sel.HasValue)
            {
                // Nothing selected - clear + show the empty state, tear the rig down.
                TeardownRig();
                if (_previewBacking != null) _previewBacking.enabled = false;   // no card behind an empty preview
                if (_previewImage != null) _previewImage.enabled = false;
                if (_previewSprite != null) _previewSprite.color = new Color(0f, 0f, 0f, 0f);
                if (_previewGlyph != null) _previewGlyph.text = "";
                if (_previewName != null) _previewName.text = "";
                if (_previewStats != null) _previewStats.text = "";
                if (_previewDelta != null) _previewDelta.text = "";
                ClearSpecs();
                if (_previewPrice != null) _previewPrice.text = "";
                if (_previewEmpty != null) _previewEmpty.gameObject.SetActive(true);
                return;
            }

            if (_previewEmpty != null) _previewEmpty.gameObject.SetActive(false);
            if (_previewBacking != null) _previewBacking.enabled = true;   // framed card behind the icon/3D square
            var d = sel.Value;
            var item = _vm.SelectedItem;

            if (_previewName != null) _previewName.text = item.HasValue ? item.Value.Name : "";
            // Flavour/desc line (rarity + class fit) under the name.
            if (_previewStats != null) _previewStats.text = d.Description ?? "";
            // Legacy single delta line kept inert (folded into the per-stat block).
            if (_previewDelta != null)
            {
                _previewDelta.text = d.Delta ?? "";
                _previewDelta.color = DeltaColor(d.Delta);
            }
            // Per-stat SPEC block: "Label  Value (Delta)" with the delta tinted by sign.
            RebuildSpecs(_vm.SelectedSpecs);
            if (_previewPrice != null)
            {
                _previewPrice.text = _vm.SelectedPriceText;
                // Affordability-colored on BUY; gilt for refund/owned.
                bool sell = _vm.Tab == PartyShopTab.Sell;
                bool affordable = item.HasValue && (item.Value.Price <= 0 || item.Value.Affordable);
                _previewPrice.color = sell ? ElarionUi.Affordable
                                    : (item.HasValue && !affordable ? ElarionUi.Danger : ElarionUi.Gilt);
            }

            BuildPreviewModelOrFallback(_vm.SelectedId, d, item);
        }

        // -- Per-stat SPEC block (WO: "make weapons matter") --------------------------
        // Render one label per spec: "<Label>   <Value> (<Delta>)" with the (Delta) tinted green for an
        // upgrade, red for a downgrade, dim for same - via TMP rich-text so the colored delta sits inline.
        // A spec with no delta (nothing comparable equipped / SELL tab) shows just "<Label>   <Value>".
        private void RebuildSpecs(System.Collections.Generic.IReadOnlyList<PartyShopSpec> specs)
        {
            ClearSpecs();
            if (_previewSpecs == null || specs == null) return;

            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                string line = "<b>" + Esc(s.Label) + "</b>   " + Esc(s.Value);
                if (!string.IsNullOrEmpty(s.Delta))
                {
                    string hex = ColorUtility.ToHtmlStringRGB(DeltaSignColor(s.DeltaSign));
                    line += "  <color=#" + hex + ">(" + Esc(s.Delta) + ")</color>";
                }

                var go = new GameObject("Spec_" + (string.IsNullOrEmpty(s.Label) ? i.ToString() : s.Label),
                    typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
                go.transform.SetParent(_previewSpecs, false);
                var le = go.GetComponent<LayoutElement>();
                le.preferredHeight = 20f;
                le.minHeight = 16f;
                var t = go.GetComponent<TMPro.TextMeshProUGUI>();
                t.richText = true;
                t.text = line;
                t.fontSize = ElarionUi.FontLabel;
                t.color = ElarionUi.Parchment;
                t.alignment = TMPro.TextAlignmentOptions.Center;
                t.raycastTarget = false;
            }
        }

        private void ClearSpecs()
        {
            if (_previewSpecs == null) return;
            for (int i = _previewSpecs.childCount - 1; i >= 0; i--)
            {
                var c = _previewSpecs.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        // Green (better) / red (worse) / dim (same) for a per-stat delta sign.
        private static Color DeltaSignColor(int sign) =>
            sign > 0 ? ElarionUi.Affordable : (sign < 0 ? ElarionUi.Danger : ElarionUi.ParchmentDim);

        // Strip TMP rich-text control chars from VM-supplied text so a stray '<' can't break the markup.
        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("<", "[").Replace(">", "]");

        // Resolve the selected gear MODEL into the rig, mirroring the equip load path (DRY the heuristic
        // with EquipmentController.LoadsViaAddressable): addressable "gear/" -> Addressables; else Resources
        // via VisualFactory.Skin (the BuildPreviewModal pattern). Falls back to the 2D sprite, then the
        // emoji glyph - never blanks. Logs the chosen branch for headless capture (?12).
        private void BuildPreviewModelOrFallback(string id, PartyShopDetail detail, ItemVM? item)
        {
            if (string.IsNullOrEmpty(id)) { ShowSpriteFallback(detail, item, "no-id"); return; }
            if (_rigModelId == id && (_rigVisual != null || _rigHandleOpen)) return;   // already mounted

            // Resolve the def for prefabPath via the same key the rows use (role-keyed catalog find).
            bool isArmor = detail.IconRole == PartyShopVM.IconRoleArmor;
            string prefabPath = null;
            bool addressable = false;
            if (isArmor)
            {
                var a = GearCatalog.FindArmor(id);
                prefabPath = a?.prefabPath;
                addressable = ArmorLoadsViaAddressable(a);
            }
            else
            {
                var w = GearCatalog.FindWeapon(id);
                prefabPath = w?.prefabPath;
                addressable = WeaponLoadsViaAddressable(w);
            }

            if (string.IsNullOrEmpty(prefabPath))
            {
                // Most gear today has no prefabPath (GearCatalog "NULL for now") - degrade to 2D.
                ShowSpriteFallback(detail, item, "no-prefab");
                return;
            }

            EnsureRig();
            ClearRigVisual();
            _rigModelId = id;

            if (addressable)
            {
                FlowTrace.Step("Store", $"preview model id={id} branch=addressable path={prefabPath}");
                BeginAddressablePreview(prefabPath, id);
            }
            else
            {
                FlowTrace.Step("Store", $"preview model id={id} branch=resources path={prefabPath}");
                GameObject skinned = null;
                Guard.Try("Store", $"skin preview model '{prefabPath}'",
                    () => skinned = VisualFactory.Skin(_rigVisual.transform, prefabPath, SkinOptions.Prop(2.5f)));
                if (skinned == null)
                {
                    ShowSpriteFallback(detail, item, "resources-miss");
                    return;
                }
                ShowModel();
            }
        }

        // Mirror of EquipmentController.LoadsViaAddressable (which is private - replicated, NOT forked;
        // see report). Addressable when loadVia=="addressable" or prefabPath starts "gear/".
        private static bool WeaponLoadsViaAddressable(WeaponDef def)
        {
            if (def == null) return false;
            // WO-536: Blink gear was JUNKED in the 2026-06-22 pivot (ff.blinkarmor OFF) and its
            // addressables no longer resolve -> the load throws + spams [Flow:Store] every preview.
            // Route junked Blink ids to the sprite fallback. Flag-safe: re-enabling ff.blinkarmor
            // restores the addressable path.
            if (!DeNelle.Core.FeatureFlags.BlinkArmor && def.id != null &&
                def.id.StartsWith("blink_", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(def.loadVia) &&
                def.loadVia.Equals("addressable", System.StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrEmpty(def.prefabPath) &&
                   def.prefabPath.StartsWith("gear/", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ArmorLoadsViaAddressable(ArmorDef def)
        {
            if (def == null) return false;
            // WO-536: junked-Blink armor (ff.blinkarmor OFF) -> sprite fallback, no dead addressable load.
            if (!DeNelle.Core.FeatureFlags.BlinkArmor && def.id != null &&
                def.id.StartsWith("blink_", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(def.loadVia) &&
                def.loadVia.Equals("addressable", System.StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrEmpty(def.prefabPath) &&
                   def.prefabPath.StartsWith("gear/", System.StringComparison.OrdinalIgnoreCase);
        }

        // Async Addressables load (mirror BeginAddressableEquip): guarded, stale-checked via _rigGeneration,
        // released on swap/close. On any miss, fall back to the 2D sprite so the preview never blanks.
        private void BeginAddressablePreview(string address, string id)
        {
            int generation = ++_rigGeneration;
            AsyncOperationHandle<GameObject> handle;
            try { handle = Addressables.LoadAssetAsync<GameObject>(address); }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("Store", $"preview addressable load threw for '{address}': {ex.Message} - 2D fallback.");
                ShowSpriteFallback(_vm != null ? _vm.Selected ?? default : default,
                                   _vm != null ? _vm.SelectedItem : null, "addr-threw");
                return;
            }
            _rigHandle = handle;
            _rigHandleOpen = true;
            handle.Completed += op =>
            {
                if (generation != _rigGeneration) return;   // a newer select superseded this load
                if (!op.IsValid() || op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    FlowTrace.Fail("Store", $"preview addressable FAILED for '{address}' (status={op.Status}) - 2D fallback.");
                    ShowSpriteFallback(_vm != null ? _vm.Selected ?? default : default,
                                       _vm != null ? _vm.SelectedItem : null, "addr-miss");
                    return;
                }
                if (_rigVisual == null) return;   // panel closed mid-flight
                GameObject skinned = null;
                Guard.Try("Store", $"skin addressable preview '{address}'",
                    () => skinned = VisualFactory.Skin(_rigVisual.transform, op.Result, SkinOptions.Prop(2.5f)));
                if (skinned == null)
                {
                    ShowSpriteFallback(_vm != null ? _vm.Selected ?? default : default,
                                       _vm != null ? _vm.SelectedItem : null, "addr-skin-null");
                    return;
                }
                ShowModel();
            };
        }

        // Show the 3D render (hide the 2D fallback), framing the camera on the rig.
        private void ShowModel()
        {
            FrameRig();
            if (_previewImage != null) { _previewImage.enabled = true; _previewImage.texture = _rigRt; }
            if (_previewSprite != null) _previewSprite.color = new Color(0f, 0f, 0f, 0f);
            if (_previewGlyph != null) _previewGlyph.text = "";
        }

        // Hide the 3D render, draw the 2D sprite (else the emoji glyph). Never blanks.
        private void ShowSpriteFallback(PartyShopDetail detail, ItemVM? item, string why)
        {
            FlowTrace.Step("Store", $"preview model id={_vm?.SelectedId} branch=sprite ({why})");
            TeardownRig();
            if (_previewImage != null) _previewImage.enabled = false;

            var sprite = ResolveItemSprite(detail, item ?? default);
            if (sprite != null && _previewSprite != null)
            {
                _previewSprite.sprite = sprite;
                _previewSprite.color = Color.white;
                if (_previewGlyph != null) _previewGlyph.text = "";
            }
            else
            {
                if (_previewSprite != null) _previewSprite.color = new Color(0f, 0f, 0f, 0f);
                if (_previewGlyph != null)
                    _previewGlyph.text = detail.IconRole == PartyShopVM.IconRoleArmor ? "[]" : "/";
            }
        }

        // -- The isolated runtime preview rig (BuildPreviewModal pattern) -------------
        private void EnsureRig()
        {
            if (_rigRt == null)
            {
                _rigRt = new RenderTexture(RtSize, RtSize, 16, RenderTextureFormat.ARGB32);
                _rigRt.Create();
            }
            if (_rigRoot != null) return;

            _rigRoot = new GameObject("PartyShopPreviewRoot");
            _rigRoot.transform.position = new Vector3(0f, -5000f, 0f);

            var light1 = new GameObject("PreviewLight1").AddComponent<Light>();
            light1.transform.SetParent(_rigRoot.transform, false);
            light1.transform.localPosition = new Vector3(2, 3, -2);
            light1.type = LightType.Directional;
            light1.color = new Color(0.9f, 0.9f, 0.95f);
            light1.intensity = 0.9f;
            light1.cullingMask = 1 << PreviewLayer;

            var light2 = new GameObject("PreviewLight2").AddComponent<Light>();
            light2.transform.SetParent(_rigRoot.transform, false);
            light2.transform.localPosition = new Vector3(-2, 2, 2);
            light2.type = LightType.Directional;
            light2.color = new Color(0.6f, 0.65f, 0.7f);
            light2.intensity = 0.5f;
            light2.cullingMask = 1 << PreviewLayer;

            _rigVisual = new GameObject("PreviewVisual");
            _rigVisual.transform.SetParent(_rigRoot.transform, false);
            _rigVisual.transform.localPosition = Vector3.zero;

            var camGo = new GameObject("PreviewCam");
            camGo.transform.SetParent(_rigRoot.transform, false);
            _rigCam = camGo.AddComponent<Camera>();
            _rigCam.clearFlags = CameraClearFlags.SolidColor;
            _rigCam.backgroundColor = new Color(0.10f, 0.09f, 0.08f, 0f);
            _rigCam.orthographic = true;
            _rigCam.nearClipPlane = 0.1f;
            _rigCam.farClipPlane = 10000f;
            _rigCam.targetTexture = _rigRt;
            _rigCam.cullingMask = 1 << PreviewLayer;

            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = CameraRenderType.Base;
            urp.renderPostProcessing = false;
            urp.requiresColorOption = CameraOverrideOption.Off;
            urp.requiresDepthOption = CameraOverrideOption.Off;

            SetLayerRecursive(_rigRoot.transform, PreviewLayer);
        }

        // Auto-spin the mounted model for a "wow factor" viewer (optional per WO-501). Cheap; only
        // runs while a model is mounted. The owner can swap this for drag-to-rotate in finesse.
        private void Update()
        {
            if (_rigVisual == null || _rigVisual.transform.childCount == 0) return;
            _rigYaw = Mathf.Repeat(_rigYaw + Time.deltaTime * 35f, 360f);
            _rigVisual.transform.localRotation = Quaternion.Euler(0f, _rigYaw, 0f);
        }

        private void ClearRigVisual()
        {
            if (_rigVisual == null) return;
            for (int i = _rigVisual.transform.childCount - 1; i >= 0; i--)
            {
                var c = _rigVisual.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        // Frame the ortho camera on the rig bounds from a 3/4 angle (mirror FrameCameraOnRig).
        private void FrameRig()
        {
            if (_rigRoot == null || _rigCam == null) return;
            SetLayerRecursive(_rigRoot.transform, PreviewLayer);
            Bounds b;
            var rends = _rigVisual != null ? _rigVisual.GetComponentsInChildren<Renderer>() : null;
            if (rends != null && rends.Length > 0)
            {
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            }
            else
            {
                b = new Bounds(_rigRoot.transform.position, Vector3.one * 2f);
            }
            float radius = Mathf.Max(0.5f, b.extents.magnitude);
            Vector3 dir = new Vector3(1f, 0.9f, -1f).normalized;
            _rigCam.transform.position = b.center + dir * (radius * 2.5f);
            _rigCam.transform.LookAt(b.center);
            _rigCam.orthographicSize = radius * 1.15f;
        }

        private void ReleaseRigHandle()
        {
            if (_rigHandleOpen && _rigHandle.IsValid())
            {
                Addressables.Release(_rigHandle);
            }
            _rigHandle = default;
            _rigHandleOpen = false;
        }

        private void TeardownRig()
        {
            _rigGeneration++;   // invalidate any in-flight addressable load
            ReleaseRigHandle();
            if (_rigRoot != null) { Destroy(_rigRoot); _rigRoot = null; }
            _rigVisual = null;
            _rigCam = null;
            if (_rigRt != null) { _rigRt.Release(); Destroy(_rigRt); _rigRt = null; }
            for (int i = 0; i < _rigMaterials.Count; i++)
                if (_rigMaterials[i] != null) Destroy(_rigMaterials[i]);
            _rigMaterials.Clear();
            _rigModelId = null;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        // -- Bottom action bar state (WO-501 owner point 4) ---------------------------
        private void RenderActionBar()
        {
            if (_vm == null) return;
            bool sell = _vm.Tab == PartyShopTab.Sell;
            var item = _vm.SelectedItem;
            bool hasSel = item.HasValue;

            bool locked = hasSel && item.Value.Locked;

            // Purchase/Sell toggle label + enable.
            if (_buySellLabel != null)
            {
                string price = _vm.SelectedPriceText;
                if (sell) _buySellLabel.text = hasSel ? "Sell  " + price : "Sell";
                else if (locked)
                    _buySellLabel.text = string.IsNullOrEmpty(item.Value.LockReason)
                        ? "Locked" : item.Value.LockReason;   // e.g. "Requires Lv 5" / "Class: Ranger"
                else _buySellLabel.text = hasSel
                        ? (item.Value.Equipped || item.Value.Price <= 0 ? "Owned" : "Purchase  " + price)
                        : "Purchase";
            }
            if (_buySellBtn != null)
            {
                // Locked items can never be purchased (wrong class / above level) - button disabled.
                bool canBuy = hasSel && !locked && (sell
                    ? true
                    : !(item.Value.Equipped) && (item.Value.Price <= 0 || item.Value.Affordable));
                _buySellBtn.interactable = canBuy;
            }

            // Equip: only for an OWNED, not-yet-equipped, UNLOCKED item on the BUY tab.
            if (_equipBtn != null)
            {
                bool canEquip = hasSel && !sell && !locked && (item.Value.Price <= 0) && !item.Value.Equipped;
                _equipBtn.interactable = canEquip;
            }
        }

        // Real item sprite from the VM detail: prefer iconPath (the rendered item image), else the
        // ItemIconCatalog art for the def, else the pack glyph, else null (the View draws a glyph).
        private static Sprite ResolveItemSprite(PartyShopDetail? detail, ItemVM item)
        {
            string iconPath = detail.HasValue ? detail.Value.IconPath : null;
            if (!string.IsNullOrEmpty(iconPath))
            {
                var s = Resources.Load<Sprite>(iconPath);
                if (s != null) return s;
            }
            // Catalog art by def (sprite-first, the same source the legacy details pane used).
            string role = detail.HasValue ? detail.Value.IconRole : item.IconRole;
            if (role == PartyShopVM.IconRoleArmor)
            {
                var a = GearCatalog.FindArmor(item.Id);
                var s = ItemIconCatalog.ForArmor(a);
                return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
            }
            else
            {
                var w = GearCatalog.FindWeapon(item.Id);
                var s = ItemIconCatalog.ForWeapon(w);
                return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
            }
        }

        private static Color DeltaColor(string delta)
        {
            if (string.IsNullOrEmpty(delta)) return ElarionUi.ParchmentDim;
            if (delta.StartsWith("+")) return ElarionUi.Affordable;
            if (delta.StartsWith("=")) return ElarionUi.ParchmentDim;
            return ElarionUi.Danger;   // a negative delta (worse than equipped)
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

        private static void CreamTab(Button btn)
        {
            if (btn == null) return;
            var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (lbl == null) return;
            lbl.color = ElarionUi.Parchment;
            lbl.fontStyle = TMPro.FontStyles.Bold;
            lbl.outlineColor = new Color32(20, 12, 4, 235);
            lbl.outlineWidth = 0.22f;
            lbl.transform.SetAsLastSibling();
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // -- Teardown ------------------------------------------------------------

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

        private void DisposeViewModel()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _store?.Dispose();
            _store = null;
            foreach (var a in _targetAdapters) a?.Dispose();
            _targetAdapters.Clear();
        }

        private void Close()
        {
            DisposeViewModel();
            TeardownRig();
            _walletText = null;
            _statusText = null;
            _headerLabel = null;
            _memberLabel = null;
            _previewRoot = null;
            _previewBacking = null;
            _previewImage = null;
            _previewSprite = null;
            _previewGlyph = null;
            _previewName = null;
            _previewStats = null;
            _previewDelta = null;
            _previewSpecs = null;
            _previewPrice = null;
            _previewEmpty = null;
            _buySellBtn = null;
            _buySellLabel = null;
            _equipBtn = null;
            _equipLabel = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _partyBar = null;
            _tabBar = null;
            _categoryBar = null;
            _typeBar = null;
            _scrollContent = null;
            _rowPlates.Clear();
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
