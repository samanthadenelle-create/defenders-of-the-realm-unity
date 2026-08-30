// =============================================================================
// ArtifactVariantStamp — WO-1282. Puts the ARTIFACT VARIANT into the device log,
// so a capture answers "which build is this?" without dumpsys, without a rebuild,
// and without the owner's eyes (§14).
// -----------------------------------------------------------------------------
// WHY: the Play and Seeker artifacts are built from ONE tree, differ only by a
// compile define (GOOGLE_PLAY vs DAPP_STORE), and carry the SAME package id and
// the SAME version stamp scheme. Installed side by side they are visually
// identical. §16 already cost this project three incidents where the installed
// build was not the build anyone thought it was; this line makes the variant a
// captured FACT in break-log.jsonl instead of an inference.
//
// It also states whether the wallet payloads that GooglePlayContentExclusion
// quarantines actually made it in — so a Seeker build that silently lost the rail
// (the regression that would be WORSE than the contamination) announces itself on
// the FIRST launch rather than at the first Connect Wallet tap.
//
// Deliberately allocation-light and exception-free: it runs before the first scene.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Core.Payments
{
    /// <summary>Logs the compile-stamped artifact variant once, at player startup.</summary>
    public static class ArtifactVariantStamp
    {
        private const string System = "ArtifactVariant";

        /// <summary>The variant name this binary was compiled as. Mirrors
        /// <see cref="PaymentChannelResolver.ResolveStampedChannel"/>'s define ladder, and is
        /// deliberately a separate, plain string so a log reader needs no enum table.</summary>
        public static string VariantName
        {
            get
            {
#if GOOGLE_PLAY && DAPP_STORE
                return "INVALID(GOOGLE_PLAY+DAPP_STORE)";
#elif GOOGLE_PLAY
                return "GOOGLE_PLAY";
#elif DAPP_STORE
                return "DAPP_STORE";
#else
                return "UNSTAMPED";
#endif
            }
        }

        /// <summary>True when the Solana wallet rail is compiled into this artifact.
        /// GOOGLE_PLAY artifacts must report false; DAPP_STORE artifacts must report true.</summary>
        public static bool WalletRailExpected
        {
#if GOOGLE_PLAY
            get { return false; }
#else
            get { return true; }
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Stamp()
        {
            // Resources.Load, not a type reference: DeNelle.Wallet does not exist in a
            // GOOGLE_PLAY player (its asmdef carries "!GOOGLE_PLAY"), so naming any type from
            // it here would not compile. The wallet PAYLOAD is what we can observe from Core,
            // and the payload is exactly what GooglePlayContentExclusion moves.
            bool walletPayloadPresent = Resources.Load<TextAsset>("Data/Canonical/wallets") != null;

            string line = $"variant={VariantName} walletRailExpected={WalletRailExpected} " +
                          $"walletPayloadPresent={walletPayloadPresent} " +
                          $"version={Application.version} platform={Application.platform}";

            if (walletPayloadPresent == WalletRailExpected)
            {
                FlowTrace.Step(System, line);
                return;
            }

            if (WalletRailExpected)
            {
                // A Seeker build with no wallet payload — the exact regression the Play
                // exclusion mechanism must never cause. Loud, and named, on first launch.
                FlowTrace.Fail(System, line + " — SEEKER BUILD IS MISSING THE WALLET PAYLOAD. " +
                                      "A GooglePlayContentExclusion quarantine was almost certainly left " +
                                      "in the tree by an interrupted Play build. DO NOT SHIP THIS BINARY.");
                return;
            }

            // A Play build that still carries the payload — the contamination itself.
            FlowTrace.Fail(System, line + " — GOOGLE_PLAY ARTIFACT STILL CARRIES wallets.json. " +
                                  "GooglePlayContentExclusion did not run. DO NOT UPLOAD THIS BINARY.");
        }
    }
}
