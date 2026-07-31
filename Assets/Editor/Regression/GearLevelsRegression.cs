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
                     "free-at-L1 + monotonic costs, full rarity coverage vs weapons/armor catalogs";
            return true;
        }
    }
}
