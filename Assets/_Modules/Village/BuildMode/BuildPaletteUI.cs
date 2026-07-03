// =============================================================================
// BuildPaletteUI — the code-built Build Mode palette (WO-108 P1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A horizontal strip of buildable cards at the bottom of the screen, populated
// straight from CatalogRegistry (the SAME buckets StructureFactory builds from —
// no parallel BuildableItem type, per the WO build-ready update). Each card shows
// the entry's display name + cost; unaffordable cards grey out. Tapping a card
// arms it for placement via the OnEntrySelected callback.
//
// WO-D conversion (2026-07-03, coverage matrix row #36): UIDocument/UITK strip ->
// code-built uGUI on the Obsidian kit language. This is an IN-WORLD-ADJACENT
// strip, NOT a full modal: it keeps its bottom-of-screen position + behaviour,
// restyled with kit buttons (BuildObsidianButton) and slot plates (RpgUiCatalog
// RoleSlot "slot_action") on its own overlay canvas — no PanelSettings adoption
// needed any more (that was a UIDocument requirement). "Done" exits Build Mode
// and IS this strip's close affordance, so its GameObject is named "CloseButton"
// per the close convention (label stays "Done").
// =============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The Build Mode structure palette. Lists CatalogRegistry entries as tappable
    /// slot-plate cards and raises <see cref="OnEntrySelected"/> when one is armed.
    /// Built in code (uGUI + Obsidian kit) so it renders in player builds.
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

        /// <summary>Raised when the palette's Done/exit button is tapped.</summary>
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

        // Strips sit ABOVE the HUD but BELOW kit modals (BuildObsidianModal defaults 31000).
        private const int SortingOrder = 900;

        private GameObject _canvas;           // own overlay canvas (kit BuildModalCanvas)
        private Transform _stripContent;      // horizontal-layout card host inside the scroll
        private TextMeshProUGUI _balanceLabel;
        private Button _orientBtn;            // shown only while an entry is armed
        private string _armedId;

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

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas);
        }

        private void OnResourcesChanged()
        {
            if (_canvas != null && _canvas.activeSelf) Render();
        }

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show()
        {
            EnsureBuilt();
            if (_canvas != null) _canvas.SetActive(true);
            Render();
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.SetActive(false);
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
            if (_canvas != null && _canvas.activeSelf) Render();
            else UpdateOrientButton();
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            _canvas = ElarionUiKit.BuildModalCanvas("BuildPaletteCanvas", SortingOrder);

            // Bottom-anchored dock: top bar (balance + Orient + Done) over the card tray.
            // Only the dock's own graphics raycast — everything above it stays click-through
            // so world taps still land placements.
            var dock = new GameObject("PaletteDock", typeof(RectTransform));
            dock.transform.SetParent(_canvas.transform, false);
            var drt = (RectTransform)dock.transform;
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(0f, 300f);

            // Top row: obsidian fill + gold under-rule (the kit panel language).
            var topBar = ElarionUiKit.AddImage(dock.transform, "TopBar",
                new Vector2(0f, 0.72f), new Vector2(1f, 1f), ElarionUiKit.ObsidianFill, rounded: false);
            var rule = ElarionUiKit.AddImage(topBar.transform, "GoldRule",
                new Vector2(0f, 0f), new Vector2(1f, 0f), ElarionUiKit.ObsidianTrim, rounded: false);
            var rrt = rule.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(0f, 2f);
            rrt.pivot = new Vector2(0.5f, 0f);
            var ruleImg = rule.GetComponent<Image>();
            if (ruleImg != null) ruleImg.raycastTarget = false;

            _balanceLabel = MakeText(topBar.transform, "Crystals: 0", 18, ElarionUi.Gilt,
                FontStyles.Bold, TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.10f), new Vector2(0.50f, 0.90f));

            // Orient — opens the 3-axis orient editor on the ARMED entry (no id typing).
            _orientBtn = ElarionUiKit.BuildObsidianButton(topBar.transform, "Orient",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.56f, 0.10f), new Vector2(0.76f, 0.90f),
                () => { if (!string.IsNullOrEmpty(_armedId)) OnOrientRequested?.Invoke(_armedId); });
            _orientBtn.gameObject.SetActive(false);   // shown only while armed

            // Done exits Build Mode — the strip's close affordance, so it carries the
            // canonical close name while keeping its "Done" label.
            var exitBtn = ElarionUiKit.BuildObsidianButton(topBar.transform, "Done",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.78f, 0.10f), new Vector2(0.98f, 0.90f),
                () => OnExitRequested?.Invoke());
            exitBtn.gameObject.name = "CloseButton";

            // Bottom: horizontal-scrolling slot-plate card tray in a recessed dark well.
            var tray = ElarionUiKit.AddImage(dock.transform, "CardTray",
                new Vector2(0f, 0f), new Vector2(1f, 0.72f),
                new Color(0f, 0f, 0f, 0.55f), rounded: false);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(tray.transform, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);   // raycast surface for drag-scroll

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childControlWidth = false;    // cards keep their fixed width
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            contentGo.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            _stripContent = contentGo.transform;
            _canvas.SetActive(false);   // built hidden; Show shows it
        }

        // ── Render ──────────────────────────────────────────────────────────────

        public void Render()
        {
            FlowTrace.Step("BuildPalette", "palette-build-start");
            EnsureBuilt();
            if (_stripContent == null)
            {
                FlowTrace.Warn("BuildPalette", "Render aborted: strip content is null (palette never built)");
                return;
            }

            for (int i = _stripContent.childCount - 1; i >= 0; i--)
                Destroy(_stripContent.GetChild(i).gameObject);
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
            // kit quirk) is logged + skipped instead of blanking the whole palette —
            // the WebGL "shows nothing, no error" silent-failure class becomes a logged line.
            var built = Guard.TryEach("BuildPalette", "build card", candidates,
                e => BuildCard(e));
            FlowTrace.Step("BuildPalette", $"rows-added: built={built.built} failed={built.failed}");

            if (built.built == 0)
            {
                var none = MakeText(_stripContent, candidates.Count == 0
                        ? "No buildables registered."
                        : "Buildables failed to load.",
                    14, ElarionUi.Parchment, FontStyles.Italic, TextAlignmentOptions.Left,
                    Vector2.zero, Vector2.one);
                var lrt = none.GetComponent<RectTransform>();
                lrt.sizeDelta = new Vector2(360f, 0f);
                none.gameObject.AddComponent<LayoutElement>().preferredWidth = 360f;
            }
        }

        private void BuildCard(CatalogEntry e)
        {
            DeNelle.Core.Catalog.ResourceCost cost = CostFor(e);
            bool affordable = CanAfford(cost);
            bool armed = e.id == _armedId;

            // Slot-plate card: the Blink "slot_action" plate as the face (Obsidian fill
            // fallback when the mirrored art is absent), a Button over the whole plate.
            var cardGo = new GameObject("Card_" + e.id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            cardGo.transform.SetParent(_stripContent, false);
            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 0f);
            cardGo.GetComponent<LayoutElement>().preferredWidth = 160f;

            var img = cardGo.GetComponent<Image>();
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotAction);
            if (plate != null)
            {
                img.sprite = plate;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                // Armed = gilt-tinted plate; rest = the plate's own obsidian face.
                img.color = armed ? ElarionUi.Gilt : Color.white;
            }
            else
            {
                img.color = armed
                    ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f)
                    : ElarionUiKit.ObsidianFill;
            }

            var btn = cardGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = affordable;
            btn.onClick.AddListener(() =>
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

            // Unaffordable cards grey out as a whole (plate + labels).
            if (!affordable) cardGo.AddComponent<CanvasGroup>().alpha = 0.45f;

            var nameLabel = MakeText(cardGo.transform,
                string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName,
                14, armed ? ElarionUi.Ink : ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.92f));
            nameLabel.raycastTarget = false;

            var costLabel = MakeText(cardGo.transform, CostLabel(cost), 13,
                affordable ? ElarionUi.Affordable : ElarionUi.Danger, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.40f));
            costLabel.raycastTarget = false;
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

        /// <summary>Compact per-resource cost string for the card (skips zero slots; ASCII only).</summary>
        private static string CostLabel(DeNelle.Core.Catalog.ResourceCost c)
        {
            if (c.IsZero) return "Free";
            var parts = new List<string>(4);
            if (c.wood     > 0) parts.Add(c.wood     + "W");
            if (c.food     > 0) parts.Add(c.food     + "F");
            if (c.iron     > 0) parts.Add(c.iron     + "I");
            if (c.crystals > 0) parts.Add(c.crystals + "C");
            return string.Join("  ", parts);
        }

        private void UpdateBalance()
        {
            if (_balanceLabel != null) _balanceLabel.text = "Crystals: " + CrystalBalance;
        }

        /// <summary>Show the Orient button only while an entry is armed.</summary>
        private void UpdateOrientButton()
        {
            if (_orientBtn != null)
                _orientBtn.gameObject.SetActive(!string.IsNullOrEmpty(_armedId));
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

        // ── uGUI helper (LeaderboardPanel/VillageCraftingPanel shape) ─────────
        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
