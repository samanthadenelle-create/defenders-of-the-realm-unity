// =============================================================================
// HUD - BugReportVM payload-builder tests (EditMode) - WO-846
// -----------------------------------------------------------------------------
// The player bug-report payload must carry the wallet/account-bound playerId and
// a BOUNDED context tail into api/bug-report.js (contract: note, sceneName,
// sessionId, version, platform, piUid?, playerId?, traceTail[], screenshotB64?).
// BugReportVM.BuildPayloadJson is the PURE assembly point (no Unity/service
// reads), factored exactly so this fixture can prove, without a scene:
//   * every contract field lands under its exact key;
//   * playerId / piUid are OMITTED (not null-valued) when absent;
//   * traceTail is bounded to the endpoint caps (MaxTailLines newest kept,
//     oldest truncated first; per-line clamp MaxTailLineChars) - mirrors of
//     api/bug-report.js MAX_TAIL_LINES / MAX_TAIL_CHARS;
//   * JSON string escaping holds for quotes / newlines / control chars;
//   * null inputs degrade to defaults - the builder never throws.
// String assertions on the emitted JSON (we own the deterministic emitter);
// deliberately no JSON-parser dependency so this test assembly cannot drag a
// precompiled-reference resolution problem into the compile gate.
// =============================================================================

using System;
using NUnit.Framework;
using DeNelle.HUD;

namespace DeNelle.HUD.Tests
{
    [TestFixture]
    public class BugReportPayloadTest
    {
        // Convenience: build with sane defaults, override what the case probes.
        private static string Build(
            string note = "it broke",
            string scene = "OuterWorld",
            string session = "br-test0001",
            string version = "0.9.1",
            string platform = "Android",
            string piUid = null,
            string playerId = null,
            string[] tail = null,
            string shotB64 = null)
            => BugReportVM.BuildPayloadJson(note, scene, session, version, platform,
                                            piUid, playerId, tail, shotB64);

        // =====================================================================
        //  Contract fields
        // =====================================================================

        [Test]
        public void all_core_fields_land_under_their_contract_keys()
        {
            string json = Build(tail: new[] { "[Flow:BugReport] line one" });
            StringAssert.Contains("\"note\":\"it broke\"", json);
            StringAssert.Contains("\"sceneName\":\"OuterWorld\"", json);
            StringAssert.Contains("\"sessionId\":\"br-test0001\"", json);
            StringAssert.Contains("\"version\":\"0.9.1\"", json);
            StringAssert.Contains("\"platform\":\"Android\"", json);
            StringAssert.Contains("\"traceTail\":[\"[Flow:BugReport] line one\"]", json);
            Assert.That(json, Does.StartWith("{").And.EndWith("}"));
        }

        [Test]
        public void player_id_rides_the_payload_when_bound()
        {
            string json = Build(playerId: "guest-local-abc123def456");
            StringAssert.Contains("\"playerId\":\"guest-local-abc123def456\"", json,
                "WO-846: the bound identity save key must land under playerId " +
                "(api/bug-report.js stores piUid ?? playerId into player_id).");
        }

        [Test]
        public void player_id_key_is_omitted_when_absent()
        {
            Assert.That(Build(playerId: null), Does.Not.Contain("playerId"),
                "a null id must omit the key entirely, not send null.");
            Assert.That(Build(playerId: ""), Does.Not.Contain("playerId"),
                "an empty id must omit the key entirely.");
        }

        [Test]
        public void pi_hash_and_player_id_can_coexist()
        {
            // Pi builds: the salted hash wins server-side (piUid ?? playerId) -
            // both may ride; the client sends what it has.
            string json = Build(piUid: "a1b2c3", playerId: "walletXYZ");
            StringAssert.Contains("\"piUid\":\"a1b2c3\"", json);
            StringAssert.Contains("\"playerId\":\"walletXYZ\"", json);
        }

        [Test]
        public void screenshot_is_included_when_given_and_omitted_when_null()
        {
            StringAssert.Contains("\"screenshotB64\":\"QUJD\"", Build(shotB64: "QUJD"));
            Assert.That(Build(shotB64: null), Does.Not.Contain("screenshotB64"));
        }

        // =====================================================================
        //  Context tail bounds (endpoint cap mirrors)
        // =====================================================================

        [Test]
        public void tail_is_bounded_to_the_newest_MaxTailLines_dropping_oldest_first()
        {
            var lines = new string[300];
            for (int i = 0; i < lines.Length; i++) lines[i] = $"line-{i:000}";
            string json = Build(tail: lines);

            int firstKept = 300 - BugReportVM.MaxTailLines;   // 180 with the 120-line cap
            Assert.That(json, Does.Not.Contain($"line-{firstKept - 1:000}"),
                "lines older than the cap must be truncated (oldest first).");
            StringAssert.Contains($"line-{firstKept:000}", json);
            StringAssert.Contains("line-299", json);

            int kept = CountOccurrences(json, "line-");
            Assert.That(kept, Is.EqualTo(BugReportVM.MaxTailLines),
                "exactly MaxTailLines newest lines must survive.");

            // Order preserved oldest-first (the endpoint contract).
            Assert.That(json.IndexOf($"line-{firstKept:000}", StringComparison.Ordinal),
                Is.LessThan(json.IndexOf("line-299", StringComparison.Ordinal)));
        }

        [Test]
        public void tail_lines_are_clamped_to_MaxTailLineChars()
        {
            string huge = new string('x', 2000);
            string json = Build(tail: new[] { huge });
            StringAssert.Contains(new string('x', BugReportVM.MaxTailLineChars), json);
            Assert.That(json, Does.Not.Contain(new string('x', BugReportVM.MaxTailLineChars + 1)),
                "a tail line must be clamped to the endpoint's per-line cap.");
        }

        [Test]
        public void endpoint_cap_mirrors_match_api_bug_report_js()
        {
            // api/bug-report.js: MAX_TAIL_LINES = 120, MAX_TAIL_CHARS = 500.
            // If these drift, the client either over-sends (server re-truncates,
            // wasted bytes) or under-sends (lost context) - fail loudly here.
            Assert.That(BugReportVM.MaxTailLines, Is.EqualTo(120));
            Assert.That(BugReportVM.MaxTailLineChars, Is.EqualTo(500));
        }

        // =====================================================================
        //  Robustness
        // =====================================================================

        [Test]
        public void note_is_json_escaped()
        {
            string json = Build(note: "quote \" back \\ line\nbreak\ttab");
            StringAssert.Contains("quote \\\" back \\\\ line\\nbreak\\ttab", json);
        }

        [Test]
        public void all_null_inputs_still_build_a_valid_shell()
        {
            string json = BugReportVM.BuildPayloadJson(
                null, null, null, null, null, null, null, null, null);
            Assert.That(json, Does.StartWith("{").And.EndWith("}"));
            StringAssert.Contains("\"note\":\"\"", json);
            StringAssert.Contains("\"sceneName\":\"?\"", json);
            StringAssert.Contains("\"traceTail\":[]", json);
            Assert.That(json, Does.Not.Contain("playerId"));
            Assert.That(json, Does.Not.Contain("piUid"));
            Assert.That(json, Does.Not.Contain("screenshotB64"));
        }

        [Test]
        public void null_tail_entries_degrade_to_empty_strings()
        {
            string json = Build(tail: new string[] { null, "ok" });
            StringAssert.Contains("\"traceTail\":[\"\",\"ok\"]", json);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
