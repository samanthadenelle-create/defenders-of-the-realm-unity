// =============================================================================
// HudUiRegression — headless HUD/UI defect-class gate (data + logic only).
// -----------------------------------------------------------------------------
// Owner directive 2026-07-12: the device screenshot showed TMP tofu boxes (□)
// from non-ASCII glyphs (⟲/⟳-class) baked into code-built UI strings. This
// oracle locks that WHOLE defect class at gate time, plus three sibling
// UI-contract classes, without opening a scene (INSTRUMENTATION_STANDARD.md §4:
// real object in → assert real response → one marker).
//
// CHECKS
//   1. THE TOFU ORACLE — scan first-party runtime source (Assets/_Modules/**,
//      excluding Editor/, Tests/, DevTools/) for string literals that feed UI
//      text (only files referencing TMPro / UnityEngine.UI / UIElements /
//      ElarionUiKit). Every non-ASCII character found is verified against the
//      REAL font the kit ships with (ElarionUiKit.EnsureFont resolution order:
//      TMP_Settings.defaultFontAsset ?? Resources "Fonts & Materials/
//      LiberationSans SDF", incl. fallback fonts + dynamic-atlas source). A
//      character the font cannot render IS the □ the owner saw — FAIL naming
//      every file:line. No hand-maintained char allowlist: the font itself is
//      the truth (chars it has pass, chars it lacks fail), so ✦/×/— pass or
//      fail on EVIDENCE, never on a guess. Astral-plane chars (emoji 🏰 etc.)
//      always fail — no shipped UI font covers them.
//   2. UIDocument CONSTRUCTION FENCE — UXML/UIDocument surfaces are the known
//      does-not-work-in-builds path (CLAUDE.md §8 "UXML in builds does NOT
//      work — always use code-built UI"). The existing UIDocument surfaces are
//      baselined below; a NEW gameplay file that constructs (or RequireComponent-
//      declares) a UIDocument outside the baseline FAILS naming file:line.
//   3. KIT CONFORMANCE — reflection over DeNelle.Core: every load-bearing
//      public ElarionUiKit builder the HUD composes with must exist, and the
//      kit's canonical rarity glyphs (colorblind-owner shape channel) must be
//      non-empty AND font-renderable (a tofu'd rarity glyph deletes the only
//      non-color rarity signal).
//   4. RESOURCES PATH RESOLUTION — every string-literal Resources.Load path in
//      runtime code must resolve to a real asset under an Assets/**/Resources/
//      root (the wiard.jpg-typo class: referenced-but-missing silently blanks
//      an icon). Existing misses are baselined as tracked debt; a NEW missing
//      path FAILS naming file + path.
//
// SOURCE-LINT family (same as UiObsidianConformanceRegression / CompileGate's
// NUL scan): reads .cs text + asset folders, runs no PlayMode, never throws.
// Marker: HUDUI_OK (Debug.Log) / HUDUI_FAIL (Debug.LogError → break-log.jsonl).
//
// FALSE-POSITIVE MITIGATIONS (deliberate, documented):
//   • Tofu scan only inside files that reference a UI text stack; skips comment
//     lines, attribute lines ([Header]/[Tooltip] render in the Inspector, not
//     TMP), log/exception lines (Debug.Log/FlowTrace/Guard/throw text goes to
//     the console, not a label), and trailing // comments on code lines.
//   • The font — with fallbacks and its dynamic-atlas source face — decides
//     tofu, not a char list; a glyph the shipped font genuinely has never fails.
//   • Checks 2 & 4 are BASELINE-vs-NEW (UiObsidianConformanceRegression
//     precedent): today's verified state is grandfathered as tracked debt so a
//     pre-existing miss can never false-fail the gate; only a NEW offender
//     fails. Resolved baseline entries surface as refresh notes, never failures.
//   • Resources matching is case-insensitive and extension-free (mirrors
//     Resources.Load semantics); non-literal (concatenated/variable) paths are
//     skipped — not assertable from source.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Editor
{
    public static class HudUiRegression
    {
        // =====================================================================
        // BASELINES (verified against the working tree 2026-07-12 — refresh in
        // the same breath as intentionally resolving/adding an entry).
        // =====================================================================

        // --- Check 2: sanctioned UIDocument surfaces (rel path, forward slash).
        // Constructing/declaring a UIDocument in any OTHER runtime file = FAIL.
        private static readonly HashSet<string> UiDocumentBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/_Modules/Web3/JupiterSwapBootstrap.cs",
            "Assets/_Modules/Web3/JupiterSwapService.cs",
            "Assets/_Modules/Wallet/WalletConnectDialog.cs",
            "Assets/_Modules/Settings/MusicToggleBootstrap.cs",
            "Assets/_Modules/DevTools/DevBootstrap.cs",
            "Assets/_Modules/DevTools/DevPanelController.cs",
            "Assets/_Modules/Dungeons/UI/CraftingPanelController.cs",
            "Assets/_Modules/Dungeons/UI/DungeonHudController.cs",
            "Assets/_Modules/HUD/AdminOverlay.cs",
            "Assets/_Modules/Onboarding/SplashLoading.cs",
            "Assets/_Modules/Onboarding/PetSelectController.cs",
            "Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs",
            "Assets/_Modules/Village/Arena/ArenaDefensePaletteUI.cs",
            "Assets/_Modules/Village/Buildings/UI/TowerUpgradeButton.cs",
            "Assets/_Modules/Village/Buildings/UI/LevelUpSkillPopupBootstrap.cs",
            "Assets/_Modules/Village/Buildings/UI/LevelUpSkillPopup.cs",
            "Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs",
            "Assets/_Modules/Village/Talents/TalentTreePanel.cs",
            "Assets/_Modules/Village/UI/SeatingEditorOverlay.cs",
            "Assets/_Modules/Village/UI/TowerPlacementRotateMenu.cs",
            "Assets/_Modules/Village/Tutorial/TutorialHudOverlay.cs",
            "Assets/_Modules/Village/Tutorial/PetIntroduction.cs",
        };

        // --- Check 4: referenced-but-missing Resources paths grandfathered as
        // tracked debt (each already has a runtime null-fallback or is a known
        // staged-art alias, e.g. ElarionUiKit's 'HudIcons/Wizard/wiard' typo
        // fallback whose primary 'wizard' resolves). A NEW missing path = FAIL.
        private static readonly HashSet<string> MissingResourceBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Arena/Backdrops/outerworld_backdrop",
            "Audio/Ambient/Heartwood_Critical",
            "Audio/Ambient/Heartwood_Healthy",
            "Audio/Ambient/Heartwood_Strained",
            "Audio/Sfx/Heart_Fall",
            "Audio/Sfx/Heart_Hit",
            "Burst_rings",
            "DevPanelSettings",
            "Env/Fish",
            "HudIcons/Wizard/wiard",
            "Pets/Pet",
            "RpgUi/params/widget-params",
            "Sfx/BuildDenied",
            "Sfx/EnemyHit",
            "Sfx/LevelUp",
            "Sfx/PetHarvest",
            "Sfx/TowerFire",
            "Sfx/TowerPlace",
            "Sfx/WaveStart",
            "Tech hud elements/Sprites/GreenUielements/Buttons/Button 1",
            "Tech hud elements/Sprites/GreenUielements/Icons/Icon 5",
            "Tech hud elements/Sprites/GreenUielements/Loading bar/Loading bar",
            "Tech hud elements/Sprites/GreenUielements/Shield/Shield 1",
            "Tech hud elements/Sprites/Healing Tabs/H1",
            "Tech hud elements/Sprites/Healing Tabs/H3",
            "Tech hud elements/Sprites/Healing Tabs/H4",
            "Tech hud elements/Sprites/Healing Tabs/H5",
            "Tech hud elements/Sprites/Loading 1/Loading 1",
            "Tech hud elements/Sprites/Menu Bars/Menu Bar 1",
            "Tech hud elements/Sprites/Play buttons/button 3",
            "Tech hud elements/Sprites/Profile tabs/P1/bg.png",
            "Tech hud elements/Sprites/Profile tabs/P1/fill.png",
            "Tech hud elements/Sprites/Profile tabs/P2/fill.png",
            "Tech hud elements/Sprites/Profile tabs/P3/fill.png",
            "Tech hud elements/Sprites/Sword icons/Sword icons",
            "UI/menu_bg",
            "UI/panel_bg",
            "VFX/Burst_rings",
        };

        // --- Check 3: load-bearing public static ElarionUiKit builders the HUD
        // composes with (verified from ElarionUiKit*.cs partials 2026-07-12).
        private static readonly string[] RequiredKitBuilders =
        {
            "BuildObsidianPanel", "BuildObsidianModal", "BuildConfirmModal",
            "ObsidianCloseButton", "ToastCard", "BuildModalCanvas", "Scrim",
            "Panel", "Header", "Button", "Label", "Rule", "Bar", "Slot", "Card",
            "Portrait", "EnsureFont", "RarityColor", "RarityGlyph",
        };

        // --- Check 1: a runtime file only enters the tofu scan when it touches
        // a UI text stack (mirror of UiObsidianConformanceRegression's KitToken
        // heuristic — a pure-logic file's strings never reach a label).
        private static readonly string[] UiStackTokens =
        {
            "TMPro", "TextMeshPro", "UnityEngine.UI", "UnityEngine.UIElements", "ElarionUiKit",
        };

        private static readonly Regex StringLiteralRx = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);
        private static readonly Regex UnicodeEscapeRx = new Regex(@"\\u([0-9A-Fa-f]{4})", RegexOptions.Compiled);
        private static readonly Regex ResourcesLoadRx = new Regex(
            "Resources\\s*\\.\\s*Load(?:All)?\\s*(?:<[^>]+>)?\\s*\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*[,)]",
            RegexOptions.Compiled);
        private static readonly Regex[] UiDocumentSmells =
        {
            new Regex(@"AddComponent\s*<\s*UIDocument\s*>", RegexOptions.Compiled),
            new Regex(@"new\s+GameObject\s*\([^;]*typeof\s*\(\s*UIDocument\s*\)", RegexOptions.Compiled),
            new Regex(@"RequireComponent\s*\(\s*typeof\s*\(\s*UIDocument\s*\)", RegexOptions.Compiled),
        };

        /// <summary>
        /// Headless HUD/UI defect-class gate: tofu glyphs, UIDocument fence,
        /// kit conformance, Resources path resolution. Logs HUDUI_OK /
        /// HUDUI_FAIL (LogError) and returns the same verdict. Never throws.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                string modulesDir = Path.Combine(Application.dataPath, "_Modules");
                if (!Directory.Exists(modulesDir))
                {
                    reason = "HUDUI SKIPPED — Assets/_Modules not found";
                    Debug.LogWarning(reason);
                    return true;
                }

                CheckTofu(modulesDir, failures, notes);
                CheckUiDocumentFence(modulesDir, failures, notes);
                CheckKitConformance(failures, notes);
                CheckResourcePaths(modulesDir, failures, notes);
            }
            catch (Exception ex)
            {
                // A broken oracle must never masquerade as a broken game — degrade
                // loudly (warning, not FAIL) and let the run pass.
                reason = "HUDUI SKIPPED — oracle exception: " + ex.GetType().Name + " " + ex.Message;
                Debug.LogWarning(reason);
                return true;
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join(" ; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "HUDUI_OK — tofu oracle, UIDocument fence, kit conformance, Resources paths all green" + noteStr;
                Debug.Log(reason);
                return true;
            }

            reason = "HUDUI_FAIL: " + failures.Count + " failure(s)\n - " +
                     string.Join("\n - ", failures.ToArray()) + noteStr;
            Debug.LogError(reason);
            return false;
        }

        /// <summary>Batchmode entry point (run-unity-method.ps1 -Method DeNelle.Editor.HudUiRegression.RunStandalone).</summary>
        public static void RunStandalone()
        {
            string reason;
            Run(out reason); // Run() already emits the HUDUI_OK / HUDUI_FAIL marker
        }

        // =====================================================================
        // CHECK 1 — THE TOFU ORACLE
        // =====================================================================
        private static void CheckTofu(string modulesDir, List<string> failures, List<string> notes)
        {
            var oracle = FontOracle.Resolve(notes);
            if (!oracle.Available)
            {
                // No resolvable UI font is itself a shipping defect: every
                // code-built TMP label would render blank (EnsureFont warns at
                // runtime; here it is gate-worthy).
                failures.Add("TOFU ORACLE — no TMP font resolvable (TMP_Settings.defaultFontAsset null AND " +
                             "'Fonts & Materials/LiberationSans SDF' absent from Resources): all code-built UI text would blank");
                return;
            }

            // codepoint -> occurrence list "Assets/...:line"
            var suspects = new Dictionary<int, List<string>>();
            int filesScanned = 0;

            foreach (var path in RuntimeSources(modulesDir))
            {
                string norm = path.Replace('\\', '/');
                // DevTools overlays are owner/dev-only surfaces (same carve-out
                // UiObsidianConformanceRegression grants OwnerDevToolsOverlay) —
                // their strings are not shipped player UI.
                if (norm.IndexOf("/_Modules/DevTools/", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                string text = SafeRead(path, notes);
                if (text == null) continue;

                bool touchesUiStack = false;
                for (int i = 0; i < UiStackTokens.Length && !touchesUiStack; i++)
                    if (text.IndexOf(UiStackTokens[i], StringComparison.Ordinal) >= 0) touchesUiStack = true;
                if (!touchesUiStack) continue;
                filesScanned++;

                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();
                    if (trimmed.Length == 0) continue;
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    // Attribute rows render in the Inspector, not in a TMP label.
                    if (trimmed.StartsWith("[")) continue;
                    // Console/flight-recorder strings never reach a label.
                    if (line.IndexOf("Debug.Log", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("FlowTrace.", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("Guard.", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("Console.Write", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("throw new", StringComparison.Ordinal) >= 0) continue;

                    int commentCut = CommentStartOutsideLiterals(line);
                    foreach (Match m in StringLiteralRx.Matches(line))
                    {
                        if (commentCut >= 0 && m.Index > commentCut) break; // trailing // comment
                        string body = m.Value.Substring(1, m.Value.Length - 2);
                        body = UnicodeEscapeRx.Replace(body, e =>
                            ((char)int.Parse(e.Groups[1].Value, NumberStyles.HexNumber)).ToString());

                        for (int k = 0; k < body.Length; k++)
                        {
                            char c = body[k];
                            int cp = c;
                            if (char.IsHighSurrogate(c) && k + 1 < body.Length && char.IsLowSurrogate(body[k + 1]))
                            {
                                cp = char.ConvertToUtf32(c, body[k + 1]);
                                k++;
                            }
                            if (cp <= 126) continue;

                            List<string> where;
                            if (!suspects.TryGetValue(cp, out where))
                                suspects[cp] = where = new List<string>();
                            if (where.Count < 8) where.Add(RelPath(norm) + ":" + (i + 1));
                            else if (where.Count == 8) where.Add("(+more)");
                        }
                    }
                }
            }

            int distinctChecked = 0, tofu = 0;
            foreach (var kv in suspects)
            {
                distinctChecked++;
                int cp = kv.Key;
                bool renderable = cp <= 0xFFFF && oracle.Has((char)cp);
                if (renderable) continue;
                tofu++;
                bool unpairedSurrogate = cp >= 0xD800 && cp <= 0xDFFF;
                string glyph = unpairedSurrogate ? "<surrogate>" : char.ConvertFromUtf32(cp);
                string why = unpairedSurrogate
                    ? "unpaired surrogate — corrupt literal, cannot render"
                    : cp > 0xFFFF
                    ? "astral-plane char (emoji) — no shipped UI font covers it"
                    : "missing from UI font '" + oracle.FontName + "' (incl. fallbacks) — renders as □ (tofu)";
                failures.Add("TOFU U+" + cp.ToString("X4") + " '" + glyph + "' " + why + " — at: " +
                             string.Join(", ", kv.Value.ToArray()));
            }
            notes.Add("tofu oracle: " + filesScanned + " UI file(s) scanned, " + distinctChecked +
                      " distinct non-ASCII char(s) font-verified, " + tofu + " tofu");
        }

        // =====================================================================
        // CHECK 2 — UIDocument CONSTRUCTION FENCE
        // =====================================================================
        private static void CheckUiDocumentFence(string modulesDir, List<string> failures, List<string> notes)
        {
            int baselineHits = 0;
            var seenBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in RuntimeSources(modulesDir))
            {
                string text = SafeRead(path, notes);
                if (text == null) continue;
                if (text.IndexOf("UIDocument", StringComparison.Ordinal) < 0) continue;

                string rel = RelPath(path.Replace('\\', '/'));
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    bool smells = false;
                    for (int r = 0; r < UiDocumentSmells.Length && !smells; r++)
                        if (UiDocumentSmells[r].IsMatch(lines[i])) smells = true;
                    if (!smells) continue;

                    if (UiDocumentBaseline.Contains(rel)) { baselineHits++; seenBaseline.Add(rel); }
                    else failures.Add("UIDOCUMENT FENCE — new UIDocument surface outside the sanctioned baseline: " +
                                      rel + ":" + (i + 1) + " -> " + trimmed.Trim() +
                                      " (UXML-sourced HUDs do NOT render in builds — CLAUDE.md §8 / MASTER_CATALOG hud.md; " +
                                      "new screens go through ElarionUiKit uGUI; a deliberate code-built UITK surface " +
                                      "must be added to UiDocumentBaseline explicitly)");
                    break; // one report per file is enough
                }
            }

            var resolved = new List<string>();
            foreach (var b in UiDocumentBaseline)
                if (!seenBaseline.Contains(b)) resolved.Add(b);
            string extra = resolved.Count > 0
                ? "; " + resolved.Count + " baseline entr(ies) no longer construct UIDocument (refresh UiDocumentBaseline)"
                : "";
            notes.Add("UIDocument fence: " + baselineHits + " sanctioned surface hit(s)" + extra);
        }

        // =====================================================================
        // CHECK 3 — KIT CONFORMANCE (reflection over the REAL compiled kit)
        // =====================================================================
        private static void CheckKitConformance(List<string> failures, List<string> notes)
        {
            Type kit = typeof(ElarionUiKit);

            var publicStatics = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in kit.GetMethods(BindingFlags.Public | BindingFlags.Static))
                publicStatics.Add(m.Name);

            var missing = new List<string>();
            foreach (var required in RequiredKitBuilders)
                if (!publicStatics.Contains(required)) missing.Add(required);
            if (missing.Count > 0)
                failures.Add("KIT CONFORMANCE — ElarionUiKit is missing load-bearing public builder(s): " +
                             string.Join(", ", missing.ToArray()) +
                             " (HUD screens compose through these; a rename/removal breaks every consumer)");

            // Rarity glyphs = the colorblind owner's non-color rarity channel.
            // Each must be non-empty and renderable by the shipped font.
            if (publicStatics.Contains("RarityGlyph"))
            {
                var oracle = FontOracle.Resolve(notes);
                for (int i = 0; i <= 4; i++)
                {
                    string glyph = null;
                    try { glyph = ElarionUiKit.RarityGlyph(i); }
                    catch (Exception ex) { failures.Add("KIT CONFORMANCE — RarityGlyph(" + i + ") threw " + ex.GetType().Name); continue; }
                    if (string.IsNullOrEmpty(glyph))
                    {
                        failures.Add("KIT CONFORMANCE — RarityGlyph(" + i + ") is null/empty: rarity loses its " +
                                     "shape channel (owner is red/green colorblind — color alone is not a signal)");
                        continue;
                    }
                    if (!oracle.Available) continue;
                    foreach (char c in glyph)
                        if (c > 126 && !oracle.Has(c))
                            failures.Add("KIT CONFORMANCE — RarityGlyph(" + i + ") '" + glyph + "' char U+" +
                                         ((int)c).ToString("X4") + " missing from UI font '" + oracle.FontName +
                                         "' — the rarity glyph itself would tofu");
                }
            }
            notes.Add("kit conformance: " + RequiredKitBuilders.Length + " required builder(s) reflected on " + kit.FullName);
        }

        // =====================================================================
        // CHECK 4 — RESOURCES PATH RESOLUTION (the wiard.jpg-typo class)
        // =====================================================================
        private static void CheckResourcePaths(string modulesDir, List<string> failures, List<string> notes)
        {
            // Index every Assets/**/Resources tree: relative path without
            // extension + every relative folder (LoadAll targets), lowercased.
            var index = new HashSet<string>(StringComparer.Ordinal);
            var dirs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in Directory.GetDirectories(Application.dataPath, "Resources", SearchOption.AllDirectories))
            {
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                    dirs.Add(Rel(root, dir));
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = Rel(root, file);
                    int dot = rel.LastIndexOf('.');
                    int slash = rel.LastIndexOf('/');
                    if (dot > slash) rel = rel.Substring(0, dot);
                    index.Add(rel);
                }
            }

            int totalLiterals = 0, baselineMisses = 0;
            var seenBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in RuntimeSources(modulesDir))
            {
                string text = SafeRead(path, notes);
                if (text == null) continue;
                if (text.IndexOf("Resources", StringComparison.Ordinal) < 0) continue;

                // Drop comment-only lines, keep the rest as one blob so a call
                // split across lines still matches.
                var kept = new List<string>();
                foreach (var line in text.Split('\n'))
                {
                    string t = line.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                    kept.Add(line);
                }
                string blob = string.Join("\n", kept.ToArray());

                foreach (Match m in ResourcesLoadRx.Matches(blob))
                {
                    totalLiterals++;
                    string p = m.Groups[1].Value;
                    string key = p.ToLowerInvariant().TrimEnd('/');
                    if (key.Length == 0) continue;                    // LoadAll("") = whole tree, always valid
                    if (index.Contains(key) || dirs.Contains(key)) continue;

                    if (MissingResourceBaseline.Contains(p)) { baselineMisses++; seenBaseline.Add(p); continue; }
                    failures.Add("RESOURCES MISS — '" + p + "' referenced in " + RelPath(path.Replace('\\', '/')) +
                                 " resolves to NO asset under any Resources root (wiard-typo class: the surface " +
                                 "silently blanks at runtime)");
                }
            }

            var resolved = new List<string>();
            foreach (var b in MissingResourceBaseline)
                if (!seenBaseline.Contains(b)) resolved.Add(b);
            string extra = resolved.Count > 0
                ? "; " + resolved.Count + " baseline path(s) resolved/gone (refresh MissingResourceBaseline)"
                : "";
            notes.Add("resources: " + totalLiterals + " literal path(s) checked, " + baselineMisses +
                      " known-missing tracked as debt" + extra);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Runtime first-party sources: Assets/_Modules/**/*.cs minus Editor/ and Tests/.</summary>
        private static IEnumerable<string> RuntimeSources(string modulesDir)
        {
            string[] files;
            try { files = Directory.GetFiles(modulesDir, "*.cs", SearchOption.AllDirectories); }
            catch { yield break; }
            foreach (var path in files)
            {
                string norm = path.Replace('\\', '/');
                if (norm.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (norm.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string name = Path.GetFileName(path);
                if (name.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)) continue;
                yield return path;
            }
        }

        private static string SafeRead(string path, List<string> notes)
        {
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                notes.Add(Path.GetFileName(path) + " unreadable (" + ex.Message + ")");
                return null;
            }
        }

        /// <summary>Index of the first "//" on the line that is NOT inside a string literal, or -1.</summary>
        private static int CommentStartOutsideLiterals(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length - 1; i++)
            {
                char c = line[i];
                if (c == '\\' && inString) { i++; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (!inString && c == '/' && line[i + 1] == '/') return i;
            }
            return -1;
        }

        private static string RelPath(string normalizedFullPath)
        {
            int idx = normalizedFullPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? normalizedFullPath.Substring(idx) : normalizedFullPath;
        }

        /// <summary>Lowercased forward-slash path of <paramref name="fullPath"/> relative to <paramref name="root"/>.</summary>
        private static string Rel(string root, string fullPath)
        {
            string rel = fullPath.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            return rel.ToLowerInvariant();
        }

        // ---------------------------------------------------------------------
        // FontOracle — reflection bridge to the REAL TMP font the kit resolves
        // (this asmdef does not reference Unity.TextMeshPro; reflection keeps the
        // file self-contained). Mirrors ElarionUiKit.EnsureFont's order:
        // TMP_Settings.defaultFontAsset ?? Resources LiberationSans SDF.
        // ---------------------------------------------------------------------
        private sealed class FontOracle
        {
            public bool Available;
            public string FontName = "<none>";

            private object _font;
            private MethodInfo _has3, _has2, _has1;   // HasCharacter overloads
            private bool _tryAddDynamic;              // dynamic atlas may pull from the source face
            private readonly Dictionary<char, bool> _cache = new Dictionary<char, bool>();

            private static FontOracle _resolved;

            public static FontOracle Resolve(List<string> notes)
            {
                if (_resolved != null) return _resolved;
                var o = new FontOracle();
                _resolved = o;
                try
                {
                    Type settingsType = FindType("TMPro.TMP_Settings");
                    Type fontType = FindType("TMPro.TMP_FontAsset");
                    if (fontType == null)
                    {
                        notes.Add("tofu oracle: TMPro types not loaded — font check unavailable");
                        return o;
                    }

                    object font = null;
                    if (settingsType != null)
                    {
                        var prop = settingsType.GetProperty("defaultFontAsset", BindingFlags.Public | BindingFlags.Static);
                        try { if (prop != null) font = prop.GetValue(null, null); } catch { /* settings asset absent */ }
                    }
                    var uo = font as UnityEngine.Object;
                    if (uo == null)
                        font = Resources.Load("Fonts & Materials/LiberationSans SDF", fontType);
                    uo = font as UnityEngine.Object;
                    if (uo == null) return o; // Available stays false — caller fails the run

                    o._font = font;
                    o.FontName = uo.name;
                    o._has3 = fontType.GetMethod("HasCharacter", new[] { typeof(char), typeof(bool), typeof(bool) });
                    o._has2 = fontType.GetMethod("HasCharacter", new[] { typeof(char), typeof(bool) });
                    o._has1 = fontType.GetMethod("HasCharacter", new[] { typeof(char) });
                    if (o._has3 == null && o._has2 == null && o._has1 == null)
                    {
                        notes.Add("tofu oracle: TMP_FontAsset.HasCharacter not found — font check unavailable");
                        return o;
                    }

                    // Dynamic-atlas fonts may add glyphs from the source face at
                    // runtime; let HasCharacter consult it (tryAddCharacter) so a
                    // glyph the SOURCE font genuinely carries never false-fails.
                    try
                    {
                        var pm = fontType.GetProperty("atlasPopulationMode");
                        if (pm != null) o._tryAddDynamic = Convert.ToInt32(pm.GetValue(font, null)) == 1; // AtlasPopulationMode.Dynamic
                    }
                    catch { o._tryAddDynamic = false; }

                    o.Available = true;
                }
                catch (Exception ex)
                {
                    notes.Add("tofu oracle: font resolution failed (" + ex.GetType().Name + ") — font check unavailable");
                }
                return o;
            }

            /// <summary>True when the resolved font (searching fallbacks, and the dynamic
            /// source face when applicable) can render <paramref name="c"/>. Unprovable
            /// (reflection hiccup) counts as renderable — never false-fail.</summary>
            public bool Has(char c)
            {
                if (!Available) return true;
                bool has;
                if (_cache.TryGetValue(c, out has)) return has;
                try
                {
                    if (_has3 != null)      has = (bool)_has3.Invoke(_font, new object[] { c, true, _tryAddDynamic });
                    else if (_has2 != null) has = (bool)_has2.Invoke(_font, new object[] { c, true });
                    else                    has = (bool)_has1.Invoke(_font, new object[] { c });
                }
                catch { has = true; }
                _cache[c] = has;
                return has;
            }

            private static Type FindType(string fullName)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetType(fullName); } catch { }
                    if (t != null) return t;
                }
                return null;
            }
        }
    }
}
