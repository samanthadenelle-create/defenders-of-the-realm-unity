using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Wallet
{
    /// <summary>
    /// Device-local wallet-app preference. This is not save data: changing it can
    /// select a different save identity, but never reads, moves, or re-keys a save.
    /// Presentation must show the kingdom warning and pass confirmed=true only
    /// after the player deliberately accepts it.
    /// </summary>
    public static class WalletPreferenceStore
    {
        public const string PackagePrefsKey = "dotr.mwa.preferred_package.v1";
        public const string DefaultPackage = "com.solanamobile.wallet";

        public static bool HasExplicitPreference => PlayerPrefs.HasKey(PackagePrefsKey);

        /// <summary>The explicit package, or empty when the default chain owns resolution.</summary>
        public static string StoredPackage =>
            PlayerPrefs.GetString(PackagePrefsKey, string.Empty).Trim();

        /// <summary>
        /// The wallet attached to the sealed session. This is exposed for picker
        /// identification only; it is not read from or written to GameState.
        /// </summary>
        public static string CurrentSessionWalletAddress => MwaSessionStore.StoredAddress;

        /// <summary>
        /// Persist a package only after the UI's deliberate kingdom-switch confirm.
        /// The package must be one of the currently installed MWA handlers.
        /// A real change clears the sealed grant before returning.
        /// </summary>
        internal static bool TrySetPreferredPackage(
            string packageName,
            IReadOnlyList<string> installedPackages,
            bool kingdomSwitchConfirmed,
            out string reason)
        {
            packageName = (packageName ?? string.Empty).Trim();
            if (!kingdomSwitchConfirmed)
            {
                reason = "wallet switch requires deliberate kingdom confirmation";
                FlowTrace.Warn("Wallet", "MWA preference change refused: " + reason + ".");
                return false;
            }
            if (packageName.Length == 0 || !Contains(installedPackages, packageName))
            {
                reason = "wallet package is not an installed MWA handler";
                FlowTrace.Warn("Wallet", "MWA preference change refused: " + reason + ".");
                return false;
            }

            string before = StoredPackage;
            string effectiveBefore = before.Length == 0 ? DefaultPackage : before;
            if (string.Equals(effectiveBefore, packageName, StringComparison.OrdinalIgnoreCase))
            {
                // Persist an explicit Seeker selection without destroying a valid
                // Seeker session: the effective wallet did not change.
                PlayerPrefs.SetString(PackagePrefsKey, packageName);
                PlayerPrefs.Save();
                reason = "preference already selected";
                FlowTrace.Step("Wallet", "MWA preference unchanged: package=" + packageName + ".");
                return true;
            }

            PlayerPrefs.SetString(PackagePrefsKey, packageName);
            PlayerPrefs.Save();
            MwaSessionStore.Clear("wallet package preference changed from " +
                (effectiveBefore.Length == 0 ? "<implicit>" : effectiveBefore) + " to " + packageName);
            reason = "preference changed and sealed session cleared";
            FlowTrace.Step("Wallet", "MWA preference changed: package=" + packageName +
                " reason=player-confirmed; sealed session cleared.");
            return true;
        }

        private static bool Contains(IReadOnlyList<string> values, string wanted)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], wanted, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
