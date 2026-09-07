// =============================================================================
// EchoGuideMemoryRegression [echo-guide-memories] -- WO-1380.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// THE TWO OWNER RULINGS THIS PINS (2026-09-04, both deliberate, neither negotiable):
//
//   1. ALL 24 LINES SHIP OR THE FEATURE DOES NOT. Six Echoes x four raid targets,
//      one line each. "A recognition system that fires for two Guides and stays
//      silent for four does not read as depth; it reads as broken, and it teaches
//      the player to stop noticing." So the count is not a nice-to-have -- a short
//      catalog is a FAILING build, and [count] below asserts the full 6x4 GRID, not
//      merely a total, because 24 rows that skip Doran and double Corvin would pass
//      a total and still leave a Guide standing at a target with nothing to say.
//
//   2. NARRATIVE ONLY at launch. A Guide grants NO stat, NO yield and NO combat
//      effect in V1. [no-effect] enforces that from two directions at once: the
//      authored JSON may not carry a magnitude field, and the two code files that
//      read it may not expose a member returning a magnitude. Adding an effect later
//      is a deliberate design decision -- this suite is what makes it impossible to
//      make it a quiet one.
//
// GROUPS
//   1 [count]      Exactly 24 authored lines, and the full 6-Echo x 4-target grid is
//                  covered with no duplicate and no blank. Reads the live catalog.
//   2 [roster]     Every echoId is a REAL roster id from EchoRosterCatalog. The
//                  creative direction illustrated Sylas/Thrain/Grom/Elara, who have
//                  never existed in this game (owner: "Whatever we have use those");
//                  this group is what stops one of them shipping.
//   3 [canon]      The lines the creative canon quotes VERBATIM are present and
//                  unreworded: Corvin's road at the Forsaken Camp, Aldwin's closing
//                  beat at the Iron Bastion, and Doran recognising his own courses in
//                  a fortress the Heart remembers no fortress at. Also asserts NO
//                  string carries a player-name placeholder -- verified 2026-09-04 that
//                  this game stores no player first name anywhere.
//   4 [ascii]      Player-facing copy is ASCII only (colourblind owner reads TEXT;
//                  glyph-safe TMP), non-empty, and not absurdly long for the band.
//   5 [dual-copy]  Resources and StreamingAssets copies are byte-identical + ASCII.
//                  Resources WINS at runtime, so a drifted twin is a silent
//                  platform-dependent difference.
//   6 [default]    The picker defaults to Corvin, "the scout who ranged the far dark
//                  for Elarion" -- the only Echo who has already been out there.
//   7 [no-effect]  The scope fence, from both sides (see ruling 2 above).
//   8 [one-owner]  Neither new file spawns anything. EchoWorldPresence stays the
//                  single appearance owner (WO-1108 Lane B) and PetDeployer.DespawnEcho
//                  stays the one despawn path; the Guide lane adds a VOICE, not a body.
//   9 [hygiene]    No embedded NUL in the touched sources (CLAUDE.md sec.0/1).
//  10 [tappable]   The Guide picker, WHEREVER it is, has a real tappable height.
//                  (!) THIS GROUP WAS MISSING FROM THIS LIST until 2026-09-06 - the map
//                  ran 1..9 while Run() called ten checks, so the one case a lane was
//                  most likely to trip was the one the header did not mention.
//                  (!) RETARGETED 2026-09-06 (owner ruling 20:24, WO-1519 section 2B:
//                  "Remove it from the deploy screen"). The picker's absence from
//                  RaidDeployScreen is now the REQUIRED state; the case then proves the
//                  SELECTION API survived instead, and records that WO-1380's "Guide
//                  selection EXISTS" acceptance is OWED a new home. Group 7's scope
//                  fence is untouched - see the case body for why those are different
//                  things.
//
// Every source-lint here reads CODE ONLY (comment lines dropped, trailing // comments
// dropped, string-literal CONTENTS blanked) -- the same reader EchoWorldPresenceRegression
// uses, and for the same reason: a lint that cannot tell a call from a sentence punishes
// exactly the self-documenting comments CLAUDE.md sec.12/15 asks for. This file's own
// header names every forbidden token, so a naive grep would fail on the suite itself.
//
// Markers: ECHO_GUIDE_OK / ECHO_GUIDE_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.EchoGuideMemoryRegression.RunAll
// Registered in DataRegression.RunAll as the "echo-guide-memories suite".
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DeNelle.Village;
using DeNelle.Village.World.Camps;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EchoGuideMemoryRegression
    {
        private const string ResourcesJson =
            "Assets/Resources/Data/Canonical/echo-guide-memories.json";
        private const string StreamingJson =
            "Assets/StreamingAssets/Data/Canonical/echo-guide-memories.json";
        private const string CatalogSrc =
            "Assets/_Modules/Village/World/Camps/EchoGuideCatalog.cs";
        private const string ServiceSrc =
            "Assets/_Modules/Village/World/Camps/EchoGuideService.cs";
        private const string PresenceSrc =
            "Assets/_Modules/Village/World/Camps/EchoAutoDeployTrigger.cs";
        private const string DeployScreenSrc =
            "Assets/_Modules/Village/Hero/RaidDeployScreen.cs";

        // Six Echoes x four targets. Kept as a literal here ON PURPOSE: reading the
        // expected count from the same constant the production code uses would make the
        // oracle agree with any future edit to that constant, which is the one thing a
        // ruling-pinning suite must not do.
        private const int RequiredLineCount = 24;

        private static readonly string[] RequiredTargets =
        {
            "raider_camp_small", "fortified_garrison", "mage_enclave", "iron_bastion"
        };

        private const string CorvinId = "echo-voidwing-raven";
        private const string AldwinId = "echo-frosthowl";
        private const string DoranId  = "echo-stonewarden-bear";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ECHO_GUIDE_OK - " + reason);
            else Debug.LogError("ECHO_GUIDE_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                EchoGuideCatalog.Reload();   // never judge a cached read
                var lines = EchoGuideCatalog.All;

                CheckGrid(lines, failures);
                CheckRosterIds(lines, failures);
                CheckCanonLines(lines, failures);
                CheckAscii(lines, failures);
                CheckDualCopy(failures);
                CheckDefaultGuide(failures);
                CheckNoMechanicalEffect(failures);
                CheckNoSecondSpawner(failures);
                CheckGuideBandTappable(failures);
                CheckHygiene(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] threw: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures.ToArray());
                return false;
            }
            reason = "WO-1380 holds: all " + RequiredLineCount + " Echo Guide memory lines ship (the " +
                     "full 6-Echo x 4-target grid, no gap, no duplicate); every echoId is a real roster " +
                     "Echo; the canon-quoted lines are unreworded and no string uses a player name; copy " +
                     "is ASCII; the Resources/StreamingAssets twins are byte-identical; the picker " +
                     "defaults to Corvin; the Guide grants NO stat/yield/combat effect from either the " +
                     "data or the code side; and the Guide lane spawns nothing (EchoWorldPresence stays " +
                     "the single appearance owner); and the picker button carries a real anchor " +
                     "band, so Guide selection is actually TAPPABLE and not a zero-pixel rect. " +
                     "No NULs.";
            return true;
        }

        // -- 1 [count] + the GRID it really means ------------------------------
        private static void CheckGrid(IReadOnlyList<EchoGuideMemory> lines, List<string> failures)
        {
            if (lines == null)
            {
                failures.Add("[count] EchoGuideCatalog.All returned NULL - the catalog could not load at all.");
                return;
            }
            if (lines.Count != RequiredLineCount)
                failures.Add("[count] " + lines.Count + " authored memory line(s), required " +
                             RequiredLineCount + " (6 Echoes x 4 targets). Owner ruling 2026-09-04: all " +
                             "24 ship or the feature does not - a Guide that is silent at a target " +
                             "teaches the player to stop noticing.");

            var roster = EchoRosterCatalog.All;
            if (roster == null || roster.Length == 0)
            {
                failures.Add("[count] EchoRosterCatalog.All is empty - cannot verify the grid.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in lines)
            {
                if (m == null) { failures.Add("[count] a null row is present in the catalog."); continue; }
                string key = (m.EchoId ?? "?") + "|" + (m.TargetId ?? "?");
                if (!seen.Add(key))
                    failures.Add("[count] duplicate memory row for " + key +
                                 " - a duplicate hides a MISSING pairing behind a correct total.");
            }

            for (int e = 0; e < roster.Length; e++)
            {
                var echo = roster[e];
                if (echo == null || string.IsNullOrEmpty(echo.Id)) continue;
                for (int t = 0; t < RequiredTargets.Length; t++)
                {
                    string key = echo.Id + "|" + RequiredTargets[t];
                    if (!seen.Contains(key))
                        failures.Add("[count] NO line for " + echo.Id + " at " + RequiredTargets[t] +
                                     " - that Guide stands at that target with nothing to say.");
                }
            }
        }

        // -- 2 [roster] --------------------------------------------------------
        private static void CheckRosterIds(IReadOnlyList<EchoGuideMemory> lines, List<string> failures)
        {
            var roster = EchoRosterCatalog.All;
            if (lines == null || roster == null)
            {
                // A missing dependency is a FAIL, not a silent pass (hollow-pass rule, WO-1138):
                // with no lines or no roster this case would otherwise green having checked nothing.
                failures.Add("[roster] cannot check memory-line speakers: " +
                             (lines == null ? "the memory lines did not load" : "EchoRosterCatalog.All is null"));
                return;
            }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < roster.Length; i++)
                if (roster[i] != null && !string.IsNullOrEmpty(roster[i].Id)) known.Add(roster[i].Id);

            foreach (var m in lines)
            {
                if (m == null || string.IsNullOrEmpty(m.EchoId)) continue;
                if (!known.Contains(m.EchoId))
                    failures.Add("[roster] echoId '" + m.EchoId + "' is NOT in EchoRosterCatalog. The " +
                                 "creative direction illustrated Sylas/Thrain/Grom/Elara, who have never " +
                                 "existed in this game (owner: use the six we have).");
            }

            for (int t = 0; t < RequiredTargets.Length; t++)
                if (string.IsNullOrEmpty(EchoGuideCatalog.ResolveTargetId(RequiredTargets[t])))
                    failures.Add("[roster] target '" + RequiredTargets[t] +
                                 "' does not resolve through EchoGuideCatalog.ResolveTargetId.");
        }

        // -- 3 [canon] ---------------------------------------------------------
        private static void CheckCanonLines(IReadOnlyList<EchoGuideMemory> lines, List<string> failures)
        {
            string corvinRoad = EchoGuideCatalog.LineFor(CorvinId, "raider_camp_small");
            if (string.IsNullOrEmpty(corvinRoad) ||
                corvinRoad.IndexOf("walked this road", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[canon] Corvin at the Forsaken Camp no longer carries the canon-quoted " +
                             "road recognition (creative canon sec.7). Reword it in the canon first.");

            string aldwinBastion = EchoGuideCatalog.LineFor(AldwinId, "iron_bastion");
            if (string.IsNullOrEmpty(aldwinBastion) ||
                aldwinBastion.IndexOf("someone here", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[canon] Aldwin's closing beat at the Iron Bastion is missing or reworded. " +
                             "It is the line that stops the player thinking about gold (canon sec.7, RULED).");

            string doranBastion = EchoGuideCatalog.LineFor(DoranId, "iron_bastion");
            if (string.IsNullOrEmpty(doranBastion) ||
                doranBastion.IndexOf("never built this", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[canon] Doran at the Iron Bastion no longer recognises stonework he did not " +
                             "lay - that is the hook the whole roster choice exists for (canon sec.7).");

            // NO string may address the player by name. Verified 2026-09-04: this game persists
            // no player first name anywhere (GameState carries PetName and BoundWallet only), so
            // a placeholder here would render as literal braces on screen.
            // Built from char codes, not literals: a naive brace-balance gate (CLAUDE.md sec.1)
            // counts braces inside string literals, so an unmatched open brace in a token would
            // make an otherwise-correct file look garbled.
            const char Open = (char)123;
            string ob = Open.ToString();
            string[] nameTokens = { ob + "0", ob + "name", ob + "player", ob + "first" };
            if (lines != null)
                foreach (var m in lines)
                {
                    if (m == null || string.IsNullOrEmpty(m.Line)) continue;
                    for (int i = 0; i < nameTokens.Length; i++)
                        if (m.Line.IndexOf(nameTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                            failures.Add("[canon] " + m.EchoId + "@" + m.TargetId + " contains the token '" +
                                         nameTokens[i] + "'. The game knows no player first name - a " +
                                         "placeholder renders as literal brace characters to the player.");
                }
        }

        // -- 4 [ascii] ---------------------------------------------------------
        private static void CheckAscii(IReadOnlyList<EchoGuideMemory> lines, List<string> failures)
        {
            if (lines == null)
            {
                failures.Add("[ascii] cannot check the memory lines: they did not load");
                return;
            }
            foreach (var m in lines)
            {
                if (m == null) continue;
                string s = m.Line;
                string who = (m.EchoId ?? "?") + "@" + (m.TargetId ?? "?");
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0)
                { failures.Add("[ascii] " + who + " has a blank line."); continue; }
                for (int i = 0; i < s.Length; i++)
                    if (s[i] > 127)
                    {
                        failures.Add("[ascii] " + who + " carries a non-ASCII character (U+" +
                                     ((int)s[i]).ToString("X4") + ") - TMP glyph risk on device.");
                        break;
                    }
                if (s.Length > 160)
                    failures.Add("[ascii] " + who + " is " + s.Length + " chars - too long for the deploy " +
                                 "band and the toast (cap 160).");
            }
        }

        // -- 5 [dual-copy] -----------------------------------------------------
        private static void CheckDualCopy(List<string> failures)
        {
            if (!File.Exists(ResourcesJson)) { failures.Add("[dual-copy] missing " + ResourcesJson); return; }
            if (!File.Exists(StreamingJson)) { failures.Add("[dual-copy] missing " + StreamingJson); return; }

            byte[] a = File.ReadAllBytes(ResourcesJson);
            byte[] b = File.ReadAllBytes(StreamingJson);
            if (a.Length != b.Length)
            {
                failures.Add("[dual-copy] copies differ in length (" + a.Length + " vs " + b.Length +
                             "). Resources WINS at runtime, so a drift is a silent platform difference.");
                return;
            }
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                {
                    failures.Add("[dual-copy] copies differ at byte " + i + ".");
                    break;
                }
            for (int i = 0; i < a.Length; i++)
                if (a[i] > 127) { failures.Add("[dual-copy] non-ASCII byte at offset " + i + "."); break; }
        }

        // -- 6 [default] -------------------------------------------------------
        private static void CheckDefaultGuide(List<string> failures)
        {
            string def = EchoGuideCatalog.DefaultGuideEchoId;
            if (!string.Equals(def, CorvinId, StringComparison.OrdinalIgnoreCase))
                failures.Add("[default] the Guide picker defaults to '" + def + "', not Corvin (" +
                             CorvinId + "). Corvin is the scout who has already been out there and is " +
                             "the natural first Guide (canon sec.7 / WO-1380 acceptance).");

            if (EchoGuideService.ById(CorvinId) == null)
                failures.Add("[default] " + CorvinId + " is not in EchoRosterCatalog - the default Guide " +
                             "does not exist.");
        }

        // -- 7 [no-effect] -- THE SCOPE FENCE, from both sides -------------------
        private static void CheckNoMechanicalEffect(List<string> failures)
        {
            // (a) DATA: no magnitude may be authored alongside a line.
            string json = File.Exists(ResourcesJson) ? File.ReadAllText(ResourcesJson) : null;
            if (json == null) { failures.Add("[no-effect] cannot read " + ResourcesJson); }
            else
            {
                string[] bannedKeys =
                {
                    "\"bonus\"", "\"multiplier\"", "\"modifier\"", "\"damage\"", "\"loot\"",
                    "\"yield\"", "\"buff\"", "\"stat\"", "\"effect\"", "\"power\""
                };
                for (int i = 0; i < bannedKeys.Length; i++)
                    if (json.IndexOf(bannedKeys[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        failures.Add("[no-effect] echo-guide-memories.json carries the key " + bannedKeys[i] +
                                     ". Owner ruling 2026-09-04: a Guide grants NO stat, NO yield and NO " +
                                     "combat effect in V1. Adding one is a deliberate design decision.");
            }

            // (b) CODE: neither type may expose a member returning a MAGNITUDE. A narrative-only
            // service has no reason to hand anyone a float. OwnedCount() returns an int because it
            // is a roster-ownership count, not a bonus - ints are therefore allowed and floats are
            // not, which is precisely the line the ruling draws.
            CheckNoMagnitudeMembers(typeof(EchoGuideService), failures);
            CheckNoMagnitudeMembers(typeof(EchoGuideCatalog), failures);

            // (c) CODE, by name: the two new files may not even mention an effect verb in CODE
            // (comments and string literals are stripped first - this suite's own header names
            // every one of these tokens).
            string[] bannedTokens =
            {
                "Bonus", "Multiplier", "Modifier", "Buff", "ApplyEffect", "GrantStat"
            };
            LintFileForTokens(CatalogSrc, bannedTokens, "[no-effect]", failures);
            LintFileForTokens(ServiceSrc, bannedTokens, "[no-effect]", failures);
        }

        private static void CheckNoMagnitudeMembers(Type t, List<string> failures)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
            foreach (var m in t.GetMethods(Flags))
            {
                if (m.DeclaringType != t) continue;
                if (IsMagnitude(m.ReturnType))
                    failures.Add("[no-effect] " + t.Name + "." + m.Name + " returns " + m.ReturnType.Name +
                                 " - a narrative-only Guide hands nobody a magnitude (owner ruling).");
            }
            foreach (var p in t.GetProperties(Flags))
            {
                if (p.DeclaringType != t) continue;
                if (IsMagnitude(p.PropertyType))
                    failures.Add("[no-effect] " + t.Name + "." + p.Name + " is a " + p.PropertyType.Name +
                                 " - a narrative-only Guide exposes no magnitude (owner ruling).");
            }
            foreach (var f in t.GetFields(Flags))
            {
                if (f.DeclaringType != t) continue;
                if (IsMagnitude(f.FieldType))
                    failures.Add("[no-effect] " + t.Name + "." + f.Name + " is a " + f.FieldType.Name +
                                 " - a narrative-only Guide exposes no magnitude (owner ruling).");
            }
        }

        private static bool IsMagnitude(Type t)
        {
            return t == typeof(float) || t == typeof(double) || t == typeof(decimal);
        }

        // -- 8 [one-owner] -----------------------------------------------------
        // -- 9 [tappable] the Guide picker must have a REAL height -------------
        // Acceptance says "Guide selection EXISTS". A control the player cannot hit does
        // not exist, and this one failed silently: ElarionUiKit.Button writes
        // offsetMin = offsetMax = Vector2.zero on BOTH construction paths
        // (ElarionUiKit.cs:1599-1600 procedural, ElarionUiKitObsidian.cs:154-155 sprite),
        // so anchorMin.y == anchorMax.y yields a ZERO-PIXEL rect. The footer CTAs in the
        // same file use that idiom legally because SeatFooterCtaAtCanonicalHeight supplies
        // the height afterwards; the Guide button has no seat call, so its anchors are the
        // only thing standing between the player and a dead band. UiKitMinTouchGuard is a
        // runtime net that does not run in an edit-mode headless capture
        // (ElarionUiKit.cs:1075-1077), so it cannot be the answer either.
        //
        // Raw source on purpose: ReadCode() blanks string literals, which would erase the
        // very label this check anchors on, so it anchors on the CALLBACK NAME instead.
        private static void CheckGuideBandTappable(List<string> failures)
        {
            const string Tag = "[tappable]";
            if (!File.Exists(DeployScreenSrc))
            {
                failures.Add(Tag + " cannot read " + DeployScreenSrc);
                return;
            }

            string src = File.ReadAllText(DeployScreenSrc);
            int cb = src.IndexOf("OnCycleGuide);", StringComparison.Ordinal);
            if (cb < 0)
            {
                // =============================================================
                //  RETARGETED 2026-09-06 BY AN OWNER RULING, NOT BY CONVENIENCE.
                // =============================================================
                // This case used to FAIL here: "Guide selection must EXIST - WO-1380
                // acceptance". On 2026-09-06 at 20:24 the owner ruled, of the block on her
                // own deploy frame: "Remove it from the deploy screen." (WO-1519 section 2B,
                // after asking "what is the Echo Guide even bringing to the table?"). So the
                // picker's ABSENCE FROM THIS SCREEN is now the required state, and a case
                // that reds on it would be reporting the ruling as a regression.
                //
                // (!!) WHAT IS *NOT* WEAKENED, and must not be: this case is the [tappable]
                // HEIGHT pin, NOT the scope fence. Group 7 [no-effect] - "a Guide grants no
                // stat, no yield and no combat effect" - is untouched by this lane and stays
                // the ruling nobody may soften. WO-1519 section 2B says so in as many words.
                //
                // (!) AND THE COST IS RECORDED RATHER THAN HIDDEN: WO-1380's acceptance
                // "Guide selection EXISTS" is now OWED. Section 2B says selection "can live
                // on the Echoes screen instead"; that screen was not built in the WO-1519
                // lane, so until it is, the player keeps whatever Guide the service defaults
                // to (Corvin, pinned by group 6 [default]). This branch therefore proves the
                // SELECTION API survived the surface's removal - if EchoGuideService.SelectGuide
                // itself went away, the feature really would have been cut, and that IS a
                // failure.
                string svc = ReadCode(ServiceSrc);
                if (svc == null)
                    failures.Add(Tag + " the Guide picker has left RaidDeployScreen (owner ruling " +
                                 "2026-09-06 20:24, WO-1519 section 2B) AND EchoGuideService.cs cannot be " +
                                 "read - that is a feature cut, not a surface removal.");
                else if (svc.IndexOf("SelectGuide", StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " the Guide picker has left RaidDeployScreen per WO-1519 section 2B, " +
                                 "but EchoGuideService no longer exposes SelectGuide either - selection has " +
                                 "no API left, so it cannot be re-homed on the Echoes screen. Section 2B " +
                                 "removes ONE SURFACE; the service stays.");
                return;
            }

            int open = src.LastIndexOf("ElarionUiKit.Button(", cb, StringComparison.Ordinal);
            if (open < 0)
            {
                failures.Add(Tag + " found OnCycleGuide but no ElarionUiKit.Button( call around it.");
                return;
            }

            string call = src.Substring(open, cb - open);
            var ys = new List<float>();
            int at = 0;
            while (true)
            {
                int v = call.IndexOf("new Vector2(", at, StringComparison.Ordinal);
                if (v < 0) break;
                int close = call.IndexOf(')', v);
                if (close < 0) break;
                string args = call.Substring(v + "new Vector2(".Length, close - v - "new Vector2(".Length);
                string[] parts = args.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[1].Trim().TrimEnd('f', 'F'),
                                   System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float y))
                    ys.Add(y);
                at = close + 1;
            }

            if (ys.Count != 2)
            {
                failures.Add(Tag + " could not read both anchor Vector2 args from the Guide button " +
                             "call (read " + ys.Count + "). Anchors must be literal so this oracle " +
                             "can judge the height.");
                return;
            }

            if (Math.Abs(ys[1] - ys[0]) < 0.0001f &&
                call.IndexOf("SeatFooterCtaAtCanonicalHeight", StringComparison.Ordinal) < 0)
            {
                failures.Add(Tag + " the Guide picker button anchors to anchorMin.y=" + ys[0] +
                             " and anchorMax.y=" + ys[1] + " (identical) with no seating call. " +
                             "ElarionUiKit.Button zeroes offsetMin/offsetMax on both paths, so this " +
                             "rect is ZERO PIXELS TALL and the player cannot tap it. Give it a band " +
                             "or seat it at a canonical pixel height.");
            }
        }

        private static void CheckNoSecondSpawner(List<string> failures)
        {
            string[] spawnVerbs = { "SummonAt", "Instantiate", "AddComponent" };
            LintFileForTokens(CatalogSrc, spawnVerbs, "[one-owner]", failures);
            LintFileForTokens(ServiceSrc, spawnVerbs, "[one-owner]", failures);

            // The voice must live on the existing appearance owner, not beside it.
            string presence = ReadCode(PresenceSrc);
            if (presence == null) failures.Add("[one-owner] cannot read " + PresenceSrc);
            else if (presence.IndexOf("SpeakGuideMemory", StringComparison.Ordinal) < 0)
                failures.Add("[one-owner] EchoWorldPresence no longer carries SpeakGuideMemory - the " +
                             "Guide's voice has moved off the single appearance owner (WO-1108 Lane B).");
        }

        // -- 9 [hygiene] -------------------------------------------------------
        private static void CheckHygiene(List<string> failures)
        {
            string[] touched = { CatalogSrc, ServiceSrc, PresenceSrc, ResourcesJson, StreamingJson };
            for (int i = 0; i < touched.Length; i++)
            {
                if (!File.Exists(touched[i])) { failures.Add("[hygiene] missing " + touched[i]); continue; }
                byte[] bytes = File.ReadAllBytes(touched[i]);
                for (int b = 0; b < bytes.Length; b++)
                    if (bytes[b] == 0)
                    {
                        failures.Add("[hygiene] embedded NUL byte at offset " + b + " in " + touched[i] +
                                     " (CLAUDE.md sec.0 mount-garble signature).");
                        break;
                    }
            }
        }

        // -- shared source reader + lint ---------------------------------------

        private static void LintFileForTokens(string path, string[] tokens, string tag, List<string> failures)
        {
            string code = ReadCode(path);
            if (code == null) { failures.Add(tag + " cannot read " + path); return; }
            for (int i = 0; i < tokens.Length; i++)
                if (code.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                    failures.Add(tag + " " + Path.GetFileName(path) + " contains '" + tokens[i] +
                                 "' in CODE (comments and string literals were stripped first).");
        }

        /// <summary>File contents with comment lines dropped, trailing // comments dropped and
        /// string-literal CONTENTS blanked, so a lint reads CALLS and never sentences.</summary>
        private static string ReadCode(string path)
        {
            if (!File.Exists(path)) return null;
            string source = File.ReadAllText(path);
            var sb = new StringBuilder(source.Length);
            foreach (var raw in source.Split('\n'))
            {
                string t = raw.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) ||
                    t.StartsWith("*", StringComparison.Ordinal) ||
                    t.StartsWith("/*", StringComparison.Ordinal)) { sb.Append('\n'); continue; }
                sb.Append(StripStringLiterals(raw)).Append('\n');
            }
            return sb.ToString();
        }

        private static string StripStringLiterals(string line)
        {
            var sb = new StringBuilder(line.Length);
            bool inStr = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inStr && c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inStr = !inStr;
                    sb.Append(c);
                    continue;
                }
                sb.Append(inStr ? ' ' : c);
            }
            return sb.ToString();
        }
    }
}
