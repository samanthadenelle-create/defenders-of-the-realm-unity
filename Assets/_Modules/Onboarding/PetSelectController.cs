// =============================================================================
// PetSelectController — drives the pet-select screen (intro flow).
// -----------------------------------------------------------------------------
// The owner-acceptance-checklist "Intro & first-run flow" calls out a missing
// pet-select screen: the three starter Wardens + PetDeployer existed, but
// there was no pick-a-pet UI. This is that screen — a sibling of hero-select,
// shown right after it.
//
// THE SCREEN reuses the hero-select scaffold: same SelectScreen.uss, same card
// layout (PetSelectScreen.uxml). Three pet cards — Aether Sprite / Flame Pup /
// Ice Wolf — built at runtime from the canonical pets.json via IntroPetCatalog
// (a StreamingAssets read, not a DeNelle.Pets dependency — module isolation).
//
// COPY: the screen title / subtitle / confirm label come from en.json via
// CanonStrings; the per-pet name + archetype come straight from pets.json.
// The card glyph is the pet's first initial; the accent uses the pet's own
// glowColor from pets.json — all presentation, no inline canon strings.
//
// PERSISTENCE: on confirm the chosen pet id is written to
// GameState.StarterPetId (#2 — "id of the onboarding starter pet"). There is
// no GameStateService.ChooseStarterPet mutator, so the controller writes the
// SO field directly and calls Save() — see the integrator note at the foot of
// this file. The id space matches DeNelle.Pets.PetCatalog ("pet-aether-sprite"
// etc.) so the village pet system resolves the pick through its own catalog.
//
// On confirm the flow routes to the Village (SceneRouter.GoVillage) — the end
// of the intro chain: Title -> HeroSelect -> PetSelect -> Village.
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
    /// Drives the pet-select screen: builds the three starter-pet cards from
    /// pets.json, tracks the player's pick, and on confirm writes
    /// <see cref="GameState.StarterPetId"/> and routes to the Village.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetSelectController : MonoBehaviour
    {
        // ── UXML element names — the binding contract with PetSelectScreen.uxml ─
        private const string RootName = "pet-select-root";
        private const string TitleName = "pet-select-title";
        private const string SubtitleName = "pet-select-subtitle";
        private const string CardRowName = "pet-card-row";
        private const string ConfirmName = "pet-select-confirm";

        // ── USS class names — styled by SelectScreen.uss (shared with hero-select) ─
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
        private const string TitleKey = "petSelect.title";
        private const string SubtitleKey = "petSelect.subtitle";
        private const string ConfirmKey = "petSelect.confirm";

        // ── en.json key prefix for the per-element pet blurb (elementBlurb.*) ─
        private const string ElementBlurbPrefix = "elementBlurb.";

        [Header("UI")]
        [Tooltip("UIDocument hosting PetSelectScreen.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Behaviour")]
        [Tooltip("Skip straight to the Village when the save already records a starter pet " +
                 "(a returning player who finished the intro). Editor testing: disable to " +
                 "always show the screen.")]
        [SerializeField] private bool _skipWhenPetChosen = true;

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private Label _title;
        private Label _subtitle;
        private VisualElement _cardRow;
        private Button _confirmButton;

        // One card VisualElement per pet, in IntroPetCatalog order.
        private VisualElement[] _cards;

        private bool _bound;
        private bool _hasSelection;
        private string _selectedPetId;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // A returning player who already picked a starter pet skips this
            // screen — route on to the Village before binding any UI.
            if (_skipWhenPetChosen && HasStarterPet())
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

        /// <summary>True when the save already records a starter pet id.</summary>
        private static bool HasStarterPet()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false;
            return !string.IsNullOrEmpty(svc.State.StarterPetId);
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[PetSelectController] No UIDocument root — pet-select will not display.");
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

            BuildCards();

            if (_confirmButton != null)
            {
                _confirmButton.clicked -= OnConfirmClicked; // guard a double OnEnable
                _confirmButton.clicked += OnConfirmClicked;
            }

            RefreshConfirmEnabled();
            _bound = true;
        }

        /// <summary>Builds one card per starter pet from the pets.json catalog.</summary>
        private void BuildCards()
        {
            if (_cardRow == null) return;
            _cardRow.Clear();

            var pets = IntroPetCatalog.Pets;
            _cards = new VisualElement[pets.Count];

            if (pets.Count == 0)
            {
                Debug.LogError("[PetSelectController] No starter pets loaded from pets.json — " +
                               "the pet-select screen has nothing to show.");
                return;
            }

            for (int i = 0; i < pets.Count; i++)
            {
                VisualElement card = BuildCard(pets[i]);
                _cardRow.Add(card);
                _cards[i] = card;
            }
        }

        /// <summary>Builds one pet card VisualElement from a pets.json entry.</summary>
        private VisualElement BuildCard(IntroPetInfo pet)
        {
            var card = new VisualElement { name = $"pet-card-{pet.Id}" };
            card.AddToClassList(CardClass);

            Color accent = pet.GlowUnityColor;

            // Portrait block — the pet's first initial, tinted with its glow.
            var portrait = new VisualElement();
            portrait.AddToClassList(PortraitClass);
            string initial = !string.IsNullOrEmpty(pet.Name)
                ? pet.Name.Substring(0, 1).ToUpperInvariant()
                : "?";
            var glyph = new Label(initial);
            glyph.AddToClassList(GlyphClass);
            glyph.style.color = accent;
            portrait.Add(glyph);
            card.Add(portrait);

            // Element-coloured accent strip.
            var accentStrip = new VisualElement();
            accentStrip.AddToClassList(AccentClass);
            accentStrip.style.backgroundColor = accent;
            card.Add(accentStrip);

            // Text body — name / archetype role / element blurb.
            var body = new VisualElement();
            body.AddToClassList(BodyClass);

            var nameLabel = new Label(pet.Name ?? "Warden");
            nameLabel.AddToClassList(NameClass);
            body.Add(nameLabel);

            var roleLabel = new Label(pet.Archetype ?? string.Empty);
            roleLabel.AddToClassList(RoleClass);
            body.Add(roleLabel);

            // The blurb is the canon element flavour line from en.json
            // (elementBlurb.aether / .flame / .ice).
            var blurbLabel = new Label(ElementBlurb(pet.Element));
            blurbLabel.AddToClassList(BlurbClass);
            body.Add(blurbLabel);

            card.Add(body);

            // The whole card is the hit target — capture the id in a local.
            string capturedId = pet.Id;
            card.RegisterCallback<PointerDownEvent>(_ => OnCardClicked(capturedId));

            return card;
        }

        /// <summary>
        /// Resolves the canon element blurb for a pet's element token. Element
        /// ids in pets.json (aether/flame/ice) key directly into the en.json
        /// <c>elementBlurb.*</c> family.
        /// </summary>
        private static string ElementBlurb(string element)
        {
            if (string.IsNullOrEmpty(element)) return string.Empty;
            return CanonStrings.Locale(ElementBlurbPrefix + element);
        }

        // =====================================================================
        //  Selection
        // =====================================================================

        /// <summary>A pet card was tapped — mark it active and clear the rest.</summary>
        private void OnCardClicked(string petId)
        {
            _selectedPetId = petId;
            _hasSelection = true;
            UpdateCardHighlight();
            RefreshConfirmEnabled();
        }

        /// <summary>Marks the active pet card and clears the others.</summary>
        private void UpdateCardHighlight()
        {
            if (_cards == null) return;
            var pets = IntroPetCatalog.Pets;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null || i >= pets.Count) continue;
                bool active = _hasSelection && pets[i].Id == _selectedPetId;
                _cards[i].EnableInClassList(CardActiveClass, active);
            }
        }

        /// <summary>
        /// Enables the confirm button only once a pet is chosen; before then it
        /// shows a dimmed, inert-looking state.
        /// </summary>
        private void RefreshConfirmEnabled()
        {
            if (_confirmButton == null) return;
            _confirmButton.SetEnabled(_hasSelection);
            _confirmButton.EnableInClassList(ConfirmDisabledClass, !_hasSelection);
        }

        // =====================================================================
        //  Confirm — write the choice + route to the Village
        // =====================================================================

        /// <summary>
        /// Confirm tapped: writes the chosen starter-pet id to
        /// <see cref="GameState.StarterPetId"/> and Save()s, then routes to the
        /// Village — the end of the intro flow.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (!_hasSelection) return;

            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
            {
                // There is no GameStateService.ChooseStarterPet mutator; the
                // starter-pet id is a plain persisted field, so set it on the SO
                // and Save() through the same Core save layer every mutator uses.
                svc.State.StarterPetId = _selectedPetId;
                svc.Save();
            }
            else
            {
                Debug.LogWarning("[PetSelectController] No GameStateService — the pet choice " +
                                 "was NOT persisted. Routing onward anyway.");
            }

            SceneRouter.GoVillage();
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the pet-select scene.
// -----------------------------------------------------------------------------
//   1. The PetSelect scene is generated by the editor builder
//      DeNelle.Editor.IntroFlowSceneBuilder.BuildAll alongside HeroSelect — a
//      Camera, an EventSystem, and a UIDocument GameObject carrying
//      PetSelectScreen.uxml with this controller attached. The builder
//      registers the scene in Build Settings between HeroSelect and Village.
//
//   2. StarterPetId persistence: this controller writes GameState.StarterPetId
//      directly and calls GameStateService.Save() because no ChooseStarterPet
//      mutator exists. That is functionally equivalent to a mutator (the field
//      is plain, nothing subscribes to a pet-roster change during the intro),
//      but if the team later wants the canonical pattern, add a
//      GameStateService.ChooseStarterPet(string) that sets the field, raises
//      PetsChanged and Save()s — then call it here instead.
//
//   3. The chosen pet id ("pet-aether-sprite" / "pet-flame-pup" / "pet-ice-wolf")
//      is the same id space DeNelle.Pets.PetCatalog uses, so the village /
//      PetDeployer resolves the player's starter through its own catalog.
// =============================================================================
