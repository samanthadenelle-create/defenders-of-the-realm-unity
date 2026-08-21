// =============================================================================
// MonetizationCovenantRegression — the build-gate that ENFORCES the monetization
// covenant the canon has long *claimed* to enforce (skr_staking.json says a
// "SkrStakingRegression" rejects combat/stat grants — that code never existed; the
// firewall was comment-only). Closes launch-blocker LB-5 / C-COV / C-KIND.
// -----------------------------------------------------------------------------
// THE COVENANT (monetization-v2-spec.md §2/§5.3): everything sellable is COSMETIC,
// SOFT-CURRENCY, or TIME-SAVING CONVENIENCE — never combat power, never a stat, never
// an RNG/gacha pull. This editor-only gate loads EVERY monetization JSON and FAILS
// (returns false + a reason naming the file+field) on any breach:
//   • a convenience/perk `kind` outside the sanctioned allowlist
//   • a grant/category `kind` == combat / stat (the pay-to-win kinds)
//   • a non-zero combat-STAT field (damage/attack/firerate/…) on any sellable
//   • any probability/odds/roll/chance/random field (no RNG monetization)
//
// NO PlayMode — pure file + JToken inspection, so it runs inside the headless
// DataRegression.RunAll batch gate. Robust to a missing file (skip-with-note, never
// throw). Wire it into DataRegression.RunAll (the orchestrator does the one-line add).
//
// The allowlist is DERIVED, not blind-hardcoded: the leaf convenience kinds and the
// perk/grant kinds are read live from skr_staking.json (convenienceAllowList +
// perkKindEnum) and unioned with the documented PackDef set (packs.json _schemaNotes)
// and the economy-pack extension set (WO economy_store_packs _schemaExtensions). If
// the JSON list grows, the gate grows with it — single source of truth.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class MonetizationCovenantRegression
    {
        // --- documented leaf CONVENIENCE kinds (time-saving only) ----------------
        // PackDef set: packs.json _schemaNotes.convenience ("instant-build / instant-repair /
        // harvest-auto-collect / xp-weekend / bounded lantern expedition blessings).
        // Economy-pack extension set: WorkOrders/
        // economy_store_packs.sample.json _schemaExtensions (harvest-boost, instant-fill-
        // storage, workforce-slot, storage-tier-jump, offline-window-extension). Stored
        // NORMALISED (lower + '-'/' ' -> '_') so hyphen/underscore spellings compare equal.
        private static readonly string[] DocumentedConvenienceKinds =
        {
            "instant_build", "instant_repair", "harvest_auto_collect", "xp_weekend",
            "lantern_oil_2x_expedition", "lantern_oil_3x_expedition",
            "harvest_boost", "instant_fill_storage", "workforce_slot",
            "storage_tier_jump", "offline_window_extension",
        };

        // --- documented GRANT/PERK wrapper kinds ---------------------------------
        // The grant envelope kinds used across skr_store / battle_monthly / packs:
        // cosmetic_sku | convenience_token | bundle | economy | skr. skr_staking's
        // perkKindEnum (cosmetic_sku, profile_flair, store_discount_pct, convenience_bump,
        // skr_rebate_share) is unioned in at runtime from the JSON.
        private static readonly string[] DocumentedGrantKinds =
        {
            "cosmetic_sku", "convenience_token", "bundle", "economy", "skr",
        };

        // --- the BANNED kinds — the pay-to-win firewall --------------------------
        private static readonly HashSet<string> BannedKinds = new HashSet<string>
        {
            "combat", "stat", "stats", "combat_stat", "stat_boost", "statboost",
            "weapon_stat", "damage", "power", "attack", "offense",
            "ability_power", "buff", "debuff",
        };

        // --- combat STAT field names (non-zero value on a sellable = pay-to-win) --
        private static readonly HashSet<string> CombatStatFields = new HashSet<string>
        {
            "damage", "attack", "attackspeed", "attack_speed", "firerate", "fire_rate",
            "crit", "critchance", "crit_chance", "armor", "defense", "health", "hp",
            "maxhp", "max_hp", "lifesteal", "dps", "penetration", "armorpen", "armor_pen",
        };

        // --- probability / RNG field-name fragments (no gacha monetization) -------
        private static readonly string[] ProbabilityFragments =
        {
            "probability", "odds", "chance", "random", "roll", "droprate", "lootchance",
        };

        // --- where a "kind" string lives decides which allowlist it answers to ----
        private static readonly HashSet<string> ConvenienceContext = new HashSet<string>
        {
            "convenience", "token", "bump",
        };
        private static readonly HashSet<string> GrantContext = new HashSet<string>
        {
            "grant", "items", "free", "premium", "perks",
        };

        // --- the monetization JSON corpus (project-root relative) ----------------
        private static readonly string[] MonetizationFiles =
        {
            "Assets/StreamingAssets/Data/Canonical/packs.json",
            "Assets/Resources/Data/Canonical/packs.json",
            "Assets/StreamingAssets/Data/Canonical/skr_store.json",
            "Assets/StreamingAssets/Data/Canonical/skr_staking.json",
            "Assets/StreamingAssets/Data/Canonical/battle_monthly_packs.sample.json",
            "WorkOrders/economy_store_packs.sample.json",
        };

        /// <summary>
        /// Loads every monetization JSON and validates the covenant. Returns true when
        /// clean; false + a reason naming the offending file/field on any breach. Never
        /// throws (a per-file parse error becomes a failure line; a missing file is a
        /// skip-with-note).
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            string root = Path.GetDirectoryName(Application.dataPath); // project root (parent of /Assets)

            // 1) DERIVE the allowlists — JSON-sourced from skr_staking.json, unioned with
            //    the documented sets. Single source of truth, no blind hardcode.
            var convenienceKinds = new HashSet<string>();
            foreach (var k in DocumentedConvenienceKinds) convenienceKinds.Add(k);
            var grantKinds = new HashSet<string>();
            foreach (var k in DocumentedGrantKinds) grantKinds.Add(k);

            string stakingPath = Combine(root, "Assets/StreamingAssets/Data/Canonical/skr_staking.json");
            if (File.Exists(stakingPath))
            {
                try
                {
                    var staking = JObject.Parse(File.ReadAllText(stakingPath));
                    if (staking["convenienceAllowList"] is JArray ca)
                        foreach (var t in ca) convenienceKinds.Add(Norm(t.ToString()));
                    if (staking["perkKindEnum"] is JArray pk)
                        foreach (var t in pk) grantKinds.Add(Norm(t.ToString()));
                }
                catch (Exception ex)
                {
                    failures.Add($"skr_staking.json: could not derive allowlist ({ex.Message})");
                }
            }
            else
            {
                notes.Add("skr_staking.json MISSING — allowlist derived from documented set only");
            }

            // 2) Sweep every monetization file.
            foreach (var rel in MonetizationFiles)
            {
                string path = Combine(root, rel);
                if (!File.Exists(path))
                {
                    notes.Add($"{rel} MISSING — skipped");
                    continue;
                }

                JToken rootTok;
                try { rootTok = JToken.Parse(File.ReadAllText(path)); }
                catch (Exception ex)
                {
                    failures.Add($"{rel}: parse error ({ex.Message})");
                    continue;
                }

                Walk(rootTok, "", rel, failures, convenienceKinds, grantKinds);
            }

            if (failures.Count == 0)
            {
                reason = $"MONETIZATION COVENANT OK — {MonetizationFiles.Length} file(s) swept; " +
                         $"{convenienceKinds.Count} convenience + {grantKinds.Count} grant kind(s) allowlisted" +
                         (notes.Count > 0 ? $" ({string.Join("; ", notes)})" : "");
                return true;
            }

            reason = $"MONETIZATION COVENANT VIOLATION x{failures.Count}: " + string.Join(" | ", failures) +
                     (notes.Count > 0 ? $" [notes: {string.Join("; ", notes)}]" : "");
            return false;
        }

        // =====================================================================
        //  Recursive covenant walk — carries the OWNING KEY so a "kind" string is
        //  validated against the right allowlist (convenience leaf vs grant/perk).
        // =====================================================================
        private static void Walk(
            JToken node, string owningKey, string file,
            List<string> failures, HashSet<string> convenienceKinds, HashSet<string> grantKinds)
        {
            if (node is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    string name = prop.Name;
                    string lname = name.ToLowerInvariant();
                    JToken val = prop.Value;

                    // (a) probability / RNG field — no gacha monetization.
                    foreach (var frag in ProbabilityFragments)
                        if (lname.Contains(frag))
                        {
                            failures.Add($"{file}: forbidden RNG field '{name}' (probability/odds monetization is banned)");
                            break;
                        }

                    // (b) non-zero combat STAT field on a sellable.
                    if (CombatStatFields.Contains(lname) && IsNonZeroNumber(val))
                        failures.Add($"{file}: forbidden combat stat field '{name}'={val} on a sellable (zero-combat-power covenant)");

                    // (c) a 'kind'/'category' string — combat firewall + allowlist by context.
                    if ((name == "kind" || name == "category" || name == "grantKind") && val.Type == JTokenType.String)
                    {
                        string k = Norm(val.ToString());
                        if (BannedKinds.Contains(k))
                        {
                            failures.Add($"{file}: forbidden '{name}' value '{val}' (combat/stat grants are banned by the covenant)");
                        }
                        else if (name == "kind")
                        {
                            if (ConvenienceContext.Contains(owningKey) && !convenienceKinds.Contains(k))
                                failures.Add($"{file}: convenience kind '{val}' (under '{owningKey}') is NOT in the sanctioned allowlist — only time-saving kinds permitted");
                            else if (GrantContext.Contains(owningKey) && !grantKinds.Contains(k))
                                failures.Add($"{file}: grant/perk kind '{val}' (under '{owningKey}') is NOT in the sanctioned grant allowlist");
                        }
                    }

                    Walk(val, name, file, failures, convenienceKinds, grantKinds);
                }
            }
            else if (node is JArray arr)
            {
                // Array elements inherit the array's owning key (e.g. each element of
                // a "convenience": [...] array is a convenience-context object).
                foreach (var el in arr)
                    Walk(el, owningKey, file, failures, convenienceKinds, grantKinds);
            }
        }

        private static bool IsNonZeroNumber(JToken val)
        {
            if (val == null) return false;
            if (val.Type == JTokenType.Integer) return val.Value<long>() != 0L;
            if (val.Type == JTokenType.Float) return val.Value<double>() != 0d;
            return false;
        }

        /// <summary>Normalise a kind/category token: lower-case, '-'/' ' -> '_'. So
        /// "instant-build", "Instant Build" and "instant_build" all compare equal.</summary>
        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        private static string Combine(string root, string rel)
        {
            return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
