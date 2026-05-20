// =============================================================================
// HeroSelectController — drives the hero-select screen (intro flow).
// -----------------------------------------------------------------------------
// The owner-acceptance-checklist "Intro & first-run flow" calls out a missing
// hero-select screen: the hero class was data-only (HeroClass / HeroClassOpt)
// with no pick-a-hero UI. This is that screen.
//
// THE SCREEN: a full-screen UI Toolkit document (HeroSelectScreen.uxml +
// SelectScreen.uss) shown between the Title scene's studio bumper and the
// Village. Three hero cards — Mage / Knight / Ranger — each with a portrait
// glyph, name, role line and blurb. Tapping a card selects it; the confirm
// button writes the choice and routes onward.
//
// COPY: every string on the screen (title, subtitle, per-hero name/role/blurb,
// the confirm label) is resolved from en.json via CanonStrings at runtime —
// none are baked into the UXML (port-spec Part 4). The card glyph + accent
// colour come from HeroCatalog (pure presentation data, legitimately in C#).
//
// THE BINDING IDIOM mirrors SettingsController: an OnEnable() BindElements()
// resolves the UXML elements by name, builds the cards once, registers the
// callbacks; OnDisable() unregisters them.
//
// PERSISTENCE: on confirm, GameStateService.ChooseHero(hero) writes
// GameState.HeroClass (#28) and Save()s it. The flow then routes to the
// pet-select screen via SceneRouter.GoPetSelect.
//
// RETURNING-PLAYER SKIP: a save that already records BOTH a hero AND a
// starter pet has finished the intro flow — the screen self-skips straight to
// the Village so a returning player is not re-asked. (A half-finished intro —
// hero chosen but no pet — still shows hero-select pre-selected, so the player
// can confirm through to pet-select.)
//
// Lives in DeNelle.Onboarding; references DeNelle.Core only — module isolation.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Drives the hero-select screen: builds the three hero cards, tracks the
    /// player's pick, and on confirm writes <see cref="GameState.HeroClass"/>
    /// and routes to pet-select. A returning player who has finished the intro
    /// is skipped straight to the Village.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HeroSelectController : MonoBehaviour
    {
        // ── UXML element names — the binding contract with HeroSelectScreen.uxml ─
        private const string RootName = "hero-select-root";
        private const string TitleName = "hero-select-title";
        private const string SubtitleName = "hero-select-subtitle";
        private const string CardRowName = "hero-card-row";
        private const string ConfirmName = "hero-select-confirm";

        // ── USS class names — styled by SelectScreen.uss ─────────────────────
        private const string CardClass = "select-card";
        private const string CardActiveClass = "select-card--active";
        private const string PortraitClass = "select-card__portrait";
        private const string GlyphClass = "select-card__glyph";
        private const string AccentClass = "select-card__accent";
        private const string BodyClass = "select-card__body";
        private const string NameClass = "select-card__name";
        private const string RoleClass = "select-card__role";
        private const string BlurbClass = "select-card__blurb";
        private const string ConfirmDisabledClass = "select-confirm--disabled";

        // ── en.json keys for the screen's own copy ───────────────────────────
        private const string TitleKey = "heroSelect.title";
        private const string SubtitleKey = "heroSelect.subtitle";
        private const string ConfirmKey = "heroSelect.confirm";

        [Header("UI")]
        [Tooltip("UIDocument hosting HeroSelectScreen.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Behaviour")]
        [Tooltip("Skip straight to the Village when the save already records a hero AND a " +
                 "starter pet (a returning player who finished the intro). Editor testing: " +
                 "disable to always show the screen.")]
        [SerializeField] private bool _skipWhenIntroComplete = true;

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private Label _title;
        private Label _subtitle;
        private VisualElement _cardRow;
        private Button _confirmButton;

        // One card VisualElement per hero, in HeroCatalog order.
        private readonly VisualElement[] _cards = new VisualElement[3];

        private bool _bound;
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
            // screen entirely — route on before binding any UI.
            if (_skipWhenIntroComplete && IsIntroComplete())
            {
                SceneRouter.GoVillage();
                return;
            }

            BindElements();
        }

        private void OnDisable()
        {
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            _bound = false;
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
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[HeroSelectController] No UIDocument root — hero-select will not display.");
                return;
            }

            _title = _root.Q<Label>(TitleName);
            _subtitle = _root.Q<Label>(SubtitleName);
            _cardRow = _root.Q<VisualElement>(CardRowName);
            _confirmButton = _root.Q<Button>(ConfirmName);

            // Header + confirm copy — canon strings, never typed inline.
            if (_title != null) _title.text = CanonStrings.Locale(TitleKey);
            if (_subtitle != null) _subtitle.text = CanonStrings.Locale(SubtitleKey);
            if (_confirmButton != null) _confirmButton.text = CanonStrings.Locale(ConfirmKey);

            // Owner direction 2026-05-20: surface a clear call-to-action so
            // the player knows what to do — "Select your hero to start" sits
            // above the card row in a soft amber tone.
            AddHeroSelectHint(_root);

            BuildCards();

            if (_confirmButton != null)
            {
                _confirmButton.clicked -= OnConfirmClicked; // guard a double OnEnable
                _confirmButton.clicked += OnConfirmClicked;
            }

            // A save mid-intro (hero chosen, no pet yet) pre-selects that hero
            // so the player can confirm straight through.
            PreselectFromSave();
            RefreshConfirmEnabled();

            _bound = true;
        }

        /// <summary>
        /// Adds an explicit "Select your hero to start" hint label above
        /// the card row. Idempotent — re-runs of OnEnable don't stack copies.
        /// </summary>
        private static void AddHeroSelectHint(VisualElement root)
        {
            if (root == null) return;
            var existing = root.Q<Label>("hero-select-hint");
            if (existing != null) return;
            var hint = new Label("Select your hero to start") { name = "hero-select-hint" };
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.fontSize = 16;
            hint.style.color = new StyleColor(new Color(1f, 0.86f, 0.55f, 1f));
            hint.style.marginTop = 4;
            hint.style.marginBottom = 6;
            hint.style.unityFontStyleAndWeight = FontStyle.Italic;
            // Insert near the top of the layout so it sits between the
            // header and the card row.
            root.Insert(Mathf.Min(2, root.childCount), hint);
        }

        /// <summary>Builds the three hero cards into the card row once.</summary>
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
            }
        }

        /// <summary>Builds one hero card VisualElement from its catalog entry.</summary>
        private VisualElement BuildCard(HeroCardInfo info)
        {
            var card = new VisualElement { name = $"hero-card-{info.Hero}" };
            card.AddToClassList(CardClass);

            // Portrait block — show the rendered hero PNG if one exists at
            // Resources/HeroPortraits/<slug>.png; fall back to the glyph
            // letter otherwise. Slug matches HeroPortraitGenerator output.
            var portrait = new VisualElement();
            portrait.AddToClassList(PortraitClass);
            string slug = info.Hero.ToString().ToLowerInvariant();
            var portraitTex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
            if (portraitTex != null)
            {
                portrait.style.backgroundImage = new StyleBackground(portraitTex);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }
            else
            {
                var glyph = new Label(info.Glyph);
                glyph.AddToClassList(GlyphClass);
                glyph.style.color = info.Accent;
                portrait.Add(glyph);
            }
            card.Add(portrait);

            // Element-coloured accent strip.
            var accent = new VisualElement();
            accent.AddToClassList(AccentClass);
            accent.style.backgroundColor = info.Accent;
            card.Add(accent);

            // Text body — name / role / blurb, all from en.json.
            var body = new VisualElement();
            body.AddToClassList(BodyClass);

            var nameLabel = new Label(CanonStrings.Locale(info.NameKey));
            nameLabel.AddToClassList(NameClass);
            body.Add(nameLabel);

            var roleLabel = new Label(CanonStrings.Locale(info.RoleKey));
            roleLabel.AddToClassList(RoleClass);
            body.Add(roleLabel);

            var blurbLabel = new Label(CanonStrings.Locale(info.BlurbKey));
            blurbLabel.AddToClassList(BlurbClass);
            body.Add(blurbLabel);

            card.Add(body);

            // The whole card is the hit target — capture the hero in a local.
            HeroClass captured = info.Hero;
            card.RegisterCallback<PointerDownEvent>(_ => OnCardClicked(captured));

            return card;
        }

        // =====================================================================
        //  Selection
        // =====================================================================

        /// <summary>A hero card was tapped — mark it active and clear the rest.</summary>
        private void OnCardClicked(HeroClass hero)
        {
            _selectedHero = hero;
            _hasSelection = true;
            UpdateCardHighlight();
            RefreshConfirmEnabled();
        }

        /// <summary>
        /// Pre-selects the hero the save already records, if any — covers a
        /// player who chose a hero on a prior session but had not yet picked a
        /// pet, so the screen reflects their earlier choice.
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
                UpdateCardHighlight();
            }
        }

        /// <summary>Marks the active hero card and clears the others.</summary>
        private void UpdateCardHighlight()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                bool active = _hasSelection && HeroCatalog.Heroes[i].Hero == _selectedHero;
                _cards[i].EnableInClassList(CardActiveClass, active);
            }
        }

        /// <summary>
        /// Enables the confirm button only once a hero is chosen; before then it
        /// shows a dimmed, inert-looking state.
        /// </summary>
        private void RefreshConfirmEnabled()
        {
            if (_confirmButton == null) return;
            _confirmButton.SetEnabled(_hasSelection);
            _confirmButton.EnableInClassList(ConfirmDisabledClass, !_hasSelection);
        }

        // =====================================================================
        //  Confirm — write the choice + route to pet-select
        // =====================================================================

        /// <summary>
        /// Confirm tapped: persists the chosen hero through
        /// <see cref="GameStateService.ChooseHero"/> (writes
        /// <see cref="GameState.HeroClass"/> and Save()s) and routes to the
        /// pet-select screen.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (!_hasSelection) return;

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

            SceneRouter.GoPetSelect();
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the hero-select scene.
// -----------------------------------------------------------------------------
//   1. The HeroSelect scene is generated by the editor builder
//      DeNelle.Editor.IntroFlowSceneBuilder.BuildAll (menu: Defenders/Intro
//      Flow/Build Hero + Pet Select Scenes). It creates a Camera, an
//      EventSystem, and a UIDocument GameObject carrying HeroSelectScreen.uxml
//      with this controller attached, and registers the scene in Build
//      Settings between Title and Village.
//
//   2. GameStateService must exist before this screen so ChooseHero persists.
//      It is a DontDestroyOnLoad singleton created by the Core bootstrap in the
//      Title scene, so it is already alive by the time the intro flow reaches
//      hero-select. If a session somehow enters HeroSelect cold, the controller
//      logs a warning and still routes on (the choice just is not saved).
//
//   3. Routing: the Title "Start" button calls SceneRouter.GoHeroSelect();
//      this controller's confirm calls SceneRouter.GoPetSelect();
//      PetSelectController's confirm calls SceneRouter.GoVillage().
// =============================================================================
