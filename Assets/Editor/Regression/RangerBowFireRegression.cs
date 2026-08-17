// =============================================================================
// RangerBowFireRegression [ranger-bow-fire]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
// Markers:  RANGER_BOW_FIRE_OK / RANGER_BOW_FIRE_FAIL
//
// THE DEFECT THIS ORACLE EXISTS TO KILL (owner, 2026-08-16):
//   "The ranger has no firing animation. I see a frozen aim pose on every shot."
//
// Ranger.controller bound the SAME motion to all three of its action states --
// Attack, Cast and CastUpper -- and that motion was Ranger_Aim_Idle.fbx, a STATIC
// AIM POSE. The arrow flew (projectiles are a separate system, and they worked),
// so nothing was ever null, nothing threw, and no gate went red. The hero simply
// stood frozen while damage happened. That is the shape of failure this suite is
// built for: a graceful, fully-wired, completely wrong binding.
//
// The project's own data had been SAYING so for months. weaponskill-animations.json
// _notes.ranger read "Ranger is the BIGGEST GAP: only Ranger_Aim_Idle exists ...
// Every ranger skill falls back to the aim pose today", and Ranger_Fire /
// Ranger_Aimed_Shot / Ranger_Rapid_Fire / Ranger_Volley all carried clipExists:false.
// A note is not a gate. This is the gate.
//
// WHAT IT PINS, and why each part is not hollow:
//
//   1. THE CLIP IS REALLY THERE AND REALLY RETARGETABLE.
//      Assets/Action/Archery Shot Away.fbx exists, its .meta is animationType: 3
//      (Humanoid -- the whole reason no rig work was needed), and it exposes a motion
//      take with a stable non-zero internalID. A clip whose internalID is 0 CANNOT be
//      referenced by fileID from a .controller, so this is a precondition of (2) and
//      not decoration.
//
//   2. THE CONTROLLER ACTUALLY BINDS IT -- asserted from the .controller FILE TEXT.
//      Attack, Cast and CastUpper must each bind {fileID = that internalID,
//      guid = that FBX's guid}. Matching the guid ALONE would be hollow: a guid with
//      the wrong fileID resolves to a NULL motion in Unity, which looks exactly like
//      the frozen pose the owner reported. Both halves are checked, and the expected
//      values are READ OUT OF THE .meta FILES rather than typed in here, so a
//      re-import that changes the ids fails loudly instead of silently drifting.
//
//   3. THE AIM POSE IS GONE FROM THE FIRE PATH. Ranger_Aim_Idle's guid must appear
//      ZERO times in Ranger.controller. This is the owner's symptom stated directly.
//
//   4. THE FIRE MOTION IS DISTINCT. The three action states must not share their
//      motion with ANY other state in the controller. Read the header note below on
//      exactly what "distinct" can mean with one bow clip in the project.
//
//   5. THE BUILDER AGREES WITH THE BUILT ASSET. HeroAnimatorFactory's Ranger spec is
//      source-linted so the next `Build Hero Animators` run cannot quietly restore the
//      aim pose and undo (2). Without this the oracle would guard a generated file
//      against its own generator.
//
//   6. THE CANON ROWS MOVED TOO. Every ranger row in weaponskill-animations.json must
//      RESOLVE (clipExists ? clip : fallbackClip) to something that is not
//      Ranger_Aim_Idle -- the doc and the controller cannot be allowed to disagree.
//
// ON "DISTINCT MOTIONS", stated plainly rather than fudged:
//   There is exactly ONE bow-loose clip in the project. Attack, Cast and CastUpper
//   therefore all bind THAT ONE CLIP, and making them artificially differ would mean
//   putting a wrong animation on two of them. So "distinct" is pinned as the property
//   that is both true and load-bearing: the action states bind a motion DISTINCT FROM
//   THE IDLE/POSE MOTIONS and distinct from every other state in the controller -- the
//   collapse onto a shared static pose is what broke, and that is what is now barred.
//   When real draw / rapid-fire / volley clips land, tighten case (4) to require the
//   three to differ from each other as well.
//
// NOT A NEW ANIMATION SELECTOR. This oracle reads the EXISTING ranger path
// (HeroAnimatorFactory's Ranger HeroSpec -> Resources/Heroes/Ranger.controller). The
// project already carries four actor-identity-keyed selectors; a fifth is the bug, so
// this suite deliberately asserts against the existing one instead of introducing any
// lookup of its own.
//
// Deterministic, editor-only file reads. No scene, no PlayMode, no AssetDatabase
// dependency -- it works on a machine that has never opened the project.
//
// Registered in DataRegression.RunAll (covenant style):
//   Guard.Try(... RangerBowFireRegression.Run(out var r) ...)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RangerBowFireRegression
    {
        private const string FlowSys = "RangerBowFire";

        private const string MarkerOk   = "RANGER_BOW_FIRE_OK";
        private const string MarkerFail = "RANGER_BOW_FIRE_FAIL";

        // The built asset the runtime loads via Resources.Load("Heroes/Ranger").
        private const string ControllerPath = "Assets/Resources/Heroes/Ranger.controller";

        // The generator whose Ranger spec produces that controller. Linted so a rebuild
        // cannot undo the binding this suite pins.
        private const string FactorySourcePath = "Assets/Editor/HeroAnimatorFactory.cs";

        // The real bow-loose clip, and the static pose it replaced.
        private const string BowFbxMetaPath = "Assets/Action/Archery Shot Away.fbx.meta";
        private const string BowFbxPath     = "Assets/Action/Archery Shot Away.fbx";
        private const string AimFbxMetaPath = "Assets/Action/Ranger/Ranger_Aim_Idle.fbx.meta";

        // Dual-copy canon (Resources copy WINS at load; StreamingAssets is the mirror).
        private const string CanonResourcesPath      = "Assets/Resources/Data/Canonical/weaponskill-animations.json";
        private const string CanonStreamingPath      = "Assets/StreamingAssets/Data/Canonical/weaponskill-animations.json";

        /// <summary>The action states that must play the shot. These are the exact state names
        /// HeroAnimatorFactory emits for a one-attack-clip class (Attack), for the base-layer
        /// cast (Cast) and for the WO-218 upper-body layer (CastUpper).</summary>
        private static readonly string[] FireStates = { "Attack", "Cast", "CastUpper" };

        /// <summary>The const identifier HeroAnimatorFactory's Ranger spec must reference. Named
        /// rather than inlined precisely so this lint can see it with string literals stripped.</summary>
        private const string ClipConstName = "RangerBowFireClip";

        /// <summary>The literal that const must carry.</summary>
        private const string BowClipBaseName = "Archery Shot Away";

        private const string AimClipBaseName = "Ranger_Aim_Idle";

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Standalone batch entry point.</summary>
        public static void RunStandalone()
        {
            string reason;
            bool pass = Run(out reason);
            Debug.Log("[ranger-bow-fire] standalone result: " + (pass ? "PASS" : "FAIL") + " - " + reason);
        }

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try
            {
                return RunCore(out reason);
            }
            catch (Exception ex)
            {
                reason = "ranger-bow-fire: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError(MarkerFail + " - " + reason);
                return false;
            }
        }

        // =====================================================================
        //  Body
        // =====================================================================
        private static bool RunCore(out string reason)
        {
            using var _scope = FlowTrace.Enter(FlowSys, "RangerBowFireRegression.RunCore");

            var failures = new List<string>();
            var log      = new StringBuilder();
            log.AppendLine("--- RANGER BOW FIRE (a shot must not resolve to the static aim pose) ---");

            // -- 1. the bow clip exists, is Humanoid, and is addressable by fileID ------
            string bowGuid = null, aimGuid = null;
            long bowClipId = 0;

            if (!File.Exists(BowFbxPath))
            {
                FlowTrace.Fail(FlowSys, "bow FBX ABSENT at " + BowFbxPath);
                failures.Add("the bow clip " + BowFbxPath + " is MISSING. It is the ONLY bow-loose " +
                             "animation in the project; without it every ranger shot falls back to " +
                             "Ranger_Aim_Idle, which is the exact frozen-pose defect this suite pins.");
            }

            string bowMeta = ReadOrNull(BowFbxMetaPath);
            if (bowMeta == null)
            {
                failures.Add("cannot read " + BowFbxMetaPath + " - the bow clip's guid and internal clip id " +
                             "are unknown, so this oracle cannot verify the controller binding and must NOT read green.");
            }
            else
            {
                bowGuid = MatchOne(bowMeta, @"^guid:\s*([0-9a-fA-F]{32})\s*$");
                if (bowGuid == null)
                    failures.Add(BowFbxMetaPath + " has no parseable guid line.");

                string animType = MatchOne(bowMeta, @"^\s*animationType:\s*(\d+)\s*$");
                if (animType != "3")
                    failures.Add("Archery Shot Away.fbx animationType is '" + (animType ?? "<absent>") +
                                 "', expected 3 (Humanoid). Only a Humanoid clip retargets onto the ranger " +
                                 "avatar for free; a Generic re-import silently stops the shot from playing.");

                bowClipId = ParseClipInternalId(bowMeta);
                if (bowClipId == 0)
                    failures.Add(BowFbxMetaPath + " exposes no motion take with a stable non-zero internalID. " +
                                 "A clip with internalID 0 CANNOT be referenced by fileID from a .controller, " +
                                 "so the binding would resolve NULL and look exactly like the frozen pose.");
            }

            string aimMeta = ReadOrNull(AimFbxMetaPath);
            if (aimMeta == null)
            {
                // Not fatal on its own: the aim pose being gone is fine. But we then cannot
                // run case 3, and a case we cannot run is a case that must be reported.
                failures.Add("cannot read " + AimFbxMetaPath + " - case 3 (the aim pose is gone from the " +
                             "fire path) cannot be evaluated, and an unevaluated case must not pass silently.");
            }
            else
            {
                aimGuid = MatchOne(aimMeta, @"^guid:\s*([0-9a-fA-F]{32})\s*$");
                if (aimGuid == null)
                    failures.Add(AimFbxMetaPath + " has no parseable guid line.");
            }

            log.Append("bow guid=").Append(bowGuid ?? "<none>")
               .Append(" clipId=").Append(bowClipId)
               .Append("  aim guid=").Append(aimGuid ?? "<none>").AppendLine();

            // -- 2/3/4. the built controller ------------------------------------------
            string controller = ReadOrNull(ControllerPath);
            if (controller == null)
            {
                failures.Add("cannot read " + ControllerPath + " - the ranger's built AnimatorController is " +
                             "missing, so nothing about what the hero actually plays can be asserted.");
            }
            else
            {
                var states = ParseAnimatorStates(controller);
                log.Append("controller states parsed: ").Append(states.Count).AppendLine();

                if (states.Count == 0)
                    failures.Add(ControllerPath + " parsed to ZERO AnimatorState blocks - the parser or the " +
                                 "asset's serialization changed; this oracle would otherwise pass vacuously.");

                foreach (var wanted in FireStates)
                {
                    if (!states.TryGetValue(wanted, out var motion))
                    {
                        failures.Add("Ranger.controller has NO state named '" + wanted + "'. The ranger's fire " +
                                     "path is built from Attack / Cast / CastUpper; a missing one means the " +
                                     "shot has nowhere to play.");
                        continue;
                    }

                    log.Append("  state ").Append(wanted)
                       .Append(" -> fileID=").Append(motion.FileId)
                       .Append(" guid=").Append(motion.Guid ?? "<none>").AppendLine();

                    // Case 3, stated as the owner's symptom.
                    if (aimGuid != null && string.Equals(motion.Guid, aimGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        FlowTrace.Fail(FlowSys, "state '" + wanted + "' still binds Ranger_Aim_Idle");
                        failures.Add("Ranger.controller state '" + wanted + "' STILL binds " + AimClipBaseName +
                                     " - a static aim pose. This is the reported defect verbatim: the hero " +
                                     "freezes mid-aim on every shot while the arrow flies.");
                        continue;
                    }

                    // Case 2, both halves. The guid alone is not enough: a guid with a wrong
                    // fileID resolves NULL, which presents identically to the frozen pose.
                    if (bowGuid != null && !string.Equals(motion.Guid, bowGuid, StringComparison.OrdinalIgnoreCase))
                        failures.Add("Ranger.controller state '" + wanted + "' binds motion guid '" +
                                     (motion.Guid ?? "<none/local>") + "', expected the '" + BowClipBaseName +
                                     "' FBX guid '" + bowGuid + "'.");
                    else if (bowClipId != 0 && motion.FileId != bowClipId)
                        failures.Add("Ranger.controller state '" + wanted + "' points at the right FBX but at " +
                                     "fileID " + motion.FileId + " instead of the clip's internalID " + bowClipId +
                                     ". Unity resolves that to a NULL motion - the hero would freeze exactly as " +
                                     "before, with the asset LOOKING correctly wired.");
                }

                // Case 3, the whole-file form: the pose must not survive anywhere in the fire path.
                if (aimGuid != null)
                {
                    int aimHits = CountOccurrences(controller, aimGuid);
                    log.Append("  Ranger_Aim_Idle guid occurrences in controller: ").Append(aimHits).AppendLine();
                    if (aimHits > 0)
                        failures.Add("Ranger_Aim_Idle's guid still appears " + aimHits + " time(s) in " +
                                     ControllerPath + ". The aim pose is an IDLE; it must not be bound as an action.");
                }

                // Case 4: the fire motion is DISTINCT - it is not shared with any other state.
                if (bowGuid != null)
                {
                    var sharers = new List<string>();
                    foreach (var kv in states)
                    {
                        bool isFire = Array.IndexOf(FireStates, kv.Key) >= 0;
                        if (isFire) continue;
                        if (string.Equals(kv.Value.Guid, bowGuid, StringComparison.OrdinalIgnoreCase))
                            sharers.Add(kv.Key);
                    }
                    if (sharers.Count > 0)
                        failures.Add("the bow-shot motion is ALSO bound by non-action state(s) [" +
                                     string.Join(", ", sharers) + "]. The fire motion must be distinct from the " +
                                     "idle/pose motions - a shot that shares a clip with a stance is how the " +
                                     "frozen-pose defect happened in the first place.");
                }
            }

            // -- 5. the generator agrees with the generated asset ----------------------
            string factoryRaw = ReadOrNull(FactorySourcePath);
            if (factoryRaw == null)
            {
                failures.Add("cannot read " + FactorySourcePath + " - the Ranger spec that REGENERATES the " +
                             "controller cannot be checked, so a rebuild could silently restore the aim pose.");
            }
            else
            {
                // (a) value assertion. The clip NAME is legitimately a string literal, so this one
                //     case strips comments only - stripping literals would erase its subject.
                string noComments = StripComments(factoryRaw);
                var constRx = new Regex(ClipConstName + @"\s*=\s*""" + Regex.Escape(BowClipBaseName) + @"""");
                if (!constRx.IsMatch(noComments))
                    failures.Add(FactorySourcePath + " no longer declares " + ClipConstName + " = \"" +
                                 BowClipBaseName + "\" in code. That const is what the Ranger spec binds; " +
                                 "if its value drifts, the next animator rebuild binds the wrong clip.");

                // (b) structural assertions, with comments AND string literals stripped, so this
                //     can only ever match real code. This file's header discusses the aim pose at
                //     length on purpose; an unstripped lint would match the prose and pass hollow.
                string code = Squash(StripCommentsAndLiterals(factoryRaw));

                if (code.IndexOf("castClip = " + ClipConstName, StringComparison.Ordinal) < 0)
                    failures.Add(FactorySourcePath + " Ranger spec no longer sets castClip = " + ClipConstName +
                                 " in CODE. HeroAbilities.TryCast fires the Cast trigger and the ranger has no " +
                                 "spellCastClips, so this one assignment is what makes ALL FOUR ability slots " +
                                 "loose an arrow.");

                if (code.IndexOf("attackClips = new[] { " + ClipConstName + " }", StringComparison.Ordinal) < 0)
                    failures.Add(FactorySourcePath + " Ranger spec no longer sets attackClips to { " +
                                 ClipConstName + " } in CODE - the Attack-trigger path (troop archers and the " +
                                 "basic attack verb) would go back to the aim pose.");

                // The old broken binding must not reappear anywhere in code.
                string literalAim = "\"" + AimClipBaseName + "\"";
                if (Squash(noComments).IndexOf("attackClips = new[] { " + literalAim + " }", StringComparison.Ordinal) >= 0)
                    failures.Add(FactorySourcePath + " still contains attackClips = new[] { " + literalAim +
                                 " } - the exact binding that produced the frozen-pose defect.");
                if (Squash(noComments).IndexOf("castClip = " + literalAim, StringComparison.Ordinal) >= 0)
                    failures.Add(FactorySourcePath + " still assigns castClip = " + literalAim +
                                 " - a pose cannot be a cast.");
            }

            // -- 6. canon rows resolve to a real shot ---------------------------------
            CheckCanonRows(CanonResourcesPath, failures, log);
            CheckCanonRows(CanonStreamingPath, failures, log);

            string resJson = ReadOrNull(CanonResourcesPath);
            string strJson = ReadOrNull(CanonStreamingPath);
            if (resJson != null && strJson != null && !string.Equals(Squash(resJson), Squash(strJson), StringComparison.Ordinal))
                failures.Add("weaponskill-animations.json Resources and StreamingAssets copies have DIVERGED. " +
                             "The Resources copy wins at load, so a stale mirror hides the real mapping.");

            // -- verdict ---------------------------------------------------------------
            if (failures.Count == 0)
            {
                reason = "ranger fire = '" + BowClipBaseName + "' on " + string.Join("/", FireStates) +
                         "; " + AimClipBaseName + " unbound in Ranger.controller";
                Debug.Log(log + MarkerOk);
                return true;
            }

            reason = "ranger-bow-fire: " + string.Join("; ", failures);
            Debug.LogError(log + MarkerFail + " - " + reason);
            return false;
        }

        // =====================================================================
        //  Canon rows
        // =====================================================================

        /// <summary>Every ranger row must RESOLVE to a clip that is not the aim pose:
        /// clipExists ? clip : fallbackClip. Deliberately checks the resolved value rather than
        /// either field alone - a row may honestly still want an unbuilt Ranger_Volley, it may
        /// just not degrade to a static pose while it waits.</summary>
        private static void CheckCanonRows(string path, List<string> failures, StringBuilder log)
        {
            string json = ReadOrNull(path);
            if (json == null)
            {
                failures.Add("cannot read " + path + " - the ranger animation canon cannot be checked.");
                return;
            }

            // Row-shaped scan: each skills[] entry is one JSON object on a few lines. Pull the
            // ranger objects by their class field, then read the three fields that decide what
            // actually plays. Regex rather than a JSON dependency: this assembly is editor-only
            // and the file's shape is stable and hand-maintained.
            var rowRx = new Regex("\\{[^{}]*\"class\"\\s*:\\s*\"ranger\"[^{}]*\\}", RegexOptions.Singleline);
            var rows = rowRx.Matches(json);
            if (rows.Count == 0)
            {
                failures.Add(path + " contains no ranger rows - either the canon lost them or the row scan " +
                             "broke; either way this case would pass vacuously and must not.");
                return;
            }

            int checkedRows = 0;
            foreach (Match row in rows)
            {
                string body     = row.Value;
                string skill    = MatchOne(body, "\"skill\"\\s*:\\s*\"([^\"]*)\"");
                string clip     = MatchOne(body, "\"clip\"\\s*:\\s*\"([^\"]*)\"");
                string exists   = MatchOne(body, "\"clipExists\"\\s*:\\s*(true|false)");
                string fallback = MatchOne(body, "\"fallbackClip\"\\s*:\\s*\"([^\"]*)\"");
                if (clip == null || exists == null || fallback == null) continue;

                checkedRows++;
                string resolved = exists == "true" ? clip : fallback;
                if (string.Equals(resolved, AimClipBaseName, StringComparison.Ordinal))
                    failures.Add(path + " ranger skill '" + (skill ?? "?") + "' still RESOLVES to " +
                                 AimClipBaseName + " (clipExists=" + exists + ", clip='" + clip +
                                 "', fallbackClip='" + fallback + "'). A ranger skill must never degrade " +
                                 "to the static aim pose now that a real bow shot exists.");
            }

            log.Append("canon ranger rows checked in ").Append(path).Append(": ").Append(checkedRows).AppendLine();
            if (checkedRows == 0)
                failures.Add(path + " yielded 0 parseable ranger rows - vacuous pass refused.");
        }

        // =====================================================================
        //  .controller parsing
        // =====================================================================

        private struct MotionRef
        {
            public long   FileId;
            public string Guid;    // null when the motion is a local object (e.g. a BlendTree) or absent
        }

        /// <summary>Map of AnimatorState m_Name -> its m_Motion reference, read straight out of the
        /// ForceText .controller YAML (EditorSettings m_SerializationMode: 2). No AssetDatabase, so
        /// this runs on a tree that has never been imported.</summary>
        private static Dictionary<string, MotionRef> ParseAnimatorStates(string yaml)
        {
            var map   = new Dictionary<string, MotionRef>(StringComparer.Ordinal);
            var lines = yaml.Replace("\r\n", "\n").Split('\n');

            // !u!1102 is the AnimatorState class id. Blocks run until the next document marker.
            var docRx    = new Regex(@"^---\s+!u!(\d+)\s+&");
            // NOTE: written with two LITERAL leading spaces (an AnimatorState's own fields sit at
            // exactly that indent, nested objects sit deeper) and with '.' standing in for the
            // opening brace of the {fileID: ...} flow map -- an unmatched brace inside a regex
            // literal trips the project's naive brace-balance gate (CLAUDE.md sec.1) for no reason.
            var nameRx   = new Regex(@"^  m_Name:\s*(.*?)\s*$");
            var motionRx = new Regex(@"^  m_Motion:\s*.fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-fA-F]+))?");

            bool inState = false;
            string name  = null;
            MotionRef motion = default;
            bool haveMotion = false;

            void Flush()
            {
                if (inState && !string.IsNullOrEmpty(name) && haveMotion && !map.ContainsKey(name))
                    map[name] = motion;
                inState = false; name = null; motion = default; haveMotion = false;
            }

            foreach (var line in lines)
            {
                var doc = docRx.Match(line);
                if (doc.Success)
                {
                    Flush();
                    inState = doc.Groups[1].Value == "1102";
                    continue;
                }
                if (!inState) continue;

                var nm = nameRx.Match(line);
                if (nm.Success && name == null) { name = nm.Groups[1].Value; continue; }

                var mo = motionRx.Match(line);
                if (mo.Success && !haveMotion)
                {
                    long id;
                    long.TryParse(mo.Groups[1].Value, out id);
                    motion = new MotionRef
                    {
                        FileId = id,
                        Guid   = mo.Groups[2].Success ? mo.Groups[2].Value : null,
                    };
                    haveMotion = true;
                }
            }
            Flush();
            return map;
        }

        // =====================================================================
        //  .meta parsing
        // =====================================================================

        /// <summary>The internalID of the FBX's motion take. Skips bind/T-pose takes by name the same
        /// way MotionClipPicker does, and refuses an id of 0 (unaddressable by fileID).</summary>
        private static long ParseClipInternalId(string meta)
        {
            var lines = meta.Replace("\r\n", "\n").Split('\n');
            var nameRx = new Regex(@"^\s*-?\s*name:\s*(.+?)\s*$");
            var idRx   = new Regex(@"^\s*internalID:\s*(-?\d+)\s*$");

            string pendingName = null;
            long fallback = 0;

            foreach (var line in lines)
            {
                var nm = nameRx.Match(line);
                if (nm.Success) { pendingName = nm.Groups[1].Value; continue; }

                var idm = idRx.Match(line);
                if (!idm.Success) continue;

                long id;
                if (!long.TryParse(idm.Groups[1].Value, out id) || id == 0) { pendingName = null; continue; }

                string n = (pendingName ?? string.Empty).ToLowerInvariant();
                bool isPose = n.Contains("t-pose") || n.Contains("tpose") || n.Contains("bind");
                if (!isPose) return id;
                if (fallback == 0) fallback = id;
                pendingName = null;
            }
            return fallback;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string ReadOrNull(string path)
        {
            string result = null;
            Guard.Try(FlowSys, "read " + path, () =>
            {
                if (File.Exists(path)) result = File.ReadAllText(path);
            });
            return result;
        }

        private static string MatchOne(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var m = Regex.Match(text, pattern, RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int count = 0, idx = 0;
            while (true)
            {
                int hit = haystack.IndexOf(needle, idx, StringComparison.OrdinalIgnoreCase);
                if (hit < 0) break;
                count++;
                idx = hit + needle.Length;
            }
            return count;
        }

        /// <summary>Collapse every run of whitespace to one space so a lint survives reformatting.</summary>
        private static string Squash(string src)
        {
            return string.IsNullOrEmpty(src) ? string.Empty : Regex.Replace(src, @"\s+", " ");
        }

        /// <summary>Blank out // and /* */ comments, keeping string literals. Used only where the
        /// literal IS the subject of the assertion (the clip name).</summary>
        private static string StripComments(string src)
        {
            return StripInternal(src, stripLiterals: false);
        }

        /// <summary>Blank out comments AND every string/char literal, so a lint can only match real
        /// CODE. This file's own header names the aim pose repeatedly on purpose; a lint that could
        /// match prose would pass on the explanation while the code was gone.</summary>
        private static string StripCommentsAndLiterals(string src)
        {
            return StripInternal(src, stripLiterals: true);
        }

        private static string StripInternal(string src, bool stripLiterals)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            var sb = new StringBuilder(src.Length);
            int n = src.Length;
            for (int i = 0; i < n; i++)
            {
                char c = src[i];

                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') i++;
                    sb.Append('\n');
                    continue;
                }
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i++;                       // land on '/', the loop's i++ steps past it
                    sb.Append(' ');
                    continue;
                }
                if (stripLiterals && (c == '"' || c == '\''))
                {
                    char quote = c;
                    i++;
                    while (i < n && src[i] != quote)
                    {
                        if (src[i] == '\\') i++;   // skip the escaped char
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
