namespace DeNelle.Core.Platform
{
    /// <summary>
    /// WO-1318 - the ONE place that decides whether this artifact talks to the Pi TESTNET SANDBOX or
    /// to Pi MAINNET.
    ///
    /// <para>WHY IT EXISTS. The rule was authored on <c>PiSignInController.sandbox</c> (WO-1317) after
    /// a hardcoded <c>true</c> shipped a testnet build to a production Pi app and broke sign-in. The
    /// payment path now needs the SAME answer, and a second copy of a build-driven boolean is exactly
    /// the duplicated-state failure CLAUDE.md sec.2 / sec.5 keep re-teaching: the two copies drift, and
    /// the drift here means a player authenticates on mainnet and is asked to pay on testnet (or the
    /// reverse), which reads to them as a payment that simply does not work.</para>
    ///
    /// <para>Editor and DEVELOPMENT builds stay sandbox so testnet remains testable without a code
    /// edit; a SHIP build is mainnet. Deliberately NOT a runtime flag or a PlayerPrefs value - the
    /// environment must be decided by the ARTIFACT, not by state a device can carry over from a
    /// previous install.</para>
    /// </summary>
    public static class PiEnvironment
    {
        // ⛔ WO-1325 (owner, 2026-09-02, verbatim: "im on testnet"): THIS APP IS REGISTERED ON PI
        // TESTNET. Not build-driven any more - TESTNET IN EVERY BUILD, including a ship build.
        //
        // How we got here, so nobody re-litigates it:
        //   - WO-1317 flipped this to mainnet-in-ship-builds on the owner's answer "Mainnet" to a
        //     direct question, after the published client had shipped a hardcoded sandbox=true.
        //   - Nine captured Pi Browser sessions then showed `PiInit(sandbox=False)` followed by
        //     "Signed in as samanthadenelle" with zero failures, which read as proof of mainnet.
        //   - It was not proof. AUTHENTICATION IS NETWORK-TOLERANT; a Pioneer is the same Pioneer on
        //     either network. PAYMENTS ARE NOT. The Developer Portal badges this app `Testnet`, and
        //     the owner confirmed it directly.
        //
        // So a successful mainnet SIGN-IN says nothing about which network a PAYMENT settles on. A
        // payment created with sandbox=false against a Testnet-registered app does not settle where
        // the portal is looking - and the outstanding Developer Portal checklist item is precisely
        // "process a transaction on your app", which must happen ON TESTNET.
        //
        // ⚠ THE LESSON, because it cost two reversals in one day: a green auth capture is evidence
        // about AUTH ONLY. Do not generalise one subsystem's success into a claim about another.
        //
        // WHEN THE APP MOVES TO MAINNET: Pi documents the network as FIXED AT REGISTRATION ("once you
        // register the app, this option cannot be changed"), so that move means a NEW portal project
        // and a new API key - not an edit here alone. Change this line in the same commit as that
        // migration, never before it. See docs/reference/PI_AD_NETWORK_APPROVAL.md.
        /// <summary>True = Pi Testnet sandbox. The app is registered on Testnet (WO-1325).</summary>
        public const bool Sandbox = true;

        /// <summary>ASCII label for trace lines. TMP renders non-ASCII as tofu, so keep it plain.</summary>
        public static string Label => Sandbox ? "sandbox(testnet)" : "mainnet";
    }
}
