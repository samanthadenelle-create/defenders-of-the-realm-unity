// =============================================================================
// SyncDrainReasonRegression — WO-1587: a cloud-save failure must NAME ITS OWN
// CAUSE, and the save body must carry the schemaVersion the server demands.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core). Pure data + pure
// functions — no scene loads, no network, no PlayerPrefs.
//
// THE DEFECT THIS PINS (measured, not inferred). Owner's Seeker, build
// 2026.09.07.359076, `adb logcat -d -s Unity`, read 2026-09-07:
//
//   09-07 07:49:07.270  pid 27292  [Sync] Saved 21787 bytes (full snapshot).
//   09-07 07:53:27.721  pid 27292  [Sync] Save request threw (400): HTTP/1.1 400 Bad Request
//   09-07 07:53:27.721  pid 27292  {"ok":false,"code":"SCHEMA_VERSION_MISSING","ref":"a6e43bfc"}
//
// The SAME process saved fine and then 400'd four minutes later: the client did
// not change, the server did. api/game/save.js `judgeSchemaVersion` stopped
// defaulting an absent version ("an absent version is a malformed payload, not a
// v10 payload") and the client had never sent one — PersistedState carries no
// version property, the version lives on the SaveFile envelope the cloud POST
// does not use, and the one place SchemaVersion was ever set (BuildDeltaPayload)
// builds an object that is never posted. Six offline-queue drains then failed in
// a row (08:01:37 / 08:11:58 / 08:15:31 / 08:18:35 / 08:20:10 / 08:23:29), each
// one millisecond after its own 400.
//
// ⛔ THAT REFUSAL IS RETIRED — DO NOT PIN IT (server, same day; read at source
// api/game/save.js:106-171 on 2026-09-07). Refusing an absent version took cloud
// save down for EVERY client in the field, because the shipped build omits the
// field and the client-side fix only reaches players with the NEXT build. The
// corrected ruling: absent/unparseable = UNKNOWN — `ok:true, version:null,
// note:'SCHEMA_VERSION_ABSENT'`, the stored column left untouched. DOWNGRADE still
// refuses, and GREATEST() still clamps.
//   * The CLIENT FIX STANDS AND IS STILL WHAT MATTERS. A tolerated omission means
//     the row's schema_version is never updated, so a row carrying the invented 10
//     (api/game/save.js:91-94) stays at 10 while holding v-current state, and
//     ApplyBackendState trusts that column on LOAD. Declaring the version heals the
//     row; relying on the server's tolerance does not.
//   * The 400 body is kept below as a HISTORICAL FIXTURE for the category mechanic
//     ("a 4xx carrying code X reads why=http-4xx and names X"), never as the
//     server's current answer.
//
// AND THE READER WAS SENT TO THE WRONG SYSTEM. The drain warning ended "Check the
// [Flow:Wallet] why= line for the identity reason" — a line only the mint path
// ever prints, naming a system that owned nothing here (the wallet session minted
// at 08:01:26 and renewed with no prompt at 08:15:31, 200 ms before failure #3).
// The real cause WAS logged, in an untagged Debug.LogWarning that no [Flow:*]
// filter and no F8 harvest ever sees.
//
// SO THREE THINGS ARE PINNED, and none of them by prose:
//   A  the wire body carries playerId AND an integer schemaVersion > 0 equal to
//      SaveSchema.CurrentVersion — asserted through a local copy of the SERVER's
//      own PARSE step, so the two ends cannot drift apart silently again. The
//      assertion is deliberately STRICTER than the server's current tolerance:
//      the server accepts an absent version, this suite does not.
//   B  EVERY failure category yields a non-empty, distinct why= token — a drain
//      failure can never again be reported without a cause.
//   C  ONLY the auth-absent category names [Flow:Wallet]. A payload refusal, a
//      transport drop and a serialize failure must not blame identity.
//   D  the drain's failure line is FORMATTED FROM THE ATTEMPT (source lint), so a
//      future edit cannot re-hardcode a fixed pointer back in.
//
// RED PROOF (reasoned; this lane does not run Unity): against the pre-fix tree,
// case A fails on its first assertion — BuildSaveBody did not exist and the inline
// body build set only `jo["playerId"]`, so the serialized object has no
// schemaVersion key at all and the parse mirror returns ABSENT, not DECLARED. Cases
// B/C fail to compile-as-written because SaveAttemptCategory did not exist; case D
// fails on the literal, which read "[Flow:Wallet] why=" inside FlushOfflineQueue.
//
// Markers: SYNC_DRAIN_REASON_OK / SYNC_DRAIN_REASON_FAIL (FAIL via Debug.LogError
// so it lands in break-log.jsonl). Entry: SyncDrainReasonRegression.Run(out reason).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class SyncDrainReasonRegression
    {
        private const string StatePath = "Assets/_Modules/Core/State/GameStateService.cs";

        /// <summary>A real base58 pubkey — a hyphenated fixture is retired on save by the
        /// 2026-08-02 identity work, so it could never stand in for a bound identity here.</summary>
        private const string Wallet = "BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SYNC_DRAIN_REASON_OK - " + reason);
            else Debug.LogError("SYNC_DRAIN_REASON_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SYNC DRAIN REASON (WO-1587 schemaVersion on the wire + a named cause on every failure) ---");

            // COUNT WHAT RAN, never a literal: a case count typed into the pass line is a
            // label, not a measurement (audit G8 / WO-1493 — the same reason the gate marker
            // derives its denominator).
            int cases = 0;
            try
            {
                CheckWireBodyCarriesSchemaVersion(failures, log);  cases++; // A
                CheckEveryCategoryNamesACause(failures, log);      cases++; // B
                CheckOnlyAuthAbsentBlamesTheWallet(failures, log); cases++; // C
                CheckDrainLineIsFormattedFromTheAttempt(failures, log); cases++; // D
                CheckDetailStaysOneLine(failures, log);            cases++; // E
            }
            catch (Exception ex)
            {
                failures.Add($"suite threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = cases + " case(s) passed (wire schemaVersion, cause on every category, wallet named only for auth-absent, drain line formatted from the attempt, one-line detail)";
                Debug.Log(log.ToString() + "SYNC_DRAIN_REASON_OK - " + reason);
                return true;
            }

            reason = string.Join(" | ", failures);
            Debug.LogError(log.ToString() + "SYNC_DRAIN_REASON_FAIL - " + reason);
            return false;
        }

        // ── A — the body the server judges ───────────────────────────────────────

        private static void CheckWireBodyCarriesSchemaVersion(List<string> failures, StringBuilder log)
        {
            var snapshot = new SaveSchema.PersistedState
            {
                BestWave = 12,
                Wood     = 250,
                Iron     = 90,
                // A deliberately-null domain: the builder strips nulls, and the strip must not
                // take schemaVersion with it.
                Resources = null,
                BoundWallet = Wallet,
            };

            byte[] body = GameStateService.BuildSaveBody(snapshot, Wallet);
            if (body == null || body.Length == 0)
            {
                failures.Add("[wire] BuildSaveBody produced no bytes");
                return;
            }

            var jo = JObject.Parse(Encoding.UTF8.GetString(body));
            log.AppendLine($"  [wire] body={body.Length} bytes, {jo.Count} top-level keys");

            var playerId = jo["playerId"];
            if (playerId == null || playerId.Type == JTokenType.Null || string.IsNullOrEmpty(playerId.ToString()))
                failures.Add("[wire] the POST body has no playerId - api/game/save.js answers PLAYER_ID_MISSING (400)");

            if (jo["resources"] != null)
                failures.Add("[wire] a null domain was NOT stripped - a partial sync would null-out a server value");

            // The server accepts either casing; the client writes camelCase. Judge exactly the
            // way api/game/save.js does, so this suite fails the moment the two ends disagree.
            //
            // ⚠ THE ABSENT ARM NO LONGER REFUSES (server, 2026-09-07, read at
            // api/game/save.js:156-171): an absent/unparseable version returns
            // `ok:true, version:null, note:'SCHEMA_VERSION_ABSENT'` and leaves the stored
            // column UNTOUCHED. So "the save is dropped" is no longer what this case is
            // defending against — but the client must STILL declare the version, and this
            // assertion is STRONGER than the server's tolerance on purpose:
            //   an absent version means the row's schema_version is never updated, so a row
            //   still carrying the invented 10 (api/game/save.js:91-94 - the old default
            //   stamped rows back to 10 while writing current-shaped state) stays at 10
            //   forever. GameStateService.ApplyBackendState trusts that column, so the LOAD
            //   path then runs the wrong migration chain over v-current data. Declaring the
            //   version is what heals the row; leaning on the server's tolerance is not.
            var judged = JudgeSchemaVersionLikeTheServer(jo["SchemaVersion"], jo["schemaVersion"]);
            log.AppendLine($"  [wire] schemaVersion={jo["schemaVersion"]} -> server judgement '{judged}'");
            if (judged != "DECLARED")
                failures.Add($"[wire] api/game/save.js would judge this body '{judged}' - the client must SEND an " +
                             "integer schemaVersion. The server now tolerates its absence (200, " +
                             "note SCHEMA_VERSION_ABSENT) instead of the 400 SCHEMA_VERSION_MISSING that took " +
                             "cloud save down on 2026-09-07, but a tolerated omission leaves the row's stored " +
                             "version frozen at whatever it already held - the client, not the server, is what " +
                             "makes it correct");

            int declared = jo["schemaVersion"] != null ? jo["schemaVersion"].Value<int>() : -1;
            if (declared != SaveSchema.CurrentVersion)
                failures.Add($"[wire] the body declares schemaVersion {declared} but SaveSchema.CurrentVersion is " +
                             $"{SaveSchema.CurrentVersion} - the wire version must come from the ONE authority, " +
                             "never a literal (CLAUDE.md s8: no copied version numbers)");
        }

        /// <summary>
        /// A local mirror of api/game/save.js <c>judgeSchemaVersion</c>'s PARSE step, kept
        /// deliberately verbatim (read at source 2026-09-07, <c>api/game/save.js:156-171</c>):
        /// "const v = typeof incoming === 'number' ? incoming : (string and non-blank ?
        /// Number(incoming) : NaN); if (!Number.isInteger(v) || v &lt;= 0) → ok:true,
        /// version:null, note:'SCHEMA_VERSION_ABSENT'".
        /// <para>
        /// ⛔ <c>SCHEMA_VERSION_MISSING</c> IS RETIRED AS AN OUTCOME. Nothing returns it any
        /// more: the refusal took cloud save down for every client in the field on 2026-09-07,
        /// because the shipped build omits the field and the fix only reaches players with the
        /// NEXT build. The constant survives on the server side purely so the two sides'
        /// disagreement stays visible — do not re-add it as an expected answer here.
        /// </para>
        /// Returns <c>DECLARED</c> (an integer &gt; 0 was sent — the write updates the stored
        /// column) or <c>ABSENT</c> (accepted, but the stored column is left untouched). The
        /// DOWNGRADE arm needs the stored row, is not reproducible headless, and is deliberately
        /// NOT asserted here.
        /// </summary>
        private static string JudgeSchemaVersionLikeTheServer(JToken pascal, JToken camel)
        {
            JToken incoming = pascal != null && pascal.Type != JTokenType.Null ? pascal : camel;
            if (incoming == null || incoming.Type == JTokenType.Null) return "ABSENT";

            if (incoming.Type == JTokenType.Integer)
                return incoming.Value<long>() > 0L ? "DECLARED" : "ABSENT";

            if (incoming.Type == JTokenType.String)
            {
                var s = incoming.Value<string>();
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) return "ABSENT";
                return long.TryParse(s.Trim(), out long parsed) && parsed > 0L ? "DECLARED" : "ABSENT";
            }

            // Floats, bools, objects: Number.isInteger(v) is false for all of them, so the
            // server takes its unparseable arm — accepted, version untouched.
            return "ABSENT";
        }

        // ── B/C — the failure line ───────────────────────────────────────────────

        private static IEnumerable<GameStateService.SaveAttemptCategory> FailureCategories()
        {
            foreach (GameStateService.SaveAttemptCategory c in
                     Enum.GetValues(typeof(GameStateService.SaveAttemptCategory)))
            {
                if (c == GameStateService.SaveAttemptCategory.Ok) continue;
                yield return c;
            }
        }

        /// <summary>
        /// The body head off the owner's device, 2026-09-07 08:01:37.093, kept as a HISTORICAL
        /// FIXTURE and nothing more.
        /// <para>
        /// ⛔ IT IS NOT THE SERVER'S CURRENT ANSWER. api/game/save.js retired
        /// <c>SCHEMA_VERSION_MISSING</c> as an outcome the same day (an absent version now
        /// returns 200 with <c>note:'SCHEMA_VERSION_ABSENT'</c>), so this suite must never be
        /// read as pinning that refusal. What it pins is category MECHANICS, which are
        /// independent of any one server code: <b>a 4xx whose body carries code X reads
        /// why=http-4xx and carries X onto the line</b>. Swap in any other refusal code and
        /// every assertion below still holds.
        /// </para>
        /// </summary>
        private const string HistoricalRefusalBody =
            "{\"ok\":false,\"code\":\"SCHEMA_VERSION_MISSING\",\"ref\":\"bb0c95eb\"}";

        private static void CheckEveryCategoryNamesACause(List<string> failures, StringBuilder log)
        {
            var seen = new Dictionary<string, GameStateService.SaveAttemptCategory>(StringComparer.Ordinal);
            int n = 0;

            foreach (var category in FailureCategories())
            {
                n++;
                var result = new GameStateService.SaveAttemptResult(category, 400L, HistoricalRefusalBody);
                string line = GameStateService.DescribeSaveFailure(result);
                log.AppendLine($"  [cause] {category} -> {line}");

                if (string.IsNullOrEmpty(line))
                {
                    failures.Add($"[cause] {category} produced an EMPTY reason - a drain failure must always name its cause");
                    continue;
                }

                string token = ExtractToken(line, "why=");
                if (string.IsNullOrEmpty(token) || token == "none" || token == "unknown")
                {
                    failures.Add($"[cause] {category} produced why='{token}' - every failure category needs a real token, " +
                                 "otherwise a reader is back to guessing (the whole point of WO-1587)");
                    continue;
                }

                if (seen.TryGetValue(token, out var other))
                    failures.Add($"[cause] {category} and {other} share the token why={token} - " +
                                 "two different causes reading identically is the ambiguity this suite exists to stop");
                else
                    seen[token] = category;

                if (line.IndexOf("body=", StringComparison.Ordinal) < 0)
                    failures.Add($"[cause] {category} omits body= - the server's own refusal code is the answer on a 4xx");
                if (line.IndexOf("http=400", StringComparison.Ordinal) < 0)
                    failures.Add($"[cause] {category} omits the HTTP status it was handed");
            }

            if (n == 0) failures.Add("[cause] SaveAttemptCategory has no failure members at all");

            // THE MECHANIC, end to end, on the shape the owner's device actually produced:
            // a 4xx whose body carries a refusal code must read as a PAYLOAD refusal that
            // NAMES that code - never as an identity problem. The specific code is a fixture
            // (see HistoricalRefusalBody); the mechanic is what is pinned, so this case
            // survives the server retiring or renaming any individual code.
            var deviceCase = new GameStateService.SaveAttemptResult(
                GameStateService.ClassifyHttp(400L), 400L, HistoricalRefusalBody);
            string deviceLine = GameStateService.DescribeSaveFailure(deviceCase);
            log.AppendLine("  [cause] historical device 400 -> " + deviceLine);
            if (ExtractToken(deviceLine, "why=") != "http-4xx")
                failures.Add("[cause] a 400 from /api/game/save no longer classifies as http-4xx - " +
                             "the owner's six drain failures would be mis-attributed again");
            if (deviceLine.IndexOf("\"code\":", StringComparison.Ordinal) < 0)
                failures.Add("[cause] the server's own refusal code is not carried onto the failure line - " +
                             "whatever the code of the day is, THAT code is the diagnosis; dropping it " +
                             "puts the reader back where WO-1587 found them");

            // Transport must never be reported as a server refusal: status 0 = no answer.
            if (GameStateService.ClassifyHttp(0L) != GameStateService.SaveAttemptCategory.Transport)
                failures.Add("[cause] a request that never got an HTTP status is being classified as a server refusal");
            if (GameStateService.ClassifyHttp(503L) != GameStateService.SaveAttemptCategory.HttpServer)
                failures.Add("[cause] a 5xx is not classified as a server failure");
        }

        private static void CheckOnlyAuthAbsentBlamesTheWallet(List<string> failures, StringBuilder log)
        {
            foreach (var category in FailureCategories())
            {
                var result = new GameStateService.SaveAttemptResult(category, 400L, "detail");
                string line = GameStateService.DescribeSaveFailure(result);
                bool blamesWallet = line.IndexOf("Flow:Wallet", StringComparison.Ordinal) >= 0;
                bool isAuth = category == GameStateService.SaveAttemptCategory.AuthAbsent;

                if (blamesWallet && !isAuth)
                    failures.Add($"[blame] {category} sends the reader to [Flow:Wallet] - that is the mis-attribution " +
                                 "that cost this ticket a day: the wallet rail minted and renewed perfectly while " +
                                 "every save 400'd on its payload");
                if (!blamesWallet && isAuth)
                    failures.Add("[blame] auth-absent no longer points at [Flow:Wallet] - it is the ONE category " +
                                 "where the wallet trace really does hold the answer");
            }
            log.AppendLine("  [blame] wallet pointer is exclusive to auth-absent");
        }

        // ── D — the drain line cannot re-hardcode a pointer ──────────────────────

        private static void CheckDrainLineIsFormattedFromTheAttempt(List<string> failures, StringBuilder log)
        {
            if (!File.Exists(StatePath))
            {
                failures.Add($"[drain] {StatePath} not found - re-point this oracle");
                return;
            }

            string src = File.ReadAllText(StatePath);

            // ⚠ ANCHOR ON THE INTERPOLATED HEAD, NOT THE BARE PHRASE. The coalesce warning
            // ~250 lines earlier now QUOTES "'offline queue drain FAILED' line." when it tells
            // the reader where the cause lives, so a bare IndexOf finds the coalesce statement
            // first and asserts against the wrong warning entirely. Only the real drain line
            // carries the marker count interpolation.
            const string DrainAnchor = "offline queue drain FAILED - {mine.Count}";
            int at = src.IndexOf(DrainAnchor, StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add("[drain] the drain-failure warning ('" + DrainAnchor + "') is gone or was " +
                             "re-worded - a failed drain must never be silent (CLAUDE.md s12); re-point this oracle");
                return;
            }

            // The warning's own statement, from the literal to the terminating ');'.
            int end = src.IndexOf(");", at, StringComparison.Ordinal);
            string stmt = end > at ? src.Substring(at, end - at) : src.Substring(at);
            log.AppendLine("  [drain] warning statement length " + stmt.Length);

            if (stmt.IndexOf("DescribeSaveFailure(attempt)", StringComparison.Ordinal) < 0)
                failures.Add("[drain] the drain-failure warning no longer formats its reason from the ATTEMPT - " +
                             "a hand-written pointer is how it came to promise a [Flow:Wallet] why= line that " +
                             "nothing prints");
            if (stmt.IndexOf("Flow:Wallet", StringComparison.Ordinal) >= 0)
                failures.Add("[drain] the drain-failure warning hardcodes [Flow:Wallet] again - the wallet is named " +
                             "ONLY by DescribeSaveFailure, and only for auth-absent");
            if (stmt.IndexOf("marker(s) re-queued and RETAINED", StringComparison.Ordinal) < 0)
                failures.Add("[drain] the retained-marker count was dropped from the failure line (WO-1441's measurement)");
        }

        // ── E — the detail is safe to put on one log line ────────────────────────

        private static void CheckDetailStaysOneLine(List<string> failures, StringBuilder log)
        {
            string squashed = GameStateService.SquashDetail("line one\r\nline two\ttabbed");
            log.AppendLine("  [detail] squashed='" + squashed + "'");
            if (squashed.IndexOf('\n') >= 0 || squashed.IndexOf('\r') >= 0 || squashed.IndexOf('\t') >= 0)
                failures.Add("[detail] SquashDetail leaves newlines/tabs - a multi-line warning breaks every log grep");

            string huge = GameStateService.SquashDetail(new string('x', 5000));
            if (huge.Length > 200)
                failures.Add($"[detail] SquashDetail returned {huge.Length} chars - an uncapped body head floods the " +
                             "logcat ring and evicts the very evidence it was added to preserve " +
                             "(memory: logcat-ring-buffer-destroys-evidence)");

            if (GameStateService.SquashDetail(null) != string.Empty)
                failures.Add("[detail] SquashDetail(null) must be empty, never a crash or a 'null' literal");
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        /// <summary>Reads the value of "&lt;key&gt;value" up to the next space.</summary>
        private static string ExtractToken(string line, string key)
        {
            int at = line.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return string.Empty;
            at += key.Length;
            int end = line.IndexOf(' ', at);
            return end < 0 ? line.Substring(at) : line.Substring(at, end - at);
        }
    }
}
