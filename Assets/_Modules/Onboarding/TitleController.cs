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
//   Start button          — loads the Village scene via SceneRouter.
//
// async UniTask for the arrival flow — never async void (port-spec Part 3).
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
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

        private void OnEnable()
        {
            // Hide the title screen until the arrival sequence has finished, so
            // the player does not see it flash behind the bumper / cold open.
            if (_titleDocument != null)
                SetTitleVisible(false);
        }

        private void Start()
        {
            // Fire-and-forget the arrival flow. RunArrival is a UniTask (not
            // async void); Forget() is the sanctioned way to launch a top-level
            // UniTask from a Unity lifecycle hook.
            RunArrival().Forget();
        }

        /// <summary>
        /// Runs the full first-launch arrival sequence, then reveals the title
        /// screen. Each stage is awaited so the next only starts when the
        /// previous has fully faded out.
        /// </summary>
        private async UniTask RunArrival()
        {
            // Stage 1 — studio bumper.
            if (_splash != null)
                await _splash.Play();

            // Stage 2 — cold open (the controller's own gate decides whether
            // it actually plays or returns immediately for a returning save).
            if (_storyIntro != null)
                await _storyIntro.Play();

            // Stage 3 — the title screen.
            BuildTitleScreen();
            SetTitleVisible(true);
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
            if (overline != null) overline.text = CanonStrings.Tagline;

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

            // ── Buttons ──────────────────────────────────────────────────────
            _startButton = _root.Q<Button>("start-button");
            if (_startButton != null)
                _startButton.clicked += OnStartClicked;

            _connectWalletButton = _root.Q<Button>("connect-wallet-button");
            if (_connectWalletButton != null)
                _connectWalletButton.clicked += OnConnectWalletClicked;
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= OnStartClicked;
            if (_connectWalletButton != null) _connectWalletButton.clicked -= OnConnectWalletClicked;
        }

        // ── Start — go to the Village scene ──────────────────────────────────
        private void OnStartClicked()
        {
            SceneRouter.GoVillage();
        }

        // ── Connect Wallet — Week-1 stub (the real flow is the Week-7 module) ─
        private void OnConnectWalletClicked()
        {
            Debug.Log("[TitleController] Connect Wallet tapped — stub. The Solana wallet flow ships with the Week-7 Wallet module.");
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
