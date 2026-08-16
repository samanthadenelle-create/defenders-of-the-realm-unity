// =============================================================================
// HeroEquipHudHubRegression [hero-equip-hub]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE DEFECT THIS EXISTS TO KILL (owner AC, 2026-08-02: "Equip HUD arms on
// Main_Castle_Overworld"):
//
//   HeroEquipHud.IsHubScene was a PRIVATE COPY of the hub list -
//       n == "MainCastle_Hall" || n == "Village2" || n == "CastleHub"
//   - and the live home hub is `Main_Castle_Overworld` (CLAUDE.md sec.7). The scene the
//   player actually plays was not in the list, so the RuntimeInitializeOnLoadMethod
//   bootstrap never called EnsureExists and the bag/inventory button simply did not
//   exist on the hub. A private hub list drifting behind canon is the exact failure
//   DeNelle.Core.HubScenes was created to end (WO-411 root cause A).
//
// HOW THIS SUITE AVOIDS BEING A SECOND COPY OF THE BUG: it does NOT re-declare the hub
// names and check them. It REFLECTS the real private predicate out of the shipping
// HeroEquipHud type and CALLS it. If that method is renamed or deleted, the suite FAILS
// loudly - it never degrades into a no-op that reports OK against a null.
//
// Cases:
//   1 [predicate-live] Reflect HeroEquipHud's real hub predicate and invoke it:
//                      "Main_Castle_Overworld" must be true, and a known non-hub
//                      ("Title") must be false so the predicate is not simply
//                      returning true for everything.
//   2 [no-private-list] Source-lint: the predicate must delegate to HubScenes and must
//                      NOT contain literal scene-name equality comparisons. A private
//                      list that happens to include the right names today is still the
//                      defect - it drifts the next time canon moves.
//   3 [bootstrap-gated] Source-lint: the RuntimeInitializeOnLoadMethod bootstrap really
//                      routes BOTH its sceneLoaded handler AND its active-scene check
//                      through that predicate. A correct predicate nothing calls arms
//                      nothing.
//   4 [canon-list]     DeNelle.Core.HubScenes itself still knows Main_Castle_Overworld
//                      (Names + IsHub). This is the thing case 1 delegates to, so if
//                      canon is removed from HubScenes the delegation quietly stops
//                      working and case 1 alone would not explain why.
//
// NOTE ON WIDENING (deliberate, recorded here so the next reader does not "fix" it):
// HubScenes.IsHub matches by `==` OR `Contains`, i.e. it is a SUBSTRING test and is
// WIDER than the `==` list it replaced - "CastleHub_MainKeep_Backup" now counts. That
// was accepted rather than tightening IsHub globally, because IsHub has ~40 callers and
// every other self-installing injector already gates on it. The worst case of the
// widening here is one extra bag ICON, not a gameplay gate.
//
// Markers: HERO_EQUIP_HUB_OK / HERO_EQUIP_HUB_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HeroEquipHudHubRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor.Regression
{
    public static class HeroEquipHudHubRegression
    {
        private const string HudSrc = "Assets/_Modules/Village/Hero/HeroEquipHud.cs";

        /// <summary>The live home hub (CLAUDE.md sec.7). The whole ticket is that this was missing.
        /// WO-1112: RESOLVED from SceneRouter, never a literal — a hardcoded hub name in a gate
        /// goes stale silently and the gate then proves a predicate about a retired scene.</summary>
        private static string LiveHub => DeNelle.Core.SceneRouter.Castle;

        /// <summary>A scene that must NOT read as a hub, so a predicate stuck on `true` is caught.</summary>
        private const string NotAHub = "Title";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_EQUIP_HUB_OK - " + reason);
            else Debug.LogError("HERO_EQUIP_HUB_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "predicate-live", () => Case1_PredicateLive(failures, notes));
                Case(failures, "no-private-list", () => Case2_NoPrivateList(failures));
                Case(failures, "bootstrap-gated", () => Case3_BootstrapGated(failures));
                Case(failures, "canon-list", () => Case4_CanonList(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "HERO EQUIP HUB OK - HeroEquipHud's real hub predicate returns true for " + LiveHub +
                         " (invoked by reflection, not re-declared), it delegates to DeNelle.Core.HubScenes " +
                         "instead of a private scene list, and the AfterSceneLoad bootstrap gates both its " +
                         "entry points on it" + noteStr;
                return true;
            }
            reason = "hero-equip-hub FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - invoke the REAL predicate the shipping bootstrap calls
        // =====================================================================
        private static void Case1_PredicateLive(List<string> failures, List<string> notes)
        {
            Type t = typeof(DeNelle.Village.HeroEquipHud);

            // The predicate is private static by design; find it by shape (one string in, bool out)
            // rather than by name alone, so a rename is reported instead of silently skipped.
            MethodInfo found = null;
            var candidates = new List<string>();
            foreach (var m in t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            {
                var ps = m.GetParameters();
                if (m.ReturnType != typeof(bool) || ps.Length != 1 || ps[0].ParameterType != typeof(string)) continue;
                candidates.Add(m.Name);
                if (string.Equals(m.Name, "IsHubScene", StringComparison.Ordinal)) found = m;
            }

            if (found == null)
            {
                if (candidates.Count == 1)
                {
                    found = t.GetMethod(candidates[0], BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    notes.Add("hub predicate resolved by shape as '" + candidates[0] + "' (not named IsHubScene)");
                }
                else
                {
                    failures.Add("[predicate-live] HeroEquipHud has no static bool(string) hub predicate named " +
                                 "'IsHubScene' (static bool(string) candidates found: " +
                                 (candidates.Count == 0 ? "<none>" : string.Join(",", candidates)) +
                                 "). This suite must invoke the REAL code path - it refuses to guess, and it " +
                                 "will not pass by testing a duplicate of the hub list");
                    return;
                }
            }

            object live, dead;
            try
            {
                live = found.Invoke(null, new object[] { LiveHub });
                dead = found.Invoke(null, new object[] { NotAHub });
            }
            catch (TargetInvocationException ex)
            {
                failures.Add("[predicate-live] HeroEquipHud." + found.Name + " THREW when invoked: " +
                             (ex.InnerException != null ? ex.InnerException.GetType().Name + ": " +
                              ex.InnerException.Message : ex.Message) +
                             " - in editor batchmode there is no play session, so the predicate must be pure " +
                             "static logic with no singleton/scene dependency");
                return;
            }

            if (!(live is bool) || !(dead is bool))
            {
                failures.Add("[predicate-live] HeroEquipHud." + found.Name + " did not return a bool");
                return;
            }

            if (!(bool)live)
                failures.Add("[predicate-live] HeroEquipHud." + found.Name + "(\"" + LiveHub + "\") is FALSE - " +
                             "the equip HUD does not self-install on the LIVE home hub, so the inventory/bag " +
                             "button does not exist in the scene the player actually plays. This is the owner AC " +
                             "verbatim");

            if ((bool)dead)
                failures.Add("[predicate-live] HeroEquipHud." + found.Name + "(\"" + NotAHub + "\") is TRUE - the " +
                             "predicate now treats a non-hub scene as a hub, so case 1's pass above proves " +
                             "nothing (it would return true for any string)");

            notes.Add("invoked HeroEquipHud." + found.Name + ": " + LiveHub + "=" + live + ", " + NotAHub + "=" + dead);
        }

        // =====================================================================
        //  CASE 2 - it delegates to the canonical list, not a private copy
        // =====================================================================
        private static void Case2_NoPrivateList(List<string> failures)
        {
            string code = ReadStripped(HudSrc, failures, "no-private-list");
            if (code == null) return;

            if (code.IndexOf("HubScenes", StringComparison.Ordinal) < 0)
                failures.Add("[no-private-list] HeroEquipHud.cs never mentions DeNelle.Core.HubScenes - its hub " +
                             "gate is a private list again. That list is what drifted behind canon and hid the " +
                             "bag button on " + LiveHub + "; route the gate through the ONE canonical predicate");

            // Any literal scene-name equality left in code (not comments - those are stripped) means a
            // private list survived somewhere, even if HubScenes is also referenced.
            var lit = Regex.Match(code, "==\\s*\"(?<n>[A-Za-z0-9_]+)\"");
            if (lit.Success)
                failures.Add("[no-private-list] HeroEquipHud.cs still compares a scene name literally (== \"" +
                             lit.Groups["n"].Value + "\") - a private hub list, in whole or in part, is back. " +
                             "Every hub name must come from HubScenes.Names so adding a hub stays ONE edit");
        }

        // =====================================================================
        //  CASE 3 - the bootstrap actually uses the predicate on BOTH paths
        // =====================================================================
        private static void Case3_BootstrapGated(List<string> failures)
        {
            string code = ReadStripped(HudSrc, failures, "bootstrap-gated");
            if (code == null) return;

            if (code.IndexOf("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) < 0)
                failures.Add("[bootstrap-gated] HeroEquipHud has no RuntimeInitializeOnLoadMethod bootstrap - " +
                             "nothing self-installs the equip HUD at all, so the hub predicate is moot");

            // Path A: scenes loaded after boot. Path B: the scene already active at boot (entering
            // play directly in the hub). The original bug needed BOTH, and only one is easy to spot.
            if (code.IndexOf("sceneLoaded", StringComparison.Ordinal) < 0)
                failures.Add("[bootstrap-gated] HeroEquipHud does not subscribe to SceneManager.sceneLoaded - " +
                             "walking into the hub from another scene would never arm the equip HUD");

            if (!Regex.IsMatch(code, @"GetActiveScene\s*\(\s*\)\s*\.name"))
                failures.Add("[bootstrap-gated] HeroEquipHud never checks the ALREADY-ACTIVE scene at boot - " +
                             "starting play directly in the hub (which is how the owner playtests) would never " +
                             "arm the equip HUD even with a correct predicate");

            int gateCalls = Regex.Matches(code, @"IsHubScene\s*\(").Count;
            if (gateCalls < 2)
                failures.Add("[bootstrap-gated] the hub predicate is called " + gateCalls + " time(s) in " +
                             "HeroEquipHud.cs; both bootstrap paths (sceneLoaded AND the active scene at boot) " +
                             "must gate on it, or one entry path arms and the other does not");
        }

        // =====================================================================
        //  CASE 4 - canon itself still knows the live hub
        // =====================================================================
        private static void Case4_CanonList(List<string> failures, List<string> notes)
        {
            bool inNames = false;
            var names = HubScenes.Names;
            if (names != null)
                for (int i = 0; i < names.Length; i++)
                    if (string.Equals(names[i], LiveHub, StringComparison.Ordinal)) inNames = true;

            if (!inNames)
                failures.Add("[canon-list] DeNelle.Core.HubScenes.Names does not contain \"" + LiveHub +
                             "\" - the canonical hub list no longer names the live home hub, so every gate that " +
                             "delegates to it (the equip HUD, the companion injectors, the town HUD, ~40 call " +
                             "sites) silently stops arming there");

            if (!HubScenes.IsHub(LiveHub))
                failures.Add("[canon-list] HubScenes.IsHub(\"" + LiveHub + "\") is FALSE - the delegation target " +
                             "itself is wrong, which no amount of routing at the call site can fix");

            notes.Add("HubScenes.Names = {" + (names != null ? string.Join(",", names) : "<null>") + "}");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>Reads a source file with // and block comments stripped, so a lint can never be
        /// satisfied by prose - this file's own comments name every symbol these cases look for.</summary>
        private static string ReadStripped(string path, List<string> failures, string caseTag)
        {
            if (!File.Exists(path))
            {
                failures.Add("[" + caseTag + "] " + path + " not found - HeroEquipHud moved without updating " +
                             "this oracle; re-point it deliberately rather than deleting the case");
                return null;
            }
            try
            {
                string src = File.ReadAllText(path);
                string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
                return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
            }
            catch (Exception ex)
            {
                failures.Add("[" + caseTag + "] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }
    }
}
