// =============================================================================
// UiObsidianConformanceRegression — the build-gate that ENFORCES the "style
// everything through the Obsidian kit" law the owner has been policing BY EYE.
// -----------------------------------------------------------------------------
// THE LAW (ARCHITECTURE_PRINCIPLES.md §2 "presentation is a separate layer";
// UI_BLINK_TEMPLATE_CANON.md; memory style-everything-obsidian): first-party
// presentation code STYLES through `ElarionUiKit` — it NEVER hand-rolls bespoke
// uGUI widgets. A screen DROPS content into the kit's builders (BuildObsidianPanel/
// Bar/Button/Modal, BuildCastBar, BuildTargetFrame, Label, AddImage, CurrencyChip);
// it does not `new GameObject(..., typeof(Image))` its own plates, `AddComponent
// <TextMeshProUGUI>()` its own labels, or paint its own `new Color(...)` fills.
//
// This is a SOURCE-LINT oracle (same family as CompileGate's leak/NUL scan): it
// READS the .cs source under Assets/_Modules/**, it does NOT run PlayMode. So it
// slots straight into the headless DataRegression.RunAll batch gate. Never throws
// (an unreadable file becomes a note, never a crash).
//
// THE SMELL (a class that CONSTRUCTS uGUI directly):
//   • `new GameObject(..., typeof(Image|RawImage|Text|TextMeshProUGUI|TMP_Text))`
//   • `AddComponent<Image|RawImage|Text|TextMeshProUGUI|TMP_Text>()`
//   • (corroborating, reported not qualifying) a raw `typeof(Canvas)` / `Add
//     Component<Canvas>()` root, and a `.color = new Color(...)` UI fill.
// ...WITHOUT routing through the kit — i.e. the file makes NO reference to
// `ElarionUiKit` (the builder). A file that constructs raw uGUI *and* references
// the kit is presumed to be a legitimate consumer that drops content into it
// (file-level heuristic — see LIMITATION below).
//
// EXCLUDES (avoid false positives): the kit/style authoring files themselves (they
// ARE the sanctioned raw-uGUI constructors), test + editor code, and a small
// ALLOW-LIST of known-sanctioned low-level precedents (world-space unit/node bars,
// full-screen fades, on-screen input controls, dev overlays) — each with a reason.
//
// BASELINE-vs-NEW MODE (chosen because the codebase carries EXISTING debt — ~13
// pure hand-rolled files predate the kit): the current offenders are recorded in
// KnownBaseline. The oracle SEPARATES offenders into BASELINE (tracked debt, does
// not fail) vs NEW (not in the baseline — a freshly hand-rolled widget). A NEW
// offender is what the gate exists to catch. Enforcement policy is a ONE-LINE flip:
//   HardFailOnNew = false (default) → PASS, but the reason LOUDLY names every NEW
//                                     offender (owner/CLI decide + convert).
//   HardFailOnNew = true            → a NEW hand-rolled widget FAILS the gate.
// ARMED 2026-07-30: the confirming run was done (5 NEW reported), each offender was
// triaged, the 2 dev-only tools moved to the allow-list, the 3 genuine shipping-UI
// offenders frozen into the baseline, and the ONE truly-resolved entry (ArenaPanel)
// dropped. NEW count is now ZERO, so HardFailOnNew is TRUE -- a freshly hand-rolled
// uGUI widget in a shipping module is a HARD DataRegression failure.
// Same pass closed a REGEX BLIND SPOT: StrongSmells required the bare type name right
// after typeof(/AddComponent<, so the NAMESPACE-QUALIFIED form slipped through and
// OutpostHub.cs read as "resolved" while still hand-rolling raw uGUI. The patterns now
// tolerate an optional namespace prefix, and OutpostHub STAYS in the baseline as the
// real debt it is. Do not drop a baseline row on a "resolved" note alone -- verify.
//
// LIMITATION (documented, deliberate): the routing check is FILE-LEVEL — a file
// that calls the kit for its chrome but then hand-rolls raw rows inside is not
// flagged (it references ElarionUiKit). That's the precision/noise trade the owner
// sanctioned ("a heuristic that's USEFUL not noisy"). The baseline mechanism still
// catches the high-value case: a brand-new file that hand-rolls with no kit routing,
// AND a former kit-consumer that DELETES its routing (it drops out of the kit set
// into the NEW-offender set). Tighten to per-construction-path later if needed.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class UiObsidianConformanceRegression
    {
        // --- enforcement policy (owner/CLI decides; see header) ------------------
        // false = PASS-with-warning-list (default, safe): NEW offenders are named in
        //         the reason but do not fail the gate.
        // true  = a NEW hand-rolled widget is a HARD build failure.  <-- ARMED 2026-07-30
        private const bool HardFailOnNew = true;

        // --- the routing token: a file that references THE KIT BUILDER is presumed
        //     to drop content into it (file-level heuristic).
        private const string KitToken = "ElarionUiKit";

        // --- the hand-rolled-uGUI construction smells ----------------------------
        // STRONG smells QUALIFY a file as an offender (a bespoke Image/Text widget).
        private static readonly Regex[] StrongSmells =
        {
            // NOTE the optional (Namespace.)* prefix: without it the NAMESPACE-QUALIFIED
            // form (typeof(UnityEngine.UI.Image), AddComponent<TMPro.TextMeshProUGUI>)
            // slipped through and a file could read as "resolved" while still hand-rolling.
            // That blind spot hid OutpostHub.cs until 2026-07-30.
            new Regex(@"new\s+GameObject\s*\([^;]*typeof\s*\(\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)*(Image|RawImage|Text|TextMeshProUGUI|TMP_Text)\s*\)", RegexOptions.Compiled),
            new Regex(@"AddComponent\s*<\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)*(Image|RawImage|Text|TextMeshProUGUI|TMP_Text)\s*>", RegexOptions.Compiled),
        };
        // WEAK smells are REPORTED as corroborating evidence but do NOT by themselves
        // qualify a file (a bare root Canvas is legitimate; a fill Color is only a
        // smell in company of a raw widget).
        private static readonly Regex[] WeakSmells =
        {
            new Regex(@"new\s+GameObject\s*\([^;]*typeof\s*\(\s*Canvas\s*\)", RegexOptions.Compiled),
            new Regex(@"AddComponent\s*<\s*Canvas\s*>", RegexOptions.Compiled),
            new Regex(@"\.color\s*=\s*new\s+Color", RegexOptions.Compiled),
        };

        // --- ALLOW-LIST — known-sanctioned raw-uGUI constructors (by file name) ---
        // Matched on Path.GetFileName so it's path-robust. Each carries WHY it is
        // sanctioned. These never count as offenders and never enter the baseline.
        private static readonly Dictionary<string, string> AllowList = new Dictionary<string, string>
        {
            // THE KIT + STYLE LAYER — they ARE the sanctioned builders/primitives.
            { "ElarionUiKit.cs",          "the kit builder (partial) — the sanctioned raw-uGUI constructor" },
            { "ElarionUiKitObsidian.cs",  "the kit builder (Obsidian widget family, partial)" },
            { "ElarionUiKitNameplate.cs", "the kit builder (nameplate/target-frame, partial)" },
            { "ElarionUiKitDemo.cs",      "the kit's own screenshot-compare demo harness" },
            { "ElarionUi.cs",             "the kit's low-level primitives (Label/AddImage) + palette" },
            { "UiStyle.cs",               "style authority (theme/icon resolve) — no bespoke widgets" },
            { "RpgUiCatalog.cs",          "sprite catalog resolver — no bespoke widgets" },
            { "ConceptIconResolver.cs",   "icon-concept resolver — no bespoke widgets" },
            { "CombatTextLayer.cs",       "low-level floating combat-text layer (routes through kit style)" },

            // SANCTIONED LOW-LEVEL PRECEDENTS (distinct patterns, not screen-space
            // Obsidian panels). NOTE: world-space bars are migrate-to-kit-target-frame
            // candidates — allow-listed for now so they are not noise.
            { "FloatingHealthBar.cs",     "world-space unit HP bar (WO-178) — self-contained precedent; migrate to kit target-frame later" },
            { "NodeFillIndicator.cs",     "world-space harvest-node fill bar — same world-space precedent as FloatingHealthBar" },
            { "VirtualJoystick.cs",       "on-screen touch INPUT control, not a styled content widget" },
            { "ScreenFader.cs",           "full-screen fade primitive (one black Image), not a content widget" },
            { "SceneTransitionTrigger.cs","full-screen scene-transition fade primitive, not a content widget" },
            { "OwnerDevToolsOverlay.cs",  "owner/dev debug overlay — not shipped player UI" },
            { "DebuggingController.cs",   "dev debug overlay — not shipped player UI" },
            // Compiled into the release player but RUNTIME-GATED OFF by default (both
            // flags resolve defaultOn:IsDevBuild = isEditor || isDebugBuild), so neither
            // ever spawns in a store build. Same category as the two overlays above.
            { "FlagCaptureButton.cs",     "dev/tester on-screen flag chip - runtime-gated OFF in release (FeatureFlags.FlagButton)" },
            { "ResourceDevTool.cs",       "dev/tester resource-grant overlay - runtime-gated OFF in release (FeatureFlags.DevResourceTool)" },
        };

        // --- KNOWN BASELINE — the EXISTING hand-rolled offenders (tracked debt) ---
        // Rel-path (Assets/... , forward slashes). These predate the kit; they are
        // reported as debt, they do NOT fail the gate. An offender NOT in this set is
        // NEW — that is what the gate catches. Refresh this list from one real run
        // (a resolved baseline file is a harmless "resolved" note, never a failure).
        private static readonly HashSet<string> KnownBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/_Modules/HUD/AttentionGlowUi.cs",
            "Assets/_Modules/Village/Arena/ArenaAttackPaletteUI.cs",
            // ArenaPanel.cs REMOVED 2026-07-30 - genuinely resolved, it now routes through
            // the kit (ElarionUiKit.PinCanonicalCtaSize at ArenaPanel.cs:212 and :395).
            "Assets/_Modules/Village/Combat/ThreatSkullPlate.cs",
            "Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs",
            "Assets/_Modules/Village/Buildings/NPCUpgradeStation.cs",
            "Assets/_Modules/Village/Buildings/MobileInteractButton.cs",
            "Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs",
            "Assets/_Modules/Village/NPCs/GearOfferChoiceUI.cs",
            "Assets/_Modules/Village/World/Camps/CampPromptUI.cs",
            "Assets/_Modules/Village/World/Camps/EchoTutorialUI.cs",
            "Assets/_Modules/Village/World/NodeDiscoverySystem.cs",
            // KEPT 2026-07-30: a run reported this as "resolved", but that was the
            // namespace-qualified regex blind spot (see the header) - the file still
            // hand-rolls raw uGUI at OutpostHub.cs:161/183/195/208/222 with zero kit
            // routing. Real debt; the tightened patterns now see it again.
            "Assets/_Modules/Village/World/OutpostHub.cs",

            // --- frozen 2026-07-30 (arming pass): SHIPPING player-facing UI, real debt.
            //     These are NOT dev tools - none is compile-stripped or flag-gated.
            //     Do NOT add to this list to silence new work; convert to the kit instead.
            // Build Mode placement ghost's world-space "why it's red" reason label
            // (GhostPreview.cs:283-316), owner-requested 2026-07-24; instantiated
            // unconditionally by BuildModeController.cs:517/1889/2362.
            "Assets/_Modules/Village/BuildMode/GhostPreview.cs",
            // Diegetic collector fill bar + "N/20" readout + FULL "!" bang
            // (CollectorStackView.cs:211-289). WO-900 (2026-08-04): the long-standing note here
            // said "Attach() currently has NO caller anywhere, so it renders nothing at runtime".
            // That is no longer a note - it is now an ASSERTION (AssertCollectorTellWired below),
            // because a dead tell is exactly the failure that hid for months: the view was fully
            // built and simply never called, so a collector capping showed the player nothing.
            // The file stays on this list as legacy-uGUI debt (it is exempt from the MVVM ratchet
            // as a world-space diegetic decorator), NOT because it is dead.
            "Assets/_Modules/Village/Buildings/Progression/CollectorStackView.cs",
            // Dead-but-shipping PauseHudButton chip (PauseHudBootstrap.cs:112-225),
            // culled 2026-07-24 into HudKitController's dock and never re-instantiated.
            // Resolve by DELETING the class, not by allow-listing it.
            "Assets/_Modules/Settings/PauseHudBootstrap.cs",
        };

        /// <summary>
        /// Source-lints first-party UI under Assets/_Modules/** for hand-rolled uGUI
        /// that bypasses ElarionUiKit. Returns true when clean (or, in the default
        /// report-only mode, when there are no offenders OUTSIDE the allow-list beyond
        /// tracked debt). NEW offenders are always named in <paramref name="reason"/>;
        /// with HardFailOnNew they also return false. Never throws.
        /// </summary>
        public static bool Run(out string reason)
        {
            var newOffenders = new List<string>();      // "rel:line construction" (NOT in baseline)
            var baselineHits = new List<string>();      // rel paths still offending (tracked debt)
            var notes = new List<string>();
            int uiFilesScanned = 0;                     // .cs with any UI construction (strong or weak)
            int routedThroughKit = 0;                   // of those, how many reference the kit

            string modulesDir = Path.Combine(Application.dataPath, "_Modules");
            string projectRoot = Path.GetDirectoryName(Application.dataPath); // parent of /Assets
            if (!Directory.Exists(modulesDir))
            {
                reason = "UI-OBSIDIAN CONFORMANCE SKIPPED — Assets/_Modules not found";
                return true;
            }

            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                reason = "UI-OBSIDIAN CONFORMANCE SKIPPED — could not enumerate _Modules (" + ex.Message + ")";
                return true;
            }

            foreach (var path in files)
            {
                string fileName = Path.GetFileName(path);

                // EXCLUDE: test + editor code (not shipped presentation).
                string norm = path.Replace('\\', '/');
                if (norm.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (norm.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (fileName.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception ex) { notes.Add(fileName + " unreadable (" + ex.Message + ")"); continue; }

                // Collect smell lines (skip comment-only lines to cut false positives).
                var strongLines = new List<string>();
                var weakLines = new List<string>();
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string raw = lines[i];
                    string trimmed = raw.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

                    foreach (var rx in StrongSmells)
                        if (rx.IsMatch(raw)) { strongLines.Add((i + 1) + ": " + trimmed.Trim()); break; }
                    foreach (var rx in WeakSmells)
                        if (rx.IsMatch(raw)) { weakLines.Add((i + 1) + ": " + trimmed.Trim()); break; }
                }

                bool hasAnyUiConstruction = strongLines.Count > 0 || weakLines.Count > 0;
                if (hasAnyUiConstruction) uiFilesScanned++;

                bool routesThroughKit = text.IndexOf(KitToken, StringComparison.Ordinal) >= 0;
                if (hasAnyUiConstruction && routesThroughKit) routedThroughKit++;

                // EXCLUDE: allow-listed sanctioned constructors.
                if (AllowList.ContainsKey(fileName)) continue;

                // A file only QUALIFIES as an offender on a STRONG smell (a bespoke
                // Image/Text widget) AND no kit routing. A bare Canvas / fill-Color
                // alone (weak only) is not enough.
                if (strongLines.Count == 0) continue;
                if (routesThroughKit) continue;

                // Offender. Classify NEW vs BASELINE.
                string rel = norm;
                int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx > 0) rel = rel.Substring(idx);

                string evidence = string.Join(" ; ", strongLines.ToArray());
                if (weakLines.Count > 0) evidence += "  [+fill: " + string.Join(" ; ", weakLines.ToArray()) + "]";

                if (KnownBaseline.Contains(rel))
                    baselineHits.Add(rel);
                else
                    newOffenders.Add(rel + " -> " + evidence);
            }

            // WO-900: the collector FULL tell must have a live caller (see the AllowList note).
            string tellViolation = AssertCollectorTellWired(projectRoot);
            if (tellViolation != null) newOffenders.Add(tellViolation);

            // Baseline files that no longer offend (resolved / renamed) — info only.
            var resolved = new List<string>();
            foreach (var b in KnownBaseline)
                if (!baselineHits.Contains(b)) resolved.Add(b);

            string summary = uiFilesScanned + " UI file(s) scanned, " + routedThroughKit +
                             " route through ElarionUiKit; " + baselineHits.Count +
                             " known-baseline hand-rolled file(s) tracked as debt";
            if (resolved.Count > 0) summary += "; " + resolved.Count + " baseline file(s) resolved (refresh KnownBaseline)";
            if (notes.Count > 0) summary += " [notes: " + string.Join("; ", notes.ToArray()) + "]";

            if (newOffenders.Count == 0)
            {
                reason = "UI-OBSIDIAN CONFORMANCE OK — " + summary + "; 0 NEW hand-rolled offenders";
                return true;
            }

            string offenderList = string.Join("  |  ", newOffenders.ToArray());
            if (HardFailOnNew)
            {
                reason = "UI-OBSIDIAN CONFORMANCE VIOLATION x" + newOffenders.Count +
                         " — NEW hand-rolled UI bypassing ElarionUiKit: " + offenderList +
                         " (" + summary + ")";
                return false;
            }

            // Default report-only: PASS, but name every NEW offender loudly.
            reason = "UI-OBSIDIAN CONFORMANCE WARN — " + newOffenders.Count +
                     " NEW hand-rolled UI file(s) bypass ElarionUiKit (report-only; flip HardFailOnNew to gate): " +
                     offenderList + " (" + summary + ")";
            return true;
        }

        /// <summary>
        /// WO-900 - assert the collector FULL tell is actually WIRED, i.e. that
        /// <c>CollectorStackView.Attach</c> has at least one caller outside its own file.
        /// <para>
        /// This exists because the opposite was true for months and only ever got written down as
        /// a comment. <c>CollectorStackView</c> is a complete, 437-line CoC fill tell - pooled prop
        /// pile / world-space fill bar, amber near-full band, redundant "N/20" readout, the "!"
        /// bang, the glint VFX and the one-time "is full" toast - and NOTHING CALLED IT. A collector
        /// filling to capacity showed the player absolutely nothing; the wallet number simply
        /// stopped moving. A note in an allow-list cannot fail a gate, so the defect survived every
        /// run of this suite. An assertion can.
        /// </para>
        /// Returns null when wired, or an offender string naming the breakage.
        /// </summary>
        private static string AssertCollectorTellWired(string projectRoot)
        {
            const string ViewRel = "Assets/_Modules/Village/Buildings/Progression/CollectorStackView.cs";
            const string Token = "CollectorStackView.Attach";

            string viewFull = Path.Combine(projectRoot, ViewRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(viewFull)) return null;   // view deleted: nothing to keep wired

            string modulesDir = Path.Combine(Application.dataPath, "_Modules");
            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch { return null; }

            foreach (var path in files)
            {
                string norm = path.Replace('\\', '/');
                if (norm.EndsWith("CollectorStackView.cs", StringComparison.OrdinalIgnoreCase)) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }

                // Ignore matches that only appear in comments - a comment is what let this rot.
                foreach (var line in text.Split('\n'))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    if (trimmed.IndexOf(Token, StringComparison.Ordinal) >= 0) return null;   // wired
                }
            }

            return ViewRel + " -> DEAD TELL: CollectorStackView.Attach has NO non-comment caller anywhere under " +
                   "Assets/_Modules. The entire collector 'I am full' tell (fill bar, amber near-full band, N/20 " +
                   "readout, '!' bang, glint, full toast) is built but never invoked, so a collector that stops " +
                   "earning gives the player no signal at all. Wire it at StructureFactory's ResourceCollector " +
                   "behavior case, right after col.Configure(buildingId)";
        }
    }
}
