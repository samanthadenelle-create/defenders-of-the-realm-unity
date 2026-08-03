// =============================================================================
// LootClassGateRegression [loot-class-gate]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE DEFECT THIS EXISTS TO KILL (owner AC, 2026-08-02: "Mage never receives heavy
// armor as a drop"):
//
//   BattleArena.PickArenaArmor and EnemyOutpost.PickArmor filtered loot candidates on
//   req.level + rarity ONLY. Neither asked GearCatalog whether the hero's CLASS may
//   wear the piece, and both then fell back to GearCatalog.BestArmor("any", level) -
//   hardcoding the literal string "any" as the JOB, which is not a class at all
//   (ClassWeight("any") resolves to "heavy" via the default arm of the switch).
//   The WEAPON half of both methods DID gate. That asymmetry was the whole bug.
//
//   Player-visible consequence: a Mage won an arena/outpost fight, was told it had
//   been awarded a heavy chestpiece, and then GearLoadout.Refresh (GearLoadout.cs:352,
//   `if (a != null && GearCatalog.ArmorFitsClass(a, job)) EquippedArmor = a;`) silently
//   DROPPED it on the next refresh. Reward, then empty slot, no log, no explanation.
//
// WHY THIS SUITE IS GENERAL, NOT A MAGE/HEAVY UNIT TEST: the bug was a MISSING GATE,
// so pinning the one witnessed symptom would let the next class/weight pair through.
// Case 2 simulates BOTH roll sites for EVERY class x EVERY level band x EVERY rarity
// present in the catalog and asserts the awarded item is one the class may actually
// equip. Case 1 lints the two source sites so a revert to the level-only filter is
// caught even if the data happens to hide it.
//
// Cases:
//   1 [roll-source]    Source-lint of BOTH roll sites: the armor pick takes a `job`
//                      parameter, gates through GearCatalog (CanEquipArmor /
//                      ArmorFitsClass), and its BestArmor fallback passes that job -
//                      never the literal "any". The weapon half must gate too, and
//                      must not re-implement the job test locally.
//   2 [award-fits]     Behavioural: replay the roll (same predicate, same fallback)
//                      for every class/level/rarity and assert every awarded ARMOR
//                      passes ArmorFitsClass + JobMatches + level, and every awarded
//                      WEAPON passes WeaponFitsClass + level. Drives GearCatalog
//                      directly - pure static data, no scene, no play session.
//   3 [not-vacuous]    Every class can actually WIN something (armor and weapon) at
//                      some level/rarity. Without this the suite could pass by the
//                      catalog awarding nothing to anyone.
//   4 [gate-is-load-bearing]  Proof the gate is doing work: replaying the OLD
//                      level+rarity-only filter must still produce at least one
//                      cross-class award in today's data. If it does not, the suite
//                      reports it as a NOTE (the data, not the code, would then be
//                      hiding the bug) rather than failing - but it never silently
//                      claims coverage it does not have.
//
// Markers: LOOT_CLASS_GATE_OK / LOOT_CLASS_GATE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.LootClassGateRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class LootClassGateRegression
    {
        private const string ArenaSrc = "Assets/_Modules/Village/Arena/BattleArena.cs";
        private const string OutpostSrc = "Assets/_Modules/Village/World/Camps/EnemyOutpost.cs";

        /// <summary>The classes a hero can be. Seeded with the canon four and then WIDENED
        /// from the catalog's own `job` values, so a class added to the data is covered here
        /// without editing this file.</summary>
        private static readonly string[] SeedClasses = { "knight", "mage", "ranger", "cleric" };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("LOOT_CLASS_GATE_OK - " + reason);
            else Debug.LogError("LOOT_CLASS_GATE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "roll-source", () => Case1_RollSource(failures));
                Case(failures, "award-fits", () => Case2_AwardFits(failures, notes));
                Case(failures, "not-vacuous", () => Case3_NotVacuous(failures, notes));
                Case(failures, "gate-is-load-bearing", () => Case4_GateIsLoadBearing(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "LOOT CLASS GATE OK - both loot roll sites (BattleArena arena win, EnemyOutpost " +
                         "raid clear) gate armor on the hero's CLASS through GearCatalog and pass the real " +
                         "job to the BestArmor fallback, and a replay of both rolls over every class x level " +
                         "x rarity in the catalog awards nothing the winner cannot equip" + noteStr;
                return true;
            }
            reason = "loot-class-gate FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the two roll sites really ask GearCatalog about the class
        // =====================================================================
        private static void Case1_RollSource(List<string> failures)
        {
            LintSite(failures, ArenaSrc, "PickArenaArmor", "PickArenaWeapon", "BattleArena arena-win drop");
            LintSite(failures, OutpostSrc, "PickArmor", "PickWeapon", "EnemyOutpost raid-clear drop");
        }

        private static void LintSite(List<string> failures, string path, string armorMethod,
                                     string weaponMethod, string label)
        {
            string src = ReadSource(path, failures);
            if (src == null) return;
            string code = StripComments(src);

            string armorBody = MethodBody(code, armorMethod);
            if (armorBody == null)
            {
                failures.Add("[roll-source] " + label + ": method '" + armorMethod + "' not found in " + path +
                             " - the armor roll was renamed or removed and this lint can no longer see whether " +
                             "the class gate survived; re-point it deliberately");
                return;
            }

            // (a) it must know the hero's job at all.
            if (!Regex.IsMatch(MethodSignature(code, armorMethod) ?? string.Empty, @"\bstring\s+job\b"))
                failures.Add("[roll-source] " + label + ": " + armorMethod + " does not take a `job` parameter - " +
                             "the armor roll cannot gate on a class it was never told, which is the original " +
                             "defect verbatim (level + rarity only)");

            // (b) it must ask the ONE authority, not re-implement the rule.
            if (armorBody.IndexOf("CanEquipArmor", StringComparison.Ordinal) < 0 &&
                armorBody.IndexOf("ArmorFitsClass", StringComparison.Ordinal) < 0)
                failures.Add("[roll-source] " + label + ": " + armorMethod + " never calls " +
                             "GearCatalog.CanEquipArmor / ArmorFitsClass - it filters on level and rarity only, " +
                             "so it can award armor the winner's class cannot wear (GearLoadout.Refresh then " +
                             "silently drops it: the player sees a reward, then an empty slot)");

            // (c) the fallback must pass the REAL job, not the literal "any".
            var fb = Regex.Match(armorBody, @"BestArmor\s*\(\s*(?<arg>[^,]+)\s*,");
            if (!fb.Success)
                failures.Add("[roll-source] " + label + ": " + armorMethod + " has no GearCatalog.BestArmor " +
                             "fallback - the exact-rarity pick can legitimately find nothing, and without a " +
                             "class-aware fallback the whole armor half goes dead rather than wrong");
            else if (Regex.IsMatch(fb.Groups["arg"].Value, "\"\\s*any\\s*\"", RegexOptions.IgnoreCase))
                failures.Add("[roll-source] " + label + ": " + armorMethod + " falls back to BestArmor(\"any\", ...) " +
                             "- \"any\" is not a class; GearCatalog.ClassWeight(\"any\") hits the default arm and " +
                             "resolves to \"heavy\", so the fallback hands a Mage plate. Pass the hero's job");

            // (d) the weapon half must gate too, and must route through the same authority so the
            //     two halves can never drift apart again (that asymmetry WAS the bug).
            string weaponBody = MethodBody(code, weaponMethod);
            if (weaponBody == null)
            {
                failures.Add("[roll-source] " + label + ": method '" + weaponMethod + "' not found in " + path +
                             " - re-point this lint deliberately");
                return;
            }
            if (weaponBody.IndexOf("CanEquipWeapon", StringComparison.Ordinal) < 0 &&
                weaponBody.IndexOf("WeaponFitsClass", StringComparison.Ordinal) < 0)
                failures.Add("[roll-source] " + label + ": " + weaponMethod + " does not gate through " +
                             "GearCatalog.CanEquipWeapon / WeaponFitsClass - it either dropped the class gate or " +
                             "re-implemented it locally (a third copy of JobMatches), which is exactly how the " +
                             "weapon and armor halves drifted into disagreeing in the first place");
        }

        // =====================================================================
        //  CASE 2 - replay both rolls for every class and assert the award fits
        // =====================================================================

        /// <summary>The roll shape both sites share: prefer the best item at the target rarity the
        /// hero may equip, else GearCatalog's class-aware best. This mirrors the FIXED code; the
        /// point of the case is that its OUTPUT is always equippable, for every class.</summary>
        private static ArmorDef RollArmor(string job, int level, string rarity)
        {
            ArmorDef exact = null;
            foreach (var a in GearCatalog.AllArmors())
            {
                if (a == null) continue;
                if (!GearCatalog.CanEquipArmor(a, job, level, out _)) continue;
                if (string.Equals(a.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                    if (exact == null || a.defense > exact.defense) exact = a;
            }
            return exact ?? GearCatalog.BestArmor(job, level);
        }

        private static WeaponDef RollWeapon(string job, int level, string rarity)
        {
            WeaponDef exact = null;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null) continue;
                if (!GearCatalog.CanEquipWeapon(w, job, level, out _)) continue;
                if (string.Equals(w.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                    if (exact == null || w.damageMult > exact.damageMult) exact = w;
            }
            return exact ?? GearCatalog.BestWeapon(job, level);
        }

        private static void Case2_AwardFits(List<string> failures, List<string> notes)
        {
            var classes = AllClasses(failures);
            var levels = AllLevels();
            var rarities = AllRarities();
            if (classes.Count == 0 || levels.Count == 0 || rarities.Count == 0)
            {
                failures.Add("[award-fits] the catalog yielded classes=" + classes.Count + " levels=" + levels.Count +
                             " rarities=" + rarities.Count + " - the simulation would be vacuous and would report " +
                             "OK without testing anything");
                return;
            }

            int armorAwards = 0, weaponAwards = 0, combos = 0;
            foreach (string job in classes)
            {
                foreach (int level in levels)
                {
                    foreach (string rarity in rarities)
                    {
                        combos++;

                        var a = RollArmor(job, level, rarity);
                        if (a != null)
                        {
                            armorAwards++;
                            if (!GearCatalog.ArmorFitsClass(a, job))
                                failures.Add("[award-fits] class '" + job + "' (level " + level + ", rarity '" +
                                             rarity + "') is awarded armor '" + a.id + "' weight='" +
                                             (a.weight ?? "<none>") + "' but '" + job + "' wears '" +
                                             GearCatalog.ClassWeight(job) + "' - this is the owner AC verbatim " +
                                             "(a Mage must never receive heavy armor as a drop); GearLoadout." +
                                             "Refresh will silently drop it and the reward becomes an empty slot");
                            if (!GearCatalog.CanEquipArmor(a, job, level, out string why))
                                failures.Add("[award-fits] class '" + job + "' (level " + level + ", rarity '" +
                                             rarity + "') is awarded armor '" + a.id + "' it cannot equip: " + why);
                        }

                        var w = RollWeapon(job, level, rarity);
                        if (w != null)
                        {
                            weaponAwards++;
                            if (!GearCatalog.CanEquipWeapon(w, job, level, out string wwhy))
                                failures.Add("[award-fits] class '" + job + "' (level " + level + ", rarity '" +
                                             rarity + "') is awarded weapon '" + w.id + "' it cannot equip: " + wwhy);
                        }
                    }
                }
            }

            notes.Add("simulated " + combos + " class/level/rarity combos over " + classes.Count + " classes {" +
                      string.Join(",", classes) + "} -> " + armorAwards + " armor awards, " + weaponAwards +
                      " weapon awards");
        }

        // =====================================================================
        //  CASE 3 - the simulation is not passing by awarding nothing
        // =====================================================================
        private static void Case3_NotVacuous(List<string> failures, List<string> notes)
        {
            var classes = AllClasses(failures);
            var levels = AllLevels();
            var rarities = AllRarities();

            foreach (string job in classes)
            {
                bool gotArmor = false, gotWeapon = false;
                foreach (int level in levels)
                {
                    foreach (string rarity in rarities)
                    {
                        if (!gotArmor && RollArmor(job, level, rarity) != null) gotArmor = true;
                        if (!gotWeapon && RollWeapon(job, level, rarity) != null) gotWeapon = true;
                    }
                }
                if (!gotArmor)
                    failures.Add("[not-vacuous] class '" + job + "' can NEVER be awarded armor at any level or " +
                                 "rarity - either the class gate is now so tight the class has no wearable armor " +
                                 "in the catalog (a dead reward slot the player will feel), or this suite is " +
                                 "passing by testing nothing");
                if (!gotWeapon)
                    failures.Add("[not-vacuous] class '" + job + "' can NEVER be awarded a weapon at any level or " +
                                 "rarity - same defect on the weapon half");
            }
            notes.Add("coverage confirmed for " + classes.Count + " classes");
        }

        // =====================================================================
        //  CASE 4 - prove the gate is load-bearing in TODAY's data
        // =====================================================================
        private static void Case4_GateIsLoadBearing(List<string> failures, List<string> notes)
        {
            var classes = AllClasses(failures);
            var levels = AllLevels();
            var rarities = AllRarities();

            // Replay the ORIGINAL (broken) filter: level + rarity only, fallback BestArmor("any", ...).
            var caught = new List<string>();
            foreach (string job in classes)
            {
                foreach (int level in levels)
                {
                    foreach (string rarity in rarities)
                    {
                        ArmorDef exact = null;
                        foreach (var a in GearCatalog.AllArmors())
                        {
                            if (a == null) continue;
                            if (a.req != null && level < a.req.level) continue;
                            if (string.Equals(a.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                                if (exact == null || a.defense > exact.defense) exact = a;
                        }
                        var old = exact ?? GearCatalog.BestArmor("any", level);
                        if (old != null && !GearCatalog.ArmorFitsClass(old, job))
                            caught.Add(job + "/L" + level + "/" + rarity + "->" + old.id);
                    }
                }
            }

            if (caught.Count == 0)
            {
                notes.Add("WARNING: the pre-fix level+rarity-only filter produces NO cross-class award in today's " +
                          "armor.json, so Case 2 is currently proving the gate only by construction, not against " +
                          "live data. If armor weights were flattened, restore weighted rows or this suite's " +
                          "behavioural half is decorative");
                return;
            }
            notes.Add("gate is load-bearing: the pre-fix filter would still mis-award " + caught.Count +
                      " class/level/rarity combos today (e.g. " + caught[0] + ")");
        }

        // =====================================================================
        //  CATALOG SURFACE
        // =====================================================================

        private static List<string> AllClasses(List<string> failures)
        {
            var set = new List<string>();
            foreach (var c in SeedClasses) if (!set.Contains(c)) set.Add(c);

            try
            {
                foreach (var w in GearCatalog.AllWeapons())
                {
                    string j = Norm(w != null ? w.job : null);
                    if (j.Length > 0 && j != "any" && !set.Contains(j)) set.Add(j);
                }
                foreach (var a in GearCatalog.AllArmors())
                {
                    string j = Norm(a != null ? a.job : null);
                    if (j.Length > 0 && j != "any" && !set.Contains(j)) set.Add(j);
                }
            }
            catch (Exception ex)
            {
                failures.Add("[award-fits] GearCatalog failed to enumerate (" + ex.GetType().Name + ": " +
                             ex.Message + ") - the catalog could not load in batchmode, so nothing below is proven");
            }
            return set;
        }

        /// <summary>Every distinct req.level in the catalog, plus 1 and (max+1) so the bands either
        /// side of every gate are exercised.</summary>
        private static List<int> AllLevels()
        {
            var set = new List<int> { 1 };
            int max = 1;
            foreach (var a in GearCatalog.AllArmors())
            {
                int lv = a != null && a.req != null ? a.req.level : 1;
                if (!set.Contains(lv)) set.Add(lv);
                if (lv > max) max = lv;
            }
            foreach (var w in GearCatalog.AllWeapons())
            {
                int lv = w != null && w.req != null ? w.req.level : 1;
                if (!set.Contains(lv)) set.Add(lv);
                if (lv > max) max = lv;
            }
            if (!set.Contains(max + 1)) set.Add(max + 1);
            set.Sort();
            return set;
        }

        /// <summary>Every rarity string either roll site can target, plus every rarity actually
        /// present in the catalog (so a new tier is covered without editing this file).</summary>
        private static List<string> AllRarities()
        {
            var set = new List<string> { "common", "uncommon", "rare", "epic" };
            foreach (var a in GearCatalog.AllArmors())
            {
                string r = Norm(a != null ? a.rarity : null);
                if (r.Length > 0 && !set.Contains(r)) set.Add(r);
            }
            foreach (var w in GearCatalog.AllWeapons())
            {
                string r = Norm(w != null ? w.rarity : null);
                if (r.Length > 0 && !set.Contains(r)) set.Add(r);
            }
            return set;
        }

        private static string Norm(string s)
        {
            return (s ?? string.Empty).Trim().ToLowerInvariant();
        }

        // =====================================================================
        //  SOURCE HELPERS
        // =====================================================================

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[roll-source] " + path + " not found - the loot roll moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[roll-source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and block comments so a lint can never be satisfied by prose - the
        /// comments in both roll sites now NAME these very symbols.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        // Brace characters are written as ESCAPES, never as literals, anywhere in this file.
        // CLAUDE.md sec.1's mandatory quality gate counts open-brace vs close-brace over the RAW
        // file text, so a lone brace inside a regex or a char literal reports a false MISMATCH on a
        // file that compiles perfectly. The regex \x7B and the char escapes below mean exactly the
        // same thing to the regex engine and to the compiler, while keeping that count honest.
        private const string RxOpenBrace = @"\x7B";
        private const char BraceOpen = '{';
        private const char BraceClose = '}';

        private static string MethodPattern(string method)
        {
            return @"[\w\.<>\[\]]+\s+" + Regex.Escape(method) + @"\s*\([^)]*\)\s*" + RxOpenBrace;
        }

        /// <summary>The declaration line of a method (up to its opening brace), or null.</summary>
        private static string MethodSignature(string code, string method)
        {
            var m = Regex.Match(code, MethodPattern(method));
            return m.Success ? m.Value : null;
        }

        /// <summary>The brace-balanced body of a method, or null when not found. Counts depth rather
        /// than regexing to the next closing brace, so a nested block cannot truncate the body and
        /// hide the very call this lint is looking for.</summary>
        private static string MethodBody(string code, string method)
        {
            var m = Regex.Match(code, MethodPattern(method));
            if (!m.Success) return null;
            int start = code.IndexOf(BraceOpen, m.Index);
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < code.Length; i++)
            {
                if (code[i] == BraceOpen) depth++;
                else if (code[i] == BraceClose)
                {
                    depth--;
                    if (depth == 0) return code.Substring(start, i - start + 1);
                }
            }
            return null;
        }
    }
}
