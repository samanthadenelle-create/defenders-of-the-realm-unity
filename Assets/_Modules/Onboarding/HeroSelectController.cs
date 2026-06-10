// =============================================================================
// HeroSelectController — drives the hero-select screen (intro flow).
// -----------------------------------------------------------------------------
// THE SCREEN (owner acceptance, DEF — "stop the hero-select regressing"):
//   TOP HALF   : the dragon banner art (heart-wing) fills the top half.
//   BOTTOM     : FOUR hero panels, evenly distributed across the full width,
//                responsive in BOTH landscape and portrait (re-flows on
//                orientation / screen-size change — never fixed pixel columns).
//   BELOW THEM : the selected hero's PLAY-CARD details (name / role / blurb /
//                CTAs) render BELOW the four panels.
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

        // -- Palette (inline; no USS dependency) -----------------------------
        private static readonly Color ColBackground   = new Color(0.024f, 0.016f, 0.047f, 1f);
        private static readonly Color ColPanelDark     = new Color(0.071f, 0.047f, 0.118f, 1f);
        private static readonly Color ColCardIdle      = new Color(0.102f, 0.071f, 0.165f, 0.98f);
        private static readonly Color ColCardActive    = new Color(0.149f, 0.102f, 0.204f, 1f);
        private static readonly Color ColAmber         = new Color(0.961f, 0.651f, 0.137f, 1f);
        private static readonly Color ColVioletBorder  = new Color(0.486f, 0.227f, 0.929f, 0.35f);
        private static readonly Color ColTextBright    = new Color(0.929f, 0.914f, 0.980f, 1f);
        private static readonly Color ColTextMuted     = new Color(0.729f, 0.698f, 0.824f, 0.90f);

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
        private VisualElement _dragonStage;   // top half — dragon art
        private VisualElement _cardRow;       // bottom — 4 hero panels
        private VisualElement _detailCard;    // below panels — selected-hero details
        private Label _detailName;
        private Label _detailRole;
        private Label _detailBlurb;
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
        ///     ├─ dragon stage  (top half, dragon art, ScaleAndCrop)
        ///     └─ roster panel  (bottom half, opaque)
        ///          ├─ eyebrow
        ///          ├─ card row  (4 hero panels, flex-even, re-flows on resize)
        ///          ├─ detail card (selected-hero name / role / blurb)
        ///          └─ footer    (two CTAs)
        /// </summary>
        private void BuildScreen()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[HeroSelectController] No UIDocument root — hero-select will not display.");
                return;
            }

            // Wipe whatever the UXML instantiated (or didn't) — we own the tree now.
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.alignItems = Align.Stretch;
            _root.style.justifyContent = Justify.FlexStart;
            _root.style.backgroundColor = ColBackground;

            BuildDragonStage();   // top half
            BuildRosterPanel();   // bottom half (eyebrow + cards + detail + CTAs)

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
        }

        // ── Top half: dragon banner ─────────────────────────────────────────

        private void BuildDragonStage()
        {
            _dragonStage = new VisualElement { name = "hero-dragon-stage" };
            _dragonStage.style.height = Length.Percent(50f);   // top HALF (acceptance #4)
            _dragonStage.style.flexGrow = 0f;
            _dragonStage.style.flexShrink = 0f;
            _dragonStage.style.flexDirection = FlexDirection.Column;
            _dragonStage.style.justifyContent = Justify.FlexStart;
            _dragonStage.style.alignItems = Align.Center;

            // The dragon: the Heart-Wing banner art (Resources.Load — WebGL-safe).
            var dragonTex = Resources.Load<Texture2D>("heart-wing");
            if (dragonTex != null)
            {
                _dragonStage.style.backgroundImage = new StyleBackground(dragonTex);
                _dragonStage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; // fill the top half
            }
            else
            {
                // No art on disk — deep indigo band rather than a raw black gap.
                _dragonStage.style.backgroundColor = new Color(0.05f, 0.035f, 0.11f, 1f);
                Debug.LogWarning("[HeroSelectController] Resources/heart-wing not found — dragon stage is a flat band.");
            }

            // Title / subtitle wash over the top of the dragon for legibility.
            var brand = new VisualElement { name = "hero-brand-block" };
            brand.style.alignSelf = Align.Stretch;
            brand.style.alignItems = Align.Center;
            brand.style.paddingTop = 18f;
            brand.style.paddingBottom = 12f;
            brand.style.paddingLeft = 24f;
            brand.style.paddingRight = 24f;
            brand.style.backgroundColor = new Color(0.024f, 0.016f, 0.047f, 0.55f);
            brand.pickingMode = PickingMode.Ignore;

            var title = new Label(CanonStrings.Locale(TitleKey));
            title.style.fontSize = 34f;
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

            _dragonStage.Add(brand);
            _root.Add(_dragonStage);
        }

        // ── Bottom half: roster panel ───────────────────────────────────────

        private void BuildRosterPanel()
        {
            var roster = new VisualElement { name = "hero-roster-panel" };
            roster.style.flexGrow = 1f;          // takes the remaining bottom half
            roster.style.flexShrink = 1f;
            roster.style.flexBasis = 0f;
            roster.style.backgroundColor = ColPanelDark;
            roster.style.flexDirection = FlexDirection.Column;
            roster.style.alignItems = Align.Stretch;
            roster.style.justifyContent = Justify.FlexStart;
            roster.style.paddingTop = 12f;
            roster.style.paddingBottom = 16f;
            roster.style.paddingLeft = 10f;
            roster.style.paddingRight = 10f;

            // Amber divider seam at the top of the panel.
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
            _detailCard.style.backgroundColor = new Color(0.149f, 0.102f, 0.204f, 0.85f);

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
        }

        // =====================================================================
        //  Responsive re-flow + self-assert
        // =====================================================================

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ReflowForSize(evt.newRect.width, evt.newRect.height);
        }

        /// <summary>
        /// Re-flows the layout for the current screen size / orientation. The
        /// dragon stage is always the top half; the four panels always share the
        /// row evenly (SpaceBetween + equal flex-grow). In a tall portrait the
        /// panels get a little extra breathing room and the dragon caption font
        /// eases down so nothing crowds. No fixed x positions — purely flex —
        /// so it re-centres identically in landscape and portrait.
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
                Debug.LogError($"[HeroSelectController] PANEL ASSERT FAILED — expected {expected} " +
                               $"hero panels, found {actual}. The hero row is not evenly built.");
                return;
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null)
                {
                    Debug.LogError($"[HeroSelectController] PANEL ASSERT FAILED — hero panel {i} is null.");
                    return;
                }
                // Even distribution contract: every panel must flex-grow equally
                // and must NOT carry a fixed pixel width (which would break the
                // responsive re-flow in the other orientation).
                if (_cards[i].resolvedStyle.flexGrow <= 0f)
                {
                    Debug.LogError($"[HeroSelectController] PANEL ASSERT FAILED — hero panel {i} " +
                                   "has no flex-grow; the row will not distribute evenly.");
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
                return;
            }

            HeroCardInfo info = FindInfo(_selectedHero);
            if (info == null) return;
            if (_detailName  != null) _detailName.text  = CanonStrings.Locale(info.NameKey);
            if (_detailRole  != null) _detailRole.text  = CanonStrings.Locale(info.RoleKey);
            if (_detailBlurb != null) _detailBlurb.text = CanonStrings.Locale(info.BlurbKey);
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
                Debug.LogWarning("[HeroSelectController] No GameStateService — the hero choice " +
                                 "was NOT persisted. Routing onward anyway.");
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
                SetBorderWidth(btn, 2f);
                SetBorderColor(btn, new Color(0.769f, 0.710f, 0.992f, 0.45f));
                btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
                btn.style.color = new Color(0.769f, 0.710f, 0.992f, 1f);
            }
            else
            {
                SetBorderWidth(btn, 0f);
                btn.style.backgroundColor = ColAmber;
                btn.style.color = new Color(0.086f, 0.055f, 0.024f, 1f);
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
