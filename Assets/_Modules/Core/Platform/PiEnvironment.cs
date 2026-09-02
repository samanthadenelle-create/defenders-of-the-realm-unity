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
        /// <summary>True = Pi Testnet sandbox. Build-driven; never assign, never override at runtime.</summary>
        public const bool Sandbox =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        /// <summary>ASCII label for trace lines. TMP renders non-ASCII as tofu, so keep it plain.</summary>
        public static string Label => Sandbox ? "sandbox(testnet)" : "mainnet";
    }
}
