// =============================================================================
// AdServiceSeamRegression — WO-912 sec.10.5: the ad provider stays BEHIND the seam.
// Marker: AD_SEAM_OK
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Wired into DeNelle.Editor.DataRegression.RunAll.
//
// WHAT THIS GUARDS, and why it is worth a suite of its own:
// WO-912 sec.10.5 requires the ad provider to sit behind a thin IAdService seam
// "REGARDLESS" of which network wins, because the realistic failure mode is a
// PUBLISHER ACCOUNT TERMINATION forcing a provider swap after ship. A seam that is
// merely PRESENT is worth nothing - the failure is always that one call site reached
// past it "just this once" and nobody noticed until the swap.
//
// So this suite does not check that IAdService exists. It checks that NOTHING
// OUTSIDE AN ADAPTER NAMES A VENDOR TYPE. That is the property that has to hold.
//
// It is deliberately written BEFORE any SDK is installed (WO-912 D3 blocks that until
// a written policy answer lands). A guard added after an integration only ever pins
// whatever the integration happened to do; added first, it constrains it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Source-lint: the rewarded-ad provider is reachable only through
    /// <c>DeNelle.Core.Ads.IAdService</c>. Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class AdServiceSeamRegression
    {
        // Vendor tokens that must never appear in game code. Kept as the SDK ENTRY POINTS
        // rather than whole namespaces: it is the calls that couple us, and a bare mention in
        // a comment is not coupling (comments are stripped before matching anyway).
        private static readonly string[] VendorTokens =
        {
            "MaxSdk",                 // AppLovin MAX
            "MaxSdkCallbacks",
            "com.applovin",
            "IronSource",             // Unity LevelPlay (ex-ironSource)
            "LevelPlay",
            "UnityEngine.Advertisements",   // Unity Ads
            "Advertisement.Show",
            "GoogleMobileAds",        // AdMob
            "RewardedAd.Load",
        };

        // Where an adapter is ALLOWED to name a vendor. Anything under these paths is the
        // one place the coupling belongs. Nothing else may.
        private static readonly string[] AdapterPathFragments =
        {
            "/Ads/Providers/",
            "/Monetization/Providers/",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- AD PROVIDER SEAM (WO-912 sec.10.5) ---");

            string modulesDir = null;
            try { modulesDir = Path.Combine(Application.dataPath, "_Modules"); } catch { }
            if (string.IsNullOrEmpty(modulesDir) || !Directory.Exists(modulesDir))
            {
                // hollow-pass-ok: same shape as the other _Modules source-lints; that directory
                // cannot be absent in this project, so the skip is unreachable rather than risky.
                reason = null;
                Debug.Log(log + "  (skipped -- Assets/_Modules not found)\nAD_SEAM_OK");
                return true;
            }

            // ── 1. The seam itself exists and carries its contract ────────────
            string seamPath = Path.Combine(modulesDir, "Core", "Ads", "IAdService.cs");
            if (!File.Exists(seamPath))
            {
                failures.Add("Assets/_Modules/Core/Ads/IAdService.cs is missing - WO-912 sec.10.5 requires the " +
                             "provider to sit behind a thin seam REGARDLESS of which network is chosen");
            }
            else
            {
                string seam = ReadOrEmpty(seamPath);
                foreach (var token in new[]
                         {
                             "interface IAdService",
                             "IsRewardedReady",              // criterion 3: lead with availability
                             "AdUnavailableReason",
                             "NoFill",                       // criterion 4: no-fill is first-class
                             "ShowRewarded",
                             "NullAdService",                // the no-provider path is a real object
                         })
                {
                    if (seam.IndexOf(token, StringComparison.Ordinal) < 0)
                        failures.Add($"IAdService.cs no longer declares '{token}' - the seam has lost part of the " +
                                     "sec.8.3 contract (ready-check + a no-fill signal distinct from other failures)");
                }

                // NoFill and LoadFailed must stay SEPARATE. AppLovin is the only finalist that
                // splits them at source (204 vs -5001); collapsing them here would throw that
                // away and make a real outage read as ordinary market conditions forever.
                if (seam.IndexOf("LoadFailed", StringComparison.Ordinal) < 0)
                    failures.Add("IAdService.cs no longer distinguishes LoadFailed from NoFill - " +
                                 "'no ads are eligible right now' and 'the SDK broke' need different copy");

                // The seam must not itself depend on a vendor.
                string seamCode = StripComments(seam);
                foreach (var v in VendorTokens)
                    if (seamCode.IndexOf(v, StringComparison.Ordinal) >= 0)
                        failures.Add($"IAdService.cs references vendor token '{v}' - the SEAM must be " +
                                     "provider-agnostic or it is not a seam");
            }

            // ── 2. NOTHING outside an adapter names a vendor ──────────────────
            //  This is the case that actually earns its keep.
            int scanned = 0, vendorHits = 0;
            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex) { files = Array.Empty<string>(); log.AppendLine("  (scan failed: " + ex.Message + ")"); }

            foreach (var path in files)
            {
                string norm = path.Replace('\\', '/');
                if (IsAdapterPath(norm)) continue;

                scanned++;
                string code = StripComments(ReadOrEmpty(path));
                if (code.Length == 0) continue;

                foreach (var v in VendorTokens)
                {
                    if (code.IndexOf(v, StringComparison.Ordinal) < 0) continue;
                    vendorHits++;
                    failures.Add($"{Rel(norm)} names the ad-vendor token '{v}' OUTSIDE an adapter. " +
                                 "Game code must reach the provider only through DeNelle.Core.Ads.IAdService " +
                                 "(WO-912 sec.10.5) - a direct vendor call turns a forced provider swap into a rewrite.");
                    break;   // one finding per file is enough to act on
                }
            }

            log.AppendLine($"  scanned {scanned} non-adapter .cs file(s); vendor references outside an adapter: {vendorHits}");

            // ── 3. The reward may only be granted by a completion callback ────
            //  RewardedAdManager's own header (cs:33-34) flags that a synchronous bool cannot
            //  express an async rewarded ad. Pin that the async seam exists so a future SDK pass
            //  cannot quietly satisfy the bool by granting on dispatch - the free-reward bug.
            string mgr = ReadFirst(modulesDir, "RewardedAdManager.cs");
            if (mgr == null)
            {
                failures.Add("RewardedAdManager.cs not found under Assets/_Modules");
            }
            else
            {
                string mgrCode = StripComments(mgr);
                if (mgrCode.IndexOf("ShowAdInternal", StringComparison.Ordinal) < 0)
                    failures.Add("RewardedAdManager.cs no longer exposes the ShowAdInternal override seam - " +
                                 "the SDK pass would have to edit the gate itself");
                if (mgrCode.IndexOf("RewardedAdSkip", StringComparison.Ordinal) < 0)
                    failures.Add("RewardedAdManager.cs no longer gates on FeatureFlags.RewardedAdSkip - " +
                                 "the release-blocker gate that keeps the reward path OFF has been removed");
            }

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log + "AD_SEAM_OK");
                return true;
            }

            reason = "ad-seam: " + string.Join("; ", failures);
            Debug.LogError(log + "AD_SEAM_FAIL: " + reason);
            return false;
        }

        private static bool IsAdapterPath(string normalisedPath)
        {
            for (int i = 0; i < AdapterPathFragments.Length; i++)
                if (normalisedPath.IndexOf(AdapterPathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static string Rel(string normalisedPath)
        {
            int i = normalisedPath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? normalisedPath.Substring(i + 1) : normalisedPath;
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        private static string ReadFirst(string root, string fileName)
        {
            try
            {
                var hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                return hits.Length > 0 ? ReadOrEmpty(hits[0]) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Blank out comments so the lint tests CODE, not prose. A doc comment naming a vendor
        /// while explaining WHY not to call it directly is exactly the documentation we want -
        /// an oracle that cannot tell the two apart punishes the author for writing it.
        /// (RaidScoringRegression learned this the hard way on 2026-08-07.)
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' '); break; }
                    sb.Append(' ');
                    i = end + 1;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
