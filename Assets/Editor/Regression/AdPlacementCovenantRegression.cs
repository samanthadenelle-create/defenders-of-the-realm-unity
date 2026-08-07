// =============================================================================
// AdPlacementCovenantRegression — ad-placements.json obeys the covenant AND the
// ad networks' reward policy. Marker: AD_COVENANT_OK
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Wired into DeNelle.Editor.DataRegression.RunAll.
//
// WHY THIS SUITE EXISTS, and why it is not merely a balance check:
// WO-912 sec.9.3 established that our rewarded ad is permitted by AdMob's and Unity's
// published terms for ONE reason — the reward is minutes off a build timer, and there is
// no path from it to money. AdMob forbids rewards "directly convertible into direct
// monetary items"; Unity forbids incentivising with "anything of value".
//
// CRYSTALS ARE THE SKR ON-RAMP. An ad that pays crystals makes the reward arguably
// convertible, and the policy protection evaporates along with the covenant. The cost of
// getting that wrong is not a balance complaint — it is a terminated publisher account.
//
// This is not hypothetical. On 2026-08-07, ad-placements.json shipped with
// `place.store.crystals` ENABLED, granting +150 crystals for watching a clip, and
// `reward.daily.bonusChest` granting +100 crystals inside a nested "bonus" object — while
// WO-912 was being prepared to tell Unity in writing that no ad reward can reach money.
// Nothing caught it because the file has NO INTERPRETER: no AdGateService exists and
// nothing under Assets/**.cs reads it. A spec with no reader has no runtime that can fail,
// so a static guard is the only thing that can hold it.
//
// The nested-bonus case is why grants are walked RECURSIVELY. A check on the top-level
// "currency" field would have passed reward.daily.bonusChest and shipped the violation.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class AdPlacementCovenantRegression
    {
        private const string Rel = "Assets/Resources/Data/Canonical/ad-placements.json";

        /// <summary>
        /// Currencies a player can obtain with real money, directly or through a chain. An ad may
        /// NEVER grant these. Kept as a deny-list rather than an allow-list ON PURPOSE: a new soft
        /// currency should not silently become ad-grantable just because nobody updated a list —
        /// but a new PREMIUM currency must be added here the day it is invented, and the
        /// [deny-list-current] case below forces that.
        /// </summary>
        private static readonly string[] PremiumCurrencies = { "crystals", "skr", "usdc", "sol", "gems" };

        /// <summary>Placements the owner ruled OUT. Their presence at all is the failure.</summary>
        private static readonly string[] RetiredPlacements = { "place.defeat.continue", "place.store.crystals" };

        /// <summary>
        /// Placements the owner has explicitly ruled LIVE. Adding to this list is a record of a
        /// PO decision, not a way to make a failure go away — an ad offer appearing in the game
        /// is a product change, and the smallness of this list is what makes that visible.
        ///
        /// 2026-08-07: place.build.skip (D4 original), then place.harvest.doubler and
        /// place.daily.chest when the owner reversed D4 ("its simple and im the only tester").
        /// </summary>
        private static readonly string[] RuledLivePlacements =
        {
            "place.build.skip",
            "place.harvest.doubler",
            "place.daily.chest",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- AD PLACEMENT COVENANT (WO-912 sec.9.3 + owner rulings D4/D7) ---");

            string path = null;
            try { path = Path.Combine(Application.dataPath, "Resources/Data/Canonical/ad-placements.json"); } catch { }
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                reason = $"ad-covenant: {Rel} not found - the rewarded-ad spec has been deleted or moved. " +
                         "If ads were genuinely removed, delete this suite in the same commit; do not leave it passing on absence.";
                Debug.LogError(log + "AD_COVENANT_FAIL: " + reason);
                return false;
            }

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                reason = $"ad-covenant: {Rel} is not valid JSON ({ex.GetType().Name}: {ex.Message})";
                Debug.LogError(log + "AD_COVENANT_FAIL: " + reason);
                return false;
            }

            var rewards = root["rewards"] as JArray ?? new JArray();
            var placements = root["placements"] as JArray ?? new JArray();
            log.AppendLine($"  {rewards.Count} reward(s), {placements.Count} placement(s)");

            // ── CASE 1 [no-premium-grant] — THE ONE THAT PROTECTS THE ACCOUNT ──
            foreach (var r in rewards)
            {
                string id = (string)r["id"] ?? "(no id)";
                var grant = r["grant"];
                if (grant == null) continue;

                foreach (var hit in FindPremiumCurrencies(grant))
                {
                    failures.Add($"[no-premium-grant] reward '{id}' grants '{hit}' - an ad reward may NEVER pay a " +
                                 "currency bought with real money. WO-912 sec.9.3: our placement is permitted ONLY " +
                                 "because the reward has no path to money. This makes it arguably convertible and " +
                                 "puts the publisher account at risk. Remove the grant; do not disable the placement " +
                                 "and leave it here.");
                }
            }

            // ── CASE 2 [retired-placement] — owner rulings D7 / covenant ───────
            foreach (var p in placements)
            {
                string id = (string)p["id"] ?? "(no id)";
                foreach (var retired in RetiredPlacements)
                {
                    if (!string.Equals(id, retired, StringComparison.Ordinal)) continue;
                    failures.Add($"[retired-placement] '{id}' is present. It was RETIRED by owner ruling on " +
                                 "2026-08-07 (D7 for place.defeat.continue - a battle-continue is combat power and " +
                                 "the covenant is convenience-only; place.store.crystals for the sec.9.3 reason). " +
                                 "Retired means DELETED, not disabled - a disabled row is one boolean away from live.");
                }
            }

            // ── CASE 3 [v1-scope] — which placements the owner has ruled LIVE ──
            //  D4 originally allowed only place.build.skip. The owner REVERSED that on 2026-08-07
            //  and re-enabled the harvest doubler and the daily chest. This case tracks the ruling
            //  rather than the original recommendation — a guard that keeps failing against a
            //  decision the PO has already made teaches people to ignore guards.
            //
            //  What it still catches is a placement going live that NOBODY ruled on. That is the
            //  real risk: the set is small and every member of it was a deliberate decision.
            var enabled = new List<string>();
            foreach (var p in placements)
                if (p["enabled"] != null && (bool)p["enabled"]) enabled.Add((string)p["id"] ?? "(no id)");

            log.AppendLine($"  enabled placements: {(enabled.Count == 0 ? "(none)" : string.Join(", ", enabled))}");

            foreach (var id in enabled)
            {
                if (Array.IndexOf(RuledLivePlacements, id) >= 0) continue;
                failures.Add($"[v1-scope] placement '{id}' is ENABLED but has not been ruled live. The live set is " +
                             $"[{string.Join(", ", RuledLivePlacements)}] (owner, 2026-08-07). Turning on an ad offer " +
                             "is a product decision the PO makes, not a config tweak - get the ruling, then add it here.");
            }

            if (!enabled.Contains("place.build.skip"))
                failures.Add("[v1-scope] 'place.build.skip' is not enabled. It is the queue-timer placement the whole " +
                             "WO-912 covenant and its near-miss economics are built on; if it is genuinely being " +
                             "retired that needs a ruling, not a disabled flag.");

            // ── CASE 4 [disabled-rows-still-legal] ────────────────────────────
            //  A disabled placement pointing at an illegal reward is a loaded gun with the safety
            //  on. Someone flipping `enabled` without reading the file must not be able to create a
            //  violation, so every placement's reward is checked regardless of enabled state.
            var rewardById = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (var r in rewards)
            {
                string id = (string)r["id"];
                if (!string.IsNullOrEmpty(id)) rewardById[id] = r;
            }

            foreach (var p in placements)
            {
                string pid = (string)p["id"] ?? "(no id)";
                string rid = (string)p["rewardId"];

                if (string.IsNullOrEmpty(rid) || !rewardById.TryGetValue(rid, out var reward))
                {
                    failures.Add($"[dangling-reward] placement '{pid}' references rewardId '{rid}' which does not " +
                                 "exist - a wired interpreter would offer an ad and grant nothing.");
                    continue;
                }

                foreach (var hit in FindPremiumCurrencies(reward["grant"]))
                    failures.Add($"[disabled-rows-still-legal] placement '{pid}' points at reward '{rid}' which " +
                                 $"grants '{hit}'. Even disabled, this is one boolean away from a policy violation.");
            }

            // ── CASE 5 [timeskip-single-authority] ────────────────────────────
            //  The JSON mirrors BuildTimerConfig.adSkipSeconds. Two copies of a number drift, and
            //  the drift is silent because nothing reads this file yet.
            float configSeconds = DeNelle.Core.Catalog.BuildTimerConfig.CreateDefault().adSkipSeconds;
            var skip = rewardById.TryGetValue("reward.build.timeskip", out var sk) ? sk : null;
            if (skip == null)
            {
                failures.Add("[timeskip-single-authority] reward 'reward.build.timeskip' is missing - it is the only " +
                             "reward the V1 placement can legally grant.");
            }
            else
            {
                var seconds = skip["grant"]?["seconds"];
                if (seconds == null)
                {
                    failures.Add("[timeskip-single-authority] reward.build.timeskip has no grant.seconds. If it has " +
                                 "gone back to an 'instant-build' grant, that hands away for free exactly what " +
                                 "crystals are sold for, and breaks WO-912 D1's near-miss math.");
                }
                else
                {
                    float jsonSeconds = (float)seconds;
                    log.AppendLine($"  timeskip: json={jsonSeconds:0}s  BuildTimerConfig.adSkipSeconds={configSeconds:0}s");
                    if (Math.Abs(jsonSeconds - configSeconds) > 0.5f)
                        failures.Add($"[timeskip-single-authority] ad-placements.json says {jsonSeconds:0}s but " +
                                     $"BuildTimerConfig.adSkipSeconds is {configSeconds:0}s. The CONFIG is the authority " +
                                     "(the shipping code path reads it); this file only mirrors it for readability. " +
                                     "Fix the mirror, not the config.");
                }
            }

            // ── CASE 6 [flag-gated] ───────────────────────────────────────────
            //  Every other path refuses ads while ff.rewardedadskip is OFF (BuildTimerService
            //  .WatchAdToSkip, RewardedAdManager.TryShowAd). A placement with an empty requiresFlag
            //  would let a wired interpreter offer one anyway - the exact free-reward hole the
            //  RELEASE BLOCKER GATE was added to close.
            foreach (var p in placements)
            {
                string pid = (string)p["id"] ?? "(no id)";
                string flag = (string)p["requiresFlag"];
                if (string.IsNullOrEmpty(flag))
                    failures.Add($"[flag-gated] placement '{pid}' has an empty requiresFlag. Every other path gates on " +
                                 "FeatureFlags.RewardedAdSkip; an ungated placement is how an interpreter offers an ad " +
                                 "the rest of the codebase refuses to honour.");
            }

            // ── CASE 7 [deny-list-current] ────────────────────────────────────
            //  Force the deny-list to keep up with the currencies that actually exist. If a new
            //  premium currency is added to packs.json and not here, case 1 goes quietly blind.
            if (Array.IndexOf(PremiumCurrencies, "crystals") < 0)
                failures.Add("[deny-list-current] 'crystals' has been removed from PremiumCurrencies - that is the " +
                             "SKR on-ramp and the single most important entry. Restoring it is not optional.");

            if (failures.Count == 0)
            {
                reason = $"ad covenant OK - {enabled.Count} enabled placement, no premium-currency grant, " +
                         $"timeskip mirrors config at {configSeconds:0}s";
                Debug.Log(log + "AD_COVENANT_OK");
                return true;
            }

            reason = "ad-covenant: " + string.Join("; ", failures);
            Debug.LogError(log + "AD_COVENANT_FAIL: " + reason);
            return false;
        }

        /// <summary>
        /// Every premium currency named anywhere inside a grant, at ANY depth. Recursive because
        /// reward.daily.bonusChest hid +100 crystals in a nested "bonus" object — a top-level
        /// "currency" check would have passed it and shipped the violation.
        /// </summary>
        private static IEnumerable<string> FindPremiumCurrencies(JToken grant)
        {
            var hits = new List<string>();
            Walk(grant, hits);
            return hits;
        }

        /// <summary>
        /// Explicit recursive descent over every string value in the token tree.
        /// Written by hand rather than with JToken.Descendants() because that extension is
        /// declared on JContainer, not JToken — a grant that is a bare value would not compile,
        /// and quietly casting to JContainer would skip it at runtime instead. The nesting is the
        /// entire point of this method, so the traversal is stated rather than borrowed.
        /// </summary>
        private static void Walk(JToken node, List<string> hits)
        {
            if (node == null) return;

            if (node.Type == JTokenType.String)
            {
                string v = node.Value<string>();
                if (string.IsNullOrEmpty(v)) return;

                for (int i = 0; i < PremiumCurrencies.Length; i++)
                {
                    if (string.Equals(v, PremiumCurrencies[i], StringComparison.OrdinalIgnoreCase))
                    {
                        string lower = v.ToLower(CultureInfo.InvariantCulture);
                        if (!hits.Contains(lower)) hits.Add(lower);
                        return;
                    }
                }
                return;
            }

            if (node is JProperty prop) { Walk(prop.Value, hits); return; }

            if (node is JContainer container)
                foreach (var child in container.Children())
                    Walk(child, hits);
        }
    }
}
