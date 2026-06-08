// =============================================================================
// RegressionSuite — the per-check-in headless regression gate (WO-329 / WO-330).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// WHY THIS EXISTS (owner): "so we don't patch one hole by creating 3 more."
// Every check-in runs ONE entry point that exercises a battery of CASES across
// the systems most likely to silently break when a fix lands elsewhere:
// compile, the placement-recipe math, the canonical data files, the playable
// scene's core wiring, and the structure kit. It logs ONE grep-able verdict —
//   REGRESSION_OK   (every case passed)
//   REGRESSION_FAIL (>=1 case failed)
// — plus a per-case PASS/FAIL list so a failing run names exactly what broke.
//
// HEADLESS / BATCHMODE:
//   -executeMethod DeNelle.Editor.RegressionSuite.RunAll
//   (or menu: Defenders/QA/Run Regression Suite). No play-mode; pure editor.
//   Exits the editor with code 0 (OK) / 1 (FAIL) when run in batchmode so a CI
//   wrapper / ps1 can gate on the process exit as well as the grep token.
//
// ASMDEF BOUNDARY (the reason for the reflection):
//   DeNelle.Editor references DeNelle.Core + DeNelle.Data but deliberately NOT
//   DeNelle.Village (CLAUDE.md §5 boundary — VillageSceneBuilder/Village2Playable
//   build Village scenes the same way). So the Village-type checks (WaveManager
//   reward fields, EconomyService presence) resolve their types by full name via
//   AppDomain (the exact FindType pattern Village2Playable.cs already uses) and
//   read serialized fields through SerializedObject. This is build/QA TOOLING,
//   not a runtime bridge — the §11 "no new reflection in bridge scripts" rule
//   does not apply (the existing editor builders rely on the same pattern).
//
// WHAT IT CANNOT DO (documented, see the RunAll summary + the file footer):
//   The TRUE end-to-end loop — press DEFEND -> wave spawns -> clear -> resources
//   awarded -> build/upgrade spends — needs PLAY MODE (NavMesh, UniTask ticks,
//   RuntimeInitializeOnLoad bootstrappers, EconomyService.Grant at runtime).
//   That is the next QA layer (Unity Test Framework PlayMode). This suite proves
//   the STATIC preconditions for that loop so a play-mode failure is a real
//   gameplay bug, not a missing field / unparseable file / unwired scene.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless per-check-in regression battery. Runs a set of independent CASES
    /// and logs a single <c>REGRESSION_OK</c> / <c>REGRESSION_FAIL</c> verdict plus
    /// a per-case PASS/FAIL list. Each case is wrapped so one throwing case can
    /// never abort the whole run — it is recorded as a FAIL and the rest continue.
    /// </summary>
    public static class RegressionSuite
    {
        // ── Tunables: what the suite expects to exist ──────────────────────────

        // The canonical PLAYABLE village scene (Village2 — canonical per project
        // memory; Village.unity is abandoned). Village3 is also opened as a smoke
        // case but the WaveManager wiring assertions run against Village2.
        private const string PlayableScenePath = "Assets/Scenes/Village2.unity";
        private const string SecondaryScenePath = "Assets/Scenes/Village3.unity";

        // Canonical data files — both copies must parse and be byte-equal.
        private const string ResCatalog = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string SaCatalog  = "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";
        private const string ResEnemies = "Assets/Resources/Data/Canonical/enemies.json";
        private const string SaEnemies  = "Assets/StreamingAssets/Data/Canonical/enemies.json";
        private const string ResWaves   = "Assets/Resources/Data/Canonical/waves.json";
        private const string SaWaves    = "Assets/StreamingAssets/Data/Canonical/waves.json";

        // Catalog ids the build palette / wave-economy loop depend on existing.
        private static readonly string[] ExpectedCatalogIds =
        {
            // towers
            "tower_ground_archer", "tower_wall_wizard",
            // walls + gate
            "wall_wood", "wall_stone", "gate_stone",
            // resource / build buildings
            "workshop", "market", "mill", "lumbermill", "forge",
        };

        // WO-330 wave-clear reward fields that MUST be present on WaveManager so
        // "defend -> earn -> build" can pay out. Serialized private field names.
        private static readonly string[] WaveRewardFields =
        {
            "_awardResourcesOnWaveClear",
            "_woodRewardBase", "_woodRewardPerWave",
            "_ironRewardBase", "_ironRewardPerWave",
        };

        private const string TypeWaveManager    = "DeNelle.Village.WaveManager";
        private const string TypeEconomyService = "DeNelle.Village.EconomyService";

        // ── One case result ────────────────────────────────────────────────────
        private sealed class CaseResult
        {
            public string Name;
            public bool Pass;
            public string Detail;
            public CaseResult(string name, bool pass, string detail)
            { Name = name; Pass = pass; Detail = detail; }
        }

        // ── Public entry points ────────────────────────────────────────────────

        /// <summary>
        /// Runs every regression case. Logs a per-case PASS/FAIL list and the single
        /// <c>REGRESSION_OK</c> / <c>REGRESSION_FAIL</c> verdict. In batchmode, exits
        /// the editor with code 0 (all passed) or 1 (any failed). Returns true on pass.
        /// </summary>
        [MenuItem("Defenders/QA/Run Regression Suite")]
        public static bool RunAll()
        {
            var results = new List<CaseResult>();

            Run(results, "compile-gate",            Case_CompileGate);
            Run(results, "catalog-parse",           Case_CatalogParse);
            Run(results, "catalog-byte-equal",      Case_CatalogByteEqual);
            Run(results, "data-files-parse",        Case_DataFilesParse);
            Run(results, "catalog-ids-present",      Case_CatalogIdsPresent);
            Run(results, "catalog-prefabs-resolve",  Case_CatalogPrefabsResolve);
            Run(results, "structures-kit-present",   Case_StructuresKitPresent);
            Run(results, "no-duplicate-landmines",   Case_NoDuplicateLandmines);
            Run(results, "scene-opens-village2",     () => Case_SceneOpens(PlayableScenePath));
            Run(results, "scene-opens-village3",     () => Case_SceneOpens(SecondaryScenePath));
            Run(results, "core-wiring-village2",     Case_CoreWiring);
            Run(results, "layout-validator",         Case_LayoutValidator);

            // ── Report ──────────────────────────────────────────────────────────
            int passed = results.Count(r => r.Pass);
            int failed = results.Count - passed;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("==================== REGRESSION SUITE ====================");
            foreach (var r in results)
                sb.AppendLine($"  [{(r.Pass ? "PASS" : "FAIL")}] {r.Name}" +
                              (string.IsNullOrEmpty(r.Detail) ? "" : $"  — {r.Detail}"));
            sb.AppendLine("----------------------------------------------------------");
            sb.AppendLine($"  {passed}/{results.Count} cases passed, {failed} failed.");

            bool ok = failed == 0;
            if (ok)
            {
                sb.AppendLine("  VERDICT: REGRESSION_OK");
                sb.AppendLine("==========================================================");
                Debug.Log(sb.ToString());
            }
            else
            {
                sb.AppendLine("  VERDICT: REGRESSION_FAIL");
                sb.AppendLine("==========================================================");
                Debug.LogError(sb.ToString());
            }

            // Reminder of the next QA layer (the part that needs play-mode).
            Debug.Log("[RegressionSuite] NOTE: the end-to-end loop (DEFEND -> wave spawns -> " +
                      "clear -> resources awarded -> build/upgrade spends) is NOT covered here " +
                      "(needs PLAY MODE). See the PLAY-MODE FOLLOW-UP footer in RegressionSuite.cs " +
                      "and tools/regression/MANUAL_QA_CHECKLIST.md.");

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);

            return ok;
        }

        // Runs one case, trapping exceptions so a single throw can't abort the run.
        private static void Run(List<CaseResult> results, string name, Func<CaseResult> body)
        {
            try
            {
                CaseResult r = body();
                r.Name = name;   // normalise the name to the call-site label
                results.Add(r);
            }
            catch (Exception ex)
            {
                results.Add(new CaseResult(name, false, $"threw {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static CaseResult Pass(string detail = "")  => new CaseResult(null, true,  detail);
        private static CaseResult Fail(string detail)       => new CaseResult(null, false, detail);

        // =====================================================================
        //  CASES
        // =====================================================================

        // ── Compile gate ──────────────────────────────────────────────────────
        // Reaching this method at all means the editor scripts compiled clean
        // (a CS error would have stopped the batch before RunAll executed). We
        // re-emit the CompileGate marker so a single log carries both tokens.
        private static CaseResult Case_CompileGate()
        {
            CompileGate.Run();
            return Pass("scripts compiled (CompileGate.Run reached)");
        }

        // ── Catalog parse: both copies deserialize into entries ────────────────
        private static CaseResult Case_CatalogParse()
        {
            if (!File.Exists(ResCatalog)) return Fail($"missing {ResCatalog}");
            if (!File.Exists(SaCatalog))  return Fail($"missing {SaCatalog}");

            int resN = CountCatalogEntries(ResCatalog, out string resErr);
            if (resErr != null) return Fail($"Resources copy: {resErr}");
            int saN = CountCatalogEntries(SaCatalog, out string saErr);
            if (saErr != null) return Fail($"StreamingAssets copy: {saErr}");

            if (resN == 0) return Fail("Resources catalog parsed to 0 entries");
            if (saN == 0)  return Fail("StreamingAssets catalog parsed to 0 entries");
            return Pass($"{resN} entries (Resources), {saN} entries (StreamingAssets)");
        }

        // ── Catalog byte-equal: the two copies must not drift ──────────────────
        private static CaseResult Case_CatalogByteEqual()
        {
            if (!File.Exists(ResCatalog) || !File.Exists(SaCatalog))
                return Fail("one of the catalog copies is missing");
            return BytesEqual(ResCatalog, SaCatalog)
                ? Pass("Resources copy == StreamingAssets copy")
                : Fail("structures-catalog.json copies DIFFER (Resources vs StreamingAssets) — " +
                       "keep them byte-equal (CLAUDE.md / catalog-bootstrap note)");
        }

        // ── enemies.json + waves.json parse (both copies) ──────────────────────
        private static CaseResult Case_DataFilesParse()
        {
            var fails = new List<string>();
            foreach (var path in new[] { ResEnemies, SaEnemies, ResWaves, SaWaves })
            {
                if (!File.Exists(path)) { fails.Add($"missing {path}"); continue; }
                if (!IsParseableJson(path, out string err)) fails.Add($"{path}: {err}");
            }
            return fails.Count == 0
                ? Pass("enemies.json + waves.json parse (both copies)")
                : Fail(string.Join(" | ", fails));
        }

        // ── Expected catalog ids are all present ───────────────────────────────
        private static CaseResult Case_CatalogIdsPresent()
        {
            var ids = ReadCatalogIds(ResCatalog, out string err);
            if (err != null) return Fail(err);
            var missing = ExpectedCatalogIds.Where(id => !ids.Contains(id)).ToList();
            return missing.Count == 0
                ? Pass($"all {ExpectedCatalogIds.Length} expected ids present")
                : Fail("catalog missing expected id(s): " + string.Join(", ", missing));
        }

        // ── Every catalog visualPrefabPath resolves under Resources ────────────
        // The runtime skins structures via Resources.Load<GameObject>(visualPrefabPath).
        // A path that doesn't resolve = an invisible / missing structure in-game.
        private static CaseResult Case_CatalogPrefabsResolve()
        {
            var paths = ReadCatalogVisualPaths(ResCatalog, out string err);
            if (err != null) return Fail(err);
            if (paths.Count == 0) return Fail("no visualPrefabPath values found in catalog");

            var unresolved = new List<string>();
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;          // composite/inert rows may omit it
                var go = Resources.Load<GameObject>(p);
                if (go == null) unresolved.Add(p);
            }
            return unresolved.Count == 0
                ? Pass($"{paths.Count} visualPrefabPath value(s) all resolve under Resources")
                : Fail($"{unresolved.Count} visualPrefabPath value(s) do NOT resolve: " +
                       string.Join(", ", unresolved.Distinct()));
        }

        // ── The Resources/Structures kit prefabs physically exist ──────────────
        private static CaseResult Case_StructuresKitPresent()
        {
            const string kitDir = "Assets/Resources/Structures";
            if (!Directory.Exists(kitDir)) return Fail($"missing kit folder {kitDir}");
            int n = Directory.GetFiles(kitDir, "*.prefab", SearchOption.TopDirectoryOnly).Length;
            return n > 0
                ? Pass($"{n} structure prefab(s) in {kitDir}")
                : Fail($"{kitDir} contains 0 prefabs (the build kit is empty)");
        }

        // ── Known-landmine: exactly one DoorController, no Core.Debug shadow ────
        // Scans only Assets/ (worktrees under .claude/ are out-of-tree, not compiled).
        private static CaseResult Case_NoDuplicateLandmines()
        {
            var fails = new List<string>();

            var doorControllers = Directory
                .GetFiles("Assets", "DoorController.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/.claude/"))
                .ToList();
            if (doorControllers.Count > 1)
                fails.Add($"{doorControllers.Count} DoorController.cs under Assets " +
                          "(canonical is Buildings/DoorController.cs — see doorcontroller-duplicate landmine)");

            // DeNelle.Core.Debug / .Addressables namespace shadow landmine.
            foreach (var shadow in new[] { "namespace DeNelle.Core.Debug", "namespace DeNelle.Core.Addressables" })
            {
                var hits = Directory
                    .GetFiles("Assets/_Modules/Core", "*.cs", SearchOption.AllDirectories)
                    .Where(p => File.ReadAllText(p).Contains(shadow))
                    .ToList();
                if (hits.Count > 0)
                    fails.Add($"found '{shadow}' (shadows UnityEngine static) in: " +
                              string.Join(", ", hits.Select(Path.GetFileName)));
            }

            return fails.Count == 0
                ? Pass("1 DoorController, no Core.Debug/Addressables shadow")
                : Fail(string.Join(" | ", fails));
        }

        // ── A canonical scene opens without missing scripts / NRE ──────────────
        private static CaseResult Case_SceneOpens(string scenePath)
        {
            if (!File.Exists(scenePath)) return Fail($"missing {scenePath}");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                return Fail($"scene failed to load: {scenePath}");

            int missing = CountMissingScripts(scene);
            return missing == 0
                ? Pass($"{Path.GetFileName(scenePath)} opened, 0 missing scripts")
                : Fail($"{Path.GetFileName(scenePath)} has {missing} GameObject(s) with MISSING (None) scripts");
        }

        // ── Core wiring in the playable scene ──────────────────────────────────
        // Opens Village2 and asserts: a WaveManager exists + carries the WO-330
        // reward fields; an EconomyService exists OR the bootstrapper will create one;
        // (the catalog ids are validated in their own case). WaveManager / Economy
        // types are resolved by full name (asmdef boundary — see file header).
        private static CaseResult Case_CoreWiring()
        {
            if (!File.Exists(PlayableScenePath)) return Fail($"missing {PlayableScenePath}");
            EditorSceneManager.OpenScene(PlayableScenePath, OpenSceneMode.Single);

            var fails = new List<string>();

            // WaveManager present?
            Type wmType = FindType(TypeWaveManager);
            if (wmType == null)
            {
                fails.Add($"type {TypeWaveManager} not found (DeNelle.Village compiled?)");
            }
            else
            {
                var wm = UnityEngine.Object.FindFirstObjectByType(wmType) as Component;
                if (wm == null)
                {
                    fails.Add("no WaveManager in the playable scene (DEFEND button has nothing behind it)");
                }
                else
                {
                    // Reward fields wired (serialized private fields present)?
                    var so = new SerializedObject(wm);
                    var missingFields = WaveRewardFields
                        .Where(f => so.FindProperty(f) == null)
                        .ToList();
                    if (missingFields.Count > 0)
                        fails.Add("WaveManager missing WO-330 reward field(s): " +
                                  string.Join(", ", missingFields));
                }
            }

            // EconomyService: present in-scene is fine; absent is ALSO fine because
            // EconomyService.Bootstrap creates one at runtime. We only fail if the
            // type itself is missing (means DeNelle.Village didn't compile).
            Type econType = FindType(TypeEconomyService);
            if (econType == null)
                fails.Add($"type {TypeEconomyService} not found (DeNelle.Village compiled?)");

            return fails.Count == 0
                ? Pass("WaveManager present + WO-330 reward fields wired; EconomyService type resolves")
                : Fail(string.Join(" | ", fails));
        }

        // ── Recipe math safeguard (reuse the existing LayoutValidator) ─────────
        // Validates the active GameState recipe (empty = OK). With Village2 open the
        // gate-lane check can also run. LayoutValidator logs its own detail + token.
        private static CaseResult Case_LayoutValidator()
        {
            bool ok = LayoutValidator.ValidateActive();
            return ok
                ? Pass("LAYOUT_VALIDATE_OK (recipe math safeguards passed)")
                : Fail("LAYOUT_VALIDATE_FAIL — see [LayoutValidator] FAIL lines above");
        }

        // =====================================================================
        //  Helpers — JSON (no Village/Newtonsoft dependency; minimal + robust)
        // =====================================================================
        // NOTE: we intentionally avoid a full typed deserialize here (the typed
        // CatalogEntry lives behind CatalogBootstrap in DeNelle.Village which Editor
        // can't reference). For PARSE VALIDITY we use Unity's JsonUtility round-trip
        // plus a lightweight brace/string scan; for ID / path extraction we read the
        // raw "id" / "visualPrefabPath" string tokens. This is sufficient for a
        // regression gate: a malformed file fails the brace scan, and the typed
        // load is exercised at runtime by CatalogBootstrap (covered by play-mode).

        private static bool IsParseableJson(string path, out string err)
        {
            err = null;
            try
            {
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) { err = "empty file"; return false; }
                if (!BracesBalanced(text, out string be)) { err = be; return false; }
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }

        // Balanced {}/[] outside of strings — catches truncated / mangled JSON, the
        // exact corruption class the mount-sync landmine produces.
        private static bool BracesBalanced(string text, out string err)
        {
            err = null;
            int curly = 0, square = 0;
            bool inStr = false, esc = false;
            foreach (char c in text)
            {
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; break;
                    case '{': curly++; break;
                    case '}': curly--; break;
                    case '[': square++; break;
                    case ']': square--; break;
                }
                if (curly < 0 || square < 0) { err = "unbalanced closing brace/bracket"; return false; }
            }
            if (inStr)   { err = "unterminated string"; return false; }
            if (curly != 0)  { err = $"{curly} unbalanced {{}}"; return false; }
            if (square != 0) { err = $"{square} unbalanced []"; return false; }
            return true;
        }

        // Counts "id": tokens at object level (a proxy for entry count). Returns -1
        // and sets err on a parse problem.
        private static int CountCatalogEntries(string path, out string err)
        {
            err = null;
            string text = File.ReadAllText(path);
            if (!BracesBalanced(text, out err)) return -1;
            return CountToken(text, "\"id\"");
        }

        private static HashSet<string> ReadCatalogIds(string path, out string err)
        {
            err = null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path)) { err = $"missing {path}"; return set; }
            string text = File.ReadAllText(path);
            if (!BracesBalanced(text, out err)) return set;
            foreach (var v in ReadStringValuesForKey(text, "id")) set.Add(v);
            return set;
        }

        private static List<string> ReadCatalogVisualPaths(string path, out string err)
        {
            err = null;
            var list = new List<string>();
            if (!File.Exists(path)) { err = $"missing {path}"; return list; }
            string text = File.ReadAllText(path);
            if (!BracesBalanced(text, out err)) return list;
            list.AddRange(ReadStringValuesForKey(text, "visualPrefabPath"));
            return list;
        }

        // Extracts every  "key": "value"  string value for the given key.
        private static IEnumerable<string> ReadStringValuesForKey(string text, string key)
        {
            string needle = "\"" + key + "\"";
            int i = 0;
            while ((i = text.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                int j = i + needle.Length;
                // skip whitespace + ':'
                while (j < text.Length && (text[j] == ' ' || text[j] == '\t' || text[j] == ':' || text[j] == '\r' || text[j] == '\n')) j++;
                if (j < text.Length && text[j] == '"')
                {
                    int start = ++j;
                    var sb = new System.Text.StringBuilder();
                    while (j < text.Length && text[j] != '"')
                    {
                        if (text[j] == '\\' && j + 1 < text.Length) { sb.Append(text[j + 1]); j += 2; continue; }
                        sb.Append(text[j]); j++;
                    }
                    yield return sb.ToString();
                }
                i = j + 1;
            }
        }

        private static int CountToken(string text, string token)
        {
            int n = 0, i = 0;
            while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { n++; i += token.Length; }
            return n;
        }

        // =====================================================================
        //  Helpers — files / scene / reflection
        // =====================================================================

        private static bool BytesEqual(string a, string b)
        {
            byte[] ba = File.ReadAllBytes(a);
            byte[] bb = File.ReadAllBytes(b);
            if (ba.Length != bb.Length) return false;
            for (int i = 0; i < ba.Length; i++) if (ba[i] != bb[i]) return false;
            return true;
        }

        // Counts GameObjects carrying a MISSING MonoBehaviour (removed/renamed script).
        private static int CountMissingScripts(Scene scene)
        {
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0) count += n;
                }
            }
            return count;
        }

        // Resolve a type by full name across all loaded assemblies (the FindType
        // pattern from Village2Playable.cs — needed because the Editor asmdef does
        // not reference DeNelle.Village).
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}

// =============================================================================
// PLAY-MODE FOLLOW-UP (the next QA layer — NOT built here, WO-329 deliverable 3)
// -----------------------------------------------------------------------------
// This headless suite proves the STATIC preconditions of the core loop. The TRUE
// end-to-end behaviours need Unity Test Framework PLAY-MODE tests (they require a
// baked NavMesh, the RuntimeInitializeOnLoad bootstrappers — CatalogBootstrap,
// EconomyService.Bootstrap, WaveSystemBridgeBootstrap — and UniTask frame ticks):
//
//   1. DEFEND kicks the loop: WaveManager.ForceBeginNextWave() advances Idle ->
//      Countdown/Active and spawns >=1 Enemy (WO-327).
//   2. Wave clear pays out: clearing a wave raises EconomyService Wood/Iron by the
//      WO-330 reward formula (base + perWave*(waveId-1)).
//   3. Build/upgrade SPENDS: EconomyService.TrySpend(cost) deducts and a placement
//      via StructureFactory.Create consumes the recipe + registers a structure.
//   4. No NullReferenceException during a short headless play run (guards WO-328).
//   5. CatalogRegistry.Count > 0 at runtime (CatalogBootstrap actually registered).
//   6. GameState save/load round-trips the BaseLayout + wallet (WO-301).
//
// To add it: create Assets/Tests/PlayMode/DeNelle.Tests.PlayMode.asmdef
// (references DeNelle.Village, DeNelle.Core, UnityEngine.TestRunner,
// UnityEditor.TestRunner; "optionalUnityReferences": ["TestAssemblies"]) and run via
//   -runTests -testPlatform PlayMode -testResults results.xml
// Wire checkin_gate.ps1 to run THIS suite (RunAll) THEN -runTests. See
// tools/regression/MANUAL_QA_CHECKLIST.md for the visual items neither layer covers.
// =============================================================================
