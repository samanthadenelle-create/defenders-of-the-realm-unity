// =============================================================================
// LoginPanelController (WO-769) — the email/password sign-in surface, plus a
// "Play as Guest" escape so the build always lets a player in (owner's clean-build
// guarantee). Presented at boot before the hub, modeled on FoundingChoiceController.
// WO-847 (owner ruling 2026-08-02): the surface is PLATFORM-CONDITIONAL - on
// Android/Seeker it is WALLET-FIRST ("connect wallet or play as guest": gold
// Connect Wallet primary + Play as Guest, NO email form, NO forgot-password);
// desktop/WebGL keep the WO-787/845 email layout. Split resolved by
// LoginSurfacePlatform (testable static override); wallet connect routes through
// LoginWalletBridge -> WalletSkinBootstrap -> WalletService.Connect with an
// EXPLICIT GameStateService.BindWallet (never skin-config-gated on this path).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Onboarding   Namespace: DeNelle.Onboarding (references DeNelle.Core only).
// UI: code-built uGUI on the Obsidian kit (ElarionUiKit) — its own overlay canvas,
// NO UXML/UIDocument (CLAUDE.md §8). Colour never carries meaning (each control labelled).
// PRESENTATION ONLY: all auth logic lives in DeNelle.Core.Auth.FirebaseAuthService; this
// file builds UI + calls the service. Guest = the existing device-hash fallback
// (GameStateService.EnsureAccount mints guest-local-* on load), so guest just continues.
//
// RUNTIME GATE: email/password sign-in needs the Email/Password provider ENABLED in the
// Firebase console — until then SignIn/SignUp surface "operation not allowed" here; Guest
// always works.
//
// SOFTLOCK LAW (security audit 2026-08-02, BINDING): this is the FIRST screen an Android
// tester sees and there is no way past it except through this file. Two invariants keep
// it from becoming a kill-the-app dead end:
//   1. "Play as Guest" is NEVER disabled. SetBusy locks every OTHER control; the escape
//      hatch stays live for the whole busy window. Previously SetBusy(true) disabled it
//      too, so an unanswered wallet handshake (no wallet app installed, or the player
//      backgrounded the wallet and came back) left the screen on "Opening your wallet..."
//      with EVERY button dead.
//   2. Every await on this surface is TIME-BOUNDED. The wallet connect await gets a
//      35s ceiling here on top of WalletService's own 30s provider ceiling, so the UI
//      un-busies and tells the truth even if the wallet layer below is rewritten.
// =============================================================================

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Obsidian email/password login + Play-as-Guest. Call <see cref="Present"/> with
    /// the "enter the game" continuation; the panel invokes it once the player signs in,
    /// creates an account, or chooses guest.
    /// </summary>
    public sealed class LoginPanelController : MonoBehaviour
    {
        private readonly LoginViewModel _vm = LoginViewModel.CreateDefault();

        private Action _onContinue;
        private bool _routed;
        private GameObject _canvas;
        private PanelHandle _panelHandle;

        private TMP_InputField _email;
        private TMP_InputField _password;
        private TextMeshProUGUI _status;
        private Button _signIn;
        private Button _createAccount;
        private Button _google;
        private Button _guest;
        private Button _forgot;
        private Button _connectWallet;
        private bool _busy;

        /// <summary>
        /// Show the login surface, then run <paramref name="onContinue"/> once the player
        /// gets in (sign-in / create / guest). Always presents; the caller decides when to
        /// call it in the boot flow.
        /// </summary>
        public static void Present(Action onContinue)
        {
            using var _ = FlowTrace.Enter("Auth", "LoginPanelController.Present");
            var host = new GameObject("LoginUI");
            var ctrl = host.AddComponent<LoginPanelController>();
            ctrl._onContinue = onContinue;
            ctrl.Build();
            FlowTrace.Step("Auth", "login panel presented (layout=" + ctrl._vm.Layout + ").");
        }

        /// <summary>
        /// THE GATE DECISION, pure and testable (2026-08-18 defect: the owner's wallet
        /// auto-resumed at boot and the SIGN IN panel was presented anyway ~5s later —
        /// device capture 20:21:38 "auto-resume SUCCEEDED", 20:21:43 LoginPanelController.Build).
        /// <para>
        /// ROOT CAUSE (fixed here): this gate read ONE source — <c>FirebaseAuthService.IsSignedIn</c>
        /// — while the identity that actually keys this game is the WALLET (see the identity law in
        /// <see cref="HandleOutcome"/>: "Firebase = ACCESS, the wallet = DATA identity"). A player who
        /// has only ever connected a wallet is not Firebase-signed-in, so the gate presented the
        /// login surface on TOP of an already-connected, already-bound session.
        /// </para>
        /// <para>
        /// <paramref name="walletIdentityBound"/> is the RACE-PROOF half: it comes from the persisted
        /// save + this device's attestation, so it is true SYNCHRONOUSLY at boot — before any silent
        /// reconnect finishes. That is why this fix needs no delay, no timeout and no extra await.
        /// <paramref name="walletConnected"/> is the live published state for the in-session case.
        /// </para>
        /// <para>FIRST RUN IS PRESERVED: a genuine first run has no connected wallet, no attested
        /// bound identity (its save key is the <c>guest-local-</c> device hash, which is not
        /// cloud-identity-shaped) and no Firebase session — all three inputs are false, so this
        /// returns false and the panel presents. That is its one legitimate purpose.</para>
        /// </summary>
        public static bool ShouldContinueWithoutLogin(bool walletConnected, bool walletIdentityBound,
                                                      bool firebaseSignedIn)
            => walletConnected || walletIdentityBound || firebaseSignedIn;

        /// <summary>
        /// Init-aware boot entry: if the player is ALREADY IN — a connected wallet, an
        /// attested wallet-bound save, or a cached Firebase session — continue straight
        /// through; otherwise present the login-or-guest surface. Safe if Firebase init
        /// fails — falls through to the panel, where Play-as-Guest always works, so the
        /// boot flow can never lock.
        /// <para>CORRECTED 2026-08-18: this doc used to say "already signed in (Firebase caches the
        /// session)" and the code matched it — Firebase-only. On a wallet-first Android build that
        /// is the wrong source, and it re-prompted a player whose wallet had just auto-resumed. The
        /// decision now lives in <see cref="ShouldContinueWithoutLogin"/>.</para>
        /// </summary>
        public static async void PresentOrContinue(Action onContinue)
        {
            bool signedIn = false;
            try
            {
                // SOFTLOCK LAW: BOUNDED. This await happens BEFORE any login UI exists, so a
                // hang here is the WORST softlock on the surface - not "stuck on the sign-in
                // screen" but "no sign-in screen ever appears". Firebase's
                // CheckAndFixDependenciesAsync talks to Play services and has no ceiling of
                // its own; on a device with a stale/updating Play services it can sit there.
                // On expiry we simply present the panel, where Guest always works.
                bool ready = await FirebaseAuthService.Instance.EnsureInitializedAsync()
                    .AsUniTask().Timeout(TimeSpan.FromSeconds(InitTimeoutSeconds), DelayType.UnscaledDeltaTime);
                signedIn = ready && FirebaseAuthService.Instance.IsSignedIn;
            }
            catch (TimeoutException)
            {
                FlowTrace.Fail("Auth",
                    $"Firebase init did not answer within {InitTimeoutSeconds}s - presenting the login " +
                    "surface anyway so the player is never left with no screen at all.");
            }
            catch (Exception e) { FlowTrace.Warn("Auth", "login init check threw: " + e.Message); }

            // The WALLET is the data identity, so it is sampled here and not assumed:
            //   * walletConnected      - live published state (CurrencySkinResolver.PublishWalletConnected,
            //                            raised by the Wallet assembly on connect AND on silent auto-resume);
            //   * walletIdentityBound  - the persisted, provider-ATTESTED save key. Available with no
            //                            await, which is what makes the boot-time resume race a non-event.
            bool walletConnected = false, walletIdentityBound = false;
            Guard.Try("Auth", "sample wallet state for the login gate", () =>
            {
                walletConnected = CurrencySkinResolver.IsWalletConnected;
                var svc = GameStateService.Instance;
                walletIdentityBound = svc != null && svc.HasAttestedWalletIdentity;
            });

            bool continueIn = ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, signedIn);

            // §1.4b: the decision AND every input it was made from, so the next reader never has to
            // guess WHY the panel appeared. A trace that cannot report the wrong outcome is decoration.
            FlowTrace.Step("Auth",
                "login gate decision=" + (continueIn ? "CONTINUE" : "PRESENT") +
                " (walletConnected=" + walletConnected +
                " wallet=" + (walletConnected ? CurrencySkinResolver.ConnectedWalletShortAddress : "none") +
                ", walletIdentityBound=" + walletIdentityBound +
                ", firebaseSignedIn=" + signedIn + ").");

            if (continueIn)
            {
                onContinue?.Invoke();
                return;
            }
            Present(onContinue);
        }

        // =====================================================================
        //  Overlay construction (code-built uGUI on the Obsidian kit)
        // =====================================================================
        private void Build()
        {
            using var _ = FlowTrace.Enter("Auth", "LoginPanelController.Build (uGUI Obsidian)");

            _canvas = ElarionUiKit.BuildModalCanvas("LoginCanvas", 31000);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_canvas, gameObject.scene);

            var scrim = ElarionUiKit.AddImage(_canvas.transform, "Scrim",
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.72f), rounded: false);
            var scrimImg = scrim.GetComponent<Image>();
            if (scrimImg != null) scrimImg.raycastTarget = true;

            // Forced surface (no dismiss X -- guest is the escape). withBackdrop:false; scrim dims.
            // WO-787: taller rect (y 0.06-0.94, was 0.14-0.86) -- the stack is 7-8 rows of
            // MinTouchPx-floored controls; on the shortest live canvas (post-scale height ~970,
            // landscape web / Seeker) the old rect cannot hold them without the touch floor
            // forcing overlap ("stacked", owner screenshot 2026-07-30).
            var chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform, "SIGN IN",
                new Vector2(0.10f, 0.06f), new Vector2(0.90f, 0.94f), onClose: null,
                withBackdrop: false);
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);

            // WO-787 Part A: lay out on the FULL-rect chrome.content, NOT chrome.layout.body.
            // BuildObsidianPanel (WO-714 P6) raises Zone_Body's floor by the close-band +
            // footer reservation (body.y up to ~0.45) to clear the shared Close -- but this
            // panel HIDES its Close, so the reservation only compressed the stack until every
            // fraction slot fell below the MinTouchPx floor and the rows overlapped.
            // Fractions below are clamp-aware: adjacent button centers sit >= 112 reference px
            // apart on the shortest live canvas, so ClampMinTouch can grow rows collision-free.
            Transform body = chrome.content.transform;

            // WO-847 (owner ruling 2026-08-02): on Android/Seeker the login surface is
            // WALLET-FIRST - "connect wallet or play as guest" - no email form, no
            // forgot-password (the wallet is its own recovery). Desktop/WebGL keep the
            // WO-787/845 email layout untouched. Resolved through the platform seam
            // (LoginSurfacePlatform) so tests and headless captures can pin either.
            if (_vm.Layout == LoginSurfaceLayout.WalletFirst) BuildWalletFirst(body);
            else BuildEmailForm(body);

            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Login", Continue, () => !_routed && _canvas != null);
            PanelManager.NotifyOpened(_panelHandle);
        }

        // =====================================================================
        //  WO-847 wallet-first layout (Android/Seeker): Connect Wallet + Guest
        // =====================================================================
        private void BuildWalletFirst(Transform body)
        {
            // ONE gold button on the surface (kit law): Connect Wallet is THE primary
            // CTA. Two rows only - button centers sit 0.25 apart, far above the ~0.131
            // MinTouch clamp floor from the WO-787 geometry analysis, so ClampMinTouch
            // can grow both rows collision-free on every live canvas.
            var intro = ElarionUiKit.Label(body,
                "Your wallet is your save. Guest progress stays on this device until you connect.",
                0.78f, 0.92f, ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            intro.textWrappingMode = TextWrappingModes.Normal;
            intro.raycastTarget = false;
            ElarionUiKit.FitBlock(intro);

            _status = ElarionUiKit.Label(body, "", 0.62f, 0.68f,
                ElarionUi.Parchment, ElarionUi.FontMicro,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            _status.raycastTarget = false;

            _connectWallet = ElarionUiKit.BuildObsidianButton(body, "Connect Wallet",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.56f), OnConnectWallet);

            _guest = ElarionUiKit.BuildObsidianButton(body, "Play as Guest",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.30f), OnPlayAsGuest);
        }

        // =====================================================================
        //  WO-787/845 email layout (desktop / WebGL) - preserved verbatim
        // =====================================================================
        private void BuildEmailForm(Transform body)
        {
            // WO-787 Part B: Google sign-in is APK-only (owner ruling + owner F8 on the exe).
            // The GoogleSignIn native plugin's asmdef is scoped [Android, Editor] and
            // LoginViewModel.SignInWithGoogleAsync already fails cleanly elsewhere -- the
            // BUTTON is simply never built off the Android target, and the remaining rows
            // reflow over the freed space.
#if UNITY_ANDROID
            const bool googleRow = true;
#else
            const bool googleRow = false;
#endif

            var intro = ElarionUiKit.Label(body,
                "Sign in to keep your progress across devices, or play as a guest and bind an account later.",
                googleRow ? 0.845f : 0.83f, googleRow ? 0.915f : 0.91f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            intro.textWrappingMode = TextWrappingModes.Normal;
            intro.raycastTarget = false;
            ElarionUiKit.FitBlock(intro);

            _email = MakeInputField(body, "Email address", TMP_InputField.ContentType.EmailAddress,
                new Vector2(0.08f, googleRow ? 0.745f : 0.72f),
                new Vector2(0.92f, googleRow ? 0.825f : 0.80f));
            _password = MakeInputField(body, "Password", TMP_InputField.ContentType.Password,
                new Vector2(0.08f, googleRow ? 0.645f : 0.61f),
                new Vector2(0.92f, googleRow ? 0.725f : 0.69f));

            _status = ElarionUiKit.Label(body, "",
                googleRow ? 0.585f : 0.545f, googleRow ? 0.63f : 0.595f,
                ElarionUi.Parchment, ElarionUi.FontMicro,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            _status.raycastTarget = false;

            _signIn = ElarionUiKit.BuildObsidianButton(body, "Sign In",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.08f, googleRow ? 0.475f : 0.42f),
                new Vector2(0.92f, googleRow ? 0.555f : 0.51f), OnSignIn);

            _createAccount = ElarionUiKit.BuildObsidianButton(body, "Create Account",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, googleRow ? 0.34f : 0.27f),
                new Vector2(0.92f, googleRow ? 0.42f : 0.36f), OnCreateAccount);

#if UNITY_ANDROID
            _google = ElarionUiKit.BuildObsidianButton(body, "Sign in with Google",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, 0.205f), new Vector2(0.92f, 0.285f), OnGoogleSignIn);
#endif

            // WO-845: password recovery. GEOMETRY LAW (WO-787): the stack sits at its
            // MinTouch capacity on the shortest live canvas — googleRow button centers are
            // only 0.135 apart vs the ~0.131 clamp floor, so a NEW row cannot fit. The
            // "Forgot password?" control therefore SPLITS the bottom band with Play as
            // Guest: identical y fractions to the shipped layout, so ClampMinTouch's
            // vertical growth profile is unchanged — zero new collision risk. Both halves
            // stay far above the 112px touch floor horizontally on any landscape canvas.
            _guest = ElarionUiKit.BuildObsidianButton(body, "Play as Guest",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, googleRow ? 0.07f : 0.12f),
                new Vector2(0.55f, googleRow ? 0.15f : 0.21f), OnPlayAsGuest);

            _forgot = ElarionUiKit.BuildObsidianButton(body, "Forgot password?",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.58f, googleRow ? 0.07f : 0.12f),
                new Vector2(0.92f, googleRow ? 0.15f : 0.21f), OnForgotPassword);
        }

        // =====================================================================
        //  Actions — presentation only; auth logic is in FirebaseAuthService
        // =====================================================================
        /// <summary>
        /// Hard ceiling on the boot-time Firebase init probe (seconds). Short on purpose:
        /// nothing is on screen while it runs, so the cost of waiting is a blank app.
        /// </summary>
        private const float InitTimeoutSeconds = 12f;

        /// <summary>
        /// Hard ceiling on any ONE network sign-in / reset call (seconds). Firebase's own
        /// Task-returning calls do not promise to complete on a captive-portal or dead-cell
        /// connection; without a ceiling the form stays greyed out with a "Signing in..."
        /// that never resolves.
        /// </summary>
        private const float NetworkTimeoutSeconds = 25f;

        /// <summary>
        /// Awaits an auth attempt with a hard ceiling, turning a HANG into an ordinary
        /// failed <see cref="AuthOutcome"/> the normal failure path can render. Never
        /// throws, so no caller is left busy forever.
        /// <para>
        /// The underlying Task is NOT cancellable (Firebase owns it), so it is handed to
        /// <see cref="Observe"/>: it may still complete later, and an unawaited faulted
        /// Task would otherwise surface as an UnobservedTaskException.
        /// </para>
        /// </summary>
        private static async UniTask<AuthOutcome> Bounded(Task<AuthOutcome> attempt, float seconds, string what)
        {
            try
            {
                return await attempt.AsUniTask()
                    .Timeout(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime);
            }
            catch (TimeoutException)
            {
                FlowTrace.Fail("Auth", what + " did not answer within " + (int)seconds +
                                       "s - releasing the form (Play as Guest was live throughout).");
                Observe(attempt);
                return AuthOutcome.Fail(what + " timed out. Check your connection and try again, " +
                                        "or tap Play as Guest to start now.");
            }
            catch (Exception e)
            {
                FlowTrace.Fail("Auth", what + " threw: " + e.GetType().Name + ": " + e.Message);
                return AuthOutcome.Fail(what + " failed. Try again, or tap Play as Guest to start now.");
            }
        }

        /// <summary>Swallow-and-log a late result so an abandoned Task can never raise
        /// UnobservedTaskException. No UI is touched - the panel has already moved on.</summary>
        private static async void Observe(Task attempt)
        {
            try { await attempt; }
            catch (Exception e) { FlowTrace.Warn("Auth", "abandoned auth attempt ended in: " + e.Message); }
        }

        private async void OnSignIn()
        {
            if (!BeginAttempt(out string email, out string password)) return;
            SetStatus("Signing in... you can still tap Play as Guest.", info: true);
            AuthOutcome outcome = await Bounded(_vm.SignInAsync(email, password), NetworkTimeoutSeconds, "Sign-in");
            HandleOutcome(outcome);
        }

        private async void OnCreateAccount()
        {
            if (!BeginAttempt(out string email, out string password)) return;
            SetStatus("Creating your account... you can still tap Play as Guest.", info: true);
            AuthOutcome outcome = await Bounded(_vm.SignUpAsync(email, password), NetworkTimeoutSeconds,
                                                "Account creation");
            HandleOutcome(outcome);
        }

        /// <summary>
        /// UI failsafe ceiling on the whole wallet-connect await (seconds). Sits ABOVE
        /// WalletService's own 30s provider ceiling so the honest, specific message
        /// normally comes from the wallet layer; this only fires if something below
        /// stops honouring its own timeout. Counted in UNSCALED time, on the player
        /// loop - so a backgrounded app (the player is IN the wallet app) does not burn
        /// the budget, and the count resumes when they come back.
        /// </summary>
        private const float ConnectUiTimeoutSeconds = 35f;

        // WO-847: the wallet-first primary. Honest statuses on every branch; a success
        // resolves the same AuthOutcome shape as email sign-in (UserId = wallet address),
        // so HandleOutcome -> Continue is byte-identical downstream.
        //
        // SOFTLOCK LAW: this await is bounded. Guest stays interactable throughout
        // (SetBusy never touches it), so even a hung handshake leaves a way into the game.
        private async void OnConnectWallet()
        {
            if (_busy || _routed) return;
            SetBusy(true);
            SetStatus("Opening your wallet... you can still tap Play as Guest.", info: true);

            Task<AuthOutcome> attempt = _vm.ConnectWalletAsync();
            AuthOutcome outcome;
            try
            {
                outcome = await attempt.AsUniTask()
                    .Timeout(TimeSpan.FromSeconds(ConnectUiTimeoutSeconds), DelayType.UnscaledDeltaTime);
            }
            catch (TimeoutException)
            {
                if (_routed) return;
                FlowTrace.Fail("Auth",
                    $"wallet connect did not resolve within {ConnectUiTimeoutSeconds}s - restoring the login " +
                    "surface (guest escape was live throughout).");
                SetBusy(false);
                SetStatus("Your wallet did not respond. Open your wallet app and try Connect Wallet again, " +
                          "or tap Play as Guest to start now.", info: false);
                WatchLateConnect(attempt);
                return;
            }
            catch (Exception e)
            {
                if (_routed) return;
                FlowTrace.Fail("Auth", "wallet connect threw at the panel: " + e.Message);
                SetBusy(false);
                SetStatus("Wallet connect failed. Try again, or tap Play as Guest to start now.", info: false);
                return;
            }
            HandleOutcome(outcome);
        }

        /// <summary>
        /// A connect that timed out at the UI is NOT cancelled underneath - Mobile Wallet
        /// Adapter can still come back minutes later and (via WalletSkinBootstrap) bind the
        /// save to that wallet. If that happens while the player is still sitting on this
        /// screen, honour it instead of leaving them bound-but-not-continued. Fully guarded:
        /// once the panel has routed or been destroyed this does nothing.
        /// </summary>
        private async void WatchLateConnect(Task<AuthOutcome> attempt)
        {
            AuthOutcome late;
            try { late = await attempt; }
            catch (Exception e) { FlowTrace.Warn("Auth", "late wallet connect ended in an error: " + e.Message); return; }

            if (this == null || _routed || _canvas == null) return;   // panel gone / player already in
            if (!late.Success) return;
            FlowTrace.Step("Auth", "wallet connect arrived AFTER the UI timeout and succeeded - continuing.");
            Continue();
        }

        private async void OnGoogleSignIn()
        {
            if (_busy || _routed) return;
            SetBusy(true);
            SetStatus("Opening Google sign-in... you can still tap Play as Guest.", info: true);
            // Longer ceiling than a plain network call: a real player is picking an account
            // in a native overlay. UNSCALED player-loop time, so while that overlay has the
            // foreground and Unity is paused the budget does not burn (same semantic as the
            // wallet handshake) - this only fires on an actually dead flow.
            AuthOutcome outcome = await Bounded(_vm.SignInWithGoogleAsync(), 60f, "Google sign-in");
            HandleOutcome(outcome);
        }

        // WO-845: password recovery — uses whatever is in the email field; honest statuses
        // for every branch (empty field / accepted send / mapped failure). The VM owns the
        // auth call; this is presentation + validation only.
        private async void OnForgotPassword()
        {
            if (_busy || _routed) return;
            string email = _email != null ? _email.text.Trim() : "";
            if (string.IsNullOrEmpty(email))
            {
                SetStatus("Enter your email first.", info: false);
                return;
            }
            FlowTrace.Step("Auth", "forgot password tapped.");
            SetBusy(true);
            SetStatus("Sending reset email...", info: true);
            AuthOutcome outcome = await Bounded(_vm.SendPasswordResetAsync(email), NetworkTimeoutSeconds,
                                                "Reset email");
            if (_routed) return;
            SetBusy(false);
            if (outcome.Success)
                SetStatus("Reset email sent to " + MaskEmail(email) + ". Check your inbox.", info: true);
            else
                SetStatus(outcome.Error, info: false);
        }

        // Presentation-only mask for the status line (mirrors the service's log mask —
        // never paint the full address on a possibly-shared screen).
        private static string MaskEmail(string email)
        {
            int at = email.IndexOf('@');
            return at <= 1 ? "*" + (at >= 0 ? email.Substring(at) : "") : email[0] + "***" + email.Substring(at);
        }

        // SOFTLOCK LAW: intentionally NOT gated on _busy. The escape hatch must work even
        // while a sign-in / wallet handshake is still pending - that pending await is
        // exactly the state a stuck player is trying to escape. Only _routed (already
        // continued) short-circuits it.
        private void OnPlayAsGuest()
        {
            if (_routed) return;
            FlowTrace.Step("Auth", "chose Play as Guest.");
            _vm.ContinueAsGuest();   // guest identity is minted on load; nothing to bind
            Continue();
        }

        // Validate + lock the form for an async attempt. Returns false (and messages) if invalid/busy.
        private bool BeginAttempt(out string email, out string password)
        {
            email = _email != null ? _email.text.Trim() : "";
            password = _password != null ? _password.text : "";
            if (_busy || _routed) return false;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetStatus("Enter your email and password.", info: false);
                return false;
            }
            SetBusy(true);
            return true;
        }

        private void HandleOutcome(AuthOutcome outcome)
        {
            if (_routed) return;
            if (outcome.Success)
            {
                // IDENTITY LAW: an EMAIL/GOOGLE success binds NOTHING (Firebase = access);
                // only the wallet path re-keys the save. View just proceeds either way.
                FlowTrace.Step("Auth", "auth OK - continuing.");
                Continue();
                return;
            }
            SetStatus(outcome.Error, info: false);
            SetBusy(false);
        }

        // SOFTLOCK LAW (see the file header): busy locks every control EXCEPT Play as
        // Guest. _guest is deliberately absent from this method and is left interactable
        // for the lifetime of the panel - it is the only guaranteed way into the game,
        // and OnPlayAsGuest's own `if (_busy || _routed) return;` is dropped for the same
        // reason (a hung connect must not swallow the tap). Do NOT "tidy" _guest back in.
        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_signIn != null) _signIn.interactable = !busy;
            if (_createAccount != null) _createAccount.interactable = !busy;
            if (_google != null) _google.interactable = !busy;
            if (_forgot != null) _forgot.interactable = !busy;
            if (_connectWallet != null) _connectWallet.interactable = !busy;
        }

        private void SetStatus(string msg, bool info)
        {
            if (_status == null) return;
            _status.text = msg ?? "";
            _status.color = info ? ElarionUi.ParchmentDim : ElarionUi.Danger;
        }

        // =====================================================================
        //  Teardown (mirrors FoundingChoiceController)
        // =====================================================================
        private void Continue()
        {
            if (_routed) return;
            _routed = true;
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            var cont = _onContinue;
            _onContinue = null;
            if (_canvas != null) Destroy(_canvas);
            Destroy(gameObject);
            cont?.Invoke();
        }

        private void OnDestroy()
        {
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            if (_canvas != null) Destroy(_canvas);
        }

        // =====================================================================
        //  Input field — TMP_InputField over a rounded well (mirrors ClanChatPanel)
        // =====================================================================
        private static TMP_InputField MakeInputField(Transform parent, string placeholder,
            TMP_InputField.ContentType contentType, Vector2 min, Vector2 max)
        {
            var host = new GameObject("Input", typeof(Image), typeof(TMP_InputField));
            host.transform.SetParent(parent, false);
            var rt = (RectTransform)host.transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = host.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            ElarionUiKit.ApplyRounded(bg);

            var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(host.transform, false);
            var art = (RectTransform)areaGo.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(16f, 6f); art.offsetMax = new Vector2(-16f, -6f);

            var text = ElarionUiKit.Label(areaGo.transform, "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Left, 0f, 1f);
            var ph = ElarionUiKit.Label(areaGo.transform, placeholder, 0f, 1f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TextAlignmentOptions.Left, 0f, 1f);
            ph.fontStyle = FontStyles.Italic;

            var field = host.GetComponent<TMP_InputField>();
            field.targetGraphic  = bg;
            field.textViewport   = art;
            field.textComponent  = text;
            field.placeholder    = ph;
            field.lineType       = TMP_InputField.LineType.SingleLine;
            field.contentType    = contentType;
            field.characterLimit = 128;
            field.text = "";
            return field;
        }
    }
}
