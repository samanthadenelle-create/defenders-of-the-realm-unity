// =============================================================================
// BattleMonthlyRegression [battle-monthly] -- the pay-to-win firewall of the
// Battle Pass + Monthly Ledger families, as a BUILD GATE.
// (WORK_ORDER_battle_and_monthly_packs section 6 + the owner rulings of 2026-08-21.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Contract mirrors the siblings:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: BATTLE_MONTHLY_OK (Debug.Log) / BATTLE_MONTHLY_FAIL (LogError)
//   registered ONCE inside DataRegression.RunAll's fenced registry region.
//
// =============================================================================
//  WHY THIS SUITE IS A GATE AND NOT A REVIEW CHECKLIST
// -----------------------------------------------------------------------------
// The covenant's promise -- convenience and beauty, never combat power -- is only
// worth what enforces it. A comment cannot stop a reward row, and a code review
// cannot run on every commit. So the ban list in the WO's section 2.3 is expressed
// here as assertions that FAIL THE BUILD: a revive, a heal, a damage or armor or
// crit or attack-speed or cooldown modifier, a permanent passive, a level or cap
// raise, an extra arena entry, a loot box, or an SKU that sells a tier.
//
// THE SUITE POLICES TWO OPPOSITE FAILURES AND BOTH MATTER:
//
//   * WHAT MAY NOT BE GRANTED -- the firewall. Combat power, a purchasable tier,
//     purchasable XP, a glimmer line, a randomized reward.
//
//   * WHAT CANNOT BE DELIVERED -- the vapor rule, which is the mirror the WO's own
//     2026-08-21 re-verification exposed. Cosmetics land on a preview-tint
//     fallback because no cosmetic art exists in the tree, and skr has no ledger to
//     credit at all. A season that pays out invisible flags for thirty days is
//     worse than a bad pack, because a pack disappoints once. So an authored
//     cosmetic_sku or skr row FAILS while its gate is shut.
//
// THE STRUCTURAL CASES ARE THE ONES THAT MATTER MOST, for the same reason the
// buy-gate suite says so: a value check ("no reward is combat power today") is
// cheap to satisfy and passes for the WRONG REASON the moment someone adds a
// reward by a route the check does not walk. So this suite also pins:
//   * [chokepoint]  BattleMonthlyCatalog.EnsureLoaded actually CALLS the firewall.
//                   A firewall that is never invoked is a comment.
//   * [xp-one-door] BattlePassService exposes exactly ONE way XP enters, it takes a
//                   battle OUTCOME rather than an amount, and no public AddXp(int)
//                   exists for anything else to call. Rule "XP is earned by
//                   playing" is only true if there is no second door.
//   * [no-countdown] the Monthly Ledger screen has no timer, in the SOURCE. Under
//                   the pool model nothing expires, so a clock would manufacture
//                   urgency over a deadline that does not exist.
//   * [one-screen-owner] each screen is implemented ONCE and that one owner is
//                   REACHABLE - a PanelRouter door plus the modal-arbiter lifecycle.
//                   Re-pointed 2026-08-21 from "refuse to let two rivals ship" to
//                   "assert the ruling held"; it still fails if a rival returns, if
//                   the retired BattleMonthlyPanels.cs comes back, or if a screen
//                   loses its door and ships unopenable.
//
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    /// <summary>Pins the battle-pass / monthly-card firewall, the vapor rule and the owner rulings.</summary>
    public static class BattleMonthlyRegression
    {
        private const string DataRelPath = "Data/Canonical/battle_monthly.json";

        /// <summary>Every reward kind the covenant sanctions. There is no combat kind, by design.</summary>
        private static readonly HashSet<string> SanctionedKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "economy", "convenience_token", "cosmetic_sku", "skr", "bundle",
        };

        /// <summary>The only economy keys a grant may carry. `glimmer` is ABSENT on purpose.</summary>
        private static readonly HashSet<string> AllowedEconomyKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "wood", "iron", "food", "crystals", "coins",
        };

        /// <summary>
        /// The WO section 2.3 ban list, as substrings scanned over the RAW file text.
        /// <para>Deliberately a TEXT scan as well as a structural walk: the structural walk can only
        /// reject a shape it knows about, and the point of a ban list is to catch the thing someone
        /// invented a new shape for. A false positive here costs one rename; a false negative ships
        /// pay-to-win.</para>
        /// </summary>
        private static readonly string[] BannedTokens =
        {
            "revive", "extralife", "extra_life", "continuetoken", "continue_token",
            "midbattle", "mid_battle", "heal", "shield", "potion",
            "damageboost", "damage_boost", "armorboost", "armor_boost",
            "critchance", "crit_chance", "attackspeed", "attack_speed",
            "cooldownreduction", "cooldown_reduction",
            "statboost", "stat_boost", "passivestat", "passive_stat",
            "levelcap", "level_cap", "capraise", "cap_raise",
            "lootbox", "loot_box", "gacha", "randomreward", "random_reward",
            "extraarenaentry", "extra_arena_entry",
            "tierskip", "tier_skip", "buytier", "buy_tier", "catchup", "catch_up",
            "buyxp", "buy_xp", "instantmaxpass", "instant_max_pass",
        };

        /// <summary>
        /// The anti-inflation anchors (WO section 6 invariant 6): a monthly card's FULL table vs the
        /// same-priced one-shot pack. Hardcoded here on purpose -- an oracle that read the ceiling
        /// out of the same file it is checking would assert nothing about it.
        /// <para>A card spread over thirty claims is legitimately worth MORE than an instant pack of
        /// the same price (patience has a price), but not an order of magnitude more, or a card buys
        /// the whole economy.</para>
        /// </summary>
        private const double DripCeilingMultiple = 2.5d;

        private static readonly KeyValuePair<string, string>[] CardAnchors =
        {
            new KeyValuePair<string, string>("monthly-wayfarer", "starters-hand"),
            new KeyValuePair<string, string>("monthly-keeper",   "folks-thanks"),
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BATTLE_MONTHLY_OK - " + reason);
            else Debug.LogError("BATTLE_MONTHLY_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== BattleMonthlyRegression [battle-monthly] (covenant firewall + vapor rule + rulings) ===");

            try
            {
                string rawText = CaseDualCopy(failures, log);

                if (rawText != null)
                {
                    CaseNoBannedTokens(rawText, failures, log);
                    CaseNoGlimmerAnywhere(rawText, failures, log);
                    CaseNoInlinedBinary(rawText, failures, log);

                    JObject root = null;
                    try { root = JObject.Parse(rawText); }
                    catch (Exception ex)
                    {
                        failures.Add("[parse] battle_monthly.json does not parse: " + ex.Message);
                    }

                    if (root != null)
                    {
                        CaseSeasons(root, failures, log);
                        CaseCards(root, failures, log);
                        CaseGrantsAreSanctioned(root, failures, log);
                        CaseAntiInflation(root, failures, log);
                    }
                }

                CaseCopy(failures, log);
                CaseChokepoint(failures, log);
                CaseXpHasOneDoor(failures, log);
                CaseNoCountdown(failures, log);
                CaseScreensAreCodeBuilt(failures, log);
                CaseOneScreenOwner(failures, log);
            }
            catch (Exception ex)
            {
                // NEVER throws (the suite contract): a throw here takes the whole gate down and
                // tells nobody which rule broke.
                failures.Add("[battle-monthly] BattleMonthlyRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "BATTLE MONTHLY OK - no reward in either family grants combat power, sells a tier, sells " +
                         "XP or carries a glimmer line; every convenience kind has a live redeemer; no cosmetic or " +
                         "skr reward is authored while its delivery gate is shut; the XP curve is strictly " +
                         "increasing and stays so when scaled to a 28..31 day month; every card is the pool model " +
                         "with a complete 1..N table under the anti-inflation ceiling; the firewall is actually " +
                         "invoked at load; XP has exactly one door and it takes an outcome, not an amount; the " +
                         "Monthly Ledger screen has no countdown because nothing on it expires; and each screen " +
                         "has exactly ONE implementation which is reachable through PanelRouter and carries the " +
                         "modal-arbiter lifecycle.";
                Debug.Log("BATTLE_MONTHLY_OK\n" + log);
                return true;
            }

            reason = "battle-monthly: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("BATTLE_MONTHLY_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  [dual-copy] -- runs FIRST. CanonicalJson reads Resources first and falls
        //  back to StreamingAssets, so if the copies differ every later case is
        //  measuring a file the shipped build may never load.
        // =====================================================================
        private static string CaseDualCopy(List<string> failures, StringBuilder log)
        {
            string res = Application.dataPath + "/Resources/" + DataRelPath;
            string sa  = Application.dataPath + "/StreamingAssets/" + DataRelPath;

            if (!File.Exists(res) || !File.Exists(sa))
            {
                failures.Add("[dual-copy] " + DataRelPath + " is missing " +
                             (File.Exists(res) ? "" : "the Resources copy ") +
                             (File.Exists(sa) ? "" : "the StreamingAssets copy") +
                             " - one missing copy silently changes what a shipped build loads.");
                return File.Exists(res) ? File.ReadAllText(res) : (File.Exists(sa) ? File.ReadAllText(sa) : null);
            }

            byte[] a = File.ReadAllBytes(res), b = File.ReadAllBytes(sa);
            bool equal = a.Length == b.Length;
            if (equal)
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { equal = false; break; }

            if (!equal)
                failures.Add("[dual-copy] " + DataRelPath + " Resources and StreamingAssets copies DIVERGED (" +
                             a.Length + " vs " + b.Length + " bytes).");
            else
                log.AppendLine("  [dual-copy] " + DataRelPath + " byte-identical across both copies (" +
                               a.Length + " bytes)");

            return File.ReadAllText(res);
        }

        // =====================================================================
        //  [no-combat] -- the ban list, scanned over the raw text.
        // =====================================================================
        private static void CaseNoBannedTokens(string raw, List<string> failures, StringBuilder log)
        {
            // Strip the authored `_`-prefixed prose notes before scanning: those notes exist to
            // EXPLAIN the ban and legitimately name the banned things ("no revive, no mid-battle
            // heal"). Scanning them would make the documentation of a rule fail the rule.
            string scanned = StripUnderscoreNotes(raw).ToLowerInvariant().Replace("-", "").Replace("_", "");

            int hits = 0;
            foreach (string token in BannedTokens)
            {
                string needle = token.Replace("-", "").Replace("_", "");
                if (scanned.IndexOf(needle, StringComparison.Ordinal) < 0) continue;
                hits++;
                failures.Add("[no-combat] battle_monthly.json contains the banned token '" + token + "'. The " +
                             "covenant (monetization-v2-spec section 2) permits convenience and beauty and NEVER " +
                             "combat power: no revive, no mid-battle heal or shield, no damage/armor/crit/" +
                             "attack-speed/cooldown modifier, no permanent passive, no level or cap raise, no " +
                             "loot box, and no SKU that sells a tier (owner ruling Q4, 2026-08-21). If this is a " +
                             "false positive from a legitimate word, RENAME THE FIELD - do not weaken this list.");
            }

            if (hits == 0)
                log.AppendLine("  [no-combat] " + BannedTokens.Length + " banned tokens scanned, zero present");
        }

        /// <summary>Removes every <c>"_key": "..."</c> authoring note so prose cannot trip the scan.</summary>
        private static string StripUnderscoreNotes(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            int i = 0;
            while (i < raw.Length)
            {
                int keyAt = raw.IndexOf("\"_", i, StringComparison.Ordinal);
                if (keyAt < 0) { sb.Append(raw, i, raw.Length - i); break; }
                sb.Append(raw, i, keyAt - i);

                // Skip to the end of this key's VALUE. Notes in this file are always plain strings.
                int colon = raw.IndexOf(':', keyAt);
                if (colon < 0) { i = keyAt + 2; continue; }
                int q1 = raw.IndexOf('"', colon);
                if (q1 < 0) { i = colon + 1; continue; }
                int j = q1 + 1;
                while (j < raw.Length && !(raw[j] == '"' && raw[j - 1] != '\\')) j++;
                i = Math.Min(raw.Length, j + 1);
            }
            return sb.ToString();
        }

        // =====================================================================
        //  [no-glimmer] -- owner ruling 2026-08-21, applied to pass tiers because a
        //  tier IS pack contents by another name.
        //  This is about CONTENTS. Glimmer the CURRENCY is untouched and is still
        //  earned and spent elsewhere - do not "fix" a failure here by deleting it.
        // =====================================================================
        private static void CaseNoGlimmerAnywhere(string raw, List<string> failures, StringBuilder log)
        {
            if (StripUnderscoreNotes(raw).IndexOf("glimmer", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[no-glimmer] battle_monthly.json carries a glimmer line. Owner ruling 2026-08-21 " +
                             "stripped glimmer from every pack ('nothing real and money has never been active') " +
                             "and a pass tier or a daily drip is pack contents by another name. Its only sink is " +
                             "cosmetics, and no cosmetic art exists in the build, so it is a reward that buys " +
                             "nothing the player can see.");
            else
                log.AppendLine("  [no-glimmer] no glimmer reward line anywhere in the file");
        }

        // =====================================================================
        //  [pointer-only] -- data-architecture T1: no binary in a catalog.
        // =====================================================================
        private static void CaseNoInlinedBinary(string raw, List<string> failures, StringBuilder log)
        {
            if (raw.IndexOf("data:image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("base64", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[pointer-only] battle_monthly.json inlines binary (a data: URI or base64). Icons and " +
                             "cosmetics are POINTER STRINGS only.");
            else
                log.AppendLine("  [pointer-only] no inlined binary; every asset reference is a pointer string");
        }

        // =====================================================================
        //  [season] -- the XP curve, the scaling ruling, the premium SKU.
        // =====================================================================
        private static void CaseSeasons(JObject root, List<string> failures, StringBuilder log)
        {
            var seasons = root["battlePassSeasons"] as JArray;
            if (seasons == null || seasons.Count == 0)
            {
                failures.Add("[season] battle_monthly.json authors no battlePassSeasons[] - the Season Track " +
                             "screen would render its empty state forever.");
                return;
            }

            foreach (var tok in seasons)
            {
                if (!(tok is JObject s)) continue;
                string id = s["seasonId"]?.Value<string>() ?? "<no id>";

                var tiers = s["tiers"] as JArray;
                if (tiers == null || tiers.Count == 0)
                {
                    failures.Add("[season] season '" + id + "' has no tiers.");
                    continue;
                }

                // -- invariant 3: strictly increasing, sequential 1..N ---------
                int prevXp = -1, expected = 1, capstones = 0;
                foreach (var tt in tiers)
                {
                    if (!(tt is JObject t)) continue;
                    int tier = t["tier"]?.Value<int>() ?? -1;
                    int xp = t["xpRequired"]?.Value<int>() ?? -1;

                    if (tier != expected)
                        failures.Add("[season] season '" + id + "' tier numbering jumps: expected " + expected +
                                     ", found " + tier + ". The track is indexed by position; a gap draws a " +
                                     "column the player can never reach.");
                    expected++;

                    if (xp <= prevXp)
                        failures.Add("[season] season '" + id + "' tier " + tier + " has xpRequired " + xp +
                                     " which is not GREATER than the previous tier's " + prevXp +
                                     ". A flat or descending gate makes two tiers unlock at once.");
                    prevXp = xp;

                    if (t["isCapstone"]?.Value<bool>() == true) capstones++;
                }

                if (capstones != 1)
                    failures.Add("[season] season '" + id + "' flags " + capstones + " capstone tier(s) - there " +
                                 "must be exactly one, and it must be the last.");
                else if (!(tiers[tiers.Count - 1] is JObject last) || last["isCapstone"]?.Value<bool>() != true)
                    failures.Add("[season] season '" + id + "': the capstone flag is not on the LAST tier.");

                // -- ruling Q1: calendar months, so the curve must SCALE --------
                // A 28-day February and a 31-day March award the same tiers over different windows.
                // Assert the derivation stays strictly increasing at every real month length, so
                // scaling can never collapse two gates onto one value.
                int lengthDays = s["lengthDays"]?.Value<int>() ?? 0;
                if (lengthDays <= 0)
                    failures.Add("[season] season '" + id + "' has no lengthDays - the ruling-Q1 XP scaling has " +
                                 "no denominator and every month would be gated identically.");
                else
                    for (int days = 28; days <= 31; days++)
                    {
                        int prevScaled = -1;
                        foreach (var tt in tiers)
                        {
                            if (!(tt is JObject t)) continue;
                            int xp = t["xpRequired"]?.Value<int>() ?? 0;
                            int scaled = (int)Math.Ceiling((double)xp * days / lengthDays);
                            if (scaled <= prevScaled)
                            {
                                failures.Add("[season] season '" + id + "': scaling the XP curve to a " + days +
                                             "-day month collapses tier " + (t["tier"]?.Value<int>() ?? -1) +
                                             " onto the previous gate (" + scaled + " <= " + prevScaled +
                                             "). Widen the authored curve.");
                                break;
                            }
                            prevScaled = scaled;
                        }
                    }

                // -- invariant 3b + the honest-CTA rule -------------------------
                string sku = s["premiumPassSku"]?.Value<string>();
                if (!string.IsNullOrEmpty(sku))
                {
                    if (PackCatalog.Find(sku) == null)
                        failures.Add("[season] season '" + id + "' names premiumPassSku '" + sku + "' which " +
                                     "resolves to NO PackDef. The Season Track would draw a purchase control " +
                                     "that cannot complete - the WO-1118 vapor rule wearing a CTA. Either author " +
                                     "the pack in the same change or leave the field empty.");
                    else
                        log.AppendLine("  [season] season '" + id + "' premium lane SKU '" + sku + "' resolves");
                }
                else
                {
                    log.AppendLine("  [season] season '" + id + "' authors NO premiumPassSku - the premium lane is " +
                                   "shown but not purchasable, which is the honest state while there is no SKR " +
                                   "ledger and purchases are flag-off.");
                }

                // -- open question 5 default: perfectBonus stays 0 until the signal exists -----
                int perfect = s["xp"]?["perfectBonus"]?.Value<int>() ?? 0;
                if (perfect != 0)
                    failures.Add("[season] season '" + id + "' authors perfectBonus=" + perfect + ", but the " +
                                 "no-hit / flawless signal is not tracked anywhere in the build, so that XP can " +
                                 "never be earned. Raise it in the SAME change that lands the signal.");

                log.AppendLine("  [season] '" + id + "': " + tiers.Count + " tiers, strictly increasing, one " +
                               "capstone last, curve stays monotonic at 28/29/30/31 days");
            }
        }

        // =====================================================================
        //  [card] -- invariant 4 + the pool-model ruling.
        // =====================================================================
        private static void CaseCards(JObject root, List<string> failures, StringBuilder log)
        {
            var cards = root["monthlyCards"] as JArray;
            if (cards == null || cards.Count == 0)
            {
                failures.Add("[card] battle_monthly.json authors no monthlyCards[].");
                return;
            }

            foreach (var tok in cards)
            {
                if (!(tok is JObject c)) continue;
                string sku = c["sku"]?.Value<string>() ?? "<no sku>";
                int duration = c["durationDays"]?.Value<int>() ?? 0;
                var table = c["dailyTable"] as JArray;

                if (table == null)
                {
                    failures.Add("[card] card '" + sku + "' has no dailyTable.");
                    continue;
                }

                if (table.Count != duration)
                    failures.Add("[card] card '" + sku + "' authors durationDays=" + duration + " but a " +
                                 table.Count + "-row dailyTable. Under the POOL model durationDays is the number " +
                                 "of CLAIMS, so a short table strands the pool on a day that pays nothing.");

                var seen = new HashSet<int>();
                for (int d = 1; d <= duration; d++) seen.Add(d);
                foreach (var dt in table)
                {
                    int day = (dt as JObject)?["day"]?.Value<int>() ?? -1;
                    if (day < 1 || day > duration)
                        failures.Add("[card] card '" + sku + "' has a dailyTable row for day " + day +
                                     ", outside 1.." + duration + ".");
                    else if (!seen.Remove(day))
                        failures.Add("[card] card '" + sku + "' authors day " + day + " more than once.");

                    if ((dt as JObject)?["grant"] == null)
                        failures.Add("[card] card '" + sku + "' day " + day + " has no grant - a claim spent on " +
                                     "nothing.");
                }
                foreach (int missing in seen)
                    failures.Add("[card] card '" + sku + "' is MISSING day " + missing + ". Every one of the " +
                                 duration + " days must be present exactly once: the Monthly Ledger draws all of " +
                                 "them pre-purchase, and a missing row is the 'hidden day' section 3.2 forbids.");

                // -- open question 2, resolved to POOL --------------------------
                string model = c["claimModel"]?.Value<string>() ?? "";
                if (!string.Equals(model, "pool", StringComparison.OrdinalIgnoreCase))
                    failures.Add("[card] card '" + sku + "' uses claimModel '" + model + "'. The owner default is " +
                                 "POOL: a missed day rolls into the pool and the card lives until every claim is " +
                                 "spent. The CALENDAR model forfeits missed days, which breaks the section 3.2 " +
                                 "promise the screen makes in words. Changing this is an OWNER decision.");

                // -- vapor: the month-exclusive cosmetic ------------------------
                string excl = c["exclusiveCosmetic"]?.Value<string>();
                if (!string.IsNullOrEmpty(excl) && !BattleMonthlyCatalog.CosmeticsDeliverable)
                    failures.Add("[card] card '" + sku + "' names exclusiveCosmetic '" + excl + "' while no " +
                                 "cosmetic art exists in this build - it would land on the applier's preview-tint " +
                                 "fallback. The headline of a monthly card cannot be a thing the player cannot " +
                                 "see. Author it in the same change that lands the art.");

                log.AppendLine("  [card] '" + sku + "': pool model, " + table.Count + "/" + duration +
                               " days present exactly once");
            }
        }

        // =====================================================================
        //  [grants] -- the recursive walk. Invariants 1, 2 and 5.
        // =====================================================================
        private static void CaseGrantsAreSanctioned(JObject root, List<string> failures, StringBuilder log)
        {
            int walked = 0;

            var seasons = root["battlePassSeasons"] as JArray;
            if (seasons != null)
                foreach (var s in seasons)
                {
                    string id = (s as JObject)?["seasonId"]?.Value<string>() ?? "?";
                    var tiers = (s as JObject)?["tiers"] as JArray;
                    if (tiers == null) continue;
                    foreach (var t in tiers)
                    {
                        int tier = (t as JObject)?["tier"]?.Value<int>() ?? -1;
                        walked += WalkGrant((t as JObject)?["free"] as JObject,
                                            "season '" + id + "' tier " + tier + " free", failures, 0);
                        walked += WalkGrant((t as JObject)?["premium"] as JObject,
                                            "season '" + id + "' tier " + tier + " premium", failures, 0);
                    }
                }

            var cards = root["monthlyCards"] as JArray;
            if (cards != null)
                foreach (var c in cards)
                {
                    string sku = (c as JObject)?["sku"]?.Value<string>() ?? "?";
                    var table = (c as JObject)?["dailyTable"] as JArray;
                    if (table == null) continue;
                    foreach (var d in table)
                    {
                        int day = (d as JObject)?["day"]?.Value<int>() ?? -1;
                        walked += WalkGrant((d as JObject)?["grant"] as JObject,
                                            "card '" + sku + "' day " + day, failures, 0);
                    }
                }

            log.AppendLine("  [grants] " + walked + " grants walked; every kind sanctioned, every economy key in " +
                           "{wood,iron,food,crystals,coins}, every convenience kind redeemable in THIS build, no " +
                           "cosmetic or skr row authored ahead of its delivery gate, no grant credits XP");
        }

        private static int WalkGrant(JObject grant, string where, List<string> failures, int depth)
        {
            if (grant == null) return 0;
            if (depth > 4)
            {
                failures.Add("[grants] " + where + ": bundle nests deeper than 4 - malformed.");
                return 1;
            }

            string kind = grant["kind"]?.Value<string>() ?? "";
            int count = 1;

            // -- invariant 1: the kind must be sanctioned. This is the branch a `combat` kind
            //    would land in, and it is the whole firewall.
            if (!SanctionedKinds.Contains(kind))
            {
                failures.Add("[grants] " + where + ": reward kind '" + kind + "' is NOT sanctioned. The permitted " +
                             "set is {economy, convenience_token, cosmetic_sku, skr, bundle}. THERE IS NO COMBAT " +
                             "KIND and there never will be - covenant section 2, WO section 2.3.");
                return count;
            }

            // -- invariant 2: no grant may credit Battle XP. XP is earned by playing, only.
            if (grant["xp"] != null || grant["battleXp"] != null || grant["tiers"] != null || grant["tier"] != null)
                failures.Add("[grants] " + where + ": a grant references XP or a TIER. Battle XP is earned by " +
                             "PLAYING and can never be granted or bought (invariant 2), and owner ruling Q4 " +
                             "(2026-08-21) is NEVER SELL TIERS - no catch-up, no skip, nothing.");

            switch (kind)
            {
                case "economy":
                {
                    var e = grant["economy"] as JObject;
                    if (e == null || !e.HasValues)
                    {
                        failures.Add("[grants] " + where + ": economy grant carries no amount.");
                        break;
                    }
                    foreach (var prop in e.Properties())
                    {
                        if (!AllowedEconomyKeys.Contains(prop.Name))
                            failures.Add("[grants] " + where + ": economy key '" + prop.Name + "' is not one of " +
                                         "{wood, iron, food, crystals, coins}. A new currency in a reward table " +
                                         "is a product decision, not a data edit.");
                        if (prop.Value.Type == JTokenType.Integer && prop.Value.Value<int>() <= 0)
                            failures.Add("[grants] " + where + ": economy key '" + prop.Name + "' is " +
                                         prop.Value.Value<int>() + " - a zero or negative reward reads to the " +
                                         "player as a broken one.");
                    }
                    break;
                }

                case "convenience_token":
                {
                    var c = grant["convenience"] as JObject;
                    string ck = c?["kind"]?.Value<string>();
                    int amount = c?["count"]?.Value<int>() ?? 0;
                    if (string.IsNullOrEmpty(ck) || amount <= 0)
                    {
                        failures.Add("[grants] " + where + ": convenience grant has no kind or a non-positive count.");
                        break;
                    }
                    // LEGAL IS NOT REDEEMABLE. Asking PackCatalog rather than re-listing kinds here
                    // means the day a redeemer ships, this oracle updates itself.
                    if (!PackCatalog.IsRedeemableConvenience(ck))
                        failures.Add("[grants] " + where + ": convenience kind '" + ck + "' has NO REDEEMER in " +
                                     "this build - nothing in the shipped game spends it, so the token would " +
                                     "accumulate unread. That is the WO-1118 vapor rule. Ship the redeemer first, " +
                                     "then author the reward.");
                    break;
                }

                case "cosmetic_sku":
                {
                    string sku = grant["cosmeticSku"]?.Value<string>();
                    if (string.IsNullOrEmpty(sku))
                        failures.Add("[grants] " + where + ": cosmetic grant names no SKU.");
                    else if (!BattleMonthlyCatalog.CosmeticsDeliverable)
                        failures.Add("[grants] " + where + ": cosmetic '" + sku + "' is authored but no cosmetic " +
                                     "art exists in this build, so an equipped cosmetic lands on the applier's " +
                                     "preview-tint fallback. A season's premium lane paying out invisible flags " +
                                     "disappoints for a MONTH, not once. Author cosmetic rewards in the same " +
                                     "change that lands the art, and flip " +
                                     "BattleMonthlyCatalog.CosmeticsDeliverable in that change too.");
                    break;
                }

                case "skr":
                {
                    if (!BattleMonthlyCatalog.SkrLedgerAvailable)
                        failures.Add("[grants] " + where + ": an skr reward is authored but there is NO SKR LEDGER " +
                                     "in this build (neither ISkrLedger nor LocalSkrLedger exists anywhere in the " +
                                     "tree - the only occurrence of the name is a doc comment in IPiPlatform.cs). " +
                                     "The credit has nowhere to land.");
                    break;
                }

                case "bundle":
                {
                    var arr = grant["bundle"] as JArray;
                    if (arr == null || arr.Count == 0)
                    {
                        failures.Add("[grants] " + where + ": empty bundle.");
                        break;
                    }
                    for (int i = 0; i < arr.Count; i++)
                        count += WalkGrant(arr[i] as JObject, where + " [bundle " + i + "]", failures, depth + 1);
                    break;
                }
            }

            return count;
        }

        // =====================================================================
        //  [anti-inflation] -- invariant 6.
        // =====================================================================
        private static void CaseAntiInflation(JObject root, List<string> failures, StringBuilder log)
        {
            var cards = root["monthlyCards"] as JArray;
            if (cards == null) return;

            foreach (var anchor in CardAnchors)
            {
                JObject card = null;
                foreach (var tok in cards)
                    if (tok is JObject c && string.Equals(c["sku"]?.Value<string>(), anchor.Key, StringComparison.Ordinal))
                    { card = c; break; }

                if (card == null)
                {
                    failures.Add("[anti-inflation] battle_monthly.json has no card '" + anchor.Key + "'. This " +
                                 "oracle anchors each card against a same-priced pack; a renamed card must be " +
                                 "re-anchored in the SAME change, or the ceiling silently stops being checked.");
                    continue;
                }

                var pack = PackCatalog.Find(anchor.Value);
                if (pack == null)
                {
                    failures.Add("[anti-inflation] anchor pack '" + anchor.Value + "' does not resolve - the " +
                                 "ceiling for card '" + anchor.Key + "' cannot be computed, so this is a FAIL, " +
                                 "not an unknown.");
                    continue;
                }

                double anchorUsd = pack.Pricing != null ? pack.Pricing.Usd : 0d;

                var totals = new Dictionary<string, long>();
                var table = card["dailyTable"] as JArray;
                if (table != null)
                    foreach (var d in table)
                        AccumulateEconomy((d as JObject)?["grant"] as JObject, totals);

                foreach (var key in AllowedEconomyKeys)
                {
                    totals.TryGetValue(key, out long got);
                    if (got == 0) continue;
                    double ceiling = pack.EconomyAmount(key) * DripCeilingMultiple;
                    if (ceiling <= 0d)
                    {
                        failures.Add("[anti-inflation] card '" + anchor.Key + "' pays " + got + " " + key +
                                     " over its whole table, but the $" +
                                     anchorUsd.ToString("0.00", CultureInfo.InvariantCulture) + " anchor '" +
                                     anchor.Value + "' grants NONE of it. A card must not open an economy channel " +
                                     "that its price rung does not sell.");
                    }
                    else if (got > ceiling)
                    {
                        failures.Add("[anti-inflation] card '" + anchor.Key + "' pays " + got + " " + key +
                                     " over its table, above the " + DripCeilingMultiple.ToString("0.0", CultureInfo.InvariantCulture) +
                                     "x ceiling of " + ceiling.ToString("0", CultureInfo.InvariantCulture) +
                                     " set by the same-priced pack '" + anchor.Value + "'. Patience is worth a " +
                                     "premium over an instant pack, but not an order of magnitude - past that, " +
                                     "buying a card skips the economy.");
                    }
                }

                log.AppendLine("  [anti-inflation] '" + anchor.Key + "' vs '" + anchor.Value + "' ($" +
                               anchorUsd.ToString("0.00", CultureInfo.InvariantCulture) + "): every channel " +
                               "under the " + DripCeilingMultiple.ToString("0.0", CultureInfo.InvariantCulture) + "x ceiling");
            }
        }

        private static void AccumulateEconomy(JObject grant, Dictionary<string, long> totals)
        {
            if (grant == null) return;
            string kind = grant["kind"]?.Value<string>() ?? "";
            if (kind == "economy")
            {
                var e = grant["economy"] as JObject;
                if (e == null) return;
                foreach (var prop in e.Properties())
                {
                    if (prop.Value.Type != JTokenType.Integer) continue;
                    totals.TryGetValue(prop.Name, out long prior);
                    totals[prop.Name] = prior + prop.Value.Value<long>();
                }
            }
            else if (kind == "bundle")
            {
                var arr = grant["bundle"] as JArray;
                if (arr == null) return;
                foreach (var child in arr) AccumulateEconomy(child as JObject, totals);
            }
        }

        // =====================================================================
        //  [copy] -- the words, and the GREYSCALE rule they carry.
        // =====================================================================
        private static void CaseCopy(List<string> failures, StringBuilder log)
        {
            StoreStrings.Reload();

            foreach (string key in Concat(StoreStrings.SeasonTrackKeys, StoreStrings.MonthlyLedgerKeys))
            {
                string s = StoreStrings.Get(key);
                if (string.IsNullOrEmpty(s) || s.StartsWith("[[missing:", StringComparison.Ordinal))
                {
                    failures.Add("[copy] canon-strings has no '" + key + "' - the screen would render a " +
                                 "placeholder marker where a sentence belongs.");
                    continue;
                }
                foreach (char c in s)
                    if (c > 127)
                    {
                        failures.Add("[copy] '" + key + "' contains a non-ASCII character - TMP renders it as tofu.");
                        break;
                    }
            }

            // -- THE GREYSCALE GATE -----------------------------------------
            // The owner is red/green colourblind, so a state carried by colour alone is a defect.
            // Every state prints a WORD, and two states must never share one: two states with the
            // same word are two states she cannot tell apart once the hue is gone.
            var seasonWords = new Dictionary<string, string>(StringComparer.Ordinal);
            var ledgerWords = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in StoreStrings.StateWordKeys)
            {
                string word = StoreStrings.Get(key);
                var bucket = key.StartsWith("seasonTrack", StringComparison.Ordinal) ? seasonWords : ledgerWords;
                if (string.IsNullOrWhiteSpace(word))
                {
                    failures.Add("[copy] state key '" + key + "' has no word. Every state on both screens must " +
                                 "carry a word: strip the hue and the screen must still read.");
                    continue;
                }
                if (bucket.TryGetValue(word, out string other))
                    failures.Add("[copy] state keys '" + key + "' and '" + other + "' share the word '" + word +
                                 "'. Two states with one word are two states that are indistinguishable in " +
                                 "greyscale, which is the exact failure this rule exists to prevent.");
                else
                    bucket[word] = key;
            }

            // -- the two formatted lines must actually format --------------
            string claims = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerClaimsLeft);
            if (claims.IndexOf("{0}", StringComparison.Ordinal) < 0)
                failures.Add("[copy] 'monthlyLedgerClaimsLeft' does not format {0} - the claim count must come " +
                             "from the pool, not be typed into the sentence.");
            if (claims.IndexOf("claim", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[copy] 'monthlyLedgerClaimsLeft' does not say 'claims'. Under the pool model the " +
                             "header counts CLAIMS, never days - saying days would imply an expiry that does not " +
                             "exist.");

            string tierLine = StoreStrings.Get(StoreStrings.KeySeasonTrackTierLine);
            if (tierLine.IndexOf("{0}", StringComparison.Ordinal) < 0 ||
                tierLine.IndexOf("{1}", StringComparison.Ordinal) < 0)
                failures.Add("[copy] 'seasonTrackTierLine' must format both {0} (current tier) and {1} (tier count).");

            log.AppendLine("  [copy] " + (StoreStrings.SeasonTrackKeys.Length + StoreStrings.MonthlyLedgerKeys.Length) +
                           " sentences present and ASCII-only; all 8 state words present and distinct within " +
                           "their screen (the greyscale gate)");
        }

        private static IEnumerable<string> Concat(string[] a, string[] b)
        {
            foreach (var s in a) yield return s;
            foreach (var s in b) yield return s;
        }

        // =====================================================================
        //  [chokepoint] -- A FIREWALL THAT IS NEVER INVOKED IS A COMMENT.
        // =====================================================================
        private static void CaseChokepoint(List<string> failures, StringBuilder log)
        {
            string path = Application.dataPath + "/_Modules/Wallet/BattleMonthlyCatalog.cs";
            if (!File.Exists(path))
            {
                failures.Add("[chokepoint] BattleMonthlyCatalog.cs not found at " + path + " - the firewall " +
                             "cannot be verified, so this is a FAIL, not an unknown.");
                return;
            }

            string src = File.ReadAllText(path);
            int loadAt = src.IndexOf("private static void EnsureLoaded()", StringComparison.Ordinal);
            if (loadAt < 0)
            {
                failures.Add("[chokepoint] BattleMonthlyCatalog.EnsureLoaded not found. If the loader was renamed, " +
                             "re-point this oracle in the SAME change - otherwise the firewall silently stops " +
                             "being checked.");
                return;
            }

            string body = src.Substring(loadAt, Math.Min(1200, src.Length - loadAt));
            if (body.IndexOf("EnforceFirewall(", StringComparison.Ordinal) < 0)
                failures.Add("[chokepoint] BattleMonthlyCatalog.EnsureLoaded does NOT call EnforceFirewall. Every " +
                             "reward reaching the game passes through that one loader; a firewall it does not " +
                             "invoke protects nothing at all.");
            else
                log.AppendLine("  [chokepoint] EnsureLoaded invokes EnforceFirewall - every reward is sanitised at load");

            if (src.IndexOf("Combat", StringComparison.Ordinal) >= 0 &&
                src.IndexOf("RewardKind", StringComparison.Ordinal) >= 0 &&
                src.IndexOf("Combat =", StringComparison.Ordinal) >= 0)
                failures.Add("[chokepoint] a Combat member appears to have been added to RewardKind. The enum has " +
                             "no way to spell combat power ON PURPOSE - that is a stronger guarantee than any " +
                             "validator that hopes to catch it.");
        }

        // =====================================================================
        //  [xp-one-door] -- "earned by playing" is only true if there is no second door.
        // =====================================================================
        private static void CaseXpHasOneDoor(List<string> failures, StringBuilder log)
        {
            string path = Application.dataPath + "/_Modules/Wallet/BattlePassService.cs";
            if (!File.Exists(path))
            {
                failures.Add("[xp-one-door] BattlePassService.cs not found at " + path + " - FAIL, not unknown.");
                return;
            }

            string src = File.ReadAllText(path);

            if (src.IndexOf("public static void OnArenaResult(", StringComparison.Ordinal) < 0)
                failures.Add("[xp-one-door] BattlePassService.OnArenaResult not found. It is the ONLY sanctioned " +
                             "way XP enters the pass; if it was renamed, re-point this oracle in the same change.");

            // A public AddXp(int) would be a second door - anything at all could credit XP, and the
            // "earned by playing" promise would be a comment rather than a property of the code.
            if (src.IndexOf("public static void AddXp(", StringComparison.Ordinal) >= 0 ||
                src.IndexOf("public static bool AddXp(", StringComparison.Ordinal) >= 0 ||
                src.IndexOf("public static int AddXp(", StringComparison.Ordinal) >= 0)
                failures.Add("[xp-one-door] BattlePassService exposes a public AddXp - that is a SECOND door into " +
                             "Battle XP, and anything could then credit it. XP enters through OnArenaResult (a " +
                             "battle OUTCOME, not an amount) and nowhere else. WO invariant 2.");

            // The XP source must still be the live arena ledger, and it must still be wired.
            string arena = Application.dataPath + "/_Modules/Village/Arena/ArenaProgressStore.cs";
            if (!File.Exists(arena))
                failures.Add("[xp-one-door] ArenaProgressStore.cs not found - the XP SOURCE cannot be verified.");
            else
            {
                string asrc = File.ReadAllText(arena);
                if (asrc.IndexOf("BattlePassService.OnArenaResult(", StringComparison.Ordinal) < 0)
                    failures.Add("[xp-one-door] ArenaProgressStore no longer notifies BattlePassService. That store " +
                                 "is the ONE wired W/L recorder (RecordWin is called from exactly one site, " +
                                 "ArenaMode). Without the notify, the pass has no XP source at all and the track " +
                                 "silently never advances - a failure with no error on screen.");
                else
                    log.AppendLine("  [xp-one-door] XP enters only via OnArenaResult, wired from ArenaProgressStore " +
                                   "(the live W/L ledger); no public AddXp exists");
            }
        }

        // =====================================================================
        //  [no-countdown] -- nothing on the ledger expires, so nothing may tick.
        // =====================================================================
        private static void CaseNoCountdown(List<string> failures, StringBuilder log)
        {
            var ledgers = FindScreens(LedgerScreenMarker);
            if (ledgers.Count == 0)
            {
                failures.Add("[no-countdown] no Monthly Ledger screen source found (nothing under _Modules/Wallet " +
                             "calls MonthlyCardService.DayState). The screen cannot be verified, so this is a " +
                             "FAIL, not an unknown.");
                return;
            }

            string[] tickers = { "TimeSpan", "expiresAt", "ExpiresAt", "countdown", "Countdown", "AddSeconds", "AddHours" };

            foreach (string path in ledgers)
            {
                // Strip comments: a screen may EXPLAIN this rule at length and legitimately name the
                // thing it forbids. Policing the explanation of a rule is how a good rule gets deleted.
                string code = StripLineComments(File.ReadAllText(path));
                foreach (string t in tickers)
                    if (code.IndexOf(t, StringComparison.Ordinal) >= 0)
                        failures.Add("[no-countdown] " + Path.GetFileName(path) + " uses '" + t + "'. The card runs " +
                                     "on the POOL model, so NOTHING EXPIRES - a ticking clock would be a lie that " +
                                     "manufactures urgency over a deadline that does not exist, which is exactly " +
                                     "the pressure the WO's section 3.2 promises not to apply. The header counts " +
                                     "CLAIMS.");
            }

            foreach (string key in StoreStrings.MonthlyLedgerKeys)
            {
                string s = StoreStrings.Get(key);
                if (s != null && (s.IndexOf("expire", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                  s.IndexOf("Nothing here expires", StringComparison.OrdinalIgnoreCase) < 0))
                    failures.Add("[no-countdown] canon-strings '" + key + "' says something expires. Under the pool " +
                                 "model nothing does; the only sentence allowed to use the word is the one that " +
                                 "says nothing expires.");
            }

            log.AppendLine("  [no-countdown] no timer type, no expiry field and no countdown copy on the Monthly " +
                           "Ledger - the header counts claims, which is the only honest number");
        }

        private static string StripLineComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            foreach (string line in src.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("///", StringComparison.Ordinal) ||
                    trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                int at = line.IndexOf("//", StringComparison.Ordinal);
                sb.AppendLine(at >= 0 ? line.Substring(0, at) : line);
            }
            return sb.ToString();
        }

        // =====================================================================
        //  [code-built] -- UXML renders EMPTY in player builds.
        // =====================================================================
        private static void CaseScreensAreCodeBuilt(List<string> failures, StringBuilder log)
        {
            var seasons = FindScreens(SeasonScreenMarker);
            var ledgers = FindScreens(LedgerScreenMarker);

            if (seasons.Count == 0)
                failures.Add("[code-built] no Season Track screen source found (nothing under _Modules/Wallet " +
                             "calls BattlePassService.FreeState). FAIL, not unknown.");
            if (ledgers.Count == 0)
                failures.Add("[code-built] no Monthly Ledger screen source found. FAIL, not unknown.");

            foreach (string path in Concat(seasons.ToArray(), ledgers.ToArray()))
            {
                string code = StripLineComments(File.ReadAllText(path));

                if (code.IndexOf("VisualTreeAsset", StringComparison.Ordinal) >= 0 ||
                    code.IndexOf("UIDocument", StringComparison.Ordinal) >= 0 ||
                    code.IndexOf(".uxml", StringComparison.Ordinal) >= 0)
                    failures.Add("[code-built] " + Path.GetFileName(path) + " reaches for UXML/UIToolkit. UXML " +
                                 "renders EMPTY in player builds (CLAUDE.md section 8) - the screen would be blank " +
                                 "on a device and correct in the editor, which is the worst way to find out.");

                // The 112 px touch floor, satisfied EITHER by flooring cell sizes explicitly OR by
                // building every control through a kit button (ElarionUiKit.Button and
                // BuildObsidianButton both call ClampMinTouch themselves). Two legitimate routes to
                // the same guarantee, so the case asks for the GUARANTEE and not for one spelling.
                bool floored = code.IndexOf("ElarionUiKit.MinTouchPx", StringComparison.Ordinal) >= 0
                            || code.IndexOf("BuildObsidianButton(", StringComparison.Ordinal) >= 0
                            || code.IndexOf("ElarionUiKit.Button(", StringComparison.Ordinal) >= 0
                            || code.IndexOf("ClampMinTouch(", StringComparison.Ordinal) >= 0;
                if (!floored)
                    failures.Add("[code-built] " + Path.GetFileName(path) + " neither references " +
                                 "ElarionUiKit.MinTouchPx nor builds its controls through a kit button, so " +
                                 "nothing enforces the 112 px touch floor on it. This is a landscape phone " +
                                 "screen; a sub-floor cell is an un-tappable reward.");
            }

            log.AppendLine("  [code-built] " + (seasons.Count + ledgers.Count) + " screen source(s) checked: " +
                           "code-built uGUI, no UXML, touch floor guaranteed on each");
        }

        // =====================================================================
        //  [one-screen-owner] -- ONE OWNER PER SCREEN, AND THAT OWNER IS REACHABLE.
        // ---------------------------------------------------------------------
        //  HISTORY, because the shape of this case only makes sense with it. Until
        //  2026-08-21 this case was authored to FAIL: two seats had independently
        //  built both screens and neither could see the other, so the gate named both
        //  files and refused to let the pair ship un-ruled. The committer then ruled -
        //  SeasonTrackPanel + MonthlyLedgerPanel survive (canon-strings copy, greyscale
        //  state words, exact geometry); BattleMonthlyPanels.cs is retired (it typed
        //  player-facing sentences inline and derived on-screen words from
        //  enum.ToString()). Its full text is preserved in the WO under "RETIRED
        //  DUPLICATE" so the deletion is recoverable.
        //
        //  THE CASE NOW ASSERTS THE RESOLVED STATE, and it is still a real gate - it
        //  fails if any of three things regress:
        //
        //    1. A RIVAL COMES BACK. Two implementations of one screen both compile and
        //       both look correct in isolation, so nothing tells the next reader which
        //       one the player actually sees, and the unused one rots while someone
        //       keeps fixing the other. This is the duplicated-state failure CLAUDE.md
        //       spends paragraphs on (the stale WO number block, the retired dependency
        //       table, the re-inlined R2 push).
        //    2. THE RETIRED FILE IS RE-ADDED under its old name.
        //    3. THE SURVIVOR STOPS BEING REACHABLE. This is the defect the winning pair
        //       actually shipped with: perfect screens that NOTHING REGISTERED, so
        //       PanelRouter.Open returned false and no door in the game could open them.
        //       A screen that cannot be opened is worth exactly as much as one that was
        //       never written, and it fails silently - which is why it is pinned here.
        // =====================================================================
        private static void CaseOneScreenOwner(List<string> failures, StringBuilder log)
        {
            var seasons = FindScreens(SeasonScreenMarker);
            var ledgers = FindScreens(LedgerScreenMarker);

            CheckSingleOwner("Season Track", seasons, failures);
            CheckSingleOwner("Monthly Ledger", ledgers, failures);

            CheckRetiredDuplicateAbsent(failures);
            CheckRoutable("Season Track", "PanelId.BattlePass", seasons, failures, log);
            CheckRoutable("Monthly Ledger", "PanelId.MonthlyLedger", ledgers, failures, log);

            log.AppendLine("  [one-screen-owner] each screen is implemented in exactly one place, the retired " +
                           "BattleMonthlyPanels duplicate is gone, and both survivors are routable");
        }

        /// <summary>Exactly ONE implementation - not two (a rival), and not zero (a deleted screen).</summary>
        private static void CheckSingleOwner(string screen, List<string> found, List<string> failures)
        {
            if (found.Count == 1) return;

            if (found.Count == 0)
            {
                failures.Add("[one-screen-owner] the " + screen + " screen has NO implementation left under " +
                             "_Modules/Wallet. The 2026-08-21 merge left exactly one owner per screen; zero " +
                             "means the survivor was deleted or its marker call was renamed. Either way the " +
                             "screen cannot be verified, so this is a FAIL, not an unknown.");
                return;
            }

            var names = new List<string>();
            foreach (string p in found) names.Add(Path.GetFileName(p));
            failures.Add("[one-screen-owner] the " + screen + " screen is implemented " + found.Count +
                         " times: " + string.Join(", ", names.ToArray()) + ". This was RULED on 2026-08-21 and " +
                         "the ruling is one owner per screen. Two implementations both compile and both look " +
                         "correct in isolation, so nothing tells the next reader which one the player actually " +
                         "sees - and the unused one rots while someone keeps fixing the other. PICK ONE and " +
                         "delete the other in the same change. The tie-breakers that decided it before: " +
                         "player-facing sentences MUST come from canon-strings.json rather than being typed " +
                         "inline (CLAUDE.md section 7), never from enum.ToString() which puts a developer " +
                         "identifier on a player's screen, every state must carry a WORD so the screen survives " +
                         "a greyscale read, and the surviving screen must be reachable through PanelRouter or " +
                         "it ships unopenable.");
        }

        /// <summary>The retired wrapper must not come back under its old name.</summary>
        private static void CheckRetiredDuplicateAbsent(List<string> failures)
        {
            string path = Application.dataPath + "/_Modules/Wallet/BattleMonthlyPanels.cs";
            if (!File.Exists(path)) return;

            failures.Add("[one-screen-owner] BattleMonthlyPanels.cs is back. It was RETIRED on 2026-08-21 as the " +
                         "losing half of a two-seat screen collision: it typed player-facing sentences inline " +
                         "(\"PLAY ARENA BATTLES TO EARN TIERS\", \"CLAIMS LEFT\", \"CLAIM TODAY\") and derived " +
                         "on-screen state words from enum.ToString(), both of which CLAUDE.md section 7 forbids. " +
                         "Its full text is preserved in WorkOrders/WORK_ORDER_battle_and_monthly_packs.md under " +
                         "'RETIRED DUPLICATE' - read it there rather than restoring it. The live screens are " +
                         "SeasonTrackPanel.cs + MonthlyLedgerPanel.cs.");
        }

        /// <summary>
        /// The surviving screen must be OPENABLE: something under _Modules/Wallet must register its
        /// PanelId with PanelRouter, and the screen itself must carry the modal-arbiter lifecycle.
        /// <para>Both halves are required and they fail differently. With no PanelRouter registration
        /// the door does not exist and <c>PanelRouter.Open</c> returns false. With no PanelManager
        /// handle the screen renders but the arbiter never learns it is up - so PanelRouter's
        /// post-open verify reports the WO-465 invisible-scrim failure on a screen that is in fact
        /// perfectly visible, and a second modal can stack on top of it.</para>
        /// </summary>
        private static void CheckRoutable(string screen, string panelIdExpr, List<string> found,
                                          List<string> failures, StringBuilder log)
        {
            string registerCall = "PanelRouter.Register(" + panelIdExpr;
            var registrars = FindScreens(registerCall);

            if (registrars.Count == 0)
                failures.Add("[one-screen-owner] NOTHING under _Modules/Wallet calls " + registerCall +
                             ", so the " + screen + " screen has no door: PanelRouter.Open(" + panelIdExpr +
                             ") returns false and no entry point in the game can reach it. This is the exact " +
                             "defect the surviving pair shipped with before the 2026-08-21 merge - a finished " +
                             "screen nothing could open, failing silently with no error on screen. The " +
                             "registration lives in BattleMonthlyPanelsBootstrap.cs.");
            else
                log.AppendLine("  [one-screen-owner] " + screen + " routable via " + registerCall + " in " +
                               Path.GetFileName(registrars[0]));

            foreach (string path in found)
            {
                string code = StripLineComments(File.ReadAllText(path));
                string file = Path.GetFileName(path);

                if (code.IndexOf("PanelManager.Register(", StringComparison.Ordinal) < 0 ||
                    code.IndexOf("PanelManager.NotifyOpened(", StringComparison.Ordinal) < 0 ||
                    code.IndexOf("PanelManager.NotifyClosed(", StringComparison.Ordinal) < 0)
                    failures.Add("[one-screen-owner] " + file + " does not carry the full modal-arbiter " +
                                 "lifecycle (PanelManager.Register + NotifyOpened + NotifyClosed). Without it " +
                                 "the arbiter never learns the screen is open: PanelRouter's post-open verify " +
                                 "Fail-logs a correctly rendered screen as the WO-465 invisible-scrim class, and " +
                                 "a second modal can sit on top of this one.");

                // NotifyOpened REFUSES the open during a battle (WO-437: no shopping while being
                // killed). Ignoring the refusal leaves an un-arbitrated modal over live combat, so
                // the return value must be acted on rather than discarded.
                if (code.IndexOf("!PanelManager.NotifyOpened(", StringComparison.Ordinal) < 0)
                    failures.Add("[one-screen-owner] " + file + " calls PanelManager.NotifyOpened but never " +
                                 "tests its result. It returns FALSE when the WO-437 battle-lock refuses the " +
                                 "open; a screen that ignores that refusal stays on top of a live battle. Close " +
                                 "on false.");
            }
        }

        /// <summary>A call only a Season Track screen makes.</summary>
        private const string SeasonScreenMarker = "BattlePassService.FreeState(";
        /// <summary>A call only a Monthly Ledger screen makes.</summary>
        private const string LedgerScreenMarker = "MonthlyCardService.DayState(";

        /// <summary>
        /// Every .cs under _Modules/Wallet whose code contains <paramref name="marker"/>.
        /// <para>Discovery rather than a hardcoded path pair, on purpose: an oracle that names two
        /// files stops policing the screen the moment one is renamed or replaced, and reports the
        /// rename as a missing file rather than as the thing that actually happened.</para>
        /// </summary>
        private static List<string> FindScreens(string marker)
        {
            var hits = new List<string>();
            try
            {
                string root = Application.dataPath + "/_Modules/Wallet";
                if (!Directory.Exists(root)) return hits;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (path.Replace('\\', '/').IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string code = StripLineComments(File.ReadAllText(path));
                    if (code.IndexOf(marker, StringComparison.Ordinal) >= 0) hits.Add(path);
                }
            }
            catch (Exception ex)
            {
                // Surfaced rather than swallowed: an empty result here would read as "no screen
                // exists", which is a very different statement from "the scan failed".
                Debug.LogWarning("[battle-monthly] screen scan for '" + marker + "' failed: " + ex.Message);
            }
            return hits;
        }
    }
}
