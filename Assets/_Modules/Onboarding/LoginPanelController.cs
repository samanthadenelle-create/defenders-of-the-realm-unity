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
// =============================================================================

using System;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;
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
        /// Init-aware boot entry: if a returning player is already signed in (Firebase
        /// caches the session), continue straight through; otherwise present the
        /// login-or-guest surface. Safe if Firebase init fails — falls through to the
        /// panel, where Play-as-Guest always works, so the boot flow can never lock.
        /// </summary>
        public static async void PresentOrContinue(Action onContinue)
        {
            bool signedIn = false;
            try
            {
                await FirebaseAuthService.Instance.EnsureInitializedAsync();
                signedIn = FirebaseAuthService.Instance.IsSignedIn;
            }
            catch (Exception e) { FlowTrace.Warn("Auth", "login init check threw: " + e.Message); }

            if (signedIn)
            {
                FlowTrace.Step("Auth", "already signed in — skipping login, continuing.");
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
        private async void OnSignIn()
        {
            if (!BeginAttempt(out string email, out string password)) return;
            SetStatus("Signing in...", info: true);
            AuthOutcome outcome = await _vm.SignInAsync(email, password);
            HandleOutcome(outcome);
        }

        private async void OnCreateAccount()
        {
            if (!BeginAttempt(out string email, out string password)) return;
            SetStatus("Creating your account...", info: true);
            AuthOutcome outcome = await _vm.SignUpAsync(email, password);
            HandleOutcome(outcome);
        }

        // WO-847: the wallet-first primary. Honest statuses on every branch; a success
        // resolves the same AuthOutcome shape as email sign-in (UserId = wallet address),
        // so HandleOutcome -> Continue is byte-identical downstream.
        private async void OnConnectWallet()
        {
            if (_busy || _routed) return;
            SetBusy(true);
            SetStatus("Opening your wallet...", info: true);
            AuthOutcome outcome = await _vm.ConnectWalletAsync();
            HandleOutcome(outcome);
        }

        private async void OnGoogleSignIn()
        {
            if (_busy || _routed) return;
            SetBusy(true);
            SetStatus("Opening Google sign-in...", info: true);
            AuthOutcome outcome = await _vm.SignInWithGoogleAsync();
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
            AuthOutcome outcome = await _vm.SendPasswordResetAsync(email);
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

        private void OnPlayAsGuest()
        {
            if (_busy || _routed) return;
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
                // The VM already bound the UID as the save player-id. View just proceeds.
                FlowTrace.Step("Auth", $"auth OK uid={outcome.UserId} — continuing.");
                Continue();
                return;
            }
            SetStatus(outcome.Error, info: false);
            SetBusy(false);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_signIn != null) _signIn.interactable = !busy;
            if (_createAccount != null) _createAccount.interactable = !busy;
            if (_google != null) _google.interactable = !busy;
            if (_guest != null) _guest.interactable = !busy;
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
