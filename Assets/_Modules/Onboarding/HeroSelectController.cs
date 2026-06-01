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

using Cysharp.Threading.Tasks;
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
        private const string DiveName   = "hero-select-dive";
        private const string WalletConnectName = "hero-wallet-connect";
        private const string NavPrevName = "hero-nav-prev";
        private const string NavNextName = "hero-nav-next";

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
        private const string TitleKey    = "heroSelect.title";
        private const string SubtitleKey = "heroSelect.subtitle";
        // "Dive into Village" — secondary CTA, routes to normal village.
        private const string DiveKey    = "heroSelect.diveVillage";
        // "Jump into the Action" — primary CTA, routes to Defend the Tower.
        private const string ConfirmKey = "heroSelect.jumpAction";

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
        private Button _confirmButton;   // "Jump into the Action" — Defend the Tower
        private Button _diveButton;      // "Dive into Village"    — normal village

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
            if (_diveButton    != null) _diveButton.clicked    -= OnDiveVillageClicked;

            // Unwire the WO-42 buttons too — re-resolve from the root so a
            // disable after a partial bind is still safe.
            if (_root != null)
            {
                var walletBtn = _root.Q<Button>(WalletConnectName);
                if (walletBtn != null) walletBtn.clicked -= OnWalletConnectClicked;
                var navPrev = _root.Q<Button>(NavPrevName);
                if (navPrev != null) navPrev.clicked -= OnNavPrevClicked;
                var navNext = _root.Q<Button>(NavNextName);
                if (navNext != null) navNext.clicked -= OnNavNextClicked;
            }

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

            _title         = _root.Q<Label>(TitleName);
            _subtitle      = _root.Q<Label>(SubtitleName);
            _cardRow       = _root.Q<VisualElement>(CardRowName);
            _confirmButton = _root.Q<Button>(ConfirmName);
            _diveButton    = _root.Q<Button>(DiveName);

            // Header + CTA copy — canon strings, never typed inline.
            if (_title   != null) _title.text   = CanonStrings.Locale(TitleKey);
            if (_subtitle != null) _subtitle.text = CanonStrings.Locale(SubtitleKey);
            // Fallback text so the buttons are always legible even if en.json
            // hasn't been updated yet.
            if (_confirmButton != null)
                _confirmButton.text = FallbackLocale(ConfirmKey, "Jump into the Action");
            if (_diveButton != null)
                _diveButton.text    = FallbackLocale(DiveKey,    "Dive into Village");

            BuildCards();

            if (_confirmButton != null)
            {
                _confirmButton.clicked -= OnConfirmClicked;     // guard a double OnEnable
                _confirmButton.clicked += OnConfirmClicked;
            }
            if (_diveButton != null)
            {
                _diveButton.clicked -= OnDiveVillageClicked;    // guard a double OnEnable
                _diveButton.clicked += OnDiveVillageClicked;
            }

            // Wallet-connect CTA (WO-42). The connection logic (SKR / Solana
            // SDK) is out of scope for this WO — register a stub callback so
            // the button is live and a future WO can drop the integration in.
            var walletBtn = _root.Q<Button>(WalletConnectName);
            if (walletBtn != null)
            {
                walletBtn.clicked -= OnWalletConnectClicked; // guard a double OnEnable
                walletBtn.clicked += OnWalletConnectClicked;
            }

            // Nav arrows flanking the card row (WO-42). With three heroes they
            // cycle the active-selection ring (wrapping); they are future-proofed
            // for a larger roster / carousel.
            var navPrev = _root.Q<Button>(NavPrevName);
            var navNext = _root.Q<Button>(NavNextName);
            if (navPrev != null)
            {
                navPrev.clicked -= OnNavPrevClicked; // guard a double OnEnable
                navPrev.clicked += OnNavPrevClicked;
            }
            if (navNext != null)
            {
                navNext.clicked -= OnNavNextClicked; // guard a double OnEnable
                navNext.clicked += OnNavNextClicked;
            }

            // A save mid-intro (hero chosen, no pet yet) pre-selects that hero
            // so the player can confirm straight through.
            PreselectFromSave();
            RefreshConfirmEnabled();

            _bound = true;
        }

        // =====================================================================
        //  Wallet connect + nav arrows  (WO-42)
        // =====================================================================

        /// <summary>
        /// Wallet-connect CTA tapped. UI-only for now: the SKR / Solana wallet
        /// SDK integration is reserved for a future WO. Logs so the wiring is
        /// observable in the editor / player console.
        /// </summary>
        private void OnWalletConnectClicked()
        {
            // TODO (future WO): wire the SKR / Solana wallet SDK here.
            Debug.Log("[HeroSelectController] Wallet connect tapped — not yet wired.");
        }

        /// <summary>‹ nav arrow — moves the selection ring one hero back (wraps).</summary>
        private void OnNavPrevClicked() => CycleHero(-1);

        /// <summary>› nav arrow — moves the selection ring one hero forward (wraps).</summary>
        private void OnNavNextClicked() => CycleHero(+1);

        /// <summary>
        /// Cycles the active-hero selection by <paramref name="direction"/>
        /// (-1 / +1), wrapping around the catalog. With no current selection it
        /// starts from the first hero (forward) or the last (backward).
        /// </summary>
        private void CycleHero(int direction)
        {
            int count = HeroCatalog.Heroes.Length;
            if (count == 0) return;

            int current = _hasSelection
                ? System.Array.FindIndex(HeroCatalog.Heroes, h => h.Hero == _selectedHero)
                : -1;
            int next = ((current + direction) % count + count) % count;
            OnCardClicked(HeroCatalog.Heroes[next].Hero);
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
            //
            // DEF: Load as Sprite first — HeroPortraitGenerator imports the PNG
            // as Sprite type, and Resources.Load<Texture2D>() returns null for
            // Sprite assets (causing the portrait to silently fall through to the
            // glyph). If no Sprite exists, try Texture2D (covers plain-imported
            // or freshly-committed textures that haven't been through the generator).
            var portrait = new VisualElement();
            portrait.AddToClassList(PortraitClass);
            string slug = info.Hero.ToString().ToLowerInvariant();

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
                    glyph.AddToClassList(GlyphClass);
                    glyph.style.color = info.Accent;
                    portrait.Add(glyph);
                }
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
        /// Enables both CTA buttons only once a hero is chosen; before then both
        /// show a dimmed, inert-looking state.
        /// </summary>
        private void RefreshConfirmEnabled()
        {
            if (_confirmButton != null)
            {
                _confirmButton.SetEnabled(_hasSelection);
                _confirmButton.EnableInClassList(ConfirmDisabledClass, !_hasSelection);
            }
            if (_diveButton != null)
            {
                _diveButton.SetEnabled(_hasSelection);
                _diveButton.EnableInClassList(ConfirmDisabledClass, !_hasSelection);
            }
        }

        /// <summary>
        /// Returns the localised string for <paramref name="key"/>, falling back to
        /// <paramref name="fallback"/> when the key is absent (so the button is never
        /// blank if <c>en.json</c> hasn't been updated yet).
        /// </summary>
        private static string FallbackLocale(string key, string fallback)
        {
            var s = CanonStrings.Locale(key);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        // =====================================================================
        //  Entry-point CTAs — write the hero choice then route
        // =====================================================================

        /// <summary>
        /// "Jump into the Action" — persists the chosen hero and routes straight to
        /// the Defend the Tower (PatriciaLight) scene, bypassing the pet-select
        /// intro flow. Wave 1 difficulty is used as the entry params.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (!_hasSelection) return;
            PersistHero();
            // Fire-and-forget the async scene load — the returned UniTask is
            // intentionally not awaited (routing proceeds as this screen tears down).
            SceneRouter.GoPatriciaLight(new PatriciaLightParams { Wave = 1 });
        }

        /// <summary>
        /// "Dive into Village" — the canonical first-run path. Persists the chosen
        /// hero, then routes to the PET-SELECT screen (WO-185) so the player bonds
        /// a starter Warden before entering the village. PetSelect's confirm then
        /// routes on to the Village.
        ///
        /// A returning player who already has a starter pet is skipped past
        /// pet-select automatically (PetSelectController self-skips to the Village
        /// when GameState already records a StarterPetId), so this single route is
        /// correct for both first-run and returning players.
        /// </summary>
        private void OnDiveVillageClicked()
        {
            if (!_hasSelection) return;
            PersistHero();
            SceneRouter.GoPetSelect();
        }

        /// <summary>
        /// Writes <see cref="GameState.HeroClass"/> via <see cref="GameStateService"/>
        /// and saves. Shared by both entry-point handlers.
        /// </summary>
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
//   3. Routing (WO-185): the Title "Start" button calls GoHeroSelect();
//      "Dive into Village" (the canonical first-run CTA) calls GoPetSelect(),
//      and PetSelectController's confirm calls GoVillage() — so the full intro
//      chain is Title -> HeroSelect -> PetSelect -> Village. The secondary
//      "Jump into the Action" CTA is an express shortcut straight to the
//      Defend-the-Tower mode (GoPatriciaLight) and intentionally bypasses
//      pet-select; the player picks a Warden on the next normal village entry.
// =============================================================================
