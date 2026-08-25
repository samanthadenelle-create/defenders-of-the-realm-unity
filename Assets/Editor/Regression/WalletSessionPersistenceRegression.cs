// =============================================================================
// WalletSessionPersistenceRegression - the wallet SESSION survives a relaunch,
// and the capability grant that makes that possible is never leaked.
// -----------------------------------------------------------------------------
// THE DEFECT (owner-reproduced on a real Seeker, 2026-08-17): connect wallet,
// play, force-quit, relaunch -> asked to connect again. The SAVE came back
// (GameState.BoundWallet is persisted and keys the row), so identity survived and
// only the MWA session did not: SolanaWalletProvider._authToken was documented
// "session-scoped only - never persisted", so on relaunch there was no grant to
// reauthorize against and MWA ran a full `authorize`, which IS the connect prompt.
//
// This oracle pins the fix AND the properties that make persisting a capability
// grant acceptable in the first place:
//   1. The store exists, seals the token with an AndroidKeyStore AES-GCM key, and
//      has NO plaintext fallback - off-device it persists NOTHING.
//   2. RUNTIME: Save() in the Editor returns false and writes nothing; a sentinel
//      token never reaches PlayerPrefs in any form.
//   3. RUNTIME: the token is BOUND to its wallet - Load() for a different address
//      discards the stored session instead of handing the grant over. Cross-keying
//      a save row is the worst outcome in this system.
//   4. RUNTIME: Clear() removes both halves (a player who disconnects is actually
//      disconnected).
//   5. The token NEVER appears in a log/trace string, in any of the three wallet
//      files that handle it. FlowTrace lines ride WebTraceSink -> api/trace.js into
//      analytics_events and plaintext Vercel logs, and F8 captures get shared, so a
//      logged grant is a real leak - the same rule that forced the guest-id
//      redaction on 2026-08-15.
//   6. The FULL AUTHORIZE FALLBACK still exists and disconnect still clears - a
//      failed/absent/expired resume must never be a dead end.
//   7. StubWalletProvider does not touch the store: a persisted session can never
//      make a stub address look like a connected, real signing wallet
//      (WalletIdentityRegression's invariant, protected from this direction too).
//   8. Wallet preference resolution defaults to Seeker, an installed stored choice
//      wins, and changing that choice clears both halves of the sealed session.
// Wire into DataRegression.RunAll as [wallet-session].
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    public static class WalletSessionPersistenceRegression
    {
        // Deliberately obvious, and deliberately NOT a real token shape - if this
        // string ever turns up in PlayerPrefs or a log, the leak is unmistakable.
        private const string SentinelToken = "SENTINEL-MWA-GRANT-MUST-NEVER-PERSIST";
        private const string WalletA = "BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV";
        private const string WalletB = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            string walletDir = Path.Combine(Application.dataPath, "_Modules/Wallet");
            string storePath = Path.Combine(walletDir, "MwaSessionStore.cs");
            string providerPath = Path.Combine(walletDir, "SolanaWalletProvider.cs");
            string scenarioPath = Path.Combine(walletDir, "TargetedLocalAssociationScenario.cs");
            string stubPath = Path.Combine(walletDir, "StubWalletProvider.cs");

            // -- 1. The store exists and SEALS, with no plaintext fallback -----
            string store = File.Exists(storePath) ? File.ReadAllText(storePath) : null;
            if (store == null)
            {
                failures.Add("MwaSessionStore.cs missing - the wallet session cannot survive a relaunch " +
                             "(the owner gets a connect prompt on every launch again)");
            }
            else
            {
                if (!store.Contains("AndroidKeyStore"))
                    failures.Add("MwaSessionStore no longer uses the AndroidKeyStore - the auth token is a " +
                                 "capability grant and must not sit in plaintext PlayerPrefs, which Android " +
                                 "auto-backup copies OFF THE DEVICE (no allowBackup=false is authored)");
                if (!store.Contains("AES/GCM/NoPadding"))
                    failures.Add("MwaSessionStore lost its AES-GCM transformation");
                if (!store.Contains("PlayerPrefs.SetString(TokenPrefsKey, cipherBlob)"))
                    failures.Add("MwaSessionStore no longer writes the SEALED blob to TokenPrefsKey - check that " +
                                 "the raw token cannot be written directly");
                if (Regex.IsMatch(store, @"SetString\(\s*TokenPrefsKey\s*,\s*authToken"))
                    failures.Add("MwaSessionStore writes the RAW auth token to PlayerPrefs - that is a wallet " +
                                 "capability grant in a cloud-backed plaintext store");
            }

            // -- 2/3/4. RUNTIME behaviour of the store -------------------------
            // Snapshot and restore: this suite must not disturb a real session.
            string savedToken = PlayerPrefs.GetString(MwaSessionStore.TokenPrefsKey, string.Empty);
            string savedAddr = PlayerPrefs.GetString(MwaSessionStore.AddressPrefsKey, string.Empty);
            try
            {
                // 2. FAIL CLOSED off-device: nothing persisted, and above all no
                //    plaintext. (SecureStorageAvailable is false in the Editor.)
                PlayerPrefs.DeleteKey(MwaSessionStore.TokenPrefsKey);
                PlayerPrefs.DeleteKey(MwaSessionStore.AddressPrefsKey);

                bool saved = MwaSessionStore.Save(SentinelToken, WalletA);
                if (MwaSessionStore.SecureStorageAvailable)
                {
                    // Only reachable on a device build of the suite; still assert the
                    // one thing that must hold everywhere.
                    if (PlayerPrefs.GetString(MwaSessionStore.TokenPrefsKey, string.Empty).Contains(SentinelToken))
                        failures.Add("SEALING FAILED: the raw token is readable in PlayerPrefs");
                }
                else
                {
                    if (saved)
                        failures.Add("MwaSessionStore.Save reported success with NO secure storage - it must " +
                                     "fail closed rather than fall back to plaintext");
                    if (!string.IsNullOrEmpty(PlayerPrefs.GetString(MwaSessionStore.TokenPrefsKey, string.Empty)))
                        failures.Add("MwaSessionStore.Save wrote a token with no secure storage available");
                }
                if (PlayerPrefs.GetString(MwaSessionStore.TokenPrefsKey, string.Empty).Contains(SentinelToken) ||
                    PlayerPrefs.GetString(MwaSessionStore.AddressPrefsKey, string.Empty).Contains(SentinelToken))
                    failures.Add("THE GRANT LEAKED: the sentinel token is stored in plaintext in PlayerPrefs");

                // 3. BINDING: a stored session must never be handed to another wallet.
                PlayerPrefs.SetString(MwaSessionStore.AddressPrefsKey, WalletA);
                PlayerPrefs.SetString(MwaSessionStore.TokenPrefsKey, "v1:AAAA:BBBB");

                if (!MwaSessionStore.MatchesStoredWallet(WalletA))
                    failures.Add("MwaSessionStore.MatchesStoredWallet rejected the wallet the session belongs to");
                if (MwaSessionStore.MatchesStoredWallet(WalletB))
                    failures.Add("CROSS-WALLET HOLE: MwaSessionStore.MatchesStoredWallet accepted a DIFFERENT " +
                                 "wallet - reusing a grant across wallets cross-keys the cloud save row");

                if (MwaSessionStore.Load(WalletB) != null)
                    failures.Add("CROSS-WALLET HOLE: Load() handed the stored grant to a different wallet");
                if (MwaSessionStore.HasStoredSession)
                    failures.Add("Load() detected a wallet mismatch but did NOT discard the stored session");

                // 4. Clear removes BOTH halves - disconnect means disconnected.
                PlayerPrefs.SetString(MwaSessionStore.AddressPrefsKey, WalletA);
                PlayerPrefs.SetString(MwaSessionStore.TokenPrefsKey, "v1:AAAA:BBBB");
                MwaSessionStore.Clear("regression");
                if (MwaSessionStore.HasStoredSession ||
                    !string.IsNullOrEmpty(MwaSessionStore.StoredAddress) ||
                    !string.IsNullOrEmpty(PlayerPrefs.GetString(MwaSessionStore.TokenPrefsKey, string.Empty)))
                    failures.Add("MwaSessionStore.Clear left a session behind - an explicit disconnect must " +
                                 "actually revoke the stored grant");

                // An empty store matches nothing, so no caller can resume from it.
                if (MwaSessionStore.MatchesStoredWallet(WalletA))
                    failures.Add("MwaSessionStore.MatchesStoredWallet returned true with NOTHING stored");
                if (MwaSessionStore.Load(WalletA) != null)
                    failures.Add("MwaSessionStore.Load returned a token with nothing stored");
            }
            finally
            {
                if (string.IsNullOrEmpty(savedToken)) PlayerPrefs.DeleteKey(MwaSessionStore.TokenPrefsKey);
                else PlayerPrefs.SetString(MwaSessionStore.TokenPrefsKey, savedToken);
                if (string.IsNullOrEmpty(savedAddr)) PlayerPrefs.DeleteKey(MwaSessionStore.AddressPrefsKey);
                else PlayerPrefs.SetString(MwaSessionStore.AddressPrefsKey, savedAddr);
                PlayerPrefs.Save();
            }

            // -- 5. WALLET PREFERENCE RESOLUTION + SEAL CLEAR ---------------
            // Snapshot every key this probe touches. It deliberately exercises
            // real PlayerPrefs because that is the production persistence seam.
            bool hadPreference = PlayerPrefs.HasKey(WalletPreferenceStore.PackagePrefsKey);
            string savedPreference = PlayerPrefs.GetString(
                WalletPreferenceStore.PackagePrefsKey, string.Empty);
            savedToken = PlayerPrefs.GetString(MwaSessionStore.TokenPrefsKey, string.Empty);
            savedAddr = PlayerPrefs.GetString(MwaSessionStore.AddressPrefsKey, string.Empty);
            const string Jupiter = "com.jup.ag";
            try
            {
                // No explicit choice: the unchanged rank-1 Seeker package wins,
                // even when another capable wallet appears first in the list.
                PlayerPrefs.DeleteKey(WalletPreferenceStore.PackagePrefsKey);
                string resolution;
                string package = TargetedLocalAssociationScenario.ResolveWalletPackage(
                    new[] { Jupiter, WalletPreferenceStore.DefaultPackage }, out resolution);
                if (package != WalletPreferenceStore.DefaultPackage || resolution != "chain rank 1")
                    failures.Add("WALLET PREFERENCE DEFAULT FAIL: no choice did not resolve Seeker at chain rank 1");

                // An explicit installed choice outranks the default chain.
                PlayerPrefs.SetString(WalletPreferenceStore.PackagePrefsKey, Jupiter);
                package = TargetedLocalAssociationScenario.ResolveWalletPackage(
                    new[] { WalletPreferenceStore.DefaultPackage, Jupiter }, out resolution);
                if (package != Jupiter || resolution != "stored choice")
                    failures.Add("WALLET PREFERENCE STORED FAIL: installed stored choice did not win");

                // Start from implicit Seeker with a sealed-session canary. The
                // internal store seam is what the public picker entry point calls
                // after enumerating installed handlers on Android.
                PlayerPrefs.DeleteKey(WalletPreferenceStore.PackagePrefsKey);
                PlayerPrefs.SetString(MwaSessionStore.AddressPrefsKey, WalletA);
                PlayerPrefs.SetString(MwaSessionStore.TokenPrefsKey, "v1:SEALED:CANARY");
                MethodInfo setter = typeof(WalletPreferenceStore).GetMethod(
                    "TrySetPreferredPackage", BindingFlags.Static | BindingFlags.NonPublic);
                if (setter == null)
                {
                    failures.Add("WALLET PREFERENCE SWITCH FAIL: TrySetPreferredPackage seam missing");
                }
                else
                {
                    object[] args = { Jupiter, new[] { Jupiter }, true, null };
                    bool changed = (bool)setter.Invoke(null, args);
                    if (!changed || PlayerPrefs.GetString(
                            WalletPreferenceStore.PackagePrefsKey, string.Empty) != Jupiter)
                        failures.Add("WALLET PREFERENCE SWITCH FAIL: confirmed installed choice was not persisted");
                    if (MwaSessionStore.HasStoredSession ||
                        !string.IsNullOrEmpty(MwaSessionStore.StoredAddress))
                        failures.Add("WALLET PREFERENCE SEAL FAIL: changing wallet left the old sealed session resumable");
                }
            }
            finally
            {
                if (hadPreference)
                    PlayerPrefs.SetString(WalletPreferenceStore.PackagePrefsKey, savedPreference);
                else
                    PlayerPrefs.DeleteKey(WalletPreferenceStore.PackagePrefsKey);
                if (string.IsNullOrEmpty(savedToken)) PlayerPrefs.DeleteKey(MwaSessionStore.TokenPrefsKey);
                else PlayerPrefs.SetString(MwaSessionStore.TokenPrefsKey, savedToken);
                if (string.IsNullOrEmpty(savedAddr)) PlayerPrefs.DeleteKey(MwaSessionStore.AddressPrefsKey);
                else PlayerPrefs.SetString(MwaSessionStore.AddressPrefsKey, savedAddr);
                PlayerPrefs.Save();
            }

            // -- 6. THE GRANT IS NEVER LOGGED ---------------------------------
            // Two independent lints, because a leak can arrive either way: a token
            // identifier on a log line, or a token identifier inside a string
            // interpolation hole (which some other line may then log).
            foreach (var path in new[] { storePath, providerPath, scenarioPath })
            {
                if (!File.Exists(path)) continue;
                string file = Path.GetFileName(path);
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                    string code = comment >= 0 ? line.Substring(0, comment) : line;

                    bool logs = code.Contains("FlowTrace.") || code.Contains("Debug.Log");
                    bool namesToken = Regex.IsMatch(code, @"\b(_?authToken|AuthToken|storedToken|cipherBlob)\b");
                    if (logs && namesToken)
                        failures.Add($"{file}:{i + 1} logs the MWA auth token - it is a capability grant and " +
                                     "FlowTrace lines reach analytics_events, Vercel plaintext logs and shared " +
                                     "F8 captures");

                    // NOTE the character classes exclude only newlines and are
                    // length-bounded, rather than excluding a literal close brace: an
                    // unpaired close brace inside a string literal still counts toward
                    // the project's brace-balance gate (CLAUDE.md s1) and would fail
                    // this file for nothing.
                    if (Regex.IsMatch(code, @"\{[^\r\n]{0,120}?\b(_?authToken|AuthToken|storedToken|cipherBlob)\b[^\r\n]{0,120}?\}"))
                        failures.Add($"{file}:{i + 1} interpolates the MWA auth token into a string - never build " +
                                     "a message that can carry the grant");
                }
            }

            // -- 7. Silent resume wired + full authorize fallback intact -------
            if (!File.Exists(providerPath))
            {
                failures.Add("SolanaWalletProvider.cs missing");
            }
            else
            {
                string prov = File.ReadAllText(providerPath);
                if (!prov.Contains("MwaSessionStore.Load("))
                    failures.Add("SolanaWalletProvider.Connect no longer loads the persisted session - the " +
                                 "2026-08-17 relaunch defect is back (connect prompt on every launch)");
                if (!prov.Contains("MwaSessionStore.Save("))
                    failures.Add("SolanaWalletProvider never persists the grant - nothing will survive a relaunch");
                if (!prov.Contains("TryResumeSession("))
                    failures.Add("SolanaWalletProvider lost TryResumeSession (the silent reauthorize path)");
                if (!prov.Contains("scenario.Authorize("))
                    failures.Add("SolanaWalletProvider lost the FULL AUTHORIZE fallback - a revoked or expired " +
                                 "grant would become a dead end with no way to connect");
                if (!Regex.IsMatch(prov, @"UniTask Disconnect\(\)[\s\S]{0,3000}?MwaSessionStore\.Clear\("))
                    failures.Add("SolanaWalletProvider.Disconnect no longer clears the stored session - a player " +
                                 "who disconnects would be silently reconnected on the next launch");
            }

            if (!File.Exists(scenarioPath))
                failures.Add("TargetedLocalAssociationScenario.cs missing");
            else if (!File.ReadAllText(scenarioPath).Contains("client.Reauthorize("))
                failures.Add("TargetedLocalAssociationScenario lost the reauthorize call - there is no silent " +
                             "resume without it");

            // -- 8. The stub can never look connected from a stored session ----
            if (File.Exists(stubPath) && File.ReadAllText(stubPath).Contains("MwaSessionStore"))
                failures.Add("StubWalletProvider touches MwaSessionStore - a persisted grant must never make a " +
                             "stub address look like a real signing wallet (see WalletIdentityRegression)");

            if (failures.Count > 0)
            {
                reason = "WALLET SESSION FAIL - " + string.Join("; ", failures);
                return false;
            }
            reason = "WALLET SESSION OK - MWA grant sealed with an AndroidKeyStore AES-GCM key (fails closed " +
                     "off-device, never plaintext), bound to its wallet (a mismatch discards it), cleared on " +
                     "disconnect and wallet preference changes, Seeker default and stored-choice precedence pinned, " +
                     "never logged or interpolated, silent reauthorize wired with the full-authorize " +
                     "fallback intact, and the stub kept out of the store";
            return true;
        }
    }
}
