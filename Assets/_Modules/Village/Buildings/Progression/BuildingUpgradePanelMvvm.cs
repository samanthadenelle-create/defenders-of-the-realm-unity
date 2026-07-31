// =============================================================================
// BuildingUpgradePanelMvvm — the building ENHANCEMENT panel VIEW (MVVM).
// A DUMB SKIN over clean uGUI chrome that BINDS a BuildingUpgradeVM. ALL
// state/logic (affordability, unlock, tier gating, perks) lives in the VM — the
// View never reads game state.  Namespace: DeNelle.Village.Buildings.Progression
// Assembly: DeNelle.Village
//
// MASTER-DETAIL REDESIGN (owner-approved mockup 2026-07-17, Screenshot 060241 —
// supersedes the vertical Tier-1/2/3 list AND the ornate carved-stone frame that
// made the panel read as a mess).  The owner wants the OBSIDIAN palette (dark
// near-black panel + runic gold + green accents) rendered CLEAN — a flat rounded
// dark rectangle with thin subtle borders, NOT the ornate FrameTalent stone
// chrome.  So this View builds its OWN clean container (code-built uGUI, no UXML,
// no BuildObsidianPanel) and only borrows the kit's clean primitives (Label,
// AddImage/ApplyRounded, StyleButtonColors, FitSingleLine/FitBlock).
//
// LAYOUT (matches 060241 exactly, BUILDING-AGNOSTIC — every field is VM data):
//   HEADER   : shield medallion top-left + centered GOLD "<Building> Enhancements".
//   CURRENCY : a row of pill chips (icon left, value right) — the spendable set.
//   TABS     : "Upgrade" (gold-filled when selected) | "Skills" (dark). 50/50.
//   BODY (Upgrade tab) = TWO COLUMNS master-detail:
//     LEFT  "ENHANCEMENT PATH" — a HORIZONTAL row of tier CARDS (arrows between),
//           each: "TIER n" + a per-tier BUILDING ILLUSTRATION (grows/changes per
//           tier) + perk NAME + one-line EFFECT + a button (gold Upgrade when
//           available / lock "Unlock '<prev>'" when locked / "Unlocked" when owned).
//           Arrow before a reachable tier is gold, grey otherwise.
//     RIGHT  DETAIL pane for the SELECTED tier — perk NAME + "TIER n - SELECTED"
//           + a BENEFIT LIST (green-check active vs dim-box locked, colorblind
//           law: glyph + luminance + text, never hue alone) + "UPGRADE COST" chips
//           + a big gold Upgrade CTA. (No Hotkeys row — mobile game; removed 2026-07-19.)
//   BODY (Skills tab) = the per-tier RESEARCH PERKS as a scroll list (unchanged
//           row grammar), so the Skills side keeps its existing content/behaviour.
//   FOOTER   : one centered gold-bordered "Close".
//
// Tapping a tier card SELECTS it (right pane repaints); the CTA / card Upgrade
// button routes the SAME vm.Select(id) command.  ONE panel serves EVERY upgradable
// building (city tiers + legacy resource buildings) — nothing here is per-building.
// SHIPS behind FeatureFlags.BuildingUpgradePanel (default ON since WO-476).
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        // ── Clean obsidian palette (dark near-black + runic gold + green accent) ──
        private static readonly Color PanelFill    = new Color(0.043f, 0.041f, 0.049f, 0.985f); // near-black obsidian
        private static readonly Color SubPanelFill = new Color(0.055f, 0.052f, 0.060f, 1f);      // left/right body sub-panels
        private static readonly Color CardFill     = new Color(0.078f, 0.073f, 0.066f, 1f);      // normal tier card
        private static readonly Color CardFillLit  = new Color(0.140f, 0.108f, 0.048f, 1f);      // selected / available card (warm)
        private static readonly Color CardFillDim  = new Color(0.052f, 0.050f, 0.055f, 1f);      // locked tier card
        private static readonly Color TabDark      = new Color(0.085f, 0.082f, 0.078f, 1f);      // unselected tab
        private static readonly Color PillFill     = new Color(0.062f, 0.059f, 0.055f, 1f);      // currency pill
        private static readonly Color BorderDim    = new Color(0.42f, 0.40f, 0.36f, 0.45f);      // subtle rule
        private static readonly Color BorderGold   = new Color(0.831f, 0.686f, 0.216f, 1f);      // gold rim (selected)
        private static readonly Color BorderGoldDim= new Color(0.58f, 0.48f, 0.22f, 0.75f);      // gold rim (available)

        // Committed currency-icon role folder (Resources/RpgUi/currency/currency_*).
        private const string CurrencyRole = "currency";

        private BuildingUpgradeVM _vm;

        private GameObject _ui;
        private RectTransform _bodyHost;          // content host (below tab row)
        private GameObject _upgradePage;          // Upgrade page root (two-column master-detail)
        private GameObject _skillsPage;           // Skills page root (scroll list)
        private RectTransform _pathCardsHost;     // LEFT column — tier cards + arrows (rebuilt on select)
        private RectTransform _detailHost;        // RIGHT column — selected-tier detail (rebuilt on select)
        private RectTransform _skillsContent;     // Skills-tab scroll content (perk rows)
        private int _activeTab;                   // 0 = Upgrade, 1 = Skills

        // Selection state — which tier card the right detail pane is showing.
        private string _selectedTierId;

        // Custom clean currency pills (built once; values refreshed on each Render).
        private struct PillRef { public ElarionUiKit.CurrencyKind Kind; public TMPro.TextMeshProUGUI Value; }
        private readonly List<PillRef> _pills = new List<PillRef>();

        // Tab visuals (restyled per active tab).
        private struct TabRef { public Image Fill; public TMPro.TextMeshProUGUI Label; }
        private readonly List<TabRef> _tabs = new List<TabRef>();

        // Status is transient: toast only NEW statuses, never the open-time baseline.
        private string _lastStatus;

        // ── Render dedup (WO fix 2026-07-19) ──────────────────────────────────────
        // EconomyService.OnChanged fires after EVERY mutation (passive income ticks,
        // pet/outpost harvest, etc.) -> BuildingUpgradeVM re-raises Changed EVERY tick ->
        // this View re-ran a FULL destroy+rebuild of every tier card + skills row each
        // tick. That churned the layout (visual jitter), drained perf, and re-armed the
        // one-shot UiKitTextFitGuard on every rebuilt label EVERY frame (the "band too
        // short" warning spam). We now hash the RENDERED state (perks + selection +
        // affordability + effect/cost strings); the expensive rebuild runs ONLY when that
        // hash actually changes. Cheap per-tick work (pill values, status toast) still runs.
        private string _lastContentSig;

        // Building-portrait cache (portraits import as plain Texture2D — wrap once).
        private static readonly Dictionary<string, Sprite> _portraitCache = new Dictionary<string, Sprite>();

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        private const float RowHeightPx   = 132f;   // Skills-tab perk row height
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
            _selectedTierId = null;   // fresh open -> default-select the next upgradeable tier

            BuildChrome();

            Bind(_vm);

            FlowTrace.Step("UpgradeUI", "open '" + (_vm != null ? _vm.Title : "?")
                + "' master-detail (Upgrade+Skills), tab=" + _activeTab);

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

        // Queue-state repaint (F8 2026-07-30): the CTA now reads the live builder gates, so
        // when a job starts/finishes WHILE this panel is open the button must re-resolve
        // (busy countdown -> Upgrade, or vice versa). Poll the published queue snapshot's
        // Version — the same change-detect seam the HUD chip uses; a repaint only fires on
        // an actual publish, never per-frame.
        private int _queueVersionSeen;
        private void Update()
        {
            if (_vm == null) return;
            var st = DeNelle.Core.UI.ObsidianQueueGate.Status;
            if (st.Version != _queueVersionSeen) { _queueVersionSeen = st.Version; Render(); }
        }

        // ── Render: repaint from vm.* ONLY ────────────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;

            // Refresh the currency pills (plain set — no red/green flash, colorblind law).
            // Only assign when the value string actually changed — avoids a TMP mesh regen
            // on every idle income tick.
            for (int i = 0; i < _pills.Count; i++)
                if (_pills[i].Value != null)
                {
                    string v = ElarionUi.CompactNumber(WalletValue(_pills[i].Kind));
                    if (_pills[i].Value.text != v) _pills[i].Value.text = v;
                }

            // Status is transient — pop a toast only when it CHANGES to a new, non-empty message.
            string status = _vm.Status;
            if (!string.IsNullOrEmpty(status) && status != _lastStatus)
            {
                _lastStatus = status;
                BuildFeedbackToast.Show(status);
            }

            // EVENT-DRIVEN rebuild: the full card/skills teardown+rebuild (and the fit-guard
            // re-arm it triggers) runs ONLY when the rendered state changed, not every tick.
            string sig = ContentSignature();
            if (sig != _lastContentSig)
            {
                RebuildUpgrade();
                RebuildSkills();
                // RebuildUpgrade resolves _selectedTierId internally, so re-hash AFTER the
                // rebuild to capture the settled selection — otherwise the first income tick
                // would see a changed sig and rebuild once more for nothing.
                _lastContentSig = ContentSignature();
            }

            RestyleTabs();
            ApplyTabVisibility();
        }

        // Hash of everything the tier cards / detail pane / skills rows render from, so
        // Render can skip the expensive rebuild when nothing visible actually changed.
        // Includes affordability + effect/cost strings so a genuine state flip (e.g. income
        // crosses a cost threshold -> a button becomes enabled) still repaints exactly once.
        private string ContentSignature()
        {
            if (_vm == null) return "";
            var sb = new StringBuilder(256);
            sb.Append(_selectedTierId).Append('|').Append(_vm.Title);
            foreach (var item in _vm.Perks)
            {
                sb.Append('#').Append(item.Id)
                  .Append(';').Append(item.Name)
                  .Append(';').Append(item.Equipped ? '1' : '0')
                  .Append(item.Locked ? '1' : '0')
                  .Append(item.Affordable ? '1' : '0')
                  .Append(';').Append(item.LockReason)
                  .Append(';').Append(_vm.EffectFor(item.Id))
                  .Append(';').Append(_vm.CostFor(item.Id));
            }
            return sb.ToString();
        }

        // Tapping a tier card selects it (immediate repaint of the left cards + right detail).
        // Refreshes the dedup cache so the next income-driven Render doesn't redundantly rebuild.
        private void SelectTier(string id)
        {
            _selectedTierId = id;
            FlowTrace.Step("UpgradeUI", "select " + id);
            RebuildUpgrade();
            _lastContentSig = ContentSignature();
        }

        // ── Chrome — CLEAN flat dark panel (no ornate frame) + zones ──────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("BuildingUpgradePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            string titleText = (_vm != null ? _vm.Title : "Building") + " Enhancements";

            // CLEAN flat obsidian panel: a rounded near-black rectangle with a thin subtle
            // gold rim + corner rivets (mockup 060241) — NOT the ornate stone frame.
            RectTransform panel = RoundedCard(_ui.transform, "Panel",
                new Vector2(0.035f, 0.05f), new Vector2(0.965f, 0.95f),
                PanelFill, new Color(BorderGold.r, BorderGold.g, BorderGold.b, 0.32f), 2.5f);
            AddCornerRivets(panel);

            // HEADER — shield medallion (top-left) + centered gold title.
            BuildMedallion(panel);
            var title = ElarionUiKit.Label(panel, titleText, 0.905f, 0.995f,
                ElarionUi.Gilt, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center,
                0.11f, 0.89f, bold: true);
            title.raycastTarget = false;
            ElarionUiKit.FitSingleLine(title);

            // CURRENCY pill row.
            var walletStrip = MakeZone(panel, "CurrencyRow", new Vector2(0.012f, 0.815f), new Vector2(0.988f, 0.888f));
            BuildCurrencyPills(walletStrip);

            // TAB row: Upgrade | Skills.
            var tabHost = MakeZone(panel, "TabRow", new Vector2(0.012f, 0.720f), new Vector2(0.988f, 0.800f));
            BuildTabs(tabHost);

            // BODY host (below tabs, above footer).
            _bodyHost = MakeZone(panel, "BodyHost", new Vector2(0.012f, 0.095f), new Vector2(0.988f, 0.705f));
            _upgradePage = BuildUpgradePage(_bodyHost);
            _skillsPage  = BuildScrollPage(_bodyHost, "SkillsPage", out _skillsContent);
            ApplyTabVisibility();

            // FOOTER — one centered gold-bordered Close.
            BuildCloseButton(panel);

            // Capture the open-time status as the toast baseline (do NOT toast the idle hint).
            _lastStatus = _vm != null ? _vm.Status : null;

            // Eased open: scale 0.92->1 + fade 0->1, ease-out (scale the outer card incl. border).
            var fx = _ui.AddComponent<PanelOpenCloseFx>();
            fx.PlayOpen((panel.parent as RectTransform) ?? panel);
        }

        // Small shield medallion disc, top-left (mockup 060241).
        private void BuildMedallion(RectTransform panel)
        {
            RectTransform disc = RoundedCard(panel, "Medallion",
                new Vector2(0.014f, 0.905f), new Vector2(0.058f, 0.985f),
                new Color(0.10f, 0.075f, 0.03f, 1f), BorderGoldDim, 2.5f);
            var shield = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
            if (shield != null)
            {
                var g = new GameObject("Shield", typeof(Image));
                g.transform.SetParent(disc, false);
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.18f, 0.18f); rt.anchorMax = new Vector2(0.82f, 0.82f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.sprite = shield; img.preserveAspect = true; img.raycastTarget = false;
                img.color = ElarionUi.Gilt;
            }
            else
            {
                var glyph = ElarionUiKit.Label(disc, ElarionUi.CrestGlyph, 0.10f, 0.90f,
                    ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                glyph.raycastTarget = false;
                ElarionUiKit.FitSingleLine(glyph);
            }
        }

        // Four subtle corner rivet dots (mockup 060241 flat-panel detail).
        private static void AddCornerRivets(RectTransform panel)
        {
            var pts = new[]
            {
                new Vector2(0.012f, 0.972f), new Vector2(0.988f, 0.972f),
                new Vector2(0.012f, 0.020f), new Vector2(0.988f, 0.020f)
            };
            foreach (var p in pts)
            {
                var go = ElarionUiKit.AddImage(panel, "Rivet",
                    new Vector2(p.x - 0.006f, p.y - 0.010f), new Vector2(p.x + 0.006f, p.y + 0.010f),
                    new Color(0.30f, 0.28f, 0.25f, 0.9f));
                go.GetComponent<Image>().raycastTarget = false;
            }
        }

        private void BuildCloseButton(RectTransform panel)
        {
            RectTransform frame = RoundedCard(panel, "Close",
                new Vector2(0.40f, 0.018f), new Vector2(0.60f, 0.082f),
                new Color(0.10f, 0.095f, 0.088f, 1f), BorderGold, 2.5f);
            var host = frame.parent as RectTransform ?? frame;   // bordered outer carries the button
            var b = host.gameObject.AddComponent<Button>();
            b.targetGraphic = host.GetComponent<Image>();
            ElarionUiKit.StyleButtonColors(b);
            SoftenButton(b);
            b.onClick.AddListener(() => { FlowTrace.Step("UpgradeUI", "close"); _vm?.Close(); });
            var lbl = ElarionUiKit.Label(frame, "Close", 0.10f, 0.90f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        // ── Currency pill row (clean: dark rounded pill, icon left, value right) ──

        private void BuildCurrencyPills(RectTransform strip)
        {
            _pills.Clear();
            if (strip == null) return;

            var kinds = DeriveSpendableCurrencies();
            int n = kinds.Count;
            if (n == 0) return;

            const float gap = 0.01f;
            for (int i = 0; i < n; i++)
            {
                float x0 = (float)i / n + (i == 0 ? 0f : gap * 0.5f);
                float x1 = (float)(i + 1) / n - (i == n - 1 ? 0f : gap * 0.5f);
                BuildCurrencyPill(strip, kinds[i], x0, x1);
            }
        }

        private void BuildCurrencyPill(RectTransform strip, ElarionUiKit.CurrencyKind kind, float x0, float x1)
        {
            RectTransform pill = RoundedCard(strip, "Pill_" + kind,
                new Vector2(x0, 0.06f), new Vector2(x1, 0.94f), PillFill, BorderDim, 1.5f);

            float textX0 = 0.08f;
            var icon = RpgUiCatalog.Get(CurrencyRole, CurrencyIconName(kind));
            if (icon != null)
            {
                var g = new GameObject("Icon", typeof(Image));
                g.transform.SetParent(pill, false);
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.06f, 0.18f); rt.anchorMax = new Vector2(0.26f, 0.82f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
                textX0 = 0.30f;
            }

            long v = WalletValue(kind);
            Color valColor = kind == ElarionUiKit.CurrencyKind.Gold ? ElarionUi.Gilt : ElarionUi.Parchment;
            var val = ElarionUiKit.Label(pill, ElarionUi.CompactNumber(v), 0.10f, 0.90f,
                valColor, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineRight, textX0, 0.92f, bold: true);
            val.raycastTarget = false;
            ElarionUiKit.FitSingleLine(val);
            _pills.Add(new PillRef { Kind = kind, Value = val });
        }

        private static string CurrencyIconName(ElarionUiKit.CurrencyKind kind)
        {
            switch (kind)
            {
                case ElarionUiKit.CurrencyKind.Gold:    return "currency_gold";
                case ElarionUiKit.CurrencyKind.Wood:    return "currency_wood";
                case ElarionUiKit.CurrencyKind.Food:    return "currency_food";
                case ElarionUiKit.CurrencyKind.Iron:    return "currency_iron";
                case ElarionUiKit.CurrencyKind.Crystal: return "currency_crystal";
                default:                                return "currency_gold";
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
                list.Add(ElarionUiKit.CurrencyKind.Crystal);
            }
            return list;
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

        // ── Tab row (Upgrade gold-filled when selected, Skills dark) ───────────────

        private void BuildTabs(RectTransform host)
        {
            _tabs.Clear();
            string[] labels = { "Upgrade", "Skills" };
            const float gap = 0.012f;
            for (int i = 0; i < labels.Length; i++)
            {
                float x0 = (float)i / labels.Length + (i == 0 ? 0f : gap * 0.5f);
                float x1 = (float)(i + 1) / labels.Length - (i == labels.Length - 1 ? 0f : gap * 0.5f);
                RectTransform fill = RoundedCard(host, "Tab_" + labels[i],
                    new Vector2(x0, 0.06f), new Vector2(x1, 0.94f), TabDark, BorderDim, 1.5f);
                var root = fill.parent as RectTransform;   // bordered outer carries the button
                var btn = root.gameObject.AddComponent<Button>();
                btn.targetGraphic = root.GetComponent<Image>();
                ElarionUiKit.StyleButtonColors(btn);
                SoftenButton(btn);
                int idx = i;
                btn.onClick.AddListener(() => OnTab(idx));
                var lbl = ElarionUiKit.Label(fill, labels[i], 0.10f, 0.90f,
                    ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                lbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(lbl);
                _tabs.Add(new TabRef { Fill = fill.GetComponent<Image>(), Label = lbl });
            }
            RestyleTabs();
        }

        private void RestyleTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool sel = i == _activeTab;
                if (_tabs[i].Fill != null)
                    _tabs[i].Fill.color = sel ? ElarionUi.GoldButton : TabDark;
                if (_tabs[i].Label != null)
                    _tabs[i].Label.color = sel ? ElarionUi.Ink : ElarionUi.Parchment;
            }
        }

        private void OnTab(int index)
        {
            _activeTab = Mathf.Clamp(index, 0, 1);
            FlowTrace.Step("UpgradeUI", "tab -> " + (_activeTab == 0 ? "Upgrade" : "Skills"));
            RestyleTabs();
            ApplyTabVisibility();
        }

        private void ApplyTabVisibility()
        {
            if (_upgradePage != null) _upgradePage.SetActive(_activeTab == 0);
            if (_skillsPage  != null) _skillsPage.SetActive(_activeTab == 1);
        }

        // ── Upgrade page — TWO-COLUMN master-detail (left path, right detail) ─────

        private GameObject BuildUpgradePage(Transform parent)
        {
            var page = new GameObject("UpgradePage", typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var prt = (RectTransform)page.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            // LEFT sub-panel (~65%) — "ENHANCEMENT PATH".
            RectTransform left = RoundedCard(page.transform, "PathPanel",
                new Vector2(0f, 0f), new Vector2(0.655f, 1f), SubPanelFill, BorderDim, 1.5f);
            var pathTitle = ElarionUiKit.Label(left, "ENHANCEMENT PATH", 0.905f, 0.985f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            pathTitle.characterSpacing = 6f;
            pathTitle.raycastTarget = false;
            _pathCardsHost = MakeZone(left, "CardsHost", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.885f));

            // RIGHT sub-panel (~35%) — DETAIL for the selected tier.
            RectTransform right = RoundedCard(page.transform, "DetailPanel",
                new Vector2(0.668f, 0f), new Vector2(1f, 1f), SubPanelFill, BorderDim, 1.5f);
            _detailHost = MakeZone(right, "DetailHost", new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.985f));

            return page;
        }

        // Repaint the whole Upgrade tab from vm.* + the current selection.
        private void RebuildUpgrade()
        {
            if (_vm == null || _pathCardsHost == null || _detailHost == null) return;

            var tiers = new List<ItemVM>();
            foreach (var item in _vm.Perks)
                if (item.Id != null && item.Id.StartsWith("tier-"))
                    tiers.Add(item);

            _selectedTierId = ResolveSelected(tiers);

            ClearChildren(_pathCardsHost);
            ClearChildren(_detailHost);

            if (tiers.Count == 0)
            {
                EmptyNote(_pathCardsHost, "This building has no enhancement path yet.");
                return;
            }

            BuildPathCards(tiers);
            BuildDetail(tiers);
        }

        // Keep the current selection if still valid; else default to the next upgradeable tier.
        private string ResolveSelected(List<ItemVM> tiers)
        {
            if (_selectedTierId != null)
                foreach (var t in tiers) if (t.Id == _selectedTierId) return _selectedTierId;

            string firstNonOwned = null, firstAvailable = null, last = null;
            foreach (var t in tiers)
            {
                last = t.Id;
                if (!t.Equipped && firstNonOwned == null) firstNonOwned = t.Id;
                if (!t.Equipped && !t.Locked && firstAvailable == null) firstAvailable = t.Id;
            }
            return firstAvailable ?? firstNonOwned ?? last;
        }

        // ── LEFT column: horizontal tier cards with arrows between ────────────────

        private void BuildPathCards(List<ItemVM> tiers)
        {
            int n = tiers.Count;
            const float pad = 0.012f;
            float arrowFrac = n > 1 ? 0.05f : 0f;
            float cardFrac = (1f - 2f * pad - arrowFrac * (n - 1)) / n;
            if (cardFrac <= 0f) cardFrac = (1f - 2f * pad) / n;

            float x = pad;
            for (int i = 0; i < n; i++)
            {
                BuildTierCard(tiers, i, x, x + cardFrac);
                x += cardFrac;
                if (i < n - 1)
                {
                    // Arrow before a reachable tier is gold; grey until the prior tier is reached.
                    bool gold = !tiers[i].Locked;   // owned OR the next-available tier
                    var arrow = ElarionUiKit.Label(_pathCardsHost, ">", 0.42f, 0.62f,
                        gold ? ElarionUi.Gilt : new Color(0.42f, 0.42f, 0.42f, 1f),
                        ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, x, x + arrowFrac, bold: true);
                    arrow.raycastTarget = false;
                    x += arrowFrac;
                }
            }
        }

        private void BuildTierCard(List<ItemVM> tiers, int index, float xMin, float xMax)
        {
            ItemVM item = tiers[index];
            bool selected  = item.Id == _selectedTierId;
            bool owned     = item.Equipped;
            bool locked    = item.Locked;
            bool available = !locked && !owned;   // the next upgradeable tier (gold affordance)
            float dim = locked ? 0.55f : 1f;

            Color fill   = (selected || available) ? CardFillLit : (locked ? CardFillDim : CardFill);
            Color border = selected ? BorderGold : (available ? BorderGoldDim : BorderDim);
            float borderPx = selected ? 3f : (available ? 2f : 1.5f);

            RectTransform card = RoundedCard(_pathCardsHost, "TierCard_" + item.Id,
                new Vector2(xMin, 0.03f), new Vector2(xMax, 0.97f), fill, border, borderPx);

            // Whole card selects it (right pane repaints).
            var root = card.parent as RectTransform;
            var selBtn = root.gameObject.AddComponent<Button>();
            selBtn.targetGraphic = root.GetComponent<Image>();
            ElarionUiKit.StyleButtonColors(selBtn);
            SoftenButton(selBtn);
            string id = item.Id;
            selBtn.onClick.AddListener(() => SelectTier(id));

            // "TIER n" header.
            var head = ElarionUiKit.Label(card, TierHeader(item), 0.865f, 0.955f,
                new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            head.characterSpacing = 4f;
            head.raycastTarget = false;
            ElarionUiKit.FitSingleLine(head);

            // BUILDING ICON — the SAME building art at the SAME size on EVERY tier (owner
            // 2026-07-17: buildings do NOT visually change per tier; a tier upgrade just unlocks
            // perks + maybe structure HP). Nothing here implies the model grows. The "TIER n"
            // header is the tier badge. (BuildingArt keeps a per-tier variant lookup that is a
            // NO-OP for buildings — no -2/-3 portraits exist — and only serves TOWERS if they are
            // later routed to this panel, since towers DO carry real per-tier art.)
            int tierNum = TierNumber(item.Id);
            var art = BuildingArt(tierNum);
            if (art != null)
            {
                var g = new GameObject("Building", typeof(Image));
                g.transform.SetParent(card, false);
                var rt = g.GetComponent<RectTransform>();
                // Illustration shrunk (was 0.44..0.82) to free vertical room below for the
                // perk name + effect to WRAP instead of ellipsize.
                rt.anchorMin = new Vector2(0.20f, 0.56f); rt.anchorMax = new Vector2(0.80f, 0.855f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.sprite = art; img.preserveAspect = true; img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                // Consistent neutral placeholder (identical on every tier — no growth): a crest glyph.
                var gl = ElarionUiKit.Label(card, ElarionUi.CrestGlyph, 0.56f, 0.855f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.7f * dim),
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.16f, 0.84f, bold: true);
                gl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(gl);
            }

            // Perk NAME — WRAPPING block (was a single-line ellipsize: narrow cards cut
            // "Reinforced Blades" mid-word). Taller band + FitBlock lets it wrap to 2 lines.
            var nameLbl = ElarionUiKit.Label(card, CardName(item), 0.37f, 0.545f,
                new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, dim),
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            nameLbl.raycastTarget = false;
            ElarionUiKit.FitBlock(nameLbl);

            // EFFECT — WRAPPING block (was too short: "Wood productio[n]" clipped). Taller
            // band + full width so the effect copy wraps to a readable 2-3 lines.
            string effect = _vm != null ? _vm.EffectFor(item.Id) : "";
            if (!string.IsNullOrEmpty(effect))
            {
                var eff = ElarionUiKit.Label(card, effect, 0.145f, 0.36f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.85f * dim),
                    ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
                eff.raycastTarget = false;
                ElarionUiKit.FitBlock(eff);
            }

            // BUTTON.
            if (owned)
            {
                var tag = ElarionUiKit.Label(card, "Unlocked", 0.03f, 0.115f,
                    ElarionUi.Affordable, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                tag.raycastTarget = false;
                ElarionUiKit.FitSingleLine(tag);
            }
            else if (available)
            {
                BuildGoldButton(card, "Upgrade", item.Affordable, 0.14f, 0.86f, 0.02f, 0.125f,
                    () => { FlowTrace.Step("UpgradeUI", "upgrade " + id); _vm?.Select(id); });
            }
            else
            {
                // Locked — a dim lock button carrying the requirement (colorblind: glyph + text, not hue).
                string reason = !string.IsNullOrEmpty(item.LockReason) ? item.LockReason : "Locked";
                BuildLockButton(card, reason, 0.06f, 0.94f, 0.02f, 0.125f,
                    () => SelectTier(id));
            }
        }

        // ── RIGHT column: the selected tier's detail pane ─────────────────────────

        private void BuildDetail(List<ItemVM> tiers)
        {
            ItemVM sel = default;
            bool found = false;
            int selNum = 0;
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i].Id == _selectedTierId) { sel = tiers[i]; found = true; selNum = TierNumber(tiers[i].Id); }
            if (!found) return;

            // NAME + subtitle.
            var name = ElarionUiKit.Label(_detailHost, CardName(sel), 0.905f, 0.98f,
                ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            name.raycastTarget = false;
            ElarionUiKit.FitSingleLine(name);

            var sub = ElarionUiKit.Label(_detailHost, TierHeader(sel) + " - SELECTED", 0.852f, 0.902f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            sub.characterSpacing = 4f;
            sub.raycastTarget = false;
            ElarionUiKit.FitSingleLine(sub);

            // BENEFIT LIST — active (green check) up to the selected tier, locked (dim box) above.
            var lines = new List<(bool active, string text)>();
            // The selected tier's own name + concrete effect read as its delivered benefits.
            string selName = CardName(sel);
            if (!string.IsNullOrEmpty(selName)) lines.Add((true, selName));
            string selEffect = _vm != null ? _vm.EffectFor(sel.Id) : "";
            if (!string.IsNullOrEmpty(selEffect)) lines.Add((true, selEffect));
            // Lower owned tiers below the selected one contribute their effect as active benefits.
            foreach (var t in tiers)
            {
                int num = TierNumber(t.Id);
                if (num < selNum && t.Equipped)
                {
                    string e = _vm.EffectFor(t.Id);
                    if (!string.IsNullOrEmpty(e)) lines.Add((true, e));
                }
            }
            // Higher tiers are future/locked previews.
            foreach (var t in tiers)
            {
                int num = TierNumber(t.Id);
                if (num > selNum)
                {
                    string e = _vm.EffectFor(t.Id);
                    string preview = "Opens " + CardName(t) + (string.IsNullOrEmpty(e) ? "" : " (" + e + ")");
                    lines.Add((false, preview));
                }
            }

            // Taller rows (was 0.092) so a long preview line like "Opens Reinforced Blades
            // (Wood production +25%)" WRAPS to ~2 legible lines instead of ellipsizing.
            const float listTop = 0.845f, listBot = 0.455f, rowH = 0.126f;
            int maxRows = Mathf.Max(1, Mathf.FloorToInt((listTop - listBot) / rowH));
            int shown = Mathf.Min(lines.Count, maxRows);
            for (int i = 0; i < shown; i++)
            {
                float yTop = listTop - i * rowH;
                float yBot = yTop - (rowH - 0.012f);
                BuildBenefitRow(_detailHost, yBot, yTop, lines[i].active, lines[i].text);
            }

            // Divider.
            var div = ElarionUiKit.AddImage(_detailHost, "Divider",
                new Vector2(0.03f, 0.437f), new Vector2(0.97f, 0.443f), BorderDim);
            div.GetComponent<Image>().raycastTarget = false;

            // UPGRADE COST.
            var costLbl = ElarionUiKit.Label(_detailHost, "UPGRADE COST", 0.372f, 0.428f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            costLbl.characterSpacing = 4f;
            costLbl.raycastTarget = false;

            var costZone = MakeZone(_detailHost, "CostZone", new Vector2(0.04f, 0.255f), new Vector2(0.96f, 0.362f));
            string cost = _vm != null ? _vm.CostFor(sel.Id) : "";
            BuildCostChips(costZone, cost, 0.04f, 0.96f);

            // BIG CTA — Upgrade / Raise Village Tier / Unlocked / Locked reason.
            BuildDetailCta(_detailHost, tiers, sel, selNum);

            // NOTE: the decorative "Hotkeys  B  ^" key-letter row was REMOVED (2026-07-19) —
            // this is a mobile (Android) game, keyboard hotkeys have no player value and the
            // letters read as noise. No keybinding logic lived here (badges were display-only).
        }

        private void BuildDetailCta(RectTransform host, List<ItemVM> tiers, ItemVM sel, int selNum)
        {
            // The NEXT reachable tier = first non-owned tier in the ladder.
            string nextId = null;
            foreach (var t in tiers) { if (!t.Equipped) { nextId = t.Id; break; } }

            const float x0 = 0.04f, x1 = 0.96f, yb = 0.115f, yt = 0.235f;
            string selId = sel.Id;

            if (sel.Equipped)
            {
                BuildGoldButton(host, "Unlocked", false, x0, x1, yb, yt, null);
                return;
            }
            if (sel.Id == nextId)
            {
                string gate = _vm != null ? _vm.GateFor(sel.Id) : "";
                if (gate == BuildingUpgradeVM.GateVillage)
                {
                    // Village-gated next tier: the CTA raises the global Village Tier (the mechanism
                    // that opens this tier). Routes to the SOLE VillageTierService caller in the VM.
                    BuildGoldButton(host, "Raise Village Tier", true, x0, x1, yb, yt,
                        () => { FlowTrace.Step("UpgradeUI", "raise-village from " + selId); _vm?.Select(BuildingUpgradeVM.VillageTierRowId); });
                    return;
                }

                // PRE-TAP REASON (owner 2026-07-30 "some way to tell user why they cannot click
                // yet"): the old CTA enabled purely on affordability, so a busy-builders state
                // read as an unexplained dead button (or, on tap, a wrong "can't afford"). Read
                // the SAME timer gates the VM's tap-path mirrors and grey the button with the
                // live reason instead — CoC behaviour: the button always says why.
                var timerSvc = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
                string vmId = _vm != null ? _vm.BuildingId : null;
                if (timerSvc != null && vmId != null && timerSvc.IsBuilding(vmId))
                {
                    BuildLockButton(host, "Under construction — " + (int)timerSvc.RemainingSeconds(vmId) + "s",
                        x0, x1, yb, yt, null);
                    return;
                }
                if (timerSvc != null && !timerSvc.HasFreeSlot)
                {
                    BuildLockButton(host, "All build crews are busy", x0, x1, yb, yt, null);
                    return;
                }
                if (!sel.Affordable)
                {
                    BuildLockButton(host, "Not enough resources yet", x0, x1, yb, yt, null);
                    return;
                }
                BuildGoldButton(host, "Upgrade", true, x0, x1, yb, yt,
                    () => { FlowTrace.Step("UpgradeUI", "upgrade " + selId); _vm?.Select(selId); });
                return;
            }
            // Further-out locked tier — disabled CTA carrying the requirement.
            string reason = !string.IsNullOrEmpty(sel.LockReason) ? sel.LockReason : "Locked";
            BuildLockButton(host, reason, x0, x1, yb, yt, null);
        }

        // One benefit line: colorblind-safe glyph (green filled box = active / dim empty box =
        // locked) + luminance + text. Never hue alone.
        private void BuildBenefitRow(RectTransform parent, float yBot, float yTop, bool active, string text)
        {
            var glyphSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement,
                active ? RpgUiCatalog.ElementToggleBoxOn : RpgUiCatalog.ElementToggleBoxOff);
            var g = new GameObject(active ? "Check" : "Lock", typeof(Image));
            g.transform.SetParent(parent, false);
            var grt = g.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.0f, yBot); grt.anchorMax = new Vector2(0.075f, yTop);
            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
            var gimg = g.GetComponent<Image>();
            if (glyphSprite != null)
            {
                gimg.sprite = glyphSprite; gimg.preserveAspect = true;
                gimg.color = active ? ElarionUi.Affordable : new Color(0.55f, 0.53f, 0.50f, 0.9f);
            }
            else
            {
                gimg.color = active ? ElarionUi.Affordable : new Color(0.40f, 0.38f, 0.36f, 0.9f);
                ElarionUiKit.ApplyRounded(gimg);
            }
            gimg.raycastTarget = false;

            var lbl = ElarionUiKit.Label(parent, text, yBot, yTop,
                active ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, 0.10f, 1f);
            lbl.raycastTarget = false;
            ElarionUiKit.FitBlock(lbl);
        }

        // ── Shared clean buttons (gold CTA + dim lock) ────────────────────────────

        private void BuildGoldButton(Transform parent, string label, bool enabled,
            float x0, float x1, float y0, float y1, System.Action onClick)
        {
            var go = ElarionUiKit.AddImage(parent, "GoldBtn", new Vector2(x0, y0), new Vector2(x1, y1),
                enabled ? ElarionUi.GoldButton : new Color(0.30f, 0.28f, 0.22f, 1f));
            var img = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = enabled && onClick != null;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var lbl = ElarionUiKit.Label(go.transform, label, 0.06f, 0.94f,
                enabled ? ElarionUi.Ink : ElarionUi.ParchmentDim,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        private void BuildLockButton(Transform parent, string reason, float x0, float x1, float y0, float y1, System.Action onClick)
        {
            var go = ElarionUiKit.AddImage(parent, "LockBtn", new Vector2(x0, y0), new Vector2(x1, y1),
                new Color(0.11f, 0.105f, 0.10f, 1f));
            var img = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = onClick != null;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // Lock glyph (empty toggle box reads "not yet" — colorblind-safe shape) + text.
            var glyph = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementToggleBoxOff);
            float textX0 = 0.06f;
            if (glyph != null)
            {
                var g = new GameObject("LockGlyph", typeof(Image));
                g.transform.SetParent(go.transform, false);
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.04f, 0.22f); rt.anchorMax = new Vector2(0.16f, 0.78f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var gi = g.GetComponent<Image>();
                gi.sprite = glyph; gi.preserveAspect = true; gi.raycastTarget = false;
                gi.color = new Color(0.62f, 0.60f, 0.56f, 1f);
                textX0 = 0.18f;
            }
            var lbl = ElarionUiKit.Label(go.transform, reason, 0.06f, 0.94f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, textX0, 0.96f);
            lbl.raycastTarget = false;
            ElarionUiKit.FitBlock(lbl);
        }

        // ── Building illustration resolver (per-tier portrait; real art) ──────────
        // The building's Portraits/<slug>[-tier] sprite. Towers carry -2/-3 tier variants;
        // resource/city buildings reuse their single portrait (grown per tier by the card).
        private Sprite BuildingArt(int tierNum)
        {
            string title = _vm != null ? _vm.Title : "";
            if (string.IsNullOrEmpty(title)) return null;
            string t = title.Trim().ToLowerInvariant().Replace("'", "");
            string nospace = t.Replace(" ", "");
            string dash = t.Replace(' ', '-');

            if (tierNum >= 2)
            {
                var v = LoadPortrait(dash + "-" + tierNum);
                if (v == null) v = LoadPortrait(nospace + "-" + tierNum);
                if (v != null) return v;
            }
            var s = LoadPortrait(nospace);
            if (s == null) s = LoadPortrait(dash);
            return s;
        }

        // Portraits import as plain Texture2D (mirror BuildPaletteUI.LoadPortrait) — wrap once, cache.
        private static Sprite LoadPortrait(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string path = "Portraits/" + key;
            if (_portraitCache.TryGetValue(path, out var cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            _portraitCache[path] = sprite;   // cache nulls too — one lookup per miss
            return sprite;
        }

        // ── Skills tab — the per-tier RESEARCH PERKS as a scroll list (unchanged) ──

        private void RebuildSkills()
        {
            if (_vm == null || _skillsContent == null) return;

            ClearChildren(_skillsContent);

            bool anyPerk = false;
            foreach (var item in _vm.Perks)
            {
                if (item.Id != null && item.Id.StartsWith("perk:"))
                {
                    CreateRow(_skillsContent, item);
                    anyPerk = true;
                }
            }

            if (!anyPerk)
                EmptyNote(_skillsContent, "No research skills for this building yet.");

            LayoutRebuilder.ForceRebuildLayoutImmediate(_skillsContent);
        }

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

        // One full-width perk ROW (Skills tab): icon | name + effect | cost chips + CTA / OWNED / reason.
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
                plate.color = new Color(c.r * 1.12f, c.g * 1.08f, c.b * 0.9f, c.a);
            }
            else if (item.Locked)
            {
                var c = plate.color;
                plate.color = new Color(c.r * 0.52f, c.g * 0.52f, c.b * 0.55f, c.a * 0.8f);
                dim = 0.6f;
            }

            bool purchasable = !item.Locked && !item.Equipped;

            var btn = row.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = purchasable;
            if (!purchasable) btn.transition = Selectable.Transition.None;
            string id = item.Id;
            btn.onClick.AddListener(() => { FlowTrace.Step("UpgradeUI", "row-tap " + id); _vm?.Select(id); });

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
                var g = ElarionUiKit.Label(row.transform, ElarionUi.CrestGlyph, 0.14f, 0.86f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.015f, 0.11f, bold: true);
                g.raycastTarget = false;
                ElarionUiKit.FitSingleLine(g);
            }

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

            if (item.Equipped)
            {
                var owned = ElarionUiKit.Label(row.transform, "OWNED", 0.30f, 0.70f,
                    ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.66f, 0.985f, bold: true);
                owned.raycastTarget = false;
                ElarionUiKit.FitSingleLine(owned);
            }
            else if (item.Locked)
            {
                string reason = !string.IsNullOrEmpty(item.LockReason) ? item.LockReason : "Locked";
                var req = ElarionUiKit.Label(row.transform, reason, 0.14f, 0.86f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.50f, 0.985f);
                req.raycastTarget = false;
                ElarionUiKit.FitBlock(req);
            }
            else
            {
                string cost = _vm != null ? _vm.CostFor(item.Id) : "";
                BuildCostChips(row.transform, cost, 0.50f, 0.795f);
                BuildRowCta(row.transform, "Research", item.Affordable,
                    () => { FlowTrace.Step("UpgradeUI", "research " + id); _vm?.Select(id); });
            }
        }

        // ── Inline cost chips (icon + number) — colorblind-safe ───────────────────

        private void BuildCostChips(Transform parent, string costText, float x0, float x1)
        {
            if (string.IsNullOrEmpty(costText)) return;
            var raw = costText.Split('·');   // VM joins cost parts with U+00B7 middle-dot
            var tokens = new List<string>();
            foreach (var r in raw)
            {
                string t = r.Trim();
                if (t.Length > 0) tokens.Add(t);
            }
            int n = tokens.Count;
            if (n == 0) return;

            const float gap = 0.02f;
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
            RectTransform chip = RoundedCard(parent, "CostChip",
                new Vector2(x0, 0.28f), new Vector2(x1, 0.72f), PillFill, BorderDim, 1.5f);

            Sprite ic = CurrencyIconFor(token);
            float textX0 = 0.10f;
            if (ic != null)
            {
                var ig = new GameObject("Icon", typeof(Image));
                ig.transform.SetParent(chip, false);
                var irt = ig.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.06f, 0.16f); irt.anchorMax = new Vector2(0.40f, 0.84f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iimg = ig.GetComponent<Image>();
                iimg.sprite = ic; iimg.preserveAspect = true; iimg.raycastTarget = false;
                textX0 = 0.44f;
            }

            string shown = ic != null ? LeadingNumber(token) : token;
            var lbl = ElarionUiKit.Label(chip, shown, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                ic != null ? TMPro.TextAlignmentOptions.MidlineLeft : TMPro.TextAlignmentOptions.Center,
                textX0, 0.94f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        // Grey/white kit CTA seated at the row's right edge (Skills rows).
        private void BuildRowCta(Transform parent, string label, bool enabled, System.Action onClick)
        {
            var go = ElarionUiKit.AddImage(parent, "RowCta", new Vector2(0.815f, 0.16f), new Vector2(0.985f, 0.84f),
                new Color(0.13f, 0.125f, 0.115f, 1f));
            var plate = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = enabled;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

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

        // ── Icon / string helpers ─────────────────────────────────────────────────

        private Sprite IconFor(ItemVM item)
        {
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

        // "Tier 2 — Reinforced Blades" -> "Reinforced Blades"; "Level 3" -> "Level 3".
        private static string CardName(ItemVM item)
        {
            string n = item.Name ?? "";
            int em = n.IndexOf('—');   // em-dash the VM composes tier names with
            if (em >= 0 && em < n.Length - 1) return n.Substring(em + 1).Trim();
            int hy = n.IndexOf(" - ");
            if (hy >= 0 && hy < n.Length - 3) return n.Substring(hy + 3).Trim();
            return n;
        }

        // "TIER 2" for a city tier / "LEVEL 3" for a resource level — derived from the VM's name.
        private static string TierHeader(ItemVM item)
        {
            string n = item.Name ?? "";
            if (n.StartsWith("Level")) return "LEVEL " + TierNumber(item.Id);
            return "TIER " + TierNumber(item.Id);
        }

        private static string LeadingNumber(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            int sp = token.IndexOf(' ');
            return sp > 0 ? token.Substring(0, sp) : token;
        }

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
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_talent_1");
            if (plate != null)
            {
                plateImg.sprite = plate;
                plateImg.type   = Image.Type.Sliced;
                plateImg.color  = Color.white;
                return;
            }
            plateImg.color = new Color(0.078f, 0.073f, 0.066f, 1f);
            ElarionUiKit.ApplyRounded(plateImg);
        }

        private static void SoftenButton(Button btn)
        {
            if (btn == null || btn.transition != Selectable.Transition.ColorTint) return;
            var colors = btn.colors;
            colors.fadeDuration = ButtonFadeSec;
            btn.colors = colors;
        }

        // ── Shared primitive: a clean rounded card = border image + inset fill image ──
        // Returns the FILL RectTransform (content host); the bordered outer image is its parent.
        private static RectTransform RoundedCard(Transform parent, string name, Vector2 min, Vector2 max,
            Color fill, Color border, float borderPx)
        {
            var b = ElarionUiKit.AddImage(parent, name, min, max, border);   // outer = border ring
            var f = ElarionUiKit.AddImage(b.transform, "Fill", Vector2.zero, Vector2.one, fill);
            var frt = (RectTransform)f.transform;
            frt.offsetMin = new Vector2(borderPx, borderPx);
            frt.offsetMax = new Vector2(-borderPx, -borderPx);
            f.GetComponent<Image>().raycastTarget = false;
            return frt;
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
            _pills.Clear();
            _tabs.Clear();
            _lastStatus = null;
            _lastContentSig = null;   // fresh chrome next Open -> force the first rebuild
            if (_ui != null)
            {
                var fx = _ui.GetComponent<PanelOpenCloseFx>();
                if (fx != null && fx.isActiveAndEnabled) fx.PlayCloseAndDestroy();
                else Destroy(_ui);
            }
            _ui = null;
            _bodyHost = null;
            _upgradePage = null;
            _skillsPage = null;
            _pathCardsHost = null;
            _detailHost = null;
            _skillsContent = null;
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
