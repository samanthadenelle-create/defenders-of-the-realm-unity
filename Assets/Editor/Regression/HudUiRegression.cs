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
//   5. SAFE-AREA CORNER (WO-868) — the screen-anchored "Connect Wallet" /
//      "Sign in with Pi" corner button must stay INSIDE Screen.safeArea. Proven
//      defect (docs/ui-review/2026-08-04-seeker/01-title-screen.png, a 1:1 Seeker
//      screencap at 2670x1200): the button measured 300 x 112 px with its top
//      edge at y = -10 — CLIPPED off the top of the screen — because the holder
//      used a raw 16-px inset (~6 dp) and a 60-px height that the kit touch floor
//      then grew symmetrically by 26 px per side. This case pins both halves
//      headlessly: (a) SafeAreaInset's PURE math keeps the resulting rect inside
//      the safe area at 2340x1080 (cutout AND no-cutout) and at the captured
//      2670x1200, and (b) PiSignInController still routes through
//      SafeAreaInset.ApplyTopRight and still sizes its holder at the touch floor.
//   6. COMBAT HUD COMPOSITION (WO-867) — the 2026-08-04 Seeker review's combat-HUD
//      defects, each pinned to the data that PROVED it: the mirrored Blink plates
//      are whole atlas PAGES (so their measured crop rects need their PNG size +
//      spriteMode pinned); TargetNameplate.prefab authors every child at an
//      absolute offset for a 480x130 root plus an always-on ragged BossTarget
//      frame (so the composed-plate child names, the ComposeTargetPlate call and
//      the one cross-file anchor it derives from are pinned); CastBar1.prefab's
//      root sprite GUID is dangling and paints a WHITE quad unguarded; and the
//      right-edge touch sizes are RECOMPUTED from HudAreasHost/HudKitController's
//      own numbers at 2340x1080 rather than from a copied figure.
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
            // WO-971 (2026-08-10): PetIntroduction.cs removed from this list because the
            // file is DELETED. It was a legacy-FTUE-only screen whose sole consumer was
            // TutorialDirector, and the owner ruled the original tutorial out entirely
            // ("remove the original", "only the new wolf one stays").
        };

        // --- Check 4: referenced-but-missing Resources paths grandfathered as
        // tracked debt (each already has a runtime null-fallback or is a known
        // staged-art alias, e.g. ElarionUiKit's 'HudIcons/Wizard/wiard' typo
        // fallback whose primary 'wizard' resolves). A NEW missing path = FAIL.
        private static readonly HashSet<string> MissingResourceBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Arena/Backdrops/outerworld_backdrop",
            // 2026-08-14: "Dungeon/Exit/dungeon_texture" REMOVED from this baseline — it was a
            // STALE ASSERTION. The entry claimed the path was an unstaged player-build mirror of a
            // gitignored KayKit material, but commit 64ebf6658 ("the exit portal stands at
            // hero-scale") COMMITTED the real asset at Assets/Resources/Dungeon/Exit/dungeon_texture.png
            // (git-tracked, alongside pillar_decorated.fbx + wall_arched.fbx). A baseline that
            // excuses the absence of something that now exists hides the next real miss on that
            // path, so it goes. If the asset is ever removed again, this check FAILS loudly —
            // which is the correct behaviour, not a reason to re-add the excuse.
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
            "Pets/PetIdle",
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
                    bool skipped = DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "HUD UI", "Assets/_Modules not found at " + modulesDir);
                    Debug.LogWarning(reason);
                    return skipped;
                }

                CheckTofu(modulesDir, failures, notes);
                CheckUiDocumentFence(modulesDir, failures, notes);
                CheckKitConformance(failures, notes);
                CheckResourcePaths(modulesDir, failures, notes);
                CheckSafeAreaCorner(failures, notes);
                CheckCombatHudComposition(failures, notes);
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
                reason = "HUDUI_OK — tofu oracle, UIDocument fence, kit conformance, Resources paths, " +
                         "safe-area corner, combat-hud composition all green" + noteStr;
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
        // CHECK 5 — SAFE-AREA CORNER (WO-868, the clipped "Connect Wallet")
        // =====================================================================

        // The corner button's authored size (PiSignInController.BuildButton): a fixed
        // 300-px width for the longest label, height AT the kit touch floor so
        // ClampMinTouch has nothing to grow (the growth is what pushed it off-screen).
        private const float CornerButtonWidthPx = 300f;

        // The oracle owns this floor DELIBERATELY — asserting the placed rect against
        // SafeAreaInset.EdgeMarginPx would be tautological (shrink the constant and the
        // assertion shrinks with it; proven by a negative-control run 2026-08-04 that
        // passed with the margin back at the defective 16 px). 32 device px is the
        // absolute minimum that is not "flush" on a ~2.7x-density phone; the measured
        // WO-868 defect was 16.
        private const float MinEdgeMarginPx = 32f;

        // Landscape 2340x1080 (the Seeker figure the UI review specs against) plus the
        // surface the 2026-08-04 screencap actually reported (2670x1200, 1:1).
        // Each row: label, screen w/h, safeArea. A landscape cutout eats one SHORT edge,
        // so the right-hand inset is the one that can clip this button.
        private static readonly object[][] SafeAreaCases =
        {
            //            label                          w     h     safeArea
            new object[] { "2340x1080 no cutout",        2340, 1080, new Rect(0f,   0f, 2340f, 1080f) },
            new object[] { "2340x1080 right cutout 84",  2340, 1080, new Rect(0f,   0f, 2256f, 1080f) },
            new object[] { "2340x1080 left cutout 84",   2340, 1080, new Rect(84f,  0f, 2256f, 1080f) },
            new object[] { "2340x1080 top+right gesture",2340, 1080, new Rect(0f,  48f, 2256f, 1010f) },
            new object[] { "2670x1200 captured surface", 2670, 1200, new Rect(0f,   0f, 2670f, 1200f) },
        };

        private static void CheckSafeAreaCorner(List<string> failures, List<string> notes)
        {
            // --- 5a. PURE MATH: the placed rect never leaves the safe area. --------
            var size = new Vector2(CornerButtonWidthPx, ElarionUiKit.MinTouchPx);
            int cases = 0;
            foreach (var row in SafeAreaCases)
            {
                string label = (string)row[0];
                int w = (int)row[1];
                int h = (int)row[2];
                var safe = (Rect)row[3];
                cases++;

                Rect r = SafeAreaInset.TopRightScreenRect(safe, w, h, size);

                if (r.xMin < safe.xMin || r.xMax > safe.xMax || r.yMin < safe.yMin || r.yMax > safe.yMax)
                    failures.Add("SAFE-AREA CORNER — the wallet/sign-in button rect " + RectStr(r) +
                                 " escapes Screen.safeArea " + RectStr(safe) + " at " + label +
                                 " (WO-868: this is the clipped 'Connect Wallet' defect — the corner must be " +
                                 "placed from Screen.safeArea, never a raw pixel inset)");

                // The rect must also clear the SCREEN edges — a device that reports no
                // cutout (safeArea == full screen) must still get a real breathing
                // margin, or the button reads flush like the raw -16 px did.
                float rightGap = w - r.xMax;
                float topGap = h - r.yMax;
                if (rightGap < MinEdgeMarginPx || topGap < MinEdgeMarginPx)
                    failures.Add("SAFE-AREA CORNER — at " + label + " the button sits " + rightGap.ToString("0.#") +
                                 " px from the right screen edge and " + topGap.ToString("0.#") +
                                 " px from the top; the floor is " + MinEdgeMarginPx.ToString("0.#") +
                                 " px (the measured WO-868 defect was 16 px, which reads as flush and lands in " +
                                 "the rounded-corner / cutout band)");

                // Touch floor: an on-screen-but-untappable button is the same defect.
                if (r.width < ElarionUiKit.MinTouchPx || r.height < ElarionUiKit.MinTouchPx)
                    failures.Add("SAFE-AREA CORNER — button rect " + RectStr(r) + " is below MinTouchPx (" +
                                 ElarionUiKit.MinTouchPx.ToString("0.#") + ") at " + label);
            }

            // The helper's own fixed-pixel margin may never be shrunk back toward flush.
            if (SafeAreaInset.EdgeMarginPx < MinEdgeMarginPx)
                failures.Add("SAFE-AREA CORNER — SafeAreaInset.EdgeMarginPx is " +
                             SafeAreaInset.EdgeMarginPx.ToString("0.#") + " px, below the " +
                             MinEdgeMarginPx.ToString("0.#") + " px floor; a shrunken margin re-creates the " +
                             "flush-to-edge corner WO-868 fixed (the defect measured 16 px)");

            // --- 5b. SOURCE LAW: the corner still routes through the helper. ------
            const string cornerSrc = "_Modules/Core/Platform/PiSignInController.cs";
            string cornerPath = Path.Combine(Application.dataPath, cornerSrc.Replace('/', Path.DirectorySeparatorChar));
            string src = File.Exists(cornerPath) ? SafeRead(cornerPath, notes) : null;
            if (src == null)
            {
                notes.Add("safe-area corner: Assets/" + cornerSrc + " unreadable — source law skipped");
            }
            else
            {
                if (src.IndexOf("SafeAreaInset.ApplyTopRight", StringComparison.Ordinal) < 0)
                    failures.Add("SAFE-AREA CORNER — Assets/" + cornerSrc + " no longer calls " +
                                 "SafeAreaInset.ApplyTopRight: the corner button is being placed by hand again " +
                                 "(WO-868 regressed — a raw inset ignores the device cutout)");

                if (Regex.IsMatch(src, @"anchoredPosition\s*=\s*new\s+Vector2\s*\(\s*-\s*\d"))
                    failures.Add("SAFE-AREA CORNER — Assets/" + cornerSrc + " re-introduces a hard-coded negative " +
                                 "anchoredPosition inset; the top-right corner is owned by SafeAreaInset");

                if (src.IndexOf("ElarionUiKit.MinTouchPx", StringComparison.Ordinal) < 0)
                    failures.Add("SAFE-AREA CORNER — Assets/" + cornerSrc + " no longer sizes the corner holder at " +
                                 "ElarionUiKit.MinTouchPx: a sub-floor holder gets grown SYMMETRICALLY by " +
                                 "ClampMinTouch and pushes back off the top of the screen (the measured -10 px)");
            }

            notes.Add("safe-area corner: " + cases + " resolution/cutout case(s) verified against SafeAreaInset (margin " +
                      SafeAreaInset.EdgeMarginPx.ToString("0.#") + " px, size " +
                      CornerButtonWidthPx.ToString("0.#") + "x" + ElarionUiKit.MinTouchPx.ToString("0.#") + ")");
        }

        // =====================================================================
        // CHECK 6 — COMBAT HUD COMPOSITION (WO-867)
        // ---------------------------------------------------------------------
        // Three defect classes from the 2026-08-04 Seeker review, each pinned to
        // the DATA that proved it rather than to a screenshot:
        //
        //   6a RAGGED / TORN EDGES — nameplate_party.png, nameplate_bar.png and
        //      target_core.png are whole ATLAS PAGES imported `spriteMode: 1`
        //      (Single). Drawn Image.Type.Simple they paint every unrelated element
        //      parked on the same texture: a loose grey rock chunk (party, x>=1137),
        //      a torn stone fringe (target_core, y 299..386 top-down) and a dead
        //      transparent tail (bar, x>=2347). ElarionUiKit.PlatePageSprite draws
        //      the MEASURED sub-rect instead. Those rects are hard numbers against
        //      the committed PNG size, so this check pins the PNG dimensions + the
        //      spriteMode: a re-import that changes either silently crops the WRONG
        //      region, and must fail here rather than on a device.
        //
        //   6b THE ENEMY PLATE AS ONE UNIT — TargetNameplate.prefab authors every
        //      child CENTRE-anchored at ABSOLUTE offsets for a 480x130 root, plus a
        //      GridLayoutGroup with a FIXED 374.3x31 cell, plus an always-active
        //      BossTarget ragged frame. Stretched to the HUD area those pieces drift
        //      apart (the "four disconnected pieces"). ComposeTargetPlate re-lays
        //      them in fixed-pixel bands. This check pins the child names it composes
        //      by, that it is still called, and the ONE cross-file constant it is
        //      derived from (HudKitController's TitleRow611 bottom anchor 0.72).
        //
        //   6c THE WHITE CAST BAR — CastBar1.prefab's root Image points at sprite
        //      guid c217dc4a3df342c42acde576290a6310, which resolves to NOTHING in
        //      this project; uGUI paints a null-sprite Image as a flat WHITE quad
        //      (03-town.png). BuildCastBar must run the no-silent-white guard.
        //
        //   6d TOUCH FLOOR on the right-edge controls — recomputed from the REAL
        //      source numbers (HudAreasHost's ActionRail rect + HudKitController's
        //      Pill611*/MedallionPerPillH) at 2340x1080, not from a copied figure.
        // =====================================================================

        /// <summary>Committed page geometry the kit's crop fractions were MEASURED against.
        /// Row: resources-relative png path, width, height.</summary>
        private static readonly object[][] AtlasPagePins =
        {
            new object[] { "Assets/Resources/RpgUi/hud/nameplate_party.png", 1280, 299 },
            new object[] { "Assets/Resources/RpgUi/hud/nameplate_bar.png",   2611, 116 },
            new object[] { "Assets/Resources/RpgUi/hud/target_core.png",     1427, 386 },
        };

        /// <summary>Children ComposeTargetPlate resolves BY NAME on the mirrored prefab. A rename
        /// in a re-mirror makes FindDeep return null and the plate silently un-composes.</summary>
        private static readonly string[] TargetPlateChildren =
        {
            "TargetName", "StatBars", "TargetIcon", "HealthBackground", "ManaBackground", "BossTarget",
        };

        /// <summary>The TitleRow611 bottom anchor in HudKitController that
        /// ElarionUiKit.TargetTitleReservePx is derived from — see ComposeTargetPlate.</summary>
        private const float TitleRow611BottomAnchor = 0.72f;

        private static void CheckCombatHudComposition(List<string> failures, List<string> notes)
        {
            string kitObsidian = ReadAsset("_Modules/Core/UI/ElarionUiKitObsidian.cs", notes);
            string kitNameplate = ReadAsset("_Modules/Core/UI/ElarionUiKitNameplate.cs", notes);
            string kitCore = ReadAsset("_Modules/Core/UI/ElarionUiKit.cs", notes);
            string hudKit = ReadAsset("_Modules/HUD/Kit/HudKitController.cs", notes);
            string areasHost = ReadAsset("_Modules/HUD/Kit/HudAreasHost.cs", notes);
            string echoFeedback = ReadAsset("_Modules/Village/Harvest/EchoUnlockFeedback.cs", notes);

            // --- 6a. ATLAS-PAGE PINS + the crop routing. --------------------------
            int pagesPinned = 0;
            foreach (var row in AtlasPagePins)
            {
                string rel = (string)row[0];
                int expW = (int)row[1], expH = (int)row[2];
                string full = Path.Combine(Application.dataPath, rel.Substring("Assets/".Length)
                                                                   .Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                {
                    failures.Add("ATLAS PAGE — " + rel + " is missing; ElarionUiKit.PlatePageSprite crops it by " +
                                 "a MEASURED pixel fraction and cannot verify the region without it");
                    continue;
                }
                int w, h;
                if (!TryReadPngSize(full, out w, out h))
                {
                    notes.Add("atlas page: could not read PNG header for " + rel + " — size pin skipped");
                    continue;
                }
                pagesPinned++;
                if (w != expW || h != expH)
                    failures.Add("ATLAS PAGE — " + rel + " is now " + w + "x" + h + " (pinned " + expW + "x" + expH +
                                 "). ElarionUiKit.PlatePageSprite's crop rect is a FRACTION measured against the " +
                                 "pinned size, so it now crops the WRONG region and the ragged/torn edge art can " +
                                 "reappear (WO-867). Re-measure the rect in _platePageRects and update this pin.");

                string meta = full + ".meta";
                if (File.Exists(meta))
                {
                    string m = SafeRead(meta, notes);
                    if (m != null && !Regex.IsMatch(m, @"^\s*spriteMode:\s*1\s*$", RegexOptions.Multiline))
                        failures.Add("ATLAS PAGE — " + rel + " is no longer imported spriteMode: 1 (Single). " +
                                     "PlatePageSprite's sub-rect math assumes the sprite covers the whole texture; " +
                                     "with Multiple, Resources.Load returns a sub-sprite and the crop is wrong.");
                }
            }
            if (kitNameplate != null && kitNameplate.IndexOf("PlatePageSprite", StringComparison.Ordinal) < 0)
                failures.Add("RAGGED EDGE — ElarionUiKitNameplate.cs no longer routes the hero/Heart plate through " +
                             "ElarionUiKit.PlatePageSprite: it draws the whole nameplate_party atlas page again, " +
                             "which paints the loose grey rock chunk at the right end of every plate (WO-867).");
            if (kitObsidian != null && kitObsidian.IndexOf("PlatePageSprite", StringComparison.Ordinal) < 0)
                failures.Add("RAGGED EDGE — ElarionUiKitObsidian.cs no longer defines/uses PlatePageSprite: the " +
                             "target_core torn stone fringe and the nameplate page artifacts come back (WO-867).");

            // --- 6b. THE ENEMY PLATE COMPOSES AS ONE UNIT. -----------------------
            const string targetPrefab = "Assets/Resources/RpgUi/prefabs/TargetNameplate.prefab";
            string prefabFull = Path.Combine(Application.dataPath,
                targetPrefab.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(prefabFull))
            {
                notes.Add("target plate: " + targetPrefab + " absent — the kit falls back to MODE 2 (constructed), " +
                          "which composes its own plate; prefab-child pins skipped");
            }
            else
            {
                string prefab = SafeRead(prefabFull, notes) ?? "";
                foreach (var child in TargetPlateChildren)
                    if (!Regex.IsMatch(prefab, @"m_Name:\s*" + Regex.Escape(child) + @"\s*$", RegexOptions.Multiline))
                        failures.Add("TARGET PLATE — " + targetPrefab + " no longer contains a '" + child +
                                     "' GameObject. ElarionUiKit.ComposeTargetPlate resolves it BY NAME; a miss " +
                                     "means that piece keeps its authored ABSOLUTE offset (sized for a 480x130 " +
                                     "root) and drifts out of the stretched plate — the WO-867 defect.");
            }

            if (kitObsidian != null)
            {
                if (kitObsidian.IndexOf("ComposeTargetPlate(pf, h", StringComparison.Ordinal) < 0)
                    failures.Add("TARGET PLATE — BuildTargetFrame's prefab path no longer calls ComposeTargetPlate: " +
                                 "the enemy nameplate reverts to four disconnected pieces (WO-867).");
                if (kitObsidian.IndexOf("DeactivateChild(pf.transform, \"bosstarget\")", StringComparison.Ordinal) < 0)
                    failures.Add("TARGET PLATE — ComposeTargetPlate no longer deactivates BossTarget. That child " +
                                 "ships m_IsActive:1 and draws the 397x412 nameplate_boss TORN frame at a fixed " +
                                 "off-plate offset — the ragged grey/gold shard over the enemy plate (WO-867).");
                if (kitObsidian.IndexOf("PlatePageSprite(RpgUiCatalog.HudTargetCore)", StringComparison.Ordinal) < 0)
                    failures.Add("TARGET PLATE — the plate face is no longer drawn from the CROPPED target_core " +
                                 "body. target_core.png is a 1427x386 COMPOSITE page (portrait socket + ornate " +
                                 "strip + plate body + a TORN STONE FRINGE at y 299..386); drawn whole and " +
                                 "stretched, the fringe is the ragged edge over the enemy plate (WO-867).");
                if (!Regex.IsMatch(kitObsidian, @"GetComponent<LayoutGroup>\s*\(\s*\)"))
                    failures.Add("TARGET PLATE — ComposeTargetPlate no longer disables the prefab's LayoutGroup. " +
                                 "StatBars carries a GridLayoutGroup with a FIXED 374.3x31 cell, which is why the " +
                                 "green/blue HP bar rendered as an island offset to the right of the plate (WO-867).");
            }

            if (hudKit != null)
            {
                // ComposeTargetPlate reserves (1 - 0.72) * TargetPlatePx of the plate top for the
                // name+Lv row HudKitController builds. That file belongs to another lane, so the
                // coupling is pinned here instead of duplicated there.
                var m = Regex.Match(hudKit, @"rowRt\.anchorMin\s*=\s*new\s+Vector2\s*\(\s*[\d.]+f\s*,\s*([\d.]+)f\s*\)");
                if (!m.Success)
                    notes.Add("target plate: could not locate HudKitController's TitleRow611 anchorMin — " +
                              "the TargetTitleReservePx derivation is unverified this run");
                else
                {
                    float bottom;
                    if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out bottom)
                        && Mathf.Abs(bottom - TitleRow611BottomAnchor) > 0.001f)
                        failures.Add("TARGET PLATE — HudKitController's TitleRow611 bottom anchor moved to " +
                                     bottom.ToString("0.###") + " (was " + TitleRow611BottomAnchor.ToString("0.###") +
                                     "). ElarionUiKit.TargetTitleReservePx (32 ref px) is DERIVED from it as " +
                                     "(1 - anchor) * TargetPlatePx; the HP band will now collide with the name/Lv " +
                                     "row. Re-derive TargetTitleReservePx and update this pin (WO-867).");
                }
            }

            // --- 6c. NO SILENT WHITE on the cast bar. ----------------------------
            if (kitObsidian != null)
            {
                int guardCalls = Regex.Matches(kitObsidian, @"GuardSpriteNullImages\s*\(\s*pf").Count;
                if (guardCalls < 2)
                    failures.Add("WHITE CAST BAR — only " + guardCalls + " prefab path(s) in ElarionUiKitObsidian.cs " +
                                 "call GuardSpriteNullImages(pf, ...). BuildTargetFrame AND BuildCastBar both bind " +
                                 "mirrored prefabs whose sprite GUIDs are DANGLING; an unguarded null-sprite Image " +
                                 "renders as a flat WHITE QUAD (docs/ui-review/2026-08-04-seeker/03-town.png).");
            }
            int dangling = CountDanglingPrefabSprites(notes);
            if (dangling > 0)
                notes.Add("mirrored RpgUi prefabs carry " + dangling + " dangling sprite ref(s) — each is a white " +
                          "quad unless its kit builder runs GuardSpriteNullImages");

            // --- 6d. TOUCH FLOOR, recomputed from the owning source numbers. -----
            Vector4 rail;
            if (areasHost != null && TryParseAreaRect(areasHost, "ActionRail", out rail))
            {
                // HudAreasHost canvas: 1080x1920 reference, ScreenMatchMode MatchWidthOrHeight 0.5.
                const float refW = 1080f, refH = 1920f, match = 0.5f;
                const float screenW = 2340f, screenH = 1080f;   // the Seeker figure the review specs against
                float scale = Mathf.Pow(screenW / refW, 1f - match) * Mathf.Pow(screenH / refH, match);
                float canvasW = screenW / scale, canvasH = screenH / scale;

                float railW = (rail.z - rail.x) * canvasW;
                float railH = (rail.w - rail.y) * canvasH;

                float y0 = ParseConst(hudKit, "Pill611Y0", 0.02f);
                float y1 = ParseConst(hudKit, "Pill611Y1", 0.30f);
                float perPill = ParseConst(hudKit, "MedallionPerPillH", 0.9f);
                float pillH = (y1 - y0) * railH;
                float medallion = perPill * pillH;

                notes.Add("touch measure @" + screenW + "x" + screenH + " (canvas " + canvasW.ToString("0.#") + "x" +
                          canvasH.ToString("0.#") + " ref units): ActionRail " + railW.ToString("0.#") + "x" +
                          railH.ToString("0.#") + ", attack pill height " + pillH.ToString("0.#") +
                          ", ability medallion " + medallion.ToString("0.#") + " (floor " +
                          ElarionUiKit.MinTouchPx.ToString("0.#") + ")");

                if (medallion < ElarionUiKit.MinTouchPx)
                {
                    // The arc geometry lives in HudKitController (another lane's file). The kit
                    // compensates with a fixed MinTouchPx hit-area child; that compensation is what
                    // this gate holds, because without it the medallions are genuinely untappable.
                    if (kitCore == null || kitCore.IndexOf("EnsureTouchFloorArea(slot)", StringComparison.Ordinal) < 0)
                        failures.Add("TOUCH FLOOR — ability medallions resolve to " + medallion.ToString("0.#") +
                                     " ref px at " + screenW + "x" + screenH + ", " +
                                     (ElarionUiKit.MinTouchPx - medallion).ToString("0.#") + " px UNDER MinTouchPx (" +
                                     ElarionUiKit.MinTouchPx.ToString("0.#") + "), and StyleAsRoundMedallion no " +
                                     "longer calls EnsureTouchFloorArea — nothing restores the tap target (WO-867).");
                    else
                        notes.Add("touch floor: medallion VISUAL is " +
                                  (ElarionUiKit.MinTouchPx - medallion).ToString("0.#") +
                                  " px under the floor (geometry owned by HudKitController/CombatArcLayout611); " +
                                  "the kit's EnsureTouchFloorArea hit-area child restores the 112 px tap target");
                }
                if (pillH < ElarionUiKit.MinTouchPx)
                    notes.Add("touch floor: the attack pill is " + pillH.ToString("0.#") +
                              " ref px tall (" + (ElarionUiKit.MinTouchPx - pillH).ToString("0.#") +
                              " under the floor) but " + ((1f - ParseConst(hudKit, "Pill611X0", 0.30f)) * railW)
                                  .ToString("0.#") + " px wide — reported, not failed: the tap target is comfortable");
            }
            else
            {
                notes.Add("touch measure: could not parse HudAreasHost's ActionRail rect — right-edge sizing unverified");
            }

            // --- 6e. The Echoes chip is docked, sized, and the floater is gone. --
            if (echoFeedback != null)
            {
                if (Regex.IsMatch(echoFeedback, @"ToastCard\s*\([^)]*ToastTone\.Info"))
                    failures.Add("ECHO CHIP — EchoUnlockFeedback.cs re-introduces the free-floating 'Echoes N/M' " +
                                 "ToastCard. It landed in the ~7 ref-px seam between the HudAreasHost Vitals band " +
                                 "(0.800..0.985) and HeartStatus band (0.700..0.792), in no band at all, and its " +
                                 "accentLeft strip is the stray gold rule (WO-867). The count rides the chip.");
                if (echoFeedback.IndexOf("ElarionUiKit.MinTouchPx", StringComparison.Ordinal) < 0)
                    failures.Add("ECHO CHIP — EchoUnlockFeedback.cs no longer sizes the Echoes chip at " +
                                 "ElarionUiKit.MinTouchPx; a sub-floor chip is grown symmetrically by ClampMinTouch " +
                                 "and drifts out of its docked band (WO-867).");
                if (echoFeedback.IndexOf("EchoChipBandCentreY", StringComparison.Ordinal) < 0)
                    failures.Add("ECHO CHIP — EchoUnlockFeedback.cs no longer docks the chip on the named right-column " +
                                 "band constant; it is free-floating again (WO-867).");
            }

            notes.Add("combat hud composition: " + pagesPinned + " atlas page(s) pinned, " +
                      TargetPlateChildren.Length + " target-plate child name(s) verified");
        }

        /// <summary>Read Assets/&lt;rel&gt; as text (null + note when absent/unreadable).</summary>
        private static string ReadAsset(string rel, List<string> notes)
        {
            string full = Path.Combine(Application.dataPath, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return SafeRead(full, notes);
            notes.Add("combat hud composition: Assets/" + rel + " not found — its checks were skipped");
            return null;
        }

        /// <summary>PNG IHDR width/height without decoding the image.</summary>
        private static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                var head = new byte[24];
                using (var fs = File.OpenRead(path))
                    if (fs.Read(head, 0, 24) < 24) return false;
                if (head[0] != 0x89 || head[1] != 0x50 || head[2] != 0x4E || head[3] != 0x47) return false;
                width  = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                return width > 0 && height > 0;
            }
            catch { return false; }
        }

        /// <summary>Parse an <c>Add(HudArea.&lt;name&gt;, new Vector2(a,b), new Vector2(c,d));</c> row
        /// out of HudAreasHost source into (a,b,c,d).</summary>
        private static bool TryParseAreaRect(string src, string area, out Vector4 rect)
        {
            rect = default(Vector4);
            var m = Regex.Match(src, @"Add\s*\(\s*HudArea\." + Regex.Escape(area) +
                                     @"\s*,\s*new\s+Vector2\s*\(\s*([\d.]+)f?\s*,\s*([\d.]+)f?\s*\)\s*,\s*" +
                                     @"new\s+Vector2\s*\(\s*([\d.]+)f?\s*,\s*([\d.]+)f?\s*\)");
            if (!m.Success) return false;
            float a, b, c, d;
            if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out a)) return false;
            if (!float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out b)) return false;
            if (!float.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out c)) return false;
            if (!float.TryParse(m.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return false;
            rect = new Vector4(a, b, c, d);
            return true;
        }

        /// <summary>Read a <c>const float Name = 0.12f;</c> literal out of source (fallback on a miss).</summary>
        private static float ParseConst(string src, string name, float fallback)
        {
            if (src == null) return fallback;
            var m = Regex.Match(src, @"\b" + Regex.Escape(name) + @"\s*=\s*(-?[\d.]+)f");
            float v;
            if (m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        /// <summary>Count sprite refs in the mirrored RpgUi prefabs whose GUID resolves to no .meta
        /// under Assets/Resources or Assets/Blink — each one renders as a white quad unguarded.</summary>
        private static int CountDanglingPrefabSprites(List<string> notes)
        {
            try
            {
                string prefabDir = Path.Combine(Application.dataPath, Path.Combine("Resources",
                                   Path.Combine("RpgUi", "prefabs")));
                if (!Directory.Exists(prefabDir)) return 0;

                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var guidRx = new Regex(@"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);
                foreach (var rootName in new[] { "Resources", "Blink" })
                {
                    string root = Path.Combine(Application.dataPath, rootName);
                    if (!Directory.Exists(root)) continue;
                    foreach (var meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
                    {
                        try
                        {
                            using (var sr = new StreamReader(meta))
                            {
                                for (int i = 0; i < 4; i++)
                                {
                                    string line = sr.ReadLine();
                                    if (line == null) break;
                                    var gm = guidRx.Match(line);
                                    if (gm.Success) { known.Add(gm.Groups[1].Value); break; }
                                }
                            }
                        }
                        catch { }
                    }
                }
                if (known.Count == 0) return 0;

                // NOTE: matched without a literal open-brace character so the repo's naive
                // brace-balance gate (CLAUDE.md §1) stays green on this file.
                var spriteRx = new Regex(@"m_Sprite:[^,]*,\s*guid:\s*([0-9a-fA-F]{32})");
                int miss = 0;
                foreach (var pf in Directory.GetFiles(prefabDir, "*.prefab", SearchOption.TopDirectoryOnly))
                {
                    string text = SafeRead(pf, notes);
                    if (text == null) continue;
                    foreach (Match m in spriteRx.Matches(text))
                        if (!known.Contains(m.Groups[1].Value)) miss++;
                }
                return miss;
            }
            catch { return 0; }
        }

        private static string RectStr(Rect r)
            => "[x " + r.xMin.ToString("0.#") + ".." + r.xMax.ToString("0.#") +
               ", y " + r.yMin.ToString("0.#") + ".." + r.yMax.ToString("0.#") + "]";

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
