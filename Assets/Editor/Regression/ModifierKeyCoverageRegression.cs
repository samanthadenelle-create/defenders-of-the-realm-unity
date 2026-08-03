// =============================================================================
// ModifierKeyCoverageRegression [modifier-key-coverage]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE BUG CLASS THIS EXISTS TO KILL (WO-861 Phase 3, 2026-08-02):
//   building-tiers.json's arcane-tower ("Cathedral of Magic") rows were re-pointed
//   to MAGE stats and authored SEVEN new perk keys - mageSpellPowerMult,
//   mageManaMax, mageManaRegenMult, mageHpBonusPct, mageShellStrengthMult,
//   mageManaCostMult, unlockSpell. DeNelle.Core.State.GameModifiers is a STRICTLY
//   TYPED flat class with no extension data, so Newtonsoft SILENTLY DROPPED every
//   one of them (MissingMemberHandling.Ignore): no exception, no log, no warning.
//   The Cathedral cost 5,500 wood and granted the mage NOTHING, and the ONLY way
//   anyone could ever have noticed was by playing it and feeling nothing change.
//
// This suite is deliberately GENERIC, not a list of the seven keys: it asserts that
// EVERY key authored in ANY `modifiers` block, in BOTH copies of building-tiers.json,
// maps to a real serializable member of GameModifiers. Author a new key without a
// field and the gate goes red instead of the perk going quietly inert.
//
// Cases:
//   1 [key-coverage]   Every `modifiers` key in both copies of building-tiers.json
//                      resolves to a GameModifiers member (by [JsonProperty] name,
//                      then by field name). This is the silent-drop defect verbatim.
//   2 [clone-covers]   Clone() really deep-copies EVERY field. Behavioural, not a
//                      lint: stamp every field with a distinct sentinel, Clone, and
//                      diff field-by-field via reflection. A field added without a
//                      Clone() line vanishes on any layered/override path - the same
//                      silent-loss class as the dropped key.
//   3 [apply-covers]   ModifierService.Apply aggregates EVERY field (source lint -
//                      Apply is private and Compute needs live GameState, so the
//                      thing that regresses here is a MISSING line, which a lint
//                      catches exactly). Also pins the KIND per field: *Mult must be
//                      compounded, the additive/flag fields must not be.
//   4 [spell-union]    GameModifiers.MergeSpellList really UNIONS the comma lists
//                      (dedupes case-insensitively, preserves order, never
//                      overwrites) - a tier-4 unlock must not revoke a tier-2 spell.
//   5 [identity]       A default GameModifiers is IDENTITY: every *Mult defaults to
//                      1, every additive/flag/string to 0/false/empty. This is what
//                      makes an UNBUILT building a no-op.
//   6 [unlock-resolves] Every id inside an authored `unlockSpell` CSV resolves to a
//                      real ability id in abilities.json (both copies). A typo'd
//                      spell id is another silent no-op the player pays gold for.
//
// Markers: MODIFIER_KEY_COVERAGE_OK / MODIFIER_KEY_COVERAGE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.ModifierKeyCoverageRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class ModifierKeyCoverageRegression
    {
        private const string TiersRes = "Assets/Resources/Data/Canonical/building-tiers.json";
        private const string TiersSA = "Assets/StreamingAssets/Data/Canonical/building-tiers.json";
        private const string AbilitiesRes = "Assets/Resources/Data/Canonical/abilities.json";
        private const string AbilitiesSA = "Assets/StreamingAssets/Data/Canonical/abilities.json";

        private const string ModifierServiceSrc = "Assets/_Modules/Core/State/ModifierService.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("MODIFIER_KEY_COVERAGE_OK - " + reason);
            else Debug.LogError("MODIFIER_KEY_COVERAGE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "key-coverage", () => Case1_KeyCoverage(failures, notes));
                Case(failures, "clone-covers", () => Case2_CloneCoversEveryField(failures, notes));
                Case(failures, "apply-covers", () => Case3_ApplyAggregatesEveryField(failures));
                Case(failures, "spell-union", () => Case4_SpellUnion(failures));
                Case(failures, "identity", () => Case5_DefaultIsIdentity(failures));
                Case(failures, "unlock-resolves", () => Case6_UnlockSpellResolves(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "MODIFIER KEY COVERAGE OK - every modifiers key authored in building-tiers.json " +
                         "maps to a real GameModifiers member, Clone() deep-copies every field, " +
                         "ModifierService.Apply aggregates every field, unlockSpell unions instead of " +
                         "overwriting, the default contract is identity, and every unlocked spell id " +
                         "resolves in abilities.json" + noteStr;
                return true;
            }
            reason = "modifier-key-coverage FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  The GameModifiers surface, discovered by REFLECTION so this suite can
        //  never drift out of date with the class it guards.
        // =====================================================================

        private static FieldInfo[] ModifierFields()
        {
            return typeof(GameModifiers).GetFields(BindingFlags.Public | BindingFlags.Instance);
        }

        /// <summary>The JSON name a field answers to (its [JsonProperty], else the field name).</summary>
        private static string JsonNameOf(FieldInfo f)
        {
            var attr = f.GetCustomAttribute<JsonPropertyAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.PropertyName)) return attr.PropertyName;
            return f.Name;
        }

        /// <summary>Every JSON key GameModifiers can absorb, plus the field names as an alias set
        /// (Newtonsoft also matches a bare field name case-insensitively when no attribute wins).</summary>
        private static HashSet<string> KnownKeys()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in ModifierFields())
            {
                keys.Add(JsonNameOf(f));
                keys.Add(f.Name);
            }
            return keys;
        }

        // =====================================================================
        //  CASE 1 - every authored modifiers key maps to a real field
        // =====================================================================
        private static void Case1_KeyCoverage(List<string> failures, List<string> notes)
        {
            var known = KnownKeys();
            if (known.Count == 0)
            {
                failures.Add("[key-coverage] GameModifiers exposes ZERO public instance fields - reflection " +
                             "found nothing to match against, so this suite cannot see the defect it exists for");
                return;
            }

            foreach (var path in new[] { TiersRes, TiersSA })
            {
                var root = ReadJson(path, "key-coverage", failures);
                if (root == null) continue;

                int blocks = 0;
                var authored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var block in FindModifierBlocks(root))
                {
                    blocks++;
                    foreach (var prop in block.Properties())
                    {
                        authored.Add(prop.Name);
                        if (known.Contains(prop.Name)) continue;
                        failures.Add("[key-coverage] '" + prop.Name + "' is authored in a modifiers block of " +
                                     CopyLabel(path) + " but GameModifiers has NO matching field - Newtonsoft " +
                                     "drops it SILENTLY (MissingMemberHandling.Ignore), so this perk costs the " +
                                     "player resources and grants exactly nothing. Add the field AND a Clone() " +
                                     "line AND a ModifierService.Apply line in the same change");
                    }
                }

                if (blocks == 0)
                    failures.Add("[key-coverage] " + CopyLabel(path) + " contains ZERO 'modifiers' blocks - either " +
                                 "the catalog shape drifted or the whole perk ladder was dropped; this suite would " +
                                 "then be vacuously green");

                notes.Add(CopyLabel(path) + "=" + blocks + " modifier blocks / " + authored.Count + " distinct keys");
            }
        }

        /// <summary>Every JSON object sitting under a "modifiers" property, anywhere in the tree
        /// (tiers[].modifiers AND tiers[].perks[].modifiers today; anything added later too).</summary>
        private static IEnumerable<JObject> FindModifierBlocks(JToken root)
        {
            var found = new List<JObject>();
            Walk(root, found);
            return found;
        }

        private static void Walk(JToken node, List<JObject> found)
        {
            if (node == null) return;
            var obj = node as JObject;
            if (obj != null)
            {
                foreach (var p in obj.Properties())
                {
                    if (string.Equals(p.Name, "modifiers", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = p.Value as JObject;
                        if (m != null) found.Add(m);
                    }
                    Walk(p.Value, found);
                }
                return;
            }
            var arr = node as JArray;
            if (arr != null)
            {
                foreach (var c in arr) Walk(c, found);
            }
        }

        // =====================================================================
        //  CASE 2 - Clone() deep-copies EVERY field (behavioural, via sentinels)
        // =====================================================================
        private static void Case2_CloneCoversEveryField(List<string> failures, List<string> notes)
        {
            var fields = ModifierFields();
            var source = new GameModifiers();

            // Stamp every field with a value that is provably NOT its default.
            var stamped = new List<string>();
            foreach (var f in fields)
            {
                object sentinel = SentinelFor(f);
                if (sentinel == null)
                {
                    failures.Add("[clone-covers] field '" + f.Name + "' has type " + f.FieldType.Name +
                                 " which this suite cannot stamp - extend SentinelFor so the new field is " +
                                 "actually covered instead of silently skipped");
                    continue;
                }
                f.SetValue(source, sentinel);
                stamped.Add(f.Name);
            }

            var copy = source.Clone();
            if (copy == null)
            {
                failures.Add("[clone-covers] Clone() returned null");
                return;
            }
            if (ReferenceEquals(copy, source))
            {
                failures.Add("[clone-covers] Clone() returned the SAME instance - callers layering an override " +
                             "would mutate the source contract");
                return;
            }

            foreach (var f in fields)
            {
                object want = f.GetValue(source);
                object got = f.GetValue(copy);
                if (Equals(want, got)) continue;
                failures.Add("[clone-covers] Clone() DROPPED '" + JsonNameOf(f) + "' (field " + f.Name + "): " +
                             "expected " + Show(want) + ", got " + Show(got) + " - Clone() is HAND-WRITTEN, and a " +
                             "field with no line in it silently reverts to its default on every layered/override " +
                             "path (SetOverrideJson, scene-creation modifier JSON, GameModifiers.None.Clone())");
            }

            notes.Add("clone covers " + stamped.Count + "/" + fields.Length + " fields");
        }

        /// <summary>A non-default value for a field type, or null when the type is unsupported.</summary>
        private static object SentinelFor(FieldInfo f)
        {
            var t = f.FieldType;
            if (t == typeof(float)) return 7.25f;
            if (t == typeof(double)) return 7.25d;
            if (t == typeof(int)) return 7;
            if (t == typeof(long)) return 7L;
            if (t == typeof(bool)) return true;
            if (t == typeof(string)) return "sentinel." + f.Name;
            return null;
        }

        // =====================================================================
        //  CASE 3 - ModifierService.Apply aggregates every field
        // =====================================================================
        private static void Case3_ApplyAggregatesEveryField(List<string> failures)
        {
            string src = ReadSource(ModifierServiceSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            var m = Regex.Match(code, @"void\s+Apply\s*\(\s*GameModifiers\s+\w+\s*,\s*GameModifiers\s+\w+\s*\)\s*\{(?<body>(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*)\}");
            if (!m.Success)
            {
                failures.Add("[apply-covers] could not locate ModifierService.Apply(GameModifiers, GameModifiers) - " +
                             "the aggregation choke point moved or changed shape; re-point this lint deliberately " +
                             "rather than leaving the coverage assert blind");
                return;
            }
            string body = m.Groups["body"].Value;

            foreach (var f in ModifierFields())
            {
                if (!Regex.IsMatch(body, @"\b" + Regex.Escape(f.Name) + @"\b"))
                {
                    failures.Add("[apply-covers] ModifierService.Apply never mentions '" + f.Name + "' - the field " +
                                 "deserializes fine but is NEVER folded into the compiled contract, so the perk is " +
                                 "inert for every building and every research perk that authors it");
                    continue;
                }

                // Kind check: a multiplier must be COMPOUNDED, never overwritten or added.
                bool isMult = f.Name.EndsWith("Mult", StringComparison.Ordinal);
                if (isMult)
                {
                    bool compounded = Regex.IsMatch(body, @"\b\w+\." + Regex.Escape(f.Name) + @"\s*\*=")
                                   || Regex.IsMatch(body, @"\b\w+\." + Regex.Escape(f.Name) + @"\s*=\s*\w*Mul\w*\s*\(");
                    if (!compounded)
                        failures.Add("[apply-covers] '" + f.Name + "' ends in Mult but Apply does not COMPOUND it " +
                                     "(no '*=' and no Mul-helper assignment) - stacking a tier with a research perk " +
                                     "would overwrite instead of multiply, so one of the two perks is silently lost");
                }
                else if (f.FieldType == typeof(float) || f.FieldType == typeof(int))
                {
                    bool accumulated = Regex.IsMatch(body, @"\b\w+\." + Regex.Escape(f.Name) + @"\s*\+=");
                    if (!accumulated)
                        failures.Add("[apply-covers] additive field '" + f.Name + "' is not accumulated with '+=' in " +
                                     "Apply - an additive perk that is assigned rather than summed keeps only the " +
                                     "last contributor");
                }
            }
        }

        // =====================================================================
        //  CASE 4 - unlockSpell UNIONS, it never overwrites
        // =====================================================================
        private static void Case4_SpellUnion(List<string> failures)
        {
            Expect(failures, "spell-union", GameModifiers.MergeSpellList("", ""), "", "empty + empty");
            Expect(failures, "spell-union", GameModifiers.MergeSpellList(null, "mage.frost-nova"), "mage.frost-nova",
                   "null + one");
            Expect(failures, "spell-union", GameModifiers.MergeSpellList("mage.frost-nova", null), "mage.frost-nova",
                   "one + null");
            Expect(failures, "spell-union",
                   GameModifiers.MergeSpellList("mage.frost-nova", "mage.manaweave,mage.arcane-bolt"),
                   "mage.frost-nova,mage.manaweave,mage.arcane-bolt",
                   "tier2 + tier3 (the real authored case - three spells, none revoked)");
            Expect(failures, "spell-union",
                   GameModifiers.MergeSpellList("mage.frost-nova", "MAGE.FROST-NOVA,mage.cataclysm"),
                   "mage.frost-nova,mage.cataclysm",
                   "case-insensitive dedupe");
            Expect(failures, "spell-union",
                   GameModifiers.MergeSpellList(" mage.a , mage.b ", "mage.c"),
                   "mage.a,mage.b,mage.c",
                   "whitespace around ids is trimmed (authored CSVs are hand-written)");

            // The contract that actually matters: a later contributor NEVER shrinks the list.
            string merged = GameModifiers.MergeSpellList("mage.frost-nova,mage.manaweave", "mage.cataclysm");
            if (merged.IndexOf("mage.frost-nova", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[spell-union] merging a tier-4 unlock onto earlier tiers LOST an earlier spell (" +
                             merged + ") - upgrading the Cathedral would take a spell away from the player");
        }

        private static void Expect(List<string> failures, string caseName, string got, string want, string what)
        {
            if (!string.Equals(got ?? "", want, StringComparison.Ordinal))
                failures.Add("[" + caseName + "] " + what + ": expected '" + want + "', got '" + (got ?? "<null>") + "'");
        }

        // =====================================================================
        //  CASE 5 - the default contract is IDENTITY (an unbuilt building = no-op)
        // =====================================================================
        private static void Case5_DefaultIsIdentity(List<string> failures)
        {
            var fresh = new GameModifiers();
            foreach (var f in ModifierFields())
            {
                object v = f.GetValue(fresh);
                bool isMult = f.Name.EndsWith("Mult", StringComparison.Ordinal);

                if (f.FieldType == typeof(float))
                {
                    float fv = (float)v;
                    float want = isMult ? 1f : 0f;
                    if (Math.Abs(fv - want) > 0.0001f)
                        failures.Add("[identity] '" + JsonNameOf(f) + "' defaults to " + Fmt(fv) + " but a " +
                                     (isMult ? "multiplier" : "bonus") + " must default to " + Fmt(want) +
                                     " - a non-identity default means an UNBUILT building already changes the game");
                }
                else if (f.FieldType == typeof(int))
                {
                    if ((int)v != (isMult ? 1 : 0))
                        failures.Add("[identity] '" + JsonNameOf(f) + "' defaults to " + v + " instead of " +
                                     (isMult ? 1 : 0) + " - an unbuilt building must be a no-op");
                }
                else if (f.FieldType == typeof(bool))
                {
                    if ((bool)v)
                        failures.Add("[identity] flag '" + JsonNameOf(f) + "' defaults to TRUE - a tier-4 signature " +
                                     "ability would be live before the player ever built the building");
                }
                else if (f.FieldType == typeof(string))
                {
                    if (!string.IsNullOrEmpty((string)v))
                        failures.Add("[identity] string '" + JsonNameOf(f) + "' defaults to '" + v + "' instead of " +
                                     "empty - a default-granted unlock is content the player never earned");
                }
            }

            // GameModifiers.None is the shared no-op every consumer falls back to.
            if (GameModifiers.None == null)
                failures.Add("[identity] GameModifiers.None is null - every '?? GameModifiers.None' fallback in the " +
                             "codebase would NRE instead of degrading to identity");
        }

        // =====================================================================
        //  CASE 6 - every authored unlockSpell id is a real ability
        // =====================================================================
        private static void Case6_UnlockSpellResolves(List<string> failures, List<string> notes)
        {
            foreach (var pair in new[]
                     {
                         new[] { TiersRes, AbilitiesRes },
                         new[] { TiersSA, AbilitiesSA },
                     })
            {
                string tiersPath = pair[0], abilitiesPath = pair[1];
                var tiers = ReadJson(tiersPath, "unlock-resolves", failures);
                var abilities = ReadJson(abilitiesPath, "unlock-resolves", failures);
                if (tiers == null || abilities == null) continue;

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectIds(abilities, ids);
                if (ids.Count == 0)
                {
                    failures.Add("[unlock-resolves] no ability 'id' fields found in " + CopyLabel(abilitiesPath) +
                                 " - the resolve check would be vacuously green");
                    continue;
                }

                int checkedCount = 0;
                foreach (var block in FindModifierBlocks(tiers))
                {
                    var tok = block["unlockSpell"];
                    if (tok == null) continue;
                    string csv = (string)tok;
                    if (string.IsNullOrWhiteSpace(csv)) continue;
                    foreach (var raw in csv.Split(','))
                    {
                        string id = raw.Trim();
                        if (id.Length == 0) continue;
                        checkedCount++;
                        if (!ids.Contains(id))
                            failures.Add("[unlock-resolves] unlockSpell names '" + id + "' in " + CopyLabel(tiersPath) +
                                         " but no ability with that id exists in abilities.json - the tier reads " +
                                         "'Learn <spell>' on the upgrade panel and unlocks nothing");
                    }
                }
                notes.Add(CopyLabel(tiersPath) + " unlockSpell ids checked=" + checkedCount);
            }
        }

        private static void CollectIds(JToken node, HashSet<string> ids)
        {
            if (node == null) return;
            var obj = node as JObject;
            if (obj != null)
            {
                foreach (var p in obj.Properties())
                {
                    if (string.Equals(p.Name, "id", StringComparison.OrdinalIgnoreCase) &&
                        p.Value != null && p.Value.Type == JTokenType.String)
                    {
                        string s = (string)p.Value;
                        if (!string.IsNullOrWhiteSpace(s)) ids.Add(s.Trim());
                    }
                    CollectIds(p.Value, ids);
                }
                return;
            }
            var arr = node as JArray;
            if (arr != null)
            {
                foreach (var c in arr) CollectIds(c, ids);
            }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private static JToken ReadJson(string path, string caseName, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[" + caseName + "] " + path + " not found");
                return null;
            }
            try { return JToken.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("[" + caseName + "] " + path + " failed to parse (" + ex.GetType().Name + ": " +
                             ex.Message + ")");
                return null;
            }
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

        private static string CopyLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "<unknown>";
            if (path.IndexOf("StreamingAssets", StringComparison.OrdinalIgnoreCase) >= 0) return "StreamingAssets/library";
            if (path.IndexOf("Resources", StringComparison.OrdinalIgnoreCase) >= 0) return "Resources/curated";
            return path;
        }

        private static string Show(object v)
        {
            if (v == null) return "<null>";
            if (v is float) return Fmt((float)v);
            return v.ToString();
        }

        private static string Fmt(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
