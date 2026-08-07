// =============================================================================
// UiMvvmConformanceRegression — the build-gate that ENFORCES strict MVVM: a View
// is a dumb skin that binds a ViewModel and NEVER reads/reconciles game state at
// runtime. Sibling of UiObsidianConformanceRegression (same source-lint family).
// -----------------------------------------------------------------------------
// THE LAW (ARCHITECTURE_PRINCIPLES.md §1/§2/§2c; UI_MVVM_BINDING_MAP.md; the gold
// standard BuildingUpgradeVM + BuildingUpgradePanelMvvm): all state + logic lives
// in an IPanelViewModel; the View binds it, re-renders on Changed, routes taps as
// commands, and reads NO game state. A View that reaches for EconomyService/
// GameStateService/a gameplay catalog/FindObjectOfType, or computes affordability/
// gating/cost itself, is a VIOLATION even if it works.
//
// SOURCE-LINT (reads .cs under Assets/_Modules/**, no PlayMode) so it slots into the
// headless DataRegression.RunAll batch gate. Never throws.
//
// CANDIDATE (a "View"): a first-party .cs that CONSTRUCTS uGUI (new GameObject(...
// typeof(Image/Text/...)) or AddComponent<Image/Text/Button/ScrollRect/...>). VMs
// are pure C# (build no uGUI) so they are never candidates.
//
// OFFENDER: a candidate that (a) contains a BANNED game-state symbol AND (b) does
// NOT route through a ViewModel (no reference to IPanelViewModel and no *VM.
// CreateDefault( call) AND (c) is not allow-listed. A View that delegates to a VM
// auto-drops out the moment it gains the CreateDefault/IPanelViewModel reference.
//
// LIMITATION (documented, deliberate, mirrors the Obsidian oracle): the routing
// check is FILE-LEVEL — a View that references a VM but STILL calls a banned symbol
// in Open() passes. That is the precision/noise trade; tighten to per-method
// scanning later. The baseline still catches the high-value case: a brand-new no-VM
// View, and a former VM-consumer that deletes its VM routing.
//
// BASELINE-vs-NEW (the codebase carries ~36 pre-existing offenders — the whole point
// of the migration program): current offenders live in KnownBaseline (tracked debt,
// non-failing); a NEW offender (not in the baseline) is what the gate catches.
//   HardFailOnNew = false (default) -> PASS, but the reason LOUDLY names every NEW
//                                      offender (report-only, safe against a stale baseline).
//   HardFailOnNew = true            -> a NEW no-VM View FAILS the gate.
// SEED FLOW: KnownBaseline starts EMPTY on purpose. The FIRST real DataRegression run
// reports every current offender with its exact rel-path; copy those into KnownBaseline,
// re-run to confirm a clean baseline, then (once the migration empties the baseline)
// flip HardFailOnNew = true so the seam can never silently rot again.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class UiMvvmConformanceRegression
    {
        // --- enforcement policy (see header; flip to true once the baseline empties) ---
        private const bool HardFailOnNew = true;

        // --- CANDIDATE detection: a file that CONSTRUCTS uGUI is a View (presentation). ---
        private static readonly Regex[] UiConstruction =
        {
            new Regex(@"new\s+GameObject\s*\([^;]*typeof\s*\(\s*(Image|RawImage|Text|TextMeshProUGUI|TMP_Text|Button|ScrollRect|Toggle|Slider)\s*\)", RegexOptions.Compiled),
            new Regex(@"AddComponent\s*<\s*(Image|RawImage|Text|TextMeshProUGUI|TMP_Text|Button|ScrollRect|Toggle|Slider)\s*>", RegexOptions.Compiled),
        };

        // --- BANNED game-state symbols (a View must not read/reconcile these). ---
        private static readonly Regex[] BannedSymbols =
        {
            new Regex(@"\bEconomyService\s*\.\s*Instance\b", RegexOptions.Compiled),
            new Regex(@"\bFind(Object|Objects|FirstObject|AnyObject)(Of|By)Type\b", RegexOptions.Compiled),
            new Regex(@"\bGameStateService\b", RegexOptions.Compiled),
            new Regex(@"\bResourceLedger\b", RegexOptions.Compiled),
            new Regex(@"\bVillageInventory\s*\.\s*Instance\b", RegexOptions.Compiled),
            new Regex(@"\b(GearCatalog|BuildingTierCatalog|AbilityCatalog|ArenaCatalog|CraftingRecipeCatalog|SceneConfigCatalog|QuestCatalog|DailyQuestCatalog)\b", RegexOptions.Compiled),
        };

        // --- VM-ROUTING tokens: presence => the View delegates to a ViewModel (exempt). ---
        private static readonly Regex[] VmRoutingTokens =
        {
            new Regex(@"\bIPanelViewModel\b", RegexOptions.Compiled),
            new Regex(@"\.\s*CreateDefault\s*\(", RegexOptions.Compiled),
        };

        // --- ALLOW-LIST — sanctioned files that read/push state by design (by file name). ---
        // The reflection *HudBridge PUSH seam (Village pushes INTO HUD) is sanctioned and matched
        // by the "HudBridge.cs" SUFFIX rule below (not enumerated). These are the non-suffix ones.
        private static readonly Dictionary<string, string> AllowList = new Dictionary<string, string>
        {
            // Dev-only tools — reflected reads, not shipped player UI (out of MVVM scope, §3 leverage).
            { "AdminOverlay.cs",         "owner/dev debug console — not shipped player UI" },
            { "OwnerDevToolsOverlay.cs", "owner/dev debug overlay — not shipped player UI" },
            { "DebugCanvasUI.cs",        "deprecated dev canvas — editor/dev builds only" },
            { "HelpMenu.cs",             "dev help menu — reflected dev mutations, not a state-reading View" },
            // World-space diegetic decorator — presentation-separate already; a category error to make it a panel VM.
            { "CollectorStackView.cs",   "world-space diegetic stack decorator (injected model) — conformant by exemption" },
            { "DebuggingController.cs",   "dev debug overlay — not shipped player UI" },
            { "ResourceDevTool.cs",       "owner/dev resource-grant tool — not shipped player UI" },
            { "VirtualJoystick.cs",       "on-screen touch INPUT control, not a state-reading panel View" },
            { "BossHealthBar.cs",         "world-space boss HP bar (finds the boss) — world-space precedent, not a panel View" },
            // Non-modal-panel Views / flow controllers / benign infra finds — out of the WO-744
            // panel-migration scope (all 36 audit panels are migrated). Honest reasons, not hiding:
            { "IntroSequencePlayer.cs",   "cinematic sequence player, not a modal panel View" },
            { "BugReportView.cs",         "VM-bound (BugReportVM); residual FindAnyObjectByType is EventSystem/scene infra" },
            { "HeroSelectController.cs",  "onboarding/menu FLOW controller, not a modal panel View" },
            { "StoryIntroController.cs",  "onboarding flow controller, not a modal panel View" },
            { "TitleController.cs",       "title/menu flow controller, not a modal panel View" },
            { "PauseHudBootstrap.cs",     "scene bootstrap wiring (finds controllers), not a panel View" },
            { "HudKitController.cs",      "HUD kit wiring; FindAnyObjectByType feeds compass providers (Transform positions), not game state" },
            { "OverworldEncounterSpawner.cs", "world encounter spawner, not a View" },
            // WO-899: this file gained a world-space Canvas for the build countdown plate (the owner
            // could not read the old bare 3D text until very close), which is what made the linter
            // classify it as a View for the first time - the scan went 90 -> 91 files. Its
            // FindObjectsByType<Building> is PRE-EXISTING and unchanged (verified: present in HEAD,
            // absent from the diff): a ONE-SHOT attach helper that locates the structure to decorate.
            // Same category as CollectorStackView above - a world-space diegetic decorator, not a
            // panel reading game state. Listed rather than baselined because KnownBaseline is
            // deliberately EMPTY (WO-744 complete) and must stay that way.
            { "UnderConstructionVisual.cs", "world-space build-countdown decorator; one-shot FindObjectsByType<Building> attach helper (pre-existing), not a panel View" },
            { "EchoUnlockDialogue.cs",    "EventSystem-ensure find, not a game-state read" },
            { "EchoUnlockFeedback.cs",    "EventSystem-ensure find, not a game-state read" },
            { "InventoryUIBuilder.cs",    "sibling-UI panel find; its state reads were migrated in Silo B" },
            { "NodeDiscoverySystem.cs",   "world node-discovery system, not a View" },
            { "EndStateView.cs",          "VM-bound (EndStateVM); residual find is EventSystem/scene infra" },
        };

        // Files ending with this suffix are the sanctioned reflection PUSH seam (Village -> HUD).
        private const string HudBridgeSuffix = "HudBridge.cs";

        // --- KNOWN BASELINE — the EXISTING offenders (tracked debt, non-failing). ---
        // SEED FROM THE FIRST REAL RUN (see header): starts empty; the run's reason lists every
        // current offender rel-path, which is pasted here verbatim. Once the migration empties this,
        // flip HardFailOnNew = true. Rel-path form: "Assets/_Modules/... .cs" (forward slashes).
        private static readonly HashSet<string> KnownBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // EMPTY 2026-07-18 — WO-744 COMPLETE. All 36 audit panel Views bind a ViewModel; the 3
            // migrated stragglers (PackStore/TroopTrainingPanel/NPCUpgradeStation) resolved out; the
            // remaining non-panel offenders (flow controllers, spawners, benign EventSystem/sibling
            // finds, HUD wiring) are in AllowList with honest reasons. Baseline empty -> HardFailOnNew
            // is now TRUE: any NEW View that reads game state HARD-FAILS the gate.
        };

        /// <summary>
        /// Source-lints first-party Views under Assets/_Modules/** for game-state reads that bypass
        /// the ViewModel seam. Returns true when clean (or, in report-only mode, always — naming every
        /// NEW offender in <paramref name="reason"/>). With HardFailOnNew a NEW offender returns false.
        /// Never throws.
        /// </summary>
        public static bool Run(out string reason)
        {
            var newOffenders = new List<string>();
            var baselineHits = new List<string>();
            var notes = new List<string>();
            int viewsScanned = 0;      // files that construct uGUI (candidates)
            int vmRouted = 0;          // of those, how many route through a VM

            string modulesDir = Path.Combine(Application.dataPath, "_Modules");
            if (!Directory.Exists(modulesDir))
            {
                reason = "UI-MVVM CONFORMANCE SKIPPED — Assets/_Modules not found";
                return true;
            }

            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                reason = "UI-MVVM CONFORMANCE SKIPPED — could not enumerate _Modules (" + ex.Message + ")";
                return true;
            }

            foreach (var path in files)
            {
                string fileName = Path.GetFileName(path);
                string norm = path.Replace('\\', '/');

                // EXCLUDE test + editor code.
                if (norm.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (norm.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (fileName.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception ex) { notes.Add(fileName + " unreadable (" + ex.Message + ")"); continue; }

                // CANDIDATE only if it constructs uGUI (it is presentation).
                bool constructsUi = false;
                foreach (var rx in UiConstruction) { if (rx.IsMatch(text)) { constructsUi = true; break; } }
                if (!constructsUi) continue;
                viewsScanned++;

                // Does it route through a VM?
                bool vmRoutes = false;
                foreach (var rx in VmRoutingTokens) { if (rx.IsMatch(text)) { vmRoutes = true; break; } }
                if (vmRoutes) vmRouted++;

                // EXCLUDE sanctioned files (dev tools, decorator, and the *HudBridge PUSH seam).
                if (AllowList.ContainsKey(fileName)) continue;
                if (fileName.EndsWith(HudBridgeSuffix, StringComparison.OrdinalIgnoreCase)) continue;

                // A VM-routed View is presumed conformant (file-level heuristic — see LIMITATION).
                if (vmRoutes) continue;

                // Collect the banned-symbol hits (skip comment-only lines to cut false positives).
                var hits = new List<string>();
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    foreach (var rx in BannedSymbols)
                    {
                        var m = rx.Match(lines[i]);
                        if (m.Success) { hits.Add((i + 1) + ":" + m.Value); break; }
                    }
                }
                if (hits.Count == 0) continue;   // constructs uGUI but reads no game state -> clean

                string rel = norm;
                int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx > 0) rel = rel.Substring(idx);

                if (KnownBaseline.Contains(rel))
                    baselineHits.Add(rel);
                else
                    newOffenders.Add(rel + " -> " + string.Join(" ; ", hits.ToArray()));
            }

            var resolved = new List<string>();
            foreach (var b in KnownBaseline)
                if (!baselineHits.Contains(b)) resolved.Add(b);

            string summary = viewsScanned + " View file(s) scanned, " + vmRouted +
                             " route through a ViewModel; " + baselineHits.Count +
                             " known-baseline offender(s) tracked as debt";
            if (resolved.Count > 0) summary += "; " + resolved.Count + " baseline file(s) resolved (refresh KnownBaseline)";
            if (notes.Count > 0) summary += " [notes: " + string.Join("; ", notes.ToArray()) + "]";

            if (newOffenders.Count == 0)
            {
                reason = "UI-MVVM CONFORMANCE OK — " + summary + "; 0 NEW state-reading Views";
                return true;
            }

            string offenderList = string.Join("  |  ", newOffenders.ToArray());
            if (HardFailOnNew)
            {
                reason = "UI-MVVM CONFORMANCE VIOLATION x" + newOffenders.Count +
                         " — NEW View(s) reading game state without a ViewModel: " + offenderList +
                         " (" + summary + ")";
                return false;
            }

            reason = "UI-MVVM CONFORMANCE WARN — " + newOffenders.Count +
                     " View(s) read game state without a ViewModel (report-only; flip HardFailOnNew to gate): " +
                     offenderList + " (" + summary + ")";
            return true;
        }
    }
}
