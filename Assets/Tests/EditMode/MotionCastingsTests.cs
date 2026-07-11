// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// MotionCastingsTests (EditMode) — WO-670 slice 1 permission-gate suite.
// -----------------------------------------------------------------------------
// Canon: docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md §6. Guards the Action
// Keyword Registry (motion-castings.json + MotionCastings interpreter):
//   #1 schema/vocabulary sync — the file parses; every target row keyword is in
//      the closed vocabulary; the json vocabulary == ActionKeywords constants
//      (one source, two views); inherits chains are acyclic and ≤3 hops.
//   #3 manual-row preservation — WriteRow over a manual:true row refuses and
//      leaves the file byte-identical (Offset Forge law, WO-490).
//   #4 fallback chain — a child target resolves through its parent's row; a
//      total registry miss returns the builder default AND self-reports via
//      LogWarning (the never-silent gate).
//   #5 cast-keyword lint — no cast/castChannel row binds an attack-taxonomy
//      clip (atk_* / *Slash*) — the melee/caster rule as a test.
//
// #2 (empty-registry controller-hash parity) needs an actual controller BAKE
// (file absent vs {} → serialized .controller hash identical) — that belongs in
// the DataRegression harness (Assets/Editor/Regression), marker
// MOTIONCAST_PARITY_OK/_FAIL. Follow-up, not an EditMode test here.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DeNelle.Core.Combat;
using DeNelle.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class MotionCastingsTests
    {
        private const string FixtureClipAssetPath = "Assets/Tests/EditMode/__MotionCastingsTestClip.anim";
        private static readonly string[] RowMetaKeys = { "_comment", "inherits" };

        private string _fixtureJsonPath;
        private AnimationClip _fixtureClip;

        [OneTimeSetUp]
        public void CreateFixtureClip()
        {
            _fixtureClip = new AnimationClip { name = "__MotionCastingsTestClip" };
            AssetDatabase.CreateAsset(_fixtureClip, FixtureClipAssetPath);
        }

        [OneTimeTearDown]
        public void DeleteFixtureClip()
        {
            AssetDatabase.DeleteAsset(FixtureClipAssetPath);
        }

        [TearDown]
        public void RestoreRegistry()
        {
            // ALWAYS restore the production path — other fixtures share the static.
            MotionCastings.RegistryPath = MotionCastings.DefaultRegistryPath;
            MotionCastings.Reload();
            if (!string.IsNullOrEmpty(_fixtureJsonPath) && File.Exists(_fixtureJsonPath))
                File.Delete(_fixtureJsonPath);
            _fixtureJsonPath = null;
        }

        private static JObject ParseRealRegistry()
        {
            string path = MotionCastings.DefaultRegistryPath; // project-relative, editor CWD = project root
            Assert.That(File.Exists(path), Is.True, $"missing registry file: {path}");
            return JObject.Parse(File.ReadAllText(path)); // throws on malformed JSON -> test fails
        }

        private static HashSet<string> FlattenVocabulary(JObject root)
        {
            var vocab = new HashSet<string>();
            var block = root["vocabulary"] as JObject;
            Assert.That(block, Is.Not.Null, "registry needs a 'vocabulary' block");
            foreach (var cat in block.Properties())
                foreach (var kw in (JArray)cat.Value)
                    vocab.Add((string)kw);
            return vocab;
        }

        private string WriteFixture(string json)
        {
            _fixtureJsonPath = Path.Combine(Path.GetTempPath(),
                "motion-castings-fixture-" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_fixtureJsonPath, json);
            MotionCastings.RegistryPath = _fixtureJsonPath;
            MotionCastings.Reload();
            return _fixtureJsonPath;
        }

        /// <summary>Minimal fixture json carrying the real closed vocabulary block.</summary>
        private static string VocabularyJson()
        {
            var vocab = new JObject
            {
                ["locomotion"] = new JArray(ActionKeywords.LocomotionKeywords),
                ["attack"]     = new JArray(ActionKeywords.AttackKeywords),
                ["cast"]       = new JArray(ActionKeywords.CastKeywords),
                ["reaction"]   = new JArray(ActionKeywords.ReactionKeywords),
                ["death"]      = new JArray(ActionKeywords.DeathKeywords),
                ["signature"]  = new JArray(ActionKeywords.SignatureKeywords),
            };
            return vocab.ToString();
        }

        // ── #1 schema / vocabulary sync ──────────────────────────────────────

        [Test]
        public void registry_parses_and_vocabulary_matches_action_keywords()
        {
            var root = ParseRealRegistry();
            var vocab = FlattenVocabulary(root);
            var consts = new HashSet<string>(ActionKeywords.All);

            Assert.That(vocab.SetEquals(consts), Is.True,
                "json vocabulary and ActionKeywords consts must be the SAME closed set — " +
                $"json-only: [{string.Join(", ", Except(vocab, consts))}], " +
                $"consts-only: [{string.Join(", ", Except(consts, vocab))}]");
        }

        [Test]
        public void every_target_row_keyword_is_in_the_vocabulary()
        {
            var root = ParseRealRegistry();
            var vocab = FlattenVocabulary(root);
            var targets = root["targets"] as JObject;
            Assert.That(targets, Is.Not.Null, "registry needs a 'targets' block");

            foreach (var target in targets.Properties())
            {
                var targetObj = target.Value as JObject;
                Assert.That(targetObj, Is.Not.Null, $"target '{target.Name}' must be an object");
                foreach (var row in targetObj.Properties())
                {
                    if (System.Array.IndexOf(RowMetaKeys, row.Name) >= 0) continue;
                    Assert.That(vocab.Contains(row.Name), Is.True,
                        $"'{target.Name}.{row.Name}' is not in the closed vocabulary — " +
                        "open strings are the VFX-two-stack scar in data form (§2).");
                }
            }
        }

        [Test]
        public void inherits_chains_are_acyclic_and_within_max_depth()
        {
            var root = ParseRealRegistry();
            var targets = (JObject)root["targets"];
            var inherits = new Dictionary<string, string>();
            foreach (var target in targets.Properties())
                if (target.Value is JObject obj && obj["inherits"] != null)
                    inherits[target.Name] = (string)obj["inherits"];

            foreach (var target in targets.Properties())
            {
                var seen = new List<string> { target.Name };
                string current = target.Name;
                while (inherits.TryGetValue(current, out string parent))
                {
                    Assert.That(seen.Contains(parent), Is.False,
                        $"inherits CYCLE: {string.Join(" -> ", seen)} -> {parent}");
                    seen.Add(parent);
                    Assert.That(seen.Count - 1, Is.LessThanOrEqualTo(3),
                        $"inherits chain from '{target.Name}' exceeds max depth 3: {string.Join(" -> ", seen)}");
                    Assert.That(targets[parent], Is.Not.Null,
                        $"'{current}' inherits unknown target '{parent}'");
                    current = parent;
                }
            }
        }

        // ── #3 manual-row preservation (Offset Forge law) ────────────────────

        [Test]
        public void writerow_never_overwrites_a_manual_row()
        {
            string fixture = WriteFixture(
                "{\n" +
                "  \"version\": 1,\n" +
                "  \"vocabulary\": " + VocabularyJson() + ",\n" +
                "  \"targets\": {\n" +
                "    \"humanoid\": {},\n" +
                "    \"orc\": {\n" +
                "      \"inherits\": \"humanoid\",\n" +
                "      \"attack0\": {\n" +
                "        \"clip\": \"" + FixtureClipAssetPath + "\",\n" +
                "        \"guid\": \"\", \"vfxKey\": \"\", \"sfxId\": \"\",\n" +
                "        \"manual\": true,\n" +
                "        \"pickedUtc\": \"2026-07-11T00:00:00Z\",\n" +
                "        \"source\": \"motion-caster\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}\n");
            byte[] before = File.ReadAllBytes(fixture);

            LogAssert.Expect(LogType.Warning, new Regex(@"WriteRow refused.*'orc\.attack0'.*CANON"));
            bool wrote = MotionCastings.WriteRow("orc", "attack0", new CastingRow
            {
                clip = "Assets/Somewhere/Else.anim",
                manual = false,
                source = "auto",
            });

            Assert.That(wrote, Is.False, "auto pass must NOT overwrite a manual:true row (§8)");
            Assert.That(File.ReadAllBytes(fixture), Is.EqualTo(before),
                "a refused WriteRow must leave the file BYTE-IDENTICAL");
        }

        // ── #4 fallback chain ────────────────────────────────────────────────

        [Test]
        public void child_target_resolves_through_parent_row()
        {
            WriteFixture(
                "{\n" +
                "  \"version\": 1,\n" +
                "  \"vocabulary\": " + VocabularyJson() + ",\n" +
                "  \"targets\": {\n" +
                "    \"humanoid\": {},\n" +
                "    \"orc\": {\n" +
                "      \"inherits\": \"humanoid\",\n" +
                "      \"attack0\": { \"clip\": \"" + FixtureClipAssetPath + "\", \"manual\": true }\n" +
                "    },\n" +
                "    \"orc-berserker\": { \"inherits\": \"orc\" }\n" +
                "  }\n" +
                "}\n");

            // orc-berserker has NO attack0 of its own — must inherit orc's row.
            var resolved = MotionCastings.Resolve("orc-berserker", ActionKeywords.Attack0, null);
            Assert.That(resolved, Is.SameAs(_fixtureClip),
                "child target must resolve the parent family's row through the inherits chain");
        }

        [Test]
        public void total_miss_returns_builder_default_and_warns()
        {
            WriteFixture(
                "{\n" +
                "  \"version\": 1,\n" +
                "  \"vocabulary\": " + VocabularyJson() + ",\n" +
                "  \"targets\": {\n" +
                "    \"humanoid\": {},\n" +
                "    \"orc\": { \"inherits\": \"humanoid\" },\n" +
                "    \"orc-berserker\": { \"inherits\": \"orc\" }\n" +
                "  }\n" +
                "}\n");

            var builderDefault = _fixtureClip;
            // The never-silent gate: the miss MUST self-report.
            LogAssert.Expect(LogType.Warning, new Regex(@"\[MotionCasting\] miss 'orc-berserker\.cast'"));
            var resolved = MotionCastings.Resolve("orc-berserker", ActionKeywords.Cast, builderDefault);
            Assert.That(resolved, Is.SameAs(builderDefault),
                "a registry-exhausted miss must return the calling builder's hardcoded default");
        }

        // ── #5 cast-keyword lint (melee/caster rule as a test) ───────────────

        [Test]
        public void no_cast_keyword_row_binds_an_attack_taxonomy_clip()
        {
            var root = ParseRealRegistry();
            var targets = (JObject)root["targets"];
            var attackTaxonomy = new Regex(@"(^|[/\\])atk_|slash", RegexOptions.IgnoreCase);

            foreach (var target in targets.Properties())
            {
                if (!(target.Value is JObject targetObj)) continue;
                foreach (string castKeyword in ActionKeywords.CastKeywords)
                {
                    if (!(targetObj[castKeyword] is JObject row)) continue;
                    string clip = (string)row["clip"] ?? string.Empty;
                    Assert.That(attackTaxonomy.IsMatch(clip), Is.False,
                        $"'{target.Name}.{castKeyword}' binds attack-taxonomy clip '{clip}' — " +
                        "cast-type actions fire CAST clips, never swings (Knight_Anim_Inventory rule, §2).");
                }
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static IEnumerable<string> Except(HashSet<string> a, HashSet<string> b)
        {
            foreach (string s in a)
                if (!b.Contains(s))
                    yield return s;
        }
    }
}
