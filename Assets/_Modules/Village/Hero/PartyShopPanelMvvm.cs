// =============================================================================
// PartyShopPanelMvvm — the PARTY weapon/armor shop VIEW (docs/STORE_EQUIP_SPEC.md).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// A DUMB SKIN: builds presentation (ElarionUiKit dark-glass + gold frame, the SHARED
// kit) and BINDS a PartyShopVM. ALL state/logic (party filter, buy/sell/equip,
// affordability, deltas) lives in the VM — the View never reads game state.
//
// MIRRORS EquipmentPanel + ShopPanel exactly:
//   • BuildModalCanvas (sortingOrder 31000 + overrideSorting) + Scrim(onTapClose) + PanelFramed;
//   • TOP-LEFT a row of PARTY-MEMBER icon buttons (one per member, portrait/crest) — tap
//     selects → vm.SelectMember → Render re-filters; the selected member is highlighted;
//   • BUY / SELL tabs (both on the SAME screen — single-tap, no leaving to sell);
//   • a dynamic scroll grid of item rows, each: the REAL item image (iconPath sprite, glyph
//     fallback), name, price, the stat + delta line, affordability colour, EQUIPPED/OWNED
//     state, and ONE single-tap buy/equip/sell action (no duplicate bars);
//   • scrim / Close ✕ (touch — no Escape; hotkeys are gone).
//
// Code-built uGUI ONLY (no UXML — §8). It builds its own Canvas on Open, so it needs no
// PanelSettings. Registered with PanelManager + PanelRouter (PanelId.PartyShop). SHIPS
// BEHIND FeatureFlags.PartyShop (OFF): the bootstrap only spawns when ON, and CmdOpenShop
// suppresses the legacy ShopPanel when ON, so the two never double-open.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        private RectTransform _scrollContent;
        private TMPro.TextMeshProUGUI _headerLabel;
        private TMPro.TextMeshProUGUI _memberLabel;
        private TMPro.TextMeshProUGUI _walletText;
        private TMPro.TextMeshProUGUI _statusText;

        private PanelHandle _panelHandle;

        // Rows recorded per rebuild as (id, plate) so Render can hold the selected row.
        private readonly List<(string id, Image plate)> _rowPlates = new List<(string id, Image plate)>();

        public bool IsOpen => _ui != null;

        // ── Registration (mirror BuildingUpgradePanelMvvm) ────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Party Shop", Close, () => IsOpen);
            PanelRouter.Register(PanelId.PartyShop, OpenGeneric);
            PanelRouter.Register(PanelId.PartyShop, (System.Action<string>)OpenContext);
        }

        private void OnDestroy()
        {
            DisposeViewModel();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.PartyShop, OpenGeneric);
            PanelRouter.Unregister(PanelId.PartyShop, (System.Action<string>)OpenContext);
        }

        private void OpenGeneric() => Open(null, null);
        private void OpenContext(string vendorContext) => Open(vendorContext, null);

        // ── Open: resolve party + store at the open-site, build chrome, bind VM ───

        public void Open(string vendorContext, string displayName)
        {
            Close();
            _vendorContext = vendorContext ?? "";
            _displayName = displayName;

            BuildChrome();
            ConstructViewModel();
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return;   // rejected (e.g. in battle) — NotifyOpened already invoked Close.

            Debug.Log($"[PartyShopPanelMvvm] Opened for vendor '{_vendorContext}'. Bound PartyShopVM (MVVM).");
        }

        // Resolve the live targets (hero + every companion body with a GearLoadout) + the owned
        // store, mirror EquipmentPanel.ConstructViewModel, then inject into the VM. Member levels
        // come from each wearer's HeroProgression (1 when absent).
        private void ConstructViewModel()
        {
            DisposeViewModel();

            _store = new InventoryStore(VillageInventory.Instance);

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

        // ── IPanelView ────────────────────────────────────────────────────────────

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

        // ── Render: repaint from vm.* ONLY ────────────────────────────────────────

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
            RebuildList();
            HighlightSelectedRow();
        }

        // ── Chrome (presentation only) ────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("PartyShopPanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            var backdrop = ElarionUiKit.AddImage(_ui.transform, "ShopBackdrop",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f), rounded: false);
            var bdImg = backdrop.GetComponent<Image>();
            if (bdImg != null) bdImg.raycastTarget = false;

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelVendor);
            var panel = panelGo.transform;

            Color fillColor = new Color(0.07f, 0.055f, 0.042f, 0.985f);
            if (DeNelle.Core.FeatureFlags.BlinkChrome) fillColor.a = 0f;
            var solidFill = ElarionUiKit.AddImage(panel, "ShopSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f), fillColor);
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            _headerLabel = ElarionUiKit.Header(panel, "Gear Shop", x0: 0.04f, x1: 0.96f, y0: 0.91f, y1: 0.98f);

            // Wallet readout (top-right band).
            var walletGo = new GameObject("Wallet", typeof(TMPro.TextMeshProUGUI));
            walletGo.transform.SetParent(panel, false);
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
            _partyBar.transform.SetParent(panel, false);
            var pb = _partyBar.GetComponent<RectTransform>();
            pb.anchorMin = new Vector2(0.04f, 0.80f); pb.anchorMax = new Vector2(0.96f, 0.885f);
            pb.offsetMin = Vector2.zero; pb.offsetMax = Vector2.zero;

            // Selected-member sub-header (name — class (Lv N)).
            var memGo = new GameObject("MemberLabel", typeof(TMPro.TextMeshProUGUI));
            memGo.transform.SetParent(panel, false);
            var mr = memGo.GetComponent<RectTransform>();
            mr.anchorMin = new Vector2(0.04f, 0.755f); mr.anchorMax = new Vector2(0.66f, 0.80f);
            mr.offsetMin = Vector2.zero; mr.offsetMax = Vector2.zero;
            _memberLabel = memGo.GetComponent<TMPro.TextMeshProUGUI>();
            _memberLabel.fontSize = ElarionUi.FontBody;
            _memberLabel.color = ElarionUi.Parchment;
            _memberLabel.fontStyle = TMPro.FontStyles.Bold;
            _memberLabel.alignment = TMPro.TextAlignmentOptions.Left;
            _memberLabel.raycastTarget = false;

            // BUY / SELL tabs (both on the same screen — spec point 4).
            _tabBar = new GameObject("TabBar", typeof(RectTransform));
            _tabBar.transform.SetParent(panel, false);
            var tb = _tabBar.GetComponent<RectTransform>();
            tb.anchorMin = new Vector2(0.66f, 0.755f); tb.anchorMax = new Vector2(0.96f, 0.80f);
            tb.offsetMin = Vector2.zero; tb.offsetMax = Vector2.zero;
            CreateTab("BUY",  new Vector2(0.02f, 0.49f), () => _vm?.SetTab(PartyShopTab.Buy));
            CreateTab("SELL", new Vector2(0.51f, 0.98f), () => _vm?.SetTab(PartyShopTab.Sell));

            // Category selector ("dropdown selections": All / Weapons / Armor) — the missing
            // narrow over the combined weapons+armor list. Pinned/hidden for single-kind vendors
            // (CategorySelectorVisible). Sits just under the tab/member band, above the grid.
            _categoryBar = new GameObject("CategoryBar", typeof(RectTransform));
            _categoryBar.transform.SetParent(panel, false);
            var cb = _categoryBar.GetComponent<RectTransform>();
            cb.anchorMin = new Vector2(0.04f, 0.705f); cb.anchorMax = new Vector2(0.96f, 0.748f);
            cb.offsetMin = Vector2.zero; cb.offsetMax = Vector2.zero;
            CreateCategory("All",     new Vector2(0.01f, 0.32f),  PartyShopCategory.All);
            CreateCategory("Armor",   new Vector2(0.34f, 0.65f),  PartyShopCategory.Armor);
            CreateCategory("Weapons", new Vector2(0.67f, 0.99f),  PartyShopCategory.Weapons);

            // The scroll list area (the item grid).
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.04f, 0.12f); cr.anchorMax = new Vector2(0.96f, 0.70f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;

            // Close + status (bottom band).
            var closeBtn = ElarionUiKit.ButtonPack(panel, "Close", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.06f, 0.03f), new Vector2(0.30f, 0.095f), () => _vm?.Close(),
                packSpriteName: RpgUiCatalog.ButtonFrame);
            CreamTab(closeBtn);

            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.32f, 0.03f); sRect.anchorMax = new Vector2(0.96f, 0.095f);
            sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;
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

        // ── Party selector (top-left member icon buttons) ─────────────────────────

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

        // ── Item list ─────────────────────────────────────────────────────────────

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

            // STOCKED-N COMMIT SEAM: rows offered vs built — splits data-empty from built-but-broken.
            FlowTrace.Step("Store",
                $"PartyShop stocked {built} row(s) (wanted {wantCount}, failed {failed}).");

            // VERIFY rows>0: show a VISIBLE empty-state row instead of a blank panel.
            if (built == 0)
            {
                if (wantCount == 0)
                    FlowTrace.Warn("Store",
                        $"PartyShop has NO items for tab {_vm.Tab} — showing empty-state row (data-empty).");
                else
                    FlowTrace.Fail("Store",
                        $"PartyShop had {wantCount} item(s) but built 0 rows ({failed} failed) — showing empty-state row (built-but-broken).");
                CreateEmptyStateRow(listRoot, _vm.Tab == PartyShopTab.Sell ? "Nothing to sell." : "No wares in stock.");
            }

            FinalizeScroll();
        }

        // A single visible row carrying the empty-state copy — the never-blank fallback.
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
                if (sel != null && _rowPlates[i].id == sel)
                {
                    var c = plate.color;
                    plate.color = new Color(c.r * RowSelectedTint.r, c.g * RowSelectedTint.g, c.b * RowSelectedTint.b, c.a);
                }
            }
        }

        private const float RowHeightPx = 74f;
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

        // One item row: real item image (iconPath sprite, glyph fallback) + name + stat/delta
        // line + price + EQUIPPED/OWNED chip + ONE single-tap action (no duplicate bars).
        private void CreateRow(Transform parent, ItemVM item)
        {
            bool isSell = _vm != null && _vm.Tab == PartyShopTab.Sell;
            var detail = _vm != null ? _vm.DetailFor(item.Id) : null;

            var row = new GameObject((isSell ? "SellRow_" : "BuyRow_") + item.Id,
                typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;

            var rowImg = row.GetComponent<Image>();
            DressRowPlate(rowImg);
            _rowPlates.Add((item.Id, rowImg));

            var rowBtn = row.GetComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            ElarionUiKit.StyleButtonColors(rowBtn);
            string id = item.Id;
            // Tap the ROW = inspect (hold-select). The action button performs the single-tap buy/sell.
            rowBtn.onClick.AddListener(() => _vm?.Select(id));

            // REAL ITEM IMAGE (spec point 5): iconPath sprite first, else the catalog/glyph fallback.
            var iconHost = ElarionUiKit.AddImage(row.transform, "Icon",
                new Vector2(0.015f, 0.12f), new Vector2(0.135f, 0.88f),
                new Color(0f, 0f, 0f, 0.18f), rounded: false);
            var iconImg = iconHost.GetComponent<Image>();
            iconImg.raycastTarget = false;
            var sprite = ResolveItemSprite(detail, item);
            if (sprite != null)
            {
                iconImg.sprite = sprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
            }
            else
            {
                iconImg.color = new Color(0f, 0f, 0f, 0f);
                ElarionUiKit.Label(iconHost.transform, item.IconRole == PartyShopVM.IconRoleArmor ? "[]" : "/",
                    0f, 1f, ElarionUi.Parchment, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }

            // Name (upper).
            ElarionUiKit.Label(row.transform, item.Name, 0.52f, 0.92f, item.Equipped ? ElarionUi.Gilt : ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.16f, 0.66f, bold: item.Equipped);

            // Stat + delta line (the "why it's better" — spec point 6).
            string statLine = detail.HasValue ? detail.Value.Stats : "";
            string delta = detail.HasValue ? detail.Value.Delta : "";
            if (!string.IsNullOrEmpty(delta)) statLine += "    " + delta;
            ElarionUiKit.Label(row.transform, statLine, 0.08f, 0.50f,
                DeltaColor(delta), ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.16f, 0.66f);

            // Price / refund column.
            string priceText = isSell ? "+" + PriceString(item) : (item.Equipped || item.Price <= 0 ? "Owned" : PriceString(item));
            Color priceColor = isSell ? ElarionUi.Affordable
                             : (item.Price <= 0 ? ElarionUi.Gilt : (item.Affordable ? ElarionUi.Affordable : ElarionUi.Danger));
            ElarionUiKit.Label(row.transform, priceText, 0.52f, 0.92f, priceColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Right, 0.66f, 0.80f, bold: true);

            // State chip (EQUIPPED / OWNED), BUY tab only.
            if (!isSell)
            {
                string chip = item.Equipped ? "EQUIPPED" : (item.Price <= 0 ? "OWNED" : "");
                if (!string.IsNullOrEmpty(chip))
                    ElarionUiKit.Label(row.transform, chip, 0.08f, 0.40f,
                        item.Equipped ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                        ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Right, 0.66f, 0.80f, bold: true);
            }

            // THE single-tap action button (exactly ONE per row — no duplicate bars).
            string actionLabel = isSell ? "Sell"
                               : item.Equipped ? "Equipped"
                               : (item.Price <= 0 ? "Equip" : "Buy");
            // packSpriteName: only the TEXT-FREE Blink confirm plate when BlinkChrome is ON; otherwise
            // the clean procedural gold button (NOT ButtonGold — its art has "PLAY" baked in, which
            // would make every row button read "PLAY"; ElarionUiKit.ButtonPack documents this trap).
            var actBtn = ElarionUiKit.ButtonPack(row.transform, actionLabel, ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.82f, 0.16f), new Vector2(0.985f, 0.84f),
                () => _vm?.Act(id),
                packSpriteName: DeNelle.Core.FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : null);
            CreamTab(actBtn);
            if (actBtn != null) actBtn.interactable = !item.Equipped;   // already-equipped row is a no-op
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

        private static string PriceString(ItemVM item) => item.Price > 0 ? item.Price + " Gold" : "Free";

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

        // ── Teardown ────────────────────────────────────────────────────────────

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
            _walletText = null;
            _statusText = null;
            _headerLabel = null;
            _memberLabel = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _partyBar = null;
            _tabBar = null;
            _categoryBar = null;
            _scrollContent = null;
            _rowPlates.Clear();
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
