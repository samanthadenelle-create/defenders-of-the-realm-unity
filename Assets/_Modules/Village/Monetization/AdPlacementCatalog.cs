// =============================================================================
// AdPlacementCatalog — WO-1120, THE PLACEMENT INTERPRETER that did not exist.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Monetization
//
// Assets/Resources/Data/Canonical/ad-placements.json has been the ad system's
// design authority since 2026-08-07 - three ENABLED placements, three rewards,
// two laws and a pile of hard-won rulings - and until this file NOTHING in
// Assets/**.cs read a single byte of it. Its own _status field said so. Every
// number in it (cooldowns, daily caps, ad unit ids, the covenant) was a comment.
//
// This is the reader. It parses the file, SCREENS IT AGAINST THE COVENANT, and
// hands AdGateService a validated table. The screen is not decoration:
//
//   _LAW_1  NO AD REWARD MAY GRANT CRYSTALS or any currency bought with real
//           money. This is the rule that keeps the ad ACCOUNT alive - AdMob
//           forbids rewards "directly convertible into direct monetary items"
//           and Unity forbids incentivising with "anything of value". Our
//           rewards pass only because minutes off a timer cannot be traded or
//           cashed out. A reward that fails this screen is DROPPED from the
//           table at load, so no placement can reach it, and the failure is a
//           FlowTrace.Fail - never a silent skip.
//
//   _LAW_2  The timeskip amount has ONE authority: BuildTimerConfig.adSkipSeconds.
//           The `seconds` in this file is a MIRROR for readability. We read the
//           config and WARN when the mirror has drifted; we never grant from the
//           JSON number. A second copy of a number is how the two drift and
//           nobody notices - the same duplicated-state failure CLAUDE.md sec.2
//           and sec.16 are written about.
//
// PARSING: UnityEngine.JsonUtility, which ignores the underscore-prefixed
// documentation keys (_comment, _LAW_*, _REMOVED_*) for free. That is why the
// removed-reward tombstones in the file cost nothing here. Entries that parse to
// a null id (the tombstone objects inside the rewards array) are dropped.
//
// WHAT THIS FILE IS NOT: it does not show ads, hold a ledger, or grant anything.
// It is a validated read of data. AdGateService owns policy and grants.
// =============================================================================
using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village.Monetization
{
    /// <summary>One reward definition from ad-placements.json, post-covenant-screen.</summary>
    [Serializable]
    public sealed class AdRewardDef
    {
        public string id;
        public string kind;              // currency | timeskip | harvest | buff | cosmeticTrial
        public AdGrantDef grant;
        public string description;
        public int maxStack;
    }

    /// <summary>
    /// The grant payload. Deliberately a SUPERSET of every kind's shape rather than a polymorphic
    /// hierarchy: JsonUtility cannot do polymorphism, and a superset makes the covenant screen
    /// trivial (one place holds every field a grant could carry, so nothing hides in a subtype).
    /// </summary>
    [Serializable]
    public sealed class AdGrantDef
    {
        public string service;           // informational: which service applies it
        public string kind;              // e.g. "skip-seconds"
        public float seconds;            // timeskip MIRROR - never the authority, see _LAW_2
        public string currency;          // currency grants only
        public int amount;
        public float multiplier;         // harvest grants
        public float durationSeconds;
    }

    /// <summary>One placement from ad-placements.json.</summary>
    [Serializable]
    public sealed class AdPlacementDef
    {
        public string id;
        public bool enabled;
        public string surface;
        public string adUnitId;
        public string rewardId;
        public int cooldownSeconds;
        public int dailyCap;             // 0 = unlimited
        public string requiresFlag;
        public int priority;
        public AdPromptDef prompt;
    }

    /// <summary>Player-facing copy for the offer button/modal.</summary>
    [Serializable]
    public sealed class AdPromptDef
    {
        public string headline;
        public string body;
        public string cta;
    }

    /// <summary>Global knobs (the sum-across-placements cap lives here).</summary>
    [Serializable]
    public sealed class AdGlobalDef
    {
        public int defaultCooldownSeconds = 480;
        public int hardDailyCap;         // 0 = unlimited
        public string adProvider = "levelplay";
        public bool respectDoNotSell = true;
        public string covenantLine;
    }

    [Serializable]
    internal sealed class AdPlacementsFile
    {
        public int version;
        public AdGlobalDef global;
        public AdRewardDef[] rewards;
        public AdPlacementDef[] placements;
    }

    /// <summary>
    /// Parsed, covenant-screened view of ad-placements.json. Loaded once per process and cached;
    /// call <see cref="Reload"/> from QA tooling if the file is edited at runtime.
    /// </summary>
    public static class AdPlacementCatalog
    {
        /// <summary>Resources path (no extension), matching every other canonical data file.</summary>
        public const string ResourcePath = "Data/Canonical/ad-placements";

        /// <summary>
        /// Currency ids an ad reward may NEVER grant (_LAW_1). "coins"/"gold" are absent on purpose:
        /// they are SOFT currency with no purchase route, which is exactly what makes them legal.
        /// glimmer is here because it is still SOLD in packs.json today - the file's own 2026-08-19
        /// tombstone records that its removed reward looked safe only because someone checked the
        /// SPEND side and not the SELL side. skr/usdc/sol are the money rails themselves.
        /// </summary>
        private static readonly string[] BannedCurrencies =
        {
            "crystal", "crystals", "aether", "aethercrystal", "aethercrystals",
            "gem", "gems", "glimmer", "skr", "usdc", "sol", "token", "tokens"
        };

        private static bool s_loaded;
        private static AdGlobalDef s_global;
        private static readonly Dictionary<string, AdRewardDef> s_rewards =
            new Dictionary<string, AdRewardDef>(StringComparer.Ordinal);
        private static readonly Dictionary<string, AdPlacementDef> s_placements =
            new Dictionary<string, AdPlacementDef>(StringComparer.Ordinal);
        private static readonly List<AdPlacementDef> s_ordered = new List<AdPlacementDef>();

        /// <summary>Global config; never null once <see cref="EnsureLoaded"/> has run.</summary>
        public static AdGlobalDef Global { get { EnsureLoaded(); return s_global; } }

        /// <summary>Every ENABLED placement that survived the covenant screen, highest priority first.</summary>
        public static IReadOnlyList<AdPlacementDef> Placements { get { EnsureLoaded(); return s_ordered; } }

        /// <summary>True when the file parsed and at least one placement survived.</summary>
        public static bool IsLoaded { get { EnsureLoaded(); return s_ordered.Count > 0; } }

        public static AdPlacementDef Placement(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;
            return s_placements.TryGetValue(id, out AdPlacementDef p) ? p : null;
        }

        public static AdRewardDef Reward(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;
            return s_rewards.TryGetValue(id, out AdRewardDef r) ? r : null;
        }

        /// <summary>The reward a placement pays, or null when the placement or its reward is gone.</summary>
        public static AdRewardDef RewardFor(AdPlacementDef placement) =>
            placement == null ? null : Reward(placement.rewardId);

        /// <summary>Drops the cache so the next read re-parses. QA/editor tooling only.</summary>
        public static void Reload()
        {
            s_loaded = false;
            s_global = null;
            s_rewards.Clear();
            s_placements.Clear();
            s_ordered.Clear();
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;
            s_global = new AdGlobalDef();

            // Guard, not try/catch-and-shrug: a malformed data file must LOG and leave the table
            // empty (every offer then refuses), never blank a screen silently (CLAUDE.md sec.12).
            Guard.Try("Ads", "AdPlacementCatalog.load", () =>
            {
                var ta = Resources.Load<TextAsset>(ResourcePath);
                if (ta == null || string.IsNullOrEmpty(ta.text))
                {
                    FlowTrace.Fail("Ads",
                        $"ad-placements.json NOT FOUND at Resources/{ResourcePath}. Every ad offer will " +
                        "refuse. This is a missing data file, not a network problem.");
                    return;
                }

                var file = JsonUtility.FromJson<AdPlacementsFile>(ta.text);
                if (file == null)
                {
                    FlowTrace.Fail("Ads", "ad-placements.json failed to parse - every ad offer will refuse.");
                    return;
                }

                if (file.global != null) s_global = file.global;

                int droppedRewards = 0;
                if (file.rewards != null)
                {
                    for (int i = 0; i < file.rewards.Length; i++)
                    {
                        var r = file.rewards[i];
                        // Tombstone objects (_REMOVED_*) parse to an all-null reward. Not an error.
                        if (r == null || string.IsNullOrEmpty(r.id)) continue;
                        if (!PassesCovenant(r)) { droppedRewards++; continue; }
                        s_rewards[r.id] = r;
                    }
                }

                if (file.placements != null)
                {
                    for (int i = 0; i < file.placements.Length; i++)
                    {
                        var p = file.placements[i];
                        if (p == null || string.IsNullOrEmpty(p.id)) continue;
                        s_placements[p.id] = p;
                        if (!p.enabled) continue;

                        if (!s_rewards.ContainsKey(p.rewardId ?? ""))
                        {
                            FlowTrace.Fail("Ads",
                                $"placement '{p.id}' is ENABLED but its reward '{p.rewardId}' is missing or " +
                                "was dropped by the covenant screen. The placement is DISABLED at load - an " +
                                "offer with nothing legal to pay must never reach a player.");
                            continue;
                        }
                        if (string.IsNullOrEmpty(p.adUnitId))
                            FlowTrace.Warn("Ads",
                                $"placement '{p.id}' is ENABLED with NO adUnitId - the network has nothing to " +
                                "serve. It will refuse at present-time rather than show a broken offer.");

                        s_ordered.Add(p);
                    }
                }

                s_ordered.Sort((a, b) => b.priority.CompareTo(a.priority));

                FlowTrace.Step("Ads",
                    $"AdPlacementCatalog loaded v{file.version}: {s_ordered.Count} live placement(s), " +
                    $"{s_rewards.Count} legal reward(s), {droppedRewards} dropped by _LAW_1, " +
                    $"provider='{s_global.adProvider}', hardDailyCap={s_global.hardDailyCap}. " +
                    "This file had NO reader before WO-1120.");
            });
        }

        /// <summary>
        /// _LAW_1 SCREEN. Returns false (and logs a Fail) for any reward that could pay premium
        /// currency. Runs at LOAD so an illegal reward is never in the table at all - the strongest
        /// available shape, because it means no future call site can reach it by any path.
        /// </summary>
        private static bool PassesCovenant(AdRewardDef r)
        {
            string kind = (r.kind ?? "").ToLowerInvariant();
            var g = r.grant;

            string currency = g != null ? (g.currency ?? "").Trim().ToLowerInvariant() : "";
            if (!string.IsNullOrEmpty(currency))
            {
                for (int i = 0; i < BannedCurrencies.Length; i++)
                {
                    if (!string.Equals(currency, BannedCurrencies[i], StringComparison.Ordinal)) continue;
                    FlowTrace.Fail("Ads",
                        $"⛔ _LAW_1 VIOLATION: reward '{r.id}' grants '{currency}', a currency bought with " +
                        "real money. DROPPED at load - it is not in the table and no placement can pay it. " +
                        "This is the rule that keeps the ad account alive (AdMob: no rewards 'directly " +
                        "convertible into direct monetary items'), not a balance preference.");
                    return false;
                }
            }

            if (kind == "currency" && string.IsNullOrEmpty(currency))
            {
                FlowTrace.Fail("Ads",
                    $"reward '{r.id}' is kind=currency with NO currency named. DROPPED: an unnamed currency " +
                    "cannot be screened against _LAW_1, and an unscreenable grant is refused, not trusted.");
                return false;
            }

            // A reward whose kind we have no grant path for is kept OUT of the table rather than
            // reaching a player as a button that pays nothing.
            if (kind != "currency" && kind != "timeskip" && kind != "harvest")
            {
                FlowTrace.Warn("Ads",
                    $"reward '{r.id}' has kind '{r.kind}', which has no grant path in AdGateService " +
                    "(only currency/timeskip/harvest are implemented). DROPPED so no offer promises it.");
                return false;
            }

            return true;
        }
    }
}
