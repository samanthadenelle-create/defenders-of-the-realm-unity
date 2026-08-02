// =============================================================================
// AuthErrorMessagesTests (WO-845) — the PURE half of FirebaseAuthService.Explain.
// Proves the desktop "An internal error has occurred." wrapper maps to honest
// player strings via the REST markers (owner F8 seq 623: SignIn on an existing
// account surfaced the internal-error wrapper while the REST probe answered
// INVALID_LOGIN_CREDENTIALS), and that aggregate/inner unwrapping reaches the
// leaf. AuthErrorMessages deliberately has NO Firebase types, so this asmdef
// (overrideReferences, no Firebase DLLs) can test it directly.
// Plus a source-lint: the reset API exists on BOTH platform branches (WebGL stub
// + real) and Explain routes through the marker mapping — the halves this asmdef
// cannot compile against.
// =============================================================================
using System;
using System.IO;
using System.Text.RegularExpressions;
using DeNelle.Core.Auth;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    public sealed class AuthErrorMessagesTests
    {
        // ── Unwrap: aggregate/inner chains resolve to the leaf ──────────────

        [Test]
        public void Unwrap_ReachesLeaf_ThroughNestedAggregates()
        {
            var leaf = new InvalidOperationException("leaf");
            var wrapped = new AggregateException(new AggregateException(leaf));
            Assert.AreSame(leaf, AuthErrorMessages.Unwrap(wrapped));
        }

        [Test]
        public void Unwrap_WalksPlainInnerChain()
        {
            var leaf = new Exception("inner-most");
            var e = new Exception("outer", new Exception("mid", leaf));
            Assert.AreSame(leaf, AuthErrorMessages.Unwrap(e));
        }

        [Test]
        public void Unwrap_PlainExceptionPassesThrough()
        {
            var plain = new Exception("plain");
            Assert.AreSame(plain, AuthErrorMessages.Unwrap(plain));
        }

        // ── FromMarkers: the F8 seq 623 shape and friends ───────────────────

        [Test]
        public void DesktopInternalError_WithInvalidLoginCredentials_MapsToCredentialMismatch()
        {
            // The exact desktop failure shape: internal-error wrapper text carrying the
            // REST marker the backend actually answered.
            string raw = "An internal error has occurred. Response: {\"error\":{\"message\":\"INVALID_LOGIN_CREDENTIALS\"}}";
            Assert.AreEqual(AuthErrorMessages.CredentialMismatch, AuthErrorMessages.FromMarkers(raw));
        }

        [TestCase("blah INVALID_PASSWORD blah")]
        [TestCase("WRONG_PASSWORD")]
        [TestCase("invalid_login_credentials lowercase still maps")]
        [TestCase("INVALID_CREDENTIAL")]
        public void CredentialMarkers_AllMapToCredentialMismatch(string raw)
        {
            Assert.AreEqual(AuthErrorMessages.CredentialMismatch, AuthErrorMessages.FromMarkers(raw));
        }

        [Test]
        public void UserNotFoundMarker_MapsToHonestMessage()
        {
            Assert.AreEqual(AuthErrorMessages.UserNotFound, AuthErrorMessages.FromMarkers("x EMAIL_NOT_FOUND x"));
            Assert.AreEqual(AuthErrorMessages.UserNotFound, AuthErrorMessages.FromMarkers("USER_NOT_FOUND"));
        }

        [Test]
        public void OtherMarkers_MapToTheirStrings()
        {
            Assert.AreEqual(AuthErrorMessages.EmailInUse, AuthErrorMessages.FromMarkers("EMAIL_EXISTS"));
            Assert.AreEqual(AuthErrorMessages.WeakPassword, AuthErrorMessages.FromMarkers("WEAK_PASSWORD : should be 6"));
            Assert.AreEqual(AuthErrorMessages.TooManyAttempts, AuthErrorMessages.FromMarkers("TOO_MANY_ATTEMPTS_TRY_LATER"));
            Assert.AreEqual(AuthErrorMessages.ProviderDisabled, AuthErrorMessages.FromMarkers("OPERATION_NOT_ALLOWED"));
        }

        [Test]
        public void NoMarker_ReturnsNull_SoCallerFallsBack()
        {
            Assert.IsNull(AuthErrorMessages.FromMarkers("A totally unrelated failure"));
            Assert.IsNull(AuthErrorMessages.FromMarkers(""));
            Assert.IsNull(AuthErrorMessages.FromMarkers(null));
        }

        // ── JoinMessages: markers can hide at any chain level ───────────────

        [Test]
        public void MarkerBuriedInInnerException_StillMaps_ViaJoinMessages()
        {
            var e = new AggregateException(
                new Exception("An internal error has occurred.", new Exception("INVALID_LOGIN_CREDENTIALS")));
            Assert.AreEqual(AuthErrorMessages.CredentialMismatch,
                AuthErrorMessages.FromMarkers(AuthErrorMessages.JoinMessages(e)));
        }

        // ── Fallback: generic path keeps the message but adds a next step ───

        [Test]
        public void Fallback_AppendsRetryHint_Once()
        {
            string outMsg = AuthErrorMessages.Fallback("An internal error has occurred.");
            StringAssert.StartsWith("An internal error has occurred.", outMsg);
            StringAssert.EndsWith(AuthErrorMessages.RetryHint, outMsg);
        }

        [Test]
        public void Fallback_EmptyMessage_StillReadsAsASentence()
        {
            string outMsg = AuthErrorMessages.Fallback(null);
            Assert.IsFalse(string.IsNullOrWhiteSpace(outMsg));
            StringAssert.EndsWith(AuthErrorMessages.RetryHint, outMsg);
        }

        // ── Source-lint: the halves this asmdef cannot compile against ──────
        // (The real Explain + the Firebase SDK call sit behind Firebase DLL refs this
        // test assembly deliberately excludes — assert their presence in source.)

        private static string ServiceSource()
        {
            string path = Path.Combine("Assets", "_Modules", "Core", "Auth", "FirebaseAuthService.cs");
            Assert.IsTrue(File.Exists(path), "FirebaseAuthService.cs not found at " + path);
            return File.ReadAllText(path);
        }

        [Test]
        public void Service_DeclaresResetApi_OnBothPlatformBranches()
        {
            string src = ServiceSource();
            int declarations = Regex.Matches(src,
                @"Task<AuthOutcome>\s+SendPasswordResetEmailAsync\(string email\)").Count;
            Assert.GreaterOrEqual(declarations, 2,
                "SendPasswordResetEmailAsync must exist on BOTH the WebGL stub and the real service (byte-identical API).");
            StringAssert.Contains("_auth.SendPasswordResetEmailAsync(email)", src,
                "real branch must call the Firebase SDK reset API");
        }

        [Test]
        public void Service_Explain_RoutesThroughMarkerMapping()
        {
            string src = ServiceSource();
            StringAssert.Contains("AuthErrorMessages.FromMarkers(AuthErrorMessages.JoinMessages(", src,
                "Explain must scan the raw chain text for REST markers when the enum maps nothing");
            StringAssert.Contains("AuthError.InvalidCredential", src,
                "Explain must map the InvalidCredential enum code to the credential-mismatch string");
        }
    }
}
