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

        // Which catalog types this palette lists + which ids are unlock-gated — now
        // SOURCED FROM DATA (owner 2026-07-10 generic build-mode). Configure(BuildType)
        // fills these from BuildCategoryRegistry.Get(type) so the SAME palette serves
        // every build verb. Defaults to the Defense recipe so the palette is coherent
        // even if Configure is never called (back-compat: the old no-arg build path is
        // BuildType.Defense).
        //
        // ⚠ PALETTE REVERSAL (WO-673, owner 2026-07-11) — supersedes the 2026-06-27
        // "Defensive only" ruling that restricted the palette to Tower/Wall/Gate:
        // functional buildings (CatalogType.Resource + Collector) JOIN the palette under
        // the Build → Town verb, and Walls split out of Defense (owner taxonomy
        // Town / Defenses / Walls). Always on — WO-682 removed ff.strategicplacement.
        private CatalogType[] _types = { CatalogType.Tower, CatalogType.Gate };

        // Catalog ids defined-but-not-yet-buildable for the active build verb. They stay
        // in the catalog (ready to unlock + referenced elsewhere) but are filtered out of
        // the palette until their unlock ships. Sourced from build-categories.json via
        // Configure; the initial value mirrors the Defense lockedIds so the pre-Configure
        // palette stays the three tower types (owner ruling 2026-07-06; WO-673 moved the
        // jeweler lock to the Town verb and the wall ids out with the Walls split — the
        // rendered pre-Configure set is unchanged, since every removed id was either
        // locked or not a Tower/Gate type).
        private HashSet<string> _lockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tower_siege_tower",
            "tower_catapult",
            "gate_stone",
        };

        /// <summary>
        /// Point the palette at a build verb (owner 2026-07-10). Sources <c>_types</c> +
        /// <c>_lockedIds</c> from <see cref="BuildCategoryRegistry"/> so <c>Render</c> lists
        /// exactly that verb's catalog types (Defense → Tower/Wall/Gate, Collector →
        /// Collector). Called by BuildModeController before Show; re-renders live if open.
        /// </summary>
        public void Configure(BuildType type)
        {
            _activeType = type;
            var cat = BuildCategoryRegistry.Get(type);
            if (cat != null)
            {
                if (cat.Types != null && cat.Types.Length > 0) _types = cat.Types;
                _lockedIds = cat.LockedIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            UpdateTabHighlight();   // WO-673 — move the gold underline to the active category tab
            if (_canvas != null && _canvas.activeSelf) Render();
        }

        // Strips sit ABOVE the HUD but BELOW kit modals (BuildObsidianModal defaults 31000).
        private const int SortingOrder = 900;

        private GameObject _canvas;           // own overlay canvas (kit BuildModalCanvas)
        private Transform _stripContent;      // horizontal-layout card host inside the scroll
        private TextMeshProUGUI _balanceLabel;
        private Button _orientBtn;            // shown only while an entry is armed
        private string _armedId;

        // Collapse-on-place (owner "minimize on select" + 2026-07-16 redesign): while an
        // entry is armed the shop FULLY minimizes — EVERY dock background is hidden so no
        // black wall covers the map. These refs let Collapse()/Expand() toggle the header
        // band, the tab row, and the card tray. The "Placing: <name>" label is folded into
        // the HUD intent bar (BuildHudController.SetPlacingLabel) — no summary panel here.
        private GameObject _topBarGo;         // the dock header band (hidden while collapsed)
        private GameObject _trayGo;           // the scroll well (hidden while collapsed)
        private GameObject _tabRowGo;         // the category tab band (hidden while collapsed)

        // WO-673 category switcher (always on — WO-682): the owner-ruled three build
        // categories — Town / Defenses / Walls — as a tab row between the header and
        // the card tray. Tapping a tab Configure()s this palette for that verb (placement
        // stays generic; BuildModeController's _activeBuildType is only ever used to
        // Configure this palette, verified BuildModeController.cs:256). The active tab
        // carries a gold UNDERLINE — position/shape tell, never color alone (owner is
        // red/green colorblind).
        private BuildType _activeType = BuildType.Defense;
        // WO — the kit tab component (BuildTabRow) that renders Town/Defenses/Walls and
        // owns the active-underline + tutorial spotlight registration (was an inline loop).
        private BuildTabRow _tabRow;

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

            // Bottom-CENTERED dock sized to content (owner F8 2026-07-06, board #4):
            // the palette lists 3 cards, so it no longer spans a full-width black wall.
            // 540 wide = padding 24 + 3×160 cards + 2×10 spacing; 224 tall = 44px
            // header row (balance | Orient | Done) over a 180px card tray. Only the
            // dock's own graphics raycast — the rest of the screen stays click-through
            // so world taps still land placements.
            // WO-673 (always on — WO-682): the dock carries a category tab row
            // (Town / Defenses / Walls) between the header and the card tray.
            // Band split rebalanced (owner felt-test 2026-07-15 "long thin rectangles"):
            // the header + tab bands are tall enough (~140px at the 540-tall dock) to
            // seat a CanonCtaHeight (132px) button WITHOUT overflowing into the tray, so
            // the tabs + Orient/Done render as proper boxes, not full-band thin bars.
            // header 0.74–1.0 (~140px), tabs 0.48–0.74 (~140px), tray 0–0.48 (~259px).
            const float trayTop = 0.48f;
            const float headerBottom = 0.74f;

            // Grok slice 4 (landscape density): the shop is now a LARGE landscape
            // bottom carousel, not the old 540px portrait dock — wider so more
            // icon-first tiles read at once (owner CoC shop bar). Bottom-centred.
            var dock = new GameObject("PaletteDock", typeof(RectTransform));
            dock.transform.SetParent(_canvas.transform, false);
            var drt = (RectTransform)dock.transform;
            drt.anchorMin = new Vector2(0.5f, 0f);
            drt.anchorMax = new Vector2(0.5f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.anchoredPosition = Vector2.zero;
            // Phone enlargement (owner felt-test 2026-07-14 "make it larger for
            // selection on a phone"): taller + wider dock so the shop tiles read big
            // and thumb-reachable on a small landscape phone screen (CoC shop bar).
            // Raised 440->540 (2026-07-15) so the rebalanced header/tab bands each hold
            // a full 132px button without overflow, while the card tray stays ~259px.
            drt.sizeDelta = new Vector2(1560f, 540f);

            // Slim header row: obsidian fill + gold under-rule (the kit panel language).
            // Held as _topBarGo so Collapse() can hide the whole header band (it was the
            // "giant black pane" left standing during placement — owner device screenshots).
            var topBar = ElarionUiKit.AddImage(dock.transform, "TopBar",
                new Vector2(0f, headerBottom), new Vector2(1f, 1f), ElarionUiKit.ObsidianFill, rounded: false);
            _topBarGo = topBar;
            var rule = ElarionUiKit.AddImage(topBar.transform, "GoldRule",
                new Vector2(0f, 0f), new Vector2(1f, 0f), ElarionUiKit.ObsidianTrim, rounded: false);
            var rrt = rule.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(0f, 2f);
            rrt.pivot = new Vector2(0.5f, 0f);
            var ruleImg = rule.GetComponent<Image>();
            if (ruleImg != null) ruleImg.raycastTarget = false;

            // Balance sits IN the dock header, left-aligned beside the buttons —
            // no more floating alone on an empty band (owner F8 2026-07-06).
            _balanceLabel = MakeText(topBar.transform, "Crystals: 0", 16, ElarionUi.Gilt,
                FontStyles.Bold, TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.10f), new Vector2(0.36f, 0.90f));

            // Orient — opens the 3-axis orient editor on the ARMED entry (no id typing).
            // DEV-ONLY (UpdateOrientButton gate). Parented to the DOCK (not the header band)
            // and pinned TOP-RIGHT of the dock so it SURVIVES Collapse: it is only ever
            // meaningful while an entry is armed (= placing = header hidden), so it must not
            // vanish with the header. Small box, no wall; sits clear of the centred bottom
            // intent cluster. Top-right slot is free now that the duplicate "Done" is gone.
            _orientBtn = ElarionUiKit.BuildObsidianButton(dock.transform, "Orient",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.78f, 0.80f), new Vector2(0.98f, 0.98f),
                () => { if (!string.IsNullOrEmpty(_armedId)) OnOrientRequested?.Invoke(_armedId); });
            PinSize(_orientBtn, 300f, ElarionUiKit.CanonCtaHeight);
            _orientBtn.gameObject.SetActive(false);   // shown only while armed (+ dev)

            // NOTE (2026-07-16 redesign): the palette's own "Done" exit was REMOVED to end
            // the duplicate-exit problem — the ONE exit is BuildHudController's top-band
            // "X Done" (always visible while Build Mode is open). OnExitRequested stays on
            // the API for back-compat but is no longer raised from this strip.

            // WO — category tab row via the reusable kit component (BuildTabRow):
            // Town / Defenses / Walls (Walls gated by FeatureFlags.WallsTab). Each tab
            // Configure()s this palette for that verb; the active tab carries a gilt
            // underline (position/shape tell, not colour alone). Owns tutorial spotlights.
            var tabRow = ElarionUiKit.AddImage(dock.transform, "CategoryTabs",
                new Vector2(0f, trayTop), new Vector2(1f, headerBottom),
                ElarionUiKit.ObsidianFill, rounded: false);
            _tabRowGo = tabRow;
            _tabRow = tabRow.AddComponent<BuildTabRow>();
            _tabRow.Build(tabRow.transform, Configure, _activeType);

            // Bottom: horizontal-scrolling slot-plate card tray in a recessed dark well
            // (content-width now, so it reads as a dock — not a screen-wide wall).
            var tray = ElarionUiKit.AddImage(dock.transform, "CardTray",
                new Vector2(0f, 0f), new Vector2(1f, trayTop),
                new Color(0f, 0f, 0f, 0.55f), rounded: false);
            _trayGo = tray;

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

            // (2026-07-16 redesign) The old full-dock "PlacingSummary" ObsidianFill panel
            // was REMOVED — spanning 0..headerBottom of the dock, it WAS the black wall that
            // covered the map during placement. The "Placing: <name>" text now lives as a
            // slim pill in the HUD intent cluster (BuildHudController.SetPlacingLabel), so
            // Collapse just hides every dock background and leaves the map fully visible.

            _canvas.SetActive(false);   // built hidden; Show shows it
        }

        // ── Grok slice 4: collapse-on-place (owner "minimize on select") ──────────

        /// <summary>
        /// FULLY minimize the shop while placing (owner redesign 2026-07-16): hide EVERY
        /// dock background — the header band, the tab row, AND the card tray — so NO black
        /// wall covers the map/ghost. The "Placing: &lt;name&gt;" label is folded into the
        /// HUD intent cluster (BuildHudController.SetPlacingLabel), so the dock shows no
        /// summary panel of its own. The dev-only Orient button (a DOCK child, not a header
        /// child) stays reachable — UpdateOrientButton keeps its armed+dev gate. Called from
        /// BuildModeController.Arm. <paramref name="armedDisplayName"/> is retained for API
        /// compat (the label is now owned by the HUD). Safe before build (no-op).
        /// </summary>
        public void Collapse(string armedDisplayName)
        {
            if (_canvas == null) return;
            if (_topBarGo != null) _topBarGo.SetActive(false);
            if (_trayGo != null) _trayGo.SetActive(false);
            if (_tabRowGo != null) _tabRowGo.SetActive(false);
            UpdateOrientButton();   // keep the dev Orient button correct while armed
            FlowTrace.Step("BuildHud",
                "palette collapsed: all dock chrome hidden (no black wall) — Placing label folded into intent bar");
        }

        /// <summary>Expand the shop back to the full header + tabs + carousel (called from CancelArmed).</summary>
        public void Expand()
        {
            if (_canvas == null) return;
            if (_topBarGo != null) _topBarGo.SetActive(true);
            if (_tabRowGo != null) _tabRowGo.SetActive(true);
            if (_trayGo != null) _trayGo.SetActive(true);
        }

        /// <summary>
        /// Public wrapper over <see cref="ResolveEntryArt"/> so the Build HUD carousel can
        /// reuse the SAME data-driven card art (Grok reuse ledger) without a second resolver.
        /// </summary>
        public static Sprite ResolveEntryArtPublic(CatalogEntry e) => ResolveEntryArt(e);

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
                    if (e.id != null && _lockedIds.Contains(e.id)) continue;   // unlock-gated — see Configure/_lockedIds
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
            // First-build freebie (owner 2026-07-13): the card reads the SAME effective
            // cost as the validator/commit (BuildModeController.EffectiveCostFor), so a
            // live freebie is a zero cost = the card never greys out on a first build.
            bool freebie = BuildModeController.FreeBuildAvailable(e);
            DeNelle.Core.Catalog.ResourceCost cost = freebie ? default : CostFor(e);
            bool affordable = CanAfford(cost);
            bool armed = e.id == _armedId;

            // Slot-plate card: the Blink "slot_action" plate as the face (Obsidian fill
            // fallback when the mirrored art is absent), a Button over the whole plate.
            var cardGo = new GameObject("Card_" + e.id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            cardGo.transform.SetParent(_stripContent, false);
            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260f, 0f);
            cardGo.GetComponent<LayoutElement>().preferredWidth = 260f;

            var img = cardGo.GetComponent<Image>();
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotAction);
            if (plate != null)
            {
                img.sprite = plate;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                // WO (owner felt-test 2026-07-16, said twice): the armed card used to
                // FLOOD the whole plate gilt — on the sliced SlotAction plate that gold
                // wash read as "a big yellow circle drawn around the card." Removed. The
                // plate now keeps its own obsidian face whether armed or not; the armed
                // cue is a GLOW on the ICON (built below in BuildCard), so the selection
                // reads as "this item is glowing," not "a ring is around it."
                img.color = Color.white;
            }
            else
            {
                img.color = ElarionUiKit.ObsidianFill;
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

            // Armed = bright gilt name (the icon glows below; the label now sits on the
            // plate's normal obsidian face, so gilt reads — the old dark Ink assumed a
            // gold-flooded plate that no longer exists).
            var nameLabel = MakeText(cardGo.transform,
                string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName,
                14, armed ? ElarionUi.Gilt : ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.96f));
            nameLabel.raycastTarget = false;

            // ── Art band UNDER the name (owner 2026-07-06) ────────────────────
            // Priority: (a) Resources/Portraits/<key> building portraits (catalog id,
            // then displayName slug — the key comes from the entry's own data, no
            // per-tower switch), (b) the concept-icons.json table via
            // ConceptIconResolver (data decides), (c) a procedural obsidian plate
            // carrying the entry's initial — NEVER a blank band (null-art law).
            // -- Armed GLOW on the ICON (owner felt-test 2026-07-16, said twice) --
            // Replaces the removed gold-flooded plate (the "big yellow circle"). A soft
            // gilt halo is built BEFORE the art band so it renders BEHIND the icon (the
            // light reads as emanating FROM the item), then a gentle emissive pulse
            // (IconGlowPulse) makes the selected item visibly glow. Sprite-first (the
            // kit rounded sprite via AddImage rounded:true) with a flat tinted-quad
            // fallback baked into ApplyRounded — it can NEVER blank if the sprite build
            // failed under WebGL. ASCII only; no Blink runtime refs.
            if (armed)
            {
                FlowTrace.Step("BuildHud", "armed glow: soft gilt icon halo on card id=" + e.id);
                var glowGo = ElarionUiKit.AddImage(cardGo.transform, "ArmedIconGlow",
                    new Vector2(0.02f, 0.14f), new Vector2(0.98f, 0.82f),
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.55f),
                    rounded: true);
                var glowImg = glowGo.GetComponent<Image>();
                if (glowImg != null) glowImg.raycastTarget = false;
                glowGo.AddComponent<IconGlowPulse>();
            }

            var art = ResolveEntryArt(e);
            var bandGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            bandGo.transform.SetParent(cardGo.transform, false);
            var brt = (RectTransform)bandGo.transform;
            brt.anchorMin = new Vector2(0.10f, 0.26f);
            brt.anchorMax = new Vector2(0.90f, 0.68f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bandImg = bandGo.GetComponent<Image>();
            bandImg.raycastTarget = false;
            if (art != null)
            {
                bandImg.sprite = art;
                bandImg.preserveAspect = true;
                // Armed = the icon itself reads warm/lit (over its glow halo); rest = plain white.
                bandImg.color = armed ? new Color(1f, 0.965f, 0.82f, 1f) : Color.white;
            }
            else
            {
                // (c) fallback plate: recessed dark well + the entry's gilt initial.
                bandImg.color = new Color(0f, 0f, 0f, 0.45f);
                string glyphSource = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;
                string glyph = string.IsNullOrEmpty(glyphSource)
                    ? "?" : glyphSource.Substring(0, 1).ToUpperInvariant();
                MakeText(bandGo.transform, glyph, 30, ElarionUi.Gilt, FontStyles.Bold,
                    TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            }

            // A live freebie says "FREE" in so many words — the WORD carries the meaning
            // (owner is red/green colorblind; never color-alone). After the one-shot flag
            // is consumed the label reverts to the normal per-resource cost. ASCII only.
            var costLabel = MakeText(cardGo.transform, freebie ? "FREE" : CostLabel(cost), 13,
                affordable ? ElarionUi.Affordable : ElarionUi.Danger, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.24f));
            costLabel.raycastTarget = false;

            // ── Targeting tag (towers only) — at-a-glance anti-air read ─────────
            // A compact "Land / Air / Land+Air" caption pinned to the bottom of the art
            // band so the player counters the flying dragon BEFORE tapping into detail
            // (owner 2026-07-08: Ballista = Air only, ground towers = Land only, Wizard/
            // Arcane = Land + Air). Colorblind-safe: meaning is the TEXT, never color
            // alone (owner is red/green colorblind). ASCII-only — WO-683: the old
            // leading shape glyphs rendered as tofu boxes on the shipped TMP font.
            string targetTag = TargetingTagFor(e);
            if (!string.IsNullOrEmpty(targetTag))
            {
                var tagBackGo = new GameObject("TargetTag", typeof(RectTransform), typeof(Image));
                tagBackGo.transform.SetParent(bandGo.transform, false);
                var trt = (RectTransform)tagBackGo.transform;
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 0.30f);
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                var tagImg = tagBackGo.GetComponent<Image>();
                tagImg.color = new Color(0f, 0f, 0f, 0.62f);   // dark backing for legibility over art
                tagImg.raycastTarget = false;
                var tagLabel = MakeText(tagBackGo.transform, targetTag, 12,
                    ElarionUi.Gilt, FontStyles.Bold, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one);
                tagLabel.raycastTarget = false;
            }
        }

        /// <summary>
        /// Compact targeting-capability caption for a DEFENSIVE tower card, from the repo
        /// flags (RepoProps.airOnly / canHitAir): airOnly → "Air only", canHitAir → "Land
        /// + Air", else "Land only". Null for non-tower structures (no tag). Pure ASCII
        /// TEXT — WO-683 (owner device screenshot 2026-07-12): the old ▲/◆/▬ leading
        /// glyphs render as tofu boxes on the shipped TMP font (the "□ Land + Air"
        /// defect). The words themselves are the color-independent cue (owner colorblind:
        /// meaning by text, never glyph/color alone).
        /// </summary>
        private static string TargetingTagFor(CatalogEntry e)
        {
            if (e == null || e.type != CatalogType.Tower) return null;
            var repo = e.repo;
            if (repo == null) return null;
            bool airOnly   = repo.airOnly;
            bool canHitAir = repo.canHitAir || airOnly;
            if (airOnly)   return "Air only";
            if (canHitAir) return "Land + Air";
            return "Land only";
        }

        // ── Entry art resolution (owner 2026-07-06 image band) ────────────────

        // Session-lifetime cache keyed on the Resources path; nulls are cached too,
        // so a portrait-less entry costs ONE failed lookup, not one per Render
        // (the PortraitCache pattern — DialogueUI/PortraitCache.cs; that class lives
        // in DeNelle.DialogueUI which DeNelle.Village does not reference, so the
        // small load-or-wrap recipe is mirrored here instead of adding a dependency).
        private static readonly Dictionary<string, Sprite> EntryArtCache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resolve a catalog entry's card art, data-driven off the entry itself:
        /// (a) Resources/Portraits/&lt;id&gt; then Portraits/&lt;displayName-slug&gt;
        /// (the existing building-portrait set), (b) the concept-icons.json table
        /// (id / slug / catalog type token) via ConceptIconResolver. Null when no
        /// art exists — the caller renders the glyph fallback plate, never blank.
        /// </summary>
        private static Sprite ResolveEntryArt(CatalogEntry e)
        {
            if (e == null) return null;
            string slug = SlugOf(e.displayName);
            var s = LoadPortrait(e.id);
            if (s == null) s = LoadPortrait(slug);
            if (s != null) return s;
            return ConceptIconResolver.ResolveAny(e.id, slug, e.type.ToString());
        }

        /// <summary>"Archer Tower" -> "archer-tower" (the Portraits/ file convention).</summary>
        private static string SlugOf(string name)
            => string.IsNullOrEmpty(name) ? null : name.Trim().ToLowerInvariant().Replace(' ', '-');

        // Load a Portraits/ sprite directly when possible; fall back to wrapping a
        // Default-imported Texture2D in a runtime Sprite (the portraits import as
        // plain textures, so a bare Resources.Load-as-Sprite returns null for them).
        private static Sprite LoadPortrait(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string path = "Portraits/" + key;
            if (EntryArtCache.TryGetValue(path, out var cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f));
            }
            EntryArtCache[path] = sprite;   // cache nulls too — one lookup per miss
            return sprite;
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
            // WO-697: cost numbers through the ONE kit formatter (compact >= 10k).
            var parts = new List<string>(4);
            if (c.wood     > 0) parts.Add(ElarionUi.CompactNumber(c.wood)     + "W");
            if (c.food     > 0) parts.Add(ElarionUi.CompactNumber(c.food)     + "F");
            if (c.iron     > 0) parts.Add(ElarionUi.CompactNumber(c.iron)     + "I");
            if (c.crystals > 0) parts.Add(ElarionUi.CompactNumber(c.crystals) + "C");
            return string.Join("  ", parts);
        }

        private void UpdateBalance()
        {
            // WO-697: balance through the ONE kit formatter (compact >= 10k).
            if (_balanceLabel != null)
                _balanceLabel.text = "Crystals: " + ElarionUi.CompactNumber(CrystalBalance);
        }

        // ── Category tabs (now the reusable BuildTabRow kit component) ─────────

        /// <summary>Move the gilt underline to the tab matching <see cref="_activeType"/>.
        /// No-op when the tab row was never built (palette not built yet).</summary>
        private void UpdateTabHighlight()
        {
            _tabRow?.SetActive(_activeType);
        }

        /// <summary>Show the Orient button only while an entry is armed.</summary>
        private void UpdateOrientButton()
        {
            // F8-30 — Orient is a DEV offset-authoring tool, not player UI: during the
            // tutorial the owner tapped it next to Done and the orient modal click-locked
            // the screen. Gate it behind the global DevHotkeys kill-switch (default OFF —
            // same gate as AdminOverlay/DebugCanvas), so players/tutorial never see it.
            // WO-707 (owner 2026-07-13 "we need the orient tool at least in dev build —
            // these are sitting wrong"): ALSO visible in Development builds (her felt-test
            // exes), without opening the whole DevHotkeys surface. Ship builds
            // (BuildOptions.None — the WebGL previews/prod) never show it.
            if (_orientBtn != null)
                _orientBtn.gameObject.SetActive(
                    (DeNelle.Core.FeatureFlags.DevHotkeys || Debug.isDebugBuild)
                    && !string.IsNullOrEmpty(_armedId));
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

        // ── Consistent-size pin (mirrors ElarionUiKit.PinCanonicalCtaSize) ──────
        /// <summary>
        /// Collapse a kit button's fraction-of-parent anchors to a POINT at the anchor
        /// rect's centre and stamp a fixed <paramref name="w"/> x <paramref name="h"/>
        /// pixel box, so the wide dock header can never stretch it into a thin bar.
        /// Height must be >= ElarionUiKit.MinTouchPx so the kit touch-floor guard no-ops.
        /// </summary>
        private static void PinSize(Button button, float w, float h)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            Vector2 centre = (rt.anchorMin + rt.anchorMax) * 0.5f;
            rt.anchorMin = centre;
            rt.anchorMax = centre;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
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

    /// <summary>
    /// Gentle emissive pulse for the armed-card icon glow (owner felt-test 2026-07-16,
    /// "instead of the circle use the VFX that makes the item glow"). Eases the halo's
    /// alpha + scale up and down so the SELECTED item visibly GLOWS, rather than sitting
    /// under a static ring/circle. Pure presentation; self-contained; ASCII only. Uses
    /// unscaled time so the pulse breathes even if Build Mode ever pauses gameplay time.
    /// </summary>
    internal sealed class IconGlowPulse : MonoBehaviour
    {
        private Image _img;
        private RectTransform _rt;
        private float _baseAlpha = 0.55f;

        private void Awake()
        {
            _img = GetComponent<Image>();
            _rt = transform as RectTransform;
            if (_img != null) _baseAlpha = _img.color.a;
        }

        private void Update()
        {
            // 0..1 eased breathing wave (~0.5 Hz).
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            if (_img != null)
            {
                var c = _img.color;
                c.a = Mathf.Lerp(_baseAlpha * 0.55f, Mathf.Min(1f, _baseAlpha + 0.30f), k);
                _img.color = c;
            }
            if (_rt != null)
            {
                float s = Mathf.Lerp(0.94f, 1.12f, k);
                _rt.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
