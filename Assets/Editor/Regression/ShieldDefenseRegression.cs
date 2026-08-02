// =============================================================================
// ShieldDefenseRegression [shield-defense]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the owner request "make shields add different defense levels and gate them
// by levels" (2026-08-02) against the TWO defects that made shields cosmetic:
//
//   DEFECT 1 (data)  - every weapons.json row with category "shield" carried NO
//                      `defense` field at all. A shield was pure decoration.
//   DEFECT 2 (code)  - even with a value, nothing read it: GearLoadout.ApplyStats
//                      summed ONLY armor + ring + amulet into ArmorDefense, and
//                      HeroHealth.TakeDamage consumes exactly that one scalar. The
//                      equipped OFF-HAND was never in the sum, so authoring data
//                      alone would still change nothing the player can feel.
//
// Cases:
//   1 [shield-data]     Every shield row in BOTH weapons.json copies has a
//                       defense > 0. Row counts differ ON PURPOSE (see below), so
//                       this is asserted per-copy, not by comparing the files.
//   2 [shield-ladder]   Defense is gated by req.level: the band at a LOWER
//                       req.level never out-defends a band at a HIGHER one, the
//                       rarity matches the band, and there is real spread (more
//                       than one band, i.e. the gating is not cosmetic).
//   3 [starter-shield]  knight_shield_starter (the WO-860 seeded starter) is still
//                       level 1, resolves through the RESOURCES copy that the
//                       shipped player actually loads, is a real off-hand item that
//                       fits the knight, and is the WEAKEST shield in the game -
//                       non-zero, but never the reason to skip an upgrade.
//   4 [loadout-sums]    GearLoadout.ApplyStats really FOLDS the equipped off-hand
//                       into the published ArmorDefense, and the 0.70 balance
//                       ceiling is still enforced on the summed total. Source-lint:
//                       there is no scene-free way to drive ApplyStats, but the
//                       thing that regresses here is a deleted term, which a lint
//                       catches exactly.
//   5 [defense-ceiling] No single shield is worth more than a legendary chestpiece
//                       (a fat-fingered 1.3 instead of 0.13 would hand the player
//                       flat immunity), and the best-in-slot stack is reported so
//                       the 0.70 clamp headroom is never a surprise.
//
// WHY THE TWO COPIES ARE NOT COMPARED: weapons.json is the one canonical file that
// is NOT a byte-identical dual pair. StreamingAssets is the full LIBRARY the tools
// edit; Resources is the CURATED runtime export produced by GearCurationExporter
// from Assets/Editor/GearCurationPicks.json. An earlier oracle that asserted
// byte-identity here was WRONG and was removed (see StarterLoadoutRegression Case 1).
// What still matters - "authored in the library but missing on device" - is covered
// by asserting the ladder independently in EACH copy plus resolving the starter
// through the Resources copy.
//
// Markers: SHIELD_DEFENSE_OK / SHIELD_DEFENSE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.ShieldDefenseRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class ShieldDefenseRegression
    {
        private const string WeaponsRes = "Assets/Resources/Data/Canonical/weapons.json";
        private const string WeaponsSA = "Assets/StreamingAssets/Data/Canonical/weapons.json";
        private const string ArmorRes = "Assets/Resources/Data/Canonical/armor.json";
        private const string AccessoriesRes = "Assets/Resources/Data/Canonical/accessories.json";
        private const string GearLevelsRes = "Assets/Resources/Data/Canonical/gear-levels.json";

        private const string LoadoutSrc = "Assets/_Modules/Village/Hero/GearLoadout.cs";

        /// <summary>The WO-860 seeded starter off-hand. Must stay level 1 and stay the floor.</summary>
        private const string StarterShieldId = "knight_shield_starter";

        /// <summary>The balance ceiling ApplyStats clamps the SUMMED defense to.</summary>
        private const float DefenseClamp = 0.70f;

        /// <summary>A shield alone may never out-defend the best chestpiece (0.35 today).</summary>
        private const float MaxSaneShieldDefense = 0.20f;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SHIELD_DEFENSE_OK - " + reason);
            else Debug.LogError("SHIELD_DEFENSE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "shield-data", () => Case1_ShieldData(failures, notes));
                Case(failures, "shield-ladder", () => Case2_Ladder(failures, notes));
                Case(failures, "starter-shield", () => Case3_Starter(failures));
                Case(failures, "loadout-sums", () => Case4_LoadoutSumsOffHand(failures));
                Case(failures, "defense-ceiling", () => Case5_Ceiling(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "SHIELD DEFENSE OK - every shield carries a defense value in both weapons.json " +
                         "copies, the value is gated by req.level with matching rarity, the seeded starter " +
                         "is the level-1 floor and still resolves on device, GearLoadout folds the equipped " +
                         "off-hand into ArmorDefense under the 0.70 clamp, and no single shield exceeds a " +
                         "legendary chestpiece" + noteStr;
                return true;
            }
            reason = "shield-defense FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  A shield row, read straight from JSON (WeaponDef is not used here on
        //  purpose: the JSON is the authored truth, and reading it directly means
        //  this suite still reports the real defect if the C# field is ever dropped).
        // =====================================================================
        private sealed class ShieldRow
        {
            public string Id;
            public string Rarity;
            public int Level;
            public float Defense;
            public bool HasDefense;
        }

        private static List<ShieldRow> ReadShields(string path, List<string> failures)
        {
            var rows = new List<ShieldRow>();
            if (!File.Exists(path))
            {
                failures.Add("[shield-data] weapons.json missing: " + path);
                return rows;
            }

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("[shield-data] " + path + " failed to parse (" + ex.GetType().Name + ": " + ex.Message + ")");
                return rows;
            }

            var arr = root["weapons"] as JArray;
            if (arr == null)
            {
                failures.Add("[shield-data] " + path + " has no 'weapons' array");
                return rows;
            }

            foreach (var w in arr)
            {
                string cat = (string)w["category"];
                if (cat == null || !cat.Trim().Equals("shield", StringComparison.OrdinalIgnoreCase)) continue;

                var row = new ShieldRow();
                row.Id = (string)w["id"];
                row.Rarity = ((string)w["rarity"] ?? "common").Trim().ToLowerInvariant();
                var req = w["req"];
                row.Level = req != null && req["level"] != null ? (int)req["level"] : 1;
                row.HasDefense = w["defense"] != null;
                row.Defense = row.HasDefense ? (float)w["defense"] : 0f;
                rows.Add(row);
            }
            return rows;
        }

        // =====================================================================
        //  CASE 1 - every shield actually carries a defense value, in BOTH copies
        // =====================================================================
        private static void Case1_ShieldData(List<string> failures, List<string> notes)
        {
            foreach (var path in new[] { WeaponsRes, WeaponsSA })
            {
                var shields = ReadShields(path, failures);
                if (shields.Count == 0)
                {
                    failures.Add("[shield-data] " + path + " contains ZERO rows with category 'shield' - either the " +
                                 "category string drifted or the shields were dropped from this copy; the off-hand " +
                                 "slot would have nothing to equip");
                    continue;
                }

                foreach (var s in shields)
                {
                    if (!s.HasDefense)
                        failures.Add("[shield-data] '" + s.Id + "' (" + CopyLabel(path) +
                                     " copy) has NO 'defense' field - this is the original defect verbatim: the " +
                                     "shield is drawn on the arm and mitigates nothing");
                    else if (s.Defense <= 0f)
                        failures.Add("[shield-data] '" + s.Id + "' has defense " + Fmt(s.Defense) + " - a zero or " +
                                     "negative shield is a cosmetic shield, and a negative one would be silently " +
                                     "swallowed by the Mathf.Max(0f, ...) guard rather than reported");
                }

                notes.Add(CopyLabel(path) + "=" + shields.Count + " shields");
            }
        }

        // =====================================================================
        //  CASE 2 - the ladder is real: defense is gated by req.level
        // =====================================================================
        private static void Case2_Ladder(List<string> failures, List<string> notes)
        {
            // The rarity a given req.level band must carry, so the shop tint, the
            // GearLevelCatalog band and the authored power all tell the same story.
            var expectedRarity = new Dictionary<int, string>
            {
                { 1, "common" }, { 3, "uncommon" }, { 6, "rare" }, { 10, "epic" },
            };

            foreach (var path in new[] { WeaponsRes, WeaponsSA })
            {
                var shields = ReadShields(path, failures);
                if (shields.Count == 0) continue;

                // Collapse to bands keyed by req.level.
                var minByLevel = new Dictionary<int, float>();
                var maxByLevel = new Dictionary<int, float>();
                foreach (var s in shields)
                {
                    if (!minByLevel.ContainsKey(s.Level) || s.Defense < minByLevel[s.Level]) minByLevel[s.Level] = s.Defense;
                    if (!maxByLevel.ContainsKey(s.Level) || s.Defense > maxByLevel[s.Level]) maxByLevel[s.Level] = s.Defense;

                    string want;
                    if (expectedRarity.TryGetValue(s.Level, out want) &&
                        !string.Equals(s.Rarity, want, StringComparison.Ordinal))
                        failures.Add("[shield-ladder] '" + s.Id + "' requires level " + s.Level + " but is rarity '" +
                                     s.Rarity + "' (band says '" + want + "') - rarity drives the gear-levels.json " +
                                     "upgrade band and the store tint, so a mismatched row levels up on the wrong curve");
                }

                var levels = new List<int>(minByLevel.Keys);
                levels.Sort();
                if (levels.Count < 2)
                {
                    failures.Add("[shield-ladder] every shield in " + CopyLabel(path) +
                                 " requires level " + (levels.Count > 0 ? levels[0].ToString() : "?") + " - the owner " +
                                 "asked for shields GATED BY LEVEL, and a single band is exactly the cosmetic gating " +
                                 "that was already there");
                    continue;
                }

                for (int i = 1; i < levels.Count; i++)
                {
                    int lo = levels[i - 1], hi = levels[i];
                    if (maxByLevel[lo] >= minByLevel[hi])
                        failures.Add("[shield-ladder] the level-" + lo + " band tops out at " + Fmt(maxByLevel[lo]) +
                                     " but the level-" + hi + " band starts at " + Fmt(minByLevel[hi]) +
                                     " - a higher level gate must buy strictly more defense or levelling past the " +
                                     "gate is a downgrade the player can see");
                }

                if (maxByLevel[levels[levels.Count - 1]] <= minByLevel[levels[0]])
                    failures.Add("[shield-ladder] the top band (" + Fmt(maxByLevel[levels[levels.Count - 1]]) +
                                 ") is no better than the bottom band (" + Fmt(minByLevel[levels[0]]) +
                                 ") - the ladder is flat");

                notes.Add(CopyLabel(path) + " bands " + DescribeBands(levels, minByLevel, maxByLevel));
            }
        }

        private static string DescribeBands(List<int> levels, Dictionary<int, float> min, Dictionary<int, float> max)
        {
            var parts = new List<string>();
            foreach (int lv in levels)
            {
                parts.Add(Fmt(min[lv]) == Fmt(max[lv])
                    ? "L" + lv + "=" + Fmt(min[lv])
                    : "L" + lv + "=" + Fmt(min[lv]) + ".." + Fmt(max[lv]));
            }
            return string.Join("/", parts);
        }

        // =====================================================================
        //  CASE 3 - the seeded starter stays the level-1 floor and stays on device
        // =====================================================================
        private static void Case3_Starter(List<string> failures)
        {
            var shields = ReadShields(WeaponsRes, failures);
            if (shields.Count == 0) return;

            ShieldRow starter = null;
            float weakest = float.MaxValue;
            foreach (var s in shields)
            {
                if (string.Equals(s.Id, StarterShieldId, StringComparison.OrdinalIgnoreCase)) starter = s;
                if (s.Defense < weakest) weakest = s.Defense;
            }

            if (starter == null)
            {
                failures.Add("[starter-shield] '" + StarterShieldId + "' is NOT in the Resources (curated) copy of " +
                             "weapons.json - the shipped player loads Resources, so the WO-860 starter kit would seed " +
                             "an id that resolves in the editor and not on device (the hero spawns shieldless)");
                return;
            }

            if (starter.Level != 1)
                failures.Add("[starter-shield] '" + StarterShieldId + "' requires level " + starter.Level +
                             " - it is handed to a brand-new level-1 hero by StarterLoadout, so any gate above 1 " +
                             "makes the seeded kit unequippable on the very first frame of a new game");

            if (starter.Defense <= 0f)
                failures.Add("[starter-shield] '" + StarterShieldId + "' has defense " + Fmt(starter.Defense) +
                             " - the starter must be the WEAKEST shield, not an inert one; the first shield is where " +
                             "the player learns the stat exists at all");

            if (starter.Defense > weakest)
                failures.Add("[starter-shield] '" + StarterShieldId + "' (" + Fmt(starter.Defense) + ") is not the " +
                             "weakest shield (" + Fmt(weakest) + ") - the free seeded starter must never out-defend " +
                             "something the player worked for");

            // Resolve through the live catalog exactly as GearLoadout / HeroBodySwapper do.
            var w = GearCatalog.FindWeapon(StarterShieldId);
            if (w == null)
            {
                failures.Add("[starter-shield] GearCatalog.FindWeapon('" + StarterShieldId + "') returned null even " +
                             "though the row exists - loader/schema drift, and EquipOffHandById would no-op with a Warn");
                return;
            }
            if (!w.IsOffHandItem)
                failures.Add("[starter-shield] '" + StarterShieldId + "' does not read as an off-hand item " +
                             "(category='" + w.category + "') - EquipOffHandById rejects it outright and no shield " +
                             "defense can ever be summed");
            if (!GearCatalog.WeaponFitsClass(w, "knight"))
                failures.Add("[starter-shield] '" + StarterShieldId + "' does not fit class 'knight' (job='" + w.job +
                             "') - the class that is seeded with it cannot equip it");
        }

        // =====================================================================
        //  CASE 4 - GearLoadout really folds the OFF-HAND into ArmorDefense
        // =====================================================================
        private static void Case4_LoadoutSumsOffHand(List<string> failures)
        {
            string src = ReadSource(LoadoutSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            // The published scalar. HeroHealth.TakeDamage consumes exactly this, so the whole
            // feature lives or dies on this one statement.
            var m = Regex.Match(code, @"ArmorDefense\s*=\s*Mathf\.Clamp\((?<args>[^;]*)\);");
            if (!m.Success)
            {
                failures.Add("[loadout-sums] GearLoadout.ApplyStats no longer assigns ArmorDefense via Mathf.Clamp - " +
                             "this suite can no longer see whether the off-hand is summed or whether the balance " +
                             "ceiling still holds; re-point the lint at the new shape deliberately");
                return;
            }

            string args = m.Groups["args"].Value;

            if (args.IndexOf("OffHand", StringComparison.OrdinalIgnoreCase) < 0 &&
                args.IndexOf("shield", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[loadout-sums] the ArmorDefense assignment does not include an off-hand/shield term " +
                             "(it reads: Mathf.Clamp(" + Condense(args) + ")) - this is DEFECT 2 verbatim: every " +
                             "shield can carry a defense value and the hero still takes full damage, because " +
                             "HeroHealth reads only this scalar");

            if (args.IndexOf("0.70f", StringComparison.Ordinal) < 0 && args.IndexOf("0.7f", StringComparison.Ordinal) < 0)
                failures.Add("[loadout-sums] the 0.70 clamp is gone from the ArmorDefense assignment (it reads: " +
                             "Mathf.Clamp(" + Condense(args) + ")) - adding a fourth defense source with no ceiling " +
                             "is how a stacked hero becomes unkillable");

            // The off-hand term must be floor-guarded like the accessory term, or a negative
            // authored value would HEAL the hero through the mitigation formula.
            if (!Regex.IsMatch(code, @"EquippedOffHand[^;]*\.defense") &&
                !Regex.IsMatch(code, @"EffectiveDefense\s*\([^;]*EquippedOffHand") &&
                !Regex.IsMatch(code, @"offDefense\s*=|shieldDefense\s*="))
                failures.Add("[loadout-sums] nothing in GearLoadout reads a defense value off EquippedOffHand - the " +
                             "clamp may mention an off-hand variable, but no shield stat is ever sourced from the " +
                             "equipped item");

            if (!Regex.IsMatch(code, @"Mathf\.Max\s*\(\s*0f\s*,\s*(off|shield)", RegexOptions.IgnoreCase))
                failures.Add("[loadout-sums] the off-hand defense term is not wrapped in Mathf.Max(0f, ...) like the " +
                             "accessory term - a negative authored defense would subtract from mitigation, i.e. a " +
                             "cursed shield would silently make the hero take MORE damage with no log line");
        }

        // =====================================================================
        //  CASE 5 - nothing in the ladder can trivialise the 0.70 ceiling
        // =====================================================================
        private static void Case5_Ceiling(List<string> failures, List<string> notes)
        {
            var shields = ReadShields(WeaponsRes, failures);
            float bestShield = 0f;
            foreach (var s in shields)
            {
                if (s.Defense > bestShield) bestShield = s.Defense;
                if (s.Defense > MaxSaneShieldDefense)
                    failures.Add("[defense-ceiling] '" + s.Id + "' has defense " + Fmt(s.Defense) + ", above the " +
                                 Fmt(MaxSaneShieldDefense) + " sanity bar - a shield worth more than a legendary " +
                                 "chestpiece (0.35) means the off-hand slot is the only defensive choice that matters, " +
                                 "and a misplaced decimal point here reads as flat immunity");
            }

            // Best-in-slot stack, computed the way ApplyStats does: armor is level-scaled
            // through its rarity band, accessories are flat.
            float bestArmor = 0f;
            string bestArmorId = "<none>";
            string bestArmorRarity = "common";
            foreach (var row in ReadRows(ArmorRes, "armor", failures))
            {
                if (row["defense"] == null) continue;
                float d = (float)row["defense"];
                if (d > bestArmor)
                {
                    bestArmor = d;
                    bestArmorId = (string)row["id"];
                    bestArmorRarity = ((string)row["rarity"] ?? "common").Trim().ToLowerInvariant();
                }
            }

            float bestRing = 0f, bestAmulet = 0f;
            foreach (var row in ReadRows(AccessoriesRes, "accessories", failures))
            {
                if (row["defense"] == null) continue;
                float d = (float)row["defense"];
                string slot = ((string)row["slot"] ?? string.Empty).ToLowerInvariant();
                if (slot.Contains("ring")) { if (d > bestRing) bestRing = d; }
                else if (slot.Contains("amulet")) { if (d > bestAmulet) bestAmulet = d; }
            }

            float bandMax = MaxBandMult(bestArmorRarity, failures);
            float worst = (bestArmor * bandMax) + bestRing + bestAmulet + bestShield;

            notes.Add("BiS stack = armor '" + bestArmorId + "' " + Fmt(bestArmor) + "x" + Fmt(bandMax) + " + ring " +
                      Fmt(bestRing) + " + amulet " + Fmt(bestAmulet) + " + shield " + Fmt(bestShield) + " = " +
                      Fmt(worst) + " vs clamp " + Fmt(DefenseClamp) +
                      (worst > DefenseClamp ? " (CLAMP BINDS - top-end defense gear is partly wasted)" : " (headroom " +
                       Fmt(DefenseClamp - worst) + ")"));

            // The shield must not be the single term that eats the whole budget.
            if (bestShield >= DefenseClamp)
                failures.Add("[defense-ceiling] the best shield alone (" + Fmt(bestShield) + ") meets or exceeds the " +
                             Fmt(DefenseClamp) + " clamp - one item would grant the game's maximum mitigation and " +
                             "every other defensive item would become worthless");
        }

        private static IEnumerable<JObject> ReadRows(string path, string arrayKey, List<string> failures)
        {
            var result = new List<JObject>();
            if (!File.Exists(path))
            {
                failures.Add("[defense-ceiling] " + path + " not found - the ceiling cannot be computed");
                return result;
            }
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var arr = root[arrayKey] as JArray;
                if (arr == null)
                {
                    failures.Add("[defense-ceiling] " + path + " has no '" + arrayKey + "' array");
                    return result;
                }
                foreach (var r in arr) { var o = r as JObject; if (o != null) result.Add(o); }
            }
            catch (Exception ex)
            {
                failures.Add("[defense-ceiling] " + path + " failed to parse (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
            return result;
        }

        private static float MaxBandMult(string rarity, List<string> failures)
        {
            foreach (var band in ReadRows(GearLevelsRes, "bands", failures))
            {
                string r = ((string)band["rarity"] ?? string.Empty).Trim().ToLowerInvariant();
                if (r != rarity) continue;
                var mults = band["statMult"] as JArray;
                if (mults == null) continue;
                float max = 1f;
                foreach (var m in mults) { float v = (float)m; if (v > max) max = v; }
                return max;
            }
            return 1f;
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>Names WHICH dual copy a message is about. Both files sit in a folder called
        /// "Canonical", so the folder name alone would label them identically.</summary>
        private static string CopyLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "<unknown>";
            if (path.IndexOf("StreamingAssets", StringComparison.OrdinalIgnoreCase) >= 0) return "StreamingAssets/library";
            if (path.IndexOf("Resources", StringComparison.OrdinalIgnoreCase) >= 0) return "Resources/curated";
            return path;
        }

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] " + path + " not found - the file moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and /* */ comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        private static string Condense(string s)
        {
            string one = Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();
            return one.Length > 160 ? one.Substring(0, 157) + "..." : one;
        }

        private static string Fmt(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
