// =============================================================================
// HeroSelectController — drives the hero-select screen (intro flow).
// -----------------------------------------------------------------------------
// THE SCREEN (owner acceptance, DEF — "stop the hero-select regressing"):
//   TOP        : a light brand block (title + subtitle). The old heart-wing
//                "dragon" banner was REMOVED in a polish pass (owner: the screen
//                felt too heavy and the dragon read as disconnected) — the roster
//                now fills the whole screen so the layout breathes.
//   MIDDLE     : FOUR hero panels, evenly distributed across the full width,
//                responsive in BOTH landscape and portrait (re-flows on
//                orientation / screen-size change — never fixed pixel columns).
//   BELOW THEM : the selected hero's PLAY-CARD details (name / role / blurb /
//                HP / Attack / Speed pips / signature ability / CTA) render
//                BELOW the four panels.
//
// WHY THIS KEPT REGRESSING (root cause, fixed here):
//   The old screen BOUND to named elements inside HeroSelectScreen.uxml
//   (_root.Q<VisualElement>("hero-dragon-stage") …). Per CLAUDE.md §8 / PIPELINE
//   §8, UXML + USS do NOT render reliably in the WebGL player build — when the
//   UXML tree failed to instantiate, every Q<>() returned null, every inline
//   styling pass silently no-op'd on a null guard, and the screen came up empty
//   or half-built. Each past "fix" layered more inline styles ON TOP of the UXML
//   elements but still DEPENDED on those elements existing — so it broke again
//   the next time UXML didn't resolve. Additionally there were no 4-even panels
//   (fixed 240px cards overflowed in portrait) and no selected-hero detail card.
//
//   THE FIX: this controller now CLEARS the root and BUILDS THE ENTIRE LAYOUT IN
//   CODE. It does not read a single name out of the UXML. A UXML edit (or a UXML
//   that doesn't render at all) can no longer break this screen — there is
//   nothing for it to break. The whole tree is code, sized with flex / percent
//   so it re-centres in both orientations, and a GeometryChangedEvent handler
//   re-flows the columns whenever the screen size / orientation changes. A
//   post-build self-assert verifies the four panels exist and are evenly spread.
//
// COPY: every hero name / role / blurb is resolved from en.json via CanonStrings
// at runtime (port-spec Part 4). The card accent colour + glyph come from
// HeroCatalog (pure presentation data, legitimately in C#).
//
// PERSISTENCE: on confirm, GameStateService.ChooseHero(hero) writes
// GameState.HeroClass and Save()s it, then routes onward.
//
// RETURNING-PLAYER SKIP: a save that already records BOTH a hero AND a starter
// pet has finished the intro flow — the screen self-skips straight to the
// Village so a returning player is not re-asked.
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
    /// Drives the hero-select screen: builds (in code) the dragon banner, the
    /// four evenly-distributed hero panels, and the selected-hero play-card
    /// detail block below them; tracks the player's pick; and on confirm writes
    /// <see cref="GameState.HeroClass"/> and routes onward. Fully responsive —
    /// re-flows on orientation change. A returning player who has finished the
    /// intro is skipped straight to the Village.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HeroSelectController : MonoBehaviour
    {
        // -- en.json keys for the screen's own copy --------------------------
        private const string TitleKey    = "heroSelect.title";
        private const string SubtitleKey = "heroSelect.subtitle";
        // "Dive into Village" — the sole confirm CTA, routes to pet-select/village.
        private const string DiveKey    = "heroSelect.diveVillage";
        // WO-327: ConfirmKey ("heroSelect.jumpAction" / "Jump into the Action") was
        // removed with the DTT-crashing button.

        // -- Palette — SOURCED from the shared ElarionUi town-HUD language ----
        // (WO restyle: matches the town HUD — dark glass + ornate gold-rune
        // frames — not the retired hardcoded purple/amber. Role-named locals keep
        // the layout below unchanged while every colour resolves canonically.)
        private static readonly Color ColBackground   = ElarionUi.PanelStoneDark;                                     // full-screen stone
        private static readonly Color ColPanelDark     = ElarionUi.PanelStone;                                         // roster panel fill
        private static readonly Color ColCardIdle      = ElarionUi.PanelStoneDark;                                     // card rest fill
        private static readonly Color ColCardActive    = ElarionUi.PanelStone;                                         // card selected fill
        private static readonly Color ColAmber         = ElarionUi.Gold;                                               // accents / eyebrow → runic gold
        private static readonly Color ColVioletBorder  = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f); // soft gold rim
        private static readonly Color ColTextBright    = ElarionUi.Parchment;                                          // primary text
        private static readonly Color ColTextMuted     = ElarionUi.ParchmentDim;                                       // secondary text

        [Header("UI")]
        [Tooltip("UIDocument hosting the hero-select panel. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Behaviour")]
        [Tooltip("Skip straight to the Village when the save already records a hero AND a " +
                 "starter pet (a returning player who finished the intro). Editor testing: " +
                 "disable to always show the screen.")]
        [SerializeField] private bool _skipWhenIntroComplete = true;

        // -- Built UI elements (all created in code) -------------------------
        private VisualElement _root;
        private VisualElement _cardRow;       // bottom — 4 hero panels
        private VisualElement _detailCard;    // below panels — selected-hero details
        private Label _detailName;
        private Label _detailRole;
        private Label _detailBlurb;
        // WO polish: the detail-card stat block — HP / Attack / Speed pip rows +
        // signature ability (name + one-line effect). Data lives in HeroCardInfo
        // (WO-329); these labels just render it.
        private Label _detailHp;
        private Label _detailAttack;
        private Label _detailSpeed;
        private Label _detailAbilityName;
        private Label _detailAbilityDesc;
        // WO-327: the "Jump into the Action" CTA (DTT/PatriciaLight quick-entry)
        // was REMOVED — it launched Defend the Tower in a state that crashed the
        // player build. Only the single "Dive into Village" confirm remains.
        private Button _diveButton;           // "Dive into Village"    — normal village

        // One card VisualElement per hero, in HeroCatalog order.
        private readonly VisualElement[] _cards = new VisualElement[HeroCatalog.Heroes.Length];
        // The gold image-frame of each card (parallel to _cards) so selection can
        // brighten the frame rim to gilt (WO-374 selection state).
        private readonly VisualElement[] _cardFrames = new VisualElement[HeroCatalog.Heroes.Length];

        private bool _built;
        private bool _hasSelection;
        private HeroClass _selectedHero;

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
            if (_diveButton    != null) _diveButton.clicked    -= OnDiveVillageClicked;
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _built = false;
        }

        // =====================================================================
        //  Returning-player gate
        // =====================================================================

        /// <summary>
        /// True when the save already records BOTH a chosen hero and a starter
        /// pet — the intro flow is finished and this screen has nothing to ask.
        /// </summary>
        private static bool IsIntroComplete()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false; // first launch
            return svc.State.HeroClass != HeroClassOpt.None
                   && !string.IsNullOrEmpty(svc.State.StarterPetId);
        }

        // =====================================================================
        //  Code-built layout — NO UXML dependency (regression-proof)
        // =====================================================================

        /// <summary>
        /// Builds the entire hero-select layout in code on a cleared root. Does
        /// not read any UXML element by name, so a UXML that fails to render in
        /// the build (CLAUDE.md §8) cannot break this screen. Structure:
        ///   root (column)
        ///     └─ roster panel  (full screen, opaque)
        ///          ├─ brand     (title + subtitle)
        ///          ├─ eyebrow
        ///          ├─ card row  (4 hero panels, flex-even, re-flows on resize)
        ///          ├─ detail card (selected-hero name / role / blurb + stats)
        ///          └─ footer    (confirm CTA)
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

            // Dragon banner REMOVED (owner polish: the screen felt too heavy and
            // the dragon read as disconnected). The roster panel now fills the
            // whole screen and carries the brand title/subtitle itself, so the
            // layout breathes lighter.
            BuildRosterPanel();   // full screen (brand + eyebrow + cards + detail + CTAs)

            // Initial selection state + a re-flow pass once we know the size.
            PreselectFromSave();
            RefreshSelectionVisuals();
            RefreshConfirmEnabled();

            // Re-flow the responsive layout on every screen-size / orientation
            // change. This is what keeps the four panels evenly centred in BOTH
            // landscape and portrait without fixed pixel positions.
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            _built = true;

            // Run the orientation re-flow + self-assert once now in case the
            // panel already has its geometry (so we never wait a frame to verify).
            ReflowForSize(_root.resolvedStyle.width, _root.resolvedStyle.height);

            // V — prove the screen built: root has children AND the four cards exist.
            // A built-but-empty hero-select is a dead-end screen, so a zero count Fails.
            int rootChildren = _root.childCount;
            int cardCount = _cardRow != null ? _cardRow.childCount : 0;
            if (rootChildren == 0 || cardCount == 0)
            {
                FlowTrace.Fail("Onboarding",
                    $"BuildScreen VERIFY FAILED — rootChildren={rootChildren} cards={cardCount}. " +
                    "Hero-select built but EMPTY (blank/dead-end screen).");
            }
            else
            {
                FlowTrace.Step("Onboarding",
                    $"BuildScreen VERIFY ok — rootChildren={rootChildren} cards={cardCount}.");
            }
        }

        // ── Full screen: roster panel ───────────────────────────────────────

        private void BuildRosterPanel()
        {
            var roster = new VisualElement { name = "hero-roster-panel" };
            roster.style.flexGrow = 1f;          // takes the full screen now
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

            // Brand block — relocated out of the (removed) dragon banner so the
            // screen still announces itself, now lighter at the top of the roster.
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

            // Eyebrow.
            var eyebrow = new Label("— CHOOSE YOUR HERO —");
            eyebrow.style.fontSize = 12f;
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            eyebrow.style.color = ColAmber;
            eyebrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            eyebrow.style.marginBottom = 8f;
            eyebrow.style.flexShrink = 0f;
            roster.Add(eyebrow);

            // The four hero panels — an evenly distributed flex row.
            _cardRow = new VisualElement { name = "hero-card-row" };
            _cardRow.style.flexDirection = FlexDirection.Row;
            _cardRow.style.justifyContent = Justify.SpaceBetween; // even across full width
            _cardRow.style.alignItems = Align.Stretch;
            _cardRow.style.flexGrow = 1f;
            _cardRow.style.flexShrink = 1f;
            _cardRow.style.alignSelf = Align.Stretch;
            roster.Add(_cardRow);

            BuildCards();

            // Selected-hero play-card details — BELOW the four panels (acceptance #3).
            BuildDetailCard();
            roster.Add(_detailCard);

            // Footer — two CTAs side by side.
            var footer = new VisualElement { name = "select-footer" };
            footer.style.marginTop = 12f;
            footer.style.flexShrink = 0f;
            footer.style.alignSelf = Align.Stretch;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.Center;
            footer.style.alignItems = Align.Center;

            // Sole CTA — the normal first-run path. "Dive into Village" persists
            // the hero then routes to pet-select. (Styled primary/amber.)
            //
            // WO-327: the old "Jump into the Action" CTA was REMOVED entirely. It
            // was the Defend-the-Tower (PatriciaLight) quick-entry that launched
            // DTT in a state that crashed the player build, so the button, its
            // styling/registration, and its onClick handler are all gone — leaving
            // a single safe confirm in both editor and player builds.
            _diveButton = new Button { text = FallbackLocale(DiveKey, "Dive into Village") };
            StyleCta(_diveButton, secondary: false);
            _diveButton.clicked += OnDiveVillageClicked;
            footer.Add(_diveButton);

            roster.Add(footer);
            _root.Add(roster);
        }

        /// <summary>Builds the four hero panels into the card row.</summary>
        private void BuildCards()
        {
            if (_cardRow == null) return;
            _cardRow.Clear();

            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
            {
                HeroCardInfo info = HeroCatalog.Heroes[i];
                VisualElement card = BuildCard(info);
                _cardRow.Add(card);
                _cards[i] = card;
                _cardFrames[i] = card.Q<VisualElement>("image-frame");
            }
        }

        /// <summary>
        /// Builds one hero panel (WO-374 card framing): a gold-framed portrait
        /// IMAGE sitting above a clearly-separated DATA strip (hero name + class),
        /// with no overlap between the two. Sized with flex (NOT a fixed pixel
        /// width) so the four panels share the row evenly and re-flow in both
        /// orientations.
        ///
        /// Structure:
        ///   card (column, padded)
        ///     ├─ image-frame   (gold double-rim, dark backing, fixed 3:4 aspect)
        ///     │    └─ portrait  (cover-fit art, or accent glyph fallback)
        ///     ├─ accent strip   (element colour)
        ///     └─ data strip     (name in gold, class line in cream — BELOW image)
        /// </summary>
        private VisualElement BuildCard(HeroCardInfo info)
        {
            var card = new VisualElement { name = $"hero-card-{info.Hero}" };

            // Even distribution: each panel flex-grows equally, with a tiny gutter.
            // No hardcoded width / x position — this is what makes it orientation-robust.
            card.style.flexGrow = 1f;
            card.style.flexShrink = 1f;
            card.style.flexBasis = 0f;
            card.style.marginLeft = 5f;
            card.style.marginRight = 5f;
            card.style.maxWidth = Length.Percent(25f);    // never wider than its even quarter
            card.style.minWidth = 0f;

            float radius = 12f;
            card.style.borderTopLeftRadius = radius;
            card.style.borderTopRightRadius = radius;
            card.style.borderBottomLeftRadius = radius;
            card.style.borderBottomRightRadius = radius;
            SetBorderWidth(card, 2f);
            SetBorderColor(card, ColVioletBorder);
            card.style.backgroundColor = ColCardIdle;
            card.style.overflow = Overflow.Hidden;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Stretch;
            card.style.justifyContent = Justify.FlexStart;
            // Breathing room inside the card so the framed image isn't flush to
            // the violet card border (WO-374 #3: cards were cramped).
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.paddingLeft = 8f;
            card.style.paddingRight = 8f;

            // ── Image frame (WO-374 #1): a gold double-rim around a dark backing
            //    holds the portrait at a fixed portrait aspect, clearly set off
            //    from the data strip below. ScaleAndCrop "covers" the frame so the
            //    art is never stretched/squashed; overflow-hidden + rounding keep
            //    it neatly inside the gilt rim.
            var imageFrame = new VisualElement { name = "image-frame" };
            imageFrame.style.flexGrow = 1f;
            imageFrame.style.flexShrink = 1f;
            imageFrame.style.minHeight = 70f;
            imageFrame.style.overflow = Overflow.Hidden;
            imageFrame.style.alignItems = Align.Center;
            imageFrame.style.justifyContent = Justify.Center;
            imageFrame.style.backgroundColor = new Color(0f, 0f, 0f, 0.30f); // dark backing
            float frameRadius = 8f;
            imageFrame.style.borderTopLeftRadius = frameRadius;
            imageFrame.style.borderTopRightRadius = frameRadius;
            imageFrame.style.borderBottomLeftRadius = frameRadius;
            imageFrame.style.borderBottomRightRadius = frameRadius;
            SetBorderWidth(imageFrame, 2f);
            SetBorderColor(imageFrame, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f));

            // Portrait art fills the frame (cover-fit). Portraits use the owner's
            // character-named art (canon roster): Thrain=Mage, Grom=Knight,
            // Sylas=Ranger, Elara=Cleric.
            var portrait = new VisualElement { name = "portrait" };
            portrait.style.position = Position.Absolute;
            portrait.style.left = 0f;
            portrait.style.right = 0f;
            portrait.style.top = 0f;
            portrait.style.bottom = 0f;
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;

            string slug = SlugFor(info.Hero);
            var portraitSprite = Resources.Load<Sprite>($"HeroPortraits/{slug}");
            if (portraitSprite != null)
            {
                portrait.style.backgroundImage = new StyleBackground(portraitSprite);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }
            else
            {
                var portraitTex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
                if (portraitTex != null)
                {
                    portrait.style.backgroundImage = new StyleBackground(portraitTex);
                    portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                }
                else
                {
                    var glyph = new Label(info.Glyph);
                    glyph.style.fontSize = 48f;
                    glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
                    glyph.style.color = info.Accent;
                    portrait.Add(glyph);
                }
            }
            imageFrame.Add(portrait);
            card.Add(imageFrame);

            // Element-coloured accent strip — a clean seam between image and data.
            var accent = new VisualElement();
            accent.style.height = 3f;
            accent.style.flexShrink = 0f;
            accent.style.marginTop = 8f;
            accent.style.backgroundColor = info.Accent;
            float accentRadius = 2f;
            accent.style.borderTopLeftRadius = accentRadius;
            accent.style.borderTopRightRadius = accentRadius;
            accent.style.borderBottomLeftRadius = accentRadius;
            accent.style.borderBottomRightRadius = accentRadius;
            card.Add(accent);

            // ── Data strip (WO-374 #2): name + class BELOW the framed image, never
            //    overlapping it. Name in runic gold, class line in muted cream.
            var dataStrip = new VisualElement { name = "card-data" };
            dataStrip.style.flexShrink = 0f;
            dataStrip.style.paddingTop = 6f;
            dataStrip.style.paddingBottom = 4f;
            dataStrip.style.paddingLeft = 2f;
            dataStrip.style.paddingRight = 2f;
            dataStrip.style.alignItems = Align.Center;

            var nameLabel = new Label(CanonStrings.Locale(info.NameKey));
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.fontSize = 15f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = ElarionUi.Gold;        // gold name (WO-374 typography)
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            nameLabel.style.flexShrink = 0f;
            dataStrip.Add(nameLabel);

            var classLabel = new Label(CanonStrings.Locale(info.RoleKey));
            classLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            classLabel.style.marginTop = 1f;
            classLabel.style.fontSize = 11f;
            classLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            classLabel.style.color = ElarionUi.ParchmentDim; // cream class line
            classLabel.style.whiteSpace = WhiteSpace.Normal;
            classLabel.style.flexShrink = 0f;
            dataStrip.Add(classLabel);

            card.Add(dataStrip);

            // Whole panel is the hit target.
            HeroClass captured = info.Hero;
            card.RegisterCallback<PointerDownEvent>(_ => OnCardClicked(captured));

            return card;
        }

        /// <summary>
        /// Builds the selected-hero detail card that sits BELOW the four panels
        /// (acceptance #3). Populated by <see cref="RefreshDetailCard"/>.
        /// </summary>
        private void BuildDetailCard()
        {
            _detailCard = new VisualElement { name = "hero-detail-card" };
            _detailCard.style.marginTop = 12f;
            _detailCard.style.flexShrink = 0f;
            _detailCard.style.alignSelf = Align.Stretch;
            _detailCard.style.paddingTop = 12f;
            _detailCard.style.paddingBottom = 12f;
            _detailCard.style.paddingLeft = 16f;
            _detailCard.style.paddingRight = 16f;
            float r = 12f;
            _detailCard.style.borderTopLeftRadius = r;
            _detailCard.style.borderTopRightRadius = r;
            _detailCard.style.borderBottomLeftRadius = r;
            _detailCard.style.borderBottomRightRadius = r;
            SetBorderWidth(_detailCard, 1f);
            SetBorderColor(_detailCard, ColVioletBorder);
            _detailCard.style.backgroundColor = ElarionUi.PanelStone;

            _detailName = new Label(string.Empty);
            _detailName.style.fontSize = 20f;
            _detailName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailName.style.color = ColTextBright;
            _detailName.style.whiteSpace = WhiteSpace.Normal;
            _detailCard.Add(_detailName);

            _detailRole = new Label(string.Empty);
            _detailRole.style.marginTop = 2f;
            _detailRole.style.fontSize = 12f;
            _detailRole.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailRole.style.color = ColAmber;
            _detailRole.style.whiteSpace = WhiteSpace.Normal;
            _detailCard.Add(_detailRole);

            _detailBlurb = new Label(string.Empty);
            _detailBlurb.style.marginTop = 6f;
            _detailBlurb.style.fontSize = 13f;
            _detailBlurb.style.color = ColTextMuted;
            _detailBlurb.style.whiteSpace = WhiteSpace.Normal;
            _detailCard.Add(_detailBlurb);

            // ── Stat block: HP / Attack / Speed as 1-5 pip rows, then the
            //    signature ability (name + one-line effect). All data already
            //    lives in HeroCardInfo (WO-329) — this is display only.
            var statBlock = new VisualElement { name = "hero-detail-stats" };
            statBlock.style.marginTop = 10f;
            statBlock.style.flexShrink = 0f;
            statBlock.style.flexDirection = FlexDirection.Column;
            statBlock.style.alignItems = Align.Stretch;

            _detailHp     = BuildStatRow(statBlock, "HP");
            _detailAttack = BuildStatRow(statBlock, "ATTACK");
            _detailSpeed  = BuildStatRow(statBlock, "SPEED");

            _detailCard.Add(statBlock);

            // Signature ability — name in runic gold, effect in muted cream.
            _detailAbilityName = new Label(string.Empty);
            _detailAbilityName.style.marginTop = 10f;
            _detailAbilityName.style.fontSize = 14f;
            _detailAbilityName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailAbilityName.style.color = ColAmber;
            _detailAbilityName.style.whiteSpace = WhiteSpace.Normal;
            _detailCard.Add(_detailAbilityName);

            _detailAbilityDesc = new Label(string.Empty);
            _detailAbilityDesc.style.marginTop = 2f;
            _detailAbilityDesc.style.fontSize = 12f;
            _detailAbilityDesc.style.color = ColTextMuted;
            _detailAbilityDesc.style.whiteSpace = WhiteSpace.Normal;
            _detailCard.Add(_detailAbilityDesc);
        }

        /// <summary>
        /// Builds one stat row — a fixed-width gold label on the left and a value
        /// label on the right that <see cref="RefreshDetailCard"/> fills with a
        /// 1-5 pip string. Returns the value label so it can be updated per-hero.
        /// </summary>
        private Label BuildStatRow(VisualElement parent, string label)
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

            var value = new Label(string.Empty);
            value.style.fontSize = 14f;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.color = ColTextBright;
            value.style.letterSpacing = 2f;
            row.Add(value);

            parent.Add(row);
            return value;
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

        // =====================================================================
        //  Responsive re-flow + self-assert
        // =====================================================================

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ReflowForSize(evt.newRect.width, evt.newRect.height);
        }

        /// <summary>
        /// Re-flows the layout for the current screen size / orientation. The four
        /// panels always share the row evenly (SpaceBetween + equal flex-grow). In
        /// a tall portrait the panels get a little extra breathing room so nothing
        /// crowds. No fixed x positions — purely flex — so it re-centres
        /// identically in landscape and portrait.
        /// </summary>
        private void ReflowForSize(float width, float height)
        {
            if (!_built || _cardRow == null) return;
            if (width <= 0f || height <= 0f) return;

            bool portrait = height >= width;

            // Tighten the inter-panel gutter in portrait so four narrow columns
            // still fit comfortably; widen it a touch in landscape.
            float gutter = portrait ? 4f : 6f;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].style.marginLeft = gutter;
                _cards[i].style.marginRight = gutter;
            }

            VerifyFourPanelsEven();
        }

        /// <summary>
        /// Regression guard: asserts the four hero panels exist and are laid out
        /// as an even row (equal flex-grow, no fixed widths). If a future edit
        /// breaks the contract this logs loudly so it is caught immediately
        /// rather than shipping a broken screen again.
        /// </summary>
        private void VerifyFourPanelsEven()
        {
            int expected = HeroCatalog.Heroes.Length;
            int actual = _cardRow != null ? _cardRow.childCount : 0;
            if (actual != expected)
            {
                // Wrong card count = a broken/empty roster — roll up as a hard failure.
                FlowTrace.Fail("Onboarding",
                    $"VerifyFourPanelsEven: PANEL ASSERT FAILED — expected {expected} hero panels, found {actual}. " +
                    "The hero row is not evenly built.");
                return;
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null)
                {
                    FlowTrace.Fail("Onboarding", $"VerifyFourPanelsEven: PANEL ASSERT FAILED — hero panel {i} is null.");
                    return;
                }
                // Even distribution contract: every panel must flex-grow equally
                // and must NOT carry a fixed pixel width (which would break the
                // responsive re-flow in the other orientation). A no-flex panel still
                // renders (visible, just uneven) — Warn rather than Fail.
                if (_cards[i].resolvedStyle.flexGrow <= 0f)
                {
                    FlowTrace.Warn("Onboarding",
                        $"VerifyFourPanelsEven: hero panel {i} has no flex-grow; the row will not distribute evenly.");
                    return;
                }
            }
        }

        // =====================================================================
        //  Selection
        // =====================================================================

        /// <summary>A hero panel was tapped — mark it active and refresh the detail card.</summary>
        private void OnCardClicked(HeroClass hero)
        {
            _selectedHero = hero;
            _hasSelection = true;
            // SOURCE FIX: persist the pick THE INSTANT it is selected so GameState.HeroClass
            // is written + saved + KnightOnly-enforced even when a bypass route (e.g.
            // FeatureFlags.BypassPetSelect → SceneRouter.GoCastle) skips the confirm/PersistHero
            // path. The confirm path (OnDiveVillageClicked → PersistHero) still calls ChooseHero;
            // ChooseHero is idempotent so the double-call is harmless.
            GameStateService.Instance?.ChooseHero(_selectedHero);
            RefreshSelectionVisuals();
            RefreshConfirmEnabled();
        }

        /// <summary>
        /// Pre-selects the hero the save already records, if any — covers a player
        /// who chose a hero on a prior session but had not yet picked a pet.
        /// </summary>
        private void PreselectFromSave()
        {
            var svc = GameStateService.Instance;
            HeroClass? saved = svc != null && svc.State != null
                ? svc.State.HeroClass.ToNullable()
                : null;
            if (saved.HasValue)
            {
                _selectedHero = saved.Value;
                _hasSelection = true;
            }
        }

        /// <summary>Marks the active panel, clears the rest, and refreshes the detail card.</summary>
        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                bool active = _hasSelection && HeroCatalog.Heroes[i].Hero == _selectedHero;
                SetBorderColor(_cards[i], active ? ColAmber : ColVioletBorder);
                _cards[i].style.backgroundColor = active ? ColCardActive : ColCardIdle;

                // Brighten the framed-image rim to gilt on the selected card so the
                // chosen hero stands out (WO-374 selection state).
                if (_cardFrames[i] != null)
                {
                    Color rim = active
                        ? ElarionUi.Gilt
                        : new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
                    SetBorderColor(_cardFrames[i], rim);
                    SetBorderWidth(_cardFrames[i], active ? 3f : 2f);
                }
            }
            RefreshDetailCard();
        }

        /// <summary>Fills the below-panels detail card with the selected hero's copy.</summary>
        private void RefreshDetailCard()
        {
            if (_detailCard == null) return;

            if (!_hasSelection)
            {
                if (_detailName  != null) _detailName.text  = "Tap a hero to see their story";
                if (_detailRole  != null) _detailRole.text  = string.Empty;
                if (_detailBlurb != null) _detailBlurb.text = string.Empty;
                if (_detailHp     != null) _detailHp.text     = string.Empty;
                if (_detailAttack != null) _detailAttack.text = string.Empty;
                if (_detailSpeed  != null) _detailSpeed.text  = string.Empty;
                if (_detailAbilityName != null) _detailAbilityName.text = string.Empty;
                if (_detailAbilityDesc != null) _detailAbilityDesc.text = string.Empty;
                return;
            }

            HeroCardInfo info = FindInfo(_selectedHero);
            if (info == null) return;
            if (_detailName  != null) _detailName.text  = CanonStrings.Locale(info.NameKey);
            if (_detailRole  != null) _detailRole.text  = CanonStrings.Locale(info.RoleKey);
            if (_detailBlurb != null) _detailBlurb.text = CanonStrings.Locale(info.BlurbKey);

            // Stats (1-5 pips) + signature ability — straight from HeroCardInfo (WO-329).
            if (_detailHp     != null) _detailHp.text     = PipString(info.Hp);
            if (_detailAttack != null) _detailAttack.text = PipString(info.Attack);
            if (_detailSpeed  != null) _detailSpeed.text  = PipString(info.Speed);
            if (_detailAbilityName != null) _detailAbilityName.text = info.AbilityName;
            if (_detailAbilityDesc != null) _detailAbilityDesc.text = info.AbilityDesc;
        }

        /// <summary>Enables the confirm CTA only once a hero is chosen.</summary>
        private void RefreshConfirmEnabled()
        {
            if (_diveButton != null) _diveButton.SetEnabled(_hasSelection);
        }

        // =====================================================================
        //  Entry-point CTAs — write the hero choice then route
        // =====================================================================

        // WO-327: OnConfirmClicked() — the "Jump into the Action" handler that
        // routed into Defend the Tower — was REMOVED with its button.

        /// <summary>
        /// "Dive into Village" — the canonical first-run path. Persists the chosen
        /// hero, then routes to the PET-SELECT screen so the player bonds a starter
        /// Warden before entering the village.
        /// </summary>
        private void OnDiveVillageClicked()
        {
            if (!_hasSelection) return;
            PersistHero();

            // WO-473 / single-hero V1: skip the PetSelect screen — straight to the castle.
            // Hero pick is persisted above; PetSelect persists nothing (Echo Hollow owns pet bonding).
            if (FeatureFlags.BypassPetSelect)
            {
                FlowTrace.Step("Onboarding", "OnDiveVillageClicked: BypassPetSelect ON — GoCastle (PetSelect skipped).");
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

        /// <summary>Canon-roster portrait slug for a hero class.</summary>
        private static string SlugFor(HeroClass hero) => hero switch
        {
            HeroClass.Mage   => "Thrain",
            HeroClass.Knight => "Grom",
            HeroClass.Ranger => "Sylas",
            HeroClass.Cleric => "Elara",
            _                => hero.ToString(),
        };

        /// <summary>Catalog entry for a hero class, or null if absent.</summary>
        private static HeroCardInfo FindInfo(HeroClass hero)
        {
            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
                if (HeroCatalog.Heroes[i].Hero == hero) return HeroCatalog.Heroes[i];
            return null;
        }

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
//      now harmless: BuildScreen() clears the root and builds the entire layout
//      in code, so whether or not the UXML renders, the screen is identical. This
//      is the regression fix: there is no longer any UXML element this screen
//      depends on, so a UXML that doesn't render in the WebGL build (CLAUDE.md §8)
//      cannot break it.
//
//   2. The intro/story cinematic (StoryIntroController) lives in the TITLE scene,
//      not here. The transition into hero-select is a single-scene
//      SceneManager.LoadScene (SceneRouter.GoHeroSelect), so Unity destroys the
//      Title scene — and the cold-open overlay with it — before this scene's
//      OnEnable runs. There is therefore zero possibility of the intro rendering
//      on top of hero-select: they never coexist in the same scene. (Title-side,
//      StoryIntroController.HideImmediate already fully tears its overlay down.)
//
//   3. GameStateService must exist before this screen so ChooseHero persists.
//      It is a DontDestroyOnLoad singleton from the Core bootstrap in Title. If a
//      session enters HeroSelect cold, the controller logs a warning and still
//      routes on (the choice just is not saved).
// =============================================================================
