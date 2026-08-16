// =============================================================================
// RangedPrimaryRegression — pins WO-1105 (Sylas plays as an ARCHER).
// -----------------------------------------------------------------------------
// regression-registry: registered by the committer (do NOT self-register here —
// DataRegression.cs is lane-fenced; the orchestrator adds the [ranged-primary] row).
//
// WHY: WO-1105 made the PRIMARY attack input resolve through the class's ranged
// basic (the locked Q def) for any class whose basic is a ranged one, and seats a
// bow's grip on the stave SURFACE (GripAnchor.BowGrip) instead of the bounds
// centre. Both rest on DATA that a catalog regeneration can silently rewrite.
// This suite pins that data:
//
//   Case 1 — CROSSBOW EXCLUSION (owner ruling R4a, the load-bearing guard).
//            The RUNTIME weapons catalog (the Resources copy, the one that WINS at
//            runtime) must contain NO weapon whose id/mesh/name carries the token
//            "crossbow". Measured 2026-08-16: Resources = 0 crossbows,
//            StreamingAssets = 125. A crossbow inverts R4's axis rule (widest -> X,
//            narrowest -> Y; it is held across the body, not upright), so every one
//            of those 125 rows would seat WRONG under the bow grip derivation. The
//            editor menu Defenders/Catalog/Generate Gear Catalog re-inflates the
//            Resources copy from ~96 rows to ~431 and writes BOTH copies — running
//            it would pull all 125 in at once. This case makes that loud instead of
//            silent. DELETE THIS CASE ONLY when the inverted crossbow mapping is
//            implemented AND proven on device (the owner's "for simplicity, let's
//            not include any crossbows until we have verified" is the gate).
//
//   Case 2 — the RANGER's basic is still a RANGED basic. classes.ranger.abilities.q
//            must be effect 'strike' with range > 0 and a cooldown > 0 (R3: "an
//            archer is not a click-spam weapon" — the bow primary's cooldown is read
//            from THIS number, never a literal in code).
//
//   Case 3 — the ranged-primary DISCRIMINATOR still separates the classes it must.
//            HeroAbilities.TryGetRangedPrimary accepts a basic whose effect is
//            strike/drainshot AND whose range exceeds the hero's melee reach by more
//            than 2x. Pinned here against a reference melee reach of 3.2 m
//            (PlayerAttackController's serialized fallback): ranger must PASS and
//            knight must FAIL. If knight.q is ever re-authored into a long-range
//            strike, the Knight would silently inherit a ranged primary and lose his
//            swing — this case fails first.
//
// Parsed straight from the JSON (never through a live catalog), so a copy that
// parses but was only half-regenerated is still caught.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RangedPrimaryRegression
    {
        private const string AbilitiesResourcesPath = "Assets/Resources/Data/Canonical/abilities.json";
        private const string WeaponsResourcesPath   = "Assets/Resources/Data/Canonical/weapons.json";

        /// <summary>The name token that identifies an excluded weapon (owner R4a, case-insensitive).</summary>
        private const string ExcludedWeaponToken = "crossbow";

        /// <summary>PlayerAttackController's serialized fallback melee reach (m) — the reference the
        /// discriminator is pinned against. Not a gameplay value; a test fixture.</summary>
        private const float ReferenceMeleeReach = 3.2f;

        /// <summary>Mirrors HeroAbilities.RangedPrimaryReachFactor.</summary>
        private const float RangedPrimaryReachFactor = 2f;

        /// <summary>Standalone batch entry — prints the RANGED_PRIMARY_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RANGED_PRIMARY_OK - " + reason);
            else Debug.LogError("RANGED_PRIMARY_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([ranged-primary]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "crossbow-exclusion", () => Case1_NoCrossbowInRuntimeCatalog(failures, notes));

            JObject abilities = null;
            Case(failures, "parse-abilities", () =>
            {
                string json = ReadText(AbilitiesResourcesPath);
                if (json == null) { failures.Add("[parse-abilities] cannot read " + AbilitiesResourcesPath); return; }
                abilities = JObject.Parse(json);
            });

            if (abilities != null)
            {
                Case(failures, "ranger-basic-ranged", () => Case2_RangerBasicIsRanged(abilities, failures, notes));
                Case(failures, "discriminator", () => Case3_DiscriminatorStillSeparates(abilities, failures, notes));
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "RANGED PRIMARY OK - 3/3 cases pass (no crossbow can reach the runtime weapons " +
                         "catalog while the R4a exclusion stands, the ranger basic is still a costed-" +
                         "cooldown ranged strike, and the ranged-primary discriminator still admits the " +
                         "ranger while rejecting the knight)" + noteStr;
                return true;
            }
            reason = "RANGED PRIMARY FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  CASE 1 — no crossbow may reach the RUNTIME (Resources) weapons catalog
        // =====================================================================
        private static void Case1_NoCrossbowInRuntimeCatalog(List<string> failures, List<string> notes)
        {
            string json = ReadText(WeaponsResourcesPath);
            if (json == null)
            {
                failures.Add("[crossbow-exclusion] cannot read " + WeaponsResourcesPath +
                             " - the runtime weapons catalog is the copy that WINS at runtime; " +
                             "without it this guard cannot prove the exclusion holds.");
                return;
            }

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex)
            {
                failures.Add("[crossbow-exclusion] " + WeaponsResourcesPath + " does not parse: " + ex.Message);
                return;
            }

            var weapons = root["weapons"] as JArray;
            if (weapons == null)
            {
                failures.Add("[crossbow-exclusion] " + WeaponsResourcesPath + " has no 'weapons' array.");
                return;
            }

            var offenders = new List<string>();
            foreach (var w in weapons)
            {
                string id = (string)w["id"] ?? string.Empty;
                string mesh = (string)w["mesh"] ?? string.Empty;
                string name = (string)w["name"] ?? string.Empty;
                // 'category' is the field the 431-row side actually keys crossbows on (measured:
                // the StreamingAssets copy carries category='crossbow'), so it is scanned too.
                string category = (string)w["category"] ?? string.Empty;
                if (Contains(id) || Contains(mesh) || Contains(name) || Contains(category))
                    offenders.Add(string.IsNullOrEmpty(id) ? (string.IsNullOrEmpty(mesh) ? name : mesh) : id);
            }

            if (offenders.Count > 0)
            {
                failures.Add("[crossbow-exclusion] the RUNTIME weapons catalog carries " + offenders.Count +
                             " crossbow row(s) (e.g. " + string.Join(", ", offenders.GetRange(0, Math.Min(5, offenders.Count))) +
                             ") - owner ruling R4a EXCLUDES crossbows until the plain-bow grip is proven " +
                             "on device. A crossbow is widest on X and narrowest on Y and is held across " +
                             "the body, so WeaponBoundsOrient's longest-to-+Y premise (and therefore the " +
                             "BowGrip derivation) is wrong for it by construction and it would seat wrong. " +
                             "Most likely cause: Defenders/Catalog/Generate Gear Catalog was run, which " +
                             "re-inflates this copy and pulls the StreamingAssets crossbow rows in.");
                return;
            }

            notes.Add("weapons(runtime)=" + weapons.Count + " rows, 0 crossbows");
        }

        private static bool Contains(string s)
            => !string.IsNullOrEmpty(s) &&
               s.IndexOf(ExcludedWeaponToken, StringComparison.OrdinalIgnoreCase) >= 0;

        // =====================================================================
        //  CASE 2 — the ranger's basic is still a ranged strike with a real cooldown
        // =====================================================================
        private static void Case2_RangerBasicIsRanged(JObject root, List<string> failures, List<string> notes)
        {
            JToken q = root.SelectToken("classes.ranger.abilities.q");
            if (q == null)
            {
                failures.Add("[ranger-basic-ranged] classes.ranger.abilities.q is missing - the ranged " +
                             "primary resolves through the LOCKED Q def; with no def the archer falls " +
                             "back to the melee sweep, which is the exact WO-1105 defect.");
                return;
            }

            string effect = ((string)q["effect"] ?? string.Empty).Trim().ToLowerInvariant();
            float range = (float?)q["range"] ?? 0f;
            float cooldown = (float?)q["cooldown"] ?? 0f;
            string id = (string)q["id"] ?? "(no id)";

            if (effect != "strike" && effect != "drainshot")
                failures.Add("[ranger-basic-ranged] ranger.q effect='" + effect + "' is not a projectile " +
                             "shape - only 'strike'/'drainshot' route through ResolveStrikeLike -> " +
                             "LaunchProjectile (damage on ARRIVAL), which is what makes an arrow read as " +
                             "an arrow and what carries the WO-997 Focus hit-confirm restore.");
            if (range <= 0f)
                failures.Add("[ranger-basic-ranged] ranger.q range=" + range + " must be > 0 - the auto-" +
                             "target engage radius (R2) and the shot's reach are both READ from it.");
            if (cooldown <= 0f)
                failures.Add("[ranger-basic-ranged] ranger.q cooldown=" + cooldown + " must be > 0 - owner " +
                             "ruling R3: the bow primary carries a REAL cooldown (an archer is not a " +
                             "click-spam weapon), and the offhand dagger is what covers while it cools.");

            notes.Add("ranger.q=" + id + " effect=" + effect + " range=" + range.ToString("0.##") +
                      "m cd=" + cooldown.ToString("0.##") + "s");
        }

        // =====================================================================
        //  CASE 3 — the discriminator still admits the ranger and rejects the knight
        // =====================================================================
        private static void Case3_DiscriminatorStillSeparates(JObject root, List<string> failures, List<string> notes)
        {
            bool ranger = IsRangedPrimary(root, "ranger", out string rWhy);
            bool knight = IsRangedPrimary(root, "knight", out string kWhy);

            if (!ranger)
                failures.Add("[discriminator] ranger.q no longer qualifies as a RANGED primary (" + rWhy +
                             ") - Sylas would go back to swinging a sword as his default verb.");
            if (knight)
                failures.Add("[discriminator] knight.q NOW qualifies as a ranged primary (" + kWhy +
                             ") - the Knight would silently lose his melee swing as the primary verb. " +
                             "WO-1105 requires the Knight path to be unaffected.");

            notes.Add("discriminator: ranger=" + (ranger ? "ranged" : "melee") + " (" + rWhy + "), knight=" +
                      (knight ? "ranged" : "melee") + " (" + kWhy + ")");
        }

        /// <summary>Mirrors HeroAbilities.TryGetRangedPrimary against the authored JSON.</summary>
        private static bool IsRangedPrimary(JObject root, string cls, out string why)
        {
            JToken q = root.SelectToken("classes." + cls + ".abilities.q");
            if (q == null) { why = "no q def"; return false; }
            string effect = ((string)q["effect"] ?? string.Empty).Trim().ToLowerInvariant();
            float range = (float?)q["range"] ?? 0f;
            if (effect != "strike" && effect != "drainshot")
            {
                why = "effect='" + effect + "' is not a projectile shape";
                return false;
            }
            float threshold = ReferenceMeleeReach * RangedPrimaryReachFactor;
            if (range <= threshold)
            {
                why = "range " + range.ToString("0.##") + "m <= " + threshold.ToString("0.##") + "m threshold";
                return false;
            }
            why = "effect='" + effect + "' range " + range.ToString("0.##") + "m > " +
                  threshold.ToString("0.##") + "m threshold";
            return true;
        }

        // ── plumbing ─────────────────────────────────────────────────────────
        private static void Case(List<string> failures, string label, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + label + "] threw: " + ex.Message); }
        }

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }
    }
}
