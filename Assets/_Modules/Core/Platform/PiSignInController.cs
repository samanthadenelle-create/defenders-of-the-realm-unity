using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Core.Platform
{
#if GOOGLE_PLAY
    /// <summary>Non-crypto Play-channel compatibility seam. The Pi runtime, UI,
    /// endpoints, and labels are not compiled into a Google Play player.</summary>
    public static class PiSignInController
    {
        public static string SignedInUid => null;
        public static string SignedInUsername => null;
        public static bool IsSignedIn => false;
        public static event Action<string, string> OnSignedIn { add { } remove { } }
    }
#else
    /// <summary>
    /// Pi Network sign-in. Inside Pi Browser it AUTO-triggers on load and also shows a
    /// manual "Sign in with Pi" button. Flow: Pi.init (awaited) → Pi.authenticate(['username'])
    /// → POST the accessToken to /api/pi/verify, which validates it against api.minepi.com/v2/me
    /// (server-side, no API key) before a session is established. Off Pi Browser the platform
    /// stub reports unavailable and this is a no-op (no button, no auth) — the game runs unchanged.
    /// Ref: PI_INTEGRATION_SPEC.md, https://pi-apps.github.io/pi-sdk-docs/quick-start/genai/Authentication
    /// </summary>
    public sealed class PiSignInController : MonoBehaviour
    {
        // Same Vercel backend the rest of the client uses (GameStateService.BackendBase).
        private const string VerifyUrl = "https://defenders-of-the-realm-v2.vercel.app/api/pi/verify";

        /// <summary>
        /// HORIZONTAL screen-edge inset for the corner chip (WO-1083 defect #6).
        /// DERIVED, not eyeballed: the hero-select panel spans screen x [0.015,0.985] and
        /// ElarionUiKit's pixel-measured FrameCore zones put the frame's INTERIOR at panel
        /// x 0.945 — i.e. everything right of screen x = 2488 (of 2670) is border ART. The
        /// default 44-px margin put the chip at x[2326..2626], squarely on that border,
        /// which is the defect. 240 px lands it at x[2130..2430], clear of the border strip
        /// by ~58 px and inside the frame's dark header region. Fixed pixels by law; the
        /// VERTICAL inset stays <see cref="SafeAreaInset.EdgeMarginPx"/>, which already
        /// seats the chip in FrameCore's header band (screen y 56..139).
        /// </summary>
        private const float FramedCornerMarginXPx = 240f;

        [Tooltip("Develop entirely on Testnet/Sandbox; flip off for mainnet go-live.")]
        [SerializeField] private bool sandbox = true;

        public static string SignedInUid { get; private set; }
        public static string SignedInUsername { get; private set; }
        public static bool IsSignedIn => !string.IsNullOrEmpty(SignedInUid);
        public static event Action<string, string> OnSignedIn; // (uid, username)

        private IPiPlatform _pi;
        private Button _button;
        private TMP_Text _label;
        private bool _signingIn; // guard: auto-fire + manual click must not double-run (orphans the shared TCS)
        // WO-603: the active currency/auth/branding skin. AuthMode==PiSdk is the live Pi path
        // (unchanged); AuthMode==SolanaWallet swaps this corner button to a wallet-connect entry.
        private CurrencySkin _skin;
        // True when this corner is the wallet-connect entry (SolanaWallet auth mode). Set in
        // BuildButton; everything wallet-related below is gated on it so the Pi path is untouched.
        private bool _walletSkin;

        /// <summary>Spawns the controller once at boot if not already present.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<PiSignInController>() != null) return;
            var go = new GameObject("PiSignInController");
            DontDestroyOnLoad(go);
            go.AddComponent<PiSignInController>();
        }

        private void Start()
        {
            // WO-603: resolve the active skin BEFORE the button is built (synchronous — skin.json
            // via Resources + a URL-param read). Default = Pi, so the live path is unchanged.
            _skin = CurrencySkinResolver.Active;
            _pi = PiPlatform.Current;
            // ALWAYS show the manual "Sign in with Pi" button so the user can trigger sign-in even
            // if auto-detection is delayed/flaky. The Pi SDK (sdk.minepi.com/pi-sdk.js) loads
            // ASYNC, so IsAvailable can be false for the first moments after boot — gating the
            // button on it (the old bug) hid the sign-in entirely in the Pi Desktop preview.
            BuildButton();
            // Earns-its-place (owner 2026-07-02): the button lives where a login DECISION makes
            // sense — the Title/menu context — not riding the HUD all game. Auto sign-in still
            // runs everywhere; once signed in the button is gone for good.
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (_, __) => UpdateButtonVisibility();
            UpdateButtonVisibility();
            // 2026-08-05 Seeker capture: wallet connect succeeded end to end ("Connect OK -
            // CHKK...sfkC") and this button still read "Connect Wallet", because nothing ever
            // told it. Subscribe to the connected-state signal under the wallet skin only
            // (the Pi path stays byte-identical); the current state was already applied in
            // BuildButton, so a button built AFTER the connect is correct too.
            if (_walletSkin)
                CurrencySkinResolver.WalletConnectionChanged += OnWalletConnectionChanged;
            // Only the Pi skin auto-triggers Pi sign-in. Under the Solana/$SKR skin the corner
            // button is a wallet-connect entry (see BuildButton) and Pi polling never runs.
            if (_skin != null && _skin.AuthMode == SkinAuthMode.PiSdk)
            {
                // Pi Browser authentication can background its WebView for native consent.
                // Do not overlap that with Unity/catalog startup. The visible Title button
                // is the user-gesture boundary and remains retryable.
                SetButton("Sign in with Pi", true);
                FlowTrace.Step("Pi", "Pi authentication deferred until the player taps Sign in on Title.");
            }
        }

        private void OnDestroy()
        {
            // No leaks: this is a DDOL widget, but a domain reload / a second boot must not
            // leave a dead subscriber holding a destroyed label. Unsubscribing an unsubscribed
            // handler is a no-op, so this is safe under either skin.
            CurrencySkinResolver.WalletConnectionChanged -= OnWalletConnectionChanged;
        }

        private void OnWalletConnectionChanged(bool connected, string shortAddress) => ApplyWalletConnectionState();

        /// <summary>
        /// Paints the corner button from the CURRENT wallet connection state
        /// (CurrencySkinResolver). Called three ways on purpose: at BUILD time (a button
        /// created after the connect must show connected, not just live transitions), on
        /// the change event, and on every scene change. Idempotent — it no-ops when the
        /// label already says the right thing, so the scene-change call cannot spam the trace.
        /// </summary>
        private void ApplyWalletConnectionState()
        {
            if (!_walletSkin || _label == null) return;

            bool connected = CurrencySkinResolver.IsWalletConnected;
            string shortAddress = CurrencySkinResolver.ConnectedWalletShortAddress;
            // The TEXT carries the state — never colour alone. ASCII only ("Wallet CHKK...sfkC"),
            // because TMP renders the U+2026 ellipsis of WalletAccount.ShortAddress as tofu.
            string desired = connected && !string.IsNullOrEmpty(shortAddress)
                ? "Wallet " + shortAddress
#if GOOGLE_PLAY
                : "Continue with Google";
#else
                : "Connect Wallet";
#endif
            if (_label.text == desired) return;

            // Connected: stop it being tappable. There is no disconnect surface here and one
            // is NOT invented — a tap would only early-out in the service anyway (the 0.0ms
            // "-> Connect / <- Connect" pairs in the capture).
            bool interactable = !connected;
            SetButton(desired, interactable);
            FlowTrace.Step("Wallet", $"Corner auth button updated: '{desired}' (interactable={interactable}).");
        }

        private void UpdateButtonVisibility()
        {
            if (_button == null) return;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool menuContext = scene == "Title" || scene == "HeroSelect";
            _button.transform.parent.gameObject.SetActive(!IsSignedIn && menuContext);
            // Cheap safety net: if the connect landed while this widget was between scenes,
            // the state is re-read here rather than depending on catching the event.
            ApplyWalletConnectionState();
        }

        /// <summary>Manual trigger (the button).</summary>
        public void SignIn() => SignInAsync().Forget();

        /// <summary>
        /// The Pi SDK script loads asynchronously, so window.Pi may not be ready when Start() runs.
        /// Poll briefly (~10s) so AUTO sign-in fires the instant the SDK becomes available (the Pi
        /// portal auto-detects a signed-in user within seconds); otherwise the manual button remains.
        /// </summary>
        private async UniTaskVoid WaitForPiThenAutoSignIn()
        {
            for (int i = 0; i < 20; i++) // ~10s @ 500ms
            {
                if (_pi == null) _pi = PiPlatform.Current;
                if (_pi != null && _pi.IsAvailable)
                {
                    // WO-678 Lane C: window.Pi exists in ANY browser once pi-sdk.js loads, but only
                    // the real Pi Browser host ever answers it — auto-firing Pi.init elsewhere
                    // guarantees one doomed promise the SDK rejects at 120s ("Promise with id 0
                    // timed out after 120000 ms"). Gate AUTO sign-in on the UA environment check;
                    // the manual button stays available everywhere (Lane A absorbs its rejection).
                    if (!WebGLPiPlatform.IsPiBrowserEnvironment)
                    {
                        FlowTrace.Step("Pi", "Pi SDK loaded but UA is not Pi Browser — skipping auto sign-in; manual button available.");
                        SetButton("Sign in with Pi", true);
                        return;
                    }
                    SignInAsync().Forget(); // auto-trigger the moment Pi is ready
                    return;
                }
                await UniTask.Delay(500);
            }
            FlowTrace.Step("Pi", "Pi not auto-detected after 10s — manual 'Sign in with Pi' button available.");
            SetButton("Sign in with Pi", true);
        }

        private async UniTaskVoid SignInAsync()
        {
            // Auto-fire (WaitForPiThenAutoSignIn) and the manual button both call this. They share the
            // single _initTcs/_authTcs on WebGLPiPlatform, so a second overlapping run overwrites the
            // first's TCS and orphans its await forever. Guard so only one sign-in runs at a time.
            if (_signingIn) { FlowTrace.Step("Pi", "SignIn already in progress — ignoring duplicate trigger."); return; }
            _signingIn = true;
            try
            {
                SetButton("Signing in...", false); // ASCII only - TMP renders U+2026 as tofu

                // ROOT CAUSE of the 2026-07-01 break: the Pi SDK calls resolve only via a JS promise
                // callback (WebGLPiPlatform HandleCallback). If the promise never settles — a dismissed
                // Pi consent popup or an SDK stall in the Pi Desktop preview — the bare `await` HANGS
                // FOREVER, leaving the button stuck on "Signing in…" (and it now auto-fires on load, so it
                // hangs with no user action). Bound every SDK await with a timeout so sign-in ALWAYS
                // resolves to a retryable state instead of a dead screen. Proven from data: client traces
                // flow but ZERO /api/pi/verify calls -> the flow dies at Init/Authenticate before verify.
                bool inited;
                try { inited = await _pi.Init(sandbox).Timeout(TimeSpan.FromSeconds(20)); }
                catch (TimeoutException)
                {
                    FlowTrace.Warn("Pi", "Pi.init timed out after 20s (SDK never signalled ready).");
                    SetButton("Sign in with Pi", true);
                    return;
                }
                if (!inited)
                {
                    FlowTrace.Warn("Pi", "Pi.init failed/unavailable.");
                    SetButton("Sign in with Pi", true);
                    return;
                }

                PiAuthResult auth;
                try { auth = await _pi.Authenticate(new[] { "username" }).Timeout(TimeSpan.FromSeconds(30)); }
                catch (TimeoutException)
                {
                    FlowTrace.Warn("Pi", "Pi.authenticate timed out after 30s (consent not completed).");
                    SetButton("Sign in with Pi", true);
                    return;
                }
                if (!auth.Ok || string.IsNullOrEmpty(auth.AccessToken))
                {
                    FlowTrace.Warn("Pi", $"Pi auth failed: {auth.Error}");
                    SetButton("Sign in with Pi", true);
                    return;
                }

                bool verified = await VerifyWithBackend(auth.AccessToken);
                if (!verified)
                {
                    SetButton("Retry Pi sign-in", true);
                    return;
                }

                // WO-603: NeonDB identity-key selection. DEFAULT OFF (skin.bindIdentityOnAuth=false)
                // so today's Pi deployment keeps its current identity behaviour (zero regression).
                // Turning it on binds the skin-appropriate key (Pi UID here) as the NeonDB playerId —
                // that changes which key writes to NeonDB, so it is gated behind a migration decision
                // (see WO-603 RESULT). Guarded: never binds a null/empty key.
                if (_skin != null && _skin.BindIdentityOnAuth)
                {
                    string idKey = _skin.ResolveIdentityKey(SignedInUid, null);
                    if (!string.IsNullOrEmpty(idKey))
                    {
                        DeNelle.Core.State.GameStateService.Instance?.BindWallet(idKey);
                        FlowTrace.Step("Skin", $"Bound NeonDB identity key ({_skin.IdentityKeyKind}) from Pi sign-in.");
                    }
                }

                SetButton($"Pi: {SignedInUsername}", false);
                OnSignedIn?.Invoke(SignedInUid, SignedInUsername);
                FlowTrace.Step("Pi", $"Signed in as {SignedInUsername} (uid bound to session).");
            }
            catch (Exception e)
            {
                FlowTrace.Fail("Pi", $"SignIn threw: {e.Message}");
                SetButton("Sign in with Pi", true);
            }
            finally
            {
                _signingIn = false;
            }
        }

        // Server-side token validation is the trust boundary — never trust the frontend identity.
        private async UniTask<bool> VerifyWithBackend(string accessToken)
        {
            string json = "{\"accessToken\":\"" + accessToken.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            using var req = new UnityWebRequest(VerifyUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 20; // never hang on a stalled backend

            try { await req.SendWebRequest().ToUniTask(); }
            catch (Exception e) { FlowTrace.Warn("Pi", $"/verify transport error: {e.Message}"); return false; }

            if (req.result != UnityWebRequest.Result.Success)
            {
                FlowTrace.Warn("Pi", $"/verify HTTP {req.responseCode}");
                return false;
            }

            VerifyResp resp;
            try { resp = JsonUtility.FromJson<VerifyResp>(req.downloadHandler.text); }
            catch (Exception e) { FlowTrace.Warn("Pi", $"/verify parse error: {e.Message}"); return false; }

            if (resp == null || !resp.success || string.IsNullOrEmpty(resp.uid))
            {
                FlowTrace.Warn("Pi", $"/verify rejected: {resp?.error}");
                return false;
            }

            SignedInUid = resp.uid;
            SignedInUsername = string.IsNullOrEmpty(resp.username) ? resp.uid : resp.username;
            return true;
        }

        [Serializable]
        private class VerifyResp { public bool success; public string uid; public string username; public string error; }

        // --- corner button, dressed by the shared kit (rounded glass + kit font + shared
        // press feedback) so it reads as OUR chrome, not a pasted web widget. Pi identity
        // survives as the violet fill over the kit's rounded sprite. Own overlay canvas is
        // deliberate: this is a DDOL cross-scene widget that must outlive every scene canvas.
        private void BuildButton()
        {
            var canvasGo = new GameObject("PiSignInCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            // WO-596 privacy: the button can show the Pi username — hide it from bug-report captures.
            DeNelle.Core.Diagnostics.PrivacySensitiveUi.Register(canvasGo);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            // WO-868: this corner is authored in DEVICE PIXELS (fixed-pixel bands, never
            // fractions of parent) and SafeAreaInset works in screen px, so pin the scaler
            // to 1:1 explicitly instead of relying on the CanvasScaler default. The
            // 2026-08-04 Seeker capture measured the button at exactly 300 px wide for a
            // 300-unit sizeDelta, confirming this is already the live behaviour.
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var holder = new GameObject("PiSignInButton", typeof(RectTransform));
            holder.transform.SetParent(canvasGo.transform, false);
            var rt = holder.GetComponent<RectTransform>();
            // Owner 2026-07-16 ("Sign in wit..." truncated on web): 220px clipped "Sign in with Pi"
            // to an ellipsis. Widen to fit the full label; the TMP label auto-sizes (below) so it
            // never truncates under either skin ("Connect Wallet" is shorter and also fits).
            //
            // WO-868 HEIGHT = MinTouchPx, not 60. PROVEN from the device capture
            // (docs/ui-review/2026-08-04-seeker/01-title-screen.png, 1:1 @ 2670x1200): the
            // visible box measured 300 x 112 with its top edge OFF-SCREEN at y = -10. The kit
            // touch floor (ElarionUiKit.ClampMinTouch) grows a sub-floor button SYMMETRICALLY
            // ABOUT ITS CENTRE, so a 60-px holder silently became 112 and pushed 26 px past the
            // holder on EVERY side — the same growth that broke the Echo picker (WO-852). Author
            // the holder AT the floor so the guard has nothing to grow and the rect stays where
            // the safe-area math put it.
            rt.sizeDelta = new Vector2(300f, ElarionUiKit.MinTouchPx);
            // WO-868 SAFE AREA: was a raw anchoredPosition of (-16,-16) — ~6 dp on the Seeker,
            // which reads as flush AND sits inside the rounded-corner / camera-cutout band, so
            // "Connect Wallet" was clipped off the top-right corner. ApplyTopRight sets the
            // anchors + pivot + position from Screen.safeArea (so it adapts to ANY cutout) plus
            // a fixed-pixel margin, and re-fits on rotation / resolution change.
            // WO-1083 defect #6 (hero-select capture tmp/heroselect2-104958.png, 2670x1200):
            // with the default 44-px margin this chip lands at x[2326..2626] — and the
            // hero-select panel's FrameCore border art runs from x 2488 (the frame's measured
            // interior edge, panel x 0.945) to the panel edge at 2630, so the chip drew ON TOP
            // of the frame's top-right border. This button is
            // shown in EXACTLY TWO scenes (UpdateButtonVisibility: "Title" and "HeroSelect"),
            // both full-screen menu contexts, so a larger HORIZONTAL inset is the whole fix:
            // it pulls the chip clear of the border and into the header band, and on Title
            // (no frame) it simply sits slightly further in.
            // Vertical is NOT changed — 44 px already seats the chip inside FrameCore's
            // header band (screen y 56..139 at this size), which is where the WO-1083 mockup
            // puts it. Widening it too would push the chip down into the body well.
            // The margin stays FIXED PIXELS and still routes through SafeAreaInset, so the
            // WO-868 contract (cutout-aware, never a hand-placed inset) is intact.
            SafeAreaInset.ApplyTopRight(rt, FramedCornerMarginXPx, SafeAreaInset.EdgeMarginPx);

            // WO-603: under the Solana/$SKR skin this corner is a wallet-connect entry, not Pi sign-in.
            // The full wallet-connect flow lives in DeNelle.Wallet (Core cannot reference it) and is a
            // flagged follow-up — the button routes through CurrencySkinResolver.RequestWalletConnect(),
            // which warns (no silent failure) until that handler is subscribed.
            bool walletSkin = _skin != null && _skin.AuthMode == SkinAuthMode.SolanaWallet;
            _walletSkin = walletSkin;
            string initialLabel = walletSkin
#if GOOGLE_PLAY
                ? "Continue with Google"
#else
                ? "Connect Wallet"
#endif
                : "Sign in with Pi";
            Action onClick = walletSkin
                ? (Action)CurrencySkinResolver.RequestWalletConnect
                : SignIn;

            _button = ElarionUiKit.Button(holder.transform, initialLabel, ElarionUiKit.ButtonKind.Quiet,
                                          Vector2.zero, Vector2.one, onClick);
            if (_button.targetGraphic is Image img)
                img.color = walletSkin
                    ? new Color(0.09f, 0.72f, 0.55f, 0.95f)  // Solana/Seeker teal-green over the kit glass
                    : new Color(0.43f, 0.30f, 0.78f, 0.95f); // Pi violet over the kit's rounded glass
            _label = _button.GetComponentInChildren<TMP_Text>();
            // No ellipsis: auto-size within the widened button + single-line overflow so the full
            // label always renders (the "Sign in wit..." truncation fix, owner 2026-07-16).
            if (_label != null)
            {
                _label.enableAutoSizing = true;
                _label.fontSizeMin = 14f;
                _label.fontSizeMax = 22f;
                _label.enableWordWrapping = false;
                _label.overflowMode = TMPro.TextOverflowModes.Overflow;
            }

            // Read the CURRENT connected state now, not only on the next transition: the
            // wallet may already be connected by the time this button is built (a panel
            // opened after connecting, or a rebuild on scene change). This is the half that
            // an event-only signal would have missed. No-ops under the Pi skin.
            ApplyWalletConnectionState();
        }

        private void SetButton(string text, bool interactable)
        {
            if (_label != null) _label.text = text;
            if (_button != null) _button.interactable = interactable;
        }
    }
#endif
}
