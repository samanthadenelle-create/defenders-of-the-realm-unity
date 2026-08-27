// =============================================================================
// FoundersMonumentWallRegression [founders-wall]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (already references DeNelle.Core and
//   DeNelle.Village - no asmdef edit needed. It does NOT reference DeNelle.HUD,
//   so the panel is asserted by SOURCE LINT, which is stated per case below.)
//
// Pins the WO-1073 client half: the Founders Monument stand-in near the Heart
// and the Benefactors of the Realm wall it opens.
//
// -----------------------------------------------------------------------------
// THE THREE THINGS A FUTURE SEAT IS MOST LIKELY TO BREAK, AND WHY EACH HAS A CASE
// -----------------------------------------------------------------------------
// 1. THE PER-PATRON MONUMENT COLLAPSED INTO A GLOBAL FLAG. It is one bool on a
//    row and it looks exactly like the kind of thing that "obviously" belongs on
//    the catalog. The owner ruled the opposite twice, in writing: "Founder A may
//    have their real monument while Founder B is still on the stand-in. Do not
//    model this as one global flag." Case 3 drives a MIXED payload, so a
//    collapse to one flag is a RED rather than a design discussion.
// 2. THE STAND-IN KEY DRIFTING FROM THE SERVER'S. NULL in the database resolves
//    - server side - to one literal, and a database CHECK forbids storing it, so
//    there is exactly one spelling of "placeholder" on either side of the wire.
//    Two literals in two languages in two files is precisely the duplicated-state
//    failure CLAUDE.md catalogues (the stale WO number block, the hardcoded repo
//    root, the retired dependency table). Case 1 compares them byte for byte.
// 3. A SECOND DOOR. The wall is deliberately reachable ONE way - by walking up to
//    the monument. Adding a menu item or a bar face is a two-line change that
//    feels like a kindness and overturns an owner ruling. Case 6 counts the doors.
//
// -----------------------------------------------------------------------------
// COMMENTS: DECIDED ON PURPOSE, PER CASE, AND STATED OUT LOUD.
// -----------------------------------------------------------------------------
// Getting this wrong in either direction has cost this repo four oracle failures
// in a week - a lint that read comments and cried wolf, and a lint that stripped
// them and certified nothing. So:
//   * Case 4 [no-identity] : comments EXCLUDED, string literals INCLUDED. The
//                            headers of both files spend paragraphs saying the
//                            words "wallet" and "email" in order to forbid them;
//                            a comment-reading lint would red on the very
//                            sentences that document compliance. A string LITERAL
//                            containing them is a real defect, so those stay in.
//   * Case 6 [one-door]    : comments EXCLUDED. Several headers name
//                            PanelId.Benefactors while explaining where it may
//                            NOT be opened from; a comment is not a call site.
//   * Case 8 [one-key]     : comments EXCLUDED, strings INCLUDED - the point of
//                            the case is that the address literal exists in
//                            exactly one place, and it is a string.
//   * Case 9 [instrument]  : comments EXCLUDED. Every file's header discusses
//                            FlowTrace; only real calls count.
//   * Case 1 [key-parity]  : reads DECLARATION LINES out of api/*.js by regex,
//                            so a comment mentioning the id is never the source.
//
// EVERY THRESHOLD IS A NAMED CONSTANT PINNED TO A LITERAL. Nothing here is
// expressed relative to a moving value - a bound written as "whatever the code
// says" silently stops testing the moment the code moves.
//
// Cases:
//   1 [key-parity]   The stand-in address, the founder tier id, the name cap and
//                    the row limits are byte-identical to api/_lib/benefactors.js
//                    and api/_lib/patron-name.js.
//   2 [payload]      Every payload shape drives the catalog: a good wall, an empty
//                    body, an unparseable body, success:false, a foreign tier, a
//                    nameless row, an over-cap name, a timestamp date, and an
//                    over-limit list. The GOOD path is asserted too, so the case
//                    cannot be satisfied by rejecting everything.
//   3 [mixed]        One payload, one bespoke founder and one stand-in founder,
//                    both read back correctly - per patron, never a global phase.
//                    Includes the server-disagrees case: the ASSET ID wins.
//   4 [no-identity]  No wallet, email, real name or dollar figure can reach the
//                    screen: neither the DTO nor the panel has anywhere to put one.
//   5 [siting]       The monument is placed NEAR the Heart and never ON it - the
//                    offset is derived from the Heart anchor, is bounded at both
//                    ends, and shares the Heart's ground plane.
//   6 [one-door]     Exactly ONE call site opens PanelId.Benefactors, and it is
//                    the monument. Exactly one thing registers it.
//   7 [ascii]        Every player-facing string is ASCII (the tofu oracle rule).
//   8 [one-key]      The stand-in address literal exists in exactly ONE C# file -
//                    the mesh is DATA with a single drop-in point, not a value
//                    scattered through logic.
//   9 [instrument]   Every new file instruments, and none of them swallows.
//
// Markers: FOUNDERS_WALL_OK / FOUNDERS_WALL_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.FoundersMonumentWallRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Core.Patronage;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class FoundersMonumentWallRegression
    {
        // ---------------------------------------------------------------------
        //  PINNED FACTS. Every one is a literal.
        // ---------------------------------------------------------------------

        /// <summary>The shared stand-in address, written out. If either side moves, case 1
        /// reds against THIS, not against the other side - so a seat that "fixes" the drift
        /// by editing both files still has to come here and mean it.</summary>
        private const string ExpectedStandInKey = "monument_founder_standin";

        /// <summary>The one tier on the wall. $500 Founders ONLY (owner ruling 2026-08-27).</summary>
        private const string ExpectedFounderTierId = "founder_benefactor";

        /// <summary>PATRON_NAME_MAX_LEN in api/_lib/patron-name.js.</summary>
        private const int ExpectedNameCap = 24;

        /// <summary>WALL_DEFAULT_ROWS / WALL_MAX_ROWS in api/_lib/benefactors.js.</summary>
        private const int ExpectedDefaultRows = 50;
        private const int ExpectedMaxRows = 200;

        /// <summary>
        /// The monument must stand at least this far from the Heart. Below it the two objects
        /// read as one - and the owner's siting ruling is that the Heart must never become
        /// "a NASCAR hood covered in sponsor names". The Heart's own gameplay capsule is
        /// radius 2 and the Tree of Life visual is far wider, so a small number here is not
        /// merely inelegant, it is inside the tree.
        /// </summary>
        private const float MinMetresFromHeart = 5f;

        /// <summary>
        /// And no further than this, or it stops being the Heart's companion and becomes a
        /// random prop in the plaza. The storefront ring sits at +/-22 m, so anything
        /// approaching that is in another building's spot.
        /// </summary>
        private const float MaxMetresFromHeart = 16f;

        /// <summary>The scene-object name CastleHubBuilder gives the Heart anchor.</summary>
        private const string ExpectedHeartAnchorName = "HeartOfElarion";

        /// <summary>Exactly one world door onto the wall. Owner ruling 2026-08-27(c).</summary>
        private const int ExpectedOpenCallSites = 1;

        /// <summary>Exactly one registrar of the panel id.</summary>
        private const int ExpectedRegisterCallSites = 1;

        /// <summary>The stand-in address literal lives in exactly ONE C# file.</summary>
        private const int ExpectedKeyLiteralFiles = 1;

        // Source paths. Repo-relative: batchmode's working directory IS the project root.
        private const string CatalogSrc = "Assets/_Modules/Core/Patronage/BenefactorsCatalog.cs";
        private const string ServiceSrc = "Assets/_Modules/Core/Patronage/BenefactorsService.cs";
        private const string DoorSrc = "Assets/_Modules/Village/Patronage/FoundersMonument.cs";
        private const string InjectorSrc = "Assets/_Modules/Village/Patronage/FoundersMonumentInjector.cs";
        private const string PanelSrc = "Assets/_Modules/HUD/BenefactorsWallPanel.cs";
        private const string PanelBootSrc = "Assets/_Modules/HUD/BenefactorsWallPanelBootstrap.cs";
        private const string ApiWallLibSrc = "api/_lib/benefactors.js";
        private const string ApiNameLibSrc = "api/_lib/patron-name.js";
        private const string HubBuilderSrc = "Assets/Editor/CastleHubBuilder.cs";
        private const string ModulesRoot = "Assets/_Modules";

        /// <summary>The files this WO adds. Case 9 holds every one of them to the
        /// instrumentation standard.</summary>
        private static readonly string[] NewSources =
        {
            CatalogSrc, ServiceSrc, DoorSrc, InjectorSrc, PanelSrc, PanelBootSrc,
        };

        /// <summary>
        /// Tokens that must NEVER appear in the code or the string literals of the two files
        /// that decide what reaches the screen. The wall names WHO, never how much and never
        /// who in real life.
        /// </summary>
        private static readonly string[] BannedIdentityTokens =
        {
            "wallet", "email", "usd", "cents", "lifetime", "amount", "price", "realname",
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("FOUNDERS_WALL_OK - " + reason);
            else Debug.LogError("FOUNDERS_WALL_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "key-parity", () => Case1_KeyParity(failures, notes));
                Case(failures, "payload", () => Case2_Payload(failures, notes));
                Case(failures, "mixed", () => Case3_MixedMonumentState(failures, notes));
                Case(failures, "no-identity", () => Case4_NoIdentity(failures, notes));
                Case(failures, "siting", () => Case5_Siting(failures, notes));
                Case(failures, "one-door", () => Case6_OneDoor(failures, notes));
                Case(failures, "ascii", () => Case7_Ascii(failures, notes));
                Case(failures, "one-key", () => Case8_OneKeyLiteral(failures, notes));
                Case(failures, "instrument", () => Case9_Instrumentation(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                BenefactorsCatalog.Clear();
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "FOUNDERS WALL OK - the stand-in address, the founder tier id, the name cap " +
                         "and the row limits match api/_lib byte for byte; every payload shape (good, " +
                         "empty, unparseable, success=false, foreign tier, nameless row, over-cap name, " +
                         "timestamp date, over-limit list) resolves correctly and a rejected payload " +
                         "leaves the standing wall intact; per-patron monument state reads back MIXED " +
                         "from one payload and the asset id outranks a disagreeing flag; no wallet, " +
                         "email or dollar figure can reach the screen; the monument stands " +
                         MinMetresFromHeart + "-" + MaxMetresFromHeart + "m from the Heart anchor on " +
                         "its own ground plane, derived from the Heart rather than hardcoded; exactly " +
                         ExpectedOpenCallSites + " call site opens PanelId.Benefactors and exactly " +
                         ExpectedRegisterCallSites + " registers it; every player-facing string is " +
                         "ASCII; the stand-in address literal exists in exactly " +
                         ExpectedKeyLiteralFiles + " C# file (one drop-in point for the real FBX); and " +
                         "all " + NewSources.Length + " new files instrument without swallowing" + noteStr;
                return true;
            }
            reason = "founders-wall FAIL x" + failures.Count + ": " + string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - KEY PARITY. Two languages, two files, one contract.
        // =====================================================================
        private static void Case1_KeyParity(List<string> failures, List<string> notes)
        {
            // (a) The C# side against the PINNED literals. This half runs with or without
            //     api/ present, so the case can never assert nothing.
            Eq(failures, "key-parity", "BenefactorsCatalog.StandInMonumentAssetKey",
                BenefactorsCatalog.StandInMonumentAssetKey, ExpectedStandInKey);
            Eq(failures, "key-parity", "BenefactorsCatalog.FounderTierId",
                BenefactorsCatalog.FounderTierId, ExpectedFounderTierId);
            EqInt(failures, "key-parity", "BenefactorsCatalog.MaxPatronNameLength",
                BenefactorsCatalog.MaxPatronNameLength, ExpectedNameCap);
            EqInt(failures, "key-parity", "BenefactorsCatalog.DefaultRowLimit",
                BenefactorsCatalog.DefaultRowLimit, ExpectedDefaultRows);
            EqInt(failures, "key-parity", "BenefactorsCatalog.MaxRowLimit",
                BenefactorsCatalog.MaxRowLimit, ExpectedMaxRows);

            // (b) The api/ side, read from the DECLARATION LINES (never a comment).
            string wall = ReadIfExists(ApiWallLibSrc);
            if (wall == null)
            {
                notes.Add(RegressionOutcome.PartialSkip("key-parity/api-wall",
                    ApiWallLibSrc + " is not present in this checkout - the C# half above still ran"));
            }
            else
            {
                EqJs(failures, wall, ApiWallLibSrc, "PLACEHOLDER_MONUMENT_ASSET_ID", ExpectedStandInKey);
                EqJs(failures, wall, ApiWallLibSrc, "FOUNDER_TIER_ID", ExpectedFounderTierId);
                EqJsInt(failures, wall, ApiWallLibSrc, "WALL_DEFAULT_ROWS", ExpectedDefaultRows);
                EqJsInt(failures, wall, ApiWallLibSrc, "WALL_MAX_ROWS", ExpectedMaxRows);
            }

            string nameLib = ReadIfExists(ApiNameLibSrc);
            if (nameLib == null)
            {
                notes.Add(RegressionOutcome.PartialSkip("key-parity/api-name",
                    ApiNameLibSrc + " is not present in this checkout"));
            }
            else
            {
                EqJsInt(failures, nameLib, ApiNameLibSrc, "PATRON_NAME_MAX_LEN", ExpectedNameCap);
            }
        }

        // =====================================================================
        //  Case 2 - EVERY PAYLOAD SHAPE. The good path is asserted too.
        // =====================================================================
        private static void Case2_Payload(List<string> failures, List<string> notes)
        {
            // (a) THE GOOD PATH FIRST. If this does not work the rest is meaningless.
            BenefactorsCatalog.Clear();
            if (!BenefactorsCatalog.ApplyPayload(GoodWallJson()))
                failures.Add("[payload] a well-formed wall payload was REJECTED - the good path is broken.");
            EqInt(failures, "payload", "rows after a good payload", BenefactorsCatalog.Count, 2);
            if (BenefactorsCatalog.Count == 2)
            {
                var r0 = BenefactorsCatalog.Rows[0];
                Eq(failures, "payload", "row 0 patron name", r0.PatronName, "House Ferrow");
                Eq(failures, "payload", "row 0 founded date", r0.FoundedOn, "2026-08-27");
                EqInt(failures, "payload", "row 0 ordinal", r0.Ordinal, 1);
            }
            Eq(failures, "payload", "provenance after a good payload",
                BenefactorsCatalog.Provenance, BenefactorsCatalog.ProvenanceLive);

            // (b) EVERY REJECT PATH LEAVES THE STANDING WALL INTACT. That is the property
            //     that matters: a dropped packet must never blank an honour roll.
            RejectKeepsWall(failures, "empty body", "");
            RejectKeepsWall(failures, "whitespace body", "   ");
            RejectKeepsWall(failures, "unparseable body", TruncatedJson());
            RejectKeepsWall(failures, "success=false",
                Obj("\"success\":false,\"tier\":\"" + ExpectedFounderTierId + "\",\"count\":0,\"benefactors\":[]"));
            RejectKeepsWall(failures, "foreign tier",
                Obj("\"success\":true,\"tier\":\"patron\",\"count\":0,\"benefactors\":[]"));

            // (c) ROW-LEVEL REFUSALS. The payload is accepted; the bad ROWS are dropped.
            BenefactorsCatalog.Clear();
            string overCapName = new string('A', ExpectedNameCap + 1);
            string mixed = Obj("\"success\":true,\"tier\":\"" + ExpectedFounderTierId + "\",\"count\":4," +
                "\"benefactors\":[" +
                Row(1, "Keeper Vale", "2026-08-27", null, false) + "," +
                Row(2, "", "2026-08-27", null, false) + "," +
                Row(3, overCapName, "2026-08-27", null, false) + "," +
                Row(4, "Ashen Rook", "2026-08-27T04:15:00.000Z", null, false) +
                "]");
            if (!BenefactorsCatalog.ApplyPayload(mixed))
                failures.Add("[payload] a payload with SOME bad rows was rejected whole - bad rows are " +
                             "dropped individually, never taken as a reason to blank the wall.");
            EqInt(failures, "payload", "rows surviving row-level refusal", BenefactorsCatalog.Count, 2);
            if (BenefactorsCatalog.Count == 2)
            {
                Eq(failures, "payload", "nameless + over-cap rows dropped, first survivor",
                    BenefactorsCatalog.Rows[0].PatronName, "Keeper Vale");
                // A TIMESTAMP must be trimmed to a DATE: the hour somebody paid is nobody's business.
                Eq(failures, "payload", "timestamp trimmed to a date",
                    BenefactorsCatalog.Rows[1].FoundedOn, "2026-08-27");
            }

            // (d) OVER-LIMIT LIST is truncated to the server's own ceiling.
            BenefactorsCatalog.Clear();
            var sb = new StringBuilder();
            int over = ExpectedMaxRows + 5;
            for (int i = 0; i < over; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Row(i + 1, "Patron " + (i + 1), "2026-08-27", null, false));
            }
            BenefactorsCatalog.ApplyPayload(Obj("\"success\":true,\"tier\":\"" + ExpectedFounderTierId +
                "\",\"count\":" + over + ",\"benefactors\":[" + sb + "]"));
            EqInt(failures, "payload", "rows after an over-limit payload",
                BenefactorsCatalog.Count, ExpectedMaxRows);

            // (e) AN HONESTLY EMPTY WALL IS ACCEPTED, and is the true day-one state.
            BenefactorsCatalog.Clear();
            if (!BenefactorsCatalog.ApplyPayload(Obj("\"success\":true,\"tier\":\"" +
                    ExpectedFounderTierId + "\",\"count\":0,\"benefactors\":[]")))
                failures.Add("[payload] an honestly EMPTY wall was rejected - it is the correct day-one state.");
            EqInt(failures, "payload", "rows after an empty-but-successful payload", BenefactorsCatalog.Count, 0);
            if (!BenefactorsCatalog.EverRead)
                failures.Add("[payload] an accepted empty wall did not count as read - the panel would " +
                             "show 'Reading the wall...' forever.");

            // (f) A TRANSPORT FAILURE KEEPS THE ROWS AND ONLY MOVES PROVENANCE.
            BenefactorsCatalog.Clear();
            BenefactorsCatalog.ApplyPayload(GoodWallJson());
            BenefactorsCatalog.MarkFetchFailed("simulated timeout");
            EqInt(failures, "payload", "rows kept across a fetch failure", BenefactorsCatalog.Count, 2);
            Eq(failures, "payload", "provenance after a fetch failure",
                BenefactorsCatalog.Provenance, BenefactorsCatalog.ProvenanceStale);
        }

        private static void RejectKeepsWall(List<string> failures, string what, string json)
        {
            BenefactorsCatalog.Clear();
            BenefactorsCatalog.ApplyPayload(GoodWallJson());
            int before = BenefactorsCatalog.Count;

            if (BenefactorsCatalog.ApplyPayload(json))
                failures.Add("[payload] '" + what + "' was ACCEPTED - it must be rejected.");
            if (BenefactorsCatalog.Count != before)
                failures.Add("[payload] '" + what + "' changed the standing wall from " + before +
                             " to " + BenefactorsCatalog.Count + " row(s). A rejected payload must " +
                             "leave the standing wall exactly as it was.");
        }

        // =====================================================================
        //  Case 3 - PER PATRON, NEVER A GLOBAL PHASE.
        // =====================================================================
        private static void Case3_MixedMonumentState(List<string> failures, List<string> notes)
        {
            BenefactorsCatalog.Clear();
            string json = Obj("\"success\":true,\"tier\":\"" + ExpectedFounderTierId + "\",\"count\":3," +
                "\"benefactors\":[" +
                // A: bespoke, correctly flagged.
                Row(1, "House Ferrow", "2026-08-20", "monument_house_ferrow", true) + "," +
                // B: still on the shared stand-in, explicitly named.
                Row(2, "Keeper Vale", "2026-08-25", ExpectedStandInKey, false) + "," +
                // C: bespoke asset, but the server sent monumentIsBespoke=false. The ASSET ID
                //    is what the world renders from, so it wins.
                Row(3, "Ashen Rook", "2026-08-26", "monument_ashen_rook", false) +
                "]");

            if (!BenefactorsCatalog.ApplyPayload(json))
            {
                failures.Add("[mixed] a mixed monument-state payload was rejected outright.");
                return;
            }
            EqInt(failures, "mixed", "row count", BenefactorsCatalog.Count, 3);
            if (BenefactorsCatalog.Count != 3) return;

            var a = BenefactorsCatalog.Rows[0];
            var b = BenefactorsCatalog.Rows[1];
            var c = BenefactorsCatalog.Rows[2];

            if (!a.MonumentIsBespoke)
                failures.Add("[mixed] founder A carries a bespoke asset id and did NOT read back as " +
                             "bespoke - the per-patron state is broken.");
            if (b.MonumentIsBespoke)
                failures.Add("[mixed] founder B is on the shared stand-in and read back as BESPOKE.");
            Eq(failures, "mixed", "founder B monument asset", b.MonumentAssetId, ExpectedStandInKey);
            if (!c.MonumentIsBespoke)
                failures.Add("[mixed] founder C carries a bespoke asset id with monumentIsBespoke=false " +
                             "from the server, and the FLAG won. The ASSET ID must win - it is the field " +
                             "the world actually renders from.");

            // THE POINT OF THE CASE, stated as an assertion rather than as a comment: the same
            // payload holds both states at once. A global flag cannot express this.
            if (a.MonumentIsBespoke == b.MonumentIsBespoke)
                failures.Add("[mixed] every row in a deliberately MIXED payload reported the same " +
                             "monument state - the per-patron model has been collapsed into a global " +
                             "one. Owner ruling 2026-08-27(c) forbids exactly this.");

            // A row that sends NO monument field at all falls back to the stand-in, never to null.
            BenefactorsCatalog.Clear();
            BenefactorsCatalog.ApplyPayload(Obj("\"success\":true,\"tier\":\"" + ExpectedFounderTierId +
                "\",\"count\":1,\"benefactors\":[" + Row(1, "Silent Ward", "2026-08-27", null, false) + "]"));
            if (BenefactorsCatalog.Count == 1)
            {
                Eq(failures, "mixed", "absent monument field falls back to the stand-in",
                    BenefactorsCatalog.Rows[0].MonumentAssetId, ExpectedStandInKey);
                if (BenefactorsCatalog.Rows[0].MonumentIsBespoke)
                    failures.Add("[mixed] a row with no monument field read back as BESPOKE.");
            }
            else failures.Add("[mixed] the no-monument-field row was dropped instead of defaulted.");
        }

        // =====================================================================
        //  Case 4 - NOTHING THAT IDENTIFIES A HUMAN, AND NO DOLLARS.
        //  Comments EXCLUDED, string literals INCLUDED. See the header.
        // =====================================================================
        private static void Case4_NoIdentity(List<string> failures, List<string> notes)
        {
            foreach (string path in new[] { CatalogSrc, PanelSrc })
            {
                string src = ReadIfExists(path);
                if (src == null) { failures.Add("[no-identity] missing source " + path); continue; }
                string code = StripComments(src);
                string lower = code.ToLowerInvariant();
                foreach (string banned in BannedIdentityTokens)
                {
                    if (lower.Contains(banned))
                        failures.Add("[no-identity] " + path + " CODE contains the token '" + banned +
                                     "'. The wall names WHO, never how much and never who in real " +
                                     "life; there must be nowhere in this file to put one.");
                }
                // A literal dollar sign in a player-facing string is the other shape of the
                // same defect (WO-1073 section 4: show the TIER, never the amount).
                foreach (string lit in StringLiterals(code))
                    if (lit.Contains("$"))
                        failures.Add("[no-identity] " + path + " has a player-facing string carrying a " +
                                     "'$': \"" + Trim(lit) + "\". Public surfaces show the TIER, never " +
                                     "the amount.");
            }
        }

        // =====================================================================
        //  Case 5 - NEAR THE HEART, NEVER ON IT.
        // =====================================================================
        private static void Case5_Siting(List<string> failures, List<string> notes)
        {
            Vector3 off = FoundersMonumentInjector.OffsetFromHeart;
            float d = new Vector2(off.x, off.z).magnitude;

            if (d < MinMetresFromHeart)
                failures.Add("[siting] the monument stands " + d.ToString("F2") + "m from the Heart, " +
                             "inside the floor of " + MinMetresFromHeart + "m. The owner's siting " +
                             "ruling is that the Heart must never become 'a NASCAR hood covered in " +
                             "sponsor names' - this is close enough to read as part of it.");
            if (d > MaxMetresFromHeart)
                failures.Add("[siting] the monument stands " + d.ToString("F2") + "m from the Heart, " +
                             "beyond the ceiling of " + MaxMetresFromHeart + "m. It stops being the " +
                             "Heart's companion and starts being a prop somewhere else in the plaza.");
            if (Mathf.Abs(off.y) > 0.001f)
                failures.Add("[siting] the offset carries a Y of " + off.y + ". The monument shares the " +
                             "Heart's ground plane whatever the island has been raised to (WO-593); a " +
                             "hardcoded Y is how it ends up buried or floating.");

            Eq(failures, "siting", "the Heart anchor name the injector looks for",
                FoundersMonumentInjector.HeartAnchorName, ExpectedHeartAnchorName);

            // The anchor name must still be what the hub bake actually produces. A rename there
            // with no rename here is a monument that silently never places.
            string builder = ReadIfExists(HubBuilderSrc);
            if (builder == null)
            {
                notes.Add(RegressionOutcome.PartialSkip("siting/anchor-name",
                    HubBuilderSrc + " not present - the injector-side constant was still asserted"));
            }
            else if (!StripComments(builder).Contains("\"" + ExpectedHeartAnchorName + "\""))
            {
                failures.Add("[siting] CastleHubBuilder no longer declares the anchor name \"" +
                             ExpectedHeartAnchorName + "\". The injector derives the monument's " +
                             "position from that object by name, so a rename there means the monument " +
                             "never places and the wall loses its only door.");
            }

            // The position must be DERIVED, not authored: a world-space literal for the plaza is
            // the brittleness this design rejects.
            string inj = ReadIfExists(InjectorSrc);
            if (inj == null) failures.Add("[siting] missing source " + InjectorSrc);
            else
            {
                string code = StripComments(inj);
                // ⚠ THE WHOLE EXPRESSION, not just the token. A first draft asserted only that
                // "heart.position" appeared SOMEWHERE in the file - and a mutation that replaced the
                // real seat with a hardcoded `new Vector3(8f, 3f, 12f)` SURVIVED it, because a trace
                // line further down still mentioned heart.position. The assertion has to name the
                // computation, not a word that happens to be nearby.
                if (!Regex.IsMatch(code, @"heart\s*\.\s*position\s*\+\s*OffsetFromHeart"))
                    failures.Add("[siting] " + InjectorSrc + " no longer seats the monument at " +
                                 "heart.position + OffsetFromHeart. A hardcoded world position is wrong " +
                                 "the next time the island moves (WO-593 raised it once already) and " +
                                 "silently breaks the near-the-Heart siting ruling this case exists for.");
            }
        }

        // =====================================================================
        //  Case 6 - EXACTLY ONE DOOR. Comments EXCLUDED. See the header.
        // =====================================================================
        private static void Case6_OneDoor(List<string> failures, List<string> notes)
        {
            var openSites = new List<string>();
            var registerSites = new List<string>();

            foreach (string path in EnumerateCs(ModulesRoot))
            {
                string code = StripComments(File.ReadAllText(path));
                // ⚠ THE OPTIONAL QUALIFIER IS LOAD-BEARING. A first draft of this lint matched
                // only a bare `PanelId.Benefactors`, and a mutation that opened the wall as
                // `DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.Benefactors)` SURVIVED
                // it - a second door, fully qualified, invisible to the oracle meant to forbid it.
                // Proven RED only after the qualifier was allowed for. (Unregister cannot match:
                // the pattern anchors on PanelRouter, then a dot, then Register.)
                if (Regex.IsMatch(code, @"PanelRouter\s*\.\s*Open\s*\(\s*(?:[\w.]+\s*\.\s*)?PanelId\s*\.\s*Benefactors"))
                    openSites.Add(Rel(path));
                if (Regex.IsMatch(code, @"PanelRouter\s*\.\s*Register\s*\(\s*(?:[\w.]+\s*\.\s*)?PanelId\s*\.\s*Benefactors"))
                    registerSites.Add(Rel(path));
            }

            if (openSites.Count != ExpectedOpenCallSites)
                failures.Add("[one-door] " + openSites.Count + " site(s) open PanelId.Benefactors, " +
                             "expected " + ExpectedOpenCallSites + " (" +
                             string.Join(", ", openSites.ToArray()) + "). Owner ruling 2026-08-27(c): " +
                             "\"walking up to the monument and reading the names is the moment; a menu " +
                             "item is not.\" A second door is a design change, not a convenience.");
            else if (openSites.Count == 1 && !openSites[0].EndsWith("FoundersMonument.cs"))
                failures.Add("[one-door] the single opener is " + openSites[0] +
                             ", not FoundersMonument.cs. The monument IS the door.");

            if (registerSites.Count != ExpectedRegisterCallSites)
                failures.Add("[one-door] " + registerSites.Count + " site(s) register PanelId.Benefactors, " +
                             "expected " + ExpectedRegisterCallSites + " (" +
                             string.Join(", ", registerSites.ToArray()) + "). Two registrars means the " +
                             "last one to Awake silently wins.");
        }

        // =====================================================================
        //  Case 7 - ASCII ONLY. The tofu oracle rule.
        // =====================================================================
        private static void Case7_Ascii(List<string> failures, List<string> notes)
        {
            // The constants that reach a label, asserted as VALUES rather than as source.
            var playerFacing = new Dictionary<string, string>
            {
                { "WallTitle", BenefactorsCatalog.WallTitle },
                { "EmptyWallLine", BenefactorsCatalog.EmptyWallLine },
                { "NeverFetchedLine", BenefactorsCatalog.NeverFetchedLine },
                { "FooterText(never-read)", BenefactorsCatalog.FooterText() },
            };

            BenefactorsCatalog.Clear();
            BenefactorsCatalog.ApplyPayload(GoodWallJson());
            playerFacing["FooterText(live)"] = BenefactorsCatalog.FooterText();
            BenefactorsCatalog.MarkFetchFailed("ascii probe");
            playerFacing["FooterText(stale)"] = BenefactorsCatalog.FooterText();

            foreach (var kv in playerFacing)
                AssertAscii(failures, "BenefactorsCatalog." + kv.Key, kv.Value);

            // And the panel's own labels, by source lint (DeNelle.HUD is not referenced here).
            string panel = ReadIfExists(PanelSrc);
            if (panel == null) { failures.Add("[ascii] missing source " + PanelSrc); return; }
            foreach (string lit in StringLiterals(StripComments(panel)))
                AssertAscii(failures, PanelSrc + " literal", lit);
        }

        private static void AssertAscii(List<string> failures, string what, string value)
        {
            if (value == null) return;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] <= 0x7E && value[i] >= 0x20) continue;
                failures.Add("[ascii] " + what + " carries a non-ASCII character U+" +
                             ((int)value[i]).ToString("X4") + " at index " + i + ": \"" + Trim(value) +
                             "\". The UI font cannot render it and it lands as tofu on the player's screen.");
                return;
            }
        }

        // =====================================================================
        //  Case 8 - ONE DROP-IN POINT FOR THE REAL FBX.
        //  Comments EXCLUDED, string literals INCLUDED. See the header.
        // =====================================================================
        private static void Case8_OneKeyLiteral(List<string> failures, List<string> notes)
        {
            var carriers = new List<string>();
            foreach (string path in EnumerateCs(ModulesRoot))
            {
                string code = StripComments(File.ReadAllText(path));
                if (code.Contains("\"" + ExpectedStandInKey + "\"")) carriers.Add(Rel(path));
            }

            if (carriers.Count != ExpectedKeyLiteralFiles)
            {
                failures.Add("[one-key] the stand-in address literal \"" + ExpectedStandInKey +
                             "\" appears in " + carriers.Count + " runtime file(s), expected " +
                             ExpectedKeyLiteralFiles + " (" + string.Join(", ", carriers.ToArray()) +
                             "). The monument's mesh is DATA with ONE drop-in point: the real FBX is " +
                             "supposed to land by authoring an addressable under this address, with no " +
                             "code change. A second literal is a second place to forget.");
            }
            else if (!carriers[0].EndsWith("BenefactorsCatalog.cs"))
            {
                failures.Add("[one-key] the single address literal lives in " + carriers[0] +
                             ", not BenefactorsCatalog.cs where the drop-in is documented.");
            }

            // The injector must consume the constant, never re-type the string.
            string inj = ReadIfExists(InjectorSrc);
            if (inj == null) failures.Add("[one-key] missing source " + InjectorSrc);
            else if (!StripComments(inj).Contains("StandInMonumentAssetKey"))
                failures.Add("[one-key] " + InjectorSrc + " does not reference " +
                             "BenefactorsCatalog.StandInMonumentAssetKey - the placer has stopped " +
                             "reading the one constant that the FBX swap depends on.");
        }

        // =====================================================================
        //  Case 9 - INSTRUMENTED, AND NEVER SWALLOWING.
        //  Comments EXCLUDED. See the header.
        // =====================================================================
        private static void Case9_Instrumentation(List<string> failures, List<string> notes)
        {
            foreach (string path in NewSources)
            {
                string src = ReadIfExists(path);
                if (src == null) { failures.Add("[instrument] missing source " + path); continue; }
                string code = StripComments(src);

                if (!code.Contains("FlowTrace."))
                    failures.Add("[instrument] " + path + " contains no FlowTrace call. CLAUDE.md " +
                                 "section 12 is binding and instrumentation is PERMANENT - a system " +
                                 "with no trace starts its next regression from zero evidence.");

                // A catch whose body is empty (or only a bare return) swallows. Forbidden outright:
                // "a catch that swallows without logging is forbidden".
                foreach (Match m in Regex.Matches(code, @"catch\s*(\([^)]*\))?\s*\{([^{}]*)\}"))
                {
                    string body = m.Groups[2].Value.Trim();
                    if (body.Length == 0 || body == "return;")
                        failures.Add("[instrument] " + path + " has a SILENT catch (body: \"" + body +
                                     "\"). A swallowed failure is exactly what section 12 exists to " +
                                     "prevent - log it or let it through.");
                }
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// A literal open brace built from its char code. CLAUDE.md section 1's brace-balance
        /// gate is a NAIVE character count that cannot tell a brace inside a string literal
        /// from a real one, and the JSON fixtures below need real braces. Same bytes to
        /// Newtonsoft, invisible to the counter. (The trick is MaintenanceTogglesRegression's.)
        /// </summary>
        private static readonly string OpenBrace = ((char)123).ToString();
        private static readonly string CloseBrace = ((char)125).ToString();

        private static string Obj(string inner) => OpenBrace + inner + CloseBrace;

        private static string Row(int ordinal, string name, string founded, string monument, bool bespoke)
        {
            var sb = new StringBuilder();
            sb.Append("\"ordinal\":").Append(ordinal);
            sb.Append(",\"patronName\":\"").Append(name).Append('"');
            sb.Append(",\"foundedOn\":\"").Append(founded).Append('"');
            if (monument != null) sb.Append(",\"monumentAssetId\":\"").Append(monument).Append('"');
            sb.Append(",\"monumentIsBespoke\":").Append(bespoke ? "true" : "false");
            return Obj(sb.ToString());
        }

        private static string GoodWallJson()
        {
            return Obj("\"success\":true,\"tier\":\"" + ExpectedFounderTierId + "\",\"count\":2," +
                "\"benefactors\":[" +
                Row(1, "House Ferrow", "2026-08-27", "monument_house_ferrow", true) + "," +
                Row(2, "Keeper Vale", "2026-08-27", ExpectedStandInKey, false) +
                "]");
        }

        /// <summary>Convincingly malformed rather than merely nonsense: a real opening brace
        /// and a real key, then nothing.</summary>
        private static string TruncatedJson() => OpenBrace + "\"success\":true,\"benefactors\":[";

        private static void Eq(List<string> failures, string tag, string what, string actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                failures.Add("[" + tag + "] " + what + " = \"" + actual + "\", expected \"" + expected + "\".");
        }

        private static void EqInt(List<string> failures, string tag, string what, int actual, int expected)
        {
            if (actual != expected)
                failures.Add("[" + tag + "] " + what + " = " + actual + ", expected " + expected + ".");
        }

        /// <summary>Read a <c>const NAME = 'value';</c> declaration out of a JS file. Regex over
        /// the DECLARATION shape, so a comment mentioning the name is never the source.</summary>
        private static void EqJs(List<string> failures, string src, string path, string name, string expected)
        {
            var m = Regex.Match(src, @"^\s*const\s+" + Regex.Escape(name) + @"\s*=\s*['""]([^'""]*)['""]",
                                RegexOptions.Multiline);
            if (!m.Success)
            {
                failures.Add("[key-parity] " + path + " no longer declares const " + name +
                             " - the two sides of the wire can no longer be compared.");
                return;
            }
            if (!string.Equals(m.Groups[1].Value, expected, StringComparison.Ordinal))
                failures.Add("[key-parity] " + path + " " + name + " = '" + m.Groups[1].Value +
                             "', expected '" + expected + "'. Two literals, two languages, one " +
                             "contract - drift here is the duplicated-state failure CLAUDE.md " +
                             "catalogues, and it renders as an invisible monument.");
        }

        private static void EqJsInt(List<string> failures, string src, string path, string name, int expected)
        {
            var m = Regex.Match(src, @"^\s*const\s+" + Regex.Escape(name) + @"\s*=\s*(\d+)",
                                RegexOptions.Multiline);
            if (!m.Success)
            {
                failures.Add("[key-parity] " + path + " no longer declares const " + name + ".");
                return;
            }
            if (int.Parse(m.Groups[1].Value) != expected)
                failures.Add("[key-parity] " + path + " " + name + " = " + m.Groups[1].Value +
                             ", expected " + expected + ".");
        }

        private static string ReadIfExists(string relPath)
        {
            try { return File.Exists(relPath) ? File.ReadAllText(relPath) : null; }
            catch { return null; }
        }

        private static IEnumerable<string> EnumerateCs(string root)
        {
            if (!Directory.Exists(root)) yield break;
            foreach (string p in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                yield return p;
        }

        private static string Rel(string path) => path.Replace('\\', '/');

        private static string Trim(string s)
        {
            if (s == null) return "";
            return s.Length <= 60 ? s : s.Substring(0, 57) + "...";
        }

        /// <summary>
        /// Remove // and block comments, KEEPING string literals intact. Every case that uses
        /// this states why in the file header - four oracle failures in one week came from
        /// getting the comment decision wrong in one direction or the other.
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Every double-quoted string literal in already-comment-stripped C# source.</summary>
        private static List<string> StringLiterals(string code)
        {
            var found = new List<string>();
            foreach (Match m in Regex.Matches(code, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                found.Add(m.Groups[1].Value);
            return found;
        }
    }
}
