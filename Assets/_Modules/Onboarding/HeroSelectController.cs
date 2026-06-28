// =============================================================================
// HeroSelectController — drives the hero-select screen (intro flow).
// -----------------------------------------------------------------------------
// THE SCREEN (WO-559 CAROUSEL):
//   TOP        : a brand block (title + subtitle) + the eyebrow "— CHOOSE YOUR HERO —".
//   CENTERPIECE: a CAROUSEL over the whole roster. PREV (◄) / NEXT (►) arrow chips
//                flank a HERO STAGE that shows ONE hero at a time:
//                  LEFT  — the HERO, large + focal (gold-framed portrait), with the
//                          LORE (name / role / blurb) directly UNDER the hero.
//                  RIGHT — the hero's STATS (HP / Attack / Speed pips + signature
//                          ability) BESIDE the hero.
//                Responsive: a ROW in landscape, re-flows to a COLUMN in portrait
//                (never fixed pixel columns).
//   INDICATOR  : a row of pip dots (●/○) showing which roster slot is on screen.
//   FOOTER     : a single confirm CTA. On the playable hero (Grom == Knight) it reads
//                "Enter Elarion" and is enabled — it persists the hero and routes to
//                the home hub (MainCastle_Hall). On a LOCKED hero the CTA is disabled
//                and reads "Coming Soon".
//
//   LOCKED HEROES: the other three heroes (Thrain/Mage, Sylas/Ranger, Elara/Cleric)
//   ARE navigable in the carousel — the player can PREVIEW their image + lore + stats —
//   but they are NOT selectable: the stage shows a dark scrim + LOCK badge over the
//   image and the confirm CTA is disabled. Only the playable hero can be confirmed.
//
// WHY THIS KEPT REGRESSING (root cause, fixed long ago, preserved here):
//   The old screen BOUND to named elements inside HeroSelectScreen.uxml
//   (_root.Q<VisualElement>("hero-dragon-stage") …). Per CLAUDE.md §8 / PIPELINE §8,
//   UXML + USS do NOT render reliably in the player build — when the UXML tree failed
//   to instantiate, every Q<>() returned null and the screen came up empty.
//   THE FIX (kept): this controller CLEARS the root and BUILDS THE ENTIRE LAYOUT IN
//   CODE. It does not read a single name out of the UXML, so a UXML that doesn't
//   render can no longer break this screen. The tree is flex/percent sized so it
//   re-centres in both orientations; a GeometryChangedEvent handler re-flows on
//   resize; a post-build self-assert verifies the carousel pieces exist.
//
// COPY: every hero name / role / blurb (the lore) is resolved from en.json via
// CanonStrings at runtime (port-spec Part 4). The card accent colour + glyph come
// from HeroCatalog (pure presentation data, legitimately in C#).
//
// STYLE: code-built UIElements (the HeroSelect scene host is a UIDocument, not a uGUI
// Canvas), dressed in the shared ElarionUi black+gold "Obsidian" palette — the same
// design language ElarionUiKit sources from. (ElarionUiKit itself is uGUI and cannot
// host on a UIDocument without regenerating the scene; see WO-559 decision flag 1.)
//
// PERSISTENCE: on confirm, GameStateService.ChooseHero(hero) writes
// GameState.HeroClass and Save()s it, then routes onward.
//
// RETURNING-PLAYER SKIP: a save that already records a hero has finished the intro
// flow — the screen self-skips straight to the Castle so a returning player is not
// re-asked.
//
// Lives in DeNelle.Onboarding; references DeNelle.Core only — module isolation.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Drives the hero-select CAROUSEL (WO-559): builds (in code) a roster carousel —
    /// prev/next arrows flanking a hero stage that shows ONE hero at a time (large
    /// focal image + lore UNDER it on the left, stats BESIDE it on the right) plus a
    /// pip-dot indicator. Every hero is navigable so the player can preview the roster;
    /// only the playable hero (Grom == Knight) is selectable — locked heroes show a
    /// LOCK badge and a disabled "Coming Soon" CTA. On confirm it writes
    /// <see cref="GameState.HeroClass"/> and routes to the home hub. Fully responsive —
    /// the stage re-flows row↔column on orientation change. A returning player who
    /// already chose a hero is skipped straight to the Castle.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HeroSelectController : MonoBehaviour
    {
        // -- en.json keys for the screen's own copy --------------------------
        private const string TitleKey    = "heroSelect.title";
        private const string SubtitleKey = "heroSelect.subtitle";
        // The confirm CTA en.json key (falls back to "Enter Elarion").
        // Routes straight to the home hub (no pet-select step).
        private const string DiveKey    = "heroSelect.diveVillage";

        // -- Palette — SOURCED from the shared ElarionUi town-HUD language ----
        // (Matches the town HUD — dark glass + ornate gold-rune frames. Role-named
        // locals keep the layout below readable while every colour resolves canonically.)
        private static readonly Color ColBackground   = ElarionUi.PanelStoneDark;   // full-screen stone
        private static readonly Color ColPanelDark     = ElarionUi.PanelStone;       // roster panel fill
        private static readonly Color ColAmber         = ElarionUi.Gold;             // accents / eyebrow → runic gold
        private static readonly Color ColTextBright    = ElarionUi.Parchment;        // primary text
        private static readonly Color ColTextMuted     = ElarionUi.ParchmentDim;     // secondary text

        [Header("UI")]
        [Tooltip("UIDocument hosting the hero-select panel. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Behaviour")]
        [Tooltip("Skip straight to the Castle when the save already records a hero " +
                 "(a returning player who finished the intro). Editor testing: " +
                 "disable to always show the screen.")]
        [SerializeField] private bool _skipWhenIntroComplete = true;

        // -- Built UI elements (all created in code) -------------------------
        // WO-559 carousel layout: prev/next arrows flank a HERO STAGE (large image +
        // lore under it on the left, stats beside it on the right) with a pip-dot row.
        private VisualElement _root;
        private VisualElement _carousel;      // row: ◄ | stage | ►
        private VisualElement _heroStage;     // row (landscape) / column (portrait): image+lore | stats
        private VisualElement _stageLeft;     // image + lore column (rebuilt per index)
        private VisualElement _stageRight;    // stats panel column (rebuilt per index)
        private VisualElement _dotsRow;       // pip-dot index indicator
        private Button _prevButton;
        private Button _nextButton;

        private bool _built;
        private bool _hasSelection;
        private HeroClass _selectedHero;

        // Which roster slot is on screen (index into HeroCatalog.Heroes).
        private int _carouselIndex;

        // The single playable hero in V1 — Grom == HeroClass.Knight (KnightOnly ON).
        private const HeroClass PlayableHero = HeroClass.Knight;

        // Confirm CTA — "Enter Elarion" on the playable hero; "Coming Soon" (disabled)
        // on a locked hero.
        private Button _diveButton;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // A returning player who already finished the intro skips this
            // screen entirely — route on before building any UI.
            if (_skipWhenIntroComplete && IsIntroComplete())
            {
                SceneRouter.GoCastle();
                return;
            }

            BuildScreen();
        }

        private void OnDisable()
        {
            if (_diveButton != null) _diveButton.clicked -= OnDiveVillageClicked;
            if (_prevButton != null) _prevButton.clicked -= OnPrevClicked;
            if (_nextButton != null) _nextButton.clicked -= OnNextClicked;
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _built = false;
        }

        // =====================================================================
        //  Returning-player gate
        // =====================================================================

        /// <summary>
        /// True when the save already records a chosen hero — the intro flow is
        /// finished and this screen has nothing to ask. The gate is HeroClass alone
        /// (the pet-select step is gone for single-hero V1).
        /// </summary>
        private static bool IsIntroComplete()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false; // first launch
            return svc.State.HeroClass != HeroClassOpt.None;
        }

        // =====================================================================
        //  Code-built layout — NO UXML dependency (regression-proof)
        // =====================================================================

        /// <summary>
        /// Builds the entire hero-select CAROUSEL in code on a cleared root. Does not
        /// read any UXML element by name, so a UXML that fails to render in the build
        /// (CLAUDE.md §8) cannot break this screen. Structure:
        ///   root (column)
        ///     └─ roster panel  (full screen, opaque)
        ///          ├─ brand       (title + subtitle)
        ///          ├─ eyebrow     ("— CHOOSE YOUR HERO —")
        ///          ├─ carousel    (◄ | hero stage [image+lore | stats] | ►)
        ///          ├─ dots        (pip-dot index indicator)
        ///          └─ footer      (confirm CTA)
        /// </summary>
        private void BuildScreen()
        {
            using var _ = FlowTrace.Enter("Onboarding", "HeroSelectController.BuildScreen");
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                // P0 — no root means the hero-select renders NOTHING (a blank screen the
                // player can't pass). Fail-loud to the break-log rather than a quiet warn.
                FlowTrace.Fail("Onboarding", "BuildScreen: NO UIDocument root — hero-select will NOT display (BLANK SCREEN).");
                return;
            }

            // Wipe whatever the UXML instantiated (or didn't) — we own the tree now.
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.alignItems = Align.Stretch;
            _root.style.justifyContent = Justify.FlexStart;
            _root.style.backgroundColor = ColBackground;

            // Open the carousel ON the playable hero so the screen starts on the
            // selectable, pre-selected Grom (not a locked hero).
            _carouselIndex = IndexOf(PlayableHero);

            BuildRosterPanel();   // full screen (brand + eyebrow + carousel + dots + CTA)

            // Pre-persist the playable hero so GameState always has a valid class even
            // if the player confirms without navigating (KnightOnly-forced; idempotent).
            GameStateService.Instance?.ChooseHero(PlayableHero);

            // Paint the opening hero into the stage (sets selection + CTA + dots).
            PopulateStage(_carouselIndex);

            // Re-flow the responsive layout on every screen-size / orientation change.
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            _built = true;

            // Run the orientation re-flow + self-assert once now in case the panel
            // already has its geometry (so we never wait a frame to verify).
            ReflowForSize(_root.resolvedStyle.width, _root.resolvedStyle.height);

            // V — prove the screen built: root has children AND the carousel + stage
            // columns + arrows + dots exist. A built-but-empty hero-select is a
            // dead-end screen, so a missing piece Fails.
            int rootChildren = _root.childCount;
            bool carouselOk = _carousel != null && _heroStage != null
                              && _stageLeft != null && _stageRight != null
                              && _prevButton != null && _nextButton != null;
            int dotCount = _dotsRow != null ? _dotsRow.childCount : 0;
            if (rootChildren == 0 || !carouselOk || dotCount == 0)
            {
                FlowTrace.Fail("Onboarding",
                    $"BuildScreen VERIFY FAILED — rootChildren={rootChildren} carouselOk={carouselOk} dots={dotCount}. " +
                    "Hero-select carousel built but EMPTY/incomplete (blank/dead-end screen).");
            }
            else
            {
                FlowTrace.Step("Onboarding",
                    $"BuildScreen VERIFY ok — rootChildren={rootChildren} carouselOk={carouselOk} dots={dotCount}.");
            }
        }

        // ── Full screen: roster panel ───────────────────────────────────────

        private void BuildRosterPanel()
        {
            var roster = new VisualElement { name = "hero-roster-panel" };
            roster.style.flexGrow = 1f;          // takes the full screen
            roster.style.flexShrink = 1f;
            roster.style.flexBasis = 0f;
            roster.style.backgroundColor = ColPanelDark;
            roster.style.flexDirection = FlexDirection.Column;
            roster.style.alignItems = Align.Stretch;
            roster.style.justifyContent = Justify.FlexStart;
            roster.style.paddingTop = 18f;
            roster.style.paddingBottom = 16f;
            roster.style.paddingLeft = 10f;
            roster.style.paddingRight = 10f;

            // Brand block — title + subtitle at the top of the roster.
            var brand = new VisualElement { name = "hero-brand-block" };
            brand.style.alignSelf = Align.Stretch;
            brand.style.alignItems = Align.Center;
            brand.style.marginBottom = 12f;
            brand.style.flexShrink = 0f;
            brand.pickingMode = PickingMode.Ignore;

            var title = new Label(CanonStrings.Locale(TitleKey));
            title.style.fontSize = 28f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ColTextBright;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            brand.Add(title);

            var subtitle = new Label(CanonStrings.Locale(SubtitleKey));
            subtitle.style.marginTop = 4f;
            subtitle.style.fontSize = 13f;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitle.style.color = ColAmber;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            brand.Add(subtitle);

            roster.Add(brand);

            // Amber divider seam below the brand.
            var divider = new VisualElement();
            divider.style.height = 2f;
            divider.style.flexShrink = 0f;
            divider.style.marginBottom = 10f;
            divider.style.alignSelf = Align.Stretch;
            divider.style.backgroundColor = ColAmber;
            roster.Add(divider);

            // Eyebrow — the carousel prompt.
            var eyebrow = new Label("— CHOOSE YOUR HERO —");
            eyebrow.style.fontSize = 12f;
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            eyebrow.style.color = ColAmber;
            eyebrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            eyebrow.style.marginBottom = 8f;
            eyebrow.style.flexShrink = 0f;
            roster.Add(eyebrow);

            // ── CAROUSEL — the centerpiece: ◄ | hero stage | ►.
            BuildCarousel();
            roster.Add(_carousel);

            // ── DOTS — pip index indicator under the carousel.
            BuildDots();
            roster.Add(_dotsRow);

            // Footer — the single confirm CTA.
            var footer = new VisualElement { name = "select-footer" };
            footer.style.marginTop = 12f;
            footer.style.flexShrink = 0f;
            footer.style.alignSelf = Align.Stretch;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.Center;
            footer.style.alignItems = Align.Center;

            _diveButton = new Button { text = FallbackLocale(DiveKey, "Enter Elarion") };
            StyleCta(_diveButton, secondary: false);
            _diveButton.clicked += OnDiveVillageClicked;
            footer.Add(_diveButton);

            roster.Add(footer);
            _root.Add(roster);
        }

        // =====================================================================
        //  Carousel — arrows + a hero stage that re-paints per index
        // =====================================================================

        /// <summary>
        /// Builds the carousel row: a PREV (◄) arrow chip, the hero stage (empty
        /// containers — <see cref="PopulateStage"/> fills them per index), and a NEXT
        /// (►) arrow chip. The arrows + stage containers are persistent; only the
        /// stage CONTENT is rebuilt on navigation.
        /// </summary>
        private void BuildCarousel()
        {
            _carousel = new VisualElement { name = "hero-carousel" };
            _carousel.style.flexGrow = 1f;
            _carousel.style.flexShrink = 1f;
            _carousel.style.flexDirection = FlexDirection.Row;
            _carousel.style.alignItems = Align.Center;
            _carousel.style.justifyContent = Justify.Center;

            _prevButton = BuildArrow("◄", "carousel-prev");
            _prevButton.clicked += OnPrevClicked;
            _carousel.Add(_prevButton);

            // Hero stage — row (landscape): image+lore | stats. ReflowForSize flips it
            // to a column in portrait. Empty until PopulateStage runs.
            _heroStage = new VisualElement { name = "hero-stage" };
            _heroStage.style.flexGrow = 1f;
            _heroStage.style.flexShrink = 1f;
            _heroStage.style.flexBasis = 0f;
            _heroStage.style.minWidth = 0f;
            _heroStage.style.flexDirection = FlexDirection.Row;
            _heroStage.style.alignItems = Align.Stretch;
            _heroStage.style.justifyContent = Justify.Center;

            // LEFT column: large image + lore under it (the larger half).
            _stageLeft = new VisualElement { name = "hero-stage-left" };
            _stageLeft.style.flexGrow = 1.3f;
            _stageLeft.style.flexShrink = 1f;
            _stageLeft.style.flexBasis = 0f;
            _stageLeft.style.minWidth = 0f;
            _stageLeft.style.flexDirection = FlexDirection.Column;
            _stageLeft.style.alignItems = Align.Stretch;
            _stageLeft.style.marginRight = 8f;
            _heroStage.Add(_stageLeft);

            // RIGHT column: the stats panel BESIDE the image.
            _stageRight = new VisualElement { name = "hero-stage-right" };
            _stageRight.style.flexGrow = 1f;
            _stageRight.style.flexShrink = 1f;
            _stageRight.style.flexBasis = 0f;
            _stageRight.style.minWidth = 0f;
            _stageRight.style.justifyContent = Justify.Center;
            _stageRight.style.marginLeft = 8f;
            _heroStage.Add(_stageRight);

            _carousel.Add(_heroStage);

            _nextButton = BuildArrow("►", "carousel-next");
            _nextButton.clicked += OnNextClicked;
            _carousel.Add(_nextButton);
        }

        /// <summary>A gold-trim circular-ish arrow chip button (◄ / ►) for carousel nav.</summary>
        private Button BuildArrow(string glyph, string name)
        {
            var btn = new Button { text = glyph, name = name };
            btn.style.flexShrink = 0f;
            btn.style.width = 44f;
            btn.style.height = 44f;
            btn.style.marginLeft = 4f;
            btn.style.marginRight = 4f;
            btn.style.fontSize = 22f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            float r = 22f;
            btn.style.borderTopLeftRadius = r;
            btn.style.borderTopRightRadius = r;
            btn.style.borderBottomLeftRadius = r;
            btn.style.borderBottomRightRadius = r;
            SetBorderWidth(btn, 2f);
            SetBorderColor(btn, new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.9f));
            btn.style.backgroundColor = ElarionUi.GoldButton;
            btn.style.color = ElarionUi.Ink;
            return btn;
        }

        /// <summary>Builds the pip-dot index indicator (one dot per roster slot).</summary>
        private void BuildDots()
        {
            _dotsRow = new VisualElement { name = "hero-dots" };
            _dotsRow.style.flexDirection = FlexDirection.Row;
            _dotsRow.style.justifyContent = Justify.Center;
            _dotsRow.style.alignItems = Align.Center;
            _dotsRow.style.flexShrink = 0f;
            _dotsRow.style.marginTop = 10f;
            _dotsRow.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
            {
                var dot = new Label("●") { name = $"dot-{i}" };
                dot.style.fontSize = 14f;
                dot.style.marginLeft = 3f;
                dot.style.marginRight = 3f;
                dot.style.color = ColTextMuted;
                _dotsRow.Add(dot);
            }
        }

        /// <summary>
        /// Re-paints the hero stage for the roster slot at <paramref name="index"/>:
        /// rebuilds the LEFT (large focal image + LOCK badge when locked + lore under
        /// it) and RIGHT (stats panel) content, then refreshes the lock state, the
        /// confirm CTA (enabled "Enter Elarion" on the playable hero, disabled
        /// "Coming Soon" on a locked hero) and the dot indicator. The selection is the
        /// playable hero only — navigating to a locked hero is a PREVIEW, not a pick.
        /// </summary>
        private void PopulateStage(int index)
        {
            if (_stageLeft == null || _stageRight == null) return;
            if (HeroCatalog.Heroes.Length == 0) return;

            index = ((index % HeroCatalog.Heroes.Length) + HeroCatalog.Heroes.Length) % HeroCatalog.Heroes.Length;
            _carouselIndex = index;
            HeroCardInfo info = HeroCatalog.Heroes[index];
            bool playable = IsPlayable(info.Hero);

            _stageLeft.Clear();
            _stageRight.Clear();

            // ── LEFT: large hero image (focal) + lore directly under it.
            var imageFrame = new VisualElement { name = "hero-image-frame" };
            imageFrame.style.flexGrow = 1f;
            imageFrame.style.flexShrink = 1f;
            imageFrame.style.minHeight = 160f;
            imageFrame.style.overflow = Overflow.Hidden;
            imageFrame.style.backgroundColor = new Color(0f, 0f, 0f, 0.30f);
            float fr = 10f;
            imageFrame.style.borderTopLeftRadius = fr;
            imageFrame.style.borderTopRightRadius = fr;
            imageFrame.style.borderBottomLeftRadius = fr;
            imageFrame.style.borderBottomRightRadius = fr;
            SetBorderWidth(imageFrame, 3f);
            SetBorderColor(imageFrame, playable ? ElarionUi.Gilt
                                                : new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.4f));

            var portrait = new VisualElement { name = "hero-portrait" };
            portrait.style.position = Position.Absolute;
            portrait.style.left = 0f;
            portrait.style.right = 0f;
            portrait.style.top = 0f;
            portrait.style.bottom = 0f;
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;
            if (!playable) portrait.style.opacity = 0.5f;   // dim a locked hero
            ApplyPortrait(portrait, info, glyphSize: 96f);
            imageFrame.Add(portrait);

            // LOCK overlay (dark scrim + LOCK badge + "Coming Soon") for a locked hero.
            if (!playable)
            {
                var scrim = new VisualElement { name = "lock-scrim" };
                scrim.style.position = Position.Absolute;
                scrim.style.left = 0f;
                scrim.style.right = 0f;
                scrim.style.top = 0f;
                scrim.style.bottom = 0f;
                scrim.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
                scrim.style.alignItems = Align.Center;
                scrim.style.justifyContent = Justify.Center;
                scrim.pickingMode = PickingMode.Ignore;

                var lockBadge = new Label("LOCKED");
                lockBadge.style.fontSize = 18f;
                lockBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                lockBadge.style.color = ElarionUi.Parchment;
                lockBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
                scrim.Add(lockBadge);

                var comingSoon = new Label("Coming Soon");
                comingSoon.style.marginTop = 2f;
                comingSoon.style.fontSize = 11f;
                comingSoon.style.unityFontStyleAndWeight = FontStyle.Italic;
                comingSoon.style.color = ColTextMuted;
                comingSoon.style.unityTextAlign = TextAnchor.MiddleCenter;
                scrim.Add(comingSoon);

                imageFrame.Add(scrim);
            }

            _stageLeft.Add(imageFrame);

            // Lore UNDER the image: name (gold, big) + role (amber) + blurb.
            var story = new VisualElement { name = "hero-lore" };
            story.style.flexShrink = 0f;
            story.style.marginTop = 10f;
            story.style.paddingLeft = 2f;
            story.style.paddingRight = 2f;

            var nameLabel = new Label(CanonStrings.Locale(info.NameKey));
            nameLabel.style.fontSize = 24f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = playable ? ElarionUi.Gold : ColTextMuted;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            story.Add(nameLabel);

            var roleLabel = new Label(CanonStrings.Locale(info.RoleKey));
            roleLabel.style.marginTop = 2f;
            roleLabel.style.fontSize = 13f;
            roleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            roleLabel.style.color = ColAmber;
            roleLabel.style.whiteSpace = WhiteSpace.Normal;
            story.Add(roleLabel);

            var blurbLabel = new Label(CanonStrings.Locale(info.BlurbKey));
            blurbLabel.style.marginTop = 6f;
            blurbLabel.style.fontSize = 13f;
            blurbLabel.style.color = ColTextBright;
            blurbLabel.style.whiteSpace = WhiteSpace.Normal;
            story.Add(blurbLabel);

            _stageLeft.Add(story);

            // ── RIGHT: the stats panel BESIDE the image.
            var statsPanel = new VisualElement { name = "hero-stats-panel" };
            statsPanel.style.paddingTop = 14f;
            statsPanel.style.paddingBottom = 14f;
            statsPanel.style.paddingLeft = 16f;
            statsPanel.style.paddingRight = 16f;
            float pr = 12f;
            statsPanel.style.borderTopLeftRadius = pr;
            statsPanel.style.borderTopRightRadius = pr;
            statsPanel.style.borderBottomLeftRadius = pr;
            statsPanel.style.borderBottomRightRadius = pr;
            SetBorderWidth(statsPanel, 2f);
            SetBorderColor(statsPanel, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f));
            statsPanel.style.backgroundColor = ElarionUi.PanelStone;

            BuildStatRow(statsPanel, "HP",     PipString(info.Hp));
            BuildStatRow(statsPanel, "ATTACK", PipString(info.Attack));
            BuildStatRow(statsPanel, "SPEED",  PipString(info.Speed));

            var statDivider = new VisualElement();
            statDivider.style.height = 1f;
            statDivider.style.marginTop = 10f;
            statDivider.style.marginBottom = 8f;
            statDivider.style.backgroundColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.4f);
            statsPanel.Add(statDivider);

            var abilityName = new Label(info.AbilityName);
            abilityName.style.fontSize = 15f;
            abilityName.style.unityFontStyleAndWeight = FontStyle.Bold;
            abilityName.style.color = ColAmber;
            abilityName.style.whiteSpace = WhiteSpace.Normal;
            statsPanel.Add(abilityName);

            var abilityDesc = new Label(info.AbilityDesc);
            abilityDesc.style.marginTop = 2f;
            abilityDesc.style.fontSize = 12f;
            abilityDesc.style.color = ColTextMuted;
            abilityDesc.style.whiteSpace = WhiteSpace.Normal;
            statsPanel.Add(abilityDesc);

            _stageRight.Add(statsPanel);

            // Selection: only the playable hero can be selected. Navigating to a
            // locked hero previews it but leaves no selectable choice.
            if (playable)
            {
                _selectedHero = info.Hero;
                _hasSelection = true;
            }
            else
            {
                _hasSelection = false;
            }

            RefreshConfirm(playable);
            RefreshDots();

            // Re-apply orientation flow so a freshly-built stage stacks correctly.
            if (_root != null)
                ReflowForSize(_root.resolvedStyle.width, _root.resolvedStyle.height);
        }

        // =====================================================================
        //  Carousel navigation
        // =====================================================================

        private void OnPrevClicked() => PopulateStage(_carouselIndex - 1);
        private void OnNextClicked() => PopulateStage(_carouselIndex + 1);

        // =====================================================================
        //  Confirm CTA + dot indicator state
        // =====================================================================

        /// <summary>
        /// Refreshes the confirm CTA for the current hero: enabled "Enter Elarion" on
        /// the playable hero, disabled "Coming Soon" on a locked hero.
        /// </summary>
        private void RefreshConfirm(bool playable)
        {
            if (_diveButton == null) return;
            _diveButton.text = playable ? FallbackLocale(DiveKey, "Enter Elarion") : "Coming Soon";
            _diveButton.SetEnabled(playable && _hasSelection);
        }

        /// <summary>Highlights the dot for the on-screen hero (gold), dims the rest.</summary>
        private void RefreshDots()
        {
            if (_dotsRow == null) return;
            for (int i = 0; i < _dotsRow.childCount; i++)
            {
                var dot = _dotsRow[i];
                dot.style.color = (i == _carouselIndex) ? ColAmber : ColTextMuted;
            }
        }

        // =====================================================================
        //  Shared stat-row + pip helpers
        // =====================================================================

        /// <summary>
        /// Builds one stat row into the stats panel — a fixed-width gold label on the
        /// left and a pre-filled 1-5 pip value on the right.
        /// </summary>
        private void BuildStatRow(VisualElement parent, string label, string pipValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2f;

            var key = new Label(label);
            key.style.width = 64f;
            key.style.flexShrink = 0f;
            key.style.fontSize = 11f;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.style.color = ColAmber;
            row.Add(key);

            var value = new Label(pipValue);
            value.style.fontSize = 14f;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.color = ColTextBright;
            value.style.letterSpacing = 2f;
            row.Add(value);

            parent.Add(row);
        }

        /// <summary>
        /// Renders a 1-5 rating as filled / empty pips (e.g. ●●●○○ for 3). A clear,
        /// glanceable archetype read that needs no extra art dependency.
        /// </summary>
        private static string PipString(int value)
        {
            if (value < 0) value = 0;
            if (value > 5) value = 5;
            return new string('●', value) + new string('○', 5 - value);
        }

        /// <summary>
        /// Applies a hero's portrait art (cover-fit) to a portrait element, falling
        /// back to the accent glyph when the art is absent. No missing-asset risk —
        /// Grom/Thrain/Sylas/Elara portraits all exist in Resources/HeroPortraits.
        /// </summary>
        private static void ApplyPortrait(VisualElement portrait, HeroCardInfo info, float glyphSize)
        {
            if (portrait == null || info == null) return;
            string slug = SlugFor(info.Hero);
            var portraitSprite = Resources.Load<Sprite>($"HeroPortraits/{slug}");
            if (portraitSprite != null)
            {
                portrait.style.backgroundImage = new StyleBackground(portraitSprite);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                return;
            }
            var portraitTex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
            if (portraitTex != null)
            {
                portrait.style.backgroundImage = new StyleBackground(portraitTex);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                return;
            }
            var glyph = new Label(info.Glyph);
            glyph.style.fontSize = glyphSize;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.color = info.Accent;
            portrait.Add(glyph);
        }

        // =====================================================================
        //  Responsive re-flow + self-assert
        // =====================================================================

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ReflowForSize(evt.newRect.width, evt.newRect.height);
        }

        /// <summary>
        /// Re-flows the hero stage for the current screen size / orientation:
        /// LANDSCAPE = a ROW (large image + lore on the left, stats on the right);
        /// PORTRAIT = a COLUMN (image + lore on top, stats below) so nothing crowds.
        /// No fixed x positions — purely flex — so it re-centres identically in both
        /// orientations. Then self-asserts.
        /// </summary>
        private void ReflowForSize(float width, float height)
        {
            if (!_built || _heroStage == null) return;
            if (width <= 0f || height <= 0f) return;

            bool portrait = height >= width;

            _heroStage.style.flexDirection = portrait ? FlexDirection.Column : FlexDirection.Row;

            if (_stageLeft != null)
            {
                _stageLeft.style.marginRight = portrait ? 0f : 8f;
                _stageLeft.style.marginBottom = portrait ? 10f : 0f;
            }
            if (_stageRight != null)
            {
                _stageRight.style.marginLeft = portrait ? 0f : 8f;
            }

            VerifyStage();
        }

        /// <summary>
        /// Regression guard: asserts the carousel pieces (stage + both columns + prev/
        /// next arrows + the dot indicator) are present and the index is in range. Logs
        /// loudly if a future edit breaks the contract so it is caught immediately.
        /// </summary>
        private void VerifyStage()
        {
            if (_heroStage == null || _stageLeft == null || _stageRight == null
                || _prevButton == null || _nextButton == null)
            {
                FlowTrace.Fail("Onboarding",
                    "VerifyStage: CAROUSEL ASSERT FAILED — stage / columns / arrows missing.");
                return;
            }

            int dots = _dotsRow != null ? _dotsRow.childCount : 0;
            if (dots != HeroCatalog.Heroes.Length)
            {
                FlowTrace.Fail("Onboarding",
                    $"VerifyStage: DOTS ASSERT FAILED — expected {HeroCatalog.Heroes.Length} dots, found {dots}.");
                return;
            }

            if (_carouselIndex < 0 || _carouselIndex >= HeroCatalog.Heroes.Length)
            {
                FlowTrace.Fail("Onboarding",
                    $"VerifyStage: INDEX OUT OF RANGE — _carouselIndex={_carouselIndex} (roster {HeroCatalog.Heroes.Length}).");
            }
        }

        // =====================================================================
        //  Entry-point CTAs — write the hero choice then route
        // =====================================================================

        /// <summary>
        /// "Enter Elarion" — the confirm CTA. Persists the chosen (playable) hero, then
        /// routes DIRECTLY to the home hub (MainCastle_Hall) via GoCastle(). A no-op when
        /// the on-screen hero is locked (the CTA is disabled there, but guard anyway).
        ///
        /// The live path is HeroSelect -> Castle — the pet-select step is gone for
        /// single-hero V1. The FeatureFlags.BypassPetSelect branch is kept ONLY as a
        /// reversibility hatch (flag OFF restores the old pet step).
        /// </summary>
        private void OnDiveVillageClicked()
        {
            if (!_hasSelection) return;   // locked hero on screen — nothing to confirm
            PersistHero();

            // Reversibility hatch: flag OFF -> old pet step. DEFAULT path is GoCastle
            // (flag default ON) — single-hero V1 never shows PetSelect.
            if (FeatureFlags.BypassPetSelect)
            {
                FlowTrace.Step("Onboarding", "OnDiveVillageClicked: single-hero V1 — GoCastle (PetSelect skipped).");
                SceneRouter.GoCastle();
                return;
            }

            SceneRouter.GoPetSelect();
        }

        /// <summary>Writes <see cref="GameState.HeroClass"/> via the service and saves.</summary>
        private void PersistHero()
        {
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                svc.ChooseHero(_selectedHero);
            }
            else
            {
                FlowTrace.Warn("Onboarding", "PersistHero: No GameStateService — the hero choice was NOT persisted. Routing onward anyway.");
            }
        }

        // =====================================================================
        //  Small helpers
        // =====================================================================

        /// <summary>True when a hero class is selectable in V1 (only the playable hero).</summary>
        private static bool IsPlayable(HeroClass hero) => hero == PlayableHero;

        /// <summary>Catalog index for a hero class, or 0 when absent (never out of range).</summary>
        private static int IndexOf(HeroClass hero)
        {
            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
                if (HeroCatalog.Heroes[i].Hero == hero) return i;
            return 0;
        }

        /// <summary>Canon-roster portrait slug for a hero class.</summary>
        private static string SlugFor(HeroClass hero) => hero switch
        {
            HeroClass.Mage   => "Thrain",
            HeroClass.Knight => "Grom",
            HeroClass.Ranger => "Sylas",
            HeroClass.Cleric => "Elara",
            _                => hero.ToString(),
        };

        /// <summary>
        /// Returns the localised string for <paramref name="key"/>, falling back to
        /// <paramref name="fallback"/> when the key is absent (so a button is never
        /// blank if <c>en.json</c> hasn't been updated yet).
        /// </summary>
        private static string FallbackLocale(string key, string fallback)
        {
            var s = CanonStrings.Locale(key);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        /// <summary>Sets all four border widths of an element.</summary>
        private static void SetBorderWidth(VisualElement el, float width)
        {
            if (el == null) return;
            el.style.borderTopWidth = width;
            el.style.borderBottomWidth = width;
            el.style.borderLeftWidth = width;
            el.style.borderRightWidth = width;
        }

        /// <summary>Sets all four border colours of an element.</summary>
        private static void SetBorderColor(VisualElement el, Color color)
        {
            if (el == null) return;
            el.style.borderTopColor = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor = color;
            el.style.borderRightColor = color;
        }

        /// <summary>Inline-styles a footer CTA (primary amber, or secondary outline).</summary>
        private static void StyleCta(Button btn, bool secondary)
        {
            if (btn == null) return;
            btn.style.flexGrow = 1f;
            btn.style.flexBasis = 0f;
            btn.style.maxWidth = 280f;
            btn.style.height = 48f;
            btn.style.fontSize = 15f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.marginLeft = 6f;
            btn.style.marginRight = 6f;
            float r = 12f;
            btn.style.borderTopLeftRadius = r;
            btn.style.borderTopRightRadius = r;
            btn.style.borderBottomLeftRadius = r;
            btn.style.borderBottomRightRadius = r;
            if (secondary)
            {
                // Neutral stone "quiet" button — gold hairline rim, parchment text.
                SetBorderWidth(btn, 2f);
                SetBorderColor(btn, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
                btn.style.backgroundColor = ElarionUi.PanelStone;
                btn.style.color = ElarionUi.Parchment;
            }
            else
            {
                // Primary gold CTA — dark ink on runic gold (town-HUD button).
                SetBorderWidth(btn, 1f);
                SetBorderColor(btn, new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.9f));
                btn.style.backgroundColor = ElarionUi.GoldButton;
                btn.style.color = ElarionUi.Ink;
            }
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the hero-select scene.
// -----------------------------------------------------------------------------
//   1. The HeroSelect scene is generated by DeNelle.Editor.IntroFlowSceneBuilder
//      (menu: Defenders/Intro Flow/Build Hero + Pet Select Scenes). It creates a
//      Camera, an EventSystem, and a UIDocument GameObject with this controller
//      attached. The UIDocument MAY still carry HeroSelectScreen.uxml — that is
//      harmless: BuildScreen() clears the root and builds the entire carousel in
//      code, so whether or not the UXML renders, the screen is identical. There is
//      no UXML element this screen depends on, so a UXML that doesn't render in the
//      player build (CLAUDE.md §8) cannot break it.
//
//   2. The intro/story cinematic (StoryIntroController) lives in the TITLE scene,
//      not here. The transition into hero-select is a single-scene LoadScene
//      (SceneRouter.GoHeroSelect), so Unity destroys the Title scene — and the
//      cold-open overlay with it — before this scene's OnEnable runs; they never
//      coexist.
//
//   3. GameStateService must exist before this screen so ChooseHero persists. It is
//      a DontDestroyOnLoad singleton from the Core bootstrap in Title. If a session
//      enters HeroSelect cold, the controller logs a warning and still routes on
//      (the choice just is not saved).
//
//   4. CAROUSEL (WO-559): all four heroes are navigable for preview; only the
//      playable hero (Grom == Knight, FeatureFlags.KnightOnly) is selectable. To
//      unlock more heroes later, widen IsPlayable(HeroClass) — no layout change
//      needed; the locked scrim/CTA state derives from it.
// =============================================================================
