// =============================================================================
// LoginPanelController (WO-769, narrowed to wallet-only by WO-837-B) — the
// "connect wallet or play as guest" surface. Presented at boot before the hub,
// modeled on FoundingChoiceController.
//
// ⛔ ONE SURFACE, EVERY PLATFORM (owner ruling 2026-08-21). The player sees a gold
// Connect Wallet primary + Play as Guest. There is NO email form, NO Create Account,
// NO Google button and NO forgot-password, on ANY platform. WO-847 had scoped
// wallet-first to Android/Seeker and kept the WO-787/845 email layout on
// desktop/WebGL for a Google Play release; the owner closed that:
//   "That's only true with the Play Store, which we are not in. We are only in the
//    dApp Store, which is all wallet authentication based."
// The LoginSurfacePlatform / LoginSurfaceLayout seam that carried the split is
// DELETED (not left resolving to a constant) — a one-armed layout switch is how the
// other arm grows back.
//
// Wallet connect routes through LoginWalletBridge -> WalletSkinBootstrap ->
// WalletService.Connect with an EXPLICIT GameStateService.BindWallet (never
// skin-config-gated on this path).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Onboarding   Namespace: DeNelle.Onboarding (references DeNelle.Core only).
// UI: code-built uGUI on the Obsidian kit (ElarionUiKit) — its own overlay canvas,
// NO UXML/UIDocument (CLAUDE.md §8). Colour never carries meaning (each control labelled).
// PRESENTATION ONLY: identity logic lives in LoginViewModel; this file builds UI.
// Guest = the existing device-hash fallback (GameStateService.EnsureAccount mints
// guest-local-* on load), so guest just continues.
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
    /// Obsidian Connect-Wallet + Play-as-Guest surface. Call <see cref="Present"/> with
    /// the "enter the game" continuation; the panel invokes it once the player connects a
    /// wallet or chooses guest.
    /// </summary>
    public sealed class LoginPanelController : MonoBehaviour
    {
        private readonly LoginViewModel _vm = LoginViewModel.CreateDefault();

        private Action _onContinue;
        private bool _routed;
        private GameObject _canvas;
        private PanelHandle _panelHandle;

        private TextMeshProUGUI _status;
        private Button _guest;
        private Button _connectWallet;
        private bool _busy;

#if UNITY_EDITOR
        /// <summary>
        /// Capture-only presentation seam. It lets editor evidence build the exact
        /// production layout with GOOGLE_PLAY copy without changing player defines.
        /// Player builds do not contain this property.
        /// </summary>
        public static bool? EditorGooglePlayPresentationOverride { get; set; }
#endif

        private static bool IsGooglePlayPresentation
        {
            get
            {
#if GOOGLE_PLAY
                return true;
#elif UNITY_EDITOR
                return EditorGooglePlayPresentationOverride ?? false;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Show the login surface, then run <paramref name="onContinue"/> once the player
        /// gets in (wallet connect / guest). Always presents; the caller decides when to
        /// call it in the boot flow.
        /// </summary>
        public static void Present(Action onContinue)
        {
            using var _ = FlowTrace.Enter("Auth", "LoginPanelController.Present");
            var host = new GameObject("LoginUI");
            var ctrl = host.AddComponent<LoginPanelController>();
            ctrl._onContinue = onContinue;
            ctrl.Build();
            FlowTrace.Step("Auth",
                "login panel presented (first-run connect or guest; production path, same on every build).");
        }

        /// <summary>
        /// THE GATE DECISION, pure and testable (2026-08-18 defect: the owner's wallet
        /// auto-resumed at boot and the SIGN IN panel was presented anyway ~5s later —
        /// device capture 20:21:38 "auto-resume SUCCEEDED", 20:21:43 LoginPanelController.Build).
        /// <para>
        /// ROOT CAUSE (fixed here): this gate read ONE source — <c>FirebaseAuthService.IsSignedIn</c>
        /// — while the identity that actually keys this game is the WALLET. A player who has only
        /// ever connected a wallet was not Firebase-signed-in, so the gate presented the login
        /// surface on TOP of an already-connected, already-bound session.
        /// </para>
        /// <para>
        /// <paramref name="legacySignedIn"/> is RETAINED-BUT-DEAD (WO-837-B): email/Firebase login is
        /// removed, so <see cref="PresentOrContinue"/> now always passes <c>false</c>. The parameter
        /// stays because it is the pure seam's contract — LoginGateRegression drives a truth table
        /// against <c>(bool,bool,bool)</c> by reflection, and a wallet-only build must still prove
        /// "any already-in signal => CONTINUE". Do not repurpose it as a second identity source.
        /// </para>
        /// <para>
        /// <paramref name="walletIdentityBound"/> is the RACE-PROOF half: it comes from the persisted
        /// save + this device's attestation, so it is true SYNCHRONOUSLY at boot — before any silent
        /// reconnect finishes. That is why this fix needs no delay, no timeout and no extra await.
        /// <paramref name="walletConnected"/> is the live published state for the in-session case.
        /// </para>
        /// <para>FIRST RUN IS PRESERVED: a genuine first run has no connected wallet and no attested
        /// bound identity (its save key is the <c>guest-local-</c> device hash, which is not
        /// cloud-identity-shaped) — every input is false, so this returns false and the panel
        /// presents. That is its one legitimate purpose.</para>
        /// </summary>
        public static bool ShouldContinueWithoutLogin(bool walletConnected, bool walletIdentityBound,
                                                      bool legacySignedIn)
            => walletConnected || walletIdentityBound || legacySignedIn;

        /// <summary>
        /// Boot entry: if the player is ALREADY IN — a connected wallet or an attested
        /// wallet-bound save — continue straight through; otherwise present the
        /// connect-or-guest surface.
        /// <para>CORRECTED 2026-08-18: this doc used to say "already signed in (Firebase caches the
        /// session)" and the code matched it — Firebase-only. On a wallet-first build that is the
        /// wrong source, and it re-prompted a player whose wallet had just auto-resumed. The
        /// decision now lives in <see cref="ShouldContinueWithoutLogin"/>.</para>
        /// <para>WO-837-B: the boot-time Firebase init probe is GONE with the email login. It was a
        /// blocking, up-to-12s network await that ran BEFORE any UI existed — the worst softlock
        /// site on the whole surface — in service of an identity source that binds nothing. Removing
        /// it makes this method synchronous, so there is no longer any await between app start and
        /// the first screen. Do not reintroduce a network call here.</para>
        /// </summary>
        public static void PresentOrContinue(Action onContinue)
        {
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

            // Third input is permanently false in a wallet-only build (WO-837-B) — see the
            // legacySignedIn note on ShouldContinueWithoutLogin.
            //
            // WO-1249 (owner 2026-08-27): this is the PRODUCTION gate. A first run with
            // every input false PRESENTS -- that is the one-time connect, not a bug, and
            // it is the same on a tester APK as on the store build. Do not branch this
            // decision on a tester define: a build that skips the connect cannot validate
            // production. Extra native wallet sheets AFTER CONTINUE are session minting
            // (WO-1157), not this panel.
            bool continueIn = ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, false);

            // §1.4b: the decision AND every input it was made from, so the next reader never has to
            // guess WHY the panel appeared. A trace that cannot report the wrong outcome is decoration.
            // WO-1249: never log a wallet address; the booleans are enough.
            FlowTrace.Step("Auth",
                "login gate decision=" + (continueIn ? "CONTINUE" : "PRESENT") +
                " (walletConnected=" + walletConnected +
                ", walletIdentityBound=" + walletIdentityBound +
                ", legacySignedIn=false [wallet-only build, WO-837-B]).");

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
            var chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform,
                IsGooglePlayPresentation ? "WELCOME" : "YOUR WALLET",
                new Vector2(0.22f, 0.12f), new Vector2(0.78f, 0.88f), onClose: null,
                withBackdrop: false, frameName: RpgUiCatalog.FrameCore);
            MedievalUiSkin.ApplyShell(chrome, compact: true);
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);
            if (chrome.layout != null && chrome.layout.medallion != null)
                chrome.layout.medallion.gameObject.SetActive(false);

            // WO-787 Part A: lay out on the FULL-rect chrome.content, NOT chrome.layout.body.
            // BuildObsidianPanel (WO-714 P6) raises Zone_Body's floor by the close-band +
            // footer reservation (body.y up to ~0.45) to clear the shared Close -- but this
            // panel HIDES its Close, so the reservation only compressed the stack until every
            // fraction slot fell below the MinTouchPx floor and the rows overlapped.
            // Fractions below are clamp-aware: adjacent button centers sit >= 112 reference px
            // apart on the shortest live canvas, so ClampMinTouch can grow rows collision-free.
            Transform body = chrome.content.transform;

            // WO-837-B (owner ruling 2026-08-21): ONE surface on every platform -
            // "connect wallet or play as guest". No email form, no forgot-password
            // (the wallet is its own recovery), no platform branch.
            BuildWalletFirst(body);

            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Login", Continue, () => !_routed && _canvas != null);
            PanelManager.NotifyOpened(_panelHandle);
        }

        // =====================================================================
        //  The login surface (every platform): Connect Wallet + Play as Guest
        // =====================================================================
        private void BuildWalletFirst(Transform body)
        {
            // ONE gold button on the surface (kit law): Connect Wallet is THE primary
            // CTA. Two rows only - button centers sit 0.25 apart, far above the ~0.131
            // MinTouch clamp floor from the WO-787 geometry analysis, so ClampMinTouch
            // can grow both rows collision-free on every live canvas.
            var intro = ElarionUiKit.Label(body,
                IsGooglePlayPresentation
                    ? "Continue with Google to protect your progress across devices, or play as a guest on this device."
                    : "Your wallet is your save. Connect now (one-time on this device). Guest progress stays here until you connect.",
                0.68f, 0.80f, ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            intro.textWrappingMode = TextWrappingModes.Normal;
            intro.raycastTarget = false;
            ElarionUiKit.FitBlock(intro);

            _status = ElarionUiKit.Label(body, "", 0.57f, 0.64f,
                ElarionUi.Parchment, ElarionUi.FontMicro,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            _status.raycastTarget = false;

            _connectWallet = ElarionUiKit.BuildObsidianButton(body,
                IsGooglePlayPresentation ? "Continue with Google" : "Connect Wallet",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.54f), OnConnectWallet);
            MedievalUiSkin.ApplyButton(_connectWallet, primary: true);
            ApplyFrontDoorButtonFrame(_connectWallet);

            _guest = ElarionUiKit.BuildObsidianButton(body, "Play as Guest",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.34f), OnPlayAsGuest);
            MedievalUiSkin.ApplyButton(_guest, primary: false);
            ApplyFrontDoorButtonFrame(_guest);
        }

        private static void ApplyFrontDoorButtonFrame(Button button)
        {
            if (button == null || !(button.targetGraphic is Image image)) return;
            var frame = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            if (frame != null) image.sprite = frame;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) ElarionUiKit.FitSingleLine(label, 24f, 36f);
        }

        // =====================================================================
        //  Actions — presentation only; identity logic is in LoginViewModel
        // =====================================================================
        /// <summary>
        /// UI failsafe ceiling on the whole wallet-connect await (seconds). Sits ABOVE
        /// WalletService's own 30s provider ceiling so the honest, specific message
        /// normally comes from the wallet layer; this only fires if something below
        /// stops honouring its own timeout. Counted in UNSCALED time, on the player
        /// loop - so a backgrounded app (the player is IN the wallet app) does not burn
        /// the budget, and the count resumes when they come back.
        /// </summary>
        private const float ConnectUiTimeoutSeconds = 35f;

        // The primary — and, since WO-837-B, the ONLY identity control on the surface.
        // Honest statuses on every branch; a success resolves an AuthOutcome whose
        // UserId is the wallet address, and HandleOutcome -> Continue takes it from there.
        //
        // SOFTLOCK LAW: this await is bounded. Guest stays interactable throughout
        // (SetBusy never touches it), so even a hung handshake leaves a way into the game.
        private async void OnConnectWallet()
        {
            if (_busy || _routed) return;
            SetBusy(true);
#if GOOGLE_PLAY
            SetStatus("Opening Google sign-in... you can still tap Play as Guest.", info: true);
#else
            SetStatus("Opening your wallet... you can still tap Play as Guest.", info: true);
#endif

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
#if GOOGLE_PLAY
                SetStatus("Google sign-in did not respond. Try Continue with Google again, " +
                          "or tap Play as Guest to start now.", info: false);
#else
                SetStatus("Your wallet did not respond. Open your wallet app and try Connect Wallet again, " +
                          "or tap Play as Guest to start now.", info: false);
#endif
                WatchLateConnect(attempt);
                return;
            }
            catch (Exception e)
            {
                if (_routed) return;
                FlowTrace.Fail("Auth", "wallet connect threw at the panel: " + e.Message);
                SetBusy(false);
#if GOOGLE_PLAY
                SetStatus("Google sign-in failed. Try again, or tap Play as Guest to start now.", info: false);
#else
                SetStatus("Wallet connect failed. Try again, or tap Play as Guest to start now.", info: false);
#endif
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

        private void HandleOutcome(AuthOutcome outcome)
        {
            if (_routed) return;
            if (outcome.Success)
            {
                // IDENTITY LAW (WO-837-B): the wallet is the ONLY identity, and the VM has
                // already bound it. View just proceeds.
                FlowTrace.Step("Auth", "wallet connect OK - continuing.");
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

        // NOTE (WO-837-B): the MakeInputField helper (a TMP_InputField over a rounded
        // well) was deleted with the email form — this surface has no text entry at all
        // now. The pattern still lives in ClanChatPanel / RedeemCodePanel if a future
        // panel needs it; do not resurrect it here for an identity field.
    }
}
