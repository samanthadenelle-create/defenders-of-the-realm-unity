// =============================================================================
// BuildPaletteUI — the code-built Build Mode palette (WO-108 P1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A horizontal strip of buildable cards at the bottom of the screen, populated
// straight from CatalogRegistry (the SAME buckets StructureFactory builds from —
// no parallel BuildableItem type, per the WO build-ready update). Each card shows
// the entry's display name + crystal cost; unaffordable cards grey out. Tapping a
// card arms it for placement via the OnEntrySelected callback.
//
// CODE-BUILT, NO UXML (repo rule: .uxml UIDocuments render empty in player builds
// — see BuildMenu.ShowCodeFallbackMenu). Owns its own UIDocument + PanelSettings,
// adopting a sibling's PanelSettings the same way BuildMenu does so it renders.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The Build Mode structure palette. Lists CatalogRegistry entries as tappable
    /// cards and raises <see cref="OnEntrySelected"/> when one is armed. Built in
    /// code so it renders in player builds.
    /// </summary>
    public sealed class BuildPaletteUI : MonoBehaviour
    {
        /// <summary>Raised when a palette card is tapped — arg is the armed entry.</summary>
        public event Action<CatalogEntry> OnEntrySelected;

        /// <summary>
        /// WO-352 — raised when a palette card is tapped, BEFORE arming, so the controller
        /// can show the Structure Info Preview panel and defer arming until the player taps
        /// "Place". When a subscriber is attached this REPLACES the immediate-arm behaviour
        /// (the card no longer raises <see cref="OnEntrySelected"/> on tap); with no
        /// subscriber the legacy immediate-arm path is unchanged. Arg = the tapped entry.
        /// </summary>
        public event Action<CatalogEntry> OnCardTapped;

        /// <summary>Raised when the palette's Exit button is tapped.</summary>
        public event Action OnExitRequested;

        /// <summary>
        /// Raised when the "Orient" button is tapped (only shown while an entry is armed).
        /// The controller opens the 3-axis orient editor ON THE ARMED ENTRY — no typing an
        /// id. Arg is the armed entry id.
        /// </summary>
        public event Action<string> OnOrientRequested;

        [Tooltip("Catalog types the palette lists. Default = Tower (the registered content).")]
        [SerializeField] private CatalogType[] _types = { CatalogType.Tower, CatalogType.Wall, CatalogType.Gate };

        // Catalog ids defined-but-not-yet-buildable. They stay in the catalog (ready
        // to unlock + referenced elsewhere) but are filtered out of the build palette
        // until their unlock feature ships. Central + reversible — no JSON risk.
        // TODO unlock-gate: remove from set when jeweler unlock ships.
        private static readonly HashSet<string> NotYetUnlockable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jeweler",
        };

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _strip;
        private Label _balanceLabel;
        private Button _orientBtn;     // shown only while an entry is armed
        private string _armedId;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        private void OnEnable()
        {
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
                svc.ResourcesChanged.AddListener(OnResourcesChanged);
            }
        }

        private void OnDisable()
        {
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
        }

        private void OnResourcesChanged()
        {
            if (_root != null && _root.style.display == DisplayStyle.Flex) Render();
        }

        /// <summary>
        /// Adopt a sibling UIDocument's PanelSettings so the palette renders (the
        /// builder leaves new docs without one → invisible). Mirrors BuildMenu.
        /// </summary>
        private void AdoptPanelSettings()
        {
            if (_document == null) return;
            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Require a THEMED PanelSettings — adopting an unthemed one renders the palette
                // blank (the empty-palette bug: in MainCastle_Hall the HUD is uGUI, so the only
                // sibling was the dev console's unthemed runtime PanelSettings).
                if (doc == null || doc == _document || doc.panelSettings == null ||
                    doc.panelSettings.themeStyleSheet == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder = src.sortingOrder + 6;   // above HUD + BuildMenu
                FlowTrace.Step("BuildPalette", $"adopted PanelSettings from '{src.gameObject.name}' (sort={_document.sortingOrder})");
                return;
            }

            // NO themed sibling (the castle hub HUD is uGUI — no UIDocument to borrow). Build our
            // OWN PanelSettings at runtime so the palette ALWAYS renders, instead of dead-ending
            // invisible. Mirrors DevBootstrap.ResolvePanelSettings — the proven no-sibling fallback
            // (the palette is inline-styled, so it renders even if no theme USS is available).
            var created = ScriptableObject.CreateInstance<PanelSettings>();
            created.name = "BuildPaletteRuntimePanelSettings";
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (doc != null && doc.panelSettings != null && doc.panelSettings.themeStyleSheet != null)
                { created.themeStyleSheet = doc.panelSettings.themeStyleSheet; break; }
            }
            _document.panelSettings = created;
            _document.sortingOrder = 130;   // above the town HUD
            FlowTrace.Step("BuildPalette", created.themeStyleSheet != null
                ? "no themed sibling — built runtime PanelSettings (borrowed a theme)"
                : "no themed sibling/theme — built runtime PanelSettings (inline-styled palette still renders)");
        }

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show()
        {
            EnsureBuilt();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            Render();
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _armedId = null;
            UpdateOrientButton();
        }

        /// <summary>
        /// WO-352 — set which entry the palette shows as ARMED (gilt highlight + Orient
        /// button), without raising OnEntrySelected. The controller calls this after the
        /// player confirms "Place" in the Structure Info Preview, so the palette stays in
        /// sync with the deferred-arm flow. Pass null to clear the highlight.
        /// </summary>
        public void SetArmed(string id)
        {
            _armedId = id;
            if (_root != null && _root.style.display == DisplayStyle.Flex) Render();
            else UpdateOrientButton();
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;
            var docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null)
            {
                // WebGL silent-blank cause #1: doc has no panel/root yet → nothing builds.
                FlowTrace.Warn("BuildPalette", _document == null
                    ? "EnsureBuilt: no UIDocument — palette cannot build"
                    : "EnsureBuilt: UIDocument.rootVisualElement is null (no PanelSettings?) — palette cannot build");
                return;
            }

            _root = new VisualElement { name = "build-palette-root" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.flexDirection = FlexDirection.Column;
            // The root is click-through; the bar inside receives taps.
            _root.pickingMode = PickingMode.Ignore;
            docRoot.Add(_root);

            // Top row: balance + exit. Stone bar with a gilt under-rule.
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 14; topBar.style.paddingRight = 14;
            topBar.style.paddingTop = 6; topBar.style.paddingBottom = 6;
            topBar.style.backgroundColor = ElarionUi.PanelStone;
            topBar.style.borderBottomWidth = 2;
            topBar.style.borderBottomColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);

            _balanceLabel = new Label("❖ 0");
            _balanceLabel.style.color = ElarionUi.Aether;
            _balanceLabel.style.fontSize = ElarionUi.FontHead;
            _balanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(_balanceLabel);

            // Right-side button cluster: Orient (armed-only) + Done.
            var rightCluster = new VisualElement();
            rightCluster.style.flexDirection = FlexDirection.Row;

            // Orient — opens the 3-axis orient editor on the ARMED entry (no id typing).
            _orientBtn = new Button(() => { if (!string.IsNullOrEmpty(_armedId)) OnOrientRequested?.Invoke(_armedId); })
                { text = "Orient" };
            ElarionUi.StyleButton(_orientBtn, ElarionUi.ButtonKind.Neutral);
            _orientBtn.style.minWidth = 88;
            _orientBtn.style.marginRight = 8;
            _orientBtn.style.display = DisplayStyle.None;   // shown only while armed
            rightCluster.Add(_orientBtn);

            var exitBtn = new Button(() => OnExitRequested?.Invoke()) { text = "Done" };
            ElarionUi.StyleButton(exitBtn, ElarionUi.ButtonKind.Gold);
            exitBtn.style.minWidth = 88;
            rightCluster.Add(exitBtn);
            topBar.Add(rightCluster);
            _root.Add(topBar);

            // Bottom row: scrollable horizontal card strip in a recessed stone tray.
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.height = 128;
            scroll.style.backgroundColor = ElarionUi.PanelStoneDark;
            scroll.style.paddingTop = 4; scroll.style.paddingBottom = 4;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _strip = scroll.contentContainer;
            _strip.style.flexDirection = FlexDirection.Row;
            _root.Add(scroll);
        }

        // ── Render ──────────────────────────────────────────────────────────────

        public void Render()
        {
            FlowTrace.Step("BuildPalette", "palette-build-start");
            EnsureBuilt();
            if (_strip == null)
            {
                // built-but-no-strip → the doc root never resolved (see EnsureBuilt warn).
                FlowTrace.Warn("BuildPalette", "Render aborted: _strip is null (palette never built)");
                return;
            }

            _strip.Clear();
            UpdateBalance();
            UpdateOrientButton();

            // Gather every candidate entry across the configured types FIRST so the
            // catalog-count is logged even if a card later throws. CatalogRegistry is
            // populated at BeforeSceneLoad (CatalogBootstrap) and is WebGL-safe — an
            // empty count here means the JSON/fallback load failed, not a render bug.
            var candidates = new List<CatalogEntry>();
            foreach (var type in _types)
            {
                var entries = CatalogRegistry.OfType(type);
                if (entries == null) continue;
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    if (e.id != null && NotYetUnlockable.Contains(e.id)) continue;   // unlock-gated — see NotYetUnlockable
                    candidates.Add(e);
                }
            }
            FlowTrace.Step("BuildPalette", $"catalog-count: registry={CatalogRegistry.Count} candidates={candidates.Count} (types={_types.Length})");

            // §12: guard EACH card build so one bad entry (missing field / service throw /
            // ElarionUi quirk) is logged + skipped instead of blanking the whole palette —
            // the WebGL "shows nothing, no error" silent-failure class becomes a logged line.
            var built = Guard.TryEach("BuildPalette", "build card", candidates,
                e => _strip.Add(BuildCard(e)));
            FlowTrace.Step("BuildPalette", $"rows-added: built={built.built} failed={built.failed}");

            if (built.built == 0)
            {
                var none = new Label(candidates.Count == 0
                    ? "No buildables registered."
                    : "Buildables failed to load.");
                none.style.color = Color.white;
                none.style.paddingLeft = 12; none.style.paddingTop = 12;
                _strip.Add(none);
            }
        }

        private VisualElement BuildCard(CatalogEntry e)
        {
            DeNelle.Core.Catalog.ResourceCost cost = CostFor(e);
            bool affordable = CanAfford(cost);
            bool armed = e.id == _armedId;

            var card = new Button(() =>
            {
                // WO-352 — if a preview subscriber is attached, defer arming: raise
                // OnCardTapped so the controller shows the Structure Info Preview panel
                // (it calls SetArmed on "Place"). Otherwise keep the legacy immediate-arm.
                if (OnCardTapped != null)
                {
                    OnCardTapped.Invoke(e);
                    return;
                }
                _armedId = e.id;
                OnEntrySelected?.Invoke(e);
                Render();   // refresh the armed highlight
            });
            card.style.width = 116; card.style.height = 108;
            card.style.marginLeft = 6; card.style.marginRight = 6;
            card.style.marginTop = 8; card.style.marginBottom = 8;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 8; card.style.paddingRight = 8;
            card.style.flexDirection = FlexDirection.Column;
            card.style.justifyContent = Justify.SpaceBetween;
            // Armed = gilt-rimmed gold glow; rest = stone slot.
            card.style.backgroundColor = armed ? ElarionUi.AetherDim : ElarionUi.PanelStone;
            ElarionUi.SetRadius(card, ElarionUi.RadiusMd);
            ElarionUi.SetBorderWidth(card, armed ? 2 : 1);
            ElarionUi.SetBorderColor(card, armed
                ? ElarionUi.Gilt
                : new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f));
            card.style.opacity = affordable ? 1f : 0.45f;
            card.SetEnabled(affordable);

            var nameLabel = new Label(string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName);
            nameLabel.style.color = ElarionUi.Parchment;
            nameLabel.style.fontSize = ElarionUi.FontLabel;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(nameLabel);

            var costLabel = new Label(CostLabel(cost));
            costLabel.style.color = affordable ? ElarionUi.Affordable : ElarionUi.Danger;
            costLabel.style.fontSize = ElarionUi.FontLabel;
            costLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            costLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(costLabel);

            return card;
        }

        // ── Cost resolution (mirrors BuildModeController — crystals-only fallback) ──

        /// <summary>
        /// Resolve a catalog entry's build cost to the Core multi-resource shape: the
        /// authored multi-cost (repo.cost) wins; otherwise fall back to repo.buildCost
        /// Crystals so legacy / cost-less rows still gate + display as before.
        /// </summary>
        private static DeNelle.Core.Catalog.ResourceCost CostFor(CatalogEntry e)
        {
            var repo = e != null ? e.repo : null;
            if (repo == null) return default;
            if (!repo.cost.IsZero) return repo.cost;
            return new DeNelle.Core.Catalog.ResourceCost { crystals = repo.buildCost };
        }

        /// <summary>Multi-resource affordability via the persisted ledger (EconomyService).</summary>
        private static bool CanAfford(DeNelle.Core.Catalog.ResourceCost cost)
        {
            var econ = EconomyService.Instance;
            if (econ != null)
                return econ.CanAfford(new ResourceCost(cost.wood, cost.food, cost.iron, cost.crystals));
            return CrystalBalance >= cost.crystals;   // service-less fallback
        }

        /// <summary>Compact per-resource cost string for the card (skips zero slots).</summary>
        private static string CostLabel(DeNelle.Core.Catalog.ResourceCost c)
        {
            if (c.IsZero) return "Free";
            var parts = new List<string>(4);
            if (c.wood     > 0) parts.Add(c.wood     + "W");
            if (c.food     > 0) parts.Add(c.food     + "F");
            if (c.iron     > 0) parts.Add(c.iron     + "I");
            if (c.crystals > 0) parts.Add("❖" + c.crystals);
            return string.Join("  ", parts);
        }

        private void UpdateBalance()
        {
            if (_balanceLabel != null) _balanceLabel.text = "❖ " + CrystalBalance;
        }

        /// <summary>Show the Orient button only while an entry is armed.</summary>
        private void UpdateOrientButton()
        {
            if (_orientBtn != null)
                _orientBtn.style.display = string.IsNullOrEmpty(_armedId)
                    ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The persisted crystal wallet (WO-131 — the single source of truth).</summary>
        private static int CrystalBalance
        {
            get
            {
                var svc = GameStateService.Instance;
                return svc != null && svc.State != null ? svc.State.Resources.Crystals : 0;
            }
        }
    }
}
