// =============================================================================
// ModalArbiterRegistrationRegression [modal-registration] -- proves top-band modals
// go through the PanelManager arbiter (back-button / battle-lock / one-modal).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. A SOURCE-LINT oracle (same family as the
// UI-Obsidian conformance gate): it reads .cs under Assets/_Modules/**. A screen
// that opens a modal in the top sorting band (>= 31000) but never registers with
// PanelManager is invisible to the arbiter -- the Android back button can't close it,
// the battle-lock can't reject it, and it can double-stack. THE LAW: any type that
// builds a modal at sortingOrder >= 31000 (BuildModalCanvas / BuildObsidianModal /
// canvas.sortingOrder) must reference PanelManager.Register + NotifyOpened +
// NotifyClosed.
//
// NOTE (audit 2026-08-01): the LAW above always said "BuildObsidianModal", but until now the
// DETECTOR only matched numeric literals -- and BuildObsidianModal takes sortingOrder=31000 as a
// DEFAULT PARAMETER, so 13 call sites had no literal and never reached the check. The suite was
// green because it was blind. BuildsTopBandModal now resolves the default (see below).
//
// It ALSO hard-checks the two named fixes -- TowerSwapMenu + WelcomeBackPopup (UIDocument
// panels that set a dynamic top sort) -- register through the arbiter.
//
// Marker: MODAL_REGISTRATION_OK / MODAL_REGISTRATION_FAIL. Expected: RED today (some
// top-band overlays -- e.g. OnboardingFlow -- do not yet register); flips green as
// each is routed through PanelManager.
//
// Wire (DataRegression.RunAll):
//   if (!ModalArbiterRegistrationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[modal-registration] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ModalArbiterRegistrationRegression
    {
        private const int TopBand = 31000;

        // Detect a top-band modal build: BuildModalCanvas(name, NNN) OR any sortingOrder: / = NNN.
        private static readonly Regex ModalCanvas = new Regex(@"BuildModalCanvas\s*\(\s*[^,]+,\s*(\d{4,6})", RegexOptions.Compiled);
        private static readonly Regex SortingOrder = new Regex(@"sortingOrder\s*[:=]\s*(\d{4,6})", RegexOptions.Compiled);

        // ── BLIND-SPOT FIX (UI audit 2026-08-01, F2) ─────────────────────────────────
        // The two regexes above only see a NUMERIC LITERAL. But the kit's one-call modal
        // builder takes the band as a DEFAULT PARAMETER:
        //     ElarionUiKit.BuildObsidianModal(name, title, min, max, onClose,
        //                                     int sortingOrder = 31000, ...)   [ElarionUiKit.cs]
        // so a call site that does NOT pass sortingOrder carries NO literal and was
        // INVISIBLE to this suite -- it reported MODAL_REGISTRATION_OK while unregistered
        // 31000-band modals shipped (SkrShowcasePanel + StakeRewardsPanel: scrim over the
        // world, PanelManager.AnyOpen still false, so the world interact button stayed live
        // UNDER the modal, the Android back button could not close them and BattleLock could
        // not reject them). 13 files were in that blind spot.
        //
        // THE LAW NOW: a BuildObsidianModal( call IS a top-band modal build UNLESS that same
        // call statement explicitly passes a sortingOrder BELOW the band. (Two panels do
        // exactly that today -- RealmMapPanel + RumorBoardPanel pass sortingOrder: 1000 --
        // so a naive "any BuildObsidianModal( is top-band" widening would mis-classify them.)
        private static readonly Regex ObsidianModalCall = new Regex(@"BuildObsidianModal\s*\(", RegexOptions.Compiled);
        private static readonly Regex ExplicitSortArg = new Regex(@"sortingOrder\s*:\s*(\d{1,6})", RegexOptions.Compiled);

        // The kit builders themselves DEFINE these APIs (default sortingOrder = 31000) -- not consumers.
        private static readonly HashSet<string> AllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ElarionUiKit.cs", "ElarionUiKitObsidian.cs", "ElarionUiKitNameplate.cs",
            "ElarionUiKitDemo.cs", "ElarionUi.cs", "PanelManager.cs", "UiStyle.cs",
        };

        // The two named fixes this suite must confirm register (dynamic top-sort UIDocument panels
        // that the sorting-literal scan would not otherwise catch).
        private static readonly string[] NamedMustRegister = { "TowerSwapMenu.cs", "WelcomeBackPopup.cs" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- MODAL ARBITER REGISTRATION (top-band modals must register with PanelManager) ---");

            string modulesDir = Path.Combine(Application.dataPath, "_Modules");
            if (!Directory.Exists(modulesDir))
            {
                reason = "MODAL REGISTRATION SKIPPED -- Assets/_Modules not found";
                return true;
            }

            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex) { reason = "MODAL REGISTRATION SKIPPED -- enumerate failed (" + ex.Message + ")"; return true; }

            int topBandBuilders = 0, registered = 0;
            var namedSeen = new Dictionary<string, bool>();

            foreach (var path in files)
            {
                string fileName = Path.GetFileName(path);
                string norm = path.Replace('\\', '/');
                if (norm.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (norm.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (fileName.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception ex) { notes.Add(fileName + " unreadable (" + ex.Message + ")"); continue; }

                bool registers = RegistersWithArbiter(text);

                // Named-fix confirmation.
                foreach (var named in NamedMustRegister)
                    if (fileName.Equals(named, StringComparison.OrdinalIgnoreCase))
                        namedSeen[named] = registers;

                if (AllowList.Contains(fileName)) continue;

                if (!BuildsTopBandModal(text)) continue;
                topBandBuilders++;

                string rel = norm;
                int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx > 0) rel = rel.Substring(idx);

                if (registers) registered++;
                else failures.Add($"[modal-registration] '{rel}' builds a top-band (>= {TopBand}) modal but does NOT register with PanelManager (Register + NotifyOpened + NotifyClosed) -- invisible to the back-button / battle-lock arbiter");
            }

            // Hard-assert the two named fixes register.
            foreach (var named in NamedMustRegister)
            {
                if (!namedSeen.TryGetValue(named, out bool ok))
                    notes.Add(named + " not found under _Modules (named fix file missing?)");
                else if (!ok)
                    failures.Add($"[modal-registration] the named fix '{named}' does NOT register with PanelManager (Register + NotifyOpened + NotifyClosed)");
                else
                    log.AppendLine($"  named OK: {named} registers with the arbiter");
            }

            log.AppendLine($"  top-band modal builders: {topBandBuilders} ({registered} register)");
            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "MODAL_REGISTRATION_OK");
                reason = $"MODAL REGISTRATION OK -- all {topBandBuilders} top-band modal builders register with PanelManager, and the named fixes register" + noteStr;
                return true;
            }
            reason = "modal-registration: " + string.Join("; ", failures) + noteStr;
            Debug.LogError(log.ToString() + "MODAL_REGISTRATION_FAIL: " + reason);
            return false;
        }

        private static bool BuildsTopBandModal(string text)
        {
            foreach (Match m in ModalCanvas.Matches(text))
                if (int.TryParse(m.Groups[1].Value, out int n) && n >= TopBand) return true;
            foreach (Match m in SortingOrder.Matches(text))
                if (int.TryParse(m.Groups[1].Value, out int n) && n >= TopBand) return true;
            // Default-parameter path: a bare BuildObsidianModal( defaults INTO the band.
            foreach (Match m in ObsidianModalCall.Matches(text))
                if (CallDefaultsIntoTopBand(text, m.Index)) return true;
            return false;
        }

        /// <summary>
        /// True when the BuildObsidianModal call starting at <paramref name="callIndex"/> lands in
        /// the top band. The call statement is the text from the call up to its terminating ';'
        /// (a C# argument list contains no ';'). If that statement names sortingOrder explicitly we
        /// honour the value; otherwise the kit's default (31000 = TopBand) applies.
        /// </summary>
        private static bool CallDefaultsIntoTopBand(string text, int callIndex)
        {
            int end = text.IndexOf(';', callIndex);
            if (end < 0) end = text.Length;
            string statement = text.Substring(callIndex, end - callIndex);
            var explicitSort = ExplicitSortArg.Match(statement);
            if (explicitSort.Success && int.TryParse(explicitSort.Groups[1].Value, out int n))
                return n >= TopBand;
            return true;   // no explicit argument -> the 31000 default
        }

        private static bool RegistersWithArbiter(string text)
        {
            return text.IndexOf("PanelManager", StringComparison.Ordinal) >= 0 &&
                   text.IndexOf("Register", StringComparison.Ordinal) >= 0 &&
                   text.IndexOf("NotifyOpened", StringComparison.Ordinal) >= 0 &&
                   text.IndexOf("NotifyClosed", StringComparison.Ordinal) >= 0;
        }
    }
}
