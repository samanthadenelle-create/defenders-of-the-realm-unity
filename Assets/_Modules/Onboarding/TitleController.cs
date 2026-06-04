// =============================================================================
// TitleController — the Title scene orchestrator (Week 1)
// -----------------------------------------------------------------------------
// Port of src/modules/onboarding/LandingPage.tsx. Owns the Title scene's
// arrival sequence and the persistent title screen itself.
//
// FIRST-LAUNCH ARRIVAL SEQUENCE (mirrors the React boot path
// StudioBumper -> StoryIntro -> LandingPage):
//   1. SplashLoading        — the DeNelle Studios studio bumper (~3s video).
//   2. StoryIntroController — the three-line cold open (~5s), first launch only.
//   3. The title screen      — Heart-Wing banner, tagline, Connect Wallet, Start.
// A returning player (GameState.Onboarded == true) skips step 2; the bumper
// still plays as a brand beat. The bumper and story intro are SEPARATE
// GameObjects (each with its own UIDocument) wired into the serialized fields
// below by OnboardingSceneBuilder.
//
// THE TITLE SCREEN itself is a UI Toolkit document (TitleScreen.uxml/.uss). Every
// canon string on it — the tagline "By lantern. By oath. By Heart.", the game
// title, the "DeNelle Studios" credit — is loaded from canon-strings.json via
// CanonStrings at runtime. NONE are baked into the .uxml (v2 port-spec Part 4).
//
//   Connect Wallet button — Week-1 stub: logs and no-ops (the real Solana flow
//                            is the Week-7 Wallet module).
//   Hero cards            — the landing IS the hero-select (see the 2026-06-03
//                            note below): tap a card, confirm, route to PetSelect
//                            (Title -> PetSelect -> Village). The standalone
//                            HeroSelect scene is no longer the visible landing.
//
// async UniTask for the arrival flow — never async void (port-spec Part 3).
//
// 2026-06-03 — RECURRING-REGRESSION FIX (the title landing is the CANONICAL
// hero-select). The owner lands on THIS screen (not HeroSelectController), so
// this is where the four hero cards must live. The old title screen BOUND to
// UXML element names (_root.Q<VisualElement>("hero-flank-left") …); per
// CLAUDE.md §8 the UXML root comes up EMPTY in the WebGL player build, so every
// Q<>() returned null and the portraits / styling / click-wiring all silently
// no-op'd — "four players, no player cards, not styled to work." The fix mirrors
// HeroSelectController's rewrite: the entire hero-pick area is now BUILT IN CODE
// (dragon top-half, four evenly-distributed bottom cards, a selected-hero detail
// card below), inline-styled with flex / percent so it re-flows in BOTH
// landscape and portrait, and guarded by a VerifyFourCardsEven assert. There is
// no longer any UXML element this landing depends on, so a UXML that does not
// render cannot break it. The card pick routes straight onward to PetSelect, so
// the standalone HeroSelect scene is not a confusing second hero-pick surface.
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Drives the Title scene: the studio-bumper -> cold-open -> title-screen
    /// arrival sequence, and the title screen's tagline / buttons.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleController : MonoBehaviour
    {
        [Header("Arrival sequence")]
        [Tooltip("The studio bumper — plays first. Optional; skipped if unassigned.")]
        [SerializeField] private SplashLoading _splash;

        [Tooltip("The three-line cold open — plays on first launch only. Optional.")]
        [SerializeField] private StoryIntroController _storyIntro;

        [Header("Title screen")]
        [Tooltip("The Title scene's UI Toolkit document (TitleScreen.uxml).")]
        [SerializeField] private UIDocument _titleDocument;

        [Tooltip("The Heart-Wing banner image — heart-wing.jpg imported as a Sprite.")]
        [SerializeField] private Sprite _heartWingBanner;

        // ── Code-built title / hero-select elements (no UXML dependency) ─────
        private VisualElement _root;
        private VisualElement _dragonStage;   // top half — dragon art
        private VisualElement _cardRow;       // bottom — 4 hero cards
        private VisualElement _detailCard;    // below cards — selected-hero details
        private Label _detailName;
        private Label _detailRole;
        private Label _detailBlurb;
        private Button _connectWalletButton;

        // One card VisualElement per hero, in HeroCatalog order.
        private readonly VisualElement[] _cards = new VisualElement[HeroCatalog.Heroes.Length];

        private bool _hasSelection;
        private HeroClass _selectedHero;

        // Auto-advance: selecting a hero IS the commit (DEF-230 — drop the extra
        // "Next"/"Choose" tap). After a card is tapped we show a brief visual
        // confirm beat, then route to PetSelect automatically. _advancing guards
        // against a double-route; until the route actually fires, tapping a
        // DIFFERENT card re-selects (and re-arms the beat) so a mis-tap is
        // trivially recoverable. The "Choose this Hero →" button is kept as a
        // belt-and-braces second path but is no longer required.
        private bool _advancing;
        private bool _autoAdvanceArmed;   // only a user TAP arms the beat (not a save-preselect)
        private float _autoAdvanceAt;
        // How long to linger on the selected card before auto-advancing — long
        // enough to register the pick + re-tap a different hero, short enough to
        // feel like "select = go" (the owner wants fewer taps).
        private const float AutoAdvanceDelay = 0.6f;

        // ── Inline palette (no USS dependency — WebGL-safe) ─────────────────
        private static readonly Color ColBackground  = new Color(0.024f, 0.016f, 0.047f, 1f);
        private static readonly Color ColPanelDark    = new Color(0.071f, 0.047f, 0.118f, 0.96f);
        private static readonly Color ColCardIdle     = new Color(0.102f, 0.071f, 0.165f, 0.98f);
        private static readonly Color ColCardActive   = new Color(0.149f, 0.102f, 0.204f, 1f);
        private static readonly Color ColAmber        = new Color(0.961f, 0.651f, 0.137f, 1f);
        private static readonly Color ColVioletBorder = new Color(0.486f, 0.227f, 0.929f, 0.35f);
        private static readonly Color ColTextBright   = new Color(0.929f, 0.914f, 0.980f, 1f);
        private static readonly Color ColTextMuted    = new Color(0.729f, 0.698f, 0.824f, 0.90f);

        // Panel-render diagnostic (temporary).
        private bool _titleBuilt;
        private int _diagFrames;

        // DEF-204: VerifyFourCardsEven now SELF-HEALS (rebuilds a malformed row)
        // instead of only logging. This guard prevents an infinite rebuild loop —
        // we only attempt the auto-correct once per build.
        private bool _cardHealAttempted;

        private void OnEnable()
        {
            // Hide the title screen until the arrival sequence has finished, so
            // the player does not see it flash behind the bumper / cold open.
            if (_titleDocument != null)
                SetTitleVisible(false);

            // Animated star/comet background — replaces the React build's
            // landing-page parallax that owners said pulled players in during
            // the 10-15 s decision window. Spawned once per Title scene load.
            if (GameObject.Find("TitleStarfield") == null)
                new GameObject("TitleStarfield").AddComponent<TitleStarfield>();
        }

        private void Start()
        {
            // Fire-and-forget the arrival flow. RunArrival is a UniTask (not
            // async void); Forget() is the sanctioned way to launch a top-level
            // UniTask from a Unity lifecycle hook.
            RunArrival().Forget();
        }

        // Temporary panel-render diagnostic — logs the title panel's state a
        // beat after it is built, from a plain Update so it runs regardless of
        // any UI Toolkit layout/scheduler issue. Also enumerates every other
        // UIDocument in the scene: if the bumper/cold-open roots are still
        // attached to the shared panel they can intercept Title-button picks.
        private void Update()
        {
            // DEF-230 auto-advance: once a hero is selected, route to PetSelect
            // after a brief confirm beat — no "Choose this Hero" tap required.
            // Re-selecting a different hero before this fires simply re-arms the
            // timer (handled in OnCardClicked), so a mis-tap is recoverable.
            if (_autoAdvanceArmed && _hasSelection && !_advancing && Time.unscaledTime >= _autoAdvanceAt)
            {
                _advancing = true;
                AdvanceToPetSelect();
                return;
            }

            if (!_titleBuilt || _diagFrames < 0) return;
            if (++_diagFrames < 120) return;
            _diagFrames = -1;
            var rt = _titleDocument != null ? _titleDocument.rootVisualElement : null;
            Debug.Log($"[TitleController] PANELDIAG docEnabled=" +
                      $"{_titleDocument != null && _titleDocument.isActiveAndEnabled} " +
                      $"panelSettings={_titleDocument != null && _titleDocument.panelSettings != null} " +
                      $"rootPanel={rt != null && rt.panel != null} " +
                      $"worldBound={(rt != null ? rt.worldBound.ToString() : "n/a")} " +
                      $"screen={Screen.width}x{Screen.height}");

            var allDocs = FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in allDocs)
            {
                var root = d != null ? d.rootVisualElement : null;
                bool attached = root != null && root.panel != null;
                Debug.Log($"[TitleController] PANELDIAG doc='{d.gameObject.name}' " +
                          $"enabled={d.enabled} active={d.gameObject.activeInHierarchy} " +
                          $"sortingOrder={d.sortingOrder} " +
                          $"attachedToPanel={attached} " +
                          $"rootDisplay={(root != null ? root.style.display.ToString() : "n/a")} " +
                          $"rootPicking={(root != null ? root.pickingMode.ToString() : "n/a")} " +
                          $"rootChildCount={(root != null ? root.childCount.ToString() : "n/a")}");
            }

            if (_cardRow != null)
                Debug.Log($"[TitleController] PANELDIAG card-row worldBound=" +
                          $"{_cardRow.worldBound} cards={_cardRow.childCount}");
        }

        /// <summary>
        /// Runs the full first-launch arrival sequence, then reveals the title
        /// screen. Each stage is awaited so the next only starts when the
        /// previous has fully faded out.
        /// </summary>
        private async UniTask RunArrival()
        {
            Debug.Log("[TitleController] Arrival: start.");

            // Stage 1 — studio bumper. Timeout-guarded: the bumper VIDEO (or any
            // await inside) can fail to complete in a WebGL build and would hang the
            // whole boot at a black screen (older builds only "limped past" it because
            // exceptions were disabled and silently swallowed). Never hang — always
            // fall through to the title.
            if (_splash != null)
                await SafeStage(_splash.Play(), "splash");
            Debug.Log("[TitleController] Arrival: splash stage done.");

            // Stage 2 — cold open (story intro), same guard. CRITICAL: the cold
            // open is a 14-beat cinematic. If SafeStage merely STOPPED AWAITING on
            // timeout, Play()'s beat loop would keep running and render beats ON TOP
            // of the title (the recurring "intro on top" bug). So we pass
            // ForceHide as the on-timeout/exception KILL: SafeStage now CANCELS the
            // cinematic (not just stops awaiting it) the instant it times out, BEFORE
            // proceeding. ForceHide cancels the CTS, blanks + detaches the overlay,
            // and marks finished — so the loop breaks immediately and cannot overlay.
            if (_storyIntro != null)
                await SafeStage(_storyIntro.Play(), "storyIntro", _storyIntro.ForceHide);
            // Belt-and-braces: ForceHide is idempotent — ensure the overlay is down
            // even on the success path (where SafeStage did not need to invoke it).
            if (_storyIntro != null) _storyIntro.ForceHide();
            Debug.Log("[TitleController] Arrival: storyIntro stage done.");

            // Stage 3 — the title screen (always reached now).
            BuildTitleScreen();
            SetTitleVisible(true);
            Debug.Log("[TitleController] Arrival: title screen built + visible.");
        }

        /// <summary>
        /// Awaits an arrival stage but never lets it hang the boot: on timeout or
        /// exception it logs and returns so the title screen is always reached.
        /// (WebGL: the bumper video / async stages can stall indefinitely.)
        ///
        /// ROOT-CAUSE FIX (DEF-211): a bare timeout only STOPS AWAITING the stage —
        /// the underlying loop keeps running. For a cinematic stage that means its
        /// remaining beats render OVER the title. <paramref name="onTimeout"/> is the
        /// authoritative KILL for the stage: when the stage times out or throws, we
        /// invoke it BEFORE returning so the stage is genuinely CANCELLED (CTS
        /// cancelled, overlay torn down), not merely abandoned. Pass
        /// <c>StoryIntroController.ForceHide</c> for the cold-open stage.
        /// </summary>
        private static async UniTask SafeStage(UniTask stage, string name, System.Action onTimeout = null)
        {
            try
            {
                await stage.Timeout(System.TimeSpan.FromSeconds(6f));
            }
            catch (System.Exception e)
            {
                // Cancel the underlying stage NOW — do not just stop awaiting it,
                // or its loop will keep rendering beats over the next screen.
                try { onTimeout?.Invoke(); }
                catch (System.Exception killEx)
                {
                    Debug.LogWarning($"[TitleController] Arrival stage '{name}' kill threw: {killEx.Message}");
                }
                Debug.LogWarning($"[TitleController] Arrival stage '{name}' skipped " +
                                 $"(timeout/exception) — cancelled + proceeding to title. {e.Message}");
            }
        }

        // =====================================================================
        //  Title screen
        // =====================================================================

        /// <summary>
        /// Builds the entire title / hero-select landing IN CODE on a cleared
        /// root — NO UXML element is read by name, so a UXML that fails to render
        /// in the WebGL build (CLAUDE.md §8) cannot break this screen. Layout:
        ///   root (column)
        ///     ├─ dragon stage  (top HALF — heart-wing art, ScaleAndCrop)
        ///     └─ roster panel  (bottom half)
        ///          ├─ title + tagline + CTA
        ///          ├─ card row   (4 hero cards, flex-even, re-flows on resize)
        ///          └─ detail card (selected-hero name / role / blurb)
        ///   + a top-right Connect Wallet button (absolute).
        /// </summary>
        private void BuildTitleScreen()
        {
            if (_titleDocument == null)
            {
                Debug.LogError("[TitleController] No title UIDocument assigned — cannot build the title screen.");
                return;
            }

            _root = _titleDocument.rootVisualElement;
            if (_root == null) return;

            // We own the tree now — wipe whatever the UXML did (or didn't) build.
            _root.Clear();
            // DEF-211: also DETACH the source UXML at runtime so a later layout
            // cycle can never re-instantiate the orphaned TitleScreen.uxml elements
            // back over the code-built landing. (Runtime visualTreeAsset=null —
            // chosen over editing the builder so the kill lives next to the Clear.)
            if (_titleDocument != null) _titleDocument.visualTreeAsset = null;
            _root.style.flexGrow = 1f;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.alignItems = Align.Stretch;
            _root.style.justifyContent = Justify.FlexStart;
            _root.style.backgroundColor = ColBackground;

            _cardHealAttempted = false;   // fresh build — re-arm the self-heal

            BuildDragonStage();   // top half (acceptance #4)
            BuildRosterPanel();   // bottom half — title + 4 cards + detail
            BuildConnectWallet(); // top-right stub button

            // Initial selection state + a re-flow pass once we know the size.
            PreselectFromSave();
            RefreshSelectionVisuals();

            // Re-flow the responsive layout on every screen-size / orientation
            // change — this keeps the four cards evenly centred in BOTH
            // landscape and portrait without fixed pixel positions.
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            _titleBuilt = true;

            // Verify now in case geometry already resolved (don't wait a frame).
            ReflowForSize(_root.resolvedStyle.width, _root.resolvedStyle.height);

            Debug.Log($"[TitleController] DIAG: code-built landing rootChildren={_root.childCount} " +
                      $"cards={(_cardRow != null ? _cardRow.childCount : 0)}");
        }

        // ── Top half: dragon banner ─────────────────────────────────────────
        private void BuildDragonStage()
        {
            _dragonStage = new VisualElement { name = "title-dragon-stage" };
            _dragonStage.style.height = Length.Percent(50f);   // top HALF (acceptance #4)
            _dragonStage.style.flexGrow = 0f;
            _dragonStage.style.flexShrink = 0f;
            _dragonStage.style.flexDirection = FlexDirection.Column;
            _dragonStage.style.justifyContent = Justify.FlexStart;
            _dragonStage.style.alignItems = Align.Center;
            _dragonStage.pickingMode = PickingMode.Ignore;

            // The dragon: the Heart-Wing banner art. Inspector sprite first (set
            // in the editor build), else Resources/heart-wing (WebGL-safe — the
            // code-built landing has no serialized sprite in the player build).
            if (_heartWingBanner != null)
            {
                _dragonStage.style.backgroundImage = new StyleBackground(_heartWingBanner);
                _dragonStage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }
            else
            {
                var dragonTex = Resources.Load<Texture2D>("heart-wing");
                if (dragonTex != null)
                {
                    _dragonStage.style.backgroundImage = new StyleBackground(dragonTex);
                    _dragonStage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                }
                else
                {
                    _dragonStage.style.backgroundColor = new Color(0.05f, 0.035f, 0.11f, 1f);
                    Debug.LogWarning("[TitleController] Resources/heart-wing not found — dragon stage is a flat band.");
                }
            }

            _root.Add(_dragonStage);
        }

        // ── Bottom half: title + roster + detail ────────────────────────────
        private void BuildRosterPanel()
        {
            var roster = new VisualElement { name = "title-roster-panel" };
            roster.style.flexGrow = 1f;          // remaining bottom half
            roster.style.flexShrink = 1f;
            roster.style.flexBasis = 0f;
            roster.style.backgroundColor = ColPanelDark;
            roster.style.flexDirection = FlexDirection.Column;
            roster.style.alignItems = Align.Stretch;
            roster.style.justifyContent = Justify.FlexStart;
            roster.style.paddingTop = 10f;
            roster.style.paddingBottom = 14f;
            roster.style.paddingLeft = 10f;
            roster.style.paddingRight = 10f;

            // Game title — canon string, never hardcoded (port-spec Part 4).
            var gameTitle = new Label(CanonStrings.GameTitle);
            gameTitle.style.fontSize = 28f;
            gameTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            gameTitle.style.color = ColTextBright;
            gameTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameTitle.style.whiteSpace = WhiteSpace.Normal;
            gameTitle.style.flexShrink = 0f;
            roster.Add(gameTitle);

            // Tagline.
            var tagline = new Label(CanonStrings.Tagline);
            tagline.style.marginTop = 2f;
            tagline.style.fontSize = 13f;
            tagline.style.color = ColTextMuted;
            tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
            tagline.style.whiteSpace = WhiteSpace.Normal;
            tagline.style.flexShrink = 0f;
            roster.Add(tagline);

            // CTA — tells the player the cards are tappable.
            var cta = new Label("✦ Select your Hero to begin ✦");
            cta.style.marginTop = 6f;
            cta.style.marginBottom = 6f;
            cta.style.fontSize = 14f;
            cta.style.unityFontStyleAndWeight = FontStyle.Bold;
            cta.style.color = ColAmber;
            cta.style.unityTextAlign = TextAnchor.MiddleCenter;
            cta.style.flexShrink = 0f;
            roster.Add(cta);

            // The four hero cards — an evenly distributed flex row (acceptance #2).
            // DEF-204 ROOT FIX: SpaceBetween + per-card maxWidth:25%/flexBasis:0
            // let the row collapse/wrap in portrait so two heroes shared one card
            // ("Thrain + Grom"). Center + NoWrap forces all four onto ONE centred
            // row that never wraps; equal flex-grow + symmetric margins keep them
            // evenly sized.
            _cardRow = new VisualElement { name = "title-card-row" };
            _cardRow.style.flexDirection = FlexDirection.Row;
            _cardRow.style.justifyContent = Justify.Center; // centred, single row
            _cardRow.style.flexWrap = Wrap.NoWrap;          // never wrap to a 2nd line
            _cardRow.style.alignItems = Align.Stretch;
            _cardRow.style.flexGrow = 1f;
            _cardRow.style.flexShrink = 1f;
            _cardRow.style.alignSelf = Align.Stretch;
            roster.Add(_cardRow);

            BuildCards();

            // Selected-hero play-card details — BELOW the four cards (acceptance #3).
            BuildDetailCard();
            roster.Add(_detailCard);

            _root.Add(roster);
        }

        /// <summary>Builds the four hero cards into the card row.</summary>
        private void BuildCards()
        {
            if (_cardRow == null) return;

            // DEF-211: a broken roster must SCREAM, not silently render an empty /
            // merged card row. Log loudly (do NOT throw) and still build whatever
            // heroes exist so the landing degrades gracefully rather than crashing.
            if (HeroCatalog.Heroes == null || HeroCatalog.Heroes.Length != 4)
            {
                int count = HeroCatalog.Heroes != null ? HeroCatalog.Heroes.Length : 0;
                Debug.LogError($"[TitleController] HERO ROSTER BROKEN — expected 4 heroes, " +
                               $"HeroCatalog.Heroes has {count}. The hero-select row will be wrong.");
            }

            _cardRow.Clear();
            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
            {
                HeroCardInfo info = HeroCatalog.Heroes[i];
                VisualElement card = BuildCard(info);
                _cardRow.Add(card);
                _cards[i] = card;
            }
        }

        /// <summary>
        /// Builds one RICH hero card (matching HeroSelectController's framed card,
        /// owner-preferred 2026-06-03): portrait + name + CLASS TITLE (role) + the
        /// full BLURB inline, all inside the card. Sized with flex (NOT a fixed
        /// pixel width) so the four cards share the row evenly and re-flow in both
        /// orientations.
        /// </summary>
        private VisualElement BuildCard(HeroCardInfo info)
        {
            var card = new VisualElement { name = $"title-hero-card-{info.Hero}" };

            // Even distribution — equal flex-grow, no fixed width / x position.
            card.style.flexGrow = 1f;
            card.style.flexShrink = 1f;
            card.style.flexBasis = 0f;
            card.style.marginLeft = 5f;
            card.style.marginRight = 5f;
            card.style.maxWidth = Length.Percent(25f);   // never wider than its even quarter
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

            // Portrait — owner's character-named art (Thrain/Grom/Sylas/Elara).
            // The character art is tall/portrait; with ScaleAndCrop centred, a
            // square-ish slot crops the face off. Load as a Sprite (Texture2D
            // fallback), ScaleAndCrop, and TOP-anchor so the head / upper body is
            // what shows — matching how HeroSelectController frames image 4.
            var portrait = new VisualElement { name = "portrait" };
            portrait.style.flexGrow = 1f;
            portrait.style.minHeight = 96f;   // taller slot so the head + torso read clearly
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;
            portrait.style.backgroundColor = new Color(0.486f, 0.227f, 0.929f, 0.10f);
            portrait.pickingMode = PickingMode.Ignore;

            string slug = SlugFor(info.Hero);
            FramePortrait(portrait, slug, info);
            card.Add(portrait);

            // Element-coloured accent strip.
            var accent = new VisualElement();
            accent.style.height = 4f;
            accent.style.flexShrink = 0f;
            accent.style.backgroundColor = info.Accent;
            accent.pickingMode = PickingMode.Ignore;
            card.Add(accent);

            // ── Rich copy block (name + class title + blurb), inside the card ──
            var copy = new VisualElement { name = "card-copy" };
            copy.style.flexShrink = 0f;
            copy.style.paddingTop = 6f;
            copy.style.paddingBottom = 8f;
            copy.style.paddingLeft = 6f;
            copy.style.paddingRight = 6f;
            copy.pickingMode = PickingMode.Ignore;

            // Hero name.
            var nameLabel = new Label(CanonStrings.Locale(info.NameKey));
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.fontSize = 14f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = ColTextBright;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            nameLabel.style.flexShrink = 0f;
            nameLabel.pickingMode = PickingMode.Ignore;
            copy.Add(nameLabel);

            // Class title (role) — Frostweaver / Lightbearer / Wood Warden / Divine Healer.
            var roleLabel = new Label(CanonStrings.Locale(info.RoleKey));
            roleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            roleLabel.style.marginTop = 1f;
            roleLabel.style.fontSize = 11f;
            roleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            roleLabel.style.color = info.Accent;
            roleLabel.style.whiteSpace = WhiteSpace.Normal;
            roleLabel.style.flexShrink = 0f;
            roleLabel.pickingMode = PickingMode.Ignore;
            copy.Add(roleLabel);

            // Full blurb INLINE in the card.
            var blurbLabel = new Label(CanonStrings.Locale(info.BlurbKey));
            blurbLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            blurbLabel.style.marginTop = 5f;
            blurbLabel.style.fontSize = 11f;
            blurbLabel.style.color = ColTextMuted;
            blurbLabel.style.whiteSpace = WhiteSpace.Normal;
            blurbLabel.style.flexShrink = 1f;
            blurbLabel.pickingMode = PickingMode.Ignore;
            copy.Add(blurbLabel);

            card.Add(copy);

            // Whole card is the hit target — tap selects (and shows the detail).
            HeroClass captured = info.Hero;
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<PointerEnterEvent>(_ =>
                card.style.scale = new StyleScale(new Scale(new Vector3(1.04f, 1.04f, 1f))));
            card.RegisterCallback<PointerLeaveEvent>(_ =>
                card.style.scale = new StyleScale(new Scale(Vector3.one)));
            card.RegisterCallback<PointerDownEvent>(_ => OnCardClicked(captured));

            return card;
        }

        /// <summary>
        /// Frames a hero portrait into its slot so the character FILLS the slot
        /// cleanly with the head / upper body showing — never centre-cropping the
        /// face off a tall portrait. Loads as a Sprite first (matching
        /// HeroSelectController, which the owner prefers), Texture2D fallback, then
        /// a glyph fallback. Uses ScaleAndCrop + a TOP background anchor.
        /// </summary>
        private static void FramePortrait(VisualElement portrait, string slug, HeroCardInfo info)
        {
            if (portrait == null) return;

            var sprite = Resources.Load<Sprite>($"HeroPortraits/{slug}");
            if (sprite != null)
            {
                portrait.style.backgroundImage = new StyleBackground(sprite);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                AnchorPortraitTop(portrait);
                return;
            }

            var tex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
            if (tex != null)
            {
                portrait.style.backgroundImage = new StyleBackground(tex);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                AnchorPortraitTop(portrait);
                return;
            }

            // No art on disk — element-coloured glyph fallback.
            var glyph = new Label(info.Glyph);
            glyph.style.fontSize = 44f;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.color = info.Accent;
            glyph.pickingMode = PickingMode.Ignore;
            portrait.Add(glyph);
        }

        /// <summary>
        /// Anchors a ScaleAndCrop background to the TOP of the slot so a tall
        /// character portrait shows its head / upper body rather than centre-
        /// cropping the face off. unityBackgroundPositionY exists on Unity 2022.2+;
        /// guarded in try/catch so an older UI Toolkit silently keeps centred crop.
        /// </summary>
        private static void AnchorPortraitTop(VisualElement portrait)
        {
            if (portrait == null) return;
            try
            {
                portrait.style.backgroundPositionY =
                    new BackgroundPosition(BackgroundPositionKeyword.Top);
            }
            catch (System.Exception)
            {
                // Older UI Toolkit without BackgroundPosition — keep centred crop.
            }
        }

        /// <summary>
        /// Builds the slim confirm BAR that sits BELOW the four rich cards. Since
        /// each card now carries its own class title + full blurb inline (the
        /// owner-preferred rich frame), this bar no longer repeats the blurb — it
        /// just confirms the current pick: a "you chose X" line + the "Choose this
        /// Hero" CTA that persists the pick and routes onward.
        /// </summary>
        private void BuildDetailCard()
        {
            _detailCard = new VisualElement { name = "title-hero-detail-card" };
            _detailCard.style.marginTop = 10f;
            _detailCard.style.flexShrink = 0f;
            _detailCard.style.alignSelf = Align.Stretch;
            _detailCard.style.flexDirection = FlexDirection.Row;
            _detailCard.style.alignItems = Align.Center;
            _detailCard.style.justifyContent = Justify.SpaceBetween;
            _detailCard.style.paddingTop = 8f;
            _detailCard.style.paddingBottom = 8f;
            _detailCard.style.paddingLeft = 14f;
            _detailCard.style.paddingRight = 14f;
            float r = 12f;
            _detailCard.style.borderTopLeftRadius = r;
            _detailCard.style.borderTopRightRadius = r;
            _detailCard.style.borderBottomLeftRadius = r;
            _detailCard.style.borderBottomRightRadius = r;
            SetBorderWidth(_detailCard, 1f);
            SetBorderColor(_detailCard, ColVioletBorder);
            _detailCard.style.backgroundColor = new Color(0.149f, 0.102f, 0.204f, 0.85f);

            // "Selected: <name> — <class title>" status line (left side).
            _detailName = new Label(string.Empty);
            _detailName.style.flexShrink = 1f;
            _detailName.style.fontSize = 15f;
            _detailName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailName.style.color = ColTextBright;
            _detailName.style.whiteSpace = WhiteSpace.Normal;
            _detailCard.Add(_detailName);

            // Kept (assigned in RefreshDetailCard) but not added to the tree — the
            // role + blurb now live inline in each card, so the bar stays slim.
            _detailRole = new Label(string.Empty);
            _detailBlurb = new Label(string.Empty);

            // Confirm CTA — persists the chosen hero and routes onward to PetSelect.
            var confirm = new Button(OnChooseHeroClicked) { text = "Choose this Hero →", name = "title-choose-hero" };
            confirm.style.marginLeft = 10f;
            confirm.style.flexShrink = 0f;
            confirm.style.height = 42f;
            confirm.style.minWidth = 180f;
            confirm.style.fontSize = 15f;
            confirm.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetBorderWidth(confirm, 0f);
            float br = 10f;
            confirm.style.borderTopLeftRadius = br;
            confirm.style.borderTopRightRadius = br;
            confirm.style.borderBottomLeftRadius = br;
            confirm.style.borderBottomRightRadius = br;
            confirm.style.backgroundColor = ColAmber;
            confirm.style.color = new Color(0.086f, 0.055f, 0.024f, 1f);
            _detailCard.Add(confirm);
        }

        // ── Connect Wallet — Week-1 stub, pinned top-right ──────────────────
        private void BuildConnectWallet()
        {
            _connectWalletButton = new Button(OnConnectWalletClicked) { text = "Connect Wallet", name = "title-connect-wallet" };
            _connectWalletButton.style.position = Position.Absolute;
            _connectWalletButton.style.top = 16f;
            _connectWalletButton.style.right = 16f;
            _connectWalletButton.style.left = StyleKeyword.Auto;
            _connectWalletButton.style.bottom = StyleKeyword.Auto;
            _connectWalletButton.style.height = 36f;
            _connectWalletButton.style.fontSize = 13f;
            SetBorderWidth(_connectWalletButton, 1f);
            SetBorderColor(_connectWalletButton, ColVioletBorder);
            _connectWalletButton.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            _connectWalletButton.style.color = ColTextBright;

            // DEF-204 ROOT FIX (paint order): older UI Toolkit has no z-index, so an
            // absolute element paints in tree order. Add the Connect-Wallet button
            // LAST to _root (after the dragon stage + roster panel) so it always
            // paints on top and is never clipped by / hidden under the roster
            // content or the Skip overlay.
            if (_connectWalletButton.parent == _root)
                _connectWalletButton.RemoveFromHierarchy();
            _root.Add(_connectWalletButton);
        }

        // =====================================================================
        //  Selection
        // =====================================================================

        /// <summary>
        /// A hero card was tapped — mark it active, refresh the detail card, and
        /// ARM the auto-advance (DEF-230: selection IS the commit). Re-tapping a
        /// different card before the beat elapses simply re-selects and re-arms,
        /// so a mis-tap is recoverable. Once the route has fired (_advancing) we
        /// ignore further taps.
        /// </summary>
        private void OnCardClicked(HeroClass hero)
        {
            if (_advancing) return;   // route already in flight — ignore late taps
            _selectedHero = hero;
            _hasSelection = true;
            RefreshSelectionVisuals();

            // Arm (or re-arm) the brief confirm beat — selecting auto-advances.
            _autoAdvanceArmed = true;
            _autoAdvanceAt = Time.unscaledTime + AutoAdvanceDelay;
        }

        /// <summary>Pre-selects the hero the save already records, if any.</summary>
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

        /// <summary>Marks the active card, clears the rest, and refreshes the detail card.</summary>
        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                bool active = _hasSelection && HeroCatalog.Heroes[i].Hero == _selectedHero;
                SetBorderColor(_cards[i], active ? ColAmber : ColVioletBorder);
                _cards[i].style.backgroundColor = active ? ColCardActive : ColCardIdle;
            }
            RefreshDetailCard();
        }

        /// <summary>
        /// Updates the slim confirm bar's status line for the current pick. The
        /// full role + blurb live inline in each rich card, so this bar only shows
        /// a short "Selected: Name — Class Title" cue next to the Choose CTA.
        /// </summary>
        private void RefreshDetailCard()
        {
            if (_detailCard == null) return;
            if (!_hasSelection)
            {
                if (_detailName != null) _detailName.text = "Tap a hero to choose";
                return;
            }
            HeroCardInfo info = FindInfo(_selectedHero);
            if (info == null) return;
            if (_detailName != null)
            {
                string name = CanonStrings.Locale(info.NameKey);
                string role = CanonStrings.Locale(info.RoleKey);
                _detailName.text = string.IsNullOrEmpty(role)
                    ? $"Selected: {name}"
                    : $"Selected: {name} — {role}";
            }
            // _detailRole / _detailBlurb intentionally not displayed (inline in cards).
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
        /// dragon stage is always the top half; the four cards always share the
        /// row evenly (SpaceBetween + equal flex-grow). No fixed x positions —
        /// purely flex — so it re-centres identically in landscape and portrait.
        /// </summary>
        private void ReflowForSize(float width, float height)
        {
            if (!_titleBuilt || _cardRow == null) return;
            if (width <= 0f || height <= 0f) return;

            bool portrait = height >= width;
            float gutter = portrait ? 4f : 6f;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].style.marginLeft = gutter;
                _cards[i].style.marginRight = gutter;
            }

            // DEF-204 FIX 4: scale the detail card's top gap with orientation so the
            // confirm bar's spacing reads correctly in both portrait (tighter) and
            // landscape (looser).
            if (_detailCard != null)
                _detailCard.style.marginTop = portrait ? 6f : 10f;

            VerifyFourCardsEven();
        }

        /// <summary>
        /// Regression guard that now SELF-HEALS (DEF-204): asserts the four hero
        /// cards exist and are laid out as an even row (equal flex-grow, no fixed
        /// widths). If the card COUNT is wrong it logs an error AND rebuilds the row
        /// once (_cardHealAttempted guards against infinite recursion) so a
        /// malformed layout auto-corrects in place rather than shipping broken — the
        /// recurring-regression sentinel that now also repairs itself.
        /// </summary>
        private void VerifyFourCardsEven()
        {
            int expected = HeroCatalog.Heroes.Length;
            int actual = _cardRow != null ? _cardRow.childCount : 0;
            if (actual != expected)
            {
                Debug.LogError($"[TitleController] CARD ASSERT FAILED — expected {expected} " +
                               $"hero cards, found {actual}. The hero row is not evenly built.");

                // Self-heal once: rebuild the card row and re-apply selection
                // visuals. The guard flag stops this from looping forever if the
                // rebuild still cannot produce the expected count.
                if (_cardRow != null && !_cardHealAttempted)
                {
                    _cardHealAttempted = true;
                    Debug.LogError("[TitleController] CARD ASSERT — auto-healing: rebuilding the card row.");
                    _cardRow.Clear();
                    BuildCards();
                    RefreshSelectionVisuals();
                }
                return;
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null)
                {
                    Debug.LogError($"[TitleController] CARD ASSERT FAILED — hero card {i} is null.");
                    return;
                }
                if (_cards[i].resolvedStyle.flexGrow <= 0f)
                {
                    Debug.LogError($"[TitleController] CARD ASSERT FAILED — hero card {i} " +
                                   "has no flex-grow; the row will not distribute evenly.");
                    return;
                }
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

        private void OnDisable()
        {
            if (_connectWalletButton != null) _connectWalletButton.clicked -= OnConnectWalletClicked;
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        // ── Choose Hero — persist the pick and route onward to PetSelect ─────
        // Kept as a belt-and-braces second path; the canonical flow now
        // auto-advances on card select (DEF-230), so this button is optional.
        private void OnChooseHeroClicked()
        {
            if (!_hasSelection)
            {
                Debug.Log("[TitleController] Choose-hero pressed with no selection — ignored.");
                return;
            }
            if (_advancing) return;   // auto-advance already routing
            _advancing = true;
            RouteToPetSelect();
        }

        /// <summary>Auto-advance entry (DEF-230): selection committed by the beat.</summary>
        private void AdvanceToPetSelect()
        {
            if (!_hasSelection) { _advancing = false; return; }
            RouteToPetSelect();
        }

        /// <summary>Persists the chosen hero and loads PetSelect (single route path).</summary>
        private void RouteToPetSelect()
        {
            Debug.Log("[TitleController] Hero confirmed: " + _selectedHero + " — routing to PetSelect.");
            var svc = GameStateService.Instance;
            if (svc != null) svc.ChooseHero(_selectedHero);
            SceneRouter.GoPetSelect();
        }

        // ── Connect Wallet — Week-1 stub (the real flow is the Week-7 module) ─
        private void OnConnectWalletClicked()
        {
            Debug.Log("[TitleController] Connect Wallet CLICK RECEIVED — stub (Week-7 Wallet module ships the real flow).");
        }

        // ── Visibility helper ────────────────────────────────────────────────
        private void SetTitleVisible(bool visible)
        {
            var root = _titleDocument != null ? _titleDocument.rootVisualElement : _root;
            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
