// =============================================================================
// CathedralCumulativeRegression [cathedral-cumulative]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE DEFECT THIS EXISTS TO KILL (owner AC, 2026-08-02: "Cathedral at T4 still grants
// frost-nova, manaweave, arcane-bolt, etc."):
//
//   ModifierService.Compute applies ONLY the CURRENT tier's def -
//       var def = BuildingTierCatalog.TierOf(kv.Key, tier);   // kv.Value == current tier
//   - one def, never a sum over the tiers beneath it. Tier modifiers are therefore
//   CUMULATIVE-ABSOLUTE by contract: every tier must RESTATE everything every lower
//   tier granted. The arcane-tower (Cathedral of Magic) mage rows were authored as
//   per-tier DELTAS instead, so upgrading REVOKED what you already had:
//       T1 spellPower 1.05
//       T2 manaMax/manaRegen + frost-nova            -> LOST spellPower
//       T3 hp/shell + manaweave,arcane-bolt          -> LOST mana*, spellPower, frost-nova
//       T4 spellPower 1.20/manaCost + cataclysm      -> LOST mana*, hp, shell, ALL 3 spells
//   A fully-upgraded 5,500-wood Cathedral was strictly WORSE than tier 3: it traded
//   three spells for one.
//
//   IT CANNOT BE FIXED IN CODE. GameModifiers.MergeSpellList does union unlockSpell,
//   and ModifierService.Apply does compound the mults - but across CONTRIBUTORS (this
//   tier + owned perks + other buildings), never across TIERS of the same building,
//   because only one tier def is ever handed to Apply. The fix is, and stays, in data.
//
// WHY THIS SUITE IS GENERAL: arcane-tower was the building that got caught. The rule
// is a property of the LOADER, so it binds EVERY building in the file - including the
// next one someone authors as deltas because the JSON gives no hint. Case 1 walks
// every building, every tier, every key; Case 2 does the same for unlockSpell ids.
//
// Cases:
//   1 [tier-keys]      For every building, every `modifiers` key present at tier N is
//                      still present at tier N+1 .. max. A key that vanishes is a
//                      buff the player PAID FOR and then lost on the next upgrade.
//   2 [tier-spells]    Same rule for every id inside the comma-separated `unlockSpell`
//                      CSV - the owner AC verbatim.
//   3 [dual-copy]      Both canonical copies of building-tiers.json are byte-identical
//                      (this file IS a mirrored pair, unlike weapons.json), so the
//                      device cannot ship a ladder the editor never validated.
//   4 [premise]        The premise this whole suite rests on is still true:
//                      ModifierService.Compute applies exactly ONE tier def. If that
//                      ever becomes a fold over tiers 1..N, cumulative-absolute stops
//                      being required and this suite must be retired deliberately
//                      rather than left to enforce a rule that no longer exists.
//
//   Informational only (never a failure): a numeric value that DECREASES between
//   tiers is reported as a note, because "lower is better" is real for cost-style
//   keys (mageManaCostMult 0.85) and asserting monotonic growth would be wrong.
//
// Markers: CATHEDRAL_CUMULATIVE_OK / CATHEDRAL_CUMULATIVE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.CathedralCumulativeRegression.RunAll
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

namespace DeNelle.Editor.Regression
{
    public static class CathedralCumulativeRegression
    {
        private const string TiersRes = "Assets/Resources/Data/Canonical/building-tiers.json";
        private const string TiersSA = "Assets/StreamingAssets/Data/Canonical/building-tiers.json";
        private const string ModifierServiceSrc = "Assets/_Modules/Core/State/ModifierService.cs";

        /// <summary>The key whose value is a comma-separated list of ability ids rather than a number.</summary>
        private const string SpellKey = "unlockSpell";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("CATHEDRAL_CUMULATIVE_OK - " + reason);
            else Debug.LogError("CATHEDRAL_CUMULATIVE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "tier-keys", () => Case1_Keys(failures, notes));
                Case(failures, "tier-spells", () => Case2_Spells(failures, notes));
                Case(failures, "dual-copy", () => Case3_DualCopy(failures));
                Case(failures, "premise", () => Case4_Premise(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "CATHEDRAL CUMULATIVE OK - every building tier ladder in both copies of " +
                         "building-tiers.json is cumulative-absolute: no modifier key and no unlockSpell id " +
                         "granted at a tier is ever revoked by a higher tier, which is required because " +
                         "ModifierService.Compute applies exactly one tier def" + noteStr;
                return true;
            }
            reason = "cathedral-cumulative FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  MODEL - read straight from JSON on purpose. The typed BuildingTierCatalog
        //  deserializes into GameModifiers, which SILENTLY DISCARDS any key with no
        //  matching field - so a typed read could not see an authored key vanish, which
        //  is precisely what this suite exists to see.
        // =====================================================================
        private sealed class TierRow
        {
            public int Tier;
            public string Name;
            public Dictionary<string, JToken> Modifiers = new Dictionary<string, JToken>(StringComparer.Ordinal);
        }

        private sealed class BuildingRow
        {
            public string Id;
            public List<TierRow> Tiers = new List<TierRow>();
        }

        private static List<BuildingRow> ReadBuildings(string path, List<string> failures, string caseTag)
        {
            var result = new List<BuildingRow>();
            if (!File.Exists(path))
            {
                failures.Add("[" + caseTag + "] " + path + " not found - the tier ladder cannot be validated");
                return result;
            }

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("[" + caseTag + "] " + path + " failed to parse (" + ex.GetType().Name + ": " +
                             ex.Message + ")");
                return result;
            }

            var buildings = root["buildings"] as JArray;
            if (buildings == null)
            {
                failures.Add("[" + caseTag + "] " + path + " has no 'buildings' array");
                return result;
            }

            foreach (var b in buildings)
            {
                var bo = b as JObject;
                if (bo == null) continue;
                var row = new BuildingRow { Id = (string)bo["id"] ?? "<no-id>" };

                var tiers = bo["tiers"] as JArray;
                if (tiers == null)
                {
                    failures.Add("[" + caseTag + "] building '" + row.Id + "' in " + CopyLabel(path) +
                                 " has no 'tiers' array");
                    result.Add(row);
                    continue;
                }

                foreach (var t in tiers)
                {
                    var to = t as JObject;
                    if (to == null) continue;
                    var tr = new TierRow
                    {
                        Tier = to["tier"] != null ? (int)to["tier"] : 0,
                        Name = (string)to["name"] ?? "<no-name>",
                    };
                    var mods = to["modifiers"] as JObject;
                    if (mods != null)
                        foreach (var p in mods.Properties())
                            tr.Modifiers[p.Name] = p.Value;
                    row.Tiers.Add(tr);
                }

                row.Tiers.Sort((x, y) => x.Tier.CompareTo(y.Tier));
                result.Add(row);
            }

            if (result.Count == 0)
                failures.Add("[" + caseTag + "] " + CopyLabel(path) + " yielded ZERO buildings - this suite would " +
                             "report OK without validating a single ladder");
            return result;
        }

        // =====================================================================
        //  CASE 1 - no modifier key is ever revoked by a higher tier
        // =====================================================================
        private static void Case1_Keys(List<string> failures, List<string> notes)
        {
            foreach (string path in new[] { TiersRes, TiersSA })
            {
                var buildings = ReadBuildings(path, failures, "tier-keys");
                int checkedPairs = 0;

                foreach (var b in buildings)
                {
                    for (int i = 1; i < b.Tiers.Count; i++)
                    {
                        var lo = b.Tiers[i - 1];
                        var hi = b.Tiers[i];
                        checkedPairs++;

                        foreach (var kv in lo.Modifiers)
                        {
                            if (string.Equals(kv.Key, SpellKey, StringComparison.Ordinal)) continue; // Case 2 owns it

                            if (!hi.Modifiers.ContainsKey(kv.Key))
                            {
                                failures.Add("[tier-keys] " + CopyLabel(path) + " building '" + b.Id + "': tier " +
                                             hi.Tier + " ('" + hi.Name + "') does NOT restate '" + kv.Key +
                                             "' granted at tier " + lo.Tier + " ('" + lo.Name + "', value " +
                                             Describe(kv.Value) + "). ModifierService.Compute applies ONLY the " +
                                             "current tier def, so upgrading to tier " + hi.Tier + " REVOKES that " +
                                             "buff - the player pays to get weaker. Tier ladders are " +
                                             "CUMULATIVE-ABSOLUTE: restate the full kit at every tier");
                                continue;
                            }

                            // Informational: a decrease is legal (cost-style keys are better when lower),
                            // but it is worth surfacing so a fat-fingered nerf is at least visible.
                            double loV, hiV;
                            if (TryNumber(kv.Value, out loV) && TryNumber(hi.Modifiers[kv.Key], out hiV) && hiV < loV)
                                notes.Add(b.Id + " '" + kv.Key + "' DROPS " + Fmt(loV) + " -> " + Fmt(hiV) +
                                          " from tier " + lo.Tier + " to " + hi.Tier + " (legal for cost-style " +
                                          "keys where lower is better - confirm it is intended)");
                        }
                    }
                }

                notes.Add(CopyLabel(path) + ": " + buildings.Count + " buildings, " + checkedPairs +
                          " adjacent tier pairs checked");
            }
        }

        // =====================================================================
        //  CASE 2 - no unlockSpell id is ever revoked by a higher tier
        // =====================================================================
        private static void Case2_Spells(List<string> failures, List<string> notes)
        {
            foreach (string path in new[] { TiersRes, TiersSA })
            {
                var buildings = ReadBuildings(path, failures, "tier-spells");
                int spellTiers = 0;

                foreach (var b in buildings)
                {
                    // The union of everything granted at or below the current tier.
                    var owed = new List<string>();
                    foreach (var t in b.Tiers)
                    {
                        var granted = SpellIds(t);
                        if (granted.Count > 0) spellTiers++;

                        foreach (string need in owed)
                        {
                            if (!granted.Contains(need))
                                failures.Add("[tier-spells] " + CopyLabel(path) + " building '" + b.Id +
                                             "': tier " + t.Tier + " ('" + t.Name + "') unlockSpell does not " +
                                             "include '" + need + "', which a lower tier already granted. Only " +
                                             "ONE tier def reaches ModifierService, and MergeSpellList unions " +
                                             "across CONTRIBUTORS, not across tiers of the same building - so " +
                                             "this upgrade TAKES THE SPELL AWAY. Restate every earlier id");
                        }

                        foreach (string g in granted) if (!owed.Contains(g)) owed.Add(g);
                    }
                }

                notes.Add(CopyLabel(path) + ": " + spellTiers + " tiers carry unlockSpell");
            }
        }

        private static List<string> SpellIds(TierRow t)
        {
            var ids = new List<string>();
            JToken tok;
            if (t == null || !t.Modifiers.TryGetValue(SpellKey, out tok)) return ids;
            string csv = tok != null ? (string)tok : null;
            if (string.IsNullOrEmpty(csv)) return ids;
            foreach (string part in csv.Split(','))
            {
                string id = part.Trim();
                if (id.Length > 0 && !ids.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        // =====================================================================
        //  CASE 3 - the mirrored pair really is byte-identical
        // =====================================================================
        private static void Case3_DualCopy(List<string> failures)
        {
            if (!File.Exists(TiersRes) || !File.Exists(TiersSA))
            {
                failures.Add("[dual-copy] one of the two canonical copies is missing (Resources exists=" +
                             File.Exists(TiersRes) + ", StreamingAssets exists=" + File.Exists(TiersSA) + ")");
                return;
            }

            byte[] res = File.ReadAllBytes(TiersRes);
            byte[] sa = File.ReadAllBytes(TiersSA);

            if (res.Length != sa.Length)
            {
                failures.Add("[dual-copy] building-tiers.json copies differ in SIZE (Resources " + res.Length +
                             " bytes vs StreamingAssets " + sa.Length + ") - the editor and the device would " +
                             "read different tier ladders, and only one of them was validated above");
                return;
            }
            for (int i = 0; i < res.Length; i++)
            {
                if (res[i] != sa[i])
                {
                    failures.Add("[dual-copy] building-tiers.json copies diverge at byte " + i +
                                 " - re-mirror the file (a BOM or a CRLF/LF flip counts, and this is exactly " +
                                 "where a PowerShell redirect silently injects one)");
                    return;
                }
            }

            if (res.Length >= 3 && res[0] == 0xEF && res[1] == 0xBB && res[2] == 0xBF)
                failures.Add("[dual-copy] building-tiers.json starts with a UTF-8 BOM - the canonical loader " +
                             "reads these files as plain UTF-8 and a BOM has broken parsing here before");
        }

        // =====================================================================
        //  CASE 4 - the premise: exactly ONE tier def is applied
        // =====================================================================
        private static void Case4_Premise(List<string> failures)
        {
            if (!File.Exists(ModifierServiceSrc))
            {
                failures.Add("[premise] " + ModifierServiceSrc + " not found - this suite enforces a rule whose " +
                             "justification it can no longer read; re-point it deliberately");
                return;
            }

            string code;
            try { code = StripComments(File.ReadAllText(ModifierServiceSrc)); }
            catch (Exception ex)
            {
                failures.Add("[premise] could not read " + ModifierServiceSrc + ": " + ex.GetType().Name + ": " +
                             ex.Message);
                return;
            }

            // The single-tier read. If this ever becomes a loop over 1..tier, the whole
            // cumulative-absolute contract dissolves and this suite must be retired, not silenced.
            if (!Regex.IsMatch(code, @"BuildingTierCatalog\.TierOf\s*\(\s*[\w\.]+\s*,\s*tier\s*\)"))
                failures.Add("[premise] ModifierService no longer reads BuildingTierCatalog.TierOf(id, tier) in " +
                             "the shape this suite expects. Either the tier fold changed (in which case " +
                             "cumulative-absolute may no longer be required and this suite should be RETIRED " +
                             "deliberately) or the modifier compile moved - do not silence this, decide it");

            if (Regex.IsMatch(code, @"for\s*\(\s*int\s+\w+\s*=\s*1\s*;\s*\w+\s*<=\s*tier\s*;"))
                failures.Add("[premise] ModifierService now appears to FOLD tiers 1..N. If that is intentional, " +
                             "the tier ladders should be re-authored as per-tier DELTAS and this suite retired - " +
                             "leaving both in place double-counts every restated modifier");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private static string CopyLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "<unknown>";
            if (path.IndexOf("StreamingAssets", StringComparison.OrdinalIgnoreCase) >= 0) return "StreamingAssets";
            if (path.IndexOf("Resources", StringComparison.OrdinalIgnoreCase) >= 0) return "Resources";
            return path;
        }

        private static bool TryNumber(JToken tok, out double value)
        {
            value = 0d;
            if (tok == null) return false;
            if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
            {
                value = (double)tok;
                return true;
            }
            return false;
        }

        private static string Describe(JToken tok)
        {
            if (tok == null) return "<null>";
            string s = tok.ToString();
            return s.Length > 60 ? s.Substring(0, 57) + "..." : s;
        }

        private static string Fmt(double d)
        {
            return d.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
