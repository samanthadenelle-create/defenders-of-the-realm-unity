// =============================================================================
// BuildingUpgradePanelMvvm — the building ENHANCEMENT panel VIEW (MVVM).
// A DUMB SKIN over ElarionUiKit chrome that BINDS a BuildingUpgradeVM. ALL
// state/logic (affordability, unlock, tier gating, perks) lives in the VM — the
// View never reads game state.  Namespace: DeNelle.Village.Buildings.Progression
// Assembly: DeNelle.Village
//
// TABBED REDESIGN (owner directive 2026-07-16 — "i dont understand the upgrade
// screen ... should be an upgrade tab, then skills at that level, its not just
// for forge its for all upgradable buildings").  BUILDING-AGNOSTIC: the title is
// composed from the live building name (_vm.Title + " Enhancements"), so ONE
// panel serves every upgradable building (Forge/Barracks/CrystalMine/... + the
// legacy resource buildings).  Two tabs, both bound to the SAME BuildingUpgradeVM
// (which already sources tiers from BuildingTierCatalog + perks from
// BuildingPerkService / building-tiers.json — no data invented here):
//
//   UPGRADE tab  -> the tier ladder.  Each tier is ONE full-width ROW:
//       crown icon | "Tier n - Name" + one-line effect | inline COST chips
//       (icon + number, colorblind-safe) | STATE:
//         * OWNED    -> "OWNED" tag, no cost, no button.
//         * NEXT     -> a grey "Upgrade" button (enabled only if affordable).
//         * LOCKED   -> greyed row + a one-line reason (no cost dumped on top).
//       The synthetic "Unlock Village Tier" control rides the TOP of this tab as
//       its own row (it is the tech-gate that opens higher tiers AND perks).
//
//   SKILLS tab   -> the per-tier RESEARCH PERKS.  Same ROW grammar: perk icon |
//       name + effect | Gold cost | OWNED / "Research" button / LOCKED-with-reason.
//       Locked perks are SHOWN (not hidden) with their specific reason so the
//       gate is legible even while the Village-Tier ladder is unmet (WO-460 gap).
//
// FIXES the old 3-column grid: no detached/overlapping wallet-vs-cost row, no
// truncated tier tile, full panel width used.  Wallet rides a TOP strip (clearly
// "your resources"), distinct from the per-row costs below.
//
// Chrome = BuildObsidianPanel(FrameTalent) landscape frame + ONE shared Close.
// Works landscape + portrait (fraction-anchored, vertical scroll per tab).
// Code-built uGUI ONLY (no UXML).  Eased open/close via PanelOpenCloseFx.
// SHIPS behind FeatureFlags.BuildingUpgradePanel (default ON since WO-476).
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    public sealed class BuildingUpgradePanelMvvm : MonoBehaviour, IPanelView
    {
        // Owned (lit) row tint — warm gilt lift over the slot plate.
        private static readonly Color OwnedTint = new Color(1.18f, 1.12f, 0.92f, 1f);
        // Locked row dim — the plate greys down + drops alpha.
        private static readonly Color LockedTint = new Color(0.52f, 0.52f, 0.55f, 0.80f);

        // Sprite-first row plate (canon §5) — the talent slot plate, ungated. Fallback procedural.
        private const string SlotTalentPlate = "slot_talent_1";
        // Committed currency-icon role folder (Resources/RpgUi/currency/currency_*).
        private const string CurrencyRole = "currency";

        private BuildingUpgradeVM _vm;

        private GameObject _ui;
        private RectTransform _bodyHost;          // content host (below wallet + tab row)
        private RectTransform _upgradeContent;    // Upgrade-tab scroll content (tier rows)
        private RectTransform _skillsContent;     // Skills-tab scroll content (perk rows)
        private GameObject _upgradePage;          // Upgrade page root (toggled)
        private GameObject _skillsPage;           // Skills page root (toggled)
        private ElarionUiKit.TabRowHandle _tabRow;
        private int _activeTab;                   // 0 = Upgrade, 1 = Skills

        // Top wallet strip chips (built once; count-tweened on each Render).
        private struct ChipRef { public ElarionUiKit.CurrencyKind Kind; public ElarionUiKit.CurrencyChipHandle Handle; }
        private readonly List<ChipRef> _chips = new List<ChipRef>();

        // Status is transient: toast only NEW statuses, never the open-time baseline.
        private string _lastStatus;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        private const float RowHeightPx   = 132f;
        private const float RowGapPx      = 12f;
        private const float ButtonFadeSec = 0.12f;   // hover/press transition — never snap

        // ── Registration ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Building Enhancements", Close, () => IsOpen);
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

        // PanelRouter plain (no-context) open — the VM resolves the default building.
        private void OpenGeneric() => Open(null);

        // ── Open: construct + bind the VM, build chrome ───────────────────────────

        public void Open(string buildingId)
        {
            Close();

            // VM FIRST — it resolves the default building + economy handle itself, so this
            // View never touches a service, and the chrome's title composes from the name.
            _vm = BuildingUpgradeVM.CreateDefault(buildingId, Close);

            BuildChrome();

            Bind(_vm);

            FlowTrace.Step("UpgradeUI", "open '" + (_vm != null ? _vm.Title : "?")
                + "' tabbed (Upgrade+Skills), tab=" + _activeTab);

            // Arbiter closes any other open panel first + applies the battle-lock.
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                // Rejected (e.g. in battle) — NotifyOpened already invoked our Close.
                return;
            }
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

            // Refresh the top wallet chips (count-tween; no red/green flash).
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].Handle?.SetAmount(WalletValue(_chips[i].Kind));

            // Status is transient — pop a toast only when it CHANGES to a new, non-empty message.
            string status = _vm.Status;
            if (!string.IsNullOrEmpty(status) && status != _lastStatus)
            {
                _lastStatus = status;
                BuildFeedbackToast.Show(status);
            }

            RebuildTabs();
            ApplyTabVisibility();
        }

        // ── Chrome — MASTER FRAME + wallet strip + tab row + two scroll pages ─────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("BuildingUpgradePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            string titleText = (_vm != null ? _vm.Title : "Building") + " Enhancements";

            // LANDSCAPE Talent frame (mirror HeroSkillTreePanelMvvm's sizing).
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, titleText,
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), () => _vm?.Close(),
                headerX0: 0.04f, headerX1: 0.74f,
                frameName: RpgUiCatalog.FrameTalent, medallionIcon: "hammer");

            // Frame path returns layout; procedural fallback synthesizes an equivalent body zone.
            RectTransform body = chrome.layout != null && chrome.layout.body != null
                ? chrome.layout.body
                : MakeZone(chrome.content.transform, "Zone_Body", new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.855f));

            SoftenButton(chrome.close);

            // TOP: wallet strip ("your resources") — clearly separate from the per-row costs below.
            var walletStrip = MakeZone(body, "WalletStrip", new Vector2(0f, 0.885f), new Vector2(1f, 1f));
            BuildWalletStrip(walletStrip);

            // TAB ROW under the wallet: Upgrade | Skills (the ONE kit tab row).
            var tabHost = MakeZone(body, "TabRow", new Vector2(0.02f, 0.785f), new Vector2(0.98f, 0.872f));
            _tabRow = ElarionUiKit.BuildTabRow(tabHost, new[] { "Upgrade", "Skills" }, OnTab, _activeTab);

            // CONTENT host (below tabs): two toggled scroll pages, same row grammar.
            _bodyHost = MakeZone(body, "ContentHost", new Vector2(0f, 0f), new Vector2(1f, 0.775f));
            _upgradePage = BuildScrollPage(_bodyHost, "UpgradePage", out _upgradeContent);
            _skillsPage  = BuildScrollPage(_bodyHost, "SkillsPage",  out _skillsContent);
            ApplyTabVisibility();

            // Capture the open-time status as the toast baseline (do NOT toast the idle hint).
            _lastStatus = _vm != null ? _vm.Status : null;

            // Eased open: scale 0.92->1 + fade 0->1, ease-out.
            var fx = _ui.AddComponent<PanelOpenCloseFx>();
            fx.PlayOpen(chrome.root != null ? chrome.root.transform as RectTransform : null);
        }

        private void OnTab(int index)
        {
            _activeTab = Mathf.Clamp(index, 0, 1);
            FlowTrace.Step("UpgradeUI", "tab -> " + (_activeTab == 0 ? "Upgrade" : "Skills"));
            ApplyTabVisibility();
        }

        private void ApplyTabVisibility()
        {
            if (_upgradePage != null) _upgradePage.SetActive(_activeTab == 0);
            if (_skillsPage  != null) _skillsPage.SetActive(_activeTab == 1);
        }

        // ── Top wallet strip ──────────────────────────────────────────────────────
        // ONE ElarionUiKit.CurrencyChip per spendable currency, count-tweened. The set is
        // derived from the VM's cost strings (presentation read of VM data — no game state).

        private void BuildWalletStrip(RectTransform strip)
        {
            _chips.Clear();
            if (strip == null) return;

            var kinds = DeriveSpendableCurrencies();
            int n = kinds.Count;
            if (n == 0) return;

            const float gap = 0.008f;
            for (int i = 0; i < n; i++)
            {
                float x0 = (float)i / n + gap;
                float x1 = (float)(i + 1) / n - gap;
                bool primary = kinds[i] == ElarionUiKit.CurrencyKind.Gold;
                var handle = ElarionUiKit.CurrencyChip(strip, kinds[i],
                    new Vector2(x0, 0.14f), new Vector2(x1, 0.86f),
                    primary: primary, tag: CurrencyTag(kinds[i]));
                _chips.Add(new ChipRef { Kind = kinds[i], Handle = handle });
            }
        }

        // Scan the VM's per-tile cost strings for the currency keywords this building spends.
        // Fixed display order (Gold primary first). Falls back to all five when nothing parses.
        private List<ElarionUiKit.CurrencyKind> DeriveSpendableCurrencies()
        {
            bool gold = false, wood = false, food = false, iron = false, crystal = false;
            if (_vm != null && _vm.Perks != null)
            {
                foreach (var item in _vm.Perks)
                {
                    string cost = _vm.CostFor(item.Id);
                    if (string.IsNullOrEmpty(cost)) continue;
                    string c = cost.ToLowerInvariant();
                    if (c.Contains("gold"))    gold = true;
                    if (c.Contains("wood"))    wood = true;
                    if (c.Contains("food"))    food = true;
                    if (c.Contains("iron"))    iron = true;
                    if (c.Contains("crystal")) crystal = true;
                }
            }

            var list = new List<ElarionUiKit.CurrencyKind>();
            if (gold)    list.Add(ElarionUiKit.CurrencyKind.Gold);
            if (wood)    list.Add(ElarionUiKit.CurrencyKind.Wood);
            if (food)    list.Add(ElarionUiKit.CurrencyKind.Food);
            if (iron)    list.Add(ElarionUiKit.CurrencyKind.Iron);
            if (crystal) list.Add(ElarionUiKit.CurrencyKind.Crystal);

            if (list.Count == 0)
            {
                list.Add(ElarionUiKit.CurrencyKind.Gold);
                list.Add(ElarionUiKit.CurrencyKind.Wood);
                list.Add(ElarionUiKit.CurrencyKind.Food);
                list.Add(ElarionUiKit.CurrencyKind.Iron);
                list.Add(ElarionUiKit.CurrencyKind.Crystal);
            }
            return list;
        }

        private static string CurrencyTag(ElarionUiKit.CurrencyKind kind)
        {
            switch (kind)
            {
                case ElarionUiKit.CurrencyKind.Gold:    return "Gold";
                case ElarionUiKit.CurrencyKind.Wood:    return "Wood";
                case ElarionUiKit.CurrencyKind.Food:    return "Food";
                case ElarionUiKit.CurrencyKind.Iron:    return "Iron";
                case ElarionUiKit.CurrencyKind.Crystal: return "Crystals";
                default:                                return kind.ToString();
            }
        }

        private long WalletValue(ElarionUiKit.CurrencyKind kind)
        {
            if (_vm == null) return 0;
            switch (kind)
            {
                case ElarionUiKit.CurrencyKind.Gold:    return _vm.Coins;
                case ElarionUiKit.CurrencyKind.Wood:    return _vm.Wood;
                case ElarionUiKit.CurrencyKind.Food:    return _vm.Food;
                case ElarionUiKit.CurrencyKind.Iron:    return _vm.Iron;
                case ElarionUiKit.CurrencyKind.Crystal: return _vm.Crystals;
                default:                                return 0;
            }
        }

        // ── Tab content: split vm.Perks by id into the two pages ──────────────────
        // "perk:*" -> Skills tab; "villagetier" + "tier-*" -> Upgrade tab. Both pages
        // use the SAME full-width row builder; the VM data alone decides each row's state.

        private void RebuildTabs()
        {
            if (_vm == null || _upgradeContent == null || _skillsContent == null) return;

            ClearChildren(_upgradeContent);
            ClearChildren(_skillsContent);

            bool anyPerk = false;
            foreach (var item in _vm.Perks)
            {
                bool isPerk = item.Id != null && item.Id.StartsWith("perk:");
                if (isPerk)
                {
                    CreateRow(_skillsContent, item);
                    anyPerk = true;
                }
                else
                {
                    CreateRow(_upgradeContent, item);   // villagetier + tier-*
                }
            }

            if (!anyPerk)
                EmptyNote(_skillsContent, "No research skills for this building yet.");

            LayoutRebuilder.ForceRebuildLayoutImmediate(_upgradeContent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_skillsContent);
        }

        // ── One scroll page (viewport + clamped ScrollRect + vertical row stack) ──

        private GameObject BuildScrollPage(Transform parent, string name, out RectTransform content)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var prt = (RectTransform)page.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(page.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);
            vImg.raycastTarget = true;   // drag-scroll target

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = Vector2.zero;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(8, 8, 6, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            content = cr;
            return page;
        }

        // ── One full-width ROW (presentation; data from the bound ItemVM) ─────────
        // Layout:  icon | name + one-line effect | (purchasable: cost chips + CTA)
        //                                          (owned: "OWNED") (locked: reason)

        private void CreateRow(Transform parent, ItemVM item)
        {
            var row = new GameObject("Row_" + item.Id, typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = RowHeightPx;
            le.preferredHeight = RowHeightPx;

            var plate = row.GetComponent<Image>();
            DressRowPlate(plate);
            float dim = 1f;
            if (item.Equipped)
            {
                var c = plate.color;
                plate.color = new Color(c.r * OwnedTint.r, c.g * OwnedTint.g, c.b * OwnedTint.b, c.a);
            }
            else if (item.Locked)
            {
                var c = plate.color;
                plate.color = new Color(c.r * LockedTint.r, c.g * LockedTint.g, c.b * LockedTint.b, c.a * LockedTint.a);
                dim = 0.6f;
            }

            bool purchasable = !item.Locked && !item.Equipped;

            var btn = row.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            // The whole row is a large touch target ONLY when purchasable; owned/locked read
            // as settled state (no hover highlight, no action).
            btn.interactable = purchasable;
            if (!purchasable) btn.transition = Selectable.Transition.None;
            string id = item.Id;
            btn.onClick.AddListener(() => { FlowTrace.Step("UpgradeUI", "row-tap " + id); _vm?.Select(id); });

            // ICON (left) — crown for a tier/village row, perk sprite for a skill, glyph fallback.
            Sprite icon = IconFor(item);
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(row.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.015f, 0.14f); irt.anchorMax = new Vector2(0.11f, 0.86f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = icon;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                var g = ElarionUiKit.Label(row.transform, TierGlyph(item.Id), 0.14f, 0.86f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.015f, 0.11f, bold: true);
                g.raycastTarget = false;
                ElarionUiKit.FitSingleLine(g);
            }

            // NAME (line 1) + EFFECT (line 2) — the concrete payoff, VM-relayed.
            var nameLbl = ElarionUiKit.Label(row.transform, item.Name, 0.50f, 0.90f,
                new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, dim),
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft, 0.135f, 0.49f, bold: true);
            nameLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nameLbl);

            string effect = _vm != null ? _vm.EffectFor(item.Id) : "";
            if (!string.IsNullOrEmpty(effect))
            {
                var effLbl = ElarionUiKit.Label(row.transform, effect, 0.12f, 0.47f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.85f * dim),
                    ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, 0.135f, 0.49f);
                effLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(effLbl);
            }

            // RIGHT region — state.
            if (item.Equipped)
            {
                var owned = ElarionUiKit.Label(row.transform, "OWNED", 0.30f, 0.70f,
                    ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.66f, 0.985f, bold: true);
                owned.raycastTarget = false;
                ElarionUiKit.FitSingleLine(owned);
            }
            else if (item.Locked)
            {
                // Colorblind law: locked reason is TEXT, never hue. No cost dumped on a locked row.
                string reason = !string.IsNullOrEmpty(item.LockReason) ? item.LockReason : "Locked";
                var req = ElarionUiKit.Label(row.transform, reason, 0.14f, 0.86f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.50f, 0.985f);
                req.raycastTarget = false;
                ElarionUiKit.FitBlock(req);
            }
            else
            {
                // Purchasable — inline cost chips (icon + number) + a grey Upgrade/Research CTA.
                string cost = _vm != null ? _vm.CostFor(item.Id) : "";
                BuildCostChips(row.transform, cost, 0.50f, 0.795f);

                bool isPerk = item.Id != null && item.Id.StartsWith("perk:");
                string cta = isPerk ? "Research" : "Upgrade";
                BuildRowCta(row.transform, cta, item.Affordable,
                    () => { FlowTrace.Step("UpgradeUI", (isPerk ? "research " : "upgrade ") + id); _vm?.Select(id); });
            }
        }

        // ── Inline cost chips (icon + number) — colorblind-safe, reuse chip grammar ──

        private void BuildCostChips(Transform parent, string costText, float x0, float x1)
        {
            if (string.IsNullOrEmpty(costText)) return;
            // "700 Wood - 450 Food" (VM joins parts with a U+00B7 middle-dot); split/trim/drop-empty.
            var raw = costText.Split('\u00B7');
            var tokens = new List<string>();
            foreach (var r in raw)
            {
                string t = r.Trim();
                if (t.Length > 0) tokens.Add(t);
            }
            int n = tokens.Count;
            if (n == 0) return;

            const float gap = 0.008f;
            float span = x1 - x0;
            float cw = (span - gap * (n - 1)) / n;
            if (cw <= 0f) cw = span / n;
            for (int i = 0; i < n; i++)
            {
                float cx0 = x0 + i * (cw + gap);
                BuildCostChip(parent, tokens[i], cx0, cx0 + cw);
            }
        }

        private void BuildCostChip(Transform parent, string token, float x0, float x1)
        {
            var go = new GameObject("CostChip", typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, 0.30f); rt.anchorMax = new Vector2(x1, 0.70f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var plate = go.GetComponent<Image>();
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
            if (plateSprite != null)
            {
                plate.sprite = plateSprite;
                plate.type = Image.Type.Sliced;
                plate.color = Color.white;
            }
            else
            {
                plate.color = ElarionUiKit.Cell;
                ElarionUiKit.ApplyRounded(plate);
            }
            plate.raycastTarget = false;

            Sprite ic = CurrencyIconFor(token);
            float textX0 = 0.10f;
            if (ic != null)
            {
                var ig = new GameObject("Icon", typeof(Image));
                ig.transform.SetParent(go.transform, false);
                var irt = ig.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.06f, 0.16f); irt.anchorMax = new Vector2(0.40f, 0.84f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iimg = ig.GetComponent<Image>();
                iimg.sprite = ic; iimg.preserveAspect = true; iimg.raycastTarget = false;
                textX0 = 0.44f;
            }

            // With an icon the chip shows the NUMBER only (icon carries identity); without art,
            // the whole token (number + name) so a chip is never a naked number.
            string shown = ic != null ? LeadingNumber(token) : token;
            var lbl = ElarionUiKit.Label(go.transform, shown, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                ic != null ? TMPro.TextAlignmentOptions.MidlineLeft : TMPro.TextAlignmentOptions.Center,
                textX0, 0.94f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        // Grey/white kit CTA seated at the row's right edge. Built manually (NOT ElarionUiKit.Button)
        // so the 112px min-touch guard cannot grow it out of the row; the row height is the target.
        private void BuildRowCta(Transform parent, string label, bool enabled, System.Action onClick)
        {
            var go = new GameObject("RowCta", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.815f, 0.16f); rt.anchorMax = new Vector2(0.985f, 0.84f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var plate = go.GetComponent<Image>();
            var gray = RpgUiCatalog.Get(RpgUiCatalog.RoleButton,
                ElarionUiKit.ObsidianButtonSpriteName(ElarionUiKit.ObsidianButtonStyle.Style1,
                                                      ElarionUiKit.ObsidianButtonColor.Gray));
            if (gray != null)
            {
                plate.sprite = gray; plate.type = Image.Type.Sliced; plate.color = Color.white;
            }
            else
            {
                plate.color = ElarionUiKit.Cell;
                ElarionUiKit.ApplyRounded(plate);
                var outline = go.AddComponent<Outline>();
                outline.effectColor = ElarionUiKit.ObsidianTrim;
                outline.effectDistance = new Vector2(2f, 2f);
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = enabled;   // disabled state greys via StyleButtonColors.disabledColor
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lbl = ElarionUiKit.Label(go.transform, label, 0.05f, 0.95f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        private void EmptyNote(Transform parent, string text)
        {
            var go = new GameObject("EmptyNote", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = RowHeightPx;
            le.preferredHeight = RowHeightPx;
            var lbl = ElarionUiKit.Label(go.transform, text, 0.30f, 0.70f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
            lbl.raycastTarget = false;
            ElarionUiKit.FitBlock(lbl);
        }

        // ── Icon / glyph helpers ──────────────────────────────────────────────────

        private Sprite IconFor(ItemVM item)
        {
            if (item.Id == BuildingUpgradeVM.VillageTierRowId)
                return RpgUiCatalog.Get(RpgUiCatalog.RoleCrown, "tier3");
            if (item.IconRole == BuildingUpgradeVM.IconRolePerk && !string.IsNullOrEmpty(item.IconName))
                return Resources.Load<Sprite>("HudIcons/BuildingUpgrades/" + item.IconName);
            if (item.IconRole == BuildingUpgradeVM.IconRoleTier
                && item.Id != null && item.Id.StartsWith("tier-"))
                return RpgUiCatalog.Get(RpgUiCatalog.RoleCrown, "tier" + Mathf.Clamp(TierNumber(item.Id), 1, 3));
            return null;
        }

        private static int TierNumber(string id)
        {
            int dash = id != null ? id.LastIndexOf('-') : -1;
            if (dash >= 0 && dash < id.Length - 1 && int.TryParse(id.Substring(dash + 1), out int n)) return n;
            return 1;
        }

        private static string TierGlyph(string id)
        {
            // "tier-3" -> "3"; anything else -> the crest glyph.
            if (id != null && id.StartsWith("tier-"))
            {
                int dash = id.LastIndexOf('-');
                string n = dash >= 0 && dash < id.Length - 1 ? id.Substring(dash + 1) : "";
                return string.IsNullOrEmpty(n) ? "-" : n;
            }
            return ElarionUi.CrestGlyph;
        }

        // The leading number of a cost token ("700 Wood" -> "700", "1.2m Crystals" -> "1.2m").
        private static string LeadingNumber(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            int sp = token.IndexOf(' ');
            return sp > 0 ? token.Substring(0, sp) : token;
        }

        // The currency icon for a cost token — the FIRST currency it names. Null when nothing matches.
        private static Sprite CurrencyIconFor(string costText)
        {
            if (string.IsNullOrEmpty(costText)) return null;
            string c = costText.ToLowerInvariant();
            string name = null;
            int best = int.MaxValue;
            void Consider(string kw, string spriteName)
            {
                int i = c.IndexOf(kw, System.StringComparison.Ordinal);
                if (i >= 0 && i < best) { best = i; name = spriteName; }
            }
            Consider("wood", "currency_wood");
            Consider("food", "currency_food");
            Consider("iron", "currency_iron");
            Consider("crystal", "currency_crystal");
            Consider("gold", "currency_gold");
            return name != null ? RpgUiCatalog.Get(CurrencyRole, name) : null;
        }

        private static void DressRowPlate(Image plateImg)
        {
            if (plateImg == null) return;
            // Sprite-first ALWAYS (canon §5): the talent slot plate, ungated.
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, SlotTalentPlate);
            if (plate != null)
            {
                plateImg.sprite = plate;
                plateImg.type   = Image.Type.Sliced;
                plateImg.color  = Color.white;
                return;
            }
            // Procedural fallback (art absent) — the row never blanks.
            plateImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(plateImg);
        }

        // Smooth hover/press: keep the kit ColorTint block but give it a real fade (never snap).
        private static void SoftenButton(Button btn)
        {
            if (btn == null || btn.transition != Selectable.Transition.ColorTint) return;
            var colors = btn.colors;
            colors.fadeDuration = ButtonFadeSec;
            btn.colors = colors;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private static void ClearChildren(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var c = host.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _chips.Clear();
            _lastStatus = null;
            if (_ui != null)
            {
                // Eased close: the dying canvas fades/scales out then destroys itself.
                var fx = _ui.GetComponent<PanelOpenCloseFx>();
                if (fx != null && fx.isActiveAndEnabled) fx.PlayCloseAndDestroy();
                else Destroy(_ui);
            }
            _ui = null;
            _bodyHost = null;
            _upgradeContent = null;
            _skillsContent = null;
            _upgradePage = null;
            _skillsPage = null;
            _tabRow = null;
            PanelManager.NotifyClosed(_panelHandle);
        }

        private static RectTransform MakeZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }
    }

    /// <summary>
    /// DEPRECATED private twin (WO-714 P8): the kit now owns this tween as
    /// <c>ElarionUiKit.PanelOpenCloseFx</c> — new code uses the kit version; this copy
    /// is kept only so parallel lanes stay additive, and migrates on-touch. Ease-out
    /// scale 0.92-&gt;1 + fade-in on open (~0.18s); ease-in fade/scale-out then self-destroy
    /// on close (~0.14s). Unscaled time; CanvasGroup blocks input while closing.
    /// </summary>
    internal sealed class PanelOpenCloseFx : MonoBehaviour
    {
        private const float OpenSec  = 0.18f;
        private const float CloseSec = 0.14f;

        private CanvasGroup _group;
        private RectTransform _scaled;
        private bool _closing;

        public void PlayOpen(RectTransform scaleTarget)
        {
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _scaled = scaleTarget;
            _group.alpha = 0f;
            if (_scaled != null) _scaled.localScale = Vector3.one * 0.92f;
            StartCoroutine(Ease(open: true, OpenSec, onDone: null));
        }

        public void PlayCloseAndDestroy()
        {
            if (_closing) return;
            _closing = true;
            if (_group == null) _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            StartCoroutine(Ease(open: false, CloseSec, onDone: () => Destroy(gameObject)));
        }

        private IEnumerator Ease(bool open, float duration, System.Action onDone)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                float k = open ? 1f - Mathf.Pow(1f - x, 3f) : 1f - Mathf.Pow(x, 3f);
                if (_group != null) _group.alpha = k;
                if (_scaled != null)
                    _scaled.localScale = Vector3.one * Mathf.Lerp(open ? 0.92f : 0.94f, 1f, k);
                yield return null;
            }
            if (_group != null) _group.alpha = open ? 1f : 0f;
            if (_scaled != null && open) _scaled.localScale = Vector3.one;
            onDone?.Invoke();
        }
    }
}
