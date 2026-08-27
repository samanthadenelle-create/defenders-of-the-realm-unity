// =============================================================================
// LookoutAlertRegression — [lookout-alert] (WO-1184).
// -----------------------------------------------------------------------------
// Source-scanning oracle for the lookout presentation lane. Presentation reads
// only. It exists because the failure modes are silent on device:
//   * a UIDocument LOOKOUT REPORT renders blank in player builds (CLAUDE.md §8)
//   * a display-name substring match quietly returns lookout level 0
//   * a panic red bang + "Raid incoming" is the owner bounce ("friendly way to
//     let you know"); the owner is red/green colourblind so words must carry it
//   * pairing a notice with a shield offer is fenced in FOUNDATIONAL_RULINGS
//   * claiming the town is under attack while the player is away is factually
//     false under banked pressure
//
// ZERO TARGETS IS A FAILURE, NOT A PASS.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>WO-1184 lookout presentation oracle.</summary>
    public static class LookoutAlertRegression
    {
        private static readonly string AlertRel = "Assets/_Modules/Village/Waves/AlertIntelSystem.cs";
        private static readonly string ChipRel = "Assets/_Modules/Village/Waves/LookoutNoticeChip.cs";
        private static readonly string PhoneRel = "Assets/_Modules/Village/Siege/RoamingHordeNotifications.cs";
        private static readonly string RoleRel = "Assets/_Modules/Core/Catalog/StructureRole.cs";

        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "lookout-alert: oracle THREW " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool RunCore(out string reason)
        {
            var failures = new List<string>();
            string root = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(root))
            {
                reason = "lookout-alert: could not resolve the project root from Application.dataPath -- " +
                         "the scan cannot run, so this is a FAILURE, not a skip";
                return false;
            }

            int checkedFiles = 0;
            string alert = ReadStripped(root, AlertRel, failures);
            string chip = ReadStripped(root, ChipRel, failures);
            string phone = ReadStripped(root, PhoneRel, failures);
            string role = ReadStripped(root, RoleRel, failures);
            if (alert != null) checkedFiles++;
            if (chip != null) checkedFiles++;
            if (phone != null) checkedFiles++;
            if (role != null) checkedFiles++;
            if (checkedFiles == 0)
            {
                reason = "lookout-alert: scanned 0 target files -- zero targets is a FAILURE, not a pass.";
                return false;
            }

            if (alert != null)
            {
                if (alert.Contains("UIDocument") || alert.Contains("UnityEngine.UIElements") ||
                    alert.Contains("VisualElement") || alert.Contains("new Label"))
                    failures.Add("AlertIntelSystem still hosts on UIDocument/UIToolkit -- that substrate " +
                                 "renders blank in player builds (CLAUDE.md §8 / WO-1182). The lookout " +
                                 "notice must be code-built uGUI.");
                if (alert.Contains("Raid incoming"))
                    failures.Add("AlertIntelSystem still says 'Raid incoming' -- owner bounce 2026-08-27: " +
                                 "the on-screen cue is a friendly tell, not an alarm.");
                if (!alert.Contains("LookoutNoticeChip"))
                    failures.Add("AlertIntelSystem does not use LookoutNoticeChip -- the uGUI helper is the " +
                                 "on-screen substrate.");
                if (alert.Contains("SiegeIntervalMs") && (alert.Contains(" = ") || alert.Contains("SiegeIntervalMs(")))
                    failures.Add("AlertIntelSystem must not write SiegeScheduler cadence (WO-1179 is orthogonal).");
                BanAlarmAndShield("AlertIntelSystem.cs", alert, failures);
            }

            if (chip != null)
            {
                if (chip.Contains("UIDocument") || chip.Contains("UnityEngine.UIElements"))
                    failures.Add("LookoutNoticeChip uses UIDocument/UIToolkit -- it must be code-built uGUI.");
                if (!chip.Contains("ElarionUiKit") || !chip.Contains("ToastTone.Info"))
                    failures.Add("LookoutNoticeChip must render through ElarionUiKit ToastTone.Info " +
                                 "(parchment/gold notice, not a Danger red bang).");
                if (chip.Contains("ToastTone.Danger"))
                    failures.Add("LookoutNoticeChip uses ToastTone.Danger -- that is the panic tell the " +
                                 "owner bounced.");
                if (!chip.Contains("Lookout notice") || !chip.Contains("Horde approaching -- "))
                    failures.Add("LookoutNoticeChip is missing the friendly ASCII copy constants " +
                                 "('Lookout notice' / 'Horde approaching -- ').");
                BanAlarmAndShield("LookoutNoticeChip.cs", chip, failures);
            }

            if (phone != null)
            {
                if (phone.Contains("IndexOf") && (phone.Contains("\"Archer\"") || phone.Contains("\"archer\"") ||
                    phone.Contains("\"Watchtower\"") || phone.Contains("\"watchtower\"")))
                    failures.Add("BestLookoutLevel still matches display-name substrings Archer/Watchtower. " +
                                 "Key off catalog id / StructureRole.Lookout.");
                if (!phone.Contains("tower_ground_archer") || !phone.Contains("StructureRole.Lookout"))
                    failures.Add("RoamingHordeNotifications must key lookouts off catalog id " +
                                 "tower_ground_archer and StructureRole.Lookout.");
                if (!phone.Contains("Horde approaching.") || !phone.Contains("Return to defend live."))
                    failures.Add("Phone copy must stay factual: 'Horde approaching. ... Return to defend live.' " +
                                 "(FOUNDATIONAL_RULINGS -- nothing attacks while the player is away).");
                BanAlarmAndShield("RoamingHordeNotifications.cs", phone, failures);
            }

            if (role != null && !role.Contains("Lookout = \"lookout\""))
                failures.Add("StructureRole.Lookout constant is missing -- BestLookoutLevel keys off the role enum.");

            // Pure-function pins. CatalogRegistry does not need to be loaded: the archer
            // id matches by equality, other towers do not, and an unroled id is not a lookout.
            if (!RoamingHordeNotifications.IsLookoutCatalogId("tower_ground_archer"))
                failures.Add("IsLookoutCatalogId('tower_ground_archer') is FALSE -- that id IS the lookout.");
            if (RoamingHordeNotifications.IsLookoutCatalogId("tower_ballista"))
                failures.Add("IsLookoutCatalogId('tower_ballista') is TRUE -- a ballista is not a lookout.");
            if (RoamingHordeNotifications.IsLookoutCatalogId("tower_catapult"))
                failures.Add("IsLookoutCatalogId('tower_catapult') is TRUE -- a catapult is not a lookout.");
            if (RoamingHordeNotifications.IsLookoutCatalogId("tower_arcane_spire"))
                failures.Add("IsLookoutCatalogId('tower_arcane_spire') is TRUE -- the spire is not a lookout.");
            if (RoamingHordeNotifications.IsLookoutCatalogId(null) ||
                RoamingHordeNotifications.IsLookoutCatalogId(""))
                failures.Add("IsLookoutCatalogId(null/empty) must be false.");

            string live = LookoutNoticeChip.FormatLiveCopy("the north gate", 5, null);
            if (live.IndexOf("Lookout notice", StringComparison.Ordinal) < 0 ||
                live.IndexOf("Horde approaching -- the north gate in 5s.", StringComparison.Ordinal) < 0)
                failures.Add("FormatLiveCopy is not the friendly ASCII notice. Got: " + live);
            if (live.IndexOf("Raid incoming", StringComparison.OrdinalIgnoreCase) >= 0 ||
                live.IndexOf("under attack", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("FormatLiveCopy must not claim a raid/attack. Got: " + live);
            string sized = LookoutNoticeChip.FormatLiveCopy("the north gate", 5, "A warband.");
            if (sized.IndexOf("A warband. Horde approaching -- the north gate in 5s.", StringComparison.Ordinal) < 0)
                failures.Add("Level-3 force-size must prefix the factual approaching line. Got: " + sized);

            if (failures.Count == 0)
            {
                reason = "LOOKOUT ALERT OK -- on-screen chip is code-built uGUI (ElarionUiKit Info, " +
                         "'Lookout notice'); BestLookoutLevel keys catalog id/role not display name; " +
                         "phone copy stays 'Horde approaching / return to defend live'; no shield pairing";
                return true;
            }
            reason = "LOOKOUT ALERT FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void BanAlarmAndShield(string name, string code, List<string> failures)
        {
            string lower = code.ToLowerInvariant();
            if (lower.Contains("under attack") || lower.Contains("losing resources") ||
                lower.Contains("act now or lose"))
                failures.Add(name + " claims offline combat/loss -- FOUNDATIONAL_RULINGS: away time " +
                             "banks pressure; nothing is attacking the player.");
            if (lower.Contains("shield"))
                failures.Add(name + " mentions a shield -- a notification may NEVER be paired with a " +
                             "shield offer (FOUNDATIONAL_RULINGS).");
        }

        private static string ReadStripped(string root, string rel, List<string> failures)
        {
            string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add("'" + rel + "' is MISSING -- this lint is no longer looking at it.");
                return null;
            }
            try { return StripComments(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("'" + rel + "' unreadable: " + ex.Message);
                return null;
            }
        }

        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' '); break; }
                    for (int k = i; k <= end + 1; k++) sb.Append(src[k] == '\n' ? '\n' : ' ');
                    i = end + 1;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
