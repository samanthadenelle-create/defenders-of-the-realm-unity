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
//   Start button          — enters the intro flow via SceneRouter.GoHeroSelect
//                            (Title -> HeroSelect -> PetSelect -> Village).
//
// async UniTask for the arrival flow — never async void (port-spec Part 3).
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

        // ── Resolved title-screen elements ───────────────────────────────────
        private VisualElement _root;
        private Button _startButton;
        private Button _connectWalletButton;

        // Panel-render diagnostic (temporary).
        private bool _titleBuilt;
        private int _diagFrames;

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

            if (_startButton != null)
                Debug.Log($"[TitleController] PANELDIAG start-button worldBound=" +
                          $"{_startButton.worldBound} picking={_startButton.pickingMode} " +
                          $"enabled={_startButton.enabledSelf} display={_startButton.style.display}");
        }

        /// <summary>
        /// Runs the full first-launch arrival sequence, then reveals the title
        /// screen. Each stage is awaited so the next only starts when the
        /// previous has fully faded out.
        /// </summary>
        private async UniTask RunArrival()
        {
            Debug.Log("[TitleController] Arrival: start.");

            // Stage 1 — studio bumper.
            if (_splash != null)
            {
                Debug.Log("[TitleController] Arrival: awaiting splash.Play() ...");
                await _splash.Play();
            }
            Debug.Log("[TitleController] Arrival: splash stage done.");

            // Stage 2 — cold open (the controller's own gate decides whether
            // it actually plays or returns immediately for a returning save).
            if (_storyIntro != null)
            {
                Debug.Log("[TitleController] Arrival: awaiting storyIntro.Play() ...");
                await _storyIntro.Play();
            }
            Debug.Log("[TitleController] Arrival: storyIntro stage done.");

            // Stage 3 — the title screen.
            BuildTitleScreen();
            SetTitleVisible(true);
            Debug.Log("[TitleController] Arrival: title screen built + visible.");
        }

        // =====================================================================
        //  Title screen
        // =====================================================================

        /// <summary>
        /// Binds the title-screen document — fills every canon string from
        /// <see cref="CanonStrings"/>, assigns the Heart-Wing banner, and wires
        /// the two buttons.
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

            // ── Canon strings — never hardcoded (v2 port-spec Part 4) ────────
            var overline = _root.Q<Label>("overline");
            if (overline != null) overline.text = string.Empty; // DEF-16: overline was duplicating the tagline

            var gameTitle = _root.Q<Label>("game-title");
            if (gameTitle != null) gameTitle.text = CanonStrings.GameTitle;

            var tagline = _root.Q<Label>("tagline");
            if (tagline != null) tagline.text = CanonStrings.Tagline;

            var studioCredit = _root.Q<Label>("studio-credit");
            if (studioCredit != null)
                studioCredit.text = $"Published by {CanonStrings.Publisher}";

            // ── Heart-Wing banner ────────────────────────────────────────────
            var banner = _root.Q<VisualElement>("heart-wing-banner");
            if (banner != null && _heartWingBanner != null)
                banner.style.backgroundImage = new StyleBackground(_heartWingBanner);

            // ── Hero trio — wizard left, knight centre, archer right (2026-05-20).
            // Pulls the same Resources/HeroPortraits/ PNGs used on HeroSelect.
            var flankL = _root.Q<VisualElement>("hero-flank-left");
            var flankC = _root.Q<VisualElement>("hero-flank-center");
            var flankR = _root.Q<VisualElement>("hero-flank-right");
            var wizardTex = Resources.Load<Texture2D>("HeroPortraits/mage");
            var knightTex = Resources.Load<Texture2D>("HeroPortraits/knight");
            var archerTex = Resources.Load<Texture2D>("HeroPortraits/ranger");
            if (flankL != null && wizardTex != null)
                flankL.style.backgroundImage = new StyleBackground(wizardTex);
            if (flankC != null && knightTex != null)
                flankC.style.backgroundImage = new StyleBackground(knightTex);
            if (flankR != null && archerTex != null)
                flankR.style.backgroundImage = new StyleBackground(archerTex);

            // Owner ask 2026-05-20: "hero select screen cannot select hero" —
            // make the three Title flanks themselves the hero pickers so the
            // player can choose straight from the title. Each click writes the
            // hero choice via GameStateService.ChooseHero and routes onward.
            WireFlankAsHeroPicker(flankL, HeroClass.Mage);
            WireFlankAsHeroPicker(flankC, HeroClass.Knight);
            WireFlankAsHeroPicker(flankR, HeroClass.Ranger);

            // ── Buttons ──────────────────────────────────────────────────────
            // Owner direction 2026-05-20: Start button is no longer needed
            // (the three hero flanks ARE the start trigger now). Hide it,
            // and reposition Connect Wallet to the top-right so the flanks
            // aren't crowded by overlapping UI.
            _startButton = _root.Q<Button>("start-button");
            if (_startButton != null)
            {
                _startButton.style.display = DisplayStyle.None;
                _startButton.clicked += OnStartClicked; // still wired for keyboard fallback
            }

            _connectWalletButton = _root.Q<Button>("connect-wallet-button");
            if (_connectWalletButton != null)
            {
                _connectWalletButton.style.position = Position.Absolute;
                _connectWalletButton.style.top = 16;
                _connectWalletButton.style.right = 16;
                _connectWalletButton.style.left = StyleKeyword.Auto;
                _connectWalletButton.clicked += OnConnectWalletClicked;
            }

            // Add a clear "Select your Hero to begin" call-to-action below
            // the title heading so the player knows the flanks are clickable.
            AddHeroCallToAction(_root);

            // Diagnostic: confirm the title document loaded and its theme tokens
            // resolved. childCount + the title-root lookup show the UXML loaded;
            // the GeometryChangedEvent fires after layout (when resolvedStyle is
            // valid) — a dark backgroundColor means the Theme.uss tokens
            // resolved, a transparent one means they did not.
            var titleRoot = _root.Q("title-root");
            Debug.Log($"[TitleController] DIAG: rootChildren={_root.childCount} " +
                      $"sheets={_root.styleSheets.count} titleRootFound={titleRoot != null}");
            if (titleRoot != null)
                titleRoot.RegisterCallback<GeometryChangedEvent>(_ => Debug.Log(
                    $"[TitleController] DIAG: title-root resolved bg = {titleRoot.resolvedStyle.backgroundColor}"));

            _titleBuilt = true;
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= OnStartClicked;
            if (_connectWalletButton != null) _connectWalletButton.clicked -= OnConnectWalletClicked;
        }

        /// <summary>
        /// Inserts a "Select your Hero to begin" call-to-action below the
        /// title heading. Idempotent — re-runs of OnEnable don't stack.
        /// </summary>
        private static void AddHeroCallToAction(VisualElement root)
        {
            if (root == null) return;
            var existing = root.Q<Label>("title-cta");
            if (existing != null) return;
            var cta = new Label("✦ Select your Hero to begin ✦") { name = "title-cta" };
            cta.style.position = Position.Absolute;
            cta.style.top = Length.Percent(33);
            cta.style.left = 0;
            cta.style.right = 0;
            cta.style.unityTextAlign = TextAnchor.MiddleCenter;
            cta.style.fontSize = 20;
            cta.style.color = new StyleColor(new Color(1f, 0.86f, 0.55f, 1f));
            cta.style.unityFontStyleAndWeight = FontStyle.Bold;
            cta.pickingMode = PickingMode.Ignore;
            root.Add(cta);
        }

        /// <summary>
        /// Wires one of the three title flanks (wizard / knight / archer) to
        /// act as a direct hero picker. Click → write the choice to
        /// GameStateService.ChooseHero and route to PetSelect, skipping the
        /// HeroSelect screen entirely. Visual cue (cursor + slight hover lift)
        /// applied so the player can tell it's interactive.
        /// </summary>
        private static void WireFlankAsHeroPicker(VisualElement flank, HeroClass hero)
        {
            if (flank == null) return;
            flank.pickingMode = PickingMode.Position;
            flank.RegisterCallback<PointerEnterEvent>(_ =>
            {
                flank.style.scale = new StyleScale(new Scale(new Vector3(1.04f, 1.04f, 1f)));
            });
            flank.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                flank.style.scale = new StyleScale(new Scale(Vector3.one));
            });
            flank.RegisterCallback<ClickEvent>(_ =>
            {
                Debug.Log("[TitleController] Flank-click picked hero: " + hero);
                var svc = GameStateService.Instance;
                if (svc != null) svc.ChooseHero(hero);
                SceneRouter.GoPetSelect();
            });
        }

        // ── Start — enter the intro flow (hero-select -> pet-select -> village) ─
        private void OnStartClicked()
        {
            Debug.Log("[TitleController] Start CLICK RECEIVED — routing to HeroSelect.");
            // The intro flow runs Title -> HeroSelect -> PetSelect -> Village.
            // HeroSelectController self-skips to the village for a returning
            // player whose save already records a hero + starter pet, so it is
            // always safe to route here.
            SceneRouter.GoHeroSelect();
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
