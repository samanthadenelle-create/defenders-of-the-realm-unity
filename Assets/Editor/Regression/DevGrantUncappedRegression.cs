// =============================================================================
// DevGrantUncappedRegression (audit 2026-08-15) - a DEV resource grant must land
// in FULL. Source-structural, headless, milliseconds, no play mode.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS:
//   Three dev surfaces - AdminOverlay.OnLoadResources, OwnerDevToolsOverlay.
//   GiveResources and HelpMenu.OnGrantResources - resolved the grant by STRING:
//       ecoType.GetMethod("GrantSpendable", int,int,int,int)
//   which routes to EconomyService.GrantInternal(BankGrantKind.EarnedIncome) and
//   is therefore CLAMPED by TownBankCapacity. The owner pressed a button labelled
//   "50,000 wood" into a 2,500-capacity town and ~47,500 evaporated, with a
//   3.2-second throttled toast as the only tell. A dev tool that does not give
//   you what it says it gives you is worse than no dev tool: every economy
//   experiment run through it was silently invalid.
//
//   EconomyService.GrantSpendableUncapped (BankGrantKind.DevHarness) already
//   existed for exactly this and is never clamped. Worse, EconomyService's own
//   doc comment DOCUMENTED the hole - the separate name was chosen so the
//   reflected dev lookups "keep resolving to the capped method unchanged". That
//   is the defect written down as if it were the design.
//
// WHY A SOURCE ORACLE IS THE ONLY ONE THAT CAN CATCH THIS:
//   The binding is reflection BY STRING. No compiler error, no IDE rename, no
//   call-graph analysis and no ordinary source lint can see a dev surface drift
//   back onto the capped method - the C# is valid either way and the failure is
//   silent at runtime. So this suite reads the literal method-name strings the
//   dev surfaces pass to GetMethod, which is precisely the thing that broke.
//
// FOUR INVARIANTS, each a hard failure:
//   1. Every dev surface resolves "GrantSpendableUncapped" ...
//   2. ... and NONE of them resolves the capped "GrantSpendable" by name.
//   3. GrantSpendableUncapped still exists on EconomyService and still routes
//      through the uncapped GrantUncapped seam (not quietly re-pointed).
//   4. The capped GrantSpendable still exists - this fix must never delete the
//      player-facing path, only stop DEV surfaces from using it.
//
// SCOPE NOTE: player-facing income (ResourceCollector, MineNode, EchoService) and
// the paid-pack path (PackStoreVM -> GrantSpendablePurchased, pinned separately by
// TownBankCapRegression) are deliberately OUT of scope. The bank cap is real
// design for those. Only DEV surfaces belong on the uncapped seam.
//
// Contract mirrors the other covenant suites: Run(out string reason).
// Registered in DataRegression.RunAll with the DISTINCT [dev-grant-uncapped] tag.
// Standalone: run-unity-method DeNelle.Editor.Regression.DevGrantUncappedRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class DevGrantUncappedRegression
    {
        private const string EconomyPath = "Assets/_Modules/Village/EconomyService.cs";

        private const string Capped   = "GrantSpendable";
        private const string Uncapped = "GrantSpendableUncapped";

        /// <summary>
        /// Every DEV surface that hands the player resources by pressing a button. Add a row here
        /// the moment a new dev grant panel appears - a surface not listed is a surface not pinned.
        /// </summary>
        private static readonly string[] DevSurfaces =
        {
            "Assets/_Modules/HUD/AdminOverlay.cs",
            "Assets/_Modules/HUD/OwnerDevToolsOverlay.cs",
            "Assets/_Modules/HUD/HelpMenu.cs",
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DEV_GRANT_UNCAPPED_OK - " + reason);
            else Debug.LogError("DEV_GRANT_UNCAPPED_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "dev-surfaces",  () => Case1_DevSurfaces(failures, notes));
                Case(failures, "economy-seams", () => Case2_EconomySeams(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DEV GRANT UNCAPPED OK - all " + DevSurfaces.Length + " dev grant surface(s) reflect " +
                         Uncapped + " and none reflects the town-bank-capped " + Capped + noteStr;
                return true;
            }
            reason = "dev-grant-uncapped FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - what method name does each dev surface actually reflect?
        // =====================================================================
        private static void Case1_DevSurfaces(List<string> failures, List<string> notes)
        {
            foreach (string path in DevSurfaces)
            {
                if (!File.Exists(path))
                {
                    failures.Add("[dev-surfaces] dev grant surface missing: " + path +
                                 " - if it was renamed, re-point this list (do NOT just drop the row)");
                    continue;
                }

                string code;
                try { code = StripComments(File.ReadAllText(path)); }
                catch (Exception ex)
                {
                    failures.Add("[dev-surfaces] could not read " + path + ": " + ex.GetType().Name);
                    continue;
                }

                var names = ReflectedMethodNames(code);
                bool hasUncapped = names.Contains(Uncapped);
                bool hasCapped   = names.Contains(Capped);

                if (hasCapped)
                {
                    failures.Add("[dev-surfaces] " + path + " resolves GetMethod(\"" + Capped + "\") - that is the " +
                                 "TownBankCapacity-CLAMPED grant, so a 50,000 dev grant into a 2,500 bank silently " +
                                 "loses ~95% of itself with only a throttled toast as the tell. Use \"" + Uncapped +
                                 "\" (BankGrantKind.DevHarness), which exists for exactly this");
                }
                if (!hasUncapped)
                {
                    failures.Add("[dev-surfaces] " + path + " does NOT resolve GetMethod(\"" + Uncapped + "\") - the " +
                                 "dev grant is not bound to the uncapped seam. This binding is reflection-BY-STRING, " +
                                 "so nothing else in the build can catch it");
                }
            }
            notes.Add(DevSurfaces.Length + " dev surface(s) scanned");
        }

        // =====================================================================
        //  CASE 2 - both economy seams still exist and still mean what they say
        // =====================================================================
        private static void Case2_EconomySeams(List<string> failures, List<string> notes)
        {
            if (!File.Exists(EconomyPath))
            {
                failures.Add("[economy-seams] EconomyService missing: " + EconomyPath + " - the tree moved; re-point this suite");
                return;
            }

            string src;
            try { src = StripComments(File.ReadAllText(EconomyPath)); }
            catch (Exception ex)
            {
                failures.Add("[economy-seams] could not read " + EconomyPath + ": " + ex.GetType().Name);
                return;
            }

            // ⚠ RETURN-TYPE AGNOSTIC ON PURPOSE. This needle used to be "void " + Uncapped + "(",
            // and it went RED the moment WO-1147 changed the grant family to return the APPLIED
            // basket (void -> ResourceCost) so the Echo silo could stop popping the pre-clamp
            // amount. The method was present and correct; the ORACLE was asserting a signature it
            // never cared about. What this case actually protects is that the method EXISTS and
            // routes through GrantUncapped - the return type is none of its business.
            int at = src.IndexOf(" " + Uncapped + "(", StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add("[economy-seams] EconomyService." + Uncapped + " is GONE - the dev surfaces resolve it " +
                             "by name and would fall back to a null lookup (no grant at all)");
            }
            else
            {
                string body = Slice(src, at, 900);
                if (body.IndexOf("GrantUncapped(", StringComparison.Ordinal) < 0)
                    failures.Add("[economy-seams] " + Uncapped + " no longer routes through GrantUncapped - if it was " +
                                 "re-pointed at the capped seam the dev grants are clamped again under an honest-looking name");
            }

            // Return-type agnostic for the same reason as the Uncapped needle above: WO-1147 moved
            // this whole family from void to ResourceCost (the APPLIED basket) and a signature-shaped
            // needle went red on a method that was present and correct. This case protects that the
            // capped grant still EXISTS - player income must keep flowing through the bank cap -
            // not what it returns.
            if (src.IndexOf(" " + Capped + "(", StringComparison.Ordinal) < 0)
                failures.Add("[economy-seams] the CAPPED EconomyService." + Capped + " is gone - player-facing income " +
                             "(ResourceCollector, MineNode, EchoService) depends on it and the bank cap is real design " +
                             "there; only DEV surfaces were meant to move off it");

            notes.Add("both economy seams present");
        }

        // ---------------------------------------------------------------------
        //  helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// The literal method names passed to GetMethod("...") in this source. Comments are already
        /// stripped by the caller; this reads the STRING LITERAL deliberately, because the string is
        /// the binding - the whole defect lives inside a quoted name no other tool inspects.
        /// </summary>
        private static HashSet<string> ReflectedMethodNames(string code)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            const string token = "GetMethod(\"";
            int at = 0;
            while ((at = code.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
            {
                int start = at + token.Length;
                int end = code.IndexOf('"', start);
                if (end < 0) break;
                found.Add(code.Substring(start, end - start));
                at = end + 1;
            }
            return found;
        }

        /// <summary>
        /// Removes // and block comments while PRESERVING string literals - the literals are the
        /// evidence here, and unstripped prose ("this used to resolve GrantSpendable") would
        /// otherwise read as a live binding and fail the suite on its own explanation.
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, verbatim = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < src.Length) { sb.Append(n); i++; }
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (inStr)
                {
                    sb.Append(c);
                    if (verbatim)
                    {
                        if (c == '"' && n == '"') { sb.Append(n); i++; }
                        else if (c == '"') { inStr = false; verbatim = false; }
                    }
                    else
                    {
                        if (c == '\\' && i + 1 < src.Length) { sb.Append(n); i++; }
                        else if (c == '"') inStr = false;
                    }
                    continue;
                }

                if (c == '@' && n == '"') { inStr = true; verbatim = true; sb.Append(c); sb.Append(n); i++; continue; }
                if (c == '"') { inStr = true; verbatim = false; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                if (c == '/' && n == '/') { inLine = true; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Slice(string s, int at, int len)
        {
            if (at < 0 || at >= s.Length) return string.Empty;
            return s.Substring(at, Math.Min(len, s.Length - at));
        }
    }
}
