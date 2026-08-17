// =============================================================================
// ShippedSurfaceGateRegression [shipped-surface-gate]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only).
// Markers: SHIPPED_SURFACE_GATE_OK (Debug.Log) / SHIPPED_SURFACE_GATE_FAIL (LogError).
// Contract mirrors every other oracle here: public static bool Run(out string reason),
// NEVER throws.
//
// WHAT IT GUARDS: three defects found 2026-08-16, all the SAME shape -- a surface that
// SHIPS in the release APK while believing it was dev-only, off, or wired up.
//
//   A. HERO GAIT FORENSICS ran in the shipping build. HeroGaitForensics
//      (Assets/_Modules/Village/Hero/HeroGaitForensics.cs) self-bootstraps from a
//      RuntimeInitializeOnLoadMethod and its LateUpdate does a 20-field boxed
//      string.Format + a StreamWriter.WriteLine into persistentDataPath/gait-forensics.csv
//      plus a GetCurrentAnimatorClipInfo allocation EVERY FRAME. It gated on a RAW
//      PlayerPrefs read that DEFAULTED ON and was declared in no flag table, in an assembly
//      (DeNelle.Village) with no #if guard and no define constraint -- so it ran on the
//      player's device, ~1,200 boxed structs/sec at 60fps and an unbounded file, with no
//      dev menu, no UI and no URL able to switch it off. CLAUDE.md section 12 forbids
//      STRIPPING the instrumentation, so the fix is a declared flag, DEFAULT OFF. This
//      suite pins both halves: the raw-key read must be gone, and the declared default
//      must still read false.
//
//   B. THE SCREEN-SHAKE TOGGLE WAS INERT IN BOTH DIRECTIONS -- a visibly broken
//      accessibility control. Settings wrote "dotr-settings-screen-shake" and mirrored it
//      onto ScreenShakeSetting.Enabled, which NOTHING under Assets read; the gameplay
//      bridge read "camerashake", which NOTHING wrote. So the ~15 shake sites funnelling
//      through CameraShakeBridge could never be turned off. The fix writes the key
//      gameplay actually reads. It CANNOT be a typed call: DeNelle.Village.asmdef does not
//      reference DeNelle.Settings and DeNelle.Settings references only DeNelle.Core, so
//      the PlayerPrefs key IS the seam -- case 2 exists partly so a future "cleanup" into a
//      direct reference is caught as the regression it would be.
//
//   C. A CRYPTO SWAP PANEL AUTO-SPAWNED, GATED BY NOTHING, in a build deliberately
//      stripped of crypto surfaces. JupiterSwapBootstrap's RuntimeInitializeOnLoadMethod
//      spawned a swap-CTA host in Title / HeroSelect / PetSelect / Dungeon_*, and its UXML
//      ships under Assets/_Modules/Web3/Resources/ so it went out unconditionally -- while
//      store-hardening (Path A) had flipped ff.skrpreview and ff.realmstorepurchase OFF
//      precisely so the build carries no crypto surface. Gated, NOT deleted: whether the
//      CTA should exist is an owner ruling, so the flag keeps it reversible in one value.
//
// WHY THE DEFAULTS ARE READ AS SOURCE TEXT AND MUST STAY SOURCE TEXT:
// FeatureFlags.Get reads PlayerPrefs FIRST and only falls through to the declared default.
// A RUNTIME read returns whatever the machine running the gate happens to have stored --
// green on a clean box, red on a dev box that once toggled it, and never a statement about
// what SHIPS. The DECLARED default is the only deterministic oracle for a FRESH INSTALL.
// Same technique and reason as the RealmStorePurchase pin in
// WalletProviderSelectionRegression. Do NOT "improve" these into runtime checks.
//
// STRIPPER DIRECTION (RegressionSourceText):
//   * BANNED-pattern scans use StripCommentsAndStrings -- blanking prose can only make a
//     "fails when found" check safer.
//   * REQUIRED-pattern scans whose needle is a QUOTED LITERAL (a PlayerPrefs key, a flag
//     name) use StripComments, which blanks comments but leaves string bodies intact. The
//     full stripper would blank the very literal being required and turn the check red.
//
// PROVE EACH CASE BITES (do this before trusting a green):
//   1  In FeatureFlags.cs flip GaitForensics' defaultOn: false -> true. Case 1 goes RED.
//   2  Delete the PlayerPrefs.SetInt("camerashake", ...) line in SettingsModel
//      .ApplyScreenShake. Case 2 goes RED. Re-point WaveMusicController's wave-start shake
//      back to SmartMobileCamera.Instance?.Shake(...) -- case 2 goes RED on the bypass scan.
//   3  Delete the FeatureFlags.JupiterSwap check in JupiterSwapBootstrap.SpawnInScene.
//      Case 3 goes RED.
//
// Registered in DataRegression.RunAll.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.ShippedSurfaceGateRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ShippedSurfaceGateRegression
    {
        // ---------------------------------------------------------------------
        //  Files under test (relative to Application.dataPath)
        // ---------------------------------------------------------------------
        private const string FlagsRel = "_Modules/Core/FeatureFlags.cs";
        private const string GaitRel = "_Modules/Village/Hero/HeroGaitForensics.cs";
        private const string SettingsModelRel = "_Modules/Settings/SettingsModel.cs";
        private const string TowerRel = "_Modules/Village/Buildings/Tower.cs";
        private const string WaveMusicRel = "_Modules/Village/Audio/WaveMusicController.cs";
        private const string JupiterRel = "_Modules/Web3/JupiterSwapBootstrap.cs";

        /// <summary>
        /// The ONLY files allowed to call <c>.Shake(</c> on something other than
        /// CameraShakeBridge, each with the reason it cannot route through the bridge.
        /// Every entry must still gate on <c>CameraShakeBridge.Enabled</c> (case 2 checks
        /// that, so an allow-listed file cannot quietly drop the preference either).
        /// A NEW file taking a direct shake fails the scan -- that is the point.
        /// SHRINK THIS LIST. It is not a place to park a new bypass.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedDirectShakeFiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "HitStopManager.cs",
                  "holds a TYPED, cached ThirdPersonCameraFollow reference (and re-enters itself " +
                  "via Instance?.Shake), so routing through the bridge's reflection resolve would " +
                  "be a per-hit cost for no gain. Gates on CameraShakeBridge.Enabled instead." },
                { "DialogueCommandSink.cs",
                  "authored dialogue always runs with HeroLocomotion.InputSuppressed = true, and " +
                  "the bridge REFUSES in that state -- routing the camera_shake verb through it " +
                  "would silence the scripted beat entirely. Gates on CameraShakeBridge.Enabled." },
            };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SHIPPED_SURFACE_GATE_OK - " + reason);
            else Debug.LogError("SHIPPED_SURFACE_GATE_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== ShippedSurfaceGateRegression [shipped-surface-gate] ===");

            try
            {
                CaseGaitForensicsGated(failures, log);
                CaseScreenShakeSeam(failures, log);
                CaseJupiterSwapGated(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("[shipped-surface-gate] THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "SHIPPED SURFACE GATE OK - gait forensics is declared in FeatureFlags and " +
                         "defaults OFF (no raw ff.gaitforensics read survives); the screen-shake " +
                         "toggle writes the \"camerashake\" key the gameplay bridge reads and no " +
                         "shake site bypasses the bridge un-gated (" + AllowedDirectShakeFiles.Count +
                         " named, Enabled-gated exception(s)); the Jupiter swap bootstrap is " +
                         "flag-gated and FeatureFlags.JupiterSwap defaults OFF.";
                Debug.Log("SHIPPED_SURFACE_GATE_OK\n" + log);
                return true;
            }

            reason = "shipped-surface-gate: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("SHIPPED_SURFACE_GATE_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  CASE 1 - gait forensics: declared flag, default OFF, no raw key read
        // =====================================================================
        private static void CaseGaitForensicsGated(List<string> failures, StringBuilder log)
        {
            log.AppendLine("-- case 1 [gait-forensics] declared + default OFF --");

            // NOTE the strippers: the flag DECLARATION needle contains a quoted literal
            // ("gaitforensics"), so it is a REQUIRED check on StripComments. The raw-key read is
            // a BANNED check but ITS needle is quoted too, so it also uses StripComments -- the
            // hazard it guards against is matching this suite's own prose, and comments are
            // exactly what StripComments blanks.
            string flags = ReadStripComments(FlagsRel, failures, log);
            if (flags != null)
            {
                if (!Regex.IsMatch(flags, @"bool\s+GaitForensics\s*=>"))
                {
                    failures.Add("[shipped-surface-gate] FeatureFlags.GaitForensics is GONE - the per-frame " +
                                 "gait CSV recorder has no declared gate again, so its only off-switch is a " +
                                 "manual PlayerPrefs edit on the device (no dev menu, no UI, no URL).");
                }
                else if (!Regex.IsMatch(flags,
                             @"GaitForensics\s*=>\s*Get\(\s*""gaitforensics""\s*,\s*defaultOn\s*:\s*false\s*\)"))
                {
                    failures.Add("[shipped-surface-gate] FeatureFlags.GaitForensics no longer declares " +
                                 "defaultOn: false - EVERY FRESH INSTALL then runs HeroGaitForensics in the " +
                                 "release APK: a 20-field boxed string.Format + StreamWriter.WriteLine + a " +
                                 "GetCurrentAnimatorClipInfo allocation per frame (~1,200 boxed structs/sec at " +
                                 "60fps) writing an unbounded gait-forensics.csv onto the player's device. " +
                                 "DeNelle.Village has no #if guard and no define constraint, so it is NOT " +
                                 "stripped from a release player the way DeNelle.DevTools is.");
                }
                else log.AppendLine("   FeatureFlags.GaitForensics declared, defaultOn: false  OK");
            }

            string gait = ReadStripComments(GaitRel, failures, log);
            if (gait != null)
            {
                if (Regex.IsMatch(gait, @"PlayerPrefs\s*\.\s*GetInt\s*\(\s*""ff\.gaitforensics"""))
                {
                    failures.Add("[shipped-surface-gate] HeroGaitForensics reads the RAW PlayerPrefs key " +
                                 "\"ff.gaitforensics\" again instead of FeatureFlags.GaitForensics. A raw read " +
                                 "carries its own default (the old one was 1 = ON) and is invisible to the flag " +
                                 "table, so it has no dev menu and no owner-facing toggle - which is precisely " +
                                 "how it shipped ON.");
                }
                else if (!gait.Contains("FeatureFlags.GaitForensics"))
                {
                    failures.Add("[shipped-surface-gate] HeroGaitForensics no longer reads " +
                                 "FeatureFlags.GaitForensics - the bootstrap is either ungated (it ships ON) " +
                                 "or gated on something this oracle cannot see.");
                }
                else log.AppendLine("   HeroGaitForensics reads the flag, not the raw key  OK");

                // CLAUDE.md section 12: instrumentation is PERMANENT. Flagging it off is the fix;
                // deleting the recorder is not. If a future change "cleans up" the FlowTrace/CSV
                // body, the next gait regression starts from zero evidence again.
                if (!gait.Contains("FlowTrace.Step") || !gait.Contains("gait-forensics.csv"))
                {
                    failures.Add("[shipped-surface-gate] HeroGaitForensics lost its instrumentation body " +
                                 "(FlowTrace.Step and/or the gait-forensics.csv writer). CLAUDE.md section 12: " +
                                 "instrumentation is PERMANENT - flag it OFF, never strip it. Stripping turns " +
                                 "the next gait/camera capture back into a guess.");
                }
                else log.AppendLine("   HeroGaitForensics instrumentation still present (flagged off, not stripped)  OK");
            }
        }

        // =====================================================================
        //  CASE 2 - the screen-shake toggle reaches gameplay, and nothing bypasses
        // =====================================================================
        private static void CaseScreenShakeSeam(List<string> failures, StringBuilder log)
        {
            log.AppendLine("-- case 2 [screen-shake] the toggle writes the key gameplay reads --");

            string settings = ReadStripComments(SettingsModelRel, failures, log);
            if (settings != null)
            {
                if (!Regex.IsMatch(settings, @"PlayerPrefs\s*\.\s*SetInt\s*\(\s*""camerashake"""))
                {
                    failures.Add("[shipped-surface-gate] SettingsModel no longer writes PlayerPrefs " +
                                 "\"camerashake\" - the Settings screen-shake toggle is INERT again. It would " +
                                 "write only \"dotr-settings-screen-shake\" + ScreenShakeSetting.Enabled, which " +
                                 "NOTHING under Assets reads, while CameraShakeBridge keeps reading " +
                                 "\"camerashake\", which NOTHING would write. That is a visibly broken " +
                                 "accessibility control. NOTE: a typed call cannot replace this - " +
                                 "DeNelle.Village.asmdef does not reference DeNelle.Settings, and " +
                                 "DeNelle.Settings references only DeNelle.Core. The KEY IS THE SEAM.");
                }
                else log.AppendLine("   SettingsModel writes PlayerPrefs \"camerashake\"  OK");

                if (!settings.Contains("ScreenShakeSetting.Enabled"))
                {
                    failures.Add("[shipped-surface-gate] SettingsModel stopped publishing " +
                                 "ScreenShakeSetting.Enabled - the typed mirror any in-assembly reader uses.");
                }
            }

            string tower = ReadStripComments(TowerRel, failures, log);
            if (tower != null)
            {
                if (!Regex.IsMatch(tower, @"PlayerPrefs\s*\.\s*GetInt\s*\(\s*""camerashake"""))
                {
                    failures.Add("[shipped-surface-gate] CameraShakeBridge (Tower.cs) no longer reads " +
                                 "PlayerPrefs \"camerashake\" - the single shake entry point stopped honouring " +
                                 "the player's comfort preference, so the Settings toggle is inert from the " +
                                 "other end.");
                }
                else log.AppendLine("   CameraShakeBridge reads PlayerPrefs \"camerashake\"  OK");

                if (!Regex.IsMatch(tower, @"bool\s+Enabled\s*=>"))
                {
                    failures.Add("[shipped-surface-gate] CameraShakeBridge.Enabled is GONE - the named " +
                                 "exceptions in " + string.Join(" / ", new List<string>(AllowedDirectShakeFiles.Keys)) +
                                 " have nothing to gate on and silently ignore the preference again.");
                }
                else log.AppendLine("   CameraShakeBridge.Enabled exposed for the named exceptions  OK");
            }

            // The wave-start sting is called out by name because it is the site that was found
            // bypassing the bridge; the generic scan below would also catch a revert, but a named
            // failure says WHICH feel beat lost the setting.
            string waveMusic = ReadStripComments(WaveMusicRel, failures, log);
            if (waveMusic != null && !waveMusic.Contains("CameraShakeBridge.Shake"))
            {
                failures.Add("[shipped-surface-gate] WaveMusicController's wave-start shake no longer routes " +
                             "through CameraShakeBridge - it bypasses the single shake entry point and so " +
                             "ignores the screen-shake setting (the DEF-67 defect, restored).");
            }
            else if (waveMusic != null) log.AppendLine("   WaveMusicController routes through the bridge  OK");

            // ---- the bypass scan (BANNED pattern -> full stripper) -----------
            // Matches any member-access .Shake( call NOT qualified by CameraShakeBridge, in LIVE
            // code across every module. Method DECLARATIONS ("public void Shake(") have no
            // preceding dot and are not matched; ".DoShake(" is not matched either (the char
            // before "Shake" is not a dot).
            string modulesRoot = Path.Combine(Application.dataPath, "_Modules");
            if (!Directory.Exists(modulesRoot))
            {
                failures.Add("[shipped-surface-gate] Assets/_Modules not found - the shake bypass scan " +
                             "could not run at all (this is a FAILURE, not a skip: a scan that cannot read " +
                             "the tree proves nothing).");
                return;
            }

            var bypass = new Regex(@"(?<!CameraShakeBridge)\.Shake\s*\(", RegexOptions.Compiled);
            int scanned = 0, offenders = 0;
            foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                string raw;
                try { raw = File.ReadAllText(file); }
                catch (Exception ex)
                {
                    failures.Add("[shipped-surface-gate] unreadable source " + name + ": " + ex.Message);
                    continue;
                }
                scanned++;
                string live = RegressionSourceText.StripCommentsAndStrings(raw);
                if (!bypass.IsMatch(live)) continue;

                string rel = file.Substring(modulesRoot.Length + 1).Replace('\\', '/');
                if (!AllowedDirectShakeFiles.ContainsKey(name))
                {
                    offenders++;
                    failures.Add("[shipped-surface-gate] SHAKE BYPASS in " + rel + " - it calls .Shake(...) " +
                                 "directly instead of CameraShakeBridge.Shake(...), so that shake ignores the " +
                                 "player's screen-shake accessibility setting. Route it through the bridge, or " +
                                 "- if it genuinely cannot (typed camera handle, or it must fire while hero " +
                                 "input is suppressed) - gate it on CameraShakeBridge.Enabled and add it to " +
                                 "AllowedDirectShakeFiles with the reason.");
                }
                else if (!live.Contains("CameraShakeBridge.Enabled"))
                {
                    offenders++;
                    failures.Add("[shipped-surface-gate] " + rel + " is an ALLOWED direct-shake site but has " +
                                 "dropped its CameraShakeBridge.Enabled gate, so it now ignores the screen-shake " +
                                 "setting outright. Allowed reason on record: " + AllowedDirectShakeFiles[name]);
                }
            }
            if (scanned == 0)
                failures.Add("[shipped-surface-gate] the shake bypass scan read ZERO .cs files under " +
                             "Assets/_Modules - it asserted nothing (hollow pass guard).");
            else if (offenders == 0)
                log.AppendLine("   shake bypass scan: " + scanned + " file(s), 0 un-gated bypass  OK");
        }

        // =====================================================================
        //  CASE 3 - the crypto swap bootstrap is flag-gated, flag defaults OFF
        // =====================================================================
        private static void CaseJupiterSwapGated(List<string> failures, StringBuilder log)
        {
            log.AppendLine("-- case 3 [jupiter-swap] bootstrap gated + default OFF --");

            string flags = ReadStripComments(FlagsRel, failures, log);
            if (flags != null)
            {
                if (!Regex.IsMatch(flags, @"bool\s+JupiterSwap\s*=>"))
                {
                    failures.Add("[shipped-surface-gate] FeatureFlags.JupiterSwap is GONE - the swap-panel " +
                                 "bootstrap has no gate at all again, which is how a crypto CTA host came to " +
                                 "auto-spawn in a build store-hardening had stripped every other crypto surface " +
                                 "out of (ff.skrpreview and ff.realmstorepurchase both OFF).");
                }
                else if (!Regex.IsMatch(flags,
                             @"JupiterSwap\s*=>\s*Get\(\s*""jupiterswap""\s*,\s*defaultOn\s*:\s*false\s*\)"))
                {
                    failures.Add("[shipped-surface-gate] FeatureFlags.JupiterSwap no longer declares " +
                                 "defaultOn: false - every FRESH INSTALL then spawns the Jupiter swap-panel " +
                                 "host in Title / HeroSelect / PetSelect / Dungeon_*, contradicting the " +
                                 "store-hardening ruling. (It is also a UXML panel, and UXML does not render " +
                                 "in player builds, so on device it would most likely draw blank.)");
                }
                else log.AppendLine("   FeatureFlags.JupiterSwap declared, defaultOn: false  OK");

                // The store-hardening siblings are what make this flag's default coherent; if one of
                // them flips, the reasoning recorded on JupiterSwap silently stops being true.
                if (!Regex.IsMatch(flags, @"SkrPreview\s*=>\s*Get\(\s*""skrpreview""\s*,\s*defaultOn\s*:\s*false\s*\)"))
                    failures.Add("[shipped-surface-gate] FeatureFlags.SkrPreview stopped defaulting OFF - the " +
                                 "zero-crypto store build now ships the 'Powered with SKR' marketing badge. " +
                                 "(Pinned here because JupiterSwap's OFF default cites this ruling; the pack " +
                                 "purchase rail has its own pin in [wallet-provider].)");
            }

            string jup = ReadStripComments(JupiterRel, failures, log);
            if (jup != null)
            {
                if (!jup.Contains("FeatureFlags.JupiterSwap"))
                {
                    failures.Add("[shipped-surface-gate] JupiterSwapBootstrap no longer checks " +
                                 "FeatureFlags.JupiterSwap - its [RuntimeInitializeOnLoadMethod] spawns the " +
                                 "crypto swap CTA host unconditionally again. Its UXML lives under " +
                                 "Assets/_Modules/Web3/Resources/, so it ships in every build.");
                }
                else log.AppendLine("   JupiterSwapBootstrap checks the flag  OK");

                // Gated, NOT deleted (owner ruling territory) - so the bootstrap must still exist.
                if (!jup.Contains("RuntimeInitializeOnLoadMethod"))
                    failures.Add("[shipped-surface-gate] JupiterSwapBootstrap lost its bootstrap entry point. " +
                                 "Whether the swap CTA should exist AT ALL is an OWNER ruling - the flag exists " +
                                 "so the answer stays reversible in one value, not so the file gets deleted.");
            }
        }

        // =====================================================================
        //  Source helpers - a missing file is a FAILURE, never a silent skip.
        // =====================================================================
        private static string ReadStripComments(string relPath, List<string> failures, StringBuilder log)
        {
            string full = Path.Combine(Application.dataPath, relPath);
            if (!File.Exists(full))
            {
                failures.Add("[shipped-surface-gate] Assets/" + relPath + " is MISSING - the gate it carries " +
                             "cannot be verified (treated as a failure, not a skip).");
                return null;
            }
            try
            {
                // StripComments (not StripCommentsAndStrings): every needle in this suite is a
                // QUOTED literal - a PlayerPrefs key or a flag name - and the full stripper would
                // blank exactly the text being required.
                return RegressionSourceText.StripComments(File.ReadAllText(full));
            }
            catch (Exception ex)
            {
                failures.Add("[shipped-surface-gate] Assets/" + relPath + " unreadable: " +
                             ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }
    }
}
