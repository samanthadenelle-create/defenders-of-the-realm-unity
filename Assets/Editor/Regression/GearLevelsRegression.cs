// =============================================================================
// GearLevelsRegression — WO-808 Option A data oracle: gear-levels.json integrity.
// -----------------------------------------------------------------------------
// Pins, from the FILES (no live scene):
//   1. Dual-copy law: Resources + StreamingAssets byte-identical.
//   2. Every band's statMult starts at exactly 1.0 (level 1 == authored baseline)
//      and climbs STRICTLY per level (an Improve is never a downgrade/no-op).
//   3. Cost curves: index 0 free, strictly increasing after (monotonic economy).
//   4. Coverage: every rarity used by weapons.json/armor.json has a band, so no
//      shipped item is silently ladder-less.
//   5. WO-814: every band carries a weaponAbilities slot (may be EMPTY - the shipped
//      state, identities are owner-authored later); any authored row has a reachable,
//      unique levelThreshold >= 2, an abilityId + name, and no damage-multiplier field.
// Runs inside DataRegression.RunAll as [gear-levels].
// =============================================================================

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class GearLevelsRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            string resPath = Path.Combine(Application.dataPath, "Resources/Data/Canonical/gear-levels.json");
            string samPath = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/gear-levels.json");

            if (!File.Exists(resPath)) failures.Add("gear-levels.json missing from Resources/Data/Canonical");
            if (!File.Exists(samPath)) failures.Add("gear-levels.json missing from StreamingAssets/Data/Canonical");

            if (failures.Count == 0)
            {
                // 1. Dual-copy byte identity.
                if (!File.ReadAllText(resPath).Equals(File.ReadAllText(samPath)))
                    failures.Add("gear-levels.json DIVERGED between Resources and StreamingAssets (dual-copy law)");

                var root = JObject.Parse(File.ReadAllText(resPath));
                var bands = root["bands"] as JArray;
                var bandRarities = new HashSet<string>();

                if (bands == null || bands.Count == 0)
                    failures.Add("gear-levels.json has no bands");
                else
                {
                    foreach (var b in bands)
                    {
                        string rarity = (string)b["rarity"] ?? "<null>";
                        bandRarities.Add(rarity.ToLowerInvariant());

                        // 2. statMult: starts 1.0, strictly increasing.
                        var mults = b["statMult"] as JArray;
                        if (mults == null || mults.Count < 2)
                        { failures.Add(rarity + ": statMult missing or < 2 levels"); continue; }
                        if (Mathf.Abs((float)mults[0] - 1f) > 1e-4f)
                            failures.Add(rarity + ": statMult[0] must be exactly 1.0 (authored baseline)");
                        for (int i = 1; i < mults.Count; i++)
                            if ((float)mults[i] <= (float)mults[i - 1])
                                failures.Add(rarity + ": statMult must climb strictly at index " + i);

                        // 3. Costs: index 0 free, strictly increasing after, aligned length.
                        foreach (var key in new[] { "costWood", "costIron" })
                        {
                            var costs = b[key] as JArray;
                            if (costs == null) { failures.Add(rarity + ": " + key + " missing"); continue; }
                            if (costs.Count != mults.Count)
                                failures.Add(rarity + ": " + key + " length != statMult length");
                            if (costs.Count > 0 && (int)costs[0] != 0)
                                failures.Add(rarity + ": " + key + "[0] must be 0 (level 1 = owned baseline)");
                            for (int i = 2; i < costs.Count; i++)
                                if ((int)costs[i] <= (int)costs[i - 1])
                                    failures.Add(rarity + ": " + key + " must climb strictly at index " + i);
                        }

                        // 5. WO-814 max-level weapon abilities (per-RARITY, weapons only).
                        //    The array must EXIST on every band - that is the slot the owner
                        //    authors into. Shipping it EMPTY is legal and is the current state;
                        //    an absent array is not, because a band with no slot silently opts
                        //    out of the feature forever.
                        var abilities = b["weaponAbilities"] as JArray;
                        if (abilities == null)
                        {
                            failures.Add(rarity + ": weaponAbilities array missing (WO-814 slot)");
                        }
                        else
                        {
                            var seenThresholds = new HashSet<int>();
                            for (int i = 0; i < abilities.Count; i++)
                            {
                                var a = abilities[i];
                                string where = rarity + ".weaponAbilities[" + i + "]";

                                int threshold = (int?)a["levelThreshold"] ?? 0;
                                if (threshold < 2)
                                    failures.Add(where + ": levelThreshold must be >= 2 (level 1 is the free baseline)");
                                if (threshold > mults.Count)
                                    failures.Add(where + ": levelThreshold " + threshold + " exceeds the band max "
                                                 + mults.Count + " (ability would be unreachable)");
                                if (!seenThresholds.Add(threshold))
                                    failures.Add(where + ": duplicate levelThreshold " + threshold);

                                if (string.IsNullOrEmpty((string)a["abilityId"]))
                                    failures.Add(where + ": abilityId missing (must resolve in abilities.json)");
                                if (string.IsNullOrEmpty((string)a["name"]))
                                    failures.Add(where + ": name missing (it is the '<ability>' in the "
                                                 + "'Lv N: <ability>' preview line)");

                                // The owner's design law: a max-level ability CHANGES PLAYSTYLE, it is
                                // not '+35% more damage'. The row shape has no damage-multiplier field;
                                // this pins that an authored row cannot smuggle one back in.
                                foreach (var banned in new[] { "damageMult", "statMult", "damageMultiplier" })
                                    if (a[banned] != null)
                                        failures.Add(where + ": '" + banned + "' is not part of the ability row - "
                                                     + "max-level abilities change behaviour, not raw damage");
                            }
                        }
                    }
                }

                // 4. Coverage vs the shipped gear catalogs.
                foreach (var (file, arrayKey) in new[] { ("weapons.json", "weapons"), ("armor.json", "armor") })
                {
                    string p = Path.Combine(Application.dataPath, "Resources/Data/Canonical/" + file);
                    if (!File.Exists(p)) { failures.Add(file + " missing (coverage check)"); continue; }
                    var cat = JObject.Parse(File.ReadAllText(p));
                    var items = cat[arrayKey] as JArray;
                    if (items == null) continue;
                    var missing = new HashSet<string>();
                    foreach (var it in items)
                    {
                        string r = ((string)it["rarity"] ?? "").ToLowerInvariant();
                        if (!string.IsNullOrEmpty(r) && !bandRarities.Contains(r)) missing.Add(r);
                    }
                    foreach (var r in missing)
                        failures.Add(file + " uses rarity '" + r + "' with NO gear-levels band (item silently ladder-less)");
                }
            }

            if (failures.Count > 0)
            {
                reason = "GEAR LEVELS FAIL - " + string.Join("; ", failures);
                return false;
            }
            reason = "GEAR LEVELS OK - dual-copy identical, all bands baseline-1.0 + strictly-climbing mults, " +
                     "free-at-L1 + monotonic costs, full rarity coverage vs weapons/armor catalogs, " +
                     "WO-814 weaponAbilities slot present on every band with reachable thresholds";
            return true;
        }
    }
}
