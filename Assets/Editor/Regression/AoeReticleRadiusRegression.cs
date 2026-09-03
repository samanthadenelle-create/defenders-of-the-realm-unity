// =============================================================================
// AoeReticleRadiusRegression - WO-1345 oracle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor
//
// PINS TWO THINGS, AND THE SECOND IS THE WHOLE TICKET:
//
//  (a) THE OWNER'S MAPPING. The key AoeCastReticle wires must still be the key she
//      tagged in Assets/Editor/VfxManualPicks.json, pointing at the prefab path she
//      named - VERBATIM. If she retags it to a different prefab and nobody updates
//      the wiring, this fails loudly instead of the reticle silently drawing the
//      wrong art. (Her isLoop / scale values are REPORTED, never asserted: they are
//      hers to change, and an oracle that fails on her retag would be a cage.)
//
//  (b) THE RETICLE'S SIZE STILL COMES FROM ABILITY DATA. A reticle that lies about
//      where the damage lands is worse than no reticle, and the way that regression
//      arrives is somebody quietly pinning the ring to a constant scale. So this
//      reads TWO REAL AoE abilities out of abilities.json - it hardcodes neither
//      their ids' radii nor an expected scale - and requires that:
//         * two different radii produce two DIFFERENT scales,
//         * the scale ratio EQUALS the radius ratio (proportional, not merely
//           different - a lookup table would pass "different" and fail this),
//         * scale == radius / the measured ring radius at scale 1,
//         * the owner's tag scale multiplies ON TOP of that, rather than replacing it.
//      Plus a source pin that HeroAbilities still hands the reticle def.Range - the
//      ability's own authored blast radius, the same number ResolveEffect gives Blast().
//
//  (c) ONE SPAWNER. AoeCastReticle must not Instantiate or Resources.Load a prefab
//      itself; the ring may only come through VFXManager.PlayKey.
//
// Registered in DataRegression by the COMMITTER, never by the lane that wrote it.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// WO-1345: pins the AoE reticle's owner-tagged key -> prefab mapping and pins that
    /// its ring size derives from the ability's own radius data rather than a constant.
    /// </summary>
    public static class AoeReticleRadiusRegression
    {
        // Two REAL AoE abilities with different authored blast radii. Their radii are
        // READ from abilities.json - only the ids live here, so a tuning change to
        // either radius flows through instead of breaking the oracle.
        private const string SmallAbilityId = "mage.frost-nova";
        private const string LargeAbilityId = "mage.meteor";

        private const float Epsilon = 0.0005f;

        public static bool Run(out string reason)
        {
            string assets = Application.dataPath;
            string picks = Path.Combine(assets, "Editor/VfxManualPicks.json");
            string abilitiesSa = Path.Combine(assets, "StreamingAssets/Data/Canonical/abilities.json");
            string hero = Path.Combine(assets, "_Modules/Village/Hero/HeroAbilities.cs");
            string reticle = Path.Combine(assets, "_Modules/Village/Vfx/AoeCastReticle.cs");

            var fails = new List<string>();
            var notes = new List<string>();

            // ── (a) her tag, verbatim ────────────────────────────────────────────────
            string picksText = ReadOrFail(picks, fails);
            if (picksText != null)
            {
                var row = Regex.Match(picksText,
                    "\\{[^{}]*\"key\"\\s*:\\s*\"" + Regex.Escape(AoeCastReticle.VfxKey) + "\"[^{}]*\\}",
                    RegexOptions.Singleline);
                if (!row.Success)
                {
                    fails.Add("VfxManualPicks.json has NO row for the owner key '" + AoeCastReticle.VfxKey +
                              "' - the AoE reticle is wired to a key she no longer tags.");
                }
                else
                {
                    string path = Group(row.Value, "\"prefabPath\"\\s*:\\s*\"([^\"]+)\"");
                    string norm = (path ?? string.Empty).Replace("\\\\", "/").Replace("\\", "/");
                    string want = AoeCastReticle.TaggedPrefabPath.Replace("\\", "/");
                    if (!string.Equals(norm, want, System.StringComparison.OrdinalIgnoreCase))
                        fails.Add("owner tag '" + AoeCastReticle.VfxKey + "' now names prefab '" + norm +
                                  "' but AoeCastReticle.TaggedPrefabPath still says '" + want +
                                  "'. Re-measure the ring radius for the NEW prefab before re-pinning this - " +
                                  "AoeCastReticle.PrefabRingRadiusAtUnitScale is a measurement of the OLD one.");
                    notes.Add("tag: key='" + AoeCastReticle.VfxKey + "' prefab='" + norm +
                              "' isLoop=" + (Group(row.Value, "\"isLoop\"\\s*:\\s*(\\w+)") ?? "?") +
                              " scale=" + (Group(row.Value, "\"scale\"\\s*:\\s*([\\d.]+)") ?? "?") +
                              " (reported, not asserted - hers to change).");
                }
            }

            // ── (b) the ring is sized from ability DATA, proportionally ─────────────
            string abilities = ReadOrFail(abilitiesSa, fails);
            float small = ReadRange(abilities, SmallAbilityId, fails);
            float large = ReadRange(abilities, LargeAbilityId, fails);

            if (small > 0f && large > 0f)
            {
                if (Mathf.Approximately(small, large))
                {
                    fails.Add("the two probe abilities now share a radius (" + small.ToString("0.00") +
                              "m) - this oracle can no longer tell a data-driven ring from a constant one. " +
                              "Point SmallAbilityId/LargeAbilityId at two AoE abilities with different radii.");
                }
                else
                {
                    float sSmall = AoeCastReticle.LocalScaleForRadius(small, 1f);
                    float sLarge = AoeCastReticle.LocalScaleForRadius(large, 1f);

                    if (Mathf.Abs(sSmall - sLarge) <= Epsilon)
                        fails.Add("PINNED TO A CONSTANT: radii " + small.ToString("0.00") + "m and " +
                                  large.ToString("0.00") + "m both produce localScale " +
                                  sSmall.ToString("0.0000") + ". Every AoE would draw the same footprint " +
                                  "regardless of its reach - the exact defect WO-1345 exists to prevent.");

                    // Proportional, not merely different: a per-spell lookup table would pass
                    // "different" and fail this.
                    float wantRatio = large / small;
                    float gotRatio = sSmall > 0f ? sLarge / sSmall : 0f;
                    if (Mathf.Abs(gotRatio - wantRatio) > 0.001f)
                        fails.Add("the ring is NOT proportional to the radius: radius ratio " +
                                  wantRatio.ToString("0.000") + " but scale ratio " + gotRatio.ToString("0.000") +
                                  ". Scale must be a straight function of the ability's radius, not a table.");

                    // The measured mapping itself.
                    float wantSmall = small / AoeCastReticle.PrefabRingRadiusAtUnitScale;
                    if (Mathf.Abs(sSmall - wantSmall) > Epsilon)
                        fails.Add("radius->scale drifted: " + small.ToString("0.00") + "m gave " +
                                  sSmall.ToString("0.0000") + ", expected " + wantSmall.ToString("0.0000") +
                                  " (= radius / PrefabRingRadiusAtUnitScale " +
                                  AoeCastReticle.PrefabRingRadiusAtUnitScale.ToString("0.00") + "m).");

                    // The owner's tag scale is a MULTIPLIER on top, never the radius.
                    float doubled = AoeCastReticle.LocalScaleForRadius(small, 2f);
                    if (Mathf.Abs(doubled - sSmall * 2f) > Epsilon)
                        fails.Add("the owner's tag scale is not applied as a multiplier on top of the " +
                                  "data-derived radius (multiplier 2 gave " + doubled.ToString("0.0000") +
                                  ", expected " + (sSmall * 2f).ToString("0.0000") + ").");

                    notes.Add("mapping: " + SmallAbilityId + " r=" + small.ToString("0.00") + "m -> scale " +
                              sSmall.ToString("0.000") + " | " + LargeAbilityId + " r=" + large.ToString("0.00") +
                              "m -> scale " + sLarge.ToString("0.000") + " (ring radius at scale 1 = " +
                              AoeCastReticle.PrefabRingRadiusAtUnitScale.ToString("0.00") + "m).");
                }
            }

            // The wiring must hand the reticle the ABILITY's own radius, not a literal.
            string heroText = ReadOrFail(hero, fails);
            if (heroText != null &&
                !Regex.IsMatch(heroText, @"_aoeReticle\.Show\(\s*def\.Name\s*,\s*def\.Range\s*,"))
                fails.Add("HeroAbilities no longer calls _aoeReticle.Show(def.Name, def.Range, ...) - " +
                          "the reticle must be handed the ability's OWN authored blast radius (the same " +
                          "def.Range ResolveEffect passes to Blast), never a literal or a per-spell table.");

            // ── (c) one spawner ─────────────────────────────────────────────────────
            string reticleText = ReadOrFail(reticle, fails);
            if (reticleText != null)
            {
                string code = StripComments(reticleText);
                if (Regex.IsMatch(code, @"\bInstantiate\s*\(") ||
                    Regex.IsMatch(code, @"Resources\.Load\s*<\s*GameObject\s*>"))
                    fails.Add("AoeCastReticle instantiates or loads a prefab itself - the ring may only " +
                              "come through VFXManager.PlayKey. No second spawner, no second pool.");
                if (!code.Contains("VFXManager.PlayKey"))
                    fails.Add("AoeCastReticle no longer routes through VFXManager.PlayKey - the reticle " +
                              "must use the one shared pooled spawner.");
            }

            reason = fails.Count == 0
                ? "AoE reticle OK - " + string.Join(" | ", notes)
                : string.Join(" || ", fails);
            return fails.Count == 0;
        }

        private static string ReadOrFail(string path, List<string> fails)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            fails.Add("missing file: " + path);
            return null;
        }

        private static string Group(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// The authored blast radius of <paramref name="id"/> from abilities.json - the
        /// "range" field of the object that declares that id. Read, never assumed.
        /// </summary>
        private static float ReadRange(string json, string id, List<string> fails)
        {
            if (string.IsNullOrEmpty(id) || json == null) return 0f;
            int at = json.IndexOf("\"" + id + "\"", System.StringComparison.Ordinal);
            if (at < 0)
            {
                fails.Add("abilities.json has no ability with id '" + id +
                          "' - repoint the oracle's probe ids at two live AoE abilities.");
                return 0f;
            }
            var m = Regex.Match(json.Substring(at), "\"range\"\\s*:\\s*([\\d.]+)");
            if (!m.Success)
            {
                fails.Add("ability '" + id + "' declares no 'range' (its authored blast radius).");
                return 0f;
            }
            float v;
            if (!float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v))
            {
                fails.Add("ability '" + id + "' has an unparseable range '" + m.Groups[1].Value + "'.");
                return 0f;
            }
            return v;
        }

        /// <summary>Drops // and /* */ comments so a WORD in prose cannot pass or fail a code pin.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", " ");
            return src;
        }
    }
}
