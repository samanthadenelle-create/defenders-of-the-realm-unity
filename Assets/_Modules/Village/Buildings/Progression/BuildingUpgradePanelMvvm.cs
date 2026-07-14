// =============================================================================
// BuildingUpgradePanelMvvm — the building ENHANCEMENT (perk-grid) VIEW (MVVM).
// A DUMB SKIN: it builds presentation through the ElarionUiKit MASTER FRAME
// (BuildObsidianPanel + drop-zones, UI_BLINK_TEMPLATE_CANON) and BINDS a
// BuildingUpgradeVM. ALL state/logic (affordability, unlock, tier gating) lives
// in the VM — the View never reads game state (audit 2026-07-02 §3.1 finish).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// WO-675 OBSIDIAN/TALENT REDESIGN (owner approved 2026-07-11 "yes so much
// clearer"). A pure View re-skin — the VM is UNTOUCHED, zero new architecture:
//   1. Frame -> RpgUiCatalog.FrameTalent (landscape talent frame, medallion hammer).
//   2. Perks are grouped under TIER BANDS (crown/tier glyph + gilt rule + "TIER n").
//      The synthetic Village-Tier tile is REMOVED from the grid; its ONE gold
//      "Unlock" action rides the first locked tier band's header (same vm.Select).
//   3. Tile plates use the slot_talent_* sprite (sprite-first ALWAYS, ungated) with
//      a procedural fallback so the panel never blanks when art is absent.
//   4. Affordance ring = a sliced rarity rim sprite (fallback: gold Outline).
//   5. Wallet -> ElarionUiKit.CurrencyChip row in the footer zone (count-tween);
//      only the currencies this building can spend, derived from the VM cost data.
//   6. Status line -> transient BuildFeedbackToast (no persistent strip).
//   7. Cost lines carry a RpgUi/currency/* icon beside the value (name text stays).
//
// Chrome = BuildObsidianPanel(FrameTalent): title -> layout.header, tier bands ->
// layout.body (scrolling), currency chips -> layout.footer. ONE shared Close.
// Code-built uGUI ONLY (no UXML — §8). Eased open/close via the local
// PanelOpenCloseFx (flagged for kit promotion, WO-675 §8 — additive, on-touch).
//
// SHIPS BEHIND FeatureFlags.BuildingUpgradePanel (default ON since WO-476 — this
// panel IS the live upgrade surface; the legacy UIDocument twin was DELETED
// 2026-07-02). Distinct GameObject name ("BuildingUpgradePanelMvvm").
// =============================================================================

using System.Collections;
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
        // Owned (lit) tile tint — warm gilt lift over the slot plate.
        private static readonly Color OwnedTint = new Color(1.18f, 1.12f, 0.92f, 1f);
        // Locked tile dim — the plate greys down + drops alpha.
        private static readonly Color LockedTint = new Color(0.52f, 0.52f, 0.55f, 0.80f);

        // Sprite-first plate (WO-675 §3) — the talent slot plate, ungated. Fallback procedural.
        private const string SlotTalentPlate = "slot_talent_1";
        // Rarity rim overlay for the unlockable+affordable tile (WO-675 §4).
        private const string AffordRimSprite = "rarity_4";
        // Committed currency-icon role folder (Resources/RpgUi/currency/currency_*).
        private const string CurrencyRole = "currency";

        private BuildingUpgradeVM _vm;

        private GameObject _ui;
        private RectTransform _bodyHost;          // persistent scroll host inside layout.body
        private RectTransform _scrollContent;     // VerticalLayoutGroup content (bands)
        private readonly List<GridLayoutGroup> _tileGrids = new List<GridLayoutGroup>();

        // Footer currency chips (built once; count-tweened on each Render). WO-675 §5.
        private struct ChipRef { public ElarionUiKit.CurrencyKind Kind; public ElarionUiKit.CurrencyChipHandle Handle; }
        private readonly List<ChipRef> _chips = new List<ChipRef>();

        // Status is now transient (WO-675 §6): toast only NEW statuses, never the open-time baseline.
        private string _lastStatus;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        private const int   BandColumns   = 3;
        private const float TileHeightPx  = 196f;
        private const float TileGapPx     = 12f;
        private const float BandHeaderPx  = 62f;
        private const float ButtonFadeSec = 0.12f;   // hover/press transition — never snap

        // ── Registration (mirror HeroSkillTreePanelMvvm) ──────────────────────────

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

        // PanelRouter plain (no-context) open — the VM resolves the default building
        // (View-side catalog reads removed per the audit §3.1).
        private void OpenGeneric() => Open(null);

        // ── Open: construct + bind the VM, build chrome ───────────────────────────

        public void Open(string buildingId)
        {
            Close();

            // VM FIRST — it resolves the default building + economy handle itself
            // (BuildingUpgradeVM.CreateDefault), so this View never touches a service,
            // and the chrome's title composes ONCE from the live building name.
            _vm = BuildingUpgradeVM.CreateDefault(buildingId, Close);

            BuildChrome();

            Bind(_vm);

            // Arbiter closes any other open panel first (DEF-212) + applies the battle-lock.
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

            // WO-675 §5: refresh the footer chips (count-tween; no red/green flash).
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].Handle?.SetAmount(WalletValue(_chips[i].Kind));

            // WO-675 §6: status is transient — pop a toast only when it CHANGES to a new,
            // non-empty message (the open-time baseline was captured in BuildChrome).
            string status = _vm.Status;
            if (!string.IsNullOrEmpty(status) && status != _lastStatus)
            {
                _lastStatus = status;
                BuildFeedbackToast.Show(status);
            }

            RebuildBands();
        }

        // ── Chrome — MASTER FRAME ONLY (UI_BLINK_TEMPLATE_CANON §2-§4) ────────────
        // BuildObsidianPanel(FrameTalent) supplies the landscape talent frame + header
        // title + the ONE shared Close. This View drops chrome-less content into the
        // returned drop-zones: layout.header -> title (pre-built), layout.body -> tier
        // bands, layout.footer -> currency chips. No per-screen cards/wells/rims.

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("BuildingUpgradePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            string titleText = (_vm != null ? _vm.Title : "Building") + " Enhancements";

            // WO-675 §1: LANDSCAPE Talent frame (mirror HeroSkillTreePanelMvvm's sizing).
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, titleText,
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), () => _vm?.Close(),
                headerX0: 0.04f, headerX1: 0.74f,
                frameName: RpgUiCatalog.FrameTalent, medallionIcon: "hammer");

            // Zones: frame path returns layout; procedural fallback (art absent) does not —
            // synthesize an equivalent body zone over chrome.content so the screen never blanks.
            RectTransform body = chrome.layout != null && chrome.layout.body != null
                ? chrome.layout.body
                : MakeZone(chrome.content.transform, "Zone_Body", new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.855f));

            // Smooth the shared Close button's tint transition too.
            SoftenButton(chrome.close);

            // BODY zone: the tier bands scroll here (full zone; the footer carries the wallet chips).
            _bodyHost = MakeZone(body, "BandHost", new Vector2(0f, 0f), new Vector2(1f, 1f));

            // WO-675 §5: currency chips ride the FOOTER band (frame path), else a synthesized
            // base strip over the body (art-absent fallback) so the wallet never clips or blanks.
            RectTransform footer = chrome.layout != null && chrome.layout.footer != null
                ? chrome.layout.footer
                : MakeZone(body, "Zone_FooterFallback", new Vector2(0f, 0f), new Vector2(1f, 0.09f));
            BuildCurrencyFooter(footer);

            // Capture the open-time status as the toast baseline (do NOT toast the idle hint).
            _lastStatus = _vm != null ? _vm.Status : null;

            // Eased open (owner smoothness directive): scale 0.92->1 + fade 0->1, ease-out.
            var fx = _ui.AddComponent<PanelOpenCloseFx>();
            fx.PlayOpen(chrome.root != null ? chrome.root.transform as RectTransform : null);
        }

        // ── Currency footer (WO-675 §5) ───────────────────────────────────────────
        // ONE ElarionUiKit.CurrencyChip per spendable currency, count-tweened. The set is
        // derived from the VM's cost strings (presentation read of VM data — no game state);
        // falls back to all five when ambiguous. Gold is the primary (larger, gilt) chip.

        private void BuildCurrencyFooter(RectTransform footer)
        {
            _chips.Clear();
            if (footer == null) return;

            var kinds = DeriveSpendableCurrencies();
            int n = kinds.Count;
            if (n == 0) return;

            const float gap = 0.008f;
            for (int i = 0; i < n; i++)
            {
                float x0 = (float)i / n + gap;
                float x1 = (float)(i + 1) / n - gap;
                bool primary = kinds[i] == ElarionUiKit.CurrencyKind.Gold;
                var handle = ElarionUiKit.CurrencyChip(footer, kinds[i],
                    new Vector2(x0, 0.12f), new Vector2(x1, 0.88f),
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

        // ── Tier bands (WO-675 §2, gate seating fixed WO-680) ────────────────────
        // Group vm.Perks into bands: each "tier-N" tile becomes a "TIER n" band (crown
        // glyph + gilt rule); research perks group under a "Research" band. The synthetic
        // Village-Tier tile is pulled OUT of the grid — its one gold Unlock action rides
        // the first VILLAGE-GATED tier band's header (one action = one button); a band
        // locked by the building's own tier ladder shows requirement text only (WO-680).

        private sealed class Band
        {
            public string Key;
            public string Label;
            public string CrownName;                 // "tier1".."tier3" or null (research)
            public int TierNumber = -1;
            public readonly List<ItemVM> Tiles = new List<ItemVM>();
            public bool Locked;                      // this band's tier tile is not yet reachable
            public string Requirement;               // the locked tile's LockReason (header hint)
            public string Gate;                      // WO-680 — vm.GateFor: which gate locks it
        }

        private void RebuildBands()
        {
            ClearContent();
            _tileGrids.Clear();

            // 1) Partition the perks into ordered bands; pull the Village-Tier control aside.
            var bands = new List<Band>();
            var byKey = new Dictionary<string, Band>();
            ItemVM villageTier = default;
            bool hasVillageTier = false;

            foreach (var item in _vm.Perks)
            {
                if (item.Id == BuildingUpgradeVM.VillageTierRowId)
                {
                    villageTier = item;
                    hasVillageTier = true;
                    continue;   // §2 — removed from the grid; becomes a band-header action
                }

                string key; string label; string crown; int tierNum;
                if (item.Id != null && item.Id.StartsWith("tier-"))
                {
                    tierNum = TierNumber(item.Id);
                    key = "tier-" + tierNum;
                    label = "TIER " + tierNum;
                    crown = "tier" + Mathf.Clamp(tierNum, 1, 3);
                }
                else
                {
                    key = "research";
                    label = "RESEARCH";
                    crown = null;
                    tierNum = int.MaxValue;   // research band sorts last
                }

                if (!byKey.TryGetValue(key, out var band))
                {
                    band = new Band { Key = key, Label = label, CrownName = crown, TierNumber = tierNum };
                    byKey[key] = band;
                    bands.Add(band);
                }
                band.Tiles.Add(item);

                // A tier band inherits "locked" + its requirement + its gate from its tier tile.
                if (key != "research" && item.Locked)
                {
                    band.Locked = true;
                    if (string.IsNullOrEmpty(band.Requirement)) band.Requirement = item.LockReason;
                    if (string.IsNullOrEmpty(band.Gate)) band.Gate = _vm.GateFor(item.Id);
                }
            }

            // 2) WO-680 — the Village-Tier Unlock CTA seats ONLY on a band the VILLAGE gate is
            // blocking (vm.GateFor). A band locked by the building's own tier ladder shows its
            // requirement text alone — a village button there pointed at the WRONG gate (the
            // "Unlock Maxed" trap's twin). When no band is village-gated the control gets a
            // dedicated band at the top so the tech-gate is never lost. The VM emits NO village
            // tile at all once the gate is maxed, so hasVillageTier implies actionable.
            Band villageHost = null;
            if (hasVillageTier)
            {
                foreach (var b in bands)
                    if (b.TierNumber != int.MaxValue && b.Locked
                        && b.Gate == BuildingUpgradeVM.GateVillage) { villageHost = b; break; }

                if (villageHost == null)
                {
                    villageHost = new Band
                    {
                        Key = "villagetier",
                        Label = "VILLAGE TIER",
                        CrownName = "tier3",
                        TierNumber = -1,
                    };
                    bands.Insert(0, villageHost);
                }
            }

            // 3) Build the scroll + one block per band.
            var content = BuildScrollContent();
            foreach (var band in bands)
            {
                bool attachVillage = hasVillageTier && band == villageHost;
                CreateBand(content, band, attachVillage ? villageTier : (ItemVM?)null);
            }
            FinalizeScroll();
        }

        private static int TierNumber(string id)
        {
            int dash = id != null ? id.LastIndexOf('-') : -1;
            if (dash >= 0 && dash < id.Length - 1 && int.TryParse(id.Substring(dash + 1), out int n)) return n;
            return 1;
        }

        // ── Scroll host: vertical stack of band blocks ────────────────────────────

        private Transform BuildScrollContent()
        {
            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_bodyHost, false);
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
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(6, 6, 6, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

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
            Canvas.ForceUpdateCanvases();
            // Size every band grid's cells from the REAL content width (BandColumns fill it).
            float w = _bodyHost != null ? _bodyHost.rect.width : 0f;
            if (w > 1f)
            {
                float cell = (w - 12f - TileGapPx * (BandColumns - 1)) / BandColumns;
                if (cell > 1f)
                    foreach (var g in _tileGrids)
                        if (g != null) g.cellSize = new Vector2(cell, TileHeightPx);
            }
            if (_scrollContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        // ── One band block: header strip + tile grid ──────────────────────────────

        private void CreateBand(Transform parent, Band band, ItemVM? villageAction)
        {
            var root = new GameObject("Band_" + band.Key, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            CreateBandHeader(root.transform, band, villageAction);
            CreateBandGrid(root.transform, band);
        }

        private void CreateBandHeader(Transform parent, Band band, ItemVM? villageAction)
        {
            var header = new GameObject("BandHeader", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            var le = header.AddComponent<LayoutElement>();
            le.minHeight = BandHeaderPx;
            le.preferredHeight = BandHeaderPx;

            // Crown glyph (left) — RpgUi/crown/tier{n}; text-crest fallback keeps the band readable.
            Sprite crown = string.IsNullOrEmpty(band.CrownName)
                ? null : RpgUiCatalog.Get(RpgUiCatalog.RoleCrown, band.CrownName);
            if (crown != null)
            {
                var cg = new GameObject("Crown", typeof(Image));
                cg.transform.SetParent(header.transform, false);
                var crt = cg.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.005f, 0.08f); crt.anchorMax = new Vector2(0.065f, 0.92f);
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                var cimg = cg.GetComponent<Image>();
                cimg.sprite = crown; cimg.preserveAspect = true; cimg.raycastTarget = false;
            }

            // "TIER n" label.
            var lbl = ElarionUiKit.Label(header.transform, band.Label, 0.10f, 0.90f,
                ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.MidlineLeft,
                crown != null ? 0.075f : 0.01f, 0.45f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);

            // Thin gilt rule under the label band.
            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(header.transform, false);
            var rr = rule.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.01f, 0.02f); rr.anchorMax = new Vector2(0.99f, 0.06f);
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var rimg = rule.GetComponent<Image>();
            rimg.color = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.45f);
            rimg.raycastTarget = false;

            // Locked-band requirement hint (colorblind law: text, never hue).
            if (band.Locked && !string.IsNullOrEmpty(band.Requirement))
            {
                float reqX1 = villageAction.HasValue ? 0.66f : 0.98f;
                var req = ElarionUiKit.Label(header.transform, band.Requirement, 0.10f, 0.90f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineRight,
                    0.46f, reqX1);
                req.raycastTarget = false;
                ElarionUiKit.FitSingleLine(req);
            }

            // §2 — the ONE gold Unlock action for the tech-gate rides this band header.
            if (villageAction.HasValue)
            {
                string cost = _vm != null ? _vm.CostFor(BuildingUpgradeVM.VillageTierRowId) : "";
                BuildUnlockAction(header.transform, cost, () => _vm?.Select(BuildingUpgradeVM.VillageTierRowId));
            }
        }

        private void BuildUnlockAction(Transform parent, string costText, System.Action onClick)
        {
            var go = new GameObject("UnlockAction", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.68f, 0.14f); rt.anchorMax = new Vector2(0.985f, 0.86f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var plate = go.GetComponent<Image>();
            var gold = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonGold);
            if (gold != null) { plate.sprite = gold; plate.type = Image.Type.Sliced; plate.color = Color.white; }
            else
            {
                plate.color = ElarionUiKit.Cell;
                ElarionUiKit.ApplyRounded(plate);
                var outline = go.AddComponent<Outline>();
                outline.effectColor = ElarionUiKit.ObsidianTrim;
                outline.effectDistance = new Vector2(3f, 3f);
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.onClick.AddListener(() => onClick?.Invoke());

            string label = string.IsNullOrEmpty(costText) ? "Unlock" : ("Unlock  " + costText);
            var lbl = ElarionUiKit.Label(go.transform, label, 0.05f, 0.95f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        private void CreateBandGrid(Transform parent, Band band)
        {
            var gridGo = new GameObject("BandGrid", typeof(RectTransform));
            gridGo.transform.SetParent(parent, false);

            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = BandColumns;
            grid.spacing = new Vector2(TileGapPx, TileGapPx);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.cellSize = new Vector2(260f, TileHeightPx);   // corrected in FinalizeScroll

            var fitter = gridGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tileGrids.Add(grid);

            foreach (var item in band.Tiles)
                CreateTile(gridGo.transform, item);
        }

        // ── Perk TILE (presentation; data from the bound ItemVM) ──────────────────
        // slot_talent plate (sprite-first, ungated — WO-675 §3). Layout inside the tile:
        //   icon (top center) / name / effect one-liner / cost line (currency icon + value).
        // States: owned = lit + "UNLOCKED"; unlockable+affordable = rarity rim (§4);
        // locked = dimmed plate + requirement. Tap = _vm.Select(id) (unlock).

        private void CreateTile(Transform parent, ItemVM item)
        {
            var tile = new GameObject("PerkTile_" + item.Id, typeof(Image), typeof(Button));
            tile.transform.SetParent(parent, false);

            var plate = tile.GetComponent<Image>();
            DressTilePlate(plate);
            if (item.Equipped)
            {
                var c = plate.color;
                plate.color = new Color(c.r * OwnedTint.r, c.g * OwnedTint.g, c.b * OwnedTint.b, c.a);
            }
            else if (item.Locked)
            {
                var c = plate.color;
                plate.color = new Color(c.r * LockedTint.r, c.g * LockedTint.g, c.b * LockedTint.b, c.a * LockedTint.a);
            }

            var btn = tile.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = !item.Locked && !item.Equipped;
            // Owned/locked tiles are non-actionable -> drop the Selectable transition so they
            // show no hover/selection highlight (they read as settled state, not a CTA).
            if (!btn.interactable) btn.transition = Selectable.Transition.None;
            string id = item.Id;
            btn.onClick.AddListener(() => _vm?.Select(id));

            // WO-675 §4 — AFFORDANCE: the unlockable-now + affordable tile carries a rarity RIM
            // sprite overlay (sliced), replacing the old uGUI Outline. Fallback = gold Outline
            // so the affordance never vanishes when the rim art is absent.
            if (btn.interactable && item.Affordable)
            {
                var rim = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, AffordRimSprite);
                if (rim != null)
                {
                    var rimGo = new GameObject("AffordRim", typeof(Image));
                    rimGo.transform.SetParent(tile.transform, false);
                    var rrt = rimGo.GetComponent<RectTransform>();
                    rrt.anchorMin = new Vector2(-0.02f, -0.02f); rrt.anchorMax = new Vector2(1.02f, 1.02f);
                    rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
                    var rimImg = rimGo.GetComponent<Image>();
                    rimImg.sprite = rim; rimImg.type = Image.Type.Sliced; rimImg.color = Color.white;
                    rimImg.raycastTarget = false;
                }
                else
                {
                    var outline = tile.AddComponent<Outline>();
                    outline.effectColor = ElarionUiKit.ObsidianTrim;
                    outline.effectDistance = new Vector2(3f, 3f);
                }
            }

            float dim = item.Locked ? 0.55f : 1f;

            // WO-680 — the tier-N tile IS the "upgrade the building" key: it carries the crown
            // tier art (distinct from perk tiles) + a KeyLineFor sub-line ("UPGRADES FORGE TO
            // TIER 2") so the gate copy's target is visibly THIS tile. VM-composed string; the
            // View only lays it out.
            string keyLine = _vm != null ? _vm.KeyLineFor(item.Id) : "";
            bool hasKeyLine = !string.IsNullOrEmpty(keyLine);

            // ICON — perk sprite (WO-432 art), crown art for a tier tile (WO-680 distinct
            // treatment), or a numeral glyph fallback so the grid stays uniform.
            Sprite icon = null;
            if (item.IconRole == BuildingUpgradeVM.IconRolePerk && !string.IsNullOrEmpty(item.IconName))
                icon = Resources.Load<Sprite>("HudIcons/BuildingUpgrades/" + item.IconName);
            else if (item.IconRole == BuildingUpgradeVM.IconRoleTier
                     && item.Id != null && item.Id.StartsWith("tier-"))
                icon = RpgUiCatalog.Get(RpgUiCatalog.RoleCrown, "tier" + Mathf.Clamp(TierNumber(item.Id), 1, 3));
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(tile.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.32f, 0.50f); irt.anchorMax = new Vector2(0.68f, 0.94f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = icon;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                string glyph = TierGlyph(item.Id);
                var g = ElarionUiKit.Label(tile.transform, glyph, 0.50f, 0.94f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                g.raycastTarget = false;
                ElarionUiKit.FitSingleLine(g);
            }

            // NAME (a key-lined tile cedes a strip below the name to the WO-680 sub-line).
            var nameLbl = ElarionUiKit.Label(tile.transform, item.Name, hasKeyLine ? 0.375f : 0.345f, 0.50f,
                new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, dim),
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            nameLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nameLbl);

            // WO-680 KEY SUB-LINE — "UPGRADES FORGE TO TIER 2" (gilt, small caps-by-content).
            if (hasKeyLine)
            {
                var keyLbl = ElarionUiKit.Label(tile.transform, keyLine, 0.30f, 0.375f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
                keyLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(keyLbl);
            }

            // EFFECT — the one-line concrete payoff, from the perk data (VM-relayed).
            string effect = _vm != null ? _vm.EffectFor(item.Id) : "";
            var effLbl = ElarionUiKit.Label(tile.transform, effect, 0.20f, hasKeyLine ? 0.30f : 0.345f,
                new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.85f * dim),
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
            effLbl.raycastTarget = false;
            ElarionUiKit.FitBlock(effLbl);

            // BOTTOM LINE — owned: "UNLOCKED"; locked: the requirement; else the COST.
            string bottom;
            Color bottomColor;
            bool isCost = false;
            if (item.Equipped)
            {
                bottom = "UNLOCKED";
                bottomColor = ElarionUi.Gilt;
            }
            else if (item.Locked)
            {
                bottom = !string.IsNullOrEmpty(item.LockReason) ? item.LockReason : "Locked";
                bottomColor = ElarionUi.ParchmentDim;
            }
            else
            {
                bottom = _vm != null ? _vm.CostFor(item.Id) : "";
                bottomColor = item.Affordable ? ElarionUi.Affordable : ElarionUi.Danger;
                isCost = true;
                // Colorblind law: affordability was encoded by green-vs-red hue ALONE —
                // add a text cue ("Need ...") so the unaffordable state reads without hue.
                if (!item.Affordable && !string.IsNullOrEmpty(bottom)) bottom = "Need " + bottom;
            }

            // WO-675 §7 — a cost line gets a RpgUi/currency/* icon left of the value (name stays).
            Sprite costIcon = isCost ? CurrencyIconFor(bottom) : null;
            float textX0 = 0.04f;
            if (costIcon != null)
            {
                var cg = new GameObject("CostIcon", typeof(Image));
                cg.transform.SetParent(tile.transform, false);
                var crt = cg.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.10f, 0.045f); crt.anchorMax = new Vector2(0.26f, 0.185f);
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                var cimg = cg.GetComponent<Image>();
                cimg.sprite = costIcon; cimg.preserveAspect = true; cimg.raycastTarget = false;
                cimg.color = new Color(1f, 1f, 1f, dim);
                textX0 = 0.27f;
            }

            var botLbl = ElarionUiKit.Label(tile.transform, bottom, 0.05f, 0.20f,
                new Color(bottomColor.r, bottomColor.g, bottomColor.b, dim),
                ElarionUi.FontLabel,
                costIcon != null ? TMPro.TextAlignmentOptions.MidlineLeft : TMPro.TextAlignmentOptions.Center,
                textX0, 0.96f, bold: !item.Locked);
            botLbl.raycastTarget = false;
            if (item.Locked) ElarionUiKit.FitBlock(botLbl);
            else ElarionUiKit.FitSingleLine(botLbl);
        }

        // The currency icon for a cost line — the FIRST currency the string names (multi-currency
        // costs keep the full text; the icon is the leading cue). Null when nothing matches / art absent.
        private static Sprite CurrencyIconFor(string costText)
        {
            if (string.IsNullOrEmpty(costText)) return null;
            string c = costText.ToLowerInvariant();
            string name = null;
            // Order matters only when a string names several — pick the earliest-mentioned.
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

        private static string TierGlyph(string id)
        {
            // "tier-3" -> "3"; a research-perk id has no numeral -> the crest glyph.
            if (id != null && id.StartsWith("tier-"))
            {
                int dash = id.LastIndexOf('-');
                string n = dash >= 0 && dash < id.Length - 1 ? id.Substring(dash + 1) : "";
                return string.IsNullOrEmpty(n) ? "-" : n;
            }
            return ElarionUi.CrestGlyph;
        }

        private static void DressTilePlate(Image plateImg)
        {
            if (plateImg == null) return;
            // WO-675 §3 — sprite-first ALWAYS (canon §5): the talent slot plate, ungated.
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, SlotTalentPlate);
            if (plate != null)
            {
                plateImg.sprite = plate;
                plateImg.type   = Image.Type.Sliced;
                plateImg.color  = Color.white;
                return;
            }
            // Procedural fallback (art absent) — the panel never blanks.
            plateImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(plateImg);
        }

        // Smooth hover/press: keep the kit ColorTint block but give it a real fade
        // (never snap) — owner smoothness directive 2026-07-02.
        private static void SoftenButton(Button btn)
        {
            if (btn == null || btn.transition != Selectable.Transition.ColorTint) return;
            var colors = btn.colors;
            colors.fadeDuration = ButtonFadeSec;
            btn.colors = colors;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearContent()
        {
            _scrollContent = null;
            _tileGrids.Clear();
            if (_bodyHost == null) return;
            for (int i = _bodyHost.childCount - 1; i >= 0; i--)
            {
                var c = _bodyHost.GetChild(i);
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
                // Eased close (owner smoothness directive): the dying canvas fades/scales out on
                // its own FX component, then destroys itself — panel state is already cleared, so
                // an immediate re-Open builds a fresh canvas without waiting.
                var fx = _ui.GetComponent<PanelOpenCloseFx>();
                if (fx != null && fx.isActiveAndEnabled) fx.PlayCloseAndDestroy();
                else Destroy(_ui);
            }
            _ui = null;
            _bodyHost = null;
            _scrollContent = null;
            _tileGrids.Clear();
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
    /// DEPRECATED private twin (WO-714 P8, 2026-07-13): the kit now owns this tween as
    /// <c>ElarionUiKit.PanelOpenCloseFx</c> (+ AttachPanelOpenFx / ClosePanelWithFx) — new
    /// code uses the kit version; this copy is kept only so tonight's parallel lanes stay
    /// additive, and migrates on-touch. Original: minimal shared open/close tween for THIS
    /// panel family. Ease-out scale 0.92-&gt;1 + fade-in on open (~0.18s); ease-in fade/
    /// scale-out then self-destroy on close (~0.14s). Unscaled time (panels open while
    /// gameplay may be paused); CanvasGroup blocks input while closing.
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
                // open = ease-OUT cubic; close = ease-IN cubic (owner-specified feel).
                float k = open ? 1f - Mathf.Pow(1f - x, 3f) : 1f - Mathf.Pow(x, 3f);
                if (_group != null) _group.alpha = k;
                // open: k 0->1 grows 0.92->1; close: k 1->0 shrinks 1->0.94 (panel rect, not
                // the canvas root — scale on an overlay canvas root does not render).
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
