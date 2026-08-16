// =============================================================================
// ClassResourceRegression — pins WO-997 (the class resource economy).
// -----------------------------------------------------------------------------
// regression-registry: registered by the committer (do NOT self-register here —
// DataRegression.cs is lane-fenced; the orchestrator adds the [class-resource] row).
//
// WHY: before WO-997 the mana plumbing was healthy but the DATA made it inert —
// every basic attack and every universal.* skill cost 0, utility 1-3, ultimates
// 6-8 against a 10 pool, so ONLY ultimates were pool-gated and mana did not
// matter. This suite pins the economy so it cannot silently rot back:
//   Case 1 — every playable class (mage/knight/ranger) authors a 'resource'
//            block (displayName + max > 0 + regenPerSecond > 0) in abilities.json.
//   Case 2 — every authored skill cost fits its owning class's pool
//            (kit + "-skills" pool <= that class's resource max; universal.* = 0).
//   Case 3 — at least one NON-ultimate (slot != r) skill per playable class kit
//            has cost > 0 — the exact "everything is cooldown-gated" regression
//            this WO exists to kill.
//   Case 4 — the two abilities.json copies (Resources mirror + StreamingAssets
//            canonical) are BYTE-IDENTICAL — a lone edit to one copy ships two
//            different economies depending on platform load path.
// Parsed straight from the JSON files (never through a live AbilityCatalog), so
// the suite also covers a copy that parses but was only half-edited.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ClassResourceRegression
    {
        private const string ResourcesPath = "Assets/Resources/Data/Canonical/abilities.json";
        private const string StreamingPath = "Assets/StreamingAssets/Data/Canonical/abilities.json";

        /// <summary>The playable classes WO-997 rules an economy for (Cleric aliases onto mage upstream).</summary>
        private static readonly string[] PlayableClasses = { "mage", "knight", "ranger" };

        /// <summary>Standalone batch entry — prints the CLASS_RESOURCE_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("CLASS_RESOURCE_OK - " + reason);
            else Debug.LogError("CLASS_RESOURCE_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([class-resource]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            JObject root = null;
            Case(failures, "parse", () =>
            {
                string json = ReadText(ResourcesPath);
                if (json == null) { failures.Add($"[parse] cannot read {ResourcesPath}"); return; }
                root = JObject.Parse(json);
            });

            if (root != null)
            {
                Case(failures, "resource-blocks", () => Case1_EveryPlayableClassHasAResourceBlock(root, failures, notes));
                Case(failures, "costs-fit-pool", () => Case2_EveryCostFitsItsOwningPool(root, failures, notes));
                Case(failures, "non-ultimate-costs", () => Case3_EachClassHasACostedNonUltimate(root, failures, notes));
            }
            Case(failures, "copies-identical", () => Case4_BothCopiesByteIdentical(failures, notes));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "CLASS RESOURCE OK - 4/4 cases pass (every playable class authors a resource " +
                         "block, every skill cost fits its owning pool (universal stays free), each class " +
                         "has a costed non-ultimate, and the two abilities.json copies are byte-identical)" +
                         noteStr;
                return true;
            }
            reason = "CLASS RESOURCE FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  CASE 1 — every playable class authors a resource block
        // =====================================================================
        private static void Case1_EveryPlayableClassHasAResourceBlock(JObject root, List<string> failures, List<string> notes)
        {
            foreach (string cls in PlayableClasses)
            {
                JToken res = root.SelectToken($"classes.{cls}.resource");
                if (res == null)
                {
                    failures.Add($"[resource-blocks] classes.{cls} has NO 'resource' block — the class " +
                                 "falls back to the serialized HeroAbilities defaults (10 @ 0.9/s) and " +
                                 "loses its WO-997 identity.");
                    continue;
                }
                string name = (string)res["displayName"];
                float max = (float?)res["max"] ?? 0f;
                float regen = (float?)res["regenPerSecond"] ?? 0f;
                if (string.IsNullOrWhiteSpace(name))
                    failures.Add($"[resource-blocks] {cls}.resource.displayName is empty — the HUD label has nothing to show.");
                if (max <= 0f)
                    failures.Add($"[resource-blocks] {cls}.resource.max={max} must be > 0.");
                if (regen <= 0f)
                    failures.Add($"[resource-blocks] {cls}.resource.regenPerSecond={regen} must be > 0.");
                float onHit = (float?)res["onHitRestore"] ?? 0f;
                notes.Add($"{cls}={name} {max:0.#}@{regen:0.##}/s hit+{onHit:0.#}");
            }
        }

        // =====================================================================
        //  CASE 2 — every authored cost fits its owning class's pool
        // =====================================================================
        private static void Case2_EveryCostFitsItsOwningPool(JObject root, List<string> failures, List<string> notes)
        {
            var classes = root["classes"] as JObject;
            if (classes == null) { failures.Add("[costs-fit-pool] no 'classes' object"); return; }

            foreach (var kvp in classes)
            {
                string key = kvp.Key;
                var abilities = kvp.Value?["abilities"] as JObject;
                if (abilities == null) continue;

                // Owning class: strip the "-skills" pool suffix (mirrors AbilityCatalog.OwningClassOf).
                string owner = key.EndsWith("-skills", StringComparison.Ordinal)
                    ? key.Substring(0, key.Length - "-skills".Length) : key;

                foreach (var ab in abilities)
                {
                    float cost = (float?)ab.Value?["manaCost"] ?? 0f;
                    string label = $"{key}.{ab.Key}";

                    if (owner == "universal")
                    {
                        // WO-997: universal.* stay FREE (0) — a costed universal skill would
                        // price the same skill differently per class pool.
                        if (cost != 0f)
                            failures.Add($"[costs-fit-pool] {label} costs {cost} but universal skills must stay 0.");
                        continue;
                    }

                    float? poolMax = (float?)root.SelectToken($"classes.{owner}.resource.max");
                    if (poolMax == null)
                    {
                        if (cost > 0f)
                            failures.Add($"[costs-fit-pool] {label} costs {cost} but owning class '{owner}' " +
                                         "authors no resource block to pay it from.");
                        continue;
                    }
                    if (cost > poolMax.Value)
                        failures.Add($"[costs-fit-pool] {label} costs {cost} — MORE than {owner}'s pool max " +
                                     $"{poolMax.Value:0.#}; the skill is uncastable at base pool.");
                }
            }
        }

        // =====================================================================
        //  CASE 3 — at least one costed NON-ultimate per playable class kit
        // =====================================================================
        private static void Case3_EachClassHasACostedNonUltimate(JObject root, List<string> failures, List<string> notes)
        {
            foreach (string cls in PlayableClasses)
            {
                var abilities = root.SelectToken($"classes.{cls}.abilities") as JObject;
                if (abilities == null)
                {
                    failures.Add($"[non-ultimate-costs] classes.{cls} has no abilities block.");
                    continue;
                }
                bool found = false;
                foreach (var ab in abilities)
                {
                    string slot = ((string)ab.Value?["slot"] ?? ab.Key).Trim().ToLowerInvariant();
                    float cost = (float?)ab.Value?["manaCost"] ?? 0f;
                    if (slot != "r" && cost > 0f) { found = true; break; }
                }
                if (!found)
                    failures.Add($"[non-ultimate-costs] {cls} has NO non-ultimate skill with cost > 0 — " +
                                 "the pre-WO-997 'only ultimates are pool-gated, mana does not matter' " +
                                 "regression is back.");
            }
        }

        // =====================================================================
        //  CASE 4 — the two abilities.json copies are byte-identical
        // =====================================================================
        private static void Case4_BothCopiesByteIdentical(List<string> failures, List<string> notes)
        {
            byte[] a = ReadBytes(ResourcesPath);
            byte[] b = ReadBytes(StreamingPath);
            if (a == null) { failures.Add($"[copies-identical] cannot read {ResourcesPath}"); return; }
            if (b == null) { failures.Add($"[copies-identical] cannot read {StreamingPath}"); return; }
            if (a.Length != b.Length)
            {
                failures.Add($"[copies-identical] the two abilities.json copies differ in SIZE " +
                             $"({a.Length} vs {b.Length} bytes) — one copy was edited alone, so the " +
                             "Resources and StreamingAssets load paths ship different economies.");
                return;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    failures.Add($"[copies-identical] the two abilities.json copies DIVERGE at byte {i} " +
                                 "— one copy was edited alone; re-mirror so both load paths agree.");
                    return;
                }
            }
            notes.Add($"copies identical ({a.Length} bytes)");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception) { return null; }
        }

        private static byte[] ReadBytes(string path)
        {
            try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
            catch (Exception) { return null; }
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
