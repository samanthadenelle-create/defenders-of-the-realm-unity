// =============================================================================
// Canonical data — JSON integrity tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-XC-09 / TC-XC-10 and bug-log BUG-013.
//
// Every file under StreamingAssets/Data/Canonical/ must:
//   - exist and be non-empty,
//   - parse as well-formed JSON (Newtonsoft round-trip),
//   - carry NO stray agent-output markup (</content>, </invoke>, </antml...>) —
//     background subagents have leaked these into data files before
//     (MEMORY.md "agent file-output pitfalls"; BUG-013 stripped them from
//     packs.json). This test is the standing automated re-scan.
//
// This fixture reads the raw files directly — it does NOT go through the typed
// catalogs — so it catches corruption a typed loader would silently tolerate.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DeNelle.Data.Tests
{
    [TestFixture]
    public class CanonicalJsonIntegrityTest
    {
        /// <summary>StreamingAssets-relative folder holding every canonical JSON file.</summary>
        private const string CanonicalDir = "Data/Canonical";

        /// <summary>
        /// Stray agent-output tokens. If a background subagent's Write call leaked
        /// closing markup into a data file, one of these substrings will appear.
        /// Deliberately specific — a bare "&lt;" would false-positive on any
        /// legitimate prose; these are the exact tag shapes the harness emits.
        /// </summary>
        private static readonly string[] StrayMarkupTokens =
        {
            "</content>",
            "</invoke>",
            "</antml",
            "<function_calls>",
            "<parameter name=",
            "<invoke name=",
        };

        /// <summary>The canonical files the data layer is required to ship (TC-XC-09).</summary>
        private static readonly string[] RequiredFiles =
        {
            "abilities.json",
            "buildings.json",
            "canon-strings.json",
            "en.json",
            "enemies.json",
            "lore-fragments.json",
            "packs.json",
            "pets.json",
            "realm-map.json",
            "themes.json",
            "wallets.json",
            "waves.json",
        };

        private static string CanonicalRoot =>
            Path.Combine(Application.streamingAssetsPath, CanonicalDir);

        /// <summary>Every *.json under the canonical tree (recurses into dungeons/).</summary>
        private static IEnumerable<string> AllCanonicalJsonFiles()
        {
            var root = CanonicalRoot;
            if (!Directory.Exists(root))
                Assert.Fail($"Canonical data directory missing: {root}");
            return Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        }

        // =====================================================================
        //  Presence
        // =====================================================================

        [Test]
        public void every_required_canonical_file_exists_and_is_non_empty()
        {
            foreach (var file in RequiredFiles)
            {
                var full = Path.Combine(CanonicalRoot, file);
                Assert.That(File.Exists(full), Is.True, $"required canonical file missing: {file}");
                Assert.That(new FileInfo(full).Length, Is.GreaterThan(0),
                    $"canonical file is empty: {file}");
            }
        }

        // =====================================================================
        //  Well-formed JSON
        // =====================================================================

        [Test]
        public void every_canonical_json_file_parses_as_well_formed_json()
        {
            foreach (var path in AllCanonicalJsonFiles())
            {
                var text = File.ReadAllText(path);
                var name = Path.GetFileName(path);
                Assert.That(() => JToken.Parse(text), Throws.Nothing,
                    $"{name} is not well-formed JSON.");
            }
        }

        // =====================================================================
        //  No stray agent-output markup (BUG-013 / TC-XC-10)
        // =====================================================================

        [Test]
        public void no_canonical_json_file_contains_stray_agent_markup()
        {
            foreach (var path in AllCanonicalJsonFiles())
            {
                var text = File.ReadAllText(path);
                var name = Path.GetFileName(path);
                foreach (var token in StrayMarkupTokens)
                {
                    Assert.That(text.Contains(token), Is.False,
                        $"BUG-013 regression: stray markup '{token}' found in {name}.");
                }
            }
        }

        [Test]
        public void no_canonical_json_file_ends_with_trailing_markup()
        {
            // BUG-013's leak landed at the END of packs.json — check the tail
            // explicitly: the last non-whitespace char must close an object/array.
            foreach (var path in AllCanonicalJsonFiles())
            {
                var trimmed = File.ReadAllText(path).TrimEnd();
                var name = Path.GetFileName(path);
                Assert.That(trimmed.Length, Is.GreaterThan(0), $"{name} is whitespace-only.");
                char last = trimmed[trimmed.Length - 1];
                Assert.That(last == '}' || last == ']', Is.True,
                    $"{name} does not end with '}}' or ']' (last char '{last}') — possible trailing junk.");
            }
        }

        // =====================================================================
        //  Schema sanity — every file carries a numeric version
        // =====================================================================

        [Test]
        public void every_versioned_catalog_has_a_positive_integer_version()
        {
            // The typed catalogs (buildings/packs/pets/abilities/waves/enemies)
            // all carry a top-level "version". A missing/zero version means a
            // hand-edit dropped it.
            string[] versioned =
            {
                "abilities.json", "buildings.json", "enemies.json",
                "packs.json", "pets.json", "waves.json",
            };
            foreach (var file in versioned)
            {
                var token = JToken.Parse(File.ReadAllText(Path.Combine(CanonicalRoot, file)));
                var version = token["version"];
                Assert.That(version, Is.Not.Null, $"{file} has no top-level 'version'.");
                Assert.That(version.Type, Is.EqualTo(JTokenType.Integer), $"{file} version is not an int.");
                Assert.That((int)version, Is.GreaterThan(0), $"{file} version must be >= 1.");
            }
        }
    }
}
