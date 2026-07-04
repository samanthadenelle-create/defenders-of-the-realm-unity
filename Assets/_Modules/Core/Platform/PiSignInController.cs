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
            // Only the Pi skin auto-triggers Pi sign-in. Under the Solana/$SKR skin the corner
            // button is a wallet-connect entry (see BuildButton) and Pi polling never runs.
            if (_skin != null && _skin.AuthMode == SkinAuthMode.PiSdk)
                WaitForPiThenAutoSignIn().Forget();
        }

        private void UpdateButtonVisibility()
        {
            if (_button == null) return;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool menuContext = scene == "Title" || scene == "HeroSelect";
            _button.transform.parent.gameObject.SetActive(!IsSignedIn && menuContext);
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
                SetButton("Signing in…", false);

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

            var holder = new GameObject("PiSignInButton", typeof(RectTransform));
            holder.transform.SetParent(canvasGo.transform, false);
            var rt = holder.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -16f);
            rt.sizeDelta = new Vector2(220f, 56f);

            // WO-603: under the Solana/$SKR skin this corner is a wallet-connect entry, not Pi sign-in.
            // The full wallet-connect flow lives in DeNelle.Wallet (Core cannot reference it) and is a
            // flagged follow-up — the button routes through CurrencySkinResolver.RequestWalletConnect(),
            // which warns (no silent failure) until that handler is subscribed.
            bool walletSkin = _skin != null && _skin.AuthMode == SkinAuthMode.SolanaWallet;
            string initialLabel = walletSkin ? "Connect Wallet" : "Sign in with Pi";
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
        }

        private void SetButton(string text, bool interactable)
        {
            if (_label != null) _label.text = text;
            if (_button != null) _button.interactable = interactable;
        }
    }
}
