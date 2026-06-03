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

        /// <summary>Raised when the palette's Exit button is tapped.</summary>
        public event Action OnExitRequested;

        [Tooltip("Catalog types the palette lists. Default = Tower (the registered content).")]
        [SerializeField] private CatalogType[] _types = { CatalogType.Tower, CatalogType.Wall, CatalogType.Gate, CatalogType.Resource };

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _strip;
        private Label _balanceLabel;
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
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder = src.sortingOrder + 6;   // above HUD + BuildMenu
            }
            else
            {
                Debug.LogWarning("[BuildPaletteUI] No sibling PanelSettings found — palette will not render.");
            }
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
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;
            var docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null) return;

            _root = new VisualElement { name = "build-palette-root" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.flexDirection = FlexDirection.Column;
            // The root is click-through; the bar inside receives taps.
            _root.pickingMode = PickingMode.Ignore;
            docRoot.Add(_root);

            // Top row: balance + exit.
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.paddingLeft = 12; topBar.style.paddingRight = 12;
            topBar.style.paddingTop = 6; topBar.style.paddingBottom = 6;
            topBar.style.backgroundColor = new Color(0.06f, 0.08f, 0.14f, 0.92f);

            _balanceLabel = new Label("◆ 0");
            _balanceLabel.style.color = Color.white;
            _balanceLabel.style.fontSize = 16;
            _balanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(_balanceLabel);

            var exitBtn = new Button(() => OnExitRequested?.Invoke()) { text = "Done" };
            exitBtn.style.height = 30; exitBtn.style.minWidth = 80;
            exitBtn.style.color = Color.white;
            exitBtn.style.backgroundColor = new Color(0.30f, 0.52f, 0.34f, 0.95f);
            topBar.Add(exitBtn);
            _root.Add(topBar);

            // Bottom row: scrollable horizontal card strip.
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.height = 120;
            scroll.style.backgroundColor = new Color(0.08f, 0.10f, 0.16f, 0.92f);
            _strip = scroll.contentContainer;
            _strip.style.flexDirection = FlexDirection.Row;
            _root.Add(scroll);
        }

        // ── Render ──────────────────────────────────────────────────────────────

        public void Render()
        {
            EnsureBuilt();
            if (_strip == null) return;

            _strip.Clear();
            UpdateBalance();

            int cards = 0;
            foreach (var type in _types)
            {
                var entries = CatalogRegistry.OfType(type);
                if (entries == null) continue;
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    _strip.Add(BuildCard(e));
                    cards++;
                }
            }

            if (cards == 0)
            {
                var none = new Label("No buildables registered.");
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
                _armedId = e.id;
                OnEntrySelected?.Invoke(e);
                Render();   // refresh the armed highlight
            });
            card.style.width = 110; card.style.height = 104;
            card.style.marginLeft = 6; card.style.marginRight = 6;
            card.style.marginTop = 8; card.style.marginBottom = 8;
            card.style.flexDirection = FlexDirection.Column;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.backgroundColor = armed
                ? new Color(0.20f, 0.42f, 0.66f, 0.98f)
                : new Color(0.14f, 0.18f, 0.26f, 0.96f);
            card.style.opacity = affordable ? 1f : 0.5f;
            card.SetEnabled(affordable);

            var nameLabel = new Label(string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName);
            nameLabel.style.color = Color.white;
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(nameLabel);

            var costLabel = new Label(CostLabel(cost));
            costLabel.style.color = affordable ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.5f, 0.5f);
            costLabel.style.fontSize = 13;
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
            if (c.crystals > 0) parts.Add("◆" + c.crystals);
            return string.Join("  ", parts);
        }

        private void UpdateBalance()
        {
            if (_balanceLabel != null) _balanceLabel.text = "◆ " + CrystalBalance;
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
