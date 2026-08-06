// =============================================================================
// WalletIdentityAndPlatformTest (EditMode) - locks the three wallet-lane defects
// found on 2026-08-05/06 so none of them can silently come back.
// -----------------------------------------------------------------------------
// These are PURE assertions over constants and provider selection. No scene, no
// device, no network, no SDK call - so they run in the normal EditMode gate and
// give us the only proof available before the owner can retest on hardware.
//
// WHAT EACH GROUP GUARDS
//
//  1. DAPP IDENTITY (approval-sheet branding).
//     An MWA wallet renders the approval sheet from the authorize request's
//     `identity` object - name, uri, icon (MobileWalletAdapterClient.cs:70-86 ->
//     JsonRequest.cs:18-37). Every one of those JSON properties is
//     NullValueHandling.Ignore, so a blank field is silently DROPPED from the
//     wire and the wallet quietly substitutes its own branding. That failure is
//     invisible at runtime, which is exactly why it must be a test.
//     The icon is additionally required to be a RELATIVE path resolved against
//     the identity URI, with no leading slash - a leading slash is what
//     append-style wallet resolvers turn into a doubled slash and a 404.
//
//  2. PLATFORM SELECTION (F8 capture 2026-08-06 10:41:22, Windows standalone).
//     SOLANA_SDK is defined on EVERY target by a platform-independent
//     versionDefine in DeNelle.Wallet.asmdef, NOT only on Android as the old
//     selector's comment claimed. Selecting the real provider off the define
//     therefore handed the Windows exe a provider whose Connect can only throw.
//     Selection must key off IsSupportedOnThisPlatform, which is compiled from
//     the same #if as the working Connect body.
//
//  3. CLOUD-SYNC ATTESTATION (must NOT regress while fixing 2).
//     Making desktop fall back to the stub is only safe while the stub still
//     fails the attestation gate. If IsRealSigningWallet ever went true for a
//     StubWalletProvider, every SDK-less device would key the SAME cloud save
//     row. Local-only on desktop is the CORRECT outcome, and this locks it.
// =============================================================================

using System;
using NUnit.Framework;
using DeNelle.Wallet;

namespace DeNelle.Wallet.Tests
{
    [TestFixture]
    public class WalletIdentityAndPlatformTest
    {
        // =====================================================================
        //  1. Dapp identity - the approval sheet's branding
        // =====================================================================

        [Test]
        public void identity_name_is_our_game_and_is_ascii()
        {
            var name = SolanaWalletProvider.DappIdentityName;

            Assert.IsFalse(string.IsNullOrEmpty(name),
                "A blank identity name is dropped from the authorize JSON and the wallet shows ITS OWN branding.");

            // ASCII only: this string crosses a JSON-RPC channel into a wallet app
            // we do not control, and rides the FlowTrace logs. Non-ASCII has no
            // guaranteed rendering on the far side.
            foreach (var c in name)
            {
                Assert.Less((int)c, 128,
                    "Identity name must be ASCII-only - it is rendered by a third-party wallet app.");
            }

            Assert.AreEqual("Echoes of Elarion", name,
                "The approval sheet must name the game the player is actually playing (owner-approved string).");
        }

        [Test]
        public void identity_uri_is_absolute_https_and_ends_with_a_slash()
        {
            var uri = SolanaWalletProvider.DappIdentityUri;

            Assert.IsFalse(string.IsNullOrEmpty(uri), "A blank identity uri means the wallet cannot verify us at all.");

            // HARD SDK CONSTRAINT: MobileWalletAdapterClient.cs:62-65 throws
            // ArgumentException on a non-absolute identity uri, before any I/O.
            Assert.IsTrue(Uri.IsWellFormedUriString(uri, UriKind.Absolute),
                "Identity uri MUST be absolute - the SDK client rejects anything else.");

            var parsed = new Uri(uri);
            Assert.AreEqual("https", parsed.Scheme,
                "Digital Asset Links verification is HTTPS-only.");

            // A trailing slash keeps relative icon resolution predictable: without
            // it, RFC-3986 resolution drops the last path segment.
            Assert.IsTrue(uri.EndsWith("/", StringComparison.Ordinal),
                "Identity uri must end with '/' so the relative icon path resolves against the ROOT.");
        }

        [Test]
        public void identity_icon_is_relative_has_no_leading_slash_and_resolves_onto_the_identity_host()
        {
            var iconUri = SolanaWalletProvider.DappIconUri;

            Assert.IsFalse(string.IsNullOrEmpty(iconUri),
                "A blank icon is dropped from the JSON and the wallet draws its own placeholder art.");

            // HARD SDK CONSTRAINT: MobileWalletAdapterClient.cs:66-69 throws
            // ArgumentException if the icon Uri is absolute.
            Assert.IsFalse(Uri.IsWellFormedUriString(iconUri, UriKind.Absolute),
                "Icon MUST be relative - the SDK client rejects an absolute icon uri.");

            // The MWA spec defines this as a path relative to the identity uri, and
            // the reference wallets resolve it by APPENDING. A leading slash then
            // produces 'https://host//icon.png' and a 404 - which costs us the
            // branding without any error anywhere. (The SDK's own default,
            // SolanaMobileWalletAdapter.cs:19, has this exact bug.)
            Assert.AreNotEqual('/', iconUri[0],
                "Icon path must not start with '/' - append-style wallet resolvers produce a doubled slash.");

            // And it must land back on the SAME host that serves the Digital Asset
            // Links statement, or the wallet fetches art from somewhere unverified.
            var resolved = new Uri(new Uri(SolanaWalletProvider.DappIdentityUri), iconUri);
            Assert.AreEqual(new Uri(SolanaWalletProvider.DappIdentityUri).Host, resolved.Host,
                "The icon must resolve onto the identity host.");
            Assert.IsFalse(resolved.AbsolutePath.StartsWith("//", StringComparison.Ordinal),
                "Resolved icon path must not contain a doubled slash.");
        }

        [Test]
        public void identity_uri_and_icon_survive_the_sdk_constructor_calls()
        {
            // Exactly the two constructions TargetedLocalAssociationScenario makes
            // before handing them to the SDK client. If either throws, connect dies
            // before a single byte reaches the wallet - so prove them here rather
            // than on the owner's device.
            Assert.DoesNotThrow(() =>
            {
                var _ = new Uri(SolanaWalletProvider.DappIdentityUri);
            }, "new Uri(identityUri) must not throw.");

            Assert.DoesNotThrow(() =>
            {
                var _ = new Uri(SolanaWalletProvider.DappIconUri, UriKind.Relative);
            }, "new Uri(iconUri, UriKind.Relative) must not throw.");
        }

        [Test]
        public void wallet_preference_chain_puts_the_seeker_wallet_first_and_jupiter_last()
        {
            // Owner ruling 2026-08-05: on a Seeker, the Seeker's own wallet is the
            // primary target. Jupiter is the incumbent Android was silently
            // electing via the implicit intent, so it must stay LAST.
            var chain = TargetedLocalAssociationScenario.PreferredWalletPackages;

            Assert.IsNotNull(chain);
            Assert.Greater(chain.Length, 0, "The preference chain must not be empty.");
            Assert.AreEqual("com.solanamobile.wallet", chain[0],
                "Rank 1 must be the Seeker's own Seed Vault wallet (owner ruling).");
            Assert.AreEqual("ag.jup.jupiter.android", chain[chain.Length - 1],
                "Jupiter must stay LAST in the preference chain (owner ruling).");
        }

        // =====================================================================
        //  2 + 3. Platform selection and the cloud-sync attestation gate
        // =====================================================================

        [Test]
        public void real_provider_is_never_supported_in_the_editor()
        {
            // This fixture is Editor-only (asmdef includePlatforms: Editor), so
            // IsSupportedOnThisPlatform is compiled with UNITY_EDITOR set and MUST
            // be false. Mobile Wallet Adapter needs a real device: an editor session
            // that selected the real provider would throw NotSupportedException out
            // of Connect, which is precisely the desktop defect (F8 2026-08-06).
            Assert.IsFalse(SolanaWalletProvider.IsSupportedOnThisPlatform,
                "The real MWA provider must never be selected in the Editor.");
        }

        [Test]
        public void auto_selected_provider_falls_back_to_the_stub_off_device()
        {
            // The auto-selecting constructor is the one the game uses
            // (WalletSkinBootstrap:68 and :125). Off-device it must resolve to the
            // stub rather than to a provider whose Connect can only throw.
            var service = new WalletService();

            Assert.AreEqual("Devnet Stub Wallet", service.ProviderName,
                "Off-device, WalletService must auto-select StubWalletProvider - not error at Connect time.");
        }

        [Test]
        public void stub_never_attests_a_cloud_save_identity()
        {
            // THE GUARD ON THE FIX. Desktop falling back to the stub is only safe
            // while the stub cannot attest. A true here would let every SDK-less
            // build key the SAME cloud player_data row.
            var service = WalletService.Create(useStub: true);

            Assert.IsFalse(service.IsRealSigningWallet,
                "The devnet stub must NEVER count as a real signing wallet - it would key a shared cloud save row.");

            // Disconnected to begin with, so the gate is false for that reason too;
            // the load-bearing half is the provider-type test inside the property.
            Assert.IsFalse(service.IsConnected, "A freshly created service is not connected.");
        }

        [Test]
        public void sdk_define_alone_does_not_imply_the_platform_is_supported()
        {
            // The exact confusion that caused the desktop defect: SOLANA_SDK is set
            // by a platform-independent versionDefine in DeNelle.Wallet.asmdef, so
            // IsSdkAvailable can be true on a platform that cannot run MWA. The two
            // properties must therefore be allowed to disagree, and the SUPPORTED
            // one must be the narrower of the two.
            if (SolanaWalletProvider.IsSupportedOnThisPlatform)
            {
                Assert.IsTrue(SolanaWalletProvider.IsSdkAvailable,
                    "Platform support implies the SDK is compiled in.");
            }

            // In this Editor fixture the narrow one is false whatever the define says.
            Assert.IsFalse(SolanaWalletProvider.IsSupportedOnThisPlatform);
        }
    }
}
