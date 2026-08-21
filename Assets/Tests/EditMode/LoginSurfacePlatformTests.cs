// =============================================================================
// LoginSurfacePlatformTests (WO-847, rewritten by WO-837-B) - the login surface
// and the wallet bridge.
//
// ⛔ THE PLATFORM SPLIT THESE TESTS USED TO PIN IS RETIRED. WO-847 gave
// Android/Seeker a wallet-first surface and left desktop/web on the WO-787/845
// email form, and three tests here asserted exactly that (including
// "NoOverride_OffAndroid_ResolvesEmailForm"). Owner ruling 2026-08-21:
//   "That's only true with the Play Store, which we are not in. We are only in
//    the dApp Store, which is all wallet authentication based."
// So LoginSurfacePlatform / LoginSurfaceLayout / LayoutOverride are DELETED and
// those three tests are deleted with them - a test that pins a removed split is
// how the split grows back. What replaces them is a source-lint below asserting
// the panel has NO email/Google/reset controls left on ANY platform.
//
// The bridge (LoginWalletBridge) lives in DeNelle.Core, so this asmdef tests it
// LIVE; the Onboarding/Wallet halves (this asmdef does not reference those
// assemblies) stay pinned by source-lint, WO-845 style.
// =============================================================================
using System;
using System.IO;
using System.Threading.Tasks;
using DeNelle.Core.Auth;
using DeNelle.Core.Platform;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    public sealed class LoginSurfacePlatformTests
    {
        // -- The bridge: honest failure with no handler; clean pass-through ---

        [Test]
        public void Bridge_NoHandler_FailsHonestly_NeverSilently()
        {
            var saved = LoginWalletBridge.ConnectHandler;
            try
            {
                LoginWalletBridge.ConnectHandler = null;
                AuthOutcome outcome = LoginWalletBridge.ConnectAsync().GetAwaiter().GetResult();
                Assert.IsFalse(outcome.Success, "no handler must resolve a FAILED outcome");
                Assert.IsFalse(string.IsNullOrEmpty(outcome.Error), "the failure must carry a player-facing message");
            }
            finally { LoginWalletBridge.ConnectHandler = saved; }
        }

        [Test]
        public void Bridge_Handler_PassesOutcomeThrough()
        {
            var saved = LoginWalletBridge.ConnectHandler;
            try
            {
                LoginWalletBridge.ConnectHandler = () => Task.FromResult(
                    new AuthOutcome { Success = true, UserId = "FakeWa11etAddr", Error = string.Empty });
                AuthOutcome outcome = LoginWalletBridge.ConnectAsync().GetAwaiter().GetResult();
                Assert.IsTrue(outcome.Success);
                Assert.AreEqual("FakeWa11etAddr", outcome.UserId, "UserId must carry the wallet address");
            }
            finally { LoginWalletBridge.ConnectHandler = saved; }
        }

        [Test]
        public void Bridge_ThrowingHandler_MapsToHonestFailure()
        {
            var saved = LoginWalletBridge.ConnectHandler;
            try
            {
                // The bridge maps the throw to an honest Fail outcome AND FlowTrace.Fail's it
                // (Debug.LogError). Declare the expected error or the runner fails the test
                // for the very log line that proves the branch worked.
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("login wallet-connect handler threw"));
                LoginWalletBridge.ConnectHandler = () => throw new InvalidOperationException("boom");
                AuthOutcome outcome = LoginWalletBridge.ConnectAsync().GetAwaiter().GetResult();
                Assert.IsFalse(outcome.Success);
                Assert.IsFalse(string.IsNullOrEmpty(outcome.Error));
            }
            finally { LoginWalletBridge.ConnectHandler = saved; }
        }

        // -- Source-lint: the halves this asmdef cannot reference -------------
        // (DeNelle.Onboarding / DeNelle.Wallet are not referenced here; pin the
        //  wiring in source, WO-845 precedent.)

        private static string ReadSource(params string[] parts)
        {
            string path = Path.Combine("Assets", Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), path + " not found");
            return File.ReadAllText(path);
        }

        /// <summary>Extracts one private method's text: from its declaration to the
        /// next "private " declaration (or EOF). Coarse but stable for lint.</summary>
        private static string MethodBlock(string src, string declaration)
        {
            int start = src.IndexOf(declaration, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "declaration not found: " + declaration);
            int end = src.IndexOf("private ", start + declaration.Length, StringComparison.Ordinal);
            return end < 0 ? src.Substring(start) : src.Substring(start, end - start);
        }

        [Test]
        public void Panel_IsWalletAndGuestOnly_OnEveryPlatform()
        {
            string src = ReadSource("_Modules", "Onboarding", "LoginPanelController.cs");

            string surface = MethodBlock(src, "private void BuildWalletFirst(");
            StringAssert.Contains("\"Connect Wallet\"", surface, "the surface must present Connect Wallet");
            StringAssert.Contains("ObsidianButtonColor.Yellow", surface,
                "Connect Wallet must be THE gold CTA (kit law: one gold button)");
            StringAssert.Contains("\"Play as Guest\"", surface, "the surface must keep the guest escape");

            // WO-837-B: the email layout is GONE, not platform-gated. Lint the whole file -
            // a #if UNITY_ANDROID around a rebuilt email form would pass a per-method check.
            string code = StripComments(src);
            foreach (var banned in new[] { "BuildEmailForm", "MakeInputField", "TMP_InputField",
                                           "Forgot password", "Create Account", "Sign in with Google",
                                           "LoginSurfaceLayout", "FirebaseAuthService" })
                StringAssert.DoesNotContain(banned, code,
                    "the login surface must contain no '" + banned + "' - the wallet is the only identity " +
                    "(owner ruling 2026-08-21: dApp Store only, wallet authentication)");
        }

        [Test]
        public void ViewModel_RoutesConnectThroughTheBridge_AndBinds_WithNoOtherIdentityPath()
        {
            string src = ReadSource("_Modules", "Onboarding", "LoginViewModel.cs");
            StringAssert.Contains("LoginWalletBridge.ConnectAsync()", src,
                "VM must route wallet connect through the Core bridge (no duplicate connect logic)");
            StringAssert.Contains("BindPlayer(outcome.UserId)", src,
                "VM must idempotently bind the connected wallet address");

            string code = StripComments(src);
            foreach (var banned in new[] { "FirebaseAuthService", "SignUpAsync", "SendPasswordResetEmailAsync",
                                           "GoogleSignIn", "SignInWithGoogleCredentialAsync" })
                StringAssert.DoesNotContain(banned, code,
                    "the login VM must expose no '" + banned + "' - email/Google/Firebase are removed as " +
                    "player-facing identity paths (WO-837-B)");
        }

        /// <summary>Strips // and /* */ comments so a lint cannot be satisfied - or tripped -
        /// by prose. The doc headers here deliberately NAME the removed APIs.</summary>
        private static string StripComments(string src)
        {
            src = System.Text.RegularExpressions.Regex.Replace(src, @"/\*[\s\S]*?\*/", " ");
            return System.Text.RegularExpressions.Regex.Replace(src, @"//[^\r\n]*", " ");
        }

        [Test]
        public void WalletBootstrap_RegistersLoginHandler_BeforeTheSkinGate_AndBindsUnconditionally()
        {
            string src = ReadSource("_Modules", "Wallet", "WalletSkinBootstrap.cs");

            int register = src.IndexOf("LoginWalletBridge.ConnectHandler = ConnectForLoginAsync", StringComparison.Ordinal);
            int skinGate = src.IndexOf("if (!CurrencySkinResolver.IsSkr) return;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(register, 0, "WalletSkinBootstrap must register the login connect handler");
            Assert.GreaterOrEqual(skinGate, 0, "the SKR skin gate must remain for the corner-button path");
            Assert.Less(register, skinGate,
                "the login handler must register BEFORE the skin gate (skin-independent - WO-847)");

            string loginConnect = MethodBlock(src, "private static async Task<AuthOutcome> ConnectForLoginAsync(");
            StringAssert.Contains("BindWallet(", loginConnect,
                "the login connect path must bind the wallet address explicitly");
            StringAssert.DoesNotContain("BindIdentityOnAuth", loginConnect,
                "identity binding on the LOGIN path must never be gated by the optional skin config");
        }
    }
}
