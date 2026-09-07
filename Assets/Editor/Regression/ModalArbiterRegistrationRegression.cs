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
// It ALSO hard-checks the named fix -- WelcomeBackPopup (a UIDocument
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
        // WO-1495 2026-09-06 remove-by 2026-12-06 - the kit/manager files that DEFINE the modal
        // build APIs rather than consume them. A file list rots when a kit partial is renamed or
        // split, so re-read then that all seven still exist and still define the API.
        private static readonly HashSet<string> AllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ElarionUiKit.cs", "ElarionUiKitObsidian.cs", "ElarionUiKitNameplate.cs",
            "ElarionUiKitDemo.cs", "ElarionUi.cs", "PanelManager.cs", "UiStyle.cs",
        };

        // The two named fixes this suite must confirm register (dynamic top-sort UIDocument panels
        // that the sorting-literal scan would not otherwise catch).
        // TowerSwapMenu.cs was REMOVED from this list 2026-08-30: the file was deleted as dead
        // code (owner ruling, PIN-3 of WO-1282). Keeping the name here would fail the suite
        // looking for a file that no longer exists. Do not re-add it.
        private static readonly string[] NamedMustRegister = { "WelcomeBackPopup.cs" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- MODAL ARBITER REGISTRATION (top-band modals must register with PanelManager) ---");

            string modulesDir = Path.Combine(Application.dataPath, "_Modules");
            if (!Directory.Exists(modulesDir))
            {
                return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                    "MODAL REGISTRATION", "Assets/_Modules not found at " + modulesDir);
            }

            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                    "MODAL REGISTRATION", "could not enumerate _Modules (" + ex.Message + ")");
            }

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
                    // A named fix whose FILE is absent is not verified - the assertion did
                    // not run. Stamped as a partial stand-down so the reason names the hole
                    // rather than folding an unchecked case into an unqualified "all OK".
                    notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "named fix " + named, "not found under _Modules (renamed or deleted?) - registration UNVERIFIED"));
                else if (!ok)
                    failures.Add($"[modal-registration] the named fix '{named}' does NOT register with PanelManager (Register + NotifyOpened + NotifyClosed)");
                else
                    log.AppendLine($"  named OK: {named} registers with the arbiter");
            }

            log.AppendLine($"  top-band modal builders: {topBandBuilders} ({registered} register)");

            CheckCloseFrameGrace(failures, log);

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

        // =====================================================================
        //  [close-frame-grace] — WO-1393 (2026-09-05)
        // ---------------------------------------------------------------------
        // PROVEN (docs/qa/UI_REVIEW_2026-09-05/11-research-upgrade-door.png): a tap issued as
        // Manage was closing reached the HUD Night Market card beneath it and opened the store.
        // The arbiter clears its record in NotifyClosed the same frame; the EventSystem raycasts
        // the in-flight tap the next frame against whatever is left under the finger. The seam:
        // NotifyClosed (and CloseOpen, the back/ESC path) stamp
        // `CloseGraceUntilFrame = Time.frameCount + 1`, and EVERY HUD tap handler early-returns
        // through one helper (SwallowedByCloseGrace) that emits the one trace line. Panels never
        // consult it - a tap on a panel that is still open is legitimate.
        // RED mutations: delete `ArmCloseGrace(` from NotifyClosed's body; change `+ 1` to `+ 2`;
        // remove any one `SwallowedByCloseGrace(` call site; add `InCloseGrace` to ManageScreenPanel.
        // =====================================================================
        private const int GraceCallSites = 11;   // Night Market, Build, Talk, Hero, Raids, Journey,
                                                 // Manage, Builders chip, Harvest chip, dock row, gear handle

        private static void CheckCloseFrameGrace(List<string> failures, StringBuilder log)
        {
            string pmPath = Path.Combine(Application.dataPath, "_Modules", "Core", "UI", "PanelManager.cs");
            string hudPath = Path.Combine(Application.dataPath, "_Modules", "HUD", "Kit", "HudKitController.cs");
            string managePath = Path.Combine(Application.dataPath, "_Modules", "Village", "UI", "Manage", "ManageScreenPanel.cs");
            string pm = File.Exists(pmPath) ? File.ReadAllText(pmPath) : "";
            string hud = File.Exists(hudPath) ? File.ReadAllText(hudPath) : "";
            string manage = File.Exists(managePath) ? File.ReadAllText(managePath) : "";
            if (pm.Length == 0 || hud.Length == 0)
            {
                failures.Add("[close-frame-grace] PanelManager.cs or HudKitController.cs is missing - the seam cannot be verified");
                return;
            }

            // The seam: NotifyClosed arms it, and the stamp is exactly frameCount + 1.
            string notifyClosed = Between(pm, "public static void NotifyClosed(", "public static void CloseAll()");
            // (end markers are brace-free on purpose: the CLAUDE.md section 1 gate counts raw braces)
            string closeOpen = Between(pm, "public static void CloseOpen()", "OpenStateChanged?.Invoke();");
            string arm = Between(pm, "private static void ArmCloseGrace(", "public static PanelHandle Register(");
            if (notifyClosed == null || !notifyClosed.Contains("ArmCloseGrace("))
                failures.Add("[close-frame-grace] PanelManager.NotifyClosed does not arm the close-frame grace - a tap in " +
                             "flight when a modal closes lands on the HUD beneath (11-research-upgrade-door.png)");
            if (closeOpen == null || !closeOpen.Contains("ArmCloseGrace("))
                failures.Add("[close-frame-grace] PanelManager.CloseOpen (back/ESC) does not arm the close-frame grace");
            if (arm == null || !arm.Contains("CloseGraceUntilFrame = UnityEngine.Time.frameCount + 1;"))
                failures.Add("[close-frame-grace] the grace is not exactly ONE frame (CloseGraceUntilFrame = Time.frameCount + 1)");
            if (!pm.Contains("public static int CloseGraceUntilFrame { get; private set; }") ||
                !pm.Contains("public static bool InCloseGrace => UnityEngine.Time.frameCount <= CloseGraceUntilFrame;"))
                failures.Add("[close-frame-grace] PanelManager.CloseGraceUntilFrame / InCloseGrace are not the public readables");

            // The consumers: one helper, one trace literal, every HUD tap handler through it.
            string helper = Between(hud, "private static bool SwallowedByCloseGrace(", "// Wire the compass");
            if (helper == null || !helper.Contains("PanelManager.InCloseGrace") ||
                !helper.Contains("FlowTrace.Step(\"HUD\", \"tap swallowed: panel closed this frame (grace)"))
                failures.Add("[close-frame-grace] HudKitController.SwallowedByCloseGrace does not consult " +
                             "PanelManager.InCloseGrace with the one 'tap swallowed' trace line");
            int calls = CountOf(hud, "SwallowedByCloseGrace(") - 1;   // minus the definition
            if (calls < GraceCallSites)
                failures.Add($"[close-frame-grace] only {calls} HUD tap handlers consult the close-frame grace; " +
                             $"{GraceCallSites} must (Night Market card, the bar faces, both rail chips, the gear dock rows and handle)");
            foreach (var handler in new[] { "private void OpenNightMarket()", "private void OnQuestsAction()",
                                            "private void OnManageAction()", "private void OnCollectorsChipTapped()",
                                            "private void OnBuildersChipTapped()" })
            {
                // Scope = this member up to the next 8-space `private ` declaration, and the guard
                // must be the OPENING statement (within the first 400 chars), never a late check.
                string body = Between(hud, handler, "\n        private ");
                int at = body != null ? body.IndexOf("SwallowedByCloseGrace(", StringComparison.Ordinal) : -1;
                if (at < 0 || at > 400)
                    failures.Add("[close-frame-grace] " + handler + " does not open by consulting the close-frame grace");
            }
            // The dock ROW builder wires the guard into the lambda it builds, so presence is the test.
            string dockTab = Between(hud, "private void AddDockTab(", "\n        private ");
            if (dockTab == null || !dockTab.Contains("SwallowedByCloseGrace(face)"))
                failures.Add("[close-frame-grace] AddDockTab does not route every gear-dock row through the close-frame grace");
            if (!hud.Contains("SwallowedByCloseGrace(\"Build face\")") || !hud.Contains("SwallowedByCloseGrace(\"Hero face\")") ||
                !hud.Contains("SwallowedByCloseGrace(\"Raids face\")") || !hud.Contains("SwallowedByCloseGrace(\"Talk face\")") ||
                !hud.Contains("SwallowedByCloseGrace(\"gear dock handle\")"))
                failures.Add("[close-frame-grace] a bar face lambda or the gear handle bypasses the close-frame grace");

            // Never the panel's own taps: the grace belongs to the layer UNDER a modal only.
            if (manage.Contains("InCloseGrace") || manage.Contains("CloseGraceUntilFrame"))
                failures.Add("[close-frame-grace] ManageScreenPanel consults the close-frame grace - it must never " +
                             "affect taps on the panel itself");

            log.AppendLine($"  close-frame grace: NotifyClosed/CloseOpen arm it, {calls} HUD handlers consult it");
        }

        private static string Between(string src, string from, string until)
        {
            int a = src.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return null;
            int b = src.IndexOf(until, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? null : src.Substring(a, b - a);
        }

        private static int CountOf(string src, string needle)
        {
            int n = 0, i = 0;
            while ((i = src.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
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
