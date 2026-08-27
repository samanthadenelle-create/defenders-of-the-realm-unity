// =============================================================================
// ArmourCatalogJobRegression [armour-catalog-job] (WO-1241)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE DEFECT. `blink_armor_dragonic` was authored job:"any", weight:"heavy",
// req.level 1. `ClassWeight("mage") == "light"`, so `ArmorFitsClass` is false and
// `CanEquipArmorNow` CORRECTLY refuses it for a Mage. The equip seam was doing
// its job. But job:"any" means every job-based loot and shop filter still OFFERS
// it to that Mage - the player is shown, and can acquire, armour the game will
// always refuse to let them wear.
//
// Owner, 2026-08-26: "The equip seam is correctly protecting you. The catalog
// should stop offering illegal gear in the first place."
//
// ⚠ WEIGHT-VS-CLASS IS THE REAL ELIGIBILITY RULE. `job` and `weight` are two
// different gates asked by two different helpers (JobMatches / ArmorFitsClass),
// and a row can be LEGAL BY JOB and ILLEGAL BY WEIGHT. That is precisely how this
// one slipped through, and it is what Case 2 measures.
//
// THE RULE THIS SUITE ENFORCES, in two halves:
//   1 [no-open-job]    A wearable armour row may NOT use job:"any" unless it is
//                      in WHITELIST below - a DELIBERATE, NAMED exception with a
//                      reason, never a default. Scoped to the RESOURCES copy
//                      because that is the catalog the shipped player loads and
//                      therefore the only one that can OFFER anything to anyone.
//   2 [job-weight-agree] Every class named in a row's `job` must actually wear
//                      that row's `weight`. A row offered to a class the weight
//                      gate will refuse is the WO-1241 defect by definition,
//                      whatever its id. NARROWER than the weight class is fine
//                      (armor_knight_common is knight-only heavy): the rule is
//                      that job must be a SUBSET of the weight's classes, not
//                      equal to it.
//   3 [whitelist-honest] Every whitelisted id must still EXIST and must really be
//                      weight-free. A whitelist entry that quietly grows a
//                      `weight` is a re-opened hole with a permission slip.
//   4 [library-sweep]  The StreamingAssets LIBRARY superset is swept too, as
//                      NOTES not failures: it is not loaded at runtime (Resources
//                      wins), so a violation there cannot reach a player - but it
//                      is where the next curated export comes from, so it is
//                      exactly how this bug would be re-seeded.
//
// PROVED RED FIRST (WO-1138): run over HEAD's Resources armor.json the rule named
// SEVEN violating rows - armor_leather, armor_chain, armor_plate,
// blink_armor_centurion, blink_armor_beasthunter, blink_armor_dragonic,
// blink_armor_basic1 - and 28 in the StreamingAssets library.
//
// Markers: ARMOUR_CATALOG_JOB_OK / ARMOUR_CATALOG_JOB_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.ArmourCatalogJobRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class ArmourCatalogJobRegression
    {
        private const string ArmorRes = "Assets/Resources/Data/Canonical/armor.json";
        private const string ArmorSA  = "Assets/StreamingAssets/Data/Canonical/armor.json";

        /// <summary>
        /// THE `job: "any"` WHITELIST - the deliberate, named universal armour.
        ///
        /// An entry earns its place by being universal IN THE DATA, not by being convenient: the
        /// row must carry NO `weight`, so <see cref="GearCatalog.ArmorFitsClass"/> genuinely admits
        /// every class and the equip seam will never refuse what the shop offered. That is the only
        /// shape of `any` that is honest. Case 3 re-checks the condition on every run, so an entry
        /// cannot silently stop deserving it.
        /// </summary>
        private static readonly Dictionary<string, string> Whitelist =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "armor_cloth",
                  "Wanderer's Cloth - the level-1 universal floor. Carries NO weight at all, so " +
                  "every class can genuinely wear it; it is also the cleric's starter-armour " +
                  "stand-in (WO-1240) precisely because it is the one weight-free level-1 row." },
                { "aegis_plate",
                  "Aegis set piece - authored weight-free on purpose so the WO-295 full-set bonus " +
                  "is reachable by every class that can hold an Aegis weapon. Its NAME says plate " +
                  "and its DATA says universal; the owner has not ruled on that tension, so this " +
                  "whitelists the authored intent rather than silently re-authoring it." },
            };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ARMOUR_CATALOG_JOB_OK - " + reason);
            else Debug.LogError("ARMOUR_CATALOG_JOB_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                var runtime = ReadRows(ArmorRes, failures);
                if (runtime != null)
                {
                    Case(failures, "no-open-job", () => Case1_NoOpenJob(runtime, failures));
                    Case(failures, "job-weight-agree", () => Case2_JobWeightAgree(runtime, failures));
                    Case(failures, "whitelist-honest", () => Case3_WhitelistHonest(runtime, failures));
                }
                else
                {
                    // FIXTURE-ABSENT -> FAIL, naming the path. ReadRows has already recorded WHY
                    // (missing / unparseable / no 'armor' array), but relying on that alone would
                    // make this branch's greenness depend on a side effect three frames away. The
                    // suite's whole guarantee lives in Cases 1-3; skipping them is never a pass.
                    failures.Add($"[armour-json] the RUNTIME armour catalog '{ArmorRes}' could not be read, so " +
                                 "no-open-job, job-weight-agree and whitelist-honest ALL stood down. That is not " +
                                 "a pass: this file is TRACKED SOURCE and it is the only catalog the shipped " +
                                 "player loads, so with it unreadable nothing about what the game OFFERS is " +
                                 "verifiable");
                }
                Case(failures, "library-sweep", () => Case4_LibrarySweep(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = $"ARMOUR CATALOG JOB OK - no wearable armour row in the runtime catalog uses " +
                         $"job:'any' outside the {Whitelist.Count}-entry whitelist, every class named in a " +
                         "row's job really wears that row's weight, and every whitelisted row is still " +
                         "genuinely weight-free" + noteStr;
                return true;
            }
            reason = "armour-catalog-job FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  CASE 1 - job:"any" is a NAMED exception, never a default
        // =====================================================================
        private static void Case1_NoOpenJob(List<Row> rows, List<string> failures)
        {
            foreach (var r in rows)
            {
                if (!r.JobIsOpen) continue;
                if (Whitelist.ContainsKey(r.Id)) continue;

                string wt = r.Weight.Length == 0 ? "<none>" : r.Weight;
                failures.Add($"[no-open-job] armour '{r.Id}' is authored job:'{r.Job}' (weight={wt}) and is NOT " +
                             "whitelisted. job:'any' is the ABSENCE of a class gate, so every job-based loot and " +
                             "shop filter offers this row to every class - including the classes " +
                             "CanEquipArmorNow will always refuse it for. The equip seam protecting the player " +
                             "is not a licence for the catalog to offer illegal gear. Either give the row its " +
                             $"explicit eligible classes (weight '{wt}' is worn by " +
                             $"{string.Join("/", ClassesWearing(wt))}) or add it to this suite's Whitelist with " +
                             "a written reason");
            }
        }

        // =====================================================================
        //  CASE 2 - job and weight must agree about WHO CAN WEAR IT
        // =====================================================================
        private static void Case2_JobWeightAgree(List<Row> rows, List<string> failures)
        {
            foreach (var r in rows)
            {
                if (r.JobIsOpen) continue;             // Case 1 owns the open-job rows
                if (r.Weight.Length == 0 || r.Weight == "any") continue;   // weight-free: nothing to disagree with

                foreach (var cls in r.JobClasses)
                {
                    string wears = GearCatalog.ClassWeight(cls);
                    if (string.Equals(wears, r.Weight, StringComparison.OrdinalIgnoreCase)) continue;

                    failures.Add($"[job-weight-agree] armour '{r.Id}' names class '{cls}' in job:'{r.Job}' but " +
                                 $"carries weight='{r.Weight}', and a {cls} wears '{wears}'. This row is LEGAL BY " +
                                 "JOB and ILLEGAL BY WEIGHT for that class - the exact shape of the " +
                                 "blink_armor_dragonic defect. A job-based filter will offer it and " +
                                 "ArmorFitsClass will then refuse it forever");
                }
            }
        }

        // =====================================================================
        //  CASE 3 - the whitelist stays honest
        // =====================================================================
        private static void Case3_WhitelistHonest(List<Row> rows, List<string> failures)
        {
            var byId = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows) byId[r.Id] = r;

            foreach (var kv in Whitelist)
            {
                if (!byId.TryGetValue(kv.Key, out var row))
                {
                    failures.Add($"[whitelist-honest] whitelisted armour '{kv.Key}' is not in the runtime catalog " +
                                 "any more - a whitelist entry for a row that no longer exists is dead permission " +
                                 "waiting to be re-granted to whatever takes the id next. Remove the entry");
                    continue;
                }

                if (!row.JobIsOpen)
                {
                    failures.Add($"[whitelist-honest] '{kv.Key}' is whitelisted for job:'any' but is now authored " +
                                 $"job:'{row.Job}' - it no longer needs the exception, and leaving it here means " +
                                 "the next author who widens it back to 'any' passes silently");
                    continue;
                }

                if (row.Weight.Length != 0 && row.Weight != "any")
                    failures.Add($"[whitelist-honest] '{kv.Key}' is whitelisted as a genuine universal but now " +
                                 $"carries weight='{row.Weight}', which only " +
                                 $"{string.Join("/", ClassesWearing(row.Weight))} can wear. That is not universal " +
                                 "- it is the WO-1241 defect with a permission slip. Either drop the weight or " +
                                 "give the row explicit classes and remove it from the Whitelist");
            }
        }

        // =====================================================================
        //  CASE 4 - the LIBRARY superset: findings are notes, an unreadable file is a FAIL
        // ---------------------------------------------------------------------
        //  StreamingAssets is the library the tools edit; Resources is the curated runtime
        //  export the player loads (CanonicalJson reads Resources first). A violation here
        //  cannot reach a player today - but it is the source the next curated export is cut
        //  from, so it is exactly how this bug would come back. Reported, never silent.
        // =====================================================================
        private static void Case4_LibrarySweep(List<string> failures, List<string> notes)
        {
            // ⚠ TWO DIFFERENT EVENTS, and conflating them is what made this a hollow pass.
            //   * A VIOLATION found in the library is a NOTE. Resources wins at runtime, so a bad
            //     library row cannot reach a player today - that is the WO-1241 scoping decision
            //     and it stands.
            //   * The library being UNREADABLE is a FAILURE. armor.json under StreamingAssets is
            //     TRACKED SOURCE, not an optional fixture and not a harness capability, so
            //     "fixture-absent -> FAIL naming the path" is the rule that applies. It is also
            //     the source the next curated Resources export is cut from, so if it is gone the
            //     forward half of this ticket's guarantee - that a future job:"any" row fails the
            //     gate instead of reaching a player - is simply unverifiable.
            //
            // THE DEFECT THIS REPLACES: the read used to route its errors into a THROWAWAY list
            // and return, so a missing or corrupt library file produced one green-reading note and
            // the suite reported success having audited zero library rows.
            var rows = ReadRows(ArmorSA, failures);
            if (rows == null)
            {
                failures.Add($"[library-sweep] the armour LIBRARY '{ArmorSA}' could not be read, so ZERO library " +
                             "rows were audited. This is tracked source, not an optional fixture - and it is the " +
                             "file the next curated export is cut from, so an unreadable library is exactly how a " +
                             "job:'any' row gets re-seeded into Resources with nothing left to catch it");
                return;
            }

            var offenders = new List<string>();
            foreach (var r in rows)
            {
                if (r.JobIsOpen && !Whitelist.ContainsKey(r.Id)) { offenders.Add(r.Id + ":open-job"); continue; }
                if (r.JobIsOpen || r.Weight.Length == 0 || r.Weight == "any") continue;
                foreach (var cls in r.JobClasses)
                    if (!string.Equals(GearCatalog.ClassWeight(cls), r.Weight, StringComparison.OrdinalIgnoreCase))
                        offenders.Add(r.Id + ":" + cls + "-vs-" + r.Weight);
            }

            notes.Add(offenders.Count == 0
                ? $"library ({rows.Count} rows) is clean"
                : $"library ({rows.Count} rows) has {offenders.Count} row(s) that would violate the rule if " +
                  "curated into Resources: " + string.Join(", ", offenders));
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>The classes whose <see cref="GearCatalog.ClassWeight"/> equals this weight -
        /// derived from the shipped helper, never re-listed, so the day a class's weight changes
        /// this suite's error text changes with it.</summary>
        private static List<string> ClassesWearing(string weight)
        {
            var hits = new List<string>();
            foreach (var cls in PlayableHeroes.AllKnownJobKeys())
                if (string.Equals(GearCatalog.ClassWeight(cls), weight, StringComparison.OrdinalIgnoreCase))
                    hits.Add(cls);
            if (hits.Count == 0) hits.Add("<no class>");
            return hits;
        }

        /// <summary>One armour row's class-gate fields, read straight from the JSON rather than
        /// through ArmorDef, so a field the typed model happens to DROP still gets audited.</summary>
        private readonly struct Row
        {
            public readonly string Id;
            public readonly string Job;
            public readonly string Weight;

            public Row(string id, string job, string weight)
            {
                Id = id ?? string.Empty;
                Job = job ?? string.Empty;
                Weight = (weight ?? string.Empty).Trim().ToLowerInvariant();
            }

            /// <summary>True when this row's job gate admits EVERY class - empty, or "any" alone
            /// or anywhere inside a list (one "any" in a list makes the whole list vacuous).</summary>
            public bool JobIsOpen
            {
                get
                {
                    string j = Job.Trim();
                    if (j.Length == 0) return true;
                    foreach (var p in j.Split(','))
                        if (p.Trim().Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
            }

            /// <summary>The explicit class keys this row names. Empty when the job gate is open.</summary>
            public IEnumerable<string> JobClasses
            {
                get
                {
                    foreach (var p in Job.Split(','))
                    {
                        string t = p.Trim().ToLowerInvariant();
                        if (t.Length > 0 && t != "any") yield return t;
                    }
                }
            }
        }

        private static List<Row> ReadRows(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"[armour-json] {path} not found");
                return null;
            }
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var arr = root["armor"] as JArray;
                if (arr == null)
                {
                    failures.Add($"[armour-json] {path} has no 'armor' array");
                    return null;
                }
                var rows = new List<Row>(arr.Count);
                int dropped = 0;
                foreach (var t in arr)
                {
                    var o = t as JObject;
                    // A non-object element cannot be audited - and silently skipping it means the
                    // row escapes every rule below while the suite still reports green over the
                    // rows it DID see. Counted and failed, never dropped.
                    if (o == null) { dropped++; continue; }
                    rows.Add(new Row((string)o["id"], (string)o["job"], (string)o["weight"]));
                }
                if (dropped > 0)
                    failures.Add($"[armour-json] {path} has {dropped} entr(ies) in its 'armor' array that are not " +
                                 "objects, so they were UNAUDITABLE. An armour row this suite cannot parse is an " +
                                 "armour row this suite cannot vouch for");
                return rows;
            }
            catch (Exception ex)
            {
                failures.Add($"[armour-json] could not parse {path}: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}
