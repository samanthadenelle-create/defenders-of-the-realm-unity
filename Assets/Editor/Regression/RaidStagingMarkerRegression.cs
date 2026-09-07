// =============================================================================
// RaidStagingMarkerRegression [raid-staging]   Marker: RAID_STAGING_OK / _FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. No PlayMode, no scene load, no bake - it
// MEASURES the generated geometry from the same two canonical files the builder
// reads (scene-configs.json + structures-catalog.json) and re-reads the builder's
// own constants out of its source, so this suite cannot drift from the code it pins.
//
// WHY IT EXISTS (WO-1520, owner ruling 2026-09-06, verbatim):
//
//   "battle for raids should start in some staging area outside of the attack range
//    of everything so time starts on first engage. as soon as you spawn in you start
//    dying without having even a second to deploy some troops"
//
// THE MEASUREMENT THAT MADE THIS SUITE RED BEFORE THE FIX. Device capture
// raid-no-abilities-2026-09-06.log, 12:59:47:
//
//   [Flow:Hero] recover: re-homed carried hero (0.00, 0.08, -39.00) (seat=baked marker)
//   [Flow:Raid] stars settled: 0 (cleared=False destruction=32 % elapsed=45s/180s ...)
//   [Flow:Raid] hero death settle: partial loot for 32% razed
//
// -39.00 is HeroStartPoint_PlayerSpawn at -(radius + 8) on raider_camp_small
// (baseRadius 31). The outer turret band sits at radius - max(2.5, segW), i.e. no
// further out than 28.5 m, and its range is capped at radius * 0.55 = 17.05 m - so
// turret fire reaches ~45.5 m from the arena centre. The hero was seated 6.5 m INSIDE
// that, on frame one, with the 180 s clock already counting. Case 2 asserts exactly
// that legacy number is unsafe, so the defect can never be re-introduced as "close
// enough", and Case 1 / Case 4 / Case 5 pin the three halves of the fix.
//
// Contract: public static bool Run(out string reason) - DataRegression-shaped, true =
// pass + a one-line summary, false = fail + the offending detail. NEVER throws (all
// I/O, JSON and regex is guarded). ASCII-only.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.RaidStagingMarkerRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RaidStagingMarkerRegression
    {
        // ---- Canonical data ---------------------------------------------------
        private const string ConfigsRes = "Assets/Resources/Data/Canonical/scene-configs.json";
        private const string CatalogRes = "Assets/Resources/Data/Canonical/structures-catalog.json";

        // ---- Source under test ------------------------------------------------
        private const string GeneratorSrc = "Assets/Editor/WallTools/RaidBaseGenerator.cs";
        private const string SensorSrc = "Assets/_Modules/Village/Enemies/Perception/AwarenessSensor.cs";
        private const string EnsurerSrc = "Assets/_Modules/Village/Hero/HeroControlEnsurer.cs";
        private const string ScoringSrc = "Assets/_Modules/Village/Troops/RaidScoring.cs";

        /// <summary>The marker name the builder authors and the hero seat prefers.</summary>
        private const string StagingMarkerName = "RaidStagingPoint";

        // =====================================================================
        //  Entry points
        // =====================================================================

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(ok ? $"RAID_STAGING_OK {reason}" : $"RAID_STAGING_FAIL {reason}");
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Safe(failures, "Case1", () => Case1_GeneratorAuthorsStagingMarker(notes));
            Safe(failures, "Case2", () => Case2_StagingClearsEveryThreat(notes));
            Safe(failures, "Case3", () => Case3_PerceptionRadiusPinned(notes));
            Safe(failures, "Case4", () => Case4_HeroSeatPrefersStaging(notes));
            Safe(failures, "Case5", () => Case5_ClockGatedOnFirstEngagement(notes));

            if (failures.Count > 0)
            {
                reason = string.Join(" | ", failures);
                return false;
            }
            reason = string.Join("; ", notes);
            return true;
        }

        private static void Safe(List<string> failures, string label, Func<string> body)
        {
            try
            {
                string f = body();
                if (!string.IsNullOrEmpty(f)) failures.Add($"{label}: {f}");
            }
            catch (Exception e)
            {
                failures.Add($"{label}: THREW {e.GetType().Name} {e.Message}");
            }
        }

        // =====================================================================
        //  CASE 1 - the builder authors a staging marker, and ASSERTS it.
        // =====================================================================

        private static string Case1_GeneratorAuthorsStagingMarker(List<string> notes)
        {
            string src = ReadText(GeneratorSrc);
            if (src == null)
            {
                // hollow-pass-ok: this is the FAIL arm already - the non-empty return IS the
                // failure (Safe() adds it to `failures`, Run() then returns false naming the
                // missing path). HollowPassScanner blanks string-literal contents in its
                // skeleton, so `return $"...";` reads to it as a bare `return;`.
                return $"cannot read {GeneratorSrc}";
            }

            if (!src.Contains($"\"{StagingMarkerName}\""))
                return $"{GeneratorSrc} never names the staging marker \"{StagingMarkerName}\" - " +
                       "the raid base bakes no staging area (WO-1520 sec.2.1).";

            if (!src.Contains("PlaceStagingMarker"))
                return "the builder has no PlaceStagingMarker step - the staging point is not authored.";

            // The distance must be COMPUTED from the measured reaches, never a literal.
            if (!src.Contains("towerMaxReach") || !src.Contains("defenderReach"))
                return "PlaceStagingMarker does not read BOTH the turret reach and the defender reach - " +
                       "WO-1520 sec.3 forbids picking the staging distance by eye.";

            if (!src.Contains("MaxReach"))
                return "PlaceTowers does not report MaxReach, so the staging distance cannot be measured " +
                       "against the turrets it actually placed.";

            // The assert must be LOUD and must not be softenable into a warning.
            if (!src.Contains("STAGING ASSERT FAILED"))
                return "the builder has no staging assertion - WO-1520 sec.4 requires the unsatisfiable " +
                       "case to be reported as a finding, not silently clamped.";

            notes.Add("builder authors + asserts the staging marker");
            return null;
        }

        // =====================================================================
        //  CASE 2 - THE MEASUREMENT. For every generated raid base, the staging
        //  distance clears every turret's reach and every defender's awareness
        //  radius; and the LEGACY seat did not.
        // =====================================================================

        private static string Case2_StagingClearsEveryThreat(List<string> notes)
        {
            string src = ReadText(GeneratorSrc);
            if (src == null)
            {
                // hollow-pass-ok: this is the FAIL arm already - the non-empty return IS the
                // failure (Safe() adds it to `failures`, Run() then returns false naming the
                // missing path). HollowPassScanner blanks string-literal contents in its
                // skeleton, so `return $"...";` reads to it as a bare `return;`.
                return $"cannot read {GeneratorSrc}";
            }

            // Re-read the builder's own constants so this suite measures what the builder
            // measures. A changed constant changes the expected numbers here automatically -
            // it never leaves a stale copy behind (CLAUDE.md: duplicated state is the bug).
            float mapHalf = ConstFloat(src, "MapHalfExtent");
            float rangeFrac = ConstFloat(src, "TowerRangeFractionOfRadius");
            float rangeFloor = ConstFloat(src, "TowerRangeFloor");
            float innerFrac = ConstFloat(src, "InnerBandFraction");
            float outerShareK = ConstFloat(src, "OuterBandShare");
            float maxSegW = ConstFloat(src, "MaxSegmentWidth");
            float stagingMargin = ConstFloat(src, "StagingMargin");
            float planeEdge = ConstFloat(src, "StagingPlaneEdgeMargin");
            float perception = ConstFloat(src, "DefenderPerceptionRadius");

            if (float.IsNaN(mapHalf) || float.IsNaN(rangeFrac) || float.IsNaN(rangeFloor) ||
                float.IsNaN(innerFrac) || float.IsNaN(outerShareK) || float.IsNaN(maxSegW) ||
                float.IsNaN(stagingMargin) || float.IsNaN(planeEdge) || float.IsNaN(perception))
                return "could not re-read the builder's staging constants (MapHalfExtent / " +
                       "TowerRangeFractionOfRadius / TowerRangeFloor / InnerBandFraction / OuterBandShare / " +
                       "MaxSegmentWidth / StagingMargin / StagingPlaneEdgeMargin / DefenderPerceptionRadius) - " +
                       "one of them was renamed or made non-const.";

            var ranges = ReadTowerRanges();
            if (ranges == null || ranges.Count == 0)
            {
                // hollow-pass-ok: this is the FAIL arm already - the non-empty return IS the
                // failure (Safe() adds it to `failures`, Run() then returns false naming the
                // missing path). HollowPassScanner blanks string-literal contents in its
                // skeleton, so `return $"...";` reads to it as a bare `return;`.
                return $"cannot read turret ranges out of {CatalogRes}";
            }

            var configs = ReadRaidConfigs();
            if (configs == null || configs.Count == 0)
            {
                // hollow-pass-ok: this is the FAIL arm already - the non-empty return IS the
                // failure (Safe() adds it to `failures`, Run() then returns false naming the
                // missing path). HollowPassScanner blanks string-literal contents in its
                // skeleton, so `return $"...";` reads to it as a bare `return;`.
                return $"cannot read any raid config with a baseRadius out of {ConfigsRes}";
            }

            var lines = new List<string>();
            var fails = new List<string>();
            int legacyUnsafe = 0;

            foreach (var c in configs)
            {
                float radius = Mathf.Max(10f, Mathf.Min(c.BaseRadius, mapHalf * 0.9f));

                // Turret reach - an UPPER BOUND of what the builder places. The panel width is
                // measured off a prefab at build time (BuildRing), so it is not knowable here.
                // outerBand = radius - max(2.5, segW), so a NARROW panel puts the band furthest
                // OUT: the bound that maximises reach is the 2.5 m floor, not MaxSegmentWidth.
                float rangeCap = Mathf.Max(rangeFloor, radius * rangeFrac);
                float maxRange = 0f;
                foreach (string id in c.TowerTypes)
                {
                    float r = ranges.TryGetValue(id, out float cat) && cat > 0.5f ? cat : 18f;
                    maxRange = Mathf.Max(maxRange, Mathf.Clamp(r, rangeFloor, rangeCap));
                }
                if (c.TowerTypes.Count == 0) maxRange = Mathf.Clamp(18f, rangeFloor, rangeCap);

                float outerBand = Mathf.Max(4f, radius - 2.5f);
                float outerReach = outerBand + maxRange;

                bool overlapping = !string.Equals(c.PlacementStyle, "Cardinal", StringComparison.OrdinalIgnoreCase);
                float innerReach = 0f;
                if (overlapping && outerShareK < 1f)
                    innerReach = Mathf.Max(3f, radius * innerFrac) + maxRange;

                float towerReach = Mathf.Max(outerReach, innerReach);

                // Defender reach - RaidGarrisonSpawner.cs:168 rings the composition at
                // max(2, baseRadius * 0.5), and every spawn carries an AwarenessSensor scan.
                float defenderReach = Mathf.Max(2f, Mathf.Max(c.BaseRadius, radius) * 0.5f) + perception;

                float threatReach = Mathf.Max(towerReach, defenderReach);
                float required = threatReach + stagingMargin;

                float axisBudget = mapHalf - planeEdge;
                float diagonalBudget = axisBudget * Mathf.Sqrt(2f);
                float placed = Mathf.Min(required, diagonalBudget);
                float clearance = placed - threatReach;

                // The legacy seat this ticket replaces: HeroStartPoint_PlayerSpawn at -(radius + 8).
                float legacy = radius + 8f;
                bool legacyWasSafe = legacy >= threatReach;
                if (!legacyWasSafe) legacyUnsafe++;

                lines.Add($"{c.Id}: staging {placed:F1}m vs threat {threatReach:F1}m " +
                          $"(turrets {towerReach:F1}m, defenders {defenderReach:F1}m) = " +
                          $"{clearance:F1}m clear; legacy seat {legacy:F1}m was " +
                          $"{(legacyWasSafe ? "safe" : "INSIDE by " + (threatReach - legacy).ToString("F1") + "m")}");

                if (clearance < 0f)
                    fails.Add($"{c.Id} stages {(-clearance):F1}m INSIDE the reach of its own base " +
                              $"(needs {required:F1}m, the {mapHalf * 2f:F0}m plane offers {diagonalBudget:F1}m). " +
                              "That is a finding about the LAYOUT - lower baseRadius or the turret range cap, " +
                              "or raise RaidNavBake.GroundScale. Do not soften this assert (WO-1520 sec.3).");
                else if (clearance < stagingMargin - 0.01f)
                    fails.Add($"{c.Id} keeps only {clearance:F1}m of clear air, under the builder's own " +
                              $"StagingMargin of {stagingMargin:F1}m.");
            }

            if (legacyUnsafe == 0)
                return "no raid config puts the LEGACY -(radius+8) seat inside its own base's reach - " +
                       "the device capture (hero at -39.00 on raider_camp_small, dead at 45s) says otherwise, " +
                       "so this suite is no longer measuring what it claims to.";

            if (fails.Count > 0) return string.Join(" ;; ", fails);

            notes.Add($"{configs.Count} raid base(s) stage clear of every threat " +
                      $"({legacyUnsafe} of them were under fire at the legacy seat) [{string.Join(" / ", lines)}]");
            return null;
        }

        // =====================================================================
        //  CASE 3 - the duplicated awareness radius is PINNED to its source.
        // =====================================================================

        private static string Case3_PerceptionRadiusPinned(List<string> notes)
        {
            string gen = ReadText(GeneratorSrc);
            string sensor = ReadText(SensorSrc);
            // hollow-pass-ok (both guards below): this is the FAIL arm already - the non-empty
            // return IS the failure (Safe() adds it to `failures`, Run() then returns false
            // naming the missing path). HollowPassScanner blanks string-literal contents in its
            // skeleton, so `return $"...";` reads to it as a bare `return;`.
            if (gen == null)
            {
                // hollow-pass-ok - see the note above this guard pair.
                return $"cannot read {GeneratorSrc}";
            }
            if (sensor == null)
            {
                // hollow-pass-ok - see the note above this guard pair.
                return $"cannot read {SensorSrc}";
            }

            float mirrored = ConstFloat(gen, "DefenderPerceptionRadius");
            if (float.IsNaN(mirrored))
                return "RaidBaseGenerator.DefenderPerceptionRadius is gone - the staging distance no " +
                       "longer accounts for defender awareness at all.";

            // AwarenessSensor._perceptionRadius is a serialized PRIVATE field; its authored
            // default is the number the generator mirrors. Read it out of the source rather
            // than reflecting, so this stays a no-PlayMode suite.
            var m = Regex.Match(sensor, @"_perceptionRadius\s*=\s*([0-9]+(?:\.[0-9]+)?)f");
            if (!m.Success)
                return "cannot find AwarenessSensor._perceptionRadius' default in source - the field was " +
                       "renamed; re-point RaidBaseGenerator.DefenderPerceptionRadius at the new one.";

            float actual = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (Mathf.Abs(actual - mirrored) > 0.01f)
                return $"RaidBaseGenerator.DefenderPerceptionRadius is {mirrored:F2} but " +
                       $"AwarenessSensor._perceptionRadius defaults to {actual:F2}. The staging distance is " +
                       "computed against the wrong awareness radius - bump the mirror in the SAME change.";

            notes.Add($"awareness radius pinned at {actual:F0}m");
            return null;
        }

        // =====================================================================
        //  CASE 4 - the carried hero is seated at the staging marker.
        // =====================================================================

        private static string Case4_HeroSeatPrefersStaging(List<string> notes)
        {
            string src = ReadText(EnsurerSrc);
            if (src == null)
            {
                // hollow-pass-ok: this is the FAIL arm already - the non-empty return IS the
                // failure (Safe() adds it to `failures`, Run() then returns false naming the
                // missing path). HollowPassScanner blanks string-literal contents in its
                // skeleton, so `return $"...";` reads to it as a bare `return;`.
                return $"cannot read {EnsurerSrc}";
            }

            int idxStaging = src.IndexOf($"\"{StagingMarkerName}\"", StringComparison.Ordinal);
            if (idxStaging < 0)
                return $"{EnsurerSrc} never mentions \"{StagingMarkerName}\" - a raid still seats the " +
                       "carried hero at the old in-range entry marker (device capture: -39.00, " +
                       "seat=baked marker).";

            int idxLegacy = src.IndexOf("\"HeroStartPoint_PlayerSpawn\"", StringComparison.Ordinal);
            if (idxLegacy >= 0 && idxStaging > idxLegacy)
                return "HeroControlEnsurer looks up HeroStartPoint_PlayerSpawn BEFORE the staging marker, " +
                       "so the staging point can never win. The staging marker must be preferred when present.";

            notes.Add("hero seat prefers the staging marker");
            return null;
        }

        // =====================================================================
        //  CASE 5 - the clock cannot advance before first engagement.
        // =====================================================================

        private static string Case5_ClockGatedOnFirstEngagement(List<string> notes)
        {
            string src = ReadText(ScoringSrc);
            if (src == null)
            {
                // hollow-pass-ok: this is the FAIL arm already - the non-empty return IS the
                // failure (Safe() adds it to `failures`, Run() then returns false naming the
                // missing path). HollowPassScanner blanks string-literal contents in its
                // skeleton, so `return $"...";` reads to it as a bare `return;`.
                return $"cannot read {ScoringSrc}";
            }

            // Exactly ONE writer of the clock. Two would be two authorities, which is the
            // failure mode this ticket exists to close.
            int writes = Regex.Matches(src, @"_elapsed\s*\+=").Count;
            if (writes != 1)
                return $"_elapsed is advanced in {writes} place(s), not 1 - there is no single clock " +
                       "authority, so the engagement gate can be bypassed.";

            if (!src.Contains("NotifyEngagement"))
                return "RaidScoring has no NotifyEngagement - nothing can start the clock on first engagement.";

            // _engaged is set in exactly one place, and that place is NotifyEngagement.
            int sets = Regex.Matches(src, @"_engaged\s*=\s*true").Count;
            if (sets != 1)
                return $"_engaged is set in {sets} place(s), not 1 - NotifyEngagement is not the one authority.";

            int notifyAt = src.IndexOf("public void NotifyEngagement", StringComparison.Ordinal);
            int setAt = src.IndexOf("_engaged = true", StringComparison.Ordinal);
            if (notifyAt < 0 || setAt < notifyAt)
                return "the _engaged latch is set outside NotifyEngagement - route it through the one method.";

            // The gate itself: the clock line must be unreachable while staging.
            if (!Regex.IsMatch(src, @"if\s*\(\s*!_engaged\s*\)"))
                return "Update() has no `if (!_engaged)` gate - _elapsed still advances from scene entry, " +
                       "which is the exact defect the 12:59:47 capture recorded (elapsed=45s with the hero dead).";

            int gateAt = src.IndexOf("if (!_engaged)", StringComparison.Ordinal);
            var clock = Regex.Match(src, @"_elapsed\s*\+=");
            if (gateAt < 0 || clock.Index < gateAt)
                return "the `if (!_engaged)` gate sits AFTER the `_elapsed +=` line - the clock runs one " +
                       "path around it. The gate must precede and return.";

            // The permanent instrumentation this ticket is accepted on (CLAUDE.md sec.12 -
            // never strip it).
            if (!src.Contains("clock started reason="))
                return "the permanent FlowTrace.Step(\"Raid\", \"clock started reason=...\") line is gone - " +
                       "WO-1520 sec.4 accepts this ticket on that captured line. Instrumentation is permanent.";

            // No grace timer smuggled in. The owner refused one explicitly.
            if (Regex.IsMatch(src, @"(_grace|graceSeconds|GracePeriod|StagingCountdown)"))
                return "a grace/countdown timer appeared in RaidScoring. The owner refused one (WO-1520 " +
                       "sec.3): a timer still spawns the player in the fire. The staging PLACE is the fix.";

            notes.Add("clock gated on first engagement, one authority");
            return null;
        }

        // =====================================================================
        //  Data readers - all guarded, all returning null instead of throwing.
        // =====================================================================

        private sealed class RaidConfig
        {
            public string Id = "";
            public float BaseRadius;
            public string PlacementStyle = "";
            public readonly List<string> TowerTypes = new List<string>();
        }

        private static List<RaidConfig> ReadRaidConfigs()
        {
            string json = ReadText(ConfigsRes);
            if (json == null) return null;

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception) { return null; }

            var arr = root["configs"] as JArray;
            if (arr == null) return null;

            var list = new List<RaidConfig>();
            foreach (var t in arr)
            {
                var o = t as JObject;
                if (o == null) continue;

                float baseRadius = (float?)o["baseRadius"] ?? 0f;
                if (baseRadius <= 1f) continue;

                // Raid bases only: a raid config authors turret counts + a difficulty. The
                // player_outpost row shares baseRadius but is not a generated raid arena.
                string difficulty = (string)o["difficulty"] ?? "";
                if (string.IsNullOrEmpty(difficulty)) continue;

                var c = new RaidConfig
                {
                    Id = (string)o["id"] ?? "(unnamed)",
                    BaseRadius = baseRadius,
                    PlacementStyle = (string)o["towerPlacementStyle"] ?? "",
                };

                if (o["towers"] is JArray towers)
                {
                    foreach (var tw in towers)
                    {
                        string type = (string)tw["type"];
                        if (!string.IsNullOrEmpty(type)) c.TowerTypes.Add(type);
                    }
                }
                // Mirrors ResolveTowerTypes' fallbacks when towers[] is empty.
                if (c.TowerTypes.Count == 0)
                {
                    c.TowerTypes.Add("tower_ground_archer");
                    c.TowerTypes.Add("tower_arcane_spire");
                }

                list.Add(c);
            }
            return list;
        }

        private static Dictionary<string, float> ReadTowerRanges()
        {
            string json = ReadText(CatalogRes);
            if (json == null) return null;

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception) { return null; }

            var entries = root["entries"] as JArray;
            if (entries == null) return null;

            var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                string id = (string)e["id"];
                if (string.IsNullOrEmpty(id)) continue;
                float range = (float?)e["repo"]?["range"] ?? 0f;
                map[id] = range;
            }
            return map;
        }

        /// <summary>Re-read a `private/public const float NAME = 12.5f;` out of a source file.</summary>
        private static float ConstFloat(string src, string name)
        {
            var m = Regex.Match(src, @"const\s+float\s+" + Regex.Escape(name) +
                                     @"\s*=\s*(-?[0-9]+(?:\.[0-9]+)?)f");
            if (!m.Success) return float.NaN;
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null; }
            catch (Exception) { return null; }
        }
    }
}
