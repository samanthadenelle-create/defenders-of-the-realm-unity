// =============================================================================
// ShaderPredicateSingleAuthorityRegression [shader-predicate-authority]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Source-lint only - no scene, no play mode.
//
// THE DEFECT THIS PINS (proven 2026-08-02):
//   The predicate "is this shader broken / would it render magenta" had been
//   COPY-PASTED into THREE runtime files, and the copies had already DRIFTED:
//     1. MagentaGuard.IsBrokenShader        - canonical, HAS `!sh.isSupported`
//     2. GhostPreview.IsBrokenGhostShader   - MISSING `!sh.isSupported`
//     3. EquipmentController.IsBrokenPropShader - MISSING `!sh.isSupported`,
//        with a header ("kept local so this silo never edits MagentaGuard")
//        that SANCTIONED the drift in review.
//
//   `!sh.isSupported` is the ANDROID / ON-DEVICE class: a shader that compiles
//   fine in the editor and on desktop but fails against the DEVICE's graphics
//   API keeps its NAME, so every name-only test passes it as "fine" while it
//   renders MAGENTA. With APKs going to real testers, two of the three
//   detectors were structurally blind to the only failure class that appears on
//   the device being tested. A build ghost or an equipped weapon prop could
//   render magenta on Android with nothing detecting or recovering it.
//
// WHY A NAME-BASED CHECK ON THREE KNOWN FILES WOULD BE WORTHLESS: it goes green
// the moment somebody writes a FOURTH copy under a new name. So this suite
// detects BY SHAPE and BY FINGERPRINT, not by file list.
//
// Cases:
//   1 [authority-unique]  Exactly ONE `bool F(Shader)` definition in the runtime
//                         tree carries the magenta fingerprint, and it lives in
//                         MagentaGuard. Zero matches also FAILS - a scan that
//                         silently finds nothing is a green check over a hole,
//                         which is the failure mode this batch exists to kill.
//   2 [authority-shape]   The surviving authority is PUBLIC (call sites can
//                         reach it) and still tests isSupported + null + empty
//                         name + Standard + Legacy + InternalError. A
//                         "consolidation" that quietly drops isSupported is the
//                         exact regression that shipped.
//   3 [sites-routed]      The two consolidated call sites (GhostPreview,
//                         EquipmentController) contain ZERO inline magenta
//                         shader tests in CODE and DO route through
//                         MagentaGuard.IsBrokenShader. This is the revert guard.
//   4 [no-new-inline]     Census of the WHOLE runtime tree for inline magenta
//                         shader tests. Pre-existing unconsolidated debt is
//                         listed explicitly in KnownInlineDebt below (each entry
//                         is a real file that still hand-rolls the test - see
//                         the notes there). ANY OTHER file that starts testing a
//                         shader for brokenness inline FAILS. Ratchet, not a
//                         snapshot: it cannot go red on arrival and it cannot
//                         stay green when a fourth copy is written.
//   5 [detector-alive]    The census itself matched a non-trivial number of
//                         files. If the token set or the comment stripper ever
//                         rots, cases 1/3/4 would all pass vacuously.
//
// SCOPE = Assets/_Modules (the RUNTIME tree, the code that ships in the APK).
//   Assets/Editor/* is deliberately EXCLUDED: those are asset-authoring tools
//   (MagentaMaterialFixer, LanaUrpMaterialFix, PolyperfectUrpFix, VfxCasterWindow,
//   VillageSceneBuilder.Helpers ...) that rewrite .mat assets at import time and
//   never run on a device. Shader.isSupported there reflects the EDITOR's
//   graphics API, not the phone's, so demanding it would be a false positive.
//   Assets/Tests, Assets/MeshBaker, Assets/GoogleSignIn and other third-party
//   roots are excluded for the same "not our shipped runtime" reason.
//
// COMMENTS ARE STRIPPED before every scan. The words "InternalError" and
// "magenta" appear in dozens of explanatory comments in this codebase (including
// the ones this very change wrote), and a lint that can be satisfied - or
// tripped - by prose is not a lint.
//
// Markers: SHADER_PREDICATE_AUTHORITY_OK / SHADER_PREDICATE_AUTHORITY_FAIL.
// Standalone: run-unity-method
//   DeNelle.Editor.Regression.ShaderPredicateSingleAuthorityRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ShaderPredicateSingleAuthorityRegression
    {
        /// <summary>The runtime tree - the code that actually ships inside the APK.</summary>
        private const string RuntimeRoot = "Assets/_Modules";

        private const string AuthorityFile = "Assets/_Modules/Core/MagentaGuard.cs";
        private const string AuthorityMethod = "IsBrokenShader";

        /// <summary>The consolidated call sites. These are the files the 2026-08-02 fix
        /// touched; if either grows a local predicate back, case 3 says so by name.</summary>
        private static readonly string[] ConsolidatedSites =
        {
            "Assets/_Modules/Village/BuildMode/GhostPreview.cs",
            "Assets/_Modules/Village/Hero/EquipmentController.cs",
        };

        /// <summary>
        /// The copy-paste FINGERPRINT of this particular predicate. Every copy of it in this
        /// codebase - past and present - carries the exact literal "Standard (Specular setup)".
        /// It is a deliberately narrow marker: VFXManager.IsLegacyParticleShader is also a
        /// bool F(Shader) that mentions InternalError, but it answers a DIFFERENT question
        /// ("is this a legacy PARTICLE shader to migrate to URP Particles/Unlit" - it returns
        /// TRUE for Particles/* which MagentaGuard leaves alone), so it must not be swept up
        /// here. Fingerprinting on this literal separates them without a name allowlist.
        /// </summary>
        private const string Fingerprint = "\"Standard (Specular setup)\"";

        /// <summary>
        /// String LITERALS that only ever appear in code when something is hand-testing a
        /// shader for magenta-under-URP brokenness. Deliberately excludes the bare literal
        /// "Standard": dozens of files legitimately call Shader.Find("Standard") or name a
        /// material, and including it made the census 41 files of pure noise.
        /// </summary>
        private static readonly string[] InlineTokens =
        {
            "\"Standard (Specular setup)\"",
            "\"InternalError",
            "\"Hidden/InternalError",
            "\"Legacy Shaders/",
        };

        /// <summary>
        /// PRE-EXISTING unconsolidated debt, recorded 2026-08-02 so case 4 is a RATCHET rather
        /// than a snapshot that is red on arrival. Every file here still hand-rolls a magenta
        /// shader test and is owned by another silo; none was touched by the consolidation.
        /// Removing an entry (after routing that file through MagentaGuard) is always safe -
        /// the case only ever fails on files NOT listed. Adding an entry is a deliberate act
        /// that should require the same argument this header makes.
        ///
        /// Known deltas worth an orchestrator's attention:
        ///   HeroBodySwapper.cs        - carries the FULL fingerprint INLINE (not in a method),
        ///                               missing isSupported. Same on-device blind spot as the
        ///                               two just fixed, on the HERO body path. Strongest
        ///                               candidate for the next consolidation.
        ///   EnvironmentTreeMaterialFixer.cs / TreeOfLifeMaterialFixer.cs
        ///                             - bool SlotNeedsFix(Material): a SUPERSET question
        ///                               (broken shader OR flat-white URP slot). Not a pure
        ///                               duplicate; consolidating needs a design call.
        ///   VFXManager.cs             - IsLegacyParticleShader answers a different question
        ///                               (see Fingerprint above). Correctly separate.
        ///   AutoPilotDriver / AutoPilotProbes
        ///                             - DIAGNOSTIC probes that must classify magenta without
        ///                               recovering it; they intentionally do not share the
        ///                               recovery authority.
        /// </summary>
        // WO-1495 2026-09-06 remove-by 2026-12-06 - eleven files still carrying an inline
        // broken-shader test instead of routing through MagentaGuard.IsBrokenShader. The summary
        // above names each one's consolidation status; by the remove-by, consolidate or go red.
        private static readonly HashSet<string> KnownInlineDebt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/_Modules/Core/EnvironmentTreeMaterialFixer.cs",
            "Assets/_Modules/Core/TreeOfLifeMaterialFixer.cs",
            "Assets/_Modules/Core/TripoMaterialFixer.cs",
            "Assets/_Modules/DevTools/AutoPilotDriver.cs",
            "Assets/_Modules/DevTools/AutoPilotProbes.cs",
            "Assets/_Modules/Village/Arena/BattleArena.cs",
            "Assets/_Modules/Village/Buildings/ProjectileVFXCatalog.cs",
            "Assets/_Modules/Village/Dungeon/PortalVFXController.cs",
            "Assets/_Modules/Village/Hero/HeroArmorVisual.cs",
            "Assets/_Modules/Village/Hero/HeroBodySwapper.cs",
            "Assets/_Modules/Village/Vfx/VFXManager.cs",
        };

        /// <summary>Below this, the census has stopped seeing the codebase (token set or the
        /// comment stripper rotted) and cases 1/3/4 would pass vacuously. Set well under the
        /// 2026-08-02 reading (12 files: the authority + 11 debt entries) so ordinary cleanup
        /// never trips it, but a detector that goes blind does.</summary>
        private const int MinCensusFiles = 6;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SHADER_PREDICATE_AUTHORITY_OK - " + reason);
            else Debug.LogError("SHADER_PREDICATE_AUTHORITY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                var files = ScanRuntimeTree(failures);

                Case(failures, "authority-unique", () => Case1_AuthorityUnique(files, failures, notes));
                Case(failures, "authority-shape", () => Case2_AuthorityShape(files, failures, notes));
                Case(failures, "sites-routed", () => Case3_SitesRouted(files, failures, notes));
                Case(failures, "no-new-inline", () => Case4_NoNewInline(files, failures, notes));
                Case(failures, "detector-alive", () => Case5_DetectorAlive(files, failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "SHADER PREDICATE AUTHORITY OK - exactly one broken-shader predicate exists in " +
                         "the runtime tree (MagentaGuard.IsBrokenShader), it is public and still tests " +
                         "isSupported (the Android on-device case), and both consolidated call sites route " +
                         "through it with no inline copy" + noteStr;
                return true;
            }
            reason = "shader-predicate-authority FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  SCAN - one pass over the runtime tree, comments stripped
        // =====================================================================

        private sealed class SourceFile
        {
            public string Path;         // forward-slashed, Assets-relative
            public string Code;         // comment-stripped source
            public List<string> Tokens; // which InlineTokens appear in CODE
        }

        private static List<SourceFile> ScanRuntimeTree(List<string> failures)
        {
            var result = new List<SourceFile>();
            if (!Directory.Exists(RuntimeRoot))
            {
                failures.Add("[scan] runtime root '" + RuntimeRoot + "' does not exist - this oracle is " +
                             "pointed at a tree that moved; every other case below would pass vacuously");
                return result;
            }

            string[] paths;
            try { paths = Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("[scan] could not enumerate '" + RuntimeRoot + "': " + ex.GetType().Name + ": " + ex.Message);
                return result;
            }

            foreach (var raw in paths)
            {
                string norm = Normalize(raw);
                string text;
                try { text = File.ReadAllText(raw); }
                catch (Exception ex)
                {
                    failures.Add("[scan] could not read " + norm + ": " + ex.GetType().Name + ": " + ex.Message);
                    continue;
                }

                string code = StripComments(text);
                var hits = new List<string>();
                foreach (var t in InlineTokens)
                    if (code.IndexOf(t, StringComparison.Ordinal) >= 0) hits.Add(t);

                result.Add(new SourceFile { Path = norm, Code = code, Tokens = hits });
            }
            return result;
        }

        // =====================================================================
        //  CASE 1 - exactly one predicate definition, and it is the authority
        // =====================================================================

        /// <summary>`bool Name(Shader x)` in any access/modifier combination.</summary>
        private static readonly Regex PredicateSig =
            new Regex(@"\bbool\s+(?<name>[A-Za-z_]\w*)\s*\(\s*(?:this\s+)?Shader\s+\w+\s*[,)]", RegexOptions.Compiled);

        private static void Case1_AuthorityUnique(List<SourceFile> files, List<string> failures, List<string> notes)
        {
            var found = new List<string>();   // "path::Method"
            foreach (var f in files)
            {
                foreach (Match m in PredicateSig.Matches(f.Code))
                {
                    // Body window: from the signature forward. Long enough to cover any
                    // reasonable predicate, short enough not to bleed into the next method.
                    int start = m.Index + m.Length;
                    int len = Math.Min(1400, f.Code.Length - start);
                    if (len <= 0) continue;
                    string body = f.Code.Substring(start, len);
                    if (body.IndexOf(Fingerprint, StringComparison.Ordinal) < 0) continue;
                    found.Add(f.Path + "::" + m.Groups["name"].Value);
                }
            }

            if (found.Count == 0)
            {
                failures.Add("[authority-unique] the detector matched ZERO broken-shader predicates in " +
                             RuntimeRoot + ". This is NOT a pass - MagentaGuard.IsBrokenShader is supposed " +
                             "to match it. Either the authority was deleted/renamed/reshaped, or the " +
                             "fingerprint literal " + Fingerprint + " / the bool-F(Shader) signature regex " +
                             "stopped matching real code. A scan that silently finds nothing is a green " +
                             "check over a hole; re-point the detector deliberately before trusting it");
                return;
            }

            var strays = new List<string>();
            bool sawAuthority = false;
            foreach (var hit in found)
            {
                if (hit.StartsWith(AuthorityFile + "::", StringComparison.OrdinalIgnoreCase))
                {
                    sawAuthority = true;
                    if (!hit.EndsWith("::" + AuthorityMethod, StringComparison.Ordinal))
                        notes.Add("authority file also defines " + hit);
                }
                else strays.Add(hit);
            }

            if (!sawAuthority)
                failures.Add("[authority-unique] no broken-shader predicate was found in the authority file " +
                             AuthorityFile + ", yet " + found.Count + " exist elsewhere (" +
                             string.Join(", ", found) + ") - the authority has moved or been deleted and the " +
                             "copies are now the only definition, which is the drift this suite exists to stop");

            foreach (var s in strays)
                failures.Add("[authority-unique] SECOND broken-shader predicate '" + s + "' - this predicate has " +
                             "exactly one correct home (" + AuthorityFile + "." + AuthorityMethod + "). Every copy " +
                             "ever written in this repo drifted: the GhostPreview and EquipmentController copies " +
                             "both shipped WITHOUT the `!sh.isSupported` branch, which is the only branch that " +
                             "catches a shader that fails to compile on the DEVICE (magenta on Android, clean in " +
                             "the editor). Delete it and call MagentaGuard.IsBrokenShader");

            notes.Add("predicate defs=" + found.Count);
        }

        // =====================================================================
        //  CASE 2 - the surviving authority is reachable and still complete
        // =====================================================================
        private static void Case2_AuthorityShape(List<SourceFile> files, List<string> failures, List<string> notes)
        {
            SourceFile guard = null;
            foreach (var f in files)
                if (string.Equals(f.Path, AuthorityFile, StringComparison.OrdinalIgnoreCase)) { guard = f; break; }

            if (guard == null)
            {
                failures.Add("[authority-shape] " + AuthorityFile + " not found under " + RuntimeRoot +
                             " - the single authority is gone or moved; consolidation cannot be verified");
                return;
            }

            var sig = Regex.Match(guard.Code,
                @"(?<mods>(?:public|internal|private|protected|static|\s)*)\bbool\s+" + AuthorityMethod +
                @"\s*\(\s*Shader\s+\w+\s*\)");
            if (!sig.Success)
            {
                failures.Add("[authority-shape] " + AuthorityFile + " no longer declares `bool " + AuthorityMethod +
                             "(Shader)` - call sites in GhostPreview/EquipmentController bind to exactly this " +
                             "signature, so a rename here silently orphans the consolidation");
                return;
            }

            string mods = sig.Groups["mods"].Value;
            if (mods.IndexOf("public", StringComparison.Ordinal) < 0)
                failures.Add("[authority-shape] " + AuthorityMethod + " is not PUBLIC (modifiers read '" +
                             mods.Trim() + "') - an unreachable authority is precisely why the two silos wrote " +
                             "local copies in the first place ('kept local so this silo never edits MagentaGuard')");

            // The body must still answer the whole question. Consolidating INTO a weaker
            // predicate would be the same regression with fewer files.
            int start = sig.Index + sig.Length;
            int len = Math.Min(1400, guard.Code.Length - start);
            string body = len > 0 ? guard.Code.Substring(start, len) : string.Empty;

            var required = new Dictionary<string, string>
            {
                { "isSupported",     "the ANDROID/on-device branch - a shader that fails to compile against the " +
                                     "device graphics API keeps its NAME, so every name-only test below passes it " +
                                     "as fine while it renders MAGENTA. THIS IS THE EXACT REGRESSION THAT SHIPPED" },
                { "== null",         "the null-shader case (a stripped/never-resolved shader)" },
                { "IsNullOrEmpty",   "the empty-name case" },
                { "\"Standard\"",    "the Built-in pipeline Standard shader (magenta under URP)" },
                { "\"Legacy Shaders/\"", "the Legacy Shaders/* family" },
                { "InternalError",   "Hidden/InternalErrorShader - literally the magenta error shader" },
            };
            foreach (var kv in required)
            {
                if (body.IndexOf(kv.Key, StringComparison.Ordinal) < 0)
                    failures.Add("[authority-shape] " + AuthorityMethod + " no longer tests `" + kv.Key + "` - " +
                                 kv.Value + ". Every call site in the runtime tree now depends on this one method, " +
                                 "so a term dropped here goes undetected everywhere at once");
            }

            notes.Add("authority=" + AuthorityFile + "." + AuthorityMethod + " (public, isSupported present)");
        }

        // =====================================================================
        //  CASE 3 - the consolidated sites really route through the authority
        // =====================================================================
        private static void Case3_SitesRouted(List<SourceFile> files, List<string> failures, List<string> notes)
        {
            foreach (var want in ConsolidatedSites)
            {
                SourceFile f = null;
                foreach (var c in files)
                    if (string.Equals(c.Path, want, StringComparison.OrdinalIgnoreCase)) { f = c; break; }

                if (f == null)
                {
                    failures.Add("[sites-routed] " + want + " not found - a consolidated call site moved without " +
                                 "updating this oracle, so its re-drift would no longer be caught");
                    continue;
                }

                if (f.Tokens.Count > 0)
                    failures.Add("[sites-routed] " + want + " tests a shader for brokenness INLINE again (found " +
                                 string.Join(", ", f.Tokens) + " in code, comments stripped) - this file was " +
                                 "consolidated on 2026-08-02 precisely because its local copy had drifted away " +
                                 "from MagentaGuard and lost the on-device isSupported case. Route it back through " +
                                 "MagentaGuard.IsBrokenShader instead of re-inlining the test");

                if (f.Code.IndexOf("MagentaGuard." + AuthorityMethod, StringComparison.Ordinal) < 0)
                    failures.Add("[sites-routed] " + want + " no longer references MagentaGuard." + AuthorityMethod +
                                 " - it either stopped checking shader brokenness at all (a magenta ghost/weapon " +
                                 "prop now ships undetected) or it found some other way to ask, which is a new copy " +
                                 "by another name");
            }

            notes.Add("routed sites=" + ConsolidatedSites.Length);
        }

        // =====================================================================
        //  CASE 4 - ratchet: no NEW file may hand-roll the test
        // =====================================================================
        private static void Case4_NoNewInline(List<SourceFile> files, List<string> failures, List<string> notes)
        {
            var offenders = new List<string>();
            var debtSeen = new List<string>();

            foreach (var f in files)
            {
                if (f.Tokens.Count == 0) continue;
                if (string.Equals(f.Path, AuthorityFile, StringComparison.OrdinalIgnoreCase)) continue;

                bool consolidated = false;
                foreach (var s in ConsolidatedSites)
                    if (string.Equals(f.Path, s, StringComparison.OrdinalIgnoreCase)) { consolidated = true; break; }
                if (consolidated) continue;   // case 3 owns these, with a stricter message

                if (KnownInlineDebt.Contains(f.Path)) { debtSeen.Add(f.Path); continue; }

                offenders.Add(f.Path + " {" + string.Join(",", f.Tokens.ToArray()) + "}");
            }

            foreach (var o in offenders)
                failures.Add("[no-new-inline] NEW inline broken-shader test in " + o + " - the predicate " +
                             "\"would this shader render magenta\" has exactly one home: MagentaGuard." +
                             AuthorityMethod + ". Every hand-rolled copy in this repo's history omitted " +
                             "`!sh.isSupported`, so it works on desktop and is blind on the device the owner " +
                             "ships APKs to. Call the authority. If this file genuinely asks a DIFFERENT question " +
                             "(e.g. VFXManager.IsLegacyParticleShader, which flags Particles/* on purpose), add it " +
                             "to KnownInlineDebt with a one-line reason - deliberately, not silently");

            // Debt that got cleaned up is worth naming so the allowlist can shrink.
            if (debtSeen.Count < KnownInlineDebt.Count)
            {
                var gone = new List<string>();
                foreach (var d in KnownInlineDebt)
                    if (!debtSeen.Contains(d)) gone.Add(d);
                notes.Add("KnownInlineDebt entries no longer inline (safe to delete from the allowlist): " +
                          string.Join(", ", gone.ToArray()));
            }

            notes.Add("inline debt still open=" + debtSeen.Count + "/" + KnownInlineDebt.Count);
        }

        // =====================================================================
        //  CASE 5 - the detector still sees the codebase
        // =====================================================================
        private static void Case5_DetectorAlive(List<SourceFile> files, List<string> failures, List<string> notes)
        {
            if (files.Count == 0)
            {
                failures.Add("[detector-alive] the scan read ZERO .cs files under " + RuntimeRoot +
                             " - every case above passed vacuously. Fix the scan before trusting this suite");
                return;
            }

            int census = 0;
            foreach (var f in files) if (f.Tokens.Count > 0) census++;

            if (census < MinCensusFiles)
                failures.Add("[detector-alive] the magenta-token census matched only " + census + " file(s) under " +
                             RuntimeRoot + " (expected at least " + MinCensusFiles + "; it read 12 on 2026-08-02). " +
                             "Either a large real consolidation happened - in which case lower MinCensusFiles and " +
                             "prune KnownInlineDebt deliberately - or the token set / comment stripper rotted and " +
                             "this suite is now a green check over a hole");

            notes.Add("scanned " + files.Count + " runtime .cs, " + census + " carry magenta tokens in code");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>Assets-relative, forward-slashed, so paths compare identically on any host.</summary>
        private static string Normalize(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int i = p.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? p.Substring(i) : p;
        }

        /// <summary>Strips // and block comments so a lint can neither be satisfied nor tripped by
        /// prose. Load-bearing here: "InternalError", "magenta" and the fingerprint literal all
        /// appear in explanatory comments across this codebase - including the comments the
        /// consolidation itself wrote into GhostPreview and EquipmentController.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
