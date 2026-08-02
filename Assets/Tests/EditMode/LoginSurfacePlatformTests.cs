// =============================================================================
// LoginSurfacePlatformTests (WO-847) - the platform-conditional login surface.
// Owner ruling 2026-08-02: Android/Seeker login = "connect wallet or play as
// guest" (wallet-first, NO email form); desktop/web keep the WO-787/845 email
// layout. The seam (LoginSurfacePlatform) and the wallet bridge
// (LoginWalletBridge) live in DeNelle.Core, so this asmdef tests them LIVE via
// the static override; the Onboarding/Wallet halves (this asmdef does not
// reference those assemblies) are pinned by source-lint, WO-845 style.
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
        [TearDown]
        public void ClearOverride()
        {
            LoginSurfacePlatform.LayoutOverride = null;
        }

        // -- The seam: override wins; live editor default is the email form ---

        [Test]
        public void Override_WalletFirst_ResolvesWalletFirst()
        {
            LoginSurfacePlatform.LayoutOverride = LoginSurfaceLayout.WalletFirst;
            Assert.AreEqual(LoginSurfaceLayout.WalletFirst, LoginSurfacePlatform.Resolve());
        }

        [Test]
        public void Override_EmailForm_ResolvesEmailForm()
        {
            LoginSurfacePlatform.LayoutOverride = LoginSurfaceLayout.EmailForm;
            Assert.AreEqual(LoginSurfaceLayout.EmailForm, LoginSurfacePlatform.Resolve());
        }

        [Test]
        public void NoOverride_OffAndroid_ResolvesEmailForm()
        {
            // EditMode runs on the editor platform (never RuntimePlatform.Android),
            // so the live branch must pick the desktop email layout.
            LoginSurfacePlatform.LayoutOverride = null;
            Assert.AreEqual(LoginSurfaceLayout.EmailForm, LoginSurfacePlatform.Resolve());
        }

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
        public void Panel_BranchesOnTheSeam_AndWalletFirstHasNoEmailControls()
        {
            string src = ReadSource("_Modules", "Onboarding", "LoginPanelController.cs");
            StringAssert.Contains("LoginSurfaceLayout.WalletFirst", src,
                "Build must branch on the platform seam");

            string walletFirst = MethodBlock(src, "private void BuildWalletFirst(");
            StringAssert.Contains("\"Connect Wallet\"", walletFirst, "wallet-first must present Connect Wallet");
            StringAssert.Contains("ObsidianButtonColor.Yellow", walletFirst,
                "Connect Wallet must be THE gold CTA (kit law: one gold button)");
            StringAssert.Contains("\"Play as Guest\"", walletFirst, "wallet-first must keep the guest escape");
            StringAssert.DoesNotContain("MakeInputField", walletFirst,
                "wallet-first must build NO email/password fields");
            StringAssert.DoesNotContain("Forgot password", walletFirst,
                "wallet-first has no forgot-password (the wallet is its own recovery)");

            string emailForm = MethodBlock(src, "private void BuildEmailForm(");
            StringAssert.Contains("MakeInputField", emailForm, "desktop email layout must keep its fields (WO-787)");
            StringAssert.Contains("Forgot password?", emailForm, "desktop email layout must keep forgot-password (WO-845)");
            StringAssert.Contains("\"Play as Guest\"", emailForm, "desktop email layout must keep the guest escape");
        }

        [Test]
        public void ViewModel_RoutesConnectThroughTheBridge_AndRebinds()
        {
            string src = ReadSource("_Modules", "Onboarding", "LoginViewModel.cs");
            StringAssert.Contains("LoginWalletBridge.ConnectAsync()", src,
                "VM must route wallet connect through the Core bridge (no duplicate connect logic)");
            StringAssert.Contains("BindPlayer(outcome.UserId)", src,
                "VM must idempotently bind the returned identity like the email paths");
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
